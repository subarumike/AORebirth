namespace ZoneEngine.Core;

public sealed class WeaponDamageCandidateFormula
{
	public string Name { get; set; }

	public WeaponDamageCandidateArOrdering ArOrdering { get; set; }

	public WeaponDamageCandidateAcOrdering AcOrdering { get; set; }

	public WeaponDamageCandidateAddDamageOrdering AddDamageOrdering { get; set; }

	public WeaponDamageCandidateCriticalOrdering CriticalOrdering { get; set; }

	public WeaponDamageCandidateAmsCapBehavior AmsCapBehavior { get; set; }

	public bool MinimumFloorAfterAc { get; set; }

	public int MultiplierNumerator { get; set; }

	public int MultiplierDenominator { get; set; }

	public WeaponDamageCandidateFormula()
	{
		Name = string.Empty;
		ArOrdering = WeaponDamageCandidateArOrdering.BasePlusTruncatedBaseTimesArOver400;
		AcOrdering = WeaponDamageCandidateAcOrdering.SubtractTruncatedAcOver10BeforeMinimumFloor;
		AddDamageOrdering = WeaponDamageCandidateAddDamageOrdering.AfterArAndAc;
		CriticalOrdering = WeaponDamageCandidateCriticalOrdering.None;
		AmsCapBehavior = WeaponDamageCandidateAmsCapBehavior.MissingCapMeansNoCap;
		MinimumFloorAfterAc = true;
		MultiplierNumerator = 1;
		MultiplierDenominator = 1;
	}
}
