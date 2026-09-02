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

    using ZoneEngine.Core;
    using ZoneEngine.Core.Functions.GameFunctions;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

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

        // Official-live SCFU landing repeated in captures 20260708-004038,
        // 20260708-175514, 20260708-181729, 20260708-182237,
        // 20260709-164219, 20260710-212455, 20260712-155528, and
        // 20260717-012522. Representative row: 20260708-004038 events.log:741
        // (IN-N3 packet #510).
        private const float CapturedSubwayEntranceLandingX = 65.80835f;

        private const float CapturedSubwayEntranceLandingY = 115.6148f;

        private const float CapturedSubwayEntranceLandingZ = 318.9879f;

        private const float CapturedSubwayEntranceHeadingX = 0.0f;

        private const float CapturedSubwayEntranceHeadingY = 0.7071124f;

        private const float CapturedSubwayEntranceHeadingZ = 0.0f;

        private const float CapturedSubwayEntranceHeadingW = 0.7071012f;

        // Capture 20260719-155043: Andromeda ICC HQ (655) â†” Holodeck Freelancers Inc. (7001).
        // No door/statel identity in capture â€” coordinate-radius zones (Subway proxy pattern).
        private const int CapturedHoloDeckPlayfieldId = 7001;

        private const int CapturedHoloDeckEntrySourcePlayfieldId = 655;

        private const float CapturedHoloDeckEntrySourceX = 3245.94f;

        private const float CapturedHoloDeckEntrySourceY = 36.085f;

        private const float CapturedHoloDeckEntrySourceZ = 943.3943f;

        private const float CapturedHoloDeckEntryRadius = 2.5f;

        private const float CapturedHoloDeckEntryVerticalTolerance = 4.0f;

        private const float CapturedHoloDeckEntryLandingX = 183.01f;

        private const float CapturedHoloDeckEntryLandingY = 1.02f;

        private const float CapturedHoloDeckEntryLandingZ = 197.01f;

        private const float CapturedHoloDeckEntryLandingHeadingX = 0.0f;

        private const float CapturedHoloDeckEntryLandingHeadingY = 0.182956f;

        private const float CapturedHoloDeckEntryLandingHeadingZ = 0.0f;

        private const float CapturedHoloDeckEntryLandingHeadingW = 0.9831211f;

        private const float CapturedHoloDeckExitSourceX = 178.5387f;

        private const float CapturedHoloDeckExitSourceY = 1.02f;

        private const float CapturedHoloDeckExitSourceZ = 197.1772f;

        private const float CapturedHoloDeckExitRadius = 2.5f;

        private const float CapturedHoloDeckExitVerticalTolerance = 4.0f;

        private const float CapturedHoloDeckExitLandingX = 3245.0f;

        private const float CapturedHoloDeckExitLandingY = 35.715f;

        private const float CapturedHoloDeckExitLandingZ = 939.0f;

        private const float CapturedHoloDeckExitLandingHeadingX = 0.0f;

        private const float CapturedHoloDeckExitLandingHeadingY = -0.8022833f;

        private const float CapturedHoloDeckExitLandingHeadingZ = 0.0f;

        private const float CapturedHoloDeckExitLandingHeadingW = 0.5969435f;

        // Capture 20260821-191836 + Mike: Nascense outdoor (4310-4313) water -> Jobe (4001).
        // Capture 20260830-110744: no MoveType=Swim packets; Y drops to ~2.59 then PF 4001.
        // Mike: anywhere in outdoor Nascence, when falling into water / swimming → Jobe.
        // Mike 20260823: under-terrain "hole" near Crippler cave mouth must NOT zone to Jobe.
        // Landing pose from capture 20260830-110744 PF4311 → 4001 SCFU.
        private const int CapturedNascenseJobeDestinationPlayfieldId = 4001;

        // Capture water entry Y≈2.59; land walking near rivers is ~Y11–15.
        private const float CapturedNascenseJobeWaterYMax = 6.0f;

        private const float CapturedNascenseJobeEntryLandingX = 826.0f;

        private const float CapturedNascenseJobeEntryLandingY = 43.81f;

        private const float CapturedNascenseJobeEntryLandingZ = 822.0f;

        private const float CapturedNascenseJobeEntryLandingHeadingX = 0.0f;

        private const float CapturedNascenseJobeEntryLandingHeadingY = -0.9996747f;

        private const float CapturedNascenseJobeEntryLandingHeadingZ = 0.0f;

        private const float CapturedNascenseJobeEntryLandingHeadingW = 0.02550354f;

        private readonly HashSet<int> capturedSunriseLobbyEntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedSunriseApartmentExitContacts = new HashSet<int>();

        private readonly HashSet<int> capturedSunriseApartmentDoorEntryContacts = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> PostZoneCollisionGraceUntil =
            new Dictionary<int, DateTime>();

        private static readonly object PostZoneCollisionGraceLock = new object();

        private static readonly TimeSpan PostZoneCollisionGrace = TimeSpan.FromSeconds(3);

        private readonly Dictionary<int, HashSet<string>> statelEnterContacts =
            new Dictionary<int, HashSet<string>>();

        private readonly HashSet<int> statelCollisionInitializedCharacters = new HashSet<int>();

        private readonly HashSet<int> capturedSubwayEntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedHoloDeckEntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedHoloDeckExitContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenseJobeEntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon1EntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon1ExitContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon2EntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon2ExitContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon3EntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon3ExitContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon4EntryContacts = new HashSet<int>();

        private readonly HashSet<int> capturedNascenceDungeon4ExitContacts = new HashSet<int>();

        // After mission enter, player must walk away from the exit door before walk-on exit arms.
        private readonly HashSet<int> missionExitDoorArmedCharacters = new HashSet<int>();

        // Throttle for the mission-instance entry position diagnostic (see TryHandleMissionInstanceEntry).
        private DateTime lastMissionEntryDiagUtc = DateTime.MinValue;

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
            this.capturedSubwayEntryContacts.Remove(dynelId);
            this.capturedNascenseJobeEntryContacts.Remove(dynelId);
            this.capturedNascenceDungeon1EntryContacts.Remove(dynelId);
            this.capturedNascenceDungeon1ExitContacts.Remove(dynelId);
            this.capturedNascenceDungeon2EntryContacts.Remove(dynelId);
            this.capturedNascenceDungeon2ExitContacts.Remove(dynelId);
            this.capturedNascenceDungeon3EntryContacts.Remove(dynelId);
            this.capturedNascenceDungeon3ExitContacts.Remove(dynelId);
            this.capturedNascenceDungeon4EntryContacts.Remove(dynelId);
            this.capturedNascenceDungeon4ExitContacts.Remove(dynelId);
        }

        internal static bool IsNascenseOutdoorPlayfield(int playfieldInstance)
        {
            return playfieldInstance == 4310
                   || playfieldInstance == 4311
                   || playfieldInstance == 4312
                   || playfieldInstance == 4313;
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
                bool inRange = TempleWorldInteractionRules.IsBoundaryLinkStatel(sd)
                    ? TempleWorldInteractionRules.IsInBoundaryTriggerRange(sd, dynel)
                    : IsInStatelCollisionRange(sd, dynel);
                if (!inRange)
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
            Action<ICharacter> stopMovement,
            Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (IsPostZoneCollisionGraceActive(dynel))
            {
                return;
            }

            if (this.TryHandleMissionInstanceEntry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleMissionInstanceExit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                playfieldIdentity.Instance))
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

            if (this.TryHandleCapturedHoloDeckEntry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedHoloDeckExit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenseJobeEntry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon1Entry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon2Entry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon3Entry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon4Entry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon1Exit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon2Exit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon3Exit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedNascenceDungeon4Exit(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedSunriseLobbyEntry(
                dynel,
                playfieldIdentity,
                stopMovement,
                teleportToPlayfield))
            {
                return;
            }

            if (this.TryHandleCapturedSunriseApartmentProximityEntry(
                dynel,
                playfieldIdentity,
                stopMovement))
            {
                return;
            }

            if (this.TryHandleCapturedSunriseApartmentExit(
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
                bool inRange = TempleWorldInteractionRules.IsBoundaryLinkStatel(sd)
                    ? TempleWorldInteractionRules.IsInBoundaryTriggerRange(sd, dynel)
                    : IsInStatelCollisionRange(sd, dynel);
                bool wasInRange = activeEnterContacts.Contains(statelKey);

                if (!inRange)
                {
                    if (wasInRange)
                    {
                        activeEnterContacts.Remove(statelKey);
                    }

                    continue;
                }

                if (TempleWorldInteractionRules.IsExteriorLinkStatel(
                    playfieldIdentity.Instance,
                    sd))
                {
                    // The official EntryHall exterior link has no statel Event. Prime the
                    // arrival contact, re-arm only after leaving it, and reuse the shared
                    // ExitProxyPlayfield owner when the character returns to the edge.
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
                    stopMovement(dynel);
                    exitproxyplayfield.TryExecute(dynel, sd);
                    return;
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
                    else
                    {
                        // OnCollide / OnTargetInVicinity are edge-triggered: fire once per vicinity
                        // entry, NOT every movement tick. Without this, a rune statel (e.g. the
                        // Shadowlands garden save rune) re-cast its nano ~8x/second while the player
                        // stood on it, flooding chat and restarting the save animation endlessly.
                        if (wasInRange)
                        {
                            continue;
                        }

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
                            (float)dynel.Position.x,
                            (float)dynel.Position.y,
                            (float)dynel.Position.z,
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
            float dx = (float)(sd.X - (float)dynel.Position.x);
            float dz = (float)(sd.Z - (float)dynel.Position.z);
            float horizontalDistance = (float)Math.Sqrt((dx * dx) + (dz * dz));
            float verticalDistance = Math.Abs(sd.Y - (float)dynel.Position.y);

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

        private bool TryHandleMissionInstanceEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (!MissionInstanceService.EntryEnabled
                || character == null
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

            bool generatedExteriorClaim =
                MissionAcgBindingRuntime.HasOwnedExteriorMarker(
                    character.Identity.Instance,
                    playfieldIdentity.Instance,
                    (float)character.Position.x,
                    (float)character.Position.y,
                    (float)character.Position.z,
                    8.0,
                    12.0)
                || MissionInstanceService.HasGeneratedAcceptedExteriorClaim(
                    character,
                    Identity.None,
                    8.0,
                    12.0);
            if (!MissionKeyGrantService.HasMissionKey(character))
            {
                return generatedExteriorClaim;
            }

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            int currentPf = playfieldIdentity.Instance;

            bool near = false;
            string nearReason = null;
            double nearestDistanceSquared = double.MaxValue;

            // Outdoor rolled marker (capture 20260718-062936: walk to map mark on dest PF â†’ enter).
            List<MissionAcceptedStore.AcceptedMission> missions =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = 0; i < missions.Count; i++)
            {
                MissionAcceptedStore.AcceptedMission entry = missions[i];
                if (entry == null || entry.MarkerPlayfield == 0 || entry.MarkerPlayfield != currentPf)
                {
                    continue;
                }

                double deltaX = sourceX - entry.MarkerX;
                double deltaZ = sourceZ - entry.MarkerZ;
                double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
                if (horizontalDistanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = horizontalDistanceSquared;
                }

                double verticalDistance = Math.Abs(sourceY - entry.MarkerY);
                // Slightly larger than Rome door radius â€” outdoor marks have no physical door mesh yet.
                if (horizontalDistanceSquared <= 8.0 * 8.0 && verticalDistance <= 12.0)
                {
                    near = true;
                    nearReason = "marker";
                    break;
                }
            }

            // Convenience: Rome Blue house doors (pf 735) still work with a key.
            if (!near && currentPf == MissionInstanceService.RomeBluePlayfieldInstance)
            {
                foreach (float[] spot in MissionInstanceService.RomeEntranceSpots)
                {
                    double deltaX = sourceX - spot[0];
                    double deltaZ = sourceZ - spot[2];
                    double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
                    if (horizontalDistanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = horizontalDistanceSquared;
                    }

                    double verticalDistance = Math.Abs(sourceY - spot[1]);
                    if (horizontalDistanceSquared <= 4.0 * 4.0 && verticalDistance <= 8.0)
                    {
                        near = true;
                        nearReason = "rome";
                        break;
                    }
                }
            }

            DateTime now = DateTime.UtcNow;
            if (!near && (now - this.lastMissionEntryDiagUtc).TotalMilliseconds >= 1500)
            {
                this.lastMissionEntryDiagUtc = now;
                MissionDiagnostics.Log(
                    "ENTRY-CHECK char={0} pf={1} hasKey=true pos=({2:F2},{3:F2},{4:F2}) nearestDist={5:F2} near=false missions={6}",
                    character.Identity.Instance,
                    currentPf,
                    sourceX,
                    sourceY,
                    sourceZ,
                    nearestDistanceSquared == double.MaxValue ? -1.0 : Math.Sqrt(nearestDistanceSquared),
                    missions.Count);
            }

            if (!near)
            {
                return false;
            }

            MissionDiagnostics.Log(
                "ENTRY-TELEPORT char={0} reason={1} pf={2} pos=({3:F2},{4:F2},{5:F2})",
                character.Identity.Instance,
                nearReason,
                currentPf,
                sourceX,
                sourceY,
                sourceZ);

            // Same path as door-use enter (shape spawn + door clearance). Do not use the
            // legacy hardcoded SpawnX/Y/Z â€” that lands on the exit door and loops.
            stopMovement(character);
            if (!MissionInstanceService.TryEnterMissionInstance(character.Controller.Client))
            {
                return generatedExteriorClaim;
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mission instance entry teleport character={0} reason={1} sourcePf={2} source=({3:F3},{4:F3},{5:F3})",
                    character.Identity.ToString(true),
                    nearReason,
                    currentPf,
                    sourceX,
                    sourceY,
                    sourceZ));

            return true;
        }

        /// <summary>
        /// Walk onto the interior exit door after leaving spawn clearance â€” click-use also exits.
        /// Armed only after the player walks away from the door once (prevents enter/exit loop).
        /// </summary>
        private bool TryHandleMissionInstanceExit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (!MissionInstanceService.EntryEnabled
                || character == null
                || !MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance)
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

            float doorX;
            float doorY;
            float doorZ;
            if (!MissionInstanceService.ResolveInteriorExitDoor(
                    playfieldIdentity.Instance,
                    out doorX,
                    out doorY,
                    out doorZ))
            {
                return false;
            }

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double dx = sourceX - doorX;
            double dz = sourceZ - doorZ;
            double horizontalSq = (dx * dx) + (dz * dz);
            double vertical = Math.Abs(sourceY - doorY);
            const double armRadius = 10.0;
            const double exitRadius = 3.5;

            int charId = character.Identity.Instance;
            if (horizontalSq > (armRadius * armRadius) || vertical > 12.0)
            {
                this.missionExitDoorArmedCharacters.Add(charId);
                return false;
            }

            if (!this.missionExitDoorArmedCharacters.Contains(charId))
            {
                return false;
            }

            if (horizontalSq > (exitRadius * exitRadius) || vertical > 8.0)
            {
                return false;
            }

            this.missionExitDoorArmedCharacters.Remove(charId);
            if (!MissionInstanceService.TryExitMissionInstance(character.Controller.Client))
            {
                return false;
            }

            stopMovement(character);
            MissionDiagnostics.Log(
                "EXIT-PROXIMITY char={0} door=({1:F1},{2:F1},{3:F1})",
                charId,
                doorX,
                doorY,
                doorZ);
            return true;
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - CapturedSubwayEntrySourceX;
            double deltaZ = sourceZ - CapturedSubwayEntrySourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - CapturedSubwayEntrySourceY);
            bool inEntryTrigger =
                horizontalDistanceSquared <= CapturedSubwayEntryRadius * CapturedSubwayEntryRadius
                && verticalDistance <= CapturedSubwayEntryVerticalTolerance;
            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedSubwayEntryContacts.Remove(dynelId);
                return false;
            }

            // The official PF655 return landing is inside this four-unit trigger.
            // Treat arrival as an existing contact so post-zone grace cannot turn
            // into a delayed bounce back to PF127; leaving and re-entering re-arms it.
            if (this.capturedSubwayEntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedSubwayEntryContacts.Add(dynelId);
                return false;
            }

            this.capturedSubwayEntryContacts.Add(dynelId);

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

        private bool TryHandleCapturedHoloDeckEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != CapturedHoloDeckEntrySourcePlayfieldId
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - CapturedHoloDeckEntrySourceX;
            double deltaZ = sourceZ - CapturedHoloDeckEntrySourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - CapturedHoloDeckEntrySourceY);
            bool inEntryTrigger =
                horizontalDistanceSquared <= CapturedHoloDeckEntryRadius * CapturedHoloDeckEntryRadius
                && verticalDistance <= CapturedHoloDeckEntryVerticalTolerance;
            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedHoloDeckEntryContacts.Remove(dynelId);
                return false;
            }

            // Exit landing on PF 655 is near this trigger; treat arrival as existing contact.
            if (this.capturedHoloDeckEntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedHoloDeckEntryContacts.Add(dynelId);
                return false;
            }

            this.capturedHoloDeckEntryContacts.Add(dynelId);

            var destination = new Coordinate(
                CapturedHoloDeckEntryLandingX,
                CapturedHoloDeckEntryLandingY,
                CapturedHoloDeckEntryLandingZ);
            var heading = new RuntimeQuaternion(
                CapturedHoloDeckEntryLandingHeadingX,
                CapturedHoloDeckEntryLandingHeadingY,
                CapturedHoloDeckEntryLandingHeadingZ,
                CapturedHoloDeckEntryLandingHeadingW);

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                CapturedHoloDeckEntrySourcePlayfieldId;

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, CapturedHoloDeckPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "HoloDeck entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260719-155043",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    CapturedHoloDeckPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedHoloDeckExit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != CapturedHoloDeckPlayfieldId
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - CapturedHoloDeckExitSourceX;
            double deltaZ = sourceZ - CapturedHoloDeckExitSourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - CapturedHoloDeckExitSourceY);
            bool inExitTrigger =
                horizontalDistanceSquared <= CapturedHoloDeckExitRadius * CapturedHoloDeckExitRadius
                && verticalDistance <= CapturedHoloDeckExitVerticalTolerance;
            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedHoloDeckExitContacts.Remove(dynelId);
                return false;
            }

            // Entry landing is near the exit pad; treat arrival as existing contact.
            if (this.capturedHoloDeckExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedHoloDeckExitContacts.Add(dynelId);
                return false;
            }

            this.capturedHoloDeckExitContacts.Add(dynelId);

            var destination = new Coordinate(
                CapturedHoloDeckExitLandingX,
                CapturedHoloDeckExitLandingY,
                CapturedHoloDeckExitLandingZ);
            var heading = new RuntimeQuaternion(
                CapturedHoloDeckExitLandingHeadingX,
                CapturedHoloDeckExitLandingHeadingY,
                CapturedHoloDeckExitLandingHeadingZ,
                CapturedHoloDeckExitLandingHeadingW);

            character.Stats[StatIds.externalplayfieldinstance].BaseValue = CapturedHoloDeckPlayfieldId;

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, CapturedHoloDeckEntrySourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "HoloDeck exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260719-155043",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    CapturedHoloDeckEntrySourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenseJobeEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !IsNascenseOutdoorPlayfield(playfieldIdentity.Instance)
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

            if (!IsCapturedNascenseJobeWaterEntry(character, -1))
            {
                this.capturedNascenseJobeEntryContacts.Remove(character.Identity.Instance);
                return false;
            }

            int dynelId = character.Identity.Instance;
            if (this.capturedNascenseJobeEntryContacts.Contains(dynelId))
            {
                return false;
            }

            this.capturedNascenseJobeEntryContacts.Add(dynelId);
            this.StartNascenseSwimTeleport(
                character,
                dynel,
                playfieldIdentity.Instance,
                stopMovement,
                teleportToPlayfield,
                "water-entry");
            return true;
        }

        private bool TryHandleCapturedNascenceDungeon1Entry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon1Rules.IsSourcePlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon1Rules.EntryTriggerX;
            double deltaZ = sourceZ - NascenceDungeon1Rules.EntryTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon1Rules.EntryTriggerY);
            bool inEntryTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon1Rules.EntryTriggerRadius * NascenceDungeon1Rules.EntryTriggerRadius
                && verticalDistance <= NascenceDungeon1Rules.EntryTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedNascenceDungeon1EntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedNascenceDungeon1EntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon1EntryContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon1EntryContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon1Rules.InteriorLandingX,
                NascenceDungeon1Rules.InteriorLandingY,
                NascenceDungeon1Rules.InteriorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon1Rules.InteriorLandingHeadingX,
                NascenceDungeon1Rules.InteriorLandingHeadingY,
                NascenceDungeon1Rules.InteriorLandingHeadingZ,
                NascenceDungeon1Rules.InteriorLandingHeadingW);

            character.Stats[StatIds.externaldoorinstance].BaseValue = NascenceDungeon1Rules.AcgEntranceInstanceStat;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = NascenceDungeon1Rules.SourcePlayfieldId;

            stopMovement(character);
            int destinationPlayfield = NascenceDungeon1Rules.AllocateDynamicPlayfieldId();
            teleportToPlayfield(dynel, destination, heading, destinationPlayfield);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon1 entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260824-125154",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    destinationPlayfield,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon2Entry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon2Rules.IsSourcePlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon2Rules.EntryTriggerX;
            double deltaZ = sourceZ - NascenceDungeon2Rules.EntryTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon2Rules.EntryTriggerY);
            bool inEntryTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon2Rules.EntryTriggerRadius * NascenceDungeon2Rules.EntryTriggerRadius
                && verticalDistance <= NascenceDungeon2Rules.EntryTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedNascenceDungeon2EntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedNascenceDungeon2EntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon2EntryContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon2EntryContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon2Rules.InteriorLandingX,
                NascenceDungeon2Rules.InteriorLandingY,
                NascenceDungeon2Rules.InteriorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon2Rules.InteriorLandingHeadingX,
                NascenceDungeon2Rules.InteriorLandingHeadingY,
                NascenceDungeon2Rules.InteriorLandingHeadingZ,
                NascenceDungeon2Rules.InteriorLandingHeadingW);

            character.Stats[StatIds.externaldoorinstance].BaseValue = NascenceDungeon2Rules.AcgEntranceInstanceStat;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = NascenceDungeon2Rules.SourcePlayfieldId;

            stopMovement(character);
            int destinationPlayfield = NascenceDungeon2Rules.AllocateDynamicPlayfieldId();
            teleportToPlayfield(dynel, destination, heading, destinationPlayfield);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon2 entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260823-182854",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    destinationPlayfield,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon1Exit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon1Rules.IsDungeonPlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon1Rules.ExitTriggerX;
            double deltaZ = sourceZ - NascenceDungeon1Rules.ExitTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon1Rules.ExitTriggerY);
            bool inExitTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon1Rules.ExitTriggerRadius * NascenceDungeon1Rules.ExitTriggerRadius
                && verticalDistance <= NascenceDungeon1Rules.ExitTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedNascenceDungeon1ExitContacts.Remove(dynelId);
                return false;
            }

            // Landing is ~1m from exit trigger — arm only after leave/re-enter
            // (same pattern as Sunrise apartment exit). Capture 20260824-132534: walk-out, no Use.
            if (this.capturedNascenceDungeon1ExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon1ExitContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon1ExitContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon1Rules.ExitOutdoorLandingX,
                NascenceDungeon1Rules.ExitOutdoorLandingY,
                NascenceDungeon1Rules.ExitOutdoorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon1Rules.ExitOutdoorLandingHeadingX,
                NascenceDungeon1Rules.ExitOutdoorLandingHeadingY,
                NascenceDungeon1Rules.ExitOutdoorLandingHeadingZ,
                NascenceDungeon1Rules.ExitOutdoorLandingHeadingW);

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, NascenceDungeon1Rules.SourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon1 exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260824-132534",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    NascenceDungeon1Rules.SourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon2Exit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon2Rules.ExitTriggerX;
            double deltaZ = sourceZ - NascenceDungeon2Rules.ExitTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon2Rules.ExitTriggerY);
            bool inExitTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon2Rules.ExitTriggerRadius * NascenceDungeon2Rules.ExitTriggerRadius
                && verticalDistance <= NascenceDungeon2Rules.ExitTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedNascenceDungeon2ExitContacts.Remove(dynelId);
                return false;
            }

            // Landing is ~1m from exit trigger — arm only after leave/re-enter
            // (same pattern as Sunrise apartment exit). Capture 20260823-182854: walk-out, no Use.
            if (this.capturedNascenceDungeon2ExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon2ExitContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon2ExitContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon2Rules.ExitOutdoorLandingX,
                NascenceDungeon2Rules.ExitOutdoorLandingY,
                NascenceDungeon2Rules.ExitOutdoorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon2Rules.ExitOutdoorLandingHeadingX,
                NascenceDungeon2Rules.ExitOutdoorLandingHeadingY,
                NascenceDungeon2Rules.ExitOutdoorLandingHeadingZ,
                NascenceDungeon2Rules.ExitOutdoorLandingHeadingW);

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, NascenceDungeon2Rules.SourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon2 exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260823-182854",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    NascenceDungeon2Rules.SourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon3Entry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon3Rules.IsSourcePlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon3Rules.EntryTriggerX;
            double deltaZ = sourceZ - NascenceDungeon3Rules.EntryTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon3Rules.EntryTriggerY);
            bool inEntryTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon3Rules.EntryTriggerRadius * NascenceDungeon3Rules.EntryTriggerRadius
                && verticalDistance <= NascenceDungeon3Rules.EntryTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedNascenceDungeon3EntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedNascenceDungeon3EntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon3EntryContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon3EntryContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon3Rules.InteriorLandingX,
                NascenceDungeon3Rules.InteriorLandingY,
                NascenceDungeon3Rules.InteriorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon3Rules.InteriorLandingHeadingX,
                NascenceDungeon3Rules.InteriorLandingHeadingY,
                NascenceDungeon3Rules.InteriorLandingHeadingZ,
                NascenceDungeon3Rules.InteriorLandingHeadingW);

            character.Stats[StatIds.externaldoorinstance].BaseValue = NascenceDungeon3Rules.AcgEntranceInstanceStat;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = NascenceDungeon3Rules.SourcePlayfieldId;

            stopMovement(character);
            int destinationPlayfield = NascenceDungeon3Rules.AllocateDynamicPlayfieldId();
            teleportToPlayfield(dynel, destination, heading, destinationPlayfield);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon3 entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260830-140240",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    destinationPlayfield,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon3Exit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon3Rules.IsDungeonPlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon3Rules.ExitTriggerX;
            double deltaZ = sourceZ - NascenceDungeon3Rules.ExitTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon3Rules.ExitTriggerY);
            bool inExitTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon3Rules.ExitTriggerRadius * NascenceDungeon3Rules.ExitTriggerRadius
                && verticalDistance <= NascenceDungeon3Rules.ExitTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedNascenceDungeon3ExitContacts.Remove(dynelId);
                return false;
            }

            // Landing is ~1m from exit trigger — arm only after leave/re-enter
            // (same pattern as Sunrise apartment exit). Capture 20260830-140240: walk-out, no Use.
            if (this.capturedNascenceDungeon3ExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon3ExitContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon3ExitContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon3Rules.ExitOutdoorLandingX,
                NascenceDungeon3Rules.ExitOutdoorLandingY,
                NascenceDungeon3Rules.ExitOutdoorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon3Rules.ExitOutdoorLandingHeadingX,
                NascenceDungeon3Rules.ExitOutdoorLandingHeadingY,
                NascenceDungeon3Rules.ExitOutdoorLandingHeadingZ,
                NascenceDungeon3Rules.ExitOutdoorLandingHeadingW);

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, NascenceDungeon3Rules.SourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon3 exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260830-140240",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    NascenceDungeon3Rules.SourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon4Entry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon4Rules.IsSourcePlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon4Rules.EntryTriggerX;
            double deltaZ = sourceZ - NascenceDungeon4Rules.EntryTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon4Rules.EntryTriggerY);
            bool inEntryTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon4Rules.EntryTriggerRadius * NascenceDungeon4Rules.EntryTriggerRadius
                && verticalDistance <= NascenceDungeon4Rules.EntryTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedNascenceDungeon4EntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedNascenceDungeon4EntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon4EntryContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon4EntryContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon4Rules.InteriorLandingX,
                NascenceDungeon4Rules.InteriorLandingY,
                NascenceDungeon4Rules.InteriorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon4Rules.InteriorLandingHeadingX,
                NascenceDungeon4Rules.InteriorLandingHeadingY,
                NascenceDungeon4Rules.InteriorLandingHeadingZ,
                NascenceDungeon4Rules.InteriorLandingHeadingW);

            character.Stats[StatIds.externaldoorinstance].BaseValue = NascenceDungeon4Rules.AcgEntranceInstanceStat;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = NascenceDungeon4Rules.SourcePlayfieldId;

            stopMovement(character);
            int destinationPlayfield = NascenceDungeon4Rules.AllocateDynamicPlayfieldId();
            teleportToPlayfield(dynel, destination, heading, destinationPlayfield);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon4 entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260830-143801",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    destinationPlayfield,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedNascenceDungeon4Exit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !NascenceDungeon4Rules.IsDungeonPlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - NascenceDungeon4Rules.ExitTriggerX;
            double deltaZ = sourceZ - NascenceDungeon4Rules.ExitTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - NascenceDungeon4Rules.ExitTriggerY);
            bool inExitTrigger =
                horizontalDistanceSquared
                    <= NascenceDungeon4Rules.ExitTriggerRadius * NascenceDungeon4Rules.ExitTriggerRadius
                && verticalDistance <= NascenceDungeon4Rules.ExitTriggerVerticalTolerance;

            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedNascenceDungeon4ExitContacts.Remove(dynelId);
                return false;
            }

            // Landing is ~1m from exit trigger — arm only after leave/re-enter
            // (same pattern as Sunrise apartment exit). Capture 20260830-143801: walk-out, no Use.
            if (this.capturedNascenceDungeon4ExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedNascenceDungeon4ExitContacts.Add(dynelId);
                return false;
            }

            this.capturedNascenceDungeon4ExitContacts.Add(dynelId);

            var destination = new Coordinate(
                NascenceDungeon4Rules.ExitOutdoorLandingX,
                NascenceDungeon4Rules.ExitOutdoorLandingY,
                NascenceDungeon4Rules.ExitOutdoorLandingZ);
            var heading = new RuntimeQuaternion(
                NascenceDungeon4Rules.ExitOutdoorLandingHeadingX,
                NascenceDungeon4Rules.ExitOutdoorLandingHeadingY,
                NascenceDungeon4Rules.ExitOutdoorLandingHeadingZ,
                NascenceDungeon4Rules.ExitOutdoorLandingHeadingW);

            stopMovement(character);
            teleportToPlayfield(dynel, destination, heading, NascenceDungeon4Rules.SourcePlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon4 exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260830-143801",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    NascenceDungeon4Rules.SourcePlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        internal bool TryHandleCapturedNascenseJobeFromMove(
            ICharacter character,
            Identity playfieldIdentity,
            byte moveType,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !IsNascenseOutdoorPlayfield(playfieldIdentity.Instance)
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

            // Capture 20260830-110744: zone on water depth (low Y), not Swim MoveType 26.
            // Cave-mouth under-terrain holes stay excluded (see IsCapturedNascenseJobeWaterEntry).
            if (!IsCapturedNascenseJobeWaterEntry(character, moveType))
            {
                this.capturedNascenseJobeEntryContacts.Remove(character.Identity.Instance);
                return false;
            }

            int dynelId = character.Identity.Instance;
            if (this.capturedNascenseJobeEntryContacts.Contains(dynelId))
            {
                return false;
            }

            this.capturedNascenseJobeEntryContacts.Add(dynelId);
            this.StartNascenseSwimTeleport(
                character,
                dynel,
                playfieldIdentity.Instance,
                stopMovement,
                teleportToPlayfield,
                "water-entry");
            return true;
        }

        /// <summary>
        /// Capture 20260830-110744 water portal: Y≈2.59 at ~(619,1304) then PLAYFIELD-INIT 4001.
        /// No Swim packets in that capture. Swim MoveType/MoveMode still accepted if present.
        /// </summary>
        private static bool IsCapturedNascenseJobeWaterEntry(ICharacter character, int moveType)
        {
            if (character == null)
            {
                return false;
            }

            float x = (float)character.Position.x;
            float y = (float)character.Position.y;
            float z = (float)character.Position.z;

            bool swimSignal = character.MoveMode == MoveModes.Swim
                || moveType == 26;
            bool inWaterDepth = y <= CapturedNascenseJobeWaterYMax;

            // Mike 20260823: Crippler cave mouth under-map falls are not the water portal.
            if (IsNascenseCripplerCaveUnderTerrainHole(x, z) && !swimSignal)
            {
                return false;
            }

            return swimSignal || inWaterDepth;
        }

        private static bool IsNascenseCripplerCaveUnderTerrainHole(float x, float z)
        {
            // Cave-mouth / high-terrain Crippler cluster (~Y 45–72), not the low river basin.
            return x >= 490f && x <= 590f && z >= 1680f && z <= 1820f;
        }

        private void StartNascenseSwimTeleport(
            ICharacter character,
            Dynel dynel,
            int sourcePlayfieldId,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield,
            string evidenceTag)
        {
            var destination = new Coordinate(
                CapturedNascenseJobeEntryLandingX,
                CapturedNascenseJobeEntryLandingY,
                CapturedNascenseJobeEntryLandingZ);
            var heading = new RuntimeQuaternion(
                CapturedNascenseJobeEntryLandingHeadingX,
                CapturedNascenseJobeEntryLandingHeadingY,
                CapturedNascenseJobeEntryLandingHeadingZ,
                CapturedNascenseJobeEntryLandingHeadingW);

            character.Stats[StatIds.externalplayfieldinstance].BaseValue = (uint)sourcePlayfieldId;

            stopMovement(character);
            teleportToPlayfield(
                dynel,
                destination,
                heading,
                CapturedNascenseJobeDestinationPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nascense->Jobe swim teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) via={9} evidence=20260821-191836+20260830-110744",
                    character.Identity.ToString(true),
                    sourcePlayfieldId,
                    (float)character.Position.x,
                    (float)character.Position.y,
                    (float)character.Position.z,
                    CapturedNascenseJobeDestinationPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z,
                    evidenceTag));
        }

        private bool TryHandleCapturedSunriseApartmentProximityEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement)
        {
            if (character == null
                || playfieldIdentity.Instance != LuxuryApartmentSunriseRules.SunriseStationPlayfieldId
                || character.Controller == null
                || character.Controller.Client == null
                || character.DoNotDoTimers)
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId))
            {
                return false;
            }

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            bool inDoorTrigger = false;
            foreach (float[] spot in LuxuryApartmentSunriseRules.OrbitalApartmentDoorProximitySpots)
            {
                double deltaX = sourceX - spot[0];
                double deltaZ = sourceZ - spot[2];
                double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
                double verticalDistance = Math.Abs(sourceY - spot[1]);
                if (horizontalDistanceSquared
                    <= LuxuryApartmentSunriseRules.OrbitalApartmentDoorProximityRadius
                       * LuxuryApartmentSunriseRules.OrbitalApartmentDoorProximityRadius
                    && verticalDistance
                       <= LuxuryApartmentSunriseRules.OrbitalApartmentDoorProximityVerticalTolerance)
                {
                    inDoorTrigger = true;
                    break;
                }
            }

            int dynelId = character.Identity.Instance;
            if (!inDoorTrigger)
            {
                this.capturedSunriseApartmentDoorEntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedSunriseApartmentDoorEntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedSunriseApartmentDoorEntryContacts.Add(dynelId);
                return false;
            }

            this.capturedSunriseApartmentDoorEntryContacts.Add(dynelId);
            stopMovement(character);

            if (!LuxuryApartmentSunriseInteractionHandler.Default.TryProximityEnterFromLobby(character))
            {
                return false;
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Sunrise apartment proximity entry character={0} source=({1:F3},{2:F3},{3:F3}) evidence=20260806-220142",
                    character.Identity.ToString(true),
                    sourceX,
                    sourceY,
                    sourceZ));
            return true;
        }

        private bool TryHandleCapturedSunriseApartmentExit(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || !LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(playfieldIdentity.Instance)
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - LuxuryApartmentSunriseRules.ApartmentExitTriggerX;
            double deltaZ = sourceZ - LuxuryApartmentSunriseRules.ApartmentExitTriggerZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - LuxuryApartmentSunriseRules.ApartmentExitTriggerY);
            bool inExitTrigger =
                horizontalDistanceSquared
                <= LuxuryApartmentSunriseRules.ApartmentExitTriggerRadius
                   * LuxuryApartmentSunriseRules.ApartmentExitTriggerRadius
                && verticalDistance <= LuxuryApartmentSunriseRules.ApartmentExitTriggerVerticalTolerance;
            int dynelId = character.Identity.Instance;
            if (!inExitTrigger)
            {
                this.capturedSunriseApartmentExitContacts.Remove(dynelId);
                return false;
            }

            // Landing is ~1.5m from the exit trigger â€” arm only after leave/re-enter
            // (same pattern as HoloDeck exit). Capture 20260806-210903: walk-out, no Use.
            if (this.capturedSunriseApartmentExitContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedSunriseApartmentExitContacts.Add(dynelId);
                return false;
            }

            this.capturedSunriseApartmentExitContacts.Add(dynelId);

            var destination = new Coordinate(
                LuxuryApartmentSunriseRules.ApartmentExitLobbyX,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyY,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyZ);
            var heading = new RuntimeQuaternion(
                0.0f,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyHeadingY,
                0.0f,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyHeadingW);

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                LuxuryApartmentSunriseRules.LuxuryApartmentPlayfieldId;

            stopMovement(character);
            teleportToPlayfield(
                dynel,
                destination,
                heading,
                LuxuryApartmentSunriseRules.SunriseStationPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Sunrise apartment exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260806-210903",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    LuxuryApartmentSunriseRules.SunriseStationPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private bool TryHandleCapturedSunriseLobbyEntry(
            ICharacter character,
            Identity playfieldIdentity,
            Action<ICharacter> stopMovement,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (character == null
                || playfieldIdentity.Instance != LuxuryApartmentSunriseRules.IccHqPlayfieldId
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
            double deltaX = sourceX - LuxuryApartmentSunriseRules.LobbyEntrySourceX;
            double deltaZ = sourceZ - LuxuryApartmentSunriseRules.LobbyEntrySourceZ;
            double horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            double verticalDistance = Math.Abs(sourceY - LuxuryApartmentSunriseRules.LobbyEntrySourceY);
            bool inEntryTrigger =
                horizontalDistanceSquared
                <= LuxuryApartmentSunriseRules.LobbyEntryRadius
                   * LuxuryApartmentSunriseRules.LobbyEntryRadius
                && verticalDistance <= LuxuryApartmentSunriseRules.LobbyEntryVerticalTolerance;
            int dynelId = character.Identity.Instance;
            if (!inEntryTrigger)
            {
                this.capturedSunriseLobbyEntryContacts.Remove(dynelId);
                return false;
            }

            if (this.capturedSunriseLobbyEntryContacts.Contains(dynelId)
                || !this.statelCollisionInitializedCharacters.Contains(dynelId))
            {
                this.capturedSunriseLobbyEntryContacts.Add(dynelId);
                return false;
            }

            this.capturedSunriseLobbyEntryContacts.Add(dynelId);

            var destination = new Coordinate(
                LuxuryApartmentSunriseRules.LobbyLandingX,
                LuxuryApartmentSunriseRules.LobbyLandingY,
                LuxuryApartmentSunriseRules.LobbyLandingZ);
            var heading = new RuntimeQuaternion(
                0.0f,
                LuxuryApartmentSunriseRules.LobbyLandingHeadingY,
                0.0f,
                LuxuryApartmentSunriseRules.LobbyLandingHeadingW);

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                LuxuryApartmentSunriseRules.IccHqPlayfieldId;

            stopMovement(character);
            teleportToPlayfield(
                dynel,
                destination,
                heading,
                LuxuryApartmentSunriseRules.SunriseStationPlayfieldId);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Sunrise Station lobby entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260806-202421",
                    character.Identity.ToString(true),
                    playfieldIdentity.Instance,
                    sourceX,
                    sourceY,
                    sourceZ,
                    LuxuryApartmentSunriseRules.SunriseStationPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
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

            float sourceX = (float)character.Position.x;
            float sourceY = (float)character.Position.y;
            float sourceZ = (float)character.Position.z;
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

}
