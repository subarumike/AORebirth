namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilityDiagnosticPacketRecord
{
	internal SubwayVisibilityDiagnosticSnapshot Snapshot { get; private set; }

	internal SubwayVisibilityDiagnosticEnemy Enemy { get; private set; }

	internal SubwayVisibilityDiagnosticPacketKind Kind { get; private set; }

	internal int WeaponIndex { get; private set; }

	internal int SerializedSize { get; private set; }

	internal SubwayVisibilityDiagnosticPacketRecord(SubwayVisibilityDiagnosticSnapshot snapshot, SubwayVisibilityDiagnosticEnemy enemy, SubwayVisibilityDiagnosticPacketKind kind, int weaponIndex, int serializedSize)
	{
		Snapshot = snapshot;
		Enemy = enemy;
		Kind = kind;
		WeaponIndex = weaponIndex;
		SerializedSize = serializedSize;
	}
}
