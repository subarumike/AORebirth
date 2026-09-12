namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    public sealed class TeleportCommand : IGmCommand
    {
        private readonly Lazy<PlayfieldManager> _playfieldManager;

        public TeleportCommand(Lazy<PlayfieldManager> playfieldManager)
        {
            ArgumentNullException.ThrowIfNull(playfieldManager);
            _playfieldManager = playfieldManager;
        }

        public string Name => "tp";

        public int RequiredGmLevel => 1;

        public string Usage => ".tp <x> <z> <playfieldId> or .tp <x> <y> <z> <playfieldId>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            bool hasExplicitY = context.Args.Length >= 4;
            int zIndex = hasExplicitY ? 2 : 1;
            int playfieldIndex = hasExplicitY ? 3 : 2;
            if (context.Args.Length < 3
                || !float.TryParse(context.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(context.Args[zIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                || !int.TryParse(context.Args[playfieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int playfieldId))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            Player subject = context.ResolveSubject();
            Playfield? playfield = subject.Playfield;
            if (playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            float y = subject.Position.yf;
            if (hasExplicitY
                && !float.TryParse(context.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }
            Vector3 landing = new Vector3(x, y, z);
            string who = ReferenceEquals(subject, context.Player)
                ? "self"
                : (string.IsNullOrEmpty(subject.Name)
                    ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                    : subject.Name);

            if (playfieldId != playfield.Identity.Instance)
            {
                if (subject.Session == null)
                {
                    GmCommandFeedback.Send(context.Session, context.Player, "Target has no session.");
                    return;
                }

                // A GM jump is not a proxy entry, so it leaves no way back: exit proxies in the
                // destination will decline until the player walks in through a real door.
                subject.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                subject.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);

                Playfield destination = _playfieldManager.Value.GetOrCreate(playfieldId);

                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Teleported {0} to ({1}, {2}, {3}) pf={4}",
                        who,
                        x,
                        y,
                        z,
                        playfieldId));

                if (!ReferenceEquals(subject, context.Player))
                {
                    GmCommandFeedback.Send(
                        subject.Session,
                        subject,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Teleported to ({0}, {1}, {2}) pf={3}",
                            x,
                            y,
                            z,
                            playfieldId));
                }

                subject.Session.TransferToPlayfield(destination, landing);
                return;
            }

            subject.Position = landing;

            CharDCMoveMessage move = new CharDCMoveMessage
            {
                Identity = subject.Identity,
                Unknown = 0x00,
                MoveType = (byte)MovementAction.FullStop,
                Heading = new MsgQuaternion
                {
                    X = subject.Rotation.xf,
                    Y = subject.Rotation.yf,
                    Z = subject.Rotation.zf,
                    W = subject.Rotation.wf
                },
                Coordinates = new MsgVector3
                {
                    X = subject.Position.xf,
                    Y = subject.Position.yf,
                    Z = subject.Position.zf
                },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            };

            playfield.GetRequiredService<PlayfieldLocality>().Announce(subject, move, includeSelf: true);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Teleported {0} to ({1}, {2}, {3}) pf={4}",
                    who,
                    x,
                    y,
                    z,
                    playfieldId));

            if (!ReferenceEquals(subject, context.Player) && subject.Session != null)
            {
                GmCommandFeedback.Send(
                    subject.Session,
                    subject,
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
}
