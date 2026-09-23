namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Client trainer. The body is untrusted: only the session's own character is trained, and only
    /// through <see cref="SkillTraining"/>.
    /// </summary>
    public sealed class SkillMessageHandler : IMessageHandler<SkillMessage>
    {
        public Type MessageBodyType => typeof(SkillMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((SkillMessage)body, session);
        }

        public void Handle(SkillMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined
                || message.Identity != player.Identity)
                return;

            bool trained = SkillTraining.TryTrain(player, message.Skills, player.SkillCatalog, out string? rejection);
            if (!trained)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Skill training rejected character={0} pairs={1} ip={2}: {3} [{4}]",
                        player.Identity.Instance,
                        message.Skills?.Length ?? 0,
                        player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base),
                        rejection,
                        SkillTraining.DescribeRequest(player, message.Skills, player.SkillCatalog)));
            }

            session.Send(new SkillMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Skills = SkillTraining.BuildReply(player, message.Skills)
            });

            if (trained)
                player.FlushDirtyStats();
        }
    }
}
