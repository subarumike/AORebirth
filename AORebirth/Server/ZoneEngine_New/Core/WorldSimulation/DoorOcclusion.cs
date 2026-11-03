namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using N3Lite;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using AoVector3 = AORebirth.Core.Vector.Vector3;
    using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;

    /// <summary>
    /// Doors as line-of-sight blockers, open and closed as every client shows them. Gamecode.dll makes a door
    /// non-repulsive and asks for its contacts (0x1011687B); a character touching a closed, unlocked door opens it
    /// (Door_t contact handler 0x1007FAA9 calls Open, vtable +0xAC). Each client runs this for every character it
    /// sees, and retail sends nothing as doors open and close in play (capture: a character ran through several
    /// doors, only movement followed). A live retail door log shows the door stays open exactly while a character is
    /// in contact and closes as the last one leaves: template 300548's contact reaches 2.70 m out at the same floor,
    /// and every open came at 2.44-2.63 m and every close at 2.69-2.89 m, whether running through (0.42 s open) or
    /// standing in the doorway (10.71 s). So a door here opens on contact and closes when its contact ends; an
    /// arriving client gets each static door's state in a DoorStatusUpdate at zone-in.
    /// <para>
    /// Any living character in the sphere holds the door open, one standing still included: retail clients show doors
    /// open with an NPC standing in them, and the retail server sends that state (DoorStatusUpdate open=1 among the
    /// spawns of a visibility refresh). A client sees a character move in or out and moves the door itself, so those
    /// changes send nothing. A change no client can see - a character that appears inside (a spawn) or vanishes from
    /// inside (a despawn or a death) - is sent to everyone as a DoorStatusUpdate, as retail sends a door's state
    /// alongside the spawns of NPCs standing in it.
    /// </para>
    /// <para>
    /// A door is a vertical panel through its position, across its heading. Its template's collision sphere
    /// (DynelCollision.json, the sphere the client's contacts use) gives the panel's height (twice the sphere centre)
    /// and half width (the rest of the radius), and the touch reach (sphere radius plus the player's).
    /// </para>
    /// </summary>
    public sealed class DoorOcclusion
    {
        /// <summary>Seconds between contact checks: how far the server may trail a client's door.</summary>
        const double TouchIntervalSeconds = 0.1;

        /// <summary>
        /// How far outside the touch sphere a character is watched, so an approach is seen before contact. A runner
        /// covers about 1 m per check.
        /// </summary>
        const float ApproachMargin = 3f;

        readonly List<Blocker> _blockers = new();
        readonly Dictionary<Identity, StaticDynel> _staticDoors = new();
        readonly Dictionary<Identity, bool> _closeInhibited = new();
        readonly List<Character> _nearby = new();
        readonly float _touchRadius;
        readonly float _touchCenterY;
        double _sinceTouch;

        public DoorOcclusion(float playerRadius, float playerCenterY)
        {
            _touchRadius = playerRadius;
            _touchCenterY = playerCenterY;
        }

        public int Count => _blockers.Count;

        /// <summary>Raised when a door changes state, so cached sight lines through it are dropped.</summary>
        public event Action? Changed;

        /// <summary>
        /// Binds placed door stats after template and placement overrides have been applied. State publication
        /// does not require a collision sphere; a door without one still receives trigger and zone-in updates.
        /// </summary>
        public void BindStaticDoor(StaticDynel door)
        {
            ArgumentNullException.ThrowIfNull(door);
            if (door.Identity.Type != IdentityType.Door)
                return;

            _staticDoors[door.Identity] = door;
            _closeInhibited.TryAdd(door.Identity, false);
            foreach (Blocker blocker in _blockers)
            {
                if (blocker.StaticIdentity == door.Identity)
                    blocker.StaticEntity = door;
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Writes the bound door's complete flags, then publishes an open/lock change. Returns true for a bound
        /// door, including an unchanged state; callers must not write Flags before calling this method.
        /// </summary>
        public bool ApplyDoorFlags(PlayfieldType playfield, StaticDynel door, int flags, bool? closeInhibit = null)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(door);
            if (!_staticDoors.TryGetValue(door.Identity, out StaticDynel? bound) || !ReferenceEquals(bound, door))
                return false;

            int previous = door.Stats.GetOrZero(CharacterStat.Flags);
            bool previousInhibit = _closeInhibited[door.Identity];
            bool inhibit = closeInhibit ?? previousInhibit;
            door.Stats.Set(CharacterStat.Flags, flags);
            _closeInhibited[door.Identity] = inhibit;
            if (((previous ^ flags) & (Door.OpenFlag | Door.LockedFlag)) != 0 || previousInhibit != inhibit)
            {
                Changed?.Invoke();
                SendStaticStatus(playfield, door.Identity, flags, inhibit);
            }
            return true;
        }

        /// <summary>A quest dungeon's door entity: its own flags say open and locked.</summary>
        public void Add(Door door, float radius, float centerY)
        {
            ArgumentNullException.ThrowIfNull(door);
            Add(new Blocker(door, door.Position, door.Rotation.yf, door.Rotation.wf, radius, centerY,
                door.IsOpen, door.IsLocked));
        }

        /// <summary>A door placed in the playfield's static data (Dynels.dat), which the client creates itself.</summary>
        public void Add(int doorInstance, AoVector3 position, float rotationY, float rotationW, float radius, float centerY,
            int flags)
        {
            var identity = new Identity { Type = IdentityType.Door, Instance = doorInstance };
            _staticDoors.TryGetValue(identity, out StaticDynel? door);
            Add(new Blocker(null, position, rotationY, rotationW, radius, centerY,
                (flags & Door.OpenFlag) != 0, (flags & Door.LockedFlag) != 0)
            {
                StaticIdentity = identity,
                StaticEntity = door
            });
        }

        /// <summary>
        /// Puts an arriving client's copy of every static door on the server's state, as retail's zone-in sends a
        /// DoorStatusUpdate per door. Quest dungeon doors carry theirs in their spawn.
        /// </summary>
        public void SendStates(Network.IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            foreach (StaticDynel door in _staticDoors.Values)
            {
                int flags = door.Stats.GetOrZero(CharacterStat.Flags);
                session.Send(Door.StatusMessage(door.Identity, (flags & Door.OpenFlag) != 0, (flags & Door.LockedFlag) != 0,
                    _closeInhibited[door.Identity]));
            }
            foreach (Blocker d in _blockers)
            {
                if (d.StaticIdentity is { } identity && !_staticDoors.ContainsKey(identity))
                    session.Send(Door.StatusMessage(identity, d.IsOpen, d.IsLocked));
            }
        }

        void Add(Blocker blocker)
        {
            if (blocker.Height <= 0f || blocker.HalfWidth <= 0f)
                return;
            _blockers.Add(blocker);
            Changed?.Invoke();
        }

        /// <summary>True when the segment passes through a closed door's panel.</summary>
        public bool Blocks(Vec3 from, Vec3 to)
        {
            float minX = MathF.Min(from.X, to.X), maxX = MathF.Max(from.X, to.X);
            float minZ = MathF.Min(from.Z, to.Z), maxZ = MathF.Max(from.Z, to.Z);
            foreach (Blocker d in _blockers)
            {
                if (d.IsOpen
                    || maxX < d.X - d.HalfWidth || minX > d.X + d.HalfWidth
                    || maxZ < d.Z - d.HalfWidth || minZ > d.Z + d.HalfWidth)
                    continue;

                // Signed distances to the panel's plane (vertical, normal = its facing).
                float sa = ((from.X - d.X) * d.FacingX) + ((from.Z - d.Z) * d.FacingZ);
                float sb = ((to.X - d.X) * d.FacingX) + ((to.Z - d.Z) * d.FacingZ);
                if ((sa > 0f && sb > 0f) || (sa < 0f && sb < 0f) || sa == sb)
                    continue;

                float t = sa / (sa - sb);
                float hx = from.X + ((to.X - from.X) * t) - d.X;
                float hy = from.Y + ((to.Y - from.Y) * t) - d.Y;
                float hz = from.Z + ((to.Z - from.Z) * t) - d.Z;
                if (MathF.Abs((hx * d.AcrossX) + (hz * d.AcrossZ)) <= d.HalfWidth && hy >= 0f && hy <= d.Height)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Opens every closed, unlocked door a living character is touching, and closes every door whose contact
        /// ended. A door that is open with nobody having touched it (spawned open, or opened by Use) stays open until
        /// someone passes through it. An authored close-inhibit latch keeps an unlocked door open after contact.
        /// </summary>
        public void Tick(PlayfieldType playfield, double deltaTime)
        {
            _sinceTouch += deltaTime;
            if (_sinceTouch < TouchIntervalSeconds || _blockers.Count == 0)
                return;
            _sinceTouch = 0;

            PlayfieldLocality? locality = playfield.GetService<PlayfieldLocality>();
            if (locality == null)
                return;

            bool changed = false;
            foreach (Blocker d in _blockers)
            {
                if (d.IsLocked && !d.IsOpen)
                    continue;

                bool touched = IsTouched(locality, d, out bool sendOpen, out bool sendClose);
                if (touched && !d.IsOpen)
                {
                    d.Open();
                    changed = true;
                    if (sendOpen)
                        SendStatus(playfield, d);
                }
                else if (!touched && d.Touched && d.IsOpen && !(IsCloseInhibited(d) && !d.IsLocked))
                {
                    d.Close();
                    changed = true;
                    if (sendClose)
                        SendStatus(playfield, d);
                }

                d.Touched = touched;
            }

            _nearby.Clear();
            if (changed)
                Changed?.Invoke();
        }

        /// <summary>
        /// True while a living character is in the sphere. <paramref name="sendOpen"/>: a character came into contact
        /// without clients seeing it move in - it was neither inside nor just outside last check (it spawned there).
        /// <paramref name="sendClose"/>: one left without being seen to move out - it is neither inside nor just outside
        /// now (it despawned or died there).
        /// </summary>
        bool IsTouched(PlayfieldLocality locality, Blocker d, out bool sendOpen, out bool sendClose)
        {
            sendOpen = false;
            float reach = d.Radius + _touchRadius;
            float watch = reach + ApproachMargin;
            locality.CollectNear(new AoVector3(d.X, d.Y, d.Z), watch, _nearby);
            d.NextInContact.Clear();
            d.NextApproaching.Clear();
            foreach (Character character in _nearby)
            {
                if (character.IsDead)
                    continue;
                float dx = character.Position.xf - d.X;
                float dy = character.Position.yf + _touchCenterY - (d.Y + d.CenterY);
                float dz = character.Position.zf - d.Z;
                float distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
                if (distanceSquared <= reach * reach)
                {
                    d.NextInContact.Add(character);
                    if (!d.InContact.Contains(character) && !d.Approaching.Contains(character))
                        sendOpen = true;
                }
                else if (distanceSquared <= watch * watch)
                {
                    d.NextApproaching.Add(character);
                }
            }

            sendClose = false;
            foreach (Character previous in d.InContact)
            {
                if (!d.NextInContact.Contains(previous) && !d.NextApproaching.Contains(previous))
                {
                    sendClose = true;
                    break;
                }
            }

            d.SwapContacts();
            return d.InContact.Count > 0;
        }

        /// <summary>Tells every client the door's state: a quest dungeon door to those who see it, a static door to the playfield.</summary>
        bool IsCloseInhibited(Blocker door)
            => door.Entity?.CloseInhibited ?? (door.StaticIdentity is { } identity
                && _closeInhibited.TryGetValue(identity, out bool inhibit) && inhibit);

        void SendStatus(PlayfieldType playfield, Blocker d)
        {
            if (d.Entity is Door door)
            {
                door.AnnounceStatus();
                return;
            }

            if (d.StaticIdentity is not { } identity)
                return;
            var message = Door.StatusMessage(identity, d.IsOpen, d.IsLocked, IsCloseInhibited(d));
            foreach (Player player in playfield.GetRequiredService<DynelRegistry>().PlayerEntities())
                player.Session?.Send(message);
        }

        static void SendStaticStatus(PlayfieldType playfield, Identity identity, int flags, bool closeInhibit)
        {
            var message = Door.StatusMessage(identity, (flags & Door.OpenFlag) != 0, (flags & Door.LockedFlag) != 0, closeInhibit);
            foreach (Player player in playfield.GetRequiredService<DynelRegistry>().PlayerEntities())
                player.Session?.Send(message);
        }

        sealed class Blocker
        {
            readonly Door? _door;
            bool _open;
            readonly bool _locked;

            public Blocker(Door? door, AoVector3 position, float rotationY, float rotationW, float radius, float centerY,
                bool open, bool locked)
            {
                _door = door;
                _open = open;
                _locked = locked;
                X = position.xf;
                Y = position.yf;
                Z = position.zf;
                Radius = radius;
                CenterY = centerY;
                Height = 2f * centerY;
                HalfWidth = MathF.Sqrt(MathF.Max(0f, (radius * radius) - (centerY * centerY)));

                // Doors turn about the vertical axis only: facing is +Z turned by the yaw, across is +X.
                float yaw = 2f * MathF.Atan2(rotationY, rotationW);
                float sin = MathF.Sin(yaw), cos = MathF.Cos(yaw);
                AcrossX = cos;
                AcrossZ = -sin;
                FacingX = sin;
                FacingZ = cos;
            }

            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public float Radius { get; }
            public float CenterY { get; }
            public float Height { get; }
            public float HalfWidth { get; }
            public float AcrossX { get; }
            public float AcrossZ { get; }
            public float FacingX { get; }
            public float FacingZ { get; }

            /// <summary>Set for a door from the playfield data; a quest dungeon door is its entity's.</summary>
            public Identity? StaticIdentity { get; init; }

            public Door? Entity => _door;

            public StaticDynel? StaticEntity { get; set; }

            public bool IsOpen => _door?.IsOpen ?? (StaticEntity is { } door
                ? (door.Stats.GetOrZero(CharacterStat.Flags) & Door.OpenFlag) != 0 : _open);

            public bool IsLocked => _door?.IsLocked ?? (StaticEntity is { } door
                ? (door.Stats.GetOrZero(CharacterStat.Flags) & Door.LockedFlag) != 0 : _locked);

            /// <summary>A character was in contact at the last check.</summary>
            public bool Touched { get; set; }

            /// <summary>Characters that moved into the sphere and are still in it.</summary>
            public HashSet<Character> InContact { get; private set; } = new();

            /// <summary>Characters just outside the sphere at the last check.</summary>
            public HashSet<Character> Approaching { get; private set; } = new();

            public HashSet<Character> NextInContact { get; private set; } = new();

            public HashSet<Character> NextApproaching { get; private set; } = new();

            /// <summary>The sets built this check become current; the old ones are reused next check.</summary>
            public void SwapContacts()
            {
                (InContact, NextInContact) = (NextInContact, InContact);
                (Approaching, NextApproaching) = (NextApproaching, Approaching);
                NextInContact.Clear();
                NextApproaching.Clear();
            }

            public void Open()
            {
                if (_door != null)
                    _door.OpenByTouch();
                else if (StaticEntity is { } door)
                    door.Stats.Set(CharacterStat.Flags, door.Stats.GetOrZero(CharacterStat.Flags) | Door.OpenFlag);
                else
                    _open = true;
            }

            public void Close()
            {
                if (_door != null)
                    _door.CloseAfterTouch();
                else if (StaticEntity is { } door)
                    door.Stats.Set(CharacterStat.Flags, door.Stats.GetOrZero(CharacterStat.Flags) & ~Door.OpenFlag);
                else
                    _open = false;
            }
        }
    }
}
