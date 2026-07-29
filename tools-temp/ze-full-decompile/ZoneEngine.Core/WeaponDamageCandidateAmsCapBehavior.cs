namespace ZoneEngine.Core;

public enum WeaponDamageCandidateAmsCapBehavior
{
	MissingCapMeansNoCap,
	ZeroCapMeansNoCap,
	ZeroCapMeansLiteralZero,
	NegativeCapInvalid,
	CapAppliedBeforePost1000Handling
}
