using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Subway.Quests;

internal sealed class TotwGatewayInteractionHandler
{
	internal const int GatewayInstance = -1073479025;

	internal static readonly TotwGatewayInteractionHandler Default = new TotwGatewayInteractionHandler();

	private TotwGatewayInteractionHandler()
	{
	}

	internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Expected O, but got Unknown
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		if (!WindcallerKarrecInteractionRules.IsGateway(target))
		{
			return false;
		}
		ICharacter val = ((client == null || client.Controller == null) ? null : client.Controller.Character);
		if (val != null && ((IInstancedEntity)val).Playfield != null)
		{
			Identity playfield = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (((Identity)(ref playfield)).Instance == 655 && IsKnownGatewayInCurrentPlayfield(val, target))
			{
				bool flag = WindcallerKarrecQuestRuntime.HasAccountAccess(val);
				if (!flag && WindcallerKarrecQuestRuntime.IsCompleted(val))
				{
					KarrecCompletionResult karrecCompletionResult = WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(val);
					if (!karrecCompletionResult.Completed)
					{
						BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(val, message);
						return true;
					}
					flag = WindcallerKarrecQuestRuntime.HasAccountAccess(val);
				}
				if (!flag)
				{
					BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(val, message);
					return true;
				}
				Dynel val2 = (Dynel)(object)((val is Dynel) ? val : null);
				Playfield playfield2 = ((IInstancedEntity)val).Playfield as Playfield;
				if (val2 == null || playfield2 == null)
				{
					BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(val, message);
					return true;
				}
				Coordinate destination = new Coordinate(1814f, 29f, 2699f);
				Quaternion heading = new Quaternion(0.0, -0.9576424956321716, 0.0, 0.2879597544670105);
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
				Quaternion heading2 = heading;
				playfield = default(Identity);
				((Identity)(ref playfield)).Type = (IdentityType)51101;
				((Identity)(ref playfield)).Instance = 647;
				playfield2.Teleport(val2, destination, (IQuaternion)(object)heading2, playfield, delegate(ICharacter transferCharacter)
				{
					//IL_0021: Unknown result type (might be due to invalid IL or missing references)
					//IL_0041: Unknown result type (might be due to invalid IL or missing references)
					//IL_0056: Expected O, but got Unknown
					//IL_0056: Expected O, but got Unknown
					BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>.Default.SendCapturedGatewayTransfer(transferCharacter, new Vector3(3214.815185546875, 35.51499938964844, 791.053466796875), new Vector3(1814.0, 29.0, 2699.0), heading, 647);
				});
				return true;
			}
		}
		if (val != null)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(val, message);
		}
		return true;
	}

	private static bool IsKnownGatewayInCurrentPlayfield(ICharacter character, Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (character != null && ((IInstancedEntity)character).Playfield != null)
		{
			Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			if (pFData.TryGetValue(((Identity)(ref identity)).Instance, out var value2))
			{
				result = (value2.Statels.Any(delegate(StatelData value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_001c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0021: Unknown result type (might be due to invalid IL or missing references)
					Identity identity2 = value.Identity;
					int result2;
					if (((Identity)(ref identity2)).Type == ((Identity)(ref target)).Type)
					{
						identity2 = value.Identity;
						result2 = ((((Identity)(ref identity2)).Instance == ((Identity)(ref target)).Instance) ? 1 : 0);
					}
					else
					{
						result2 = 0;
					}
					return (byte)result2 != 0;
				}) ? 1 : 0);
				goto IL_0054;
			}
		}
		result = 0;
		goto IL_0054;
		IL_0054:
		return (byte)result != 0;
	}
}
