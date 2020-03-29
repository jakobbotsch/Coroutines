using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Coroutines
{
	public partial class Coroutine
	{
		private class CoroutineYieldAwaitable : INotifyCompletion
		{
			public bool Canceled { get; set; }

			public CoroutineYieldAwaitable GetAwaiter()
			{
				return this;
			}

			public bool IsCompleted => false;

			public void GetResult()
			{
				if (Canceled)
				{
					throw CanceledException();
				}
			}

			public void OnCompleted(Action continuation)
			{
				Debug.Assert(Current._continuation == null);
				Current._continuation = continuation;
			}
		}
	}
}