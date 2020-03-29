using System;

namespace Coroutines
{
	/// <summary>
	/// Represents an exception that is thrown when a coroutine throws an exception.
	/// </summary>
	/// <remarks>The exception thrown can be accessed through <see cref="Exception.InnerException"/>.</remarks>
	public class CoroutineUnhandledException : CoroutineException
	{
		/// <summary>
		/// Initializes a new <see cref="CoroutineUnhandledException"/> with the specified message.
		/// </summary>
		/// <param name="message">The message.</param>
		public CoroutineUnhandledException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new <see cref="CoroutineUnhandledException"/> with the specified message and inner exception.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="innerException">The inner exception.</param>
		public CoroutineUnhandledException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}