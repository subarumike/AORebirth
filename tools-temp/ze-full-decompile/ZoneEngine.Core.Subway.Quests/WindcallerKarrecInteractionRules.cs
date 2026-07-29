using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecInteractionRules
{
	internal const int PlayfieldId = 655;

	internal const int KarrecInstance = 2036555963;

	internal const int BurgerItemId = 297042;

	internal const int CreditCardItemId = 297043;

	internal const int GatewayInstance = -1073479025;

	internal static bool IsKarrec(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref identity)).Type != 50000)
		{
			return false;
		}
		if (((Identity)(ref identity)).Instance == 2036555963)
		{
			return true;
		}
		WindcallerKarrecNpcRuntimeDefinition runtime;
		return WindcallerKarrecNpcRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out runtime) && runtime != null && runtime.Content != null && runtime.Content.SourceNpcInstance == 2036555963;
	}

	internal static bool IsGateway(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref identity)).Type == 51005 && ((Identity)(ref identity)).Instance == -1073479025;
	}

	internal static bool AreCapturedPerkUpdateFieldsResolved()
	{
		return false;
	}

	internal static KarrecTradeEligibility EvaluateTrade(int characterId, int playfieldId, Identity npcIdentity, bool missionActive, IEnumerable<int> itemIds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if (!IsKarrec(npcIdentity))
		{
			return KarrecTradeEligibility.WrongNpc;
		}
		if (characterId <= 0)
		{
			return KarrecTradeEligibility.InvalidPlayer;
		}
		if (playfieldId != 655)
		{
			return KarrecTradeEligibility.WrongPlayfield;
		}
		if (!missionActive)
		{
			return KarrecTradeEligibility.MissionNotActive;
		}
		int[] array = (itemIds ?? new int[0]).ToArray();
		return (!HasExactOfferings(array, array.Length, containsUnrecognizedItem: false)) ? KarrecTradeEligibility.MissingOrWrongOfferings : KarrecTradeEligibility.Eligible;
	}

	internal static bool HasExactOfferings(IEnumerable<int> itemIds, int stagedSlotCount, bool containsUnrecognizedItem)
	{
		int[] array = (itemIds ?? new int[0]).OrderBy((int value) => value).ToArray();
		return !containsUnrecognizedItem && stagedSlotCount == 2 && array.Length == 2 && array[0] == 297042 && array[1] == 297043;
	}
}
