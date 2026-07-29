namespace ZoneEngine.Core.Arete.Quests;

public sealed class RexB18ECompletionResult
{
	public bool IsApplicable { get; private set; }

	public bool Attempted { get; private set; }

	public bool Completed { get; private set; }

	public string Message { get; private set; }

	private RexB18ECompletionResult()
	{
	}

	public static RexB18ECompletionResult NotApplicable()
	{
		return new RexB18ECompletionResult();
	}

	public static RexB18ECompletionResult Skipped(string message)
	{
		return new RexB18ECompletionResult
		{
			IsApplicable = true,
			Attempted = false,
			Completed = false,
			Message = message
		};
	}

	public static RexB18ECompletionResult Succeeded(string message)
	{
		return new RexB18ECompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = true,
			Message = message
		};
	}

	public static RexB18ECompletionResult Failed(string message)
	{
		return new RexB18ECompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = false,
			Message = message
		};
	}
}
