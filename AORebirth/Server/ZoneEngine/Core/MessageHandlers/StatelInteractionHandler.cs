namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class StatelInteractionHandler
    {
        public static readonly StatelInteractionHandler Default =
            new StatelInteractionHandler();

        private StatelInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (StatelInteractionRules.ResolveRouteMode(true) != StatelInteractionRouteMode.StatelFallback)
            {
                return false;
            }

            client.Controller.UseStatel(target);
            GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
            return true;
        }
    }
}
