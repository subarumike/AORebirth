namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    public sealed class CharacterActionMessageHandler : IMessageHandler<CharacterActionMessage>
    {
        public Type MessageBodyType => typeof(CharacterActionMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CharacterActionMessage)body, session);
        }

        public void Handle(CharacterActionMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CharacterAction action={0}({1}) target={2} p1={3} p2={4} character={5}",
                    message.Action,
                    (int)message.Action,
                    message.Target,
                    message.Parameter1,
                    message.Parameter2,
                    player.Identity.Instance));

            switch (message.Action)
            {
                case CharacterActionType.StandUp:
                    // Sit posture arrives via CharDCMove (MoveType SwitchToSit), not CharacterAction.
                    ApplyStand(player);
                    break;

                case CharacterActionType.StartSneak: //TODO: Wire in hiding when sneaking
                    player.Motor.ApplyAction(MovementAction.SwitchToSneak);
                    AnnounceAction(player, CharacterActionType.StartedSneaking);
                    break;

                case CharacterActionType.StopSneaking: //TODO: Wire in cooldown on sneak
                    player.Motor.ApplyAction(MovementAction.LeaveSneak);
                    AnnounceAction(player, CharacterActionType.StopSneaking);
                    break;

                case CharacterActionType.Logout:
                    //TODO: Combat guard this at least
                    session.Send(
                        new StartLogoutMessage
                        {
                            Identity = player.Identity
                        });

                    Playfield? playfield = player.Playfield;
                    if (playfield != null)
                        playfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
                    else
                        session.Close();
                    break;

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled CharacterAction action={0}({1}) character={2}",
                            message.Action,
                            (int)message.Action,
                            player.Identity.Instance));
                    break;
            }
        }

        static void ApplyStand(Player player)
        {
            player.Motor.ApplyAction(MovementAction.LeaveSit);

            Cell? cell = player.Cell;
            if (cell == null)
                return;

            AnnounceAction(player, CharacterActionType.StandUp);
            cell.Announce(CreatePostureMove(player, (byte)MovementAction.LeaveSit));
        }

        static void AnnounceAction(Player player, CharacterActionType action)
        {
            Cell? cell = player.Cell;
            if (cell == null)
                return;

            cell.Announce(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0x00,
                    Action = action,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        static CharDCMoveMessage CreatePostureMove(Player player, byte moveType)
        {
            return new CharDCMoveMessage
            {
                Identity = player.Identity,
                Unknown = 0x00,
                MoveType = moveType,
                Heading = new MsgQuaternion
                {
                    X = player.Rotation.xf,
                    Y = player.Rotation.yf,
                    Z = player.Rotation.zf,
                    W = player.Rotation.wf
                },
                Coordinates = new MsgVector3
                {
                    X = player.Position.xf,
                    Y = player.Position.yf,
                    Z = player.Position.zf
                },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            };
        }
    }
}
