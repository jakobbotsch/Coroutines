using System;

namespace Coroutines
{
	/// <summary>
	/// Represents an exception that is thrown when a coroutine behaves unexpectedly.
	/// </summary>
	public class CoroutineBehaviorException : CoroutineException
	{
		/// <summary>
		/// Initializes a new <see cref="CoroutineBehaviorException"/> with the specified message.
		/// </summary>
		/// <param name="message">The message.</param>
		public CoroutineBehaviorException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new <see cref="CoroutineBehaviorException"/> with the specified message and inner exception.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="innerException">The inner exception.</param>
		public CoroutineBehaviorException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}