namespace ZoneEngine.Core;

public enum WeaponDamageCandidateAcOrdering
{
	None,
	SubtractTruncatedAcOver10BeforeMinimumFloor,
	SubtractTruncatedAcOver10AfterMinimumFloor,
	ApplyToCriticalBonus,
	DoNotApplyToCriticalBonus
}
