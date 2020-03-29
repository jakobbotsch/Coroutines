namespace Coroutines
{
	/// <summary>
	/// Represents the different states a coroutine can be in.
	/// </summary>
	public enum CoroutineStatus
	{
		/// <summary>
		/// The coroutine is runnable.
		/// </summary>
		Runnable,
		/// <summary>
		/// The coroutine has finished running successfully.
		/// </summary>
		RanToCompletion,
		/// <summary>
		/// The coroutine finished running by being stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// The coroutine finished running with an error.
		/// </summary>
		Faulted,
	}
}