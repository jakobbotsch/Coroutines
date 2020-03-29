using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Coroutines
{
	internal static class TaskExtensions
	{
		public static bool WaitOrUnwrap(this Task task, int timeout)
		{
			try
			{
				return task.Wait(timeout);
			}
			catch (AggregateException)
			{
				Debug.Assert(task.IsFaulted || task.IsCanceled);

				// Try to rethrow exception through awaiter
				task.GetAwaiter().GetResult();

				throw;
			}
		}
	}
}