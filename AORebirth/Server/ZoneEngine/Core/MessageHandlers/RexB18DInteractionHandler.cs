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
            if (!RexMarcusChainCoordinator.IsCargoBoxTarget(target))
            {
                return false;
            }

            if (RexMarcusChainCoordinator.OnCargoUse(client.Controller.Character, target))
            {
                GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                return true;
            }

            // Capture 20260719-203251: cargo without quest → Temp1=2 + FormatFeedback wire body.
            if (RexMarcusChainCoordinator.TryRejectCargoWithoutQuest(client, message, target))
            {
                return true;
            }

            return false;
        }
    }
}
