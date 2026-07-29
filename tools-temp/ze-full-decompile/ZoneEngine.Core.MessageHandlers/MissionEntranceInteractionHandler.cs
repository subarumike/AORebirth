using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class MissionEntranceInteractionHandler
{
	public static readonly MissionEntranceInteractionHandler Default = new MissionEntranceInteractionHandler();

	private MissionEntranceInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Invalid comparison between Unknown and I4
		if (client == null || client.Controller == null)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance))
		{
			bool flag = MissionInstanceService.IsMissionExitDoorTarget(target);
			bool flag2 = MissionInstanceService.IsNearInteriorExitDoor(character, 8.0, 10.0);
			if (!flag && !flag2)
			{
				return false;
			}
			if (!MissionInstanceService.TryExitMissionInstance(client))
			{
				return false;
			}
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
			return true;
		}
		if (!MissionInstanceService.IsAcceptedMissionEntranceUse(character, target))
		{
			return false;
		}
		if (MissionInstanceService.IsRomeEntranceDoor(((Identity)(ref target)).Instance))
		{
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			if (((Identity)(ref identity)).Instance != 735 && (int)((Identity)(ref target)).Type != 56006 && !MissionInstanceService.IsNearAcceptedMarker(character, 10.0, 14.0))
			{
				return false;
			}
		}
		if (!MissionKeyGrantService.HasMissionKey(character))
		{
			return false;
		}
		if (!MissionInstanceService.TryEnterMissionInstance(client))
		{
			return false;
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		return true;
	}
}
