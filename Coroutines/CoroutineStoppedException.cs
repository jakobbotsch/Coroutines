using System;

namespace Coroutines
{
	/// <summary>
	/// Represents an exception that is thrown when the coroutine is stopped.
	/// </summary>
	public class CoroutineStoppedException : Exception
	{
		internal CoroutineStoppedException(string message) : base(message)
		{
		}
	}
}