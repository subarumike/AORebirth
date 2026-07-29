using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core;

internal static class SocialActionRuntimeService
{
	internal static void BroadcastAthleteBackflip(ICharacter character)
	{
		if (character != null)
		{
			SocialActionCmdMessage val = CreateAthleteBackflipMessage(character);
			IZoneClient val2 = ((((IDynel)character).Controller == null) ? null : ((IDynel)character).Controller.Client);
			if (val2 != null)
			{
				val2.SendCompressed((MessageBody)(object)val);
			}
			IPlayfield playfield = ((IInstancedEntity)character).Playfield;
			if (playfield != null)
			{
				playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
				{
					Body = (MessageBody)(object)val
				});
			}
		}
	}

	internal static void TriggerLevelUpBackflip(ICharacter character)
	{
		BroadcastAthleteBackflip(character);
	}

	private static SocialActionCmdMessage CreateAthleteBackflipMessage(ICharacter character)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		return new SocialActionCmdMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Unknown1 = 0,
			Unknown2 = 0,
			Unknown3 = 0,
			Unknown4 = 1,
			Unknown5 = 0,
			Action = (SocialAction)6
		};
	}
}
