namespace Coroutines
{
	/// <summary>
	/// Represents the result of an external task wait.
	/// </summary>
	/// <typeparam name="T">The return type of the external task.</typeparam>
	public struct ExternalTaskWaitResult<T>
	{
		internal static readonly ExternalTaskWaitResult<T> TimedOut = new ExternalTaskWaitResult<T>();

		private ExternalTaskWaitResult(T result)
		{
			Completed = true;
			Result = result;
		}

		/// <summary>Gets a value that indicates whether the task completed in the allotted timeout period.</summary>
		public bool Completed { get; }
		/// <summary>Gets the result of the external task. Only valid if <see cref="Completed"/> is <c>true</c>.</summary>
		public T Result { get; }

		internal static ExternalTaskWaitResult<T> WithResult(T result)
		{
			return new ExternalTaskWaitResult<T>(result);
		}
	}
}