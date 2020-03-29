using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
	/// <summary>
	/// Represents a coroutine.
	/// </summary>
	public sealed partial class Coroutine : IDisposable
	{
		private readonly CoroutineYieldAwaitable _yieldAwaiter = new CoroutineYieldAwaitable();
		private Task<object> _task;
		private Action _continuation;

		/// <summary>
		/// Initializes a new <see cref="Coroutine"/> with the specified coroutine task producer.
		/// </summary>
		/// <param name="taskProducer">A producer that kicks off the coroutine task and returns it.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="taskProducer"/> is <c>null</c>.</exception>
		public Coroutine(Func<Task> taskProducer) : this(async () => { await taskProducer(); return null; })
		{
		}

		/// <summary>
		/// Initializes a new <see cref="Coroutine"/> with the specified coroutine task producer.
		/// </summary>
		/// <param name="taskProducer">A producer that kicks off the coroutine task and returns it. The result returned by the task is saved in <see cref="Result"/> when the coroutine finishes.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="taskProducer"/> is <c>null</c>.</exception>
		public Coroutine(Func<Task<object>> taskProducer)
		{
			if (taskProducer == null)
				throw new ArgumentNullException(nameof(taskProducer));

			_continuation = () =>
			{
				Task<object> task;
				try
				{
					task = taskProducer();
				}
				catch (Exception ex)
				{
					throw SetException(new CoroutineUnhandledException("Exception was thrown by root coroutine task producer", ex));
				}

				if (task == null)
				{
					throw SetException(new CoroutineBehaviorException("The root coroutine task producer returned null"));
				}

				_task = task;
			};

			Status = CoroutineStatus.Runnable;
		}

		[ThreadStatic]
		private static Coroutine _currentCoroutine;
		/// <summary>
		/// Gets the currently executing coroutine on this thread, or null, if not executing in a coroutine.
		/// </summary>
		public static Coroutine Current => _currentCoroutine;

		/// <summary>
		/// Gets the result returned by the original task.
		/// </summary>
		/// <remarks>Only valid when <see cref="Status"/> is <see cref="CoroutineStatus.RanToCompletion"/>.</remarks>
		public object Result { get; private set; }

		/// <summary>
		/// Gets the status this coroutine is currently in.
		/// </summary>
		public CoroutineStatus Status { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this coroutine has been disposed of.
		/// </summary>
		public bool IsDisposed { get; private set; }

		/// <summary>
		/// Gets the exception that was thrown when this coroutine faulted.
		/// </summary>
		/// <remarks>This can be a <see cref="CoroutineUnhandledException"/> or a <see cref="CoroutineBehaviorException"/>.</remarks>
		public CoroutineException FaultingException { get; private set; }

		/// <summary>
		/// Gets a bool that indicates whether the coroutine is finished
		/// </summary>
		public bool IsFinished => Status == CoroutineStatus.RanToCompletion || Status == CoroutineStatus.Faulted || Status == CoroutineStatus.Stopped;

		/// <summary>
		/// Gets the amount of times the coroutine has been resumed/ticked.
		/// </summary>
		public int Ticks { get; private set; }

		/// <summary>
		/// Resumes the coroutine.
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if the coroutine is disposed.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the coroutine cannot be resumed because it has finished running; see <see cref="IsFinished"/>. Also thrown if a coroutine tries to resume itself.</exception>
		/// <exception cref="CoroutineUnhandledException">Thrown if the coroutine throws an exception. For the exception thrown by the coroutine, see <see cref="Exception.InnerException"/>.</exception>
		/// <exception cref="CoroutineBehaviorException">
		/// <para>Thrown if the code being executed by this <see cref="Coroutine"/> class behaves in an unexpected way.</para>
		/// <para>There are several situations where this exception is thrown. They are listed below.</para>
		/// <list type="bullet">
		/// <item><description>The root coroutine task producer (that is, the task producer passed to the constructor of this class) returns <c>null</c>.</description></item>
		/// <item><description>The coroutine awaits an external task. The coroutine should only await tasks from the <see cref="Coroutine"/> class, or other tasks that only await tasks from the <see cref="Coroutine"/> class.</description></item>
		/// <item><description>The coroutine creates multiple tasks without awaiting them. Coroutines should always immediately await the tasks they create.</description></item>
		/// </list>
		/// </exception>
		public void Resume()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (IsFinished)
				throw new InvalidOperationException("This coroutine has finished execution and cannot be resumed.");

			if (_currentCoroutine == this)
				throw new InvalidOperationException("A coroutine cannot resume itself");

			Resume(false);
		}

		private void Resume(bool forStop)
		{
			SynchronizationContext oldSyncContext = SynchronizationContext.Current;
			Coroutine oldCoroutine = _currentCoroutine;

			try
			{
				_currentCoroutine = this;

				// When using a sync context, a lot of continuations are posted back to it
				// We can avoid that by nulling it out during the continuation - this way continuations will be inlined
				// on Task.
				SynchronizationContext.SetSynchronizationContext(null);

				Action continuation = _continuation;
				// Mark that there is no continuation. This is important so we don't throw "multiple continuations" errors
				// from Coroutine.YieldAwaiter.
				_continuation = null;
				continuation();

				if (!forStop)
					Ticks++;

				CheckPostConditions(forStop);
			}
			finally
			{
				_currentCoroutine = oldCoroutine;
				SynchronizationContext.SetSynchronizationContext(oldSyncContext);
			}
		}

		private Exception SetException(CoroutineException ex)
		{
			Status = CoroutineStatus.Faulted;
			FaultingException = ex;
			return ex;
		}

		private void CheckPostConditions(bool shouldBeCanceled)
		{
			switch (_task.Status)
			{
				case TaskStatus.RanToCompletion:
					if (_continuation != null)
					{
						throw SetException(
							new CoroutineBehaviorException("The coroutine finished with a continuation queued. This is usually an indication that a task was created but not awaited."));
					}

					Status = CoroutineStatus.RanToCompletion;
					Result = _task.Result;
					break;
				case TaskStatus.WaitingForActivation:
					// Were we supposed to be canceled now?
					if (shouldBeCanceled)
					{
						// Coroutine cannot be unwound. Notify devs..
						throw SetException(
							new CoroutineBehaviorException("The coroutine could not successfully be disposed of. This is usually an indication that the CoroutineStoppedException was caught."));
					}

					// If no continuation are queued, something went wrong
					// The only time there should be no continuation is when
					// the task finishes. The TaskStatus would be RanToCompletion then.
					// This happens if the coroutine task awaits something that isn't inlined, for example an external task.
					if (_continuation == null)
					{
						// Coroutine will be unwound by external task, hopefully

						throw SetException(new CoroutineBehaviorException(
							"No continuation was queued and coroutine didn't finish. This is usually an indication that an external task was awaited, which is not supported by coroutines."));
					}

					break;
				case TaskStatus.Faulted:
					Debug.Assert(_task.Exception != null);
					Exception exception = _task.Exception.InnerExceptions.FirstOrDefault();

					if (exception is CoroutineStoppedException)
						Status = CoroutineStatus.Stopped; // Do not throw any exception here, simply mark that we are unwound
					else
						throw SetException(new CoroutineUnhandledException("Exception was thrown by coroutine", exception));

					break;
				case TaskStatus.Canceled:
					try
					{
						_task.WaitOrUnwrap(0);
						throw SetException(new CoroutineBehaviorException("Coroutine was canceled without any exceptions"));
					}
					catch (Exception ex)
					{
						throw SetException(new CoroutineUnhandledException("Exception was thrown by coroutine", ex));
					}
				default:
					throw SetException(new CoroutineBehaviorException("Unexpected task status " + _task.Status));
			}
		}

		/// <summary>
		/// Disposes this coroutine.
		/// </summary>
		/// <exception cref="CoroutineBehaviorException">Thrown if the coroutine being disposed of catches the <see cref="CoroutineStoppedException"/> thrown.</exception>
		/// <exception cref="CoroutineStoppedException">Thrown if the coroutine being disposed of is the current coroutine. This exception is expected and handled by the coroutine framework, and should therefore not be caught.</exception>
		/// <remarks>
		/// <para>Disposing a coroutine before it has finished running is a complicated process which requires unwinding the coroutine's tasks to make sure any finally blocks are executed.</para>
		/// </remarks>
		/// <example>
		/// <para>The following example demonstrates when exceptions are thrown by this function, and when they are not.</para>
		/// <code>
		/// private static async Task Example1()
		/// {
		///		// Proper unwinding. Exception is thrown by Dispose to facilitate the unwinding.
		///		try
		///		{
		///			await DisposeCurrent();
		///			await Coroutine.Yield(); // This line is never hit
		///		}
		///		finally
		///		{
		///			// The finally block is executed as a result of the exception being thrown
		///			// Coroutine is properly unwound
		///		}
		/// }
		/// 
		/// private static async Task DisposeCurrent()
		/// {
		///		await Coroutine.Yield();
		///		Coroutine.Current.Dispose(); // CoroutineStoppedException is thrown here. This exception is expected and should not be caught.
		/// }
		/// 
		/// private static async Task Example2()
		/// {
		///		// Proper unwinding. No exception is thrown by Dispose
		///		try
		///		{
		///			await Coroutine.Yield();
		///			await Coroutine.Yield();
		///		}
		///		finally
		///		{
		///			// The finally block is executed as a result of 'Coroutine.Yield' throwing the CoroutineStoppedException
		///			// Coroutine is properly unwound
		///		}
		/// }
		/// 
		/// private static async Task Example3()
		/// {
		///		try
		///		{
		///			try
		///			{
		///				await Coroutine.Yield();
		///			}
		///			catch (CoroutineStoppedException)
		///			{
		///			}
		/// 
		///			await Coroutine.Yield();
		///		}
		///		finally
		///		{
		///			// This finally block is never executed. Dispose throws a CoroutineBehaviorException.
		///		}
		/// }
		/// private static void Main()
		/// {
		///		Coroutine coroutine = new Coroutine(() => Example1());
		///		coroutine.Resume();
		///		coroutine.Resume();
		///		// coroutine.IsFinished == true, coroutine.Status == CoroutineStatus.Stopped
		/// 
		///		coroutine = new Coroutine(() => Example2());
		///		coroutine.Resume();
		///		coroutine.Dispose();
		///		// coroutine.IsFinished == true, coroutine.Status == CoroutineStatus.Stopped
		/// 
		///		coroutine = new Coroutine(() => Example3());
		///		coroutine.Resume();
		///		coroutine.Dispose(); // CoroutineBehaviorException is thrown here
		/// }
		/// </code>
		/// </example>
		public void Dispose()
		{
			if (IsDisposed)
				return;

			if (Current == this)
			{
				IsDisposed = true;
				throw CanceledException();
			}

			if (Status == CoroutineStatus.Runnable)
			{
				if (_task != null)
				{
					// Make the yield awaiter know that the coroutine should unwind..
					_yieldAwaiter.Canceled = true;
					// As we resume by calling the next continuation, GetResult() will be invoked on the yield awaiter, which will throw an exception
					// this exception will propagate and unwind the coroutine
					Resume(true);
				}
				else
				{
					// Disposing right after constructing, ie.
					// Coroutine cr = new Coroutine(() => SomeTask());
					// cr.Dispose();
					_continuation = null;
				}
			}

			IsDisposed = true;
		}

		/// <summary>
		/// Gets a string representation of this coroutine.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			switch (Status)
			{
				case CoroutineStatus.Faulted:
				{
					if (FaultingException != null)
						return "Faulted with exception " + FaultingException.InnerException.GetType();

					return "Faulted";
				}
				case CoroutineStatus.RanToCompletion:
				{
					if (Result != null)
						return "Ran to completion. Result: " + Result;

					return "Ran to completion";
				}
				case CoroutineStatus.Runnable:
				{
					return "Runnable";
				}
				case CoroutineStatus.Stopped:
				{
					return "Stopped";
				}
				default:
					return "Invalid state";
			}
		}

		internal static Exception CanceledException()
		{
			return new CoroutineStoppedException("Coroutine was stopped");
		}
	}
}