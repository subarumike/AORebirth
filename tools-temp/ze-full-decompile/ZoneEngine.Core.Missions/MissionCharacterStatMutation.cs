namespace ZoneEngine.Core.Missions;

public sealed class MissionCharacterStatMutation
{
	public int StatIdentityType { get; set; }

	public int StatId { get; set; }

	public MissionStatMutationKind Kind { get; set; }

	public long Value { get; set; }

	public long MinimumValue { get; set; }

	public long MaximumValue { get; set; }
}
