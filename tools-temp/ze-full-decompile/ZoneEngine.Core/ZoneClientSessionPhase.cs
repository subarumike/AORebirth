namespace ZoneEngine.Core;

public enum ZoneClientSessionPhase
{
	Connected,
	CharacterLoading,
	PlayfieldLoading,
	ReadyBlock,
	FullCharacterBoundary,
	CharInPlay,
	InPlay,
	Zoning,
	Disconnecting
}
