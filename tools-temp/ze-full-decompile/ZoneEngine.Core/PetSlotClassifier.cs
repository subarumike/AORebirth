using System;

namespace ZoneEngine.Core;

internal static class PetSlotClassifier
{
	public const int RegularPetStrain = 1015;

	public const int HealingPetStrain = 1016;

	public const int BureaucratCompanionStrain = 1017;

	public const int HealingSpellListSlot = 2;

	public const int RegularSpellListSlot = 5;

	public const int CapturedPetStateValue = 2304001;

	public static int ResolveStrain(string petHash)
	{
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return 0;
		}
		if (petHash.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
		{
			return 1015;
		}
		switch (petHash.ToUpperInvariant())
		{
		case "A020":
		case "A141":
		case "BCBG":
			return 1015;
		case "A142":
		case "CRLT":
			return 1017;
		default:
			return 1016;
		}
	}

	public static int ResolveSpellListSlot(int petSlotStrain)
	{
		return (petSlotStrain == 1016) ? 2 : 5;
	}

	public static bool IsBureaucratCompanionStrain(int petSlotStrain)
	{
		return petSlotStrain == 1017;
	}
}
