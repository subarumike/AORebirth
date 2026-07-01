namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class CorpseInteractionHandler
    {
        public static readonly CorpseInteractionHandler Default =
            new CorpseInteractionHandler();

        private CorpseInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (CorpseInteractionRules.IsDirectCorpseTarget(target))
            {
                bool used = client.Controller.Character.Playfield.TryUseCorpse(
                    client.Controller.Character,
                    target);

                client.Server.Info(
                    client,
                    "CorpseUse direct target={0} used={1}",
                    target,
                    used);

                if (used)
                {
                    AcknowledgeCorpseUseDelayed(client.Controller.Character, message, target);
                }

                return true;
            }

            Identity routedCorpseIdentity;
            if (target.Type == IdentityType.CanbeAffected
                && this.TryRouteDeadNpcCorpseUse(client, target, out routedCorpseIdentity))
            {
                AcknowledgeCorpseUseDelayed(client.Controller.Character, message, routedCorpseIdentity);
                return true;
            }

            return false;
        }

        private bool TryRouteDeadNpcCorpseUse(
            IZoneClient client,
            Identity target,
            out Identity routedCorpseIdentity)
        {
            bool routed = client.Controller.Character.Playfield.TryUseDeadNpcCorpse(
                client.Controller.Character,
                target,
                out routedCorpseIdentity);

            client.Server.Info(
                client,
                "CorpseUse deadNpc target={0} routed={1} corpse={2}",
                target,
                routed,
                routedCorpseIdentity);

            return routed;
        }

        private static void AcknowledgeCorpseUseDelayed(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(CorpseInteractionRules.CorpseUseAcknowledgeDelayMilliseconds);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    GenericCmdMessageHandler.Default.AcknowledgeCorpseUse(
                        character,
                        message,
                        corpse,
                        announceToPlayfield);
                });
        }
    }
}
