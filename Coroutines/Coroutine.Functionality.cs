using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Coroutines
{
	public partial class Coroutine
	{
		/// <summary>
		/// Ensures the current function is executing in a coroutine and raises an exception if not.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		private static void CheckObtainCoroutineTask()
		{
			Coroutine coroutine = _currentCoroutine;
			if (coroutine == null)
				throw new InvalidOperationException("This function can only be used from within a coroutine");

			if (coroutine._continuation != null)
				throw new InvalidOperationException(
					"Cannot obtain multiple coroutine tasks in a single coroutine tick. This is usually an indication that a coroutine task was created but not awaited.");
		}

		private static async Task InternalYield()
		{
			await Current._yieldAwaiter;
		}

		/// <summary>
		/// Yields back to the coroutine, executing the rest of the current function in the next tick.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>The coroutine task.</returns>
		public static Task Yield()
		{
			CheckObtainCoroutineTask();

			return InternalYield();
		}

		private static async Task InternalSleep(TimeSpan timeout)
		{
			if (timeout == Timeout.InfiniteTimeSpan)
			{
				while (true)
					await Current._yieldAwaiter;
			}

			Stopwatch timer = Stopwatch.StartNew();
			do
			{
				await Current._yieldAwaiter;
			} while (timer.Elapsed < timeout);
		}

		/// <summary>
		/// Gets a coroutine task that sleeps for the specified <paramref name="timeout"/>.
		/// </summary>
		/// <param name="timeout">The timeout to sleep.</param>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is negative and not equal to <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
		/// <returns>The coroutine task.</returns>
		public static Task Sleep(TimeSpan timeout)
		{
			if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
				throw new ArgumentOutOfRangeException(nameof(timeout));

			CheckObtainCoroutineTask();

			return InternalSleep(timeout);
		}

		/// <summary>
		/// Gets a coroutine task that sleeps for the specified amount of <paramref name="milliseconds"/>.
		/// </summary>
		/// <param name="milliseconds">The amount of milliseconds to sleep.</param>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="milliseconds"/> is negative and not equal to <see cref="Timeout.Infinite"/>.</exception>
		/// <returns>The coroutine task.</returns>
		public static Task Sleep(int milliseconds)
		{
			if (milliseconds < 0 && milliseconds != Timeout.Infinite)
				throw new ArgumentOutOfRangeException(nameof(milliseconds));

			return Sleep(new TimeSpan(0, 0, 0, 0, milliseconds));
		}

		private static async Task<bool> InternalWait(TimeSpan maxTimeout, Func<bool> condition)
		{
			if (maxTimeout == TimeSpan.Zero)
			{
				return condition();
			}

			if (maxTimeout == Timeout.InfiniteTimeSpan)
			{
				// ReSharper disable once LoopVariableIsNeverChangedInsideLoop
				while (!condition())
					await Current._yieldAwaiter;

				return true;
			}

			Stopwatch timer = Stopwatch.StartNew();
			do
			{
				if (condition())
					return true;

				await Current._yieldAwaiter;
			} while (timer.Elapsed < maxTimeout);

			return false;
		}

		/// <summary>Gets a coroutine task that waits for the specified <paramref name="condition"/> to become true, for up to the specified max time.
		/// Returns <c>true</c> if the <paramref name="condition"/> becomes <c>true</c> before the max wait time is over.</summary>
		/// <param name="maxWaitTimeout">The max time to wait, or <c>Timeout.InfiniteTimeSpan</c> for an infinite wait.</param>
		/// <param name="condition">The condition.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWaitTimeout"/> is negative and not equal to <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>A coroutine task that returns true if <paramref name="condition"/> evaluates to true during the timeout period; otherwise false.</returns>
		public static Task<bool> Wait(TimeSpan maxWaitTimeout, Func<bool> condition)
		{
			if (maxWaitTimeout < TimeSpan.Zero && maxWaitTimeout != Timeout.InfiniteTimeSpan)
				throw new ArgumentOutOfRangeException(nameof(maxWaitTimeout), "Cannot wait for a negative timespan");

			if (condition == null)
				throw new ArgumentNullException(nameof(condition));

			CheckObtainCoroutineTask();

			return InternalWait(maxWaitTimeout, condition);
		}

		/// <summary>Gets a coroutine task that waits for the specified <paramref name="condition"/> to become true, for up to the specified max time.
		/// Returns <c>true</c> if the <paramref name="condition"/> becomes <c>true</c> before the max wait time is over.</summary>
		/// <param name="maxWaitMs">The max time to wait, in milliseconds, or <c>Timeout.Infinite</c> for an infinite wait.</param>
		/// <param name="condition">The condition.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWaitMs"/> is negative and not equal to <see cref="Timeout.Infinite"/>.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>A coroutine task that returns true if <paramref name="condition"/> evaluates to true during the timeout period; otherwise false.</returns>
		public static Task<bool> Wait(int maxWaitMs, Func<bool> condition)
		{
			if (maxWaitMs < 0 && maxWaitMs != Timeout.Infinite)
				throw new ArgumentOutOfRangeException(nameof(maxWaitMs), "Cannot wait for a negative amount of time");

			return Wait(new TimeSpan(0, 0, 0, 0, maxWaitMs), condition);
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of a produced external task.
		/// </summary>
		/// <param name="taskProducer">A function that constructs the external task.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="taskProducer"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown if <paramref name="taskProducer"/> produces a <c>null</c> value.</exception>
		/// <remarks>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</remarks>
		public static Task ExternalTask(Func<Task> taskProducer)
		{
			if (taskProducer == null)
				throw new ArgumentNullException(nameof(taskProducer));

			Task task = taskProducer();
			if (task == null)
				throw new ArgumentException("The task producer returned null", nameof(taskProducer));

			return ExternalTask(task);
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <param name="externalTask">The external task.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <remarks>
		/// <para>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</para>
		/// <para>
		/// If the external task throws an exception while the coroutine is running, the exception will be propagated.
		/// If the coroutine stops before the external task throws an exception, the external task will not have its exception
		/// observed by this method. For that reason, the external task should not throw exceptions. The overload that takes a producer
		/// allows for easy exception handling of an external task.
		/// </para>
		/// </remarks>
		public static Task ExternalTask(Task externalTask)
		{
			return ExternalTaskCore(externalTask, Timeout.InfiniteTimeSpan);
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <param name="externalTask">The external task.</param>
		/// <param name="timeout">The max time to wait for the external task to complete.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is negative (and not equal to <see cref="Timeout.InfiniteTimeSpan"/>).</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns><c>true</c> if the external task completed within <paramref name="timeout"/>; otherwise <c>false</c>.</returns>
		/// <remarks>
		/// <para>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</para>
		/// <para>
		/// If the external task throws an exception while the coroutine is running, the exception will be propagated.
		/// If the coroutine stops before the external task throws an exception, the external task will not have its exception
		/// observed by this method. For that reason, the external task should not throw exceptions. The overload that takes a producer
		/// allows for easy exception handling of an external task.
		/// </para>
		/// </remarks>
		[Obsolete("Timeouts should be handled in the external task, not by this method. Use the overloads with infinite timeouts instead.")]
		public static Task<bool> ExternalTask(Task externalTask, TimeSpan timeout)
		{
			return ExternalTaskCore(externalTask, timeout);
		}


		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <param name="externalTask">The external task.</param>
		/// <param name="millisecondsTimeout">The max time to wait for the external task to complete, in milliseconds.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="millisecondsTimeout"/> is negative (and not equal to <see cref="Timeout.Infinite"/>).</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns><c>true</c> if the external task completed within <paramref name="millisecondsTimeout"/>; otherwise <c>false</c>.</returns>
		/// <remarks>
		/// <para>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</para>
		/// <para>
		/// If the external task throws an exception while the coroutine is running, the exception will be propagated.
		/// If the coroutine stops before the external task throws an exception, the external task will not have its exception
		/// observed by this method. For that reason, the external task should not throw exceptions. The overload that takes a producer
		/// allows for easy exception handling of an external task.
		/// </para>
		/// </remarks>
		[Obsolete("Timeouts should be handled in the external task, not by this method. Use the overloads with infinite timeouts instead.")]
		public static Task<bool> ExternalTask(Task externalTask, int millisecondsTimeout)
		{
			if (millisecondsTimeout != Timeout.Infinite && millisecondsTimeout < 0)
				throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), "Timeout cannot be negative");

			return ExternalTaskCore(externalTask, new TimeSpan(0, 0, 0, 0, millisecondsTimeout));
		}

		private static Task<bool> ExternalTaskCore(Task externalTask, TimeSpan timeout)
		{
			if (externalTask == null)
				throw new ArgumentNullException(nameof(externalTask));

			if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
				throw new ArgumentOutOfRangeException(nameof(timeout));

			CheckObtainCoroutineTask();

			return InternalWaitForExternalTask(externalTask, timeout);
		}

		private static async Task<bool> InternalWaitForExternalTask(Task externalTask, TimeSpan timeout)
		{
			if (timeout == TimeSpan.Zero)
				return externalTask.WaitOrUnwrap(0);

			if (timeout == Timeout.InfiniteTimeSpan)
			{
				while (!externalTask.WaitOrUnwrap(0))
					await Current._yieldAwaiter;

				return true;
			}

			Stopwatch timer = Stopwatch.StartNew();
			do
			{
				if (externalTask.WaitOrUnwrap(0))
					return true;

				await Current._yieldAwaiter;
			} while (timer.Elapsed < timeout);

			return false;
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of a produced external task.
		/// </summary>
		/// <param name="taskProducer">A function that constructs the external task.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="taskProducer"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown if <paramref name="taskProducer"/> produces a <c>null</c> value.</exception>
		/// <return>The result of the external task.</return>
		/// <remarks>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</remarks>
		public static Task<T> ExternalTask<T>(Func<Task<T>> taskProducer)
		{
			if (taskProducer == null)
				throw new ArgumentNullException(nameof(taskProducer));

			Task<T> task = taskProducer();
			if (task == null)
				throw new ArgumentException("The task producer returned null", nameof(taskProducer));

			return ExternalTask(task);
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <typeparam name="T">The return type of the external task.</typeparam>
		/// <param name="externalTask">The external task.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>The result of the external task.</returns>
		/// <remarks>
		/// <para>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</para>
		/// <para>
		/// If the external task throws an exception while the coroutine is running, the exception will be propagated.
		/// If the coroutine stops before the external task throws an exception, the external task will not have its exception
		/// observed by this method. For that reason, the external task should not throw exceptions. The overload that takes a producer
		/// allows for easy exception handling of an external task.
		/// </para>
		/// </remarks>
		public static Task<T> ExternalTask<T>(Task<T> externalTask)
		{
			Task<ExternalTaskWaitResult<T>> task = ExternalTaskCore(externalTask, Timeout.InfiniteTimeSpan);
			return ExtractWaitResult(task);
		}

		private static async Task<T> ExtractWaitResult<T>(Task<ExternalTaskWaitResult<T>> task)
		{
			var result = await task;
			return result.Result;
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <typeparam name="T">The return type of the external task.</typeparam>
		/// <param name="externalTask">The external task.</param>
		/// <param name="millisecondsTimeout">The max time to wait for the external task to complete, in milliseconds.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="millisecondsTimeout"/> is negative (and not equal to <see cref="Timeout.Infinite"/>).</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>The result of the wait which indicates whether the external task timed out or not, and if not, the actual result.</returns>
		/// <remarks>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</remarks>
		[Obsolete("Timeouts should be handled in the external task, not by this method. Use the overloads with infinite timeouts instead.")]
		public static Task<ExternalTaskWaitResult<T>> ExternalTask<T>(Task<T> externalTask, int millisecondsTimeout)
		{
			if (millisecondsTimeout < 0 && millisecondsTimeout != Timeout.Infinite)
				throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), "Timeout cannot be negative");

			return ExternalTaskCore(externalTask, new TimeSpan(0, 0, 0, 0, millisecondsTimeout));
		}

		/// <summary>
		/// Gets a coroutine task that waits for completion of an external task (a task not running as a coroutine).
		/// </summary>
		/// <typeparam name="T">The return type of the external task.</typeparam>
		/// <param name="externalTask">The external task.</param>
		/// <param name="timeout">The max time to wait for the external task to complete.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="externalTask"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is negative (and not equal to <see cref="Timeout.InfiniteTimeSpan"/>).</exception>
		/// <exception cref="InvalidOperationException">Thrown if the function is not executing in a coroutine, or if it has already obtained a coroutine task this tick.</exception>
		/// <returns>The result of the wait which indicates whether the external task timed out or not, and if not, the actual result.</returns>
		/// <remarks>Do not pass a coroutine task to this function. Doing so will result in possible dead locks and exceptions.</remarks>
		[Obsolete("Timeouts should be handled in the external taks, not by this method. Use the overloads with infinite timeouts instead.")]
		public static Task<ExternalTaskWaitResult<T>> ExternalTask<T>(Task<T> externalTask, TimeSpan timeout)
		{
			return ExternalTaskCore(externalTask, timeout);
		}

		private static Task<ExternalTaskWaitResult<T>> ExternalTaskCore<T>(Task<T> externalTask, TimeSpan timeout)
		{
			if (externalTask == null)
				throw new ArgumentNullException(nameof(externalTask));

			if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
				throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative");

			CheckObtainCoroutineTask();

			return InternalWaitForExternalTask(externalTask, timeout);
		}

		private static async Task<ExternalTaskWaitResult<T>> InternalWaitForExternalTask<T>(Task<T> externalTask,
																					TimeSpan timeout)
		{
			if (await InternalWaitForExternalTask((Task)externalTask, timeout))
				return ExternalTaskWaitResult<T>.WithResult(externalTask.Result);

			return ExternalTaskWaitResult<T>.TimedOut;
		}
	}
}