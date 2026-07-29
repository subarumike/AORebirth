using System.Collections.Generic;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Missions;

internal static class MissionOfferStore
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, QuestInfo[]> OffersByCharacter = new Dictionary<int, QuestInfo[]>();

	public static void StoreRoll(int characterInstance, QuestInfo[] offers)
	{
		lock (Sync)
		{
			OffersByCharacter[characterInstance] = (QuestInfo[])(((object)offers) ?? ((object)new QuestInfo[0]));
		}
	}

	public static bool TryGetOffer(int characterInstance, Identity questIdentity, out QuestInfo offer)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		offer = null;
		QuestInfo[] value;
		lock (Sync)
		{
			if (!OffersByCharacter.TryGetValue(characterInstance, out value) || value == null)
			{
				return false;
			}
		}
		QuestInfo[] array = value;
		foreach (QuestInfo val in array)
		{
			if (val == null)
			{
				continue;
			}
			Identity questIdentity2 = val.QuestIdentity;
			if (((Identity)(ref questIdentity2)).Instance == ((Identity)(ref questIdentity)).Instance)
			{
				questIdentity2 = val.QuestIdentity;
				if (((Identity)(ref questIdentity2)).Type == ((Identity)(ref questIdentity)).Type)
				{
					offer = val;
					return true;
				}
			}
		}
		return false;
	}
}
