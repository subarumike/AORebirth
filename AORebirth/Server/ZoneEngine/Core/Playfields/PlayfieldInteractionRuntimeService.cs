namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Subway.Quests;

    #endregion

    internal sealed class PlayfieldInteractionRuntimeService
    {
        internal bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
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

            if (GridTerminalInteractionHandler.Default.TryHandleCapturedUse(client, target))
            {
                return true;
            }

            if (GridTerminalInteractionHandler.Default.TryHandleGridEnterUse(client, target))
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

            if (TotwGatewayInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            if (StaticDynelInteractionHandler.Default.TryHandleUse(client, message, target))
            {
                return true;
            }

            return StatelInteractionHandler.Default.TryHandleUse(client, message, target);
        }
    }
}
