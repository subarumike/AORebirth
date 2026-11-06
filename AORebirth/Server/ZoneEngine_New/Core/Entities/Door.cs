namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

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
        public int Flags
        {
            get => Stats.GetOrZero(CharacterStat.Flags);
            private set => Stats.Set(CharacterStat.Flags, value);
        }

        /// <summary>
        /// DoorFullUpdate Unknown7 in live ACG dungeons: (room &lt;&lt; 16) | linked room; the exit is 0xFFFF0000
        /// (outside to the entrance room).
        /// </summary>
        public int RoomLink { get; }

        public bool IsOpen => (Flags & OpenFlag) != 0;

        public bool IsLocked => (Flags & LockedFlag) != 0;

        /// <summary>Authored open state held until an explicit close/lock; ordinary touch doors default false.</summary>
        public bool CloseInhibited { get; private set; }

        /// <summary>Applies authoritative flags without treating trigger signal bits as door flags.</summary>
        public void ApplyFlags(int flags, bool? closeInhibit = null)
        {
            int previous = Flags;
            bool previousInhibit = CloseInhibited;
            Flags = flags;
            if (closeInhibit is bool inhibit)
                CloseInhibited = inhibit;
            if (((previous ^ flags) & (OpenFlag | LockedFlag)) == 0 && previousInhibit == CloseInhibited)
                return;

            Playfield?.WorldAccess.Instance?.InvalidateLineOfSight();
            AnnounceStatus();
        }

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
                Playfield = Playfield?.ClientPlayfieldId ?? 0,
                Unknown1 = new Identity { Type = (IdentityType)0xF424F, Instance = 1 },
                Unknown2 = 0,
                Unknown3 = 0x6F,
                Stats =
                [
                    Stat(CharacterStat.Flags, unchecked((uint)Flags)),
                    Stat(CharacterStat.StaticInstance, (uint)TemplateId),
                    Stat(CharacterStat.ACGItemLevel, 0),
                    Stat(CharacterStat.ACGItemTemplateID, 0),
                    Stat(CharacterStat.ACGItemTemplateID2, 0),
                    Stat(CharacterStat.MultipleCount, 1),
                    Stat(CharacterStat.MetersWalked, 0),
                    Stat(CharacterStat.ExternalPlayfieldInstance, 0),
                    Stat(CharacterStat.ExternalDoorInstance, 0),
                    Stat(CharacterStat.AccessKey, 0),
                    Stat(CharacterStat.DoorFlags, 0),
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

        /// <summary>The full spawn carries flags, but an authored close-inhibit latch needs the status message.</summary>
        public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
        {
            if (CloseInhibited)
            {
                yield return StatusMessage(Identity, IsOpen, IsLocked, closeInhibit: true);
            }
        }

        /// <summary>Opens or closes the door for a character standing at it, unless it is locked.</summary>
        public bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (IsLocked || Playfield == null || player.IsDead || !ReferenceEquals(player.Playfield, Playfield)
                || GetEdgeDistanceTo(player) > UseRange)
                return false;

            Flags ^= OpenFlag;
            Playfield.WorldAccess.Instance?.InvalidateLineOfSight();
            var locality = Playfield.GetRequiredService<PlayfieldLocality>();
            locality.Announce(this, BuildSpawnMessage(), includeSelf: true);
            // A client that already has the door ignores a second full update; the status update moves it.
            locality.Announce(this, StatusMessage(Identity, IsOpen, IsLocked, CloseInhibited), includeSelf: true);
            return true;
        }

        /// <summary>
        /// DoorStatusUpdate: sets a door the client already has open or closed, locked or unlocked (Gamecode.dll
        /// DoorStatusUpdateIIR_t, read 0x100A010A, applied 0x1009FF34). The body is version 2, locked, open,
        /// AccessKey (stat 195), a close-inhibit byte and an identity list; retail sends these at zone-in and on
        /// authored state changes. The PF665 capture includes closed/unlocked, open/unlocked and closed/locked
        /// states; its empty identity list is the four-byte encoded count 000003F1.
        /// </summary>
        // A true close-inhibit byte sets the client's latch. False does not clear an already set native latch;
        // explicit locked/closed state must therefore perform a relock rather than relying on that byte alone.
        public static DoorStatusUpdateMessage StatusMessage(Identity door, bool open, bool locked, bool closeInhibit = false) => new()
        {
            Identity = door,
            Unknown = 0,
            Unknown1 = 2,
            Unknown2 = (byte)(locked ? 1 : 0),
            Unknown3 = (byte)(open ? 1 : 0),
            Unknown4 = 0,
            Unknown5 = (byte)(closeInhibit ? 1 : 0),
            Unknown6 = []
        };

        /// <summary>
        /// A character walked into the closed, unlocked door: every client opens it on that contact (Gamecode.dll
        /// Door_t contact handler, 0x1007FAA9), so it is open here too, and new viewers get it open in the spawn.
        /// </summary>
        public bool OpenByTouch()
        {
            if (IsOpen || IsLocked)
                return false;
            Flags |= OpenFlag;
            return true;
        }

        /// <summary>Sends the door's state to everyone who sees it (a change no client saw happen).</summary>
        public void AnnounceStatus()
            => Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, StatusMessage(Identity, IsOpen, IsLocked, CloseInhibited), includeSelf: true);

        /// <summary>The last character in contact with the door left it: every client closes it then.</summary>
        public bool CloseAfterTouch()
        {
            if (!IsOpen)
                return false;
            Flags &= ~OpenFlag;
            return true;
        }

        /// <summary>
        /// A Break and Enter item used on the locked door: it unlocks and opens for everyone nearby (the client's
        /// unlock result does both).
        /// </summary>
        public BreakAndEnterResult TryBreakAndEnter(Player player, int pickRating)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!IsLocked || Playfield == null || player.IsDead || !ReferenceEquals(player.Playfield, Playfield)
                || GetEdgeDistanceTo(player) > UseRange)
                return BreakAndEnterResult.Refused;

            if (pickRating < LockDifficulty)
            {
                BreakAndEnterActions.SendFailed(this, player);
                return BreakAndEnterResult.Failed;
            }

            Flags = (Flags & ~LockedFlag) | OpenFlag;
            Playfield.WorldAccess.Instance?.InvalidateLineOfSight();
            Playfield.GetRequiredService<PlayfieldLocality>().Announce(this,
                BreakAndEnterActions.Result(Identity, player.Identity, BreakAndEnterActions.Unlocked), includeSelf: true);
            return BreakAndEnterResult.Unlocked;
        }

        /// <summary>Heading for a door turned <paramref name="yawDegrees"/> about the vertical axis.</summary>
        public static AORebirth.Core.Vector.Quaternion Heading(int yawDegrees)
        {
            double half = yawDegrees * Math.PI / 360.0;
            return new AORebirth.Core.Vector.Quaternion(0, Math.Sin(half), 0, Math.Cos(half));
        }
    }
}
