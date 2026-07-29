namespace ZoneEngine.Core.Arete.Quests;

public sealed class MarcusB196CompletionResult
{
	public bool IsApplicable { get; private set; }

	public bool Attempted { get; private set; }

	public bool Completed { get; private set; }

	public string Message { get; private set; }

	private MarcusB196CompletionResult()
	{
	}

	public static MarcusB196CompletionResult NotApplicable()
	{
		return new MarcusB196CompletionResult();
	}

	public static MarcusB196CompletionResult Skipped(string message)
	{
		return new MarcusB196CompletionResult
		{
			IsApplicable = true,
			Attempted = false,
			Completed = false,
			Message = message
		};
	}

	public static MarcusB196CompletionResult Succeeded(string message)
	{
		return new MarcusB196CompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = true,
			Message = message
		};
	}

	public static MarcusB196CompletionResult Failed(string message)
	{
		return new MarcusB196CompletionResult
		{
			IsApplicable = true,
			Attempted = true,
			Completed = false,
			Message = message
		};
	}
}
