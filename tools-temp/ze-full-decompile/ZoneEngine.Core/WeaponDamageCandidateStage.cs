namespace ZoneEngine.Core;

public sealed class WeaponDamageCandidateStage
{
	public string Name { get; private set; }

	public int? Before { get; private set; }

	public int? After { get; private set; }

	public string Assumption { get; private set; }

	public WeaponDamageCandidateStage(string name, int? before, int? after, string assumption)
	{
		Name = name ?? string.Empty;
		Before = before;
		After = after;
		Assumption = assumption ?? string.Empty;
	}
}
