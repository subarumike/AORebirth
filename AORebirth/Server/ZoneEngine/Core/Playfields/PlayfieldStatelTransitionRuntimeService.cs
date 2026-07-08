namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using Utility;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Identity = SmokeLounge.AOtomation.Messaging.GameData.Identity;
    using RuntimeQuaternion = AORebirth.Core.Vector.Quaternion;
    using StatelEvent = AORebirth.Core.Events.Event;

    #endregion

    internal sealed class PlayfieldStatelTransitionRuntimeService
    {
        private const int CapturedMontroyalEntrySourcePlayfieldId = 655;

        private const int CapturedMontroyalPrivateCityInstance = 1196045;

        private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

        private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

        private const float CapturedMontroyalEntrySourceX = 3140.412f;

        private const float CapturedMontroyalEntrySourceY = 51.54391f;

        private const float CapturedMontroyalEntrySourceZ = 799.8611f;

        private const float CapturedMontroyalEntryRadius = 2.5f;

        private const float CapturedMontroyalEntryVerticalTolerance = 6.0f;

        private const float CapturedMontroyalEntryDestinationX = 530.0042f;

        private const float CapturedMontroyalEntryDestinationY = 163.2545f;

        private const float CapturedMontroyalEntryDestinationZ = 580.9957f;

        private const float CapturedOwnedMontroyalEntryDestinationX = 528.6631f;

        private const float CapturedOwnedMontroyalEntryDestinationY = 163.2526f;

        private const float CapturedOwnedMontroyalEntryDestinationZ = 580.9919f;

        private const float UserConfirmedMontroyalExitSourceX = 530.4664f;

        private const float UserConfirmedMontroyalExitSourceY = 160.6381f;

        private const float UserConfirmedMontroyalExitSourceZ = 590.7054f;

        private const float UserConfirmedMontroyalExitRadius = 3.0f;

        private const float UserConfirmedMontroyalExitVerticalTolerance = 12.0f;

        private const float UserConfirmedMontroyalExitDestinationX = 3138.2f;

        private const float UserConfirmedMontroyalExitDestinationY = 51.4f;

        private const float UserConfirmedMontroyalExitDestinationZ = 812.8f;

        private const int CapturedSubwayPlayfieldId = 127;

        private const int CapturedSubwayEntrySourcePlayfieldId = 655;

        private const uint CapturedSubwayEntrySourceDoorInstance = 0xC01A028F;

        private const float CapturedSubwayEntrySourceX = 3305.5f;

        private const float CapturedSubwayEntrySourceY = 35.3f;

        private const float CapturedSubwayEntrySourceZ = 836.4f;

        private const float CapturedSubwayEntryRadius = 4.0f;

        private const float CapturedSubwayEntryVerticalTolerance = 8.0f;

        private const float CapturedSubwayEntranceLandingX = 71.4f;

        private const float CapturedSubwayEntranceLandingY = 115.6f;

        private const float CapturedSubwayEntranceLandingZ = 319.0f;

        private const float CapturedSubwayEntranceHeadingX = 0.707102f;

        private const float CapturedSubwayEntranceHeadingY = 0.0f;

        private const float CapturedSubwayEntranceHeadingZ = 0.707112f;

        private const float CapturedSubwayEntranceHeadingW = 0.0f;

        private const float CapturedSubwayExitSourceX = 64.2f;

        private const float CapturedSubwayExitSourceY = 115.6f;

        private const float CapturedSubwayExitSourceZ = 319.3f;

        private const float CapturedSubwayExitRadius = 4.0f;

        private const float CapturedSubwayExitVerticalTolerance = 8.0f;

        private static readonly Dictionary<int, DateTime> PostZoneCollisionGraceUntil =
            new Dictionary<int, DateTime>();

        private static readonly object PostZoneCollisionGraceLock = new object();

        private static readonly TimeSpan PostZoneCollisionGrace = TimeSpan.FromSeconds(3);

        private readonly Dictionary<int, HashSet<string>> statelEnterContacts =
            new Dictionary<int, HashSet<string>>();

        private readonly HashSet<int> statelCollisionInitializedCharacters = new HashSet<int>();

        internal static void ArmPostZoneCollisionGrace(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (PostZoneCollisionGraceLock)
            {
                PostZoneCollisionGraceUntil[character.Identity.Instance] = DateTime.UtcNow + PostZoneCollisionGrace;
            }
        }

        internal static bool IsCapturedMontroyalPrivateCityInstance(int playfieldInstance)
        {
            return playfieldInstance == CapturedMontroyalPrivateCityInstance
                   || playfieldInstance == CapturedOwnedMontroyalPrivateCityInstance;
        }

        internal static int ResolveCapturedMontroyalPrivateCityInstance(
            int organizationInstance,
            int organizationCityId)
        {
            if (organizationCityId > 0)
            {
                return organizationCityId;
            }

            return organizationInstance == CapturedOwnedPrivateCityOrganizationInstance
                       ? CapturedOwnedMontroyalPrivateCityInstance
                       : CapturedMontroyalPrivateCityInstance;
        }

        internal static bool IsCapturedOwnedPrivateCityOrganization(int organizationInstance)
        {
            return organizationInstance == CapturedOwnedPrivateCityOrganizationInstance;
        }

        internal void ClearContactState(int dynelId)
        {
            this.statelEnterContacts.Remove(dynelId);
            this.statelCollisionInitializedCharacters.Remove(dynelId);
        }

        internal void PrimeStatelCollisionContacts(
            ICharacter dynel,
            IEnumerable<StatelData> collisionStatels)
        {
            int dynelId = dynel.Identity.Instance;
            HashSet<string> activeEnterContacts;
            if (!this.statelEnterContacts.TryGetValue(dynelId, out activeEnterContacts))
            {
                activeEnterContacts = new HashSet<string>();
                this.statelEnterContacts[dynelId] = activeEnterContacts;
            }

            foreach (StatelData sd in collisionStatels)
            {
                if (!IsInStatelCollisionRange(sd, dynel))
                {
                    continue;
                }

                string statelKey = BuildStatelContactKey(sd);
                activeEnterContacts.Add(statelKey);
            }

            this.statelCollisionInitializedCharacters.Add(dynelId);
        }

        internal void CheckStatelCollision(
            ICharacter dynel,
            Identity playfieldIdentity,
            IEnumerable<StatelData> collisionStatels,
            Func<ICharacter, int> resolvePrivateCityDestinationPlayfield,
            Func<ICharacter, int> resolveCharacterOrganizationInstance,
            Func<ICharacter, ProxyPlayfieldExitDestination> resolveProxyExitDestination,
            Action<ICharacter> stopMovement,
            Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (IsPostZoneCollisionGraceActive(dynel))
            {
                return;
            }

            if (this.TryHandleCapturedSubwayProxyEntry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedMontroyalPrivateCityEntry(
                dynel,
                playfieldIdentity,
                resolvePrivateCityDestinationPlayfield,
                resolveCharacterOrganizationInstance,
                stopMovement,
                sendCapturedPrivateCityEntrySocialStatus,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleUserConfirmedMontroyalPrivateCityExit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedSubwayProxyExit(
                dynel,
                playfieldIdentity,
                resolveProxyExitDestination,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            int dynelId = dynel.Identity.Instance;
            bool initialized = this.statelCollisionInitializedCharacters.Contains(dynelId);
            HashSet<string> activeEnterContacts;
            if (!this.statelEnterContacts.TryGetValue(dynelId, out activeEnterContacts))
            {
                activeEnterContacts = new HashSet<string>();
                this.statelEnterContacts[dynelId] = activeEnterContacts;
            }

            foreach (StatelData sd in collisionStatels)
            {
                string statelKey = BuildStatelContactKey(sd);
                bool inRange = IsInStatelCollisionRange(sd, dynel);
                bool wasInRange = activeEnterContacts.Contains(statelKey);

                if (!inRange)
                {
                    if (wasInRange)
                    {
                        activeEnterContacts.Remove(statelKey);
                    }

                    continue;
                }

                foreach (StatelEvent ev in
                    sd.Events.Where(
                        x =>
                            (x.EventType == EventType.OnCollide) || (x.EventType == EventType.OnEnter)
                            || (x.EventType == EventType.OnTargetInVicinity)))
                {
                    if (ev.EventType == EventType.OnEnter)
                    {
                        if (!initialized)
                        {
                            activeEnterContacts.Add(statelKey);
                            continue;
                        }

                        if (wasInRange)
                        {
                            continue;
                        }

                        activeEnterContacts.Add(statelKey);
                    }
                    else if (!wasInRange)
                    {
                        activeEnterContacts.Add(statelKey);
                    }

                    LogUtil.Debug(DebugInfoDetail.Statel, "Stepped on Statel " + sd.Identity.ToString(true));
                    LogUtil.Debug(DebugInfoDetail.Statel, ev.ToString());
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Statel collision firing character={0} playfield={1} coords={2:F1},{3:F1},{4:F1} statel={5} event={6}",
                            dynel.Identity.ToString(true),
                            dynel.Playfield.Identity.Instance,
                            dynel.RawCoordinates.X,
                            dynel.RawCoordinates.Y,
                            dynel.RawCoordinates.Z,
                            sd.Identity.ToString(true),
                            ev.EventType));
                    ev.Perform(dynel, sd);
                }
            }

            if (!initialized)
            {
                this.statelCollisionInitializedCharacters.Add(dynelId);
            }
        }

        private static Coordinate ResolveCapturedMontroyalEntryDestination(int destinationPlayfieldId)
        {
            return destinationPlayfieldId == CapturedOwnedMontroyalPrivateCityInstance
                       ? new Coordinate(
                             CapturedOwnedMontroyalEntryDestinationX,
                             CapturedOwnedMontroyalEntryDestinationY,
                             CapturedOwnedMontroyalEntryDestinationZ)
                       : new Coordinate(
                             CapturedMontroyalEntryDestinationX,
                             CapturedMontroyalEntryDestinationY,
                             CapturedMontroyalEntryDestinationZ);
        }

        private static string BuildStatelContactKey(StatelData sd)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2:0.###}:{3:0.###}:{4:0.###}",
                (int)sd.Identity.Type,
                sd.Identity.Instance,
                sd.X,
                sd.Y,
                sd.Z);
        }

        private static bool IsInStatelCollisionRange(StatelData sd, ICharacter dynel)
        {
            float dx = sd.X - dynel.RawCoordinates.X;
            float dz = sd.Z - dynel.RawCoordinates.Z;
            float horizontalDistance = (float)Math.Sqrt((dx * dx) + (dz * dz));
            float verticalDistance = Math.Abs(sd.Y - dynel.RawCoordinates.Y);

            return horizontalDistance < 2.0f && verticalDistance <= 6.0f;
        }

        internal static bool IsPostZoneCollisionGraceActive(ICharacter dynel)
        {
            if (dynel == null)
            {
                return false;
            }

            lock (PostZoneCollisionGraceLock)
            {
                DateTime until;
                if (!PostZoneCollisionGraceUntil.TryGetValue(dynel.Identity.Instance, out until))
                {
                    return false;
                }

                if (DateTime.UtcNow < until)
                {
                    return true;
                }

                PostZoneCollisionGraceUntil.Remove(dynel.Identity.Instance);
                return false;
            }
        }

        private bool TryHandleCapturedSubwayProxyEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != CapturedSubwayEntrySourcePlayfieldId
                || character.Controller == null
                || character.Controller.Client == null
                || character.DoNotDoTimers)
            {
                return false;
            }

            var dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            float sourceX = character.RawCoordinates.X;
            float sourceY = character.RawCoordinates.Y;
            float sourceZ = character.RawCoordinates.Z;
            double deltaX = sourceX - CapturedSubwayEntrySourceX;
            double deltaZ = sourceZ - CapturedSubwayEntrySourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - CapturedSubwayEntrySourceY);
            if (horizontalDistanceSquared > CapturedSubwayEntryRadius * CapturedSubwayEntryRadius
                || verticalDistance > CapturedSubwayEntryVerticalTolerance)
            {
                return false;
            }

            var destination = new Coordinate(
                CapturedSubwayEntranceLandingX,
                CapturedSubwayEntranceLandingY,
                CapturedSubwayEntranceLandingZ);
            var heading = new RuntimeQuaternion(
                CapturedSubwayEntranceHeadingX,
                CapturedSubwayEntranceHeadingY,
                CapturedSubwayEntranceHeadingZ,
                CapturedSubwayEntranceHeadingW);

            character.Stats[StatIds.externaldoorinstance].BaseValue = CapturedSubwayEntrySourceDoorInstance;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = CapturedSubwayEntrySourcePlayfieldId;

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, CapturedSubwayPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Subway proxy entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) sourceDoor={5:X8} destPf={6} dest=({7:F3},{8:F3},{9:F3}) evidence=server_log_20260708_1634 user_extended_location_20260708_2135",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    CapturedSubwayEntrySourceDoorInstance,
                    CapturedSubwayPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedSubwayProxyExit(
            ICharacter character,
            Identity playfieldIdentity,
            Func<ICharacter, ProxyPlayfieldExitDestination> resolveProxyExitDestination,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != CapturedSubwayPlayfieldId
                || character.Controller == null
                || character.Controller.Client == null
                || character.DoNotDoTimers)
            {
                return false;
            }

            var dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            float sourceX = character.RawCoordinates.X;
            float sourceY = character.RawCoordinates.Y;
            float sourceZ = character.RawCoordinates.Z;
            double deltaX = sourceX - CapturedSubwayExitSourceX;
            double deltaZ = sourceZ - CapturedSubwayExitSourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - CapturedSubwayExitSourceY);
            if (horizontalDistanceSquared > CapturedSubwayExitRadius * CapturedSubwayExitRadius
                || verticalDistance > CapturedSubwayExitVerticalTolerance)
            {
                return false;
            }

            ProxyPlayfieldExitDestination exitDestination =
                resolveProxyExitDestination == null ? null : resolveProxyExitDestination(character);
            if (exitDestination == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Subway proxy exit skipped character={0} source=({1:F3},{2:F3},{3:F3}) externalDoor={4:X8} externalPf={5} reason=missing-external-door evidence=server_log_20260708_1609",
                        character.Identity.ToString(true),
                        sourceX,
                        sourceY,
                        sourceZ,
                        character.Stats[StatIds.externaldoorinstance].BaseValue,
                        character.Stats[StatIds.externalplayfieldinstance].Value));
                return false;
            }

            stopMovement(character);
            teleportToPlayfield(
                dynel,
                exitDestination.Destination,
                exitDestination.Heading,
                exitDestination.PlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Subway proxy exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) externalDoor={5:X8} destPf={6} dest=({7:F3},{8:F3},{9:F3}) evidence=server_log_20260708_1609",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    exitDestination.ExternalDoorInstance,
                    exitDestination.PlayfieldId,
                    exitDestination.Destination.x,
                    exitDestination.Destination.y,
                    exitDestination.Destination.z));

            return true;
        }

        private bool TryHandleCapturedMontroyalPrivateCityEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Func<ICharacter, int> resolvePrivateCityDestinationPlayfield,
            Func<ICharacter, int> resolveCharacterOrganizationInstance,
            Action<ICharacter> stopMovement,
            Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != CapturedMontroyalEntrySourcePlayfieldId
                || character.Controller == null
                || character.Controller.Client == null
                || character.DoNotDoTimers)
            {
                return false;
            }

            var dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            float sourceX = character.RawCoordinates.X;
            float sourceY = character.RawCoordinates.Y;
            float sourceZ = character.RawCoordinates.Z;
            double deltaX = sourceX - CapturedMontroyalEntrySourceX;
            double deltaZ = sourceZ - CapturedMontroyalEntrySourceZ;
            double horizontalDistanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            double verticalDistance = Math.Abs(sourceY - CapturedMontroyalEntrySourceY);
            if (horizontalDistanceSquared > CapturedMontroyalEntryRadius * CapturedMontroyalEntryRadius
                || verticalDistance > CapturedMontroyalEntryVerticalTolerance)
            {
                return false;
            }

            int destinationPlayfieldId = resolvePrivateCityDestinationPlayfield(character);
            if (destinationPlayfieldId <= 0)
            {
                return false;
            }

            Coordinate destination = ResolveCapturedMontroyalEntryDestination(destinationPlayfieldId);
            var heading = new RuntimeQuaternion(0.0f, 1.0f, 0.0f, -4.371139E-08f);

            stopMovement(character);
            sendCapturedPrivateCityEntrySocialStatus(character);
            teleportToPlayfield(dynel, destination, heading, destinationPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Montroyal private city entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) org={9} evidence=live_capture_20260622-101935 live_capture_20260623-021643",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    destinationPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z,
                    resolveCharacterOrganizationInstance(character)));

            return true;
        }

        private bool TryHandleUserConfirmedMontroyalPrivateCityExit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !IsCapturedMontroyalPrivateCityInstance(playfieldIdentity.Instance)
                || character.Controller == null
                || character.Controller.Client == null
                || character.DoNotDoTimers)
            {
                return false;
            }

            var dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            float sourceX = character.RawCoordinates.X;
            float sourceY = character.RawCoordinates.Y;
            float sourceZ = character.RawCoordinates.Z;
            double deltaX = sourceX - UserConfirmedMontroyalExitSourceX;
            double deltaZ = sourceZ - UserConfirmedMontroyalExitSourceZ;
            double horizontalDistanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            double verticalDistance = Math.Abs(sourceY - UserConfirmedMontroyalExitSourceY);
            if (horizontalDistanceSquared > UserConfirmedMontroyalExitRadius * UserConfirmedMontroyalExitRadius
                || verticalDistance > UserConfirmedMontroyalExitVerticalTolerance)
            {
                return false;
            }

            var destination = new Coordinate(
                UserConfirmedMontroyalExitDestinationX,
                UserConfirmedMontroyalExitDestinationY,
                UserConfirmedMontroyalExitDestinationZ);
            var heading = new RuntimeQuaternion(0.0f, 0.9991581f, 0.0f, 0.04102511f);

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, CapturedMontroyalEntrySourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Montroyal private city exit teleport character={0} sourceInstance={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=live_capture_20260622-101935 user_extended_location_20260622_180812",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    CapturedMontroyalEntrySourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }
    }

    internal sealed class ProxyPlayfieldExitDestination
    {
        internal ProxyPlayfieldExitDestination(
            int playfieldId,
            uint externalDoorInstance,
            Coordinate destination,
            RuntimeQuaternion heading)
        {
            this.PlayfieldId = playfieldId;
            this.ExternalDoorInstance = externalDoorInstance;
            this.Destination = destination;
            this.Heading = heading;
        }

        internal int PlayfieldId { get; private set; }

        internal uint ExternalDoorInstance { get; private set; }

        internal Coordinate Destination { get; private set; }

        internal RuntimeQuaternion Heading { get; private set; }
    }
}
