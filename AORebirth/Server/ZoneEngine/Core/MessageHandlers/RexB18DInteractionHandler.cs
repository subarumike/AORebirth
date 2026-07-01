namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class RexB18DInteractionHandler
    {
        public static readonly RexB18DInteractionHandler Default =
            new RexB18DInteractionHandler();

        private RexB18DInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            bool boxProgressObserved = RexB18DBoxProgressTracker.TryObserveBoxUse(
                client.Controller.Character,
                target);
            if (RexB18DInteractionRules.ResolveRouteMode(boxProgressObserved)
                != RexB18DInteractionRouteMode.RexB18DBoxProgress)
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
            return true;
        }
    }
}
