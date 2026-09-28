namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;
    using System.IO;

    using AORebirth.Core.GameData;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
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
        private readonly IGameData _gameData;

        public TeleportCommand(Lazy<PlayfieldManager> playfieldManager, IGameData gameData)
        {
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(gameData);
            _playfieldManager = playfieldManager;
            _gameData = gameData;
        }

        public string Name => "tp";

        public int RequiredGmLevel => 1;

        public string Usage =>
            ".tp <x> <z> <playfieldId> or .tp <x> <y> <z> <playfieldId> (look-at player if selected) | .tp summon <character name>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "summon", StringComparison.OrdinalIgnoreCase))
            {
                Summon(context);
                return;
            }

            bool hasExplicitY = context.Args.Length >= 4;
            int zIndex = hasExplicitY ? 2 : 1;
            int playfieldIndex = hasExplicitY ? 3 : 2;
            if (context.Args.Length < 3
                || !float.TryParse(context.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(context.Args[zIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                || !int.TryParse(context.Args[playfieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int playfieldId)
                || playfieldId <= 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            string playfieldDir = Path.Combine(
                _gameData.RootPath,
                GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
            if (!Directory.Exists(playfieldDir))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unknown playfield id: {0}",
                        playfieldId));
                return;
            }

            // Look-at player is the subject; do not silently teleport self when target is set.
            if (!context.TryResolveSubject(out Player subject, requirePlayerTarget: true))
                return;

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

                // Building a playfield that is not loaded yet takes a moment; say so before it starts.
                if (!_playfieldManager.Value.TryGet(playfieldId, out _))
                    GmCommandFeedback.Send(context.Session, context.Player, "Initializing playfield, please wait...");

                Relocate(context, subject, _playfieldManager.Value.GetOrCreate(playfieldId), landing, who);
                return;
            }

            Relocate(context, subject, playfield, landing, who);
        }

        /// <summary>
        /// <c>.tp summon &lt;name&gt;</c>: brings an online character (matched by name, ignoring case) to the issuer's
        /// playfield and position, including into a quest dungeon the issuer is in.
        /// </summary>
        void Summon(GmCommandContext context)
        {
            if (context.Args.Length < 2)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: .tp summon <character name>");
                return;
            }

            string name = string.Join(" ", context.Args, 1, context.Args.Length - 1).Trim();
            Player? subject = null;
            foreach (Player online in _playfieldManager.Value.SnapshotPlayers())
            {
                if (string.Equals(online.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    subject = online;
                    break;
                }
            }

            if (subject == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "No online character named " + name + ".");
                return;
            }

            if (ReferenceEquals(subject, context.Player))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "You cannot summon yourself.");
                return;
            }

            if (context.Player.Playfield is not Playfield destination)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            if (subject.Playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, subject.Name + " is not on a playfield.");
                return;
            }

            Vector3 landing = new Vector3(context.Player.Position.xf, context.Player.Position.yf, context.Player.Position.zf);
            Relocate(context, subject, destination, landing, subject.Name);
        }

        /// <summary>
        /// Moves <paramref name="subject"/> to <paramref name="landing"/> on <paramref name="destination"/>: a zone
        /// transfer to another playfield, or a position update announced where they are. Tells the issuer and, when it
        /// is someone else, the subject.
        /// </summary>
        void Relocate(GmCommandContext context, Player subject, Playfield destination, Vector3 landing, string who)
        {
            Playfield? current = subject.Playfield;
            if (current == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return;
            }

            int playfieldId = destination.Identity.Instance;
            string toIssuer = string.Format(CultureInfo.InvariantCulture, "Teleported {0} to ({1}, {2}, {3}) pf={4}",
                who, landing.xf, landing.yf, landing.zf, playfieldId);
            string toSubject = string.Format(CultureInfo.InvariantCulture, "Teleported to ({0}, {1}, {2}) pf={3}",
                landing.xf, landing.yf, landing.zf, playfieldId);

            if (!ReferenceEquals(destination, current))
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

                GmCommandFeedback.Send(context.Session, context.Player, toIssuer);
                if (!ReferenceEquals(subject, context.Player))
                    GmCommandFeedback.Send(subject.Session, subject, toSubject);

                subject.Session.TransferToPlayfield(destination, landing);
                return;
            }

            subject.Position = landing;
            current.GetRequiredService<PlayfieldLocality>().Announce(subject, new CharDCMoveMessage
            {
                Identity = subject.Identity,
                Unknown = 0x00,
                MoveType = (byte)MovementAction.FullStop,
                Heading = new MsgQuaternion { X = subject.Rotation.xf, Y = subject.Rotation.yf, Z = subject.Rotation.zf, W = subject.Rotation.wf },
                Coordinates = new MsgVector3 { X = subject.Position.xf, Y = subject.Position.yf, Z = subject.Position.zf },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            }, includeSelf: true);

            GmCommandFeedback.Send(context.Session, context.Player, toIssuer);
            if (!ReferenceEquals(subject, context.Player) && subject.Session != null)
                GmCommandFeedback.Send(subject.Session, subject, toSubject);
        }
    }
}
