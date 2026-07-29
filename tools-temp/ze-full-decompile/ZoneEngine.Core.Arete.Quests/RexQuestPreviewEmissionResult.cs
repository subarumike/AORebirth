namespace ZoneEngine.Core.Arete.Quests;

public sealed class RexQuestPreviewEmissionResult
{
	public bool IsApplicable { get; private set; }

	public bool Attempted { get; private set; }

	public bool Emitted { get; private set; }

	public string Message { get; private set; }

	private RexQuestPreviewEmissionResult()
	{
	}

	public static RexQuestPreviewEmissionResult NotApplicable()
	{
		return new RexQuestPreviewEmissionResult();
	}

	public static RexQuestPreviewEmissionResult Skipped(string message)
	{
		return new RexQuestPreviewEmissionResult
		{
			IsApplicable = true,
			Attempted = false,
			Emitted = false,
			Message = message
		};
	}

	public static RexQuestPreviewEmissionResult Sent(string message)
	{
		return new RexQuestPreviewEmissionResult
		{
			IsApplicable = true,
			Attempted = true,
			Emitted = true,
			Message = message
		};
	}

	public static RexQuestPreviewEmissionResult Failed(string message)
	{
		return new RexQuestPreviewEmissionResult
		{
			IsApplicable = true,
			Attempted = true,
			Emitted = false,
			Message = message
		};
	}
}
