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
            bool generatedPlayfield =
                MissionAcgRuntimeInteractionService.ClaimsCurrentGeneratedPlayfield(client);
            if (generatedPlayfield
                && CorpseInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Persisted ACG missions route every interaction by owner + allocated PF2 + runtime
            // identity before any legacy global tracker can claim the target.
            if (MissionAcgRuntimeInteractionService.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Mission Repair Kit used on / Use of Broken Machine inside the instance.
            if (MissionRepairService.TryHandleUse(client, message, target))
            {
                return true;
            }

            // FindPerson: Use/tag the fictional contact inside the instance.
            if (MissionFindPersonService.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (MissionLootPropService.TryHandleUse(client, message, target))
            {
                return true;
            }

            // FindItem / FindItemReturn: Use Mission Cube → real item.
            if (MissionFindItemService.TryHandleCubeUse(client, message, target))
            {
                return true;
            }

            // Capture 20260721-finish: Use Exit Arete Landing Terminal:574187C3 → ICC HQ PF 655.
            // Must run before Insurance — template 297303 must not be stolen as SaveChar.
            if (VaughnHammondQuestRuntime.TryHandleExitAreteLandingUse(client, message, target))
            {
                return true;
            }

            if (CrashedAlienShipDoorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (LeonoraMartyQuestRuntime.TryHandleCreditCardPickup(client, message, target))
            {
                return true;
            }

            var source = client?.Controller?.Character;
            if (PatrickSunQuestRuntime.TryHandleInsuranceTerminalUse(source, target))
            {
                GenericCmdMessageHandler.Default.Acknowledge(source, message);
                return true;
            }

            // Insurance Terminal → SaveChar (must run; playfields.dat has no SaveChar OnUse).
            // Surgery clinic Uses are excluded inside InsuranceTerminalInteractionHandler so they
            // fall through to SurgeryClinicInteractionHandler (Arete Terminal:574187D1).
            if (InsuranceTerminalInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Capture 20260721-Mason / 20260721-182543: Arete surgery clinic before other quest Uses.
            if (SurgeryClinicInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Capture 20260721-sara: Use Remains of Shop Thief → DNA-Locked Armor.
            if (SarahGreeneQuestRuntime.TryHandleShopThiefUse(client, message, target))
            {
                return true;
            }

            // Capture 20260730-214622: Use Bank of Rubi-Ka Credit Card floor Terminal → tips + item.
            if (LeonoraMartyQuestRuntime.TryHandleCreditCardPickup(client, message, target))
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

            // NPC-owned capture shops before corpse routing (living Marco is CanbeAffected).
            if (CapturedSubwayVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            // Buckethead Technodealer: registry match only (never name-match other NPCs).
            if (CapturedBucketheadTechnodealerInteractionHandler.Default.TryHandleUse(client, message, target))
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

            if (CapturedAreteMarcoSpidaVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (CapturedAreteLoreleiVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (CapturedAreteAntonioStacklundVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (CapturedAreteRemiGalloisVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (CapturedAreteSarahGreeneVendorInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (!generatedPlayfield
                && CorpseInteractionHandler.Default.TryHandleUse(client, message, target))
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

            if (TotwGatewayInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            return StatelInteractionHandler.Default.TryHandleUse(client, message, target);
        }
    }
}
