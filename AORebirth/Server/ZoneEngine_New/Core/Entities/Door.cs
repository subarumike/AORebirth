namespace ZoneEngine_New.Core.Entities
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// A door dynel (0xC748), sent as DoorFullUpdate: a door item template (StaticInstance, e.g. 41557) whose Flags
    /// stat is the door's base flags, plus its state bits: the client's IsLocked is
    /// HasFlag(0x40) (Gamecode.dll Door_t vtable +0xFC, 0x10085412) and closing a door clears 0x80
    /// (0x1008541A), so 0x80 is open. Using an unlocked door opens or closes it for everyone nearby; a locked door
    /// refuses (the client already shows "It is locked"), but a Break and Enter item unlocks and opens it.
    /// Wire values are from captured live ACG dungeon doors.
    /// </summary>
    public sealed class Door : Dynel, IUsableDynel, IBreakAndEnterTarget
    {
        public const int OpenFlag = 0x80;

        public const int LockedFlag = 0x40;

        /// <summary>How close a character must be to open or close a door (units).</summary>
        const float UseRange = 5f;

        public Door(Identity identity, int templateId, int flags, int roomLink, int lockDifficulty = 0)
            : base(identity)
        {
            TemplateId = templateId;
            Flags = flags;
            RoomLink = roomLink;
            LockDifficulty = Math.Max(0, lockDifficulty);
        }

        /// <summary>Door item template (StaticInstance, stat 23).</summary>
        public int TemplateId { get; }

        /// <summary>Stat 0; see <see cref="OpenFlag"/> and <see cref="LockedFlag"/>.</summary>
        public int Flags { get; private set; }

        /// <summary>
        /// DoorFullUpdate Unknown7 in live ACG dungeons: (room &lt;&lt; 16) | linked room; the exit is 0xFFFF0000
        /// (outside to the entrance room).
        /// </summary>
        public int RoomLink { get; }

        public bool IsOpen => (Flags & OpenFlag) != 0;

        public bool IsLocked => (Flags & LockedFlag) != 0;

        /// <summary>Stat 299, sent while the door is locked so the client lets a lock pick be tried on it.</summary>
        public int LockDifficulty { get; }

        public override MessageBody BuildSpawnMessage()
        {
            static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
            return new DoorFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                MsgVersion = 0x0B,
                Identitytype = 0,
                Instance = 0,
                Coordinate = new SmokeLounge.AOtomation.Messaging.GameData.Vector3(Position.xf, Position.yf, Position.zf),
                Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion
                {
                    X = Rotation.xf,
                    Y = Rotation.yf,
                    Z = Rotation.zf,
                    W = Rotation.wf
                },
                Playfield = Playfield?.Identity.Instance ?? 0,
                Unknown1 = new Identity { Type = (IdentityType)0xF424F, Instance = 1 },
                Unknown2 = 0,
                Unknown3 = 0x6F,
                Stats =
                [
                    Stat(CharacterStat.Flags, unchecked((uint)Flags)),
                    Stat(CharacterStat.StaticInstance, (uint)TemplateId),
                    Stat((CharacterStat)701, 0),
                    Stat((CharacterStat)702, 0),
                    Stat((CharacterStat)703, 0),
                    Stat((CharacterStat)412, 1),
                    Stat((CharacterStat)252, 0),
                    Stat((CharacterStat)192, 0),
                    Stat((CharacterStat)193, 0),
                    Stat((CharacterStat)195, 0),
                    Stat((CharacterStat)259, 0),
                    .. (IsLocked && LockDifficulty > 0 ? [Stat(CharacterStat.LockDifficulty, (uint)LockDifficulty)] : Array.Empty<GameTuple<CharacterStat, uint>>())
                ],
                Name = string.Empty,
                Unknown4 = 2,
                Unknown5 = 50,
                Identities = [],
                Unknown6 = 2,
                Unknown7 = RoomLink
            };
        }

        /// <summary>Opens or closes the door for a character standing at it, unless it is locked.</summary>
        public bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (IsLocked || Playfield == null || player.IsDead || !ReferenceEquals(player.Playfield, Playfield)
                || GetEdgeDistanceTo(player) > UseRange)
                return false;

            Flags ^= OpenFlag;
            Playfield.GetRequiredService<PlayfieldLocality>().Announce(this, BuildSpawnMessage(), includeSelf: true);
            return true;
        }

        /// <summary>
        /// A Break and Enter item used on the locked door: it unlocks and opens for everyone nearby (the client's
        /// unlock result does both).
        /// </summary>
        public bool TryBreakAndEnter(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!IsLocked || Playfield == null || player.IsDead || !ReferenceEquals(player.Playfield, Playfield)
                || GetEdgeDistanceTo(player) > UseRange)
                return false;

            Flags = (Flags & ~LockedFlag) | OpenFlag;
            Playfield.GetRequiredService<PlayfieldLocality>().Announce(this,
                BreakAndEnterActions.Result(Identity, player.Identity, BreakAndEnterActions.Unlocked), includeSelf: true);
            return true;
        }

        /// <summary>Heading for a door turned <paramref name="yawDegrees"/> about the vertical axis.</summary>
        public static AORebirth.Core.Vector.Quaternion Heading(int yawDegrees)
        {
            double half = yawDegrees * Math.PI / 360.0;
            return new AORebirth.Core.Vector.Quaternion(0, Math.Sin(half), 0, Math.Cos(half));
        }
    }
}
