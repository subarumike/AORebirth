namespace ZoneEngine.Core;

public sealed class AttackSkillContribution
{
	public int StatId { get; set; }

	public int Percentage { get; set; }

	public int Value { get; set; }

	public int Contribution => Value * Percentage / 100;
}
