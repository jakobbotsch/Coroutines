using System;

namespace Coroutines
{
	/// <summary>
	/// Represents the base class for exceptions thrown by the coroutine framework.
	/// </summary>
	public abstract class CoroutineException : Exception
	{
		/// <summary>
		/// Initializes a new <see cref="CoroutineException"/> with the specified message.
		/// </summary>
		/// <param name="message">The message.</param>
		protected CoroutineException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new <see cref="CoroutineException"/> with the specified message and inner exception.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="innerException">The inner exception.</param>
		protected CoroutineException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}