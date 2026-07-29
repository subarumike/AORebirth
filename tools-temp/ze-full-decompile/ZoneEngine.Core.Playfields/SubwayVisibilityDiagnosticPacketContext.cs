namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilityDiagnosticPacketContext
{
	internal SubwayVisibilityDiagnosticSnapshot Snapshot { get; private set; }

	internal SubwayVisibilityDiagnosticEnemy Enemy { get; private set; }

	internal SubwayVisibilityDiagnosticPacketKind Kind { get; private set; }

	internal int WeaponIndex { get; private set; }

	internal SubwayVisibilityDiagnosticPacketContext(SubwayVisibilityDiagnosticSnapshot snapshot, SubwayVisibilityDiagnosticEnemy enemy, SubwayVisibilityDiagnosticPacketKind kind, int weaponIndex)
	{
		Snapshot = snapshot;
		Enemy = enemy;
		Kind = kind;
		WeaponIndex = weaponIndex;
	}
}
