namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    public sealed class TeleportCommand : IGmCommand
    {
        public string Name => "tp";

        public int RequiredGmLevel => 1;

        public string Usage => ".tp <x> <z> <playfieldId>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 3
                || !float.TryParse(context.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(context.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                || !int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int playfieldId))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            Player player = context.Player;
            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            if (playfieldId != playfield.Identity.Instance)
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    "Cross-playfield teleport is not implemented.");
                return;
            }

            float y = player.Position.yf;
            player.Position = new Vector3(x, y, z);

            CharDCMoveMessage move = new CharDCMoveMessage
            {
                Identity = player.Identity,
                Unknown = 0x00,
                MoveType = (byte)MovementAction.FullStop,
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

            playfield.GetRequiredService<PlayfieldLocality>().Announce(player, move, includeSelf: true);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Teleported to ({0}, {1}, {2}) pf={3}",
                    x,
                    y,
                    z,
                    playfieldId));
        }
    }
}
