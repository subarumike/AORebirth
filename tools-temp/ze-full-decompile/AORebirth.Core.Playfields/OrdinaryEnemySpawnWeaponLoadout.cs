namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySpawnWeaponLoadout
{
	internal int LowId { get; private set; }

	internal int HighId { get; private set; }

	internal int Quality { get; private set; }

	internal string Evidence { get; private set; }

	internal bool IsValid => LowId > 0 && HighId > 0 && Quality > 0 && !string.IsNullOrWhiteSpace(Evidence);

	internal OrdinaryEnemySpawnWeaponLoadout(int lowId, int highId, int quality, string evidence)
	{
		LowId = lowId;
		HighId = highId;
		Quality = quality;
		Evidence = evidence ?? string.Empty;
	}
}
