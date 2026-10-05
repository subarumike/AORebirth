namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Player /follow. The client drives the follow itself (it steers its own body and sends CharDCMove), but its
    /// FollowTarget is a pass-on command (<c>FollowTargetIIR_c</c>, Gamecode.dll 0x1007381e) that only takes effect once
    /// the server sends it back. Live acknowledges to the follower alone (capture 2026-10-05T02:36:46Z):
    /// FollowTarget, follower identity, info type 2, MoveType 0, the followed character, 0x40 0x20000000, zero XYZ.
    /// </summary>
    public sealed class FollowTargetMessageHandler : IMessageHandler<FollowTargetMessage>
    {
        // The two fields after the target as live sends them (together they read as float 2.5).
        const byte AckDummy = 0x40;
        const int AckDummy1 = 0x20000000;

        public Type MessageBodyType => typeof(FollowTargetMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((FollowTargetMessage)body, session);
        }

        public void Handle(FollowTargetMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || message.Info is not FollowTargetInfo request)
                return;

            // The client also passes on FollowTargets it executed for others (NPC paths); only its own is a request.
            if (message.Identity.Instance != 0 && message.Identity.Instance != player.Identity.Instance)
                return;

            Identity target = request.Target;
            if (target.Instance == 0 || target.Instance == player.Identity.Instance)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null
                || !playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                || dynel is not Character followed
                || followed.IsDead
                || !playfield.GetRequiredService<PlayfieldLocality>().IsVisibleTo(followed, player))
                return;

            if (!player.TryBeginFollowAck(followed.Identity, DateTime.UtcNow))
                return;

            session.Send(
                new FollowTargetMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Info = new FollowTargetInfo
                    {
                        FollowInfoType = 2,
                        MoveType = 0,
                        Target = followed.Identity,
                        Dummy = AckDummy,
                        Dummy1 = AckDummy1,
                        X = 0f,
                        Y = 0f,
                        Z = 0f
                    }
                });
        }
    }
}
