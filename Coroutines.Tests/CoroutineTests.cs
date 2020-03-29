using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coroutines.Tests
{
	public class CoroutineTests
	{
		private readonly int _threadID;

		public CoroutineTests()
		{
			_threadID = Thread.CurrentThread.ManagedThreadId;
		}

		private void AssertThread()
		{
			Assert.Equal(_threadID, Thread.CurrentThread.ManagedThreadId);
		}

		private async Task TwoTicks()
		{
			Assert.Equal(0, Coroutine.Current.Ticks);
			await Coroutine.Yield();

			AssertThread();

			Assert.Equal(1, Coroutine.Current.Ticks);
			await Coroutine.Yield();

			AssertThread();
		}

		private void AssertStates(Coroutine coroutine, params CoroutineStatus[] statuses)
		{
			for (int i = 0; i < statuses.Length; i++)
			{
				CoroutineStatus status = statuses[i];
				Assert.Equal(coroutine.Status, status);

				if (coroutine.IsFinished)
				{
					Assert.Equal(statuses.Length - 1, i);
					break;
				}

				coroutine.Resume();
			}
		}

		[Fact]
		public void TestRanToCompletion()
		{
			Coroutine coroutine = new Coroutine(new Func<Task>(TwoTicks));
			AssertStates(coroutine, CoroutineStatus.Runnable, CoroutineStatus.Runnable,
			             CoroutineStatus.Runnable, CoroutineStatus.RanToCompletion);

			AssertCoroutineFinished(coroutine, CoroutineStatus.RanToCompletion);
		}

		[Fact]
		public void TestSleep()
		{
			const int maxSleepTime = 2000;
			int totalSleepTime = 0;
			Random rand = new Random();
			while (totalSleepTime < maxSleepTime)
			{
				int sleepTime = rand.Next(0, 500);
				Coroutine routine = new Coroutine(async () =>
				                                        {
					                                        await Coroutine.Sleep(sleepTime);
					                                        AssertThread();
				                                        });

				Stopwatch timer = Stopwatch.StartNew();
				do
				{
					routine.Resume();
				} while (!routine.IsFinished);

				timer.Stop();
				Assert.True(Math.Abs(timer.ElapsedMilliseconds - sleepTime) < 5);

				AssertCoroutineFinished(routine, CoroutineStatus.RanToCompletion);

				totalSleepTime += sleepTime;
			}
		}

		private async Task NestNestNest(int ticks)
		{
			Assert.Equal(ticks, Coroutine.Current.Ticks);
		}

		private async Task NestNest(int ticks)
		{
			await NestNestNest(ticks);
			AssertThread();
			Assert.Equal(ticks, Coroutine.Current.Ticks);

			await Coroutine.Yield();
			AssertThread();
			Assert.Equal(ticks + 1, Coroutine.Current.Ticks);
		}

		private async Task Nest(int ticks)
		{
			Assert.Equal(ticks, Coroutine.Current.Ticks);
			await NestNest(ticks);
			AssertThread();
			Assert.Equal(ticks + 1, Coroutine.Current.Ticks);

			await Coroutine.Yield();
			AssertThread();
			Assert.Equal(ticks + 2, Coroutine.Current.Ticks);
		}

		private async Task NestingTest()
		{
			Assert.Equal(0, Coroutine.Current.Ticks);
			await Nest(0);
			AssertThread();
			Assert.Equal(2, Coroutine.Current.Ticks);
			await NestNest(2);
			AssertThread();
			Assert.Equal(3, Coroutine.Current.Ticks);
		}

		[Fact]
		public void TestNesting()
		{
			Coroutine routine = new Coroutine(() => NestingTest());

			do
			{
				routine.Resume();
			} while (!routine.IsFinished);

			AssertCoroutineFinished(routine, CoroutineStatus.RanToCompletion);
		}

		private async Task EmptyCoroutineTest()
		{
			AssertThread();
		}

		[Fact]
		public void TestEmptyCoroutine()
		{
			Coroutine routine = new Coroutine(() => EmptyCoroutineTest());
			routine.Resume();

			AssertCoroutineFinished(routine, CoroutineStatus.RanToCompletion);

			Assert.Throws<InvalidOperationException>(() => routine.Resume());
		}

		private Exception _thrownException;

		private async Task ThrowsException()
		{
			await Coroutine.Yield();
			AssertThread();
			throw _thrownException = new Exception();
		}

		[Fact]
		public void TestException()
		{
			Coroutine routine = new Coroutine(() => ThrowsException());

			routine.Resume();

			CoroutineUnhandledException ex =
				Assert.Throws<CoroutineUnhandledException>(() => routine.Resume());
			Assert.Same(ex, routine.FaultingException);
			Assert.Same(_thrownException, ex.InnerException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);
		}

		private static void AssertCoroutineFinished(Coroutine routine, CoroutineStatus finishStatus)
		{
			Assert.True(routine.IsFinished);
			Assert.Equal(finishStatus, routine.Status);
		}

		private bool _executedFinally;

		private async Task FinallyTest()
		{
			try
			{
				await ThrowsException();
			}
			finally
			{
				_executedFinally = true;
			}
		}

		[Fact]
		public void TestFinally()
		{
			Coroutine routine = new Coroutine(() => FinallyTest());

			CoroutineUnhandledException ex =
				Assert.Throws<CoroutineUnhandledException>(
					() =>
					{
						do
						{
							routine.Resume();
						} while (!routine.IsFinished);
					});
			Assert.Same(ex, routine.FaultingException);
			Assert.Same(_thrownException, ex.InnerException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);
			Assert.True(_executedFinally);
		}

		private bool _executedFinally2;

		private async Task FinallyTest2()
		{
			try
			{
				await Coroutine.Yield();
				AssertThread();
				await Coroutine.Yield();
				AssertThread();
				await Coroutine.Yield();
				AssertThread();
			}
			finally
			{
				_executedFinally2 = true;
				AssertThread();
			}
		}

		[Fact]
		public void TestFinally2()
		{
			Coroutine routine = new Coroutine(() => FinallyTest2());

			routine.Resume();
			routine.Resume();
			routine.Dispose();

			AssertCoroutineFinished(routine, CoroutineStatus.Stopped);
			Assert.True(_executedFinally2);
		}

		private bool _executedFinally3;

		private async Task DisposesCurrent()
		{
			await Coroutine.Yield();
			AssertThread();
			Coroutine.Current.Dispose();
		}

		private async Task FinallyTest3()
		{
			try
			{
				await Coroutine.Yield();
				AssertThread();
				await DisposesCurrent();
			}
			finally
			{
				_executedFinally3 = true;
			}
		}

		[Fact]
		public void TestFinally3()
		{
			Coroutine routine = new Coroutine(async () => await FinallyTest3());
			routine.Resume();
			routine.Resume();
			routine.Resume();

			AssertCoroutineFinished(routine, CoroutineStatus.Stopped);
			Assert.True(_executedFinally3);
		}

		private bool _executedFinally4;
		private bool _beforeLastYield;
		private bool _afterLastYield;

		private async Task BadBehaviorTest()
		{
			try
			{
				await Coroutine.Yield();
				AssertThread();

				try
				{
					await Coroutine.Yield();
				}
				catch (CoroutineStoppedException)
				{
				}

				AssertThread();
				_beforeLastYield = true;
				await Coroutine.Yield();
				// Should never reach here
				_beforeLastYield = false;
				_afterLastYield = true;

				AssertThread();
			}
			finally
			{
				_executedFinally4 = true;
			}
		}

		[Fact]
		public void TestBadBehavior()
		{
			Coroutine routine = new Coroutine(() => BadBehaviorTest());

			routine.Resume();
			routine.Resume();

			CoroutineBehaviorException ex = Assert.Throws<CoroutineBehaviorException>(() => routine.Dispose());
			Assert.Same(ex, routine.FaultingException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);

			Assert.True(_beforeLastYield);
			Assert.False(_afterLastYield);
			Assert.False(_executedFinally4);
		}

		private async Task AwaitExternalTask()
		{
			await Task.Factory.StartNew(() => Thread.Sleep(100));
		}

		[Fact]
		public void TestAwaitExternalTaskThrows()
		{
			Coroutine routine = new Coroutine(() => AwaitExternalTask());

			CoroutineBehaviorException ex = Assert.Throws<CoroutineBehaviorException>(() => routine.Resume());
			Assert.Same(ex, routine.FaultingException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);
		}

		[Fact]
		public void TestMultipleCoroutineTasksThrows()
		{
			RunCR(async () =>
			            {
				            Task task = Coroutine.Yield();

				            Assert.Throws<InvalidOperationException>(() => { Coroutine.Yield(); });

				            await task;
			            });
		}

		private async Task YieldProxy()
		{
			await Coroutine.Yield();
		}

		[Fact]
		public void TestNoAwaitThrows()
		{
			Coroutine routine = new Coroutine(async () => YieldProxy());

			CoroutineBehaviorException ex = Assert.Throws<CoroutineBehaviorException>(() => routine.Resume());
			Assert.Same(ex, routine.FaultingException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);
		}

		private async Task<object> ResultTest()
		{
			await Coroutine.Yield();
			return 10;
		}

		[Fact]
		public void TestResult()
		{
			Coroutine routine = new Coroutine(ResultTest);

			routine.Resume();
			routine.Resume();
			AssertCoroutineFinished(routine, CoroutineStatus.RanToCompletion);
			Assert.Equal(10, routine.Result);
		}

		private async Task<object> ResultTest2()
		{
			await Coroutine.Yield();

			throw new Exception("herpa");
		}

		[Fact]
		public void TestResult2()
		{
			Coroutine routine = new Coroutine(ResultTest2);

			routine.Resume();
			CoroutineUnhandledException ex =
				Assert.Throws<CoroutineUnhandledException>(() => routine.Resume());
			Assert.Same(ex, routine.FaultingException);

			AssertCoroutineFinished(routine, CoroutineStatus.Faulted);
			Assert.Null(routine.Result);
		}

		[Fact]
		public void TestExternalTaskPropagatesException()
		{
			RunCR(async () =>
			            {
				            Exception thrownException = new Exception();
				            Exception ex = await
					            Assert.ThrowsAsync<Exception>(
						            async () =>
						                  {
							                  await Coroutine.ExternalTask(Task.Run(() => { throw thrownException; }));
						                  });

				            Assert.Same(thrownException, ex);
			            });
		}

		[Fact]
		public void TestExternalTaskPropagatesCancellationException()
		{
			RunCR(async () =>
			            {
				            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
				            Task ignored = Task.Delay(100).ContinueWith(t => tcs.SetCanceled());
				            await
					            Assert.ThrowsAnyAsync<OperationCanceledException>(
						            async () => await Coroutine.ExternalTask(tcs.Task));
			            });
		}

		[Fact]
		public void Test_ExternalTask_WaitsForResult()
		{
			RunCR(async () =>
			            {
				            int value = await Coroutine.ExternalTask(async () =>
				                                                           {
					                                                           await Task.Delay(100);
					                                                           return 42;
				                                                           });

				            Assert.Equal(42, value);
			            });
		}

		private void RunCR(Func<Task> coroutine)
		{
			using (Coroutine cr = new Coroutine(coroutine))
			{
				while (!cr.IsFinished)
					cr.Resume();
			}
		}

		[Fact]
		public void Test_CoroutineYield_OutsideCoroutine()
		{
			Assert.Throws<InvalidOperationException>(() => { Coroutine.Yield(); });
		}
	}
}
