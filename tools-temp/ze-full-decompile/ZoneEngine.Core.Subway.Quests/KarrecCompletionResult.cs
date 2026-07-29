using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Subway.Quests;

internal sealed class KarrecCompletionResult
{
	internal bool Completed { get; private set; }

	internal long SideTokenValue { get; private set; }

	internal MissionRewardExecutionStatus SideTokenStatus { get; private set; }

	internal MissionRewardExecutionStatus ResearchStatus { get; private set; }

	internal string Error { get; private set; }

	private KarrecCompletionResult()
	{
	}

	internal static KarrecCompletionResult Succeeded(long sideTokenValue, MissionRewardExecutionStatus sideTokenStatus, MissionRewardExecutionStatus researchStatus)
	{
		return new KarrecCompletionResult
		{
			Completed = true,
			SideTokenValue = sideTokenValue,
			SideTokenStatus = sideTokenStatus,
			ResearchStatus = researchStatus
		};
	}

	internal static KarrecCompletionResult Failed(string error)
	{
		return new KarrecCompletionResult
		{
			Error = error
		};
	}
}
