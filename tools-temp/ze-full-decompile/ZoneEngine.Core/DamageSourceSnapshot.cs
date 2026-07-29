using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class DamageSourceSnapshot
{
	public string Identity { get; set; }

	public DamageSourceCategory Category { get; set; }

	public int Level { get; set; }

	public int AttackRating { get; set; }

	public int AddAllOff { get; set; }

	public IList<AttackSkillContribution> AttackSkillContributions { get; private set; }

	public DamageSourceSnapshot()
	{
		Identity = string.Empty;
		Category = DamageSourceCategory.Unknown;
		AttackSkillContributions = new List<AttackSkillContribution>();
	}
}
