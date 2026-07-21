namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.GMI;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Subway.Quests;

    #endregion

    internal sealed class PlayfieldInteractionRuntimeService
    {
        internal bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            // Mission Repair Kit used on / Use of Broken Machine inside the instance.
            if (MissionRepairService.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Insurance Terminal → SaveChar (must run; playfields.dat has no SaveChar OnUse).
            if (InsuranceTerminalInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Capture 20260719-224226: Use Health Regeneration Stim on selected Wounded Dockworker.
            if (MarcusWoundedWorkersQuestRuntime.TryHandleStimUse(client, message, target))
            {
                return true;
            }

            // Capture 20260720-105157: Use Rebuilt HC-12 SecTec Monitor on Surveillance Droid → RC-P bug.
            if (SurveillanceUplinkQuestRuntime.TryHandleSecTecUse(client, message, target))
            {
                return true;
            }

            // Market/GMI trade terminal — ACK Use so client opens Market browser (not Grid).
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

            // Mission-key holder clicking a Rome building entrance → enter their private mission instance.
            // Runs before the grid/statel handlers so the mission redirect wins for building doors.
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

            // Fallback when Pool miss: items.dat Teleport / catalog (CellAO still needs Pool).
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
}
