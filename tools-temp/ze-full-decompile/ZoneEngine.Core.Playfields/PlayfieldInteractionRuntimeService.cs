using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.GMI;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Subway.Quests;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldInteractionRuntimeService
{
	internal bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		if (MissionRepairService.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (InsuranceTerminalInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (MarcusWoundedWorkersQuestRuntime.TryHandleStimUse(client, message, target))
		{
			return true;
		}
		if (SurveillanceUplinkQuestRuntime.TryHandleSecTecUse(client, message, target))
		{
			return true;
		}
		if (GmiMarketTerminalInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (RexB18DInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (InventoryContainerInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (GuestKeyGeneratorInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (CityControllerInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (CorpseInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (MissionEntranceInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (GridTerminalInteractionHandler.Default.TryHandleCapturedUse(client, target))
		{
			return true;
		}
		if (GridTerminalInteractionHandler.Default.TryHandleGridEnterUse(client, target))
		{
			return true;
		}
		if (StaticDynelInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (NascenceStatueTeleportInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (SurgeryClinicInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (CapturedSubwayVendorInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (CapturedThrakGardenVendorInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (CapturedHoloDeckVendorInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		if (TotwGatewayInteractionHandler.Default.TryHandleUse(client, message, target))
		{
			return true;
		}
		return StatelInteractionHandler.Default.TryHandleUse(client, message, target);
	}
}
