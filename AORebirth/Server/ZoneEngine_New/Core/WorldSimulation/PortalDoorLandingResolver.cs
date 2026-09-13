namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;

    using AodbEventType = AODB.Common.Enums.EventType;
    using AodbFunctionOperator = AODB.Common.Enums.FunctionOperator;
    using AodbFunctionType = AODB.Common.Enums.FunctionType;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>How a portal's landing coordinate is looked up in the destination playfield.</summary>
    public enum PortalLandingKind
    {
        /// <summary>Stand in front of a specific door dynel. Used by TeleportProxy.</summary>
        DoorDynel,

        /// <summary>Stand beside a numbered line in Destinations.dat. Used by LineTeleport.</summary>
        DestinationLine
    }

    /// <summary>
    /// The door a character walked in through, so an exit proxy can send them back out of it. This
    /// is the pair of return stats legacy keeps on the character rather than anything static.
    /// </summary>
    public readonly record struct ProxyReturn
    {
        public int PlayfieldId { get; init; }

        public int DoorInstance { get; init; }

        public bool IsSet => PlayfieldId > 0 && DoorInstance != 0;
    }

    /// <summary>Where a portal sends a character, as read out of Dynels.dat.</summary>
    public readonly record struct PortalDestination
    {
        /// <summary>Destination playfield, or 0 meaning the door's own playfield.</summary>
        public int PlayfieldId { get; init; }

        public PortalLandingKind Kind { get; init; }

        /// <summary>Target door instance when <see cref="Kind"/> is DoorDynel.</summary>
        public int DoorInstance { get; init; }

        /// <summary>Destinations.dat key when <see cref="Kind"/> is DestinationLine.</summary>
        public byte DestinationIndex { get; init; }

        /// <summary>How far in front of the target door to place the character.</summary>
        public float DoorClearance { get; init; }

        /// <summary>
        /// True when the destination door becomes a way back out. Only TeleportProxy does this;
        /// TeleportProxy2 clears the return instead, so its destinations are one-way.
        /// </summary>
        public bool RecordsReturn { get; init; }
    }

    /// <summary>
    /// Reads playfield-to-playfield door portals out of Dynels.dat and resolves where they land.
    /// <para>
    /// The two teleport functions a walk-in door can carry resolve their landing from entirely
    /// different data, so the function type decides the lookup:
    /// </para>
    /// <list type="bullet">
    /// <item><b>TeleportProxy</b> / <b>TeleportProxy2</b> name a door directly:
    /// <c>{IdentityType.PlayfieldDoor, destPlayfieldId, destDoorIndex, sourceDoorInstance}</c>. The
    /// target is the dynel <c>0xC0000000 | destPlayfieldId | (destDoorIndex &lt;&lt; 16)</c> in the
    /// destination playfield, and the character is placed
    /// <see cref="ProxyEntryDoorClearance"/> (or <see cref="Proxy2EntryDoorClearance"/>) units in
    /// front of it along its heading — landing on the door itself puts you inside its frame.</item>
    /// <item><b>LineTeleport</b> names a <em>destination line</em>, not a door:
    /// <c>{IdentityType.Playfield3, packed, destPlayfieldId}</c>, where the Destinations.dat key is
    /// <c>packed &gt;&gt; 16</c> and a <c>destPlayfieldId</c> of 0 means the door's own playfield.
    /// The landing is that line's midpoint pushed <see cref="LineLandingOffset"/> units to its
    /// left, which is the same data wall borders land on.</item>
    /// </list>
    /// <para>
    /// The low 16 bits of <c>packed</c> happen to repeat the destination playfield, which makes the
    /// two layouts look interchangeable on most records; they are not, because the index means a
    /// door in one and a destination line in the other. Any other destination type (whompas,
    /// apartments) is resolved from server state at runtime and is not baked as a static portal.
    /// </para>
    /// </summary>
    public static class PortalDoorLandingResolver
    {
        /// <summary>Forward clearance TeleportProxy leaves in front of the destination door.</summary>
        public const float ProxyEntryDoorClearance = 5.0f;

        /// <summary>TeleportProxy2 uses a tighter clearance than TeleportProxy.</summary>
        public const float Proxy2EntryDoorClearance = 2.5f;

        /// <summary>Clearance an exit proxy leaves in front of the door it puts you back out of.</summary>
        public const float ExitDoorClearance = 2.5f;

        /// <summary>Sideways offset LineTeleport leaves from its destination line.</summary>
        public const float LineLandingOffset = 4.0f;

        /// <summary>Events raised when a character walks into a door rather than using it.</summary>
        static readonly AodbEventType[] WalkInEvents = [AodbEventType.OnEnter, AodbEventType.OnCollide];

        static readonly (AodbFunctionType Function, float Clearance, bool RecordsReturn)[] ProxyFunctions =
        [
            (AodbFunctionType.TeleportProxy, ProxyEntryDoorClearance, true),
            (AodbFunctionType.TeleportProxy2, Proxy2EntryDoorClearance, false)
        ];

        /// <summary>
        /// Reads the static playfield portal a dynel exposes on walk-in, if it has one.
        /// </summary>
        public static bool TryReadPortal(PlayfieldDynel? dynel, out PortalDestination destination)
        {
            destination = default;
            if (dynel?.Modifiers == null)
                return false;

            for (int i = 0; i < WalkInEvents.Length; i++)
            {
                if (!dynel.Modifiers.TryGetValue(WalkInEvents[i], out Modifier? modifier)
                    || modifier?.Modifiers == null)
                    continue;

                if (TryReadProxyPortal(modifier, out destination)
                    || TryReadLinePortal(modifier, out destination))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the destination door in <paramref name="geometry"/> and reports where it puts the
        /// arriving character: <paramref name="clearance"/> units out along the door's heading. A
        /// dynel instance is only unique per identity type — playfield 3084 has a Terminal, a
        /// MailTerminal and a Door all numbered <c>0xC0010C0C</c> — so only
        /// <see cref="IdentityType.Door"/> is considered, matching the legacy door lookup.
        /// </summary>
        public static bool TryResolveDoorLanding(
            PlayfieldGeometryData? geometry,
            int doorInstance,
            float clearance,
            out Vector3 landing)
            => TryResolveDoorLanding(geometry, doorInstance, clearance, out landing, out _);

        /// <summary>Returns the same door heading used to place the arrival away from its frame.</summary>
        public static bool TryResolveDoorLanding(
            PlayfieldGeometryData? geometry,
            int doorInstance,
            float clearance,
            out Vector3 landing,
            out Quaternion heading)
        {
            landing = default!;
            heading = default!;
            List<PlayfieldDynel>? dynels = geometry?.Dynels?.Dynels;
            if (dynels == null)
                return false;

            for (int i = 0; i < dynels.Count; i++)
            {
                PlayfieldDynel door = dynels[i];
                if (door.IdentityInstance != doorInstance || door.IdentityType != (int)IdentityType.Door)
                    continue;

                heading = new Quaternion(door.Heading.X, door.Heading.Y, door.Heading.Z, door.Heading.W);
                var forward = (Vector3)heading.RotateVector3(Vector3.AxisZ);
                landing = new Vector3(
                    door.Position.X + (forward.x * clearance),
                    door.Position.Y,
                    door.Position.Z + (forward.z * clearance));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a LineTeleport landing: the midpoint of the destination line, offset to its left
        /// so the character does not stand on the line itself. Y comes from the line rather than the
        /// character, because a LineTeleport can change floor height.
        /// </summary>
        public static bool TryResolveLineLanding(
            DestinationsCatalog destinations,
            int playfieldId,
            byte destinationIndex,
            out Vector3 landing)
        {
            ArgumentNullException.ThrowIfNull(destinations);
            landing = default!;
            if (!destinations.TryGetDestination(playfieldId, destinationIndex, out PlayfieldDestination? line)
                || line == null)
                return false;

            float spanX = line.EndX - line.StartX;
            float spanZ = line.EndZ - line.StartZ;
            float newX = (spanX * 0.5f) + line.StartX;
            float newZ = (spanZ * 0.5f) + line.StartZ;

            float length = MathF.Sqrt((spanX * spanX) + (spanZ * spanZ));
            if (length <= 1e-4f)
                return false;

            newX -= spanZ / length * LineLandingOffset;
            newZ += spanX / length * LineLandingOffset;

            landing = new Vector3(newX, line.EndY, newZ);
            return true;
        }

        static bool TryReadProxyPortal(Modifier modifier, out PortalDestination destination)
        {
            destination = default;
            for (int i = 0; i < ProxyFunctions.Length; i++)
            {
                (AodbFunctionType function, float clearance, bool recordsReturn) = ProxyFunctions[i];
                if (!TryGetArgumentSets(modifier, function, out List<Dictionary<AodbFunctionOperator, object>>? sets))
                    continue;

                for (int s = 0; s < sets!.Count; s++)
                {
                    if (!TryGetArgumentList(sets[s], out IList? values)
                        || values!.Count < 3
                        || ToInt(values[0]) != (int)IdentityType.PlayfieldDoor)
                        continue;

                    int playfieldId = ToInt(values[1]);
                    int doorIndex = ToInt(values[2]);
                    if (!IsAddressable(playfieldId, doorIndex) || playfieldId <= 0)
                        continue;

                    destination = new PortalDestination
                    {
                        PlayfieldId = playfieldId,
                        Kind = PortalLandingKind.DoorDynel,
                        DoorInstance = ToDoorInstance(playfieldId, doorIndex),
                        DoorClearance = clearance,
                        RecordsReturn = recordsReturn
                    };
                    return true;
                }
            }

            return false;
        }

        static bool TryReadLinePortal(Modifier modifier, out PortalDestination destination)
        {
            destination = default;
            if (!TryGetArgumentSets(
                    modifier,
                    AodbFunctionType.LineTeleport,
                    out List<Dictionary<AodbFunctionOperator, object>>? sets))
                return false;

            for (int s = 0; s < sets!.Count; s++)
            {
                if (!TryGetArgumentList(sets[s], out IList? values)
                    || values!.Count < 3
                    || ToInt(values[0]) != (int)IdentityType.Playfield3)
                    continue;

                // The destination line index sits above the packed playfield id, and a playfield of
                // 0 means "stay here" rather than "no destination".
                uint packed = unchecked((uint)ToInt(values[1]));
                destination = new PortalDestination
                {
                    PlayfieldId = ToInt(values[2]),
                    Kind = PortalLandingKind.DestinationLine,
                    DestinationIndex = (byte)(packed >> 16)
                };
                return true;
            }

            return false;
        }

        static bool TryGetArgumentSets(
            Modifier modifier,
            AodbFunctionType function,
            out List<Dictionary<AodbFunctionOperator, object>>? sets)
            => modifier.Modifiers.TryGetValue(function, out sets) && sets != null;

        static bool TryGetArgumentList(Dictionary<AodbFunctionOperator, object>? arguments, out IList? values)
        {
            values = null;
            if (arguments == null || !arguments.TryGetValue(AodbFunctionOperator.Arg1, out object? raw))
                return false;

            values = raw as IList;
            return values != null;
        }

        // The door instance packs the playfield id into the low 16 bits and the index above it, so
        // anything wider than that is not a playfield id.
        static bool IsAddressable(int playfieldId, int index)
            => playfieldId >= 0 && playfieldId <= 0xFFFF && index >= 0 && index <= 0xFF;

        static int ToDoorInstance(int playfieldId, int index)
            => unchecked((int)(0xC0000000u | (uint)playfieldId | ((uint)index << 16)));

        static int ToInt(object? raw)
        {
            return raw switch
            {
                int i => i,
                short s => s,
                long l => (int)l,
                byte b => b,
                uint u => unchecked((int)u),
                float f => (int)f,
                _ => 0
            };
        }
    }
}
