namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationIssue
{
	public WeaponDamageObservationIssueKind Kind { get; private set; }

	public string Detail { get; private set; }

	public WeaponDamageObservationIssue(WeaponDamageObservationIssueKind kind, string detail)
	{
		Kind = kind;
		Detail = detail ?? string.Empty;
	}
}
