namespace ZoneEngine.Core.Arete.Quests;

public sealed class RexB18CProgressUpdateResult
{
	public bool IsApplicable { get; private set; }

	public bool Matched { get; private set; }

	public string Message { get; private set; }

	public ObjectiveProgressRecord Progress { get; private set; }

	private RexB18CProgressUpdateResult()
	{
	}

	public static RexB18CProgressUpdateResult NotApplicable()
	{
		return new RexB18CProgressUpdateResult();
	}

	public static RexB18CProgressUpdateResult Ignored(string message)
	{
		return new RexB18CProgressUpdateResult
		{
			IsApplicable = true,
			Matched = false,
			Message = message
		};
	}

	public static RexB18CProgressUpdateResult MatchedProgress(ObjectiveProgressRecord progress)
	{
		return new RexB18CProgressUpdateResult
		{
			IsApplicable = true,
			Matched = true,
			Progress = progress
		};
	}
}
