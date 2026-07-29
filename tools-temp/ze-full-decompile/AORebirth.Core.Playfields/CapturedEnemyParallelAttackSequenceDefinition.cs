namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemyParallelAttackSequenceDefinition
{
	internal CapturedEnemyParallelAttackStreamDefinition[] Streams { get; private set; }

	internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

	internal int SpecialAttackWeaponUnknown1 { get; private set; }

	internal int SpecialAttackWeaponUnknown2 { get; private set; }

	internal int SpecialAttackWeaponUnknown3 { get; private set; }

	internal int SpecialAttackWeaponUnknown4 { get; private set; }

	internal int SpecialAttackWeaponUnknown5 { get; private set; }

	internal bool IsValid
	{
		get
		{
			if (Streams.Length == 0)
			{
				return false;
			}
			CapturedEnemyParallelAttackStreamDefinition[] streams = Streams;
			foreach (CapturedEnemyParallelAttackStreamDefinition capturedEnemyParallelAttackStreamDefinition in streams)
			{
				if (capturedEnemyParallelAttackStreamDefinition == null || !capturedEnemyParallelAttackStreamDefinition.IsValid)
				{
					return false;
				}
			}
			return true;
		}
	}

	internal CapturedEnemyParallelAttackSequenceDefinition(CapturedEnemyParallelAttackStreamDefinition[] streams, CapturedEnemySpecialAttackDefinition[] specialAttacks, int specialAttackWeaponUnknown1, int specialAttackWeaponUnknown2, int specialAttackWeaponUnknown3, int specialAttackWeaponUnknown4, int specialAttackWeaponUnknown5)
	{
		Streams = streams ?? new CapturedEnemyParallelAttackStreamDefinition[0];
		SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
		SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
		SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
		SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
		SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
		SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
	}
}
