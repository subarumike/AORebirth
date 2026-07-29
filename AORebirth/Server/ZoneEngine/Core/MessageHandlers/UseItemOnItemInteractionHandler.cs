namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;

    #endregion

    public sealed class UseItemOnItemInteractionHandler
    {
        public static readonly UseItemOnItemInteractionHandler Default =
            new UseItemOnItemInteractionHandler();

        private UseItemOnItemInteractionHandler()
        {
        }

        public bool TryHandle(IZoneClient client, GenericCmdMessage message)
        {
            if (MissionAcgObjectiveInteractionService.TryHandleUseItemOnItem(
                client,
                message))
            {
                return true;
            }

            if (MissionRepairService.TryHandleUseItemOnItem(client, message))
            {
                return true;
            }

            // FindItemReturn: L-click mission item + R-click mission terminal.
            if (MissionFindItemService.TryHandleReturnToTerminal(client, message))
            {
                return true;
            }

            if (MarcusB194GasFireProgressTracker.TryHandleUseItemOnItem(client, message))
            {
                return true;
            }

            // Capture 20260720-105157: Use RC-P Audio Recording Device on Prized Houseplant → Plant a Bug.
            if (SurveillanceUplinkQuestRuntime.TryHandleUseItemOnItem(client, message))
            {
                return true;
            }

            // Capture 20260721-afgter dog lockpick goodman: Lock Pick on Merchant's Strongbox.
            if (StanGoodmanQuestRuntime.TryHandleUseItemOnItem(client, message))
            {
                return true;
            }

            // CellAO GenericCmd UseItemOnItem: stamp insignia + Pool StaticDynel OnUseItemOn first.
            if (InventoryContainerRuntimeService.Default.TryHandleUseItemOnItem(client, message))
            {
                return true;
            }

            return NascenceStatueTeleportInteractionHandler.Default.TryHandleUseItemOnItem(client, message);
        }
    }
}
