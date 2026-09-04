namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class GenericCmdMessageHandler : IMessageHandler<GenericCmdMessage>
    {
        public Type MessageBodyType => typeof(GenericCmdMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((GenericCmdMessage)body, session);
        }

        public void Handle(GenericCmdMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || player.IsDead)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                Deny(session, message);
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd action={0}({1}) target={2} character={3}",
                    message.Action,
                    (int)message.Action,
                    FormatTarget(message),
                    player.Identity.Instance));

            switch (message.Action)
            {
                case GenericCmdAction.Use:
                    HandleUse(message, session, player, playfield);
                    break;

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled GenericCmd action={0}({1}) character={2}",
                            message.Action,
                            (int)message.Action,
                            player.Identity.Instance));
                    Deny(session, message);
                    break;
            }
        }

        static void HandleUse(GenericCmdMessage message, IZoneSession session, Player player, Playfield playfield)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                ? message.Target[0]
                : Identity.None;

            if (target.Instance == 0
                || !playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                || dynel is not LootableDynel lootable)
            {
                Deny(session, message);
                return;
            }

            if (!lootable.TryOpen(player))
            {
                Deny(session, message);
                return;
            }

            Acknowledge(session, message, lootable.Identity);
        }

        static string FormatTarget(GenericCmdMessage message)
        {
            if (message.Target == null || message.Target.Length == 0)
                return Identity.None.ToString();

            return message.Target[0].ToString();
        }

        static void Acknowledge(IZoneSession session, GenericCmdMessage message, Identity target)
        {
            session.Send(Reply(message, target, temp1: 1));
        }

        static void Deny(IZoneSession session, GenericCmdMessage message)
        {
            session.Send(Reply(message, Identity.None, temp1: 2));
        }

        static GenericCmdMessage Reply(GenericCmdMessage message, Identity targetOverride, int temp1)
        {
            Identity[] targets = message.Target != null
                ? (Identity[])message.Target.Clone()
                : [];

            if (targetOverride != Identity.None && targets.Length > 0)
                targets[0] = targetOverride;

            return new GenericCmdMessage
            {
                Identity = message.Identity,
                Temp1 = temp1,
                Count = message.Count,
                Action = message.Action,
                Temp4 = message.Temp4,
                User = message.User,
                Target = targets,
                Unknown = 0
            };
        }
    }
}
