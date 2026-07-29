using System;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Thrak.Quests;

internal static class ThrakGardenKeyInteractionRules
{
	internal const int VeronicaPlayfieldId = 4310;

	internal const int ProphetPlayfieldId = 4311;

	internal const int HypnagogicPlayfieldId = 4677;

	internal const int SilvertailPlayfieldId = 4310;

	internal const int VeronicaInstance = 2021348530;

	internal const int ProphetInstance = 2027061494;

	internal const int HypnagogicInstance = 2037747514;

	internal const int SilvertailInstanceA = 2037797536;

	internal const int SilvertailInstanceB = 2037797621;

	internal const int SilvertailInstanceC = 2037797543;

	internal const string VeronicaName = "Scientist Veronica Escobar";

	internal const string ProphetName = "Prophet Yutt Thrak";

	internal const string HypnagogicName = "Hypnagogic Urga-Lum Thrak";

	internal const string DreamingSilvertailName = "Dreaming Silvertail";

	internal const string CursedSilvertailName = "Cursed Silvertail";

	internal const int AncientPatternAnalyzerItemId = 214998;

	internal const int InspectedAncientPatternAnalyzerItemId = 214783;

	internal const int InsigniaOfThrakItemId = 214789;

	internal const int FavoredAncientPatternAnalyzerItemId = 214785;

	internal const int SacredGardenKeyItemId = 226994;

	internal const string QuestVeronica = "Mission:5556893A";

	internal const string QuestInsignia = "Mission:55563C16";

	internal const string QuestVeronicaUpdated = "Mission:55563C17";

	internal const string QuestGarden = "Mission:55563C18";

	internal const string QuestSouls = "Mission:5556591A";

	internal const string QuestSouls1 = "Mission:5556893B";

	internal const string QuestSouls2 = "Mission:5556893C";

	internal const string QuestReturn = "Mission:5556893D";

	internal const string AccountKeyFlag = "thrak-garden-key";

	internal const string AnalyzerGrantedFlag = "thrak-analyzer-granted";

	internal const string InspectedAnalyzerGrantedFlag = "thrak-inspected-analyzer-granted";

	internal const string ProphetDeviceInspectedFlag = "thrak-prophet-device-inspected";

	internal const string InsigniaGrantedFlag = "thrak-insignia-granted";

	internal const string KeyGrantedFlag = "thrak-key-granted";

	internal const string SoulCountFlag = "thrak-soul-count";

	internal static bool IsSacredGardenKey(int itemId)
	{
		return itemId == 226994;
	}

	internal static bool IsSacredGardenKeyItem(int lowId, int highId)
	{
		return IsSacredGardenKey(lowId) || IsSacredGardenKey(highId);
	}

	internal static bool IsVeronica(Identity identity)
	{
		return ((Identity)(ref identity)).Instance == 2021348530;
	}

	internal static bool IsProphet(Identity identity)
	{
		return ((Identity)(ref identity)).Instance == 2027061494;
	}

	internal static bool IsHypnagogic(Identity identity)
	{
		return ((Identity)(ref identity)).Instance == 2037747514;
	}

	internal static bool IsDreamingSilvertail(Identity identity)
	{
		return ((Identity)(ref identity)).Instance == 2037797536 || ((Identity)(ref identity)).Instance == 2037797621 || ((Identity)(ref identity)).Instance == 2037797543;
	}

	internal static bool IsThrakQuestNpcName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		return string.Equals(name, "Scientist Veronica Escobar", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Prophet Yutt Thrak", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Hypnagogic Urga-Lum Thrak", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Dreaming Silvertail", StringComparison.OrdinalIgnoreCase);
	}
}
