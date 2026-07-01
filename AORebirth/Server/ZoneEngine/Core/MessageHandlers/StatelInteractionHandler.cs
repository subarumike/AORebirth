namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

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

#if DEBUG
            string s = string.Format(
                "Generic Command received:\r\nAction: {0} ({1}){2}Target: {3} {4}",
                message.Action,
                (int)message.Action,
                Environment.NewLine,
                target.Type,
                target.ToString(true));
            ChatTextMessageHandler.Default.Send(client.Controller.Character, s);
#endif
            client.Controller.UseStatel(target);
            return true;
        }
    }
}
