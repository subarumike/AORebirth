namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// RK mission instances. Interior playfield ids and spawns from capture
    /// <c>20260719-5-different-shape-fo-mish</c> (three shapes: 1419310 / 1419335 / 1419382)
    /// plus legacy enter capture <c>20260718-062936</c> (1413198).
    /// </summary>
    internal static class MissionInstanceService
    {
        // Enabled after capture 20260718-062936 proved enter + finish handshake on live AO.
        internal const bool EntryEnabled = true;

        // Legacy default; ResolveInstancePlayfieldId prefers a captured shape.
        internal const int InstancePlayfieldId = 1413198;

        // Omni Trade / Rome Blue — convenience entrances so a key holder need not travel to the outdoor marker.
        internal const int RomeBluePlayfieldInstance = 735;

        // Fallback spawn if shape lookup fails (legacy 20260718-062936).
        internal const float SpawnX = 1.8f;

        internal const float SpawnY = 5.01f;

        internal const float SpawnZ = 85.01f;

        // Fixed Blue Rome mission entrances (pf 735) — capture 20260717-211215.
        internal static readonly int[] RomeEntranceDoorInstances =
        {
            unchecked((int)0xC00302DF),
            unchecked((int)0xC00202DF),
            unchecked((int)0xC00102DF),
        };

        internal static readonly float[][] RomeEntranceSpots =
        {
            new[] { 582.7546f, 22.25639f, 348.7862f },
            new[] { 582.5867f, 22.25661f, 279.4357f },
            new[] { 608.6941f, 22.25724f, 279.2319f },
        };

        private static readonly object ObjectiveGate = new object();

        private static readonly Dictionary<int, MissionRollType> ObjectiveByPlayfield =
            new Dictionary<int, MissionRollType>();

        internal static bool IsMissionInstancePlayfield(int playfieldInstance)
        {
            if (playfieldInstance == InstancePlayfieldId || playfieldInstance == 1419307)
            {
                return true;
            }

            if (AORebirth.Core.Playfields.MissionInstanceShapeCatalog.IsCapturedShapePlayfield(playfieldInstance))
            {
                return true;
            }

            // Live AO mission interiors are high-band playfield2 ids (~0x15xxxx).
            return playfieldInstance >= 0x150000 && playfieldInstance <= 0x16FFFF;
        }

        internal static void StampObjective(int playfieldInstance, MissionRollType type)
        {
            lock (ObjectiveGate)
            {
                ObjectiveByPlayfield[playfieldInstance] = type;
            }
        }

        internal static bool TryGetStampedObjective(int playfieldInstance, out MissionRollType type)
        {
            lock (ObjectiveGate)
            {
                return ObjectiveByPlayfield.TryGetValue(playfieldInstance, out type);
            }
        }

        internal static bool IsRomeEntranceDoor(int doorInstance)
        {
            for (int i = 0; i < RomeEntranceDoorInstances.Length; i++)
            {
                if (RomeEntranceDoorInstances[i] == doorInstance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the use-target is a mission entrance dynel (Rome house doors, MissionEntrance type,
        /// or a door instance matching an accepted mission's entrance ids).
        /// </summary>
        internal static bool IsMissionEntranceTarget(Identity target)
        {
            if (target.Type == IdentityType.MissionEntrance)
            {
                return true;
            }

            if (IsRomeEntranceDoor(target.Instance))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when the use-target matches a stored outdoor entrance id for this character, or is near
        /// their accepted marker (click-to-enter fallback when no MissionEntrance dynel exists).
        /// </summary>
        internal static bool IsAcceptedMissionEntranceUse(ICharacter character, Identity target)
        {
            if (character == null)
            {
                return false;
            }

            if (IsMissionEntranceTarget(target))
            {
                return true;
            }

            if (target.Type != IdentityType.Door && target.Type != IdentityType.MissionEntrance)
            {
                // Still allow proximity click on any usable target at the marker (client may send Door).
                if (character.Playfield == null)
                {
                    return false;
                }

                return IsNearAcceptedMarker(character, 10.0, 14.0);
            }

            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = 0; i < all.Count; i++)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.EntranceLow != 0
                    && (target.Instance == entry.EntranceLow
                        || (target.Instance & 0xFFFF) == (entry.EntranceLow & 0xFFFF)))
                {
                    return true;
                }

                if (entry.EntranceHigh != 0
                    && (target.Instance == entry.EntranceHigh
                        || (target.Instance & 0xFFFF) == (entry.EntranceHigh & 0xFFFF)))
                {
                    return true;
                }
            }

            return IsNearAcceptedMarker(character, 10.0, 14.0);
        }

        internal static bool IsNearAcceptedMarker(ICharacter character, double horizontalRadius, double verticalRadius)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            int pf = character.Playfield.Identity.Instance;
            float x = character.RawCoordinates.X;
            float y = character.RawCoordinates.Y;
            float z = character.RawCoordinates.Z;
            double radiusSq = horizontalRadius * horizontalRadius;

            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = 0; i < all.Count; i++)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (entry == null || entry.MarkerPlayfield == 0 || entry.MarkerPlayfield != pf)
                {
                    continue;
                }

                double dx = x - entry.MarkerX;
                double dz = z - entry.MarkerZ;
                if (((dx * dx) + (dz * dz)) <= radiusSq && Math.Abs(y - entry.MarkerY) <= verticalRadius)
                {
                    return true;
                }
            }

            return false;
        }

        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            AORebirth.Core.Playfields.MissionShape[] shapes =
                AORebirth.Core.Playfields.MissionInstanceShapeCatalog.Shapes;
            if (shapes == null || shapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            // Rotate among the three captured shapes so each enter can get a different layout.
            int salt = character != null
                           ? character.Identity.Instance ^ Environment.TickCount
                           : Environment.TickCount;
            return shapes[Math.Abs(salt) % shapes.Length].CapturedPlayfieldId;
        }

        internal static MissionRollType ResolveCharacterObjective(ICharacter character)
        {
            if (character == null)
            {
                return MissionRollType.KillPerson;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all == null || all.Count == 0)
            {
                return MissionRollType.KillPerson;
            }

            // Most recently accepted entry is last.
            MissionAcceptedStore.AcceptedMission latest = all[all.Count - 1];
            if (latest == null)
            {
                return MissionRollType.KillPerson;
            }

            return MissionTypeCatalog.TypeFromIcon(latest.MissionIconId);
        }

        internal static bool TryEnterMissionInstance(IZoneClient client)
        {
            if (!EntryEnabled || client == null || client.Controller == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (!MissionKeyGrantService.HasMissionKey(character))
            {
                return false;
            }

            int fromPf = character.Playfield.Identity.Instance;
            int pfId = ResolveInstancePlayfieldId(character);
            MissionRollType objective = ResolveCharacterObjective(character);
            StampObjective(pfId, objective);

            float sx;
            float sy;
            float sz;
            ResolveInteriorSpawn(pfId, out sx, out sy, out sz);

            var pfIdentity = new Identity { Type = IdentityType.Playfield, Instance = pfId };

            character.DoNotDoTimers = false;
            character.Teleport(
                new Coordinate { x = sx, y = sy, z = sz },
                character.Heading,
                pfIdentity);
            AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);

            MissionDiagnostics.Log(
                "ENTRY-TELEPORT char={0} fromPf={1} destPf={2} objective={3} spawn=({4},{5},{6})",
                character.Identity.Instance,
                fromPf,
                pfId,
                MissionTypeCatalog.TypeName(objective),
                sx,
                sy,
                sz);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MissionInstance enter char=" + character.Identity + " pf=" + pfId
                + " objective=" + MissionTypeCatalog.TypeName(objective));
            return true;
        }

        /// <summary>
        /// Exit door inside the instance → outdoor marker for the active mission (or Rome Blue fallback).
        /// </summary>
        internal static bool TryExitMissionInstance(IZoneClient client)
        {
            if (!EntryEnabled || client == null || client.Controller == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (!IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            int destPf;
            float destX;
            float destY;
            float destZ;
            ResolveOutdoorExitDestination(character, out destPf, out destX, out destY, out destZ);

            var pfIdentity = new Identity { Type = IdentityType.Playfield, Instance = destPf };
            character.DoNotDoTimers = false;
            character.Teleport(
                new Coordinate { x = destX, y = destY, z = destZ },
                character.Heading,
                pfIdentity);
            AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);

            MissionDiagnostics.Log(
                "EXIT-TELEPORT char={0} destPf={1} dest=({2},{3},{4})",
                character.Identity.Instance,
                destPf,
                destX,
                destY,
                destZ);
            return true;
        }

        /// <summary>
        /// True when the use-target is an interior door usable for exit (any Door/MissionEntrance inside instance).
        /// </summary>
        internal static bool IsMissionExitDoorTarget(Identity target)
        {
            return target.Type == IdentityType.Door || target.Type == IdentityType.MissionEntrance;
        }

        /// <summary>
        /// Raw shape spawn (exit door) without clearance nudge — used for exit proximity / use.
        /// </summary>
        internal static void ResolveInteriorExitDoor(
            int playfieldId,
            out float x,
            out float y,
            out float z)
        {
            x = SpawnX;
            y = SpawnY;
            z = SpawnZ;
            AORebirth.Core.Playfields.MissionShape shape =
                AORebirth.Core.Playfields.MissionInstanceShapeCatalog.PickShape(playfieldId, null);
            if (shape != null)
            {
                x = shape.SpawnX;
                y = shape.SpawnY;
                z = shape.SpawnZ;
            }
        }

        internal static bool IsNearInteriorExitDoor(
            ICharacter character,
            double horizontalRadius,
            double verticalRadius)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            float doorX;
            float doorY;
            float doorZ;
            ResolveInteriorExitDoor(character.Playfield.Identity.Instance, out doorX, out doorY, out doorZ);
            double dx = character.RawCoordinates.X - doorX;
            double dz = character.RawCoordinates.Z - doorZ;
            double dy = Math.Abs(character.RawCoordinates.Y - doorY);
            return ((dx * dx) + (dz * dz)) <= (horizontalRadius * horizontalRadius) && dy <= verticalRadius;
        }

        /// <summary>
        /// Interior spawn from the captured shape, nudged off the exit door so the player
        /// does not land on the door mesh / walk-on volume.
        /// </summary>
        internal static void ResolveInteriorSpawn(int playfieldId, out float x, out float y, out float z)
        {
            x = SpawnX;
            y = SpawnY;
            z = SpawnZ;
            AORebirth.Core.Playfields.MissionShape shape =
                AORebirth.Core.Playfields.MissionInstanceShapeCatalog.PickShape(playfieldId, null);
            if (shape != null)
            {
                x = shape.SpawnX;
                y = shape.SpawnY;
                z = shape.SpawnZ;
                ApplySpawnDoorClearance(shape.CapturedPlayfieldId, ref x, ref z);
                return;
            }

            // Legacy spawn sits on the exit door — nudge into the corridor.
            z += OutdoorExitMarkerStandoff;
        }

        // Keep exit landing outside the outdoor walk-on entry radius (8m).
        private const float OutdoorExitMarkerStandoff = 12.0f;

        private static void ApplySpawnDoorClearance(int capturedPlayfieldId, ref float x, ref float z)
        {
            switch (capturedPlayfieldId)
            {
                case 1419310:
                case 1419335:
                    // Door on high-X wall; interior / targets are lower X.
                    x -= OutdoorExitMarkerStandoff;
                    break;
                case 1419382:
                    // Door near origin; interior runs toward higher X / lower Z.
                    x += OutdoorExitMarkerStandoff;
                    z -= 8.0f;
                    break;
                default:
                    z += OutdoorExitMarkerStandoff;
                    break;
            }
        }

        /// <summary>
        /// Outdoor exit: accepted marker (or Rome fallback), stood off so proximity entry does not re-fire.
        /// </summary>
        internal static void ResolveOutdoorExitDestination(
            ICharacter character,
            out int destPf,
            out float destX,
            out float destY,
            out float destZ)
        {
            destPf = RomeBluePlayfieldInstance;
            destX = RomeEntranceSpots[0][0];
            destY = RomeEntranceSpots[0][1];
            destZ = RomeEntranceSpots[0][2];

            if (character != null)
            {
                List<MissionAcceptedStore.AcceptedMission> all =
                    MissionAcceptedStore.GetAll(character.Identity.Instance);
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    MissionAcceptedStore.AcceptedMission entry = all[i];
                    if (entry != null && entry.MarkerPlayfield != 0)
                    {
                        destPf = entry.MarkerPlayfield;
                        destX = entry.MarkerX;
                        destY = entry.MarkerY;
                        destZ = entry.MarkerZ;
                        break;
                    }
                }
            }

            destX += OutdoorExitMarkerStandoff;
        }
    }
}
