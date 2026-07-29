namespace ZoneEngine.Core.Arete.Quests;

public sealed class MarcusB18FCompletionResult
{
	public bool IsApplicable { get; private set; }

	public bool Attempted { get; private set; }

	public bool Completed { get; private set; }

	public string Message { get; private set; }

	private MarcusB18FCompletionResult()
	{
	}

	public static MarcusB18FCompletionResult NotApplicable()
	{
		return new MarcusB18FCompletionResult();
	}

	public static MarcusB18FCompletionResult Skipped(string message)
	{
		return new MarcusB18FCompletionResult
		{
			IsApplicable = true,
			Attempted = false,
			Completed = false,
			Message = message
		};
	}

	public static MarcusB18FCompletionResult Succeeded(string message)
	{
		return new MarcusB18FCompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = true,
			Message = message
		};
	}

	public static MarcusB18FCompletionResult Failed(string message)
	{
		return new MarcusB18FCompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = false,
			Message = message
		};
	}
}
