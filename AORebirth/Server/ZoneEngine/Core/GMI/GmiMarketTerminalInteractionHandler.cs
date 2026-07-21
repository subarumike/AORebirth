namespace ZoneEngine.Core.GMI
{
    using System;

    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Omni-Trade / ICC Market terminal → client Market browser (GMI).
    /// Capture + live ZoneEngineLog: GenericCmd Use Terminal:C0070320 then MarketSend.
    /// Client opens the browser on successful Use ACK (same pattern as MailTerminal).
    /// </summary>
    public sealed class GmiMarketTerminalInteractionHandler
    {
        public static readonly GmiMarketTerminalInteractionHandler Default =
            new GmiMarketTerminalInteractionHandler();

        /// <summary>ICC/Omni Market terminal (char 18 logs, PF 4680).</summary>
        private const int MarketTerminalInstanceNerko = unchecked((int)0xC0070320);

        /// <summary>Second Market terminal from GMI deposit sessions (char 67).</summary>
        private const int MarketTerminalInstanceTraner = unchecked((int)0xC008028F);

        private GmiMarketTerminalInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || target == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Terminal || !IsMarketTerminalInstance(target.Instance))
            {
                return false;
            }

            var character = client.Controller.Character;
            if (character == null)
            {
                return false;
            }

            // Match MailTerminal: clear timer gate, run statel OnUse, always ACK so client opens UI.
            character.DoNotDoTimers = false;

            try
            {
                client.Controller.UseStatel(target);
            }
            catch (Exception ex)
            {
                client.Server.Info(
                    client,
                    "GMI Market terminal UseStatel error char={0} target={1} ex={2}",
                    character.Identity,
                    target,
                    ex.Message);
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "GMI Market terminal Use ACK char={0} pf={1} target={2}",
                character.Identity,
                character.Playfield != null ? character.Playfield.Identity.Instance : 0,
                target);

            return true;
        }

        private static bool IsMarketTerminalInstance(int instance)
        {
            return instance == MarketTerminalInstanceNerko
                   || instance == MarketTerminalInstanceTraner;
        }
    }
}
