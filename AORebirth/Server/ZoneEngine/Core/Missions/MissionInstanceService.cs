namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

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

        internal const int OmniTradePlayfieldInstance = 710;

        // Omni Trade marker fallback (diag ACCEPT-WINDOW markerPf=710).
        private static readonly float[] OmniTradeFallbackSpot = { 235.65f, 5.01f, 402.99f };

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

        private static readonly Dictionary<int, int> QualityByPlayfield = new Dictionary<int, int>();

        private static readonly Dictionary<int, string> TargetNameByPlayfield = new Dictionary<int, string>();

        private static readonly Dictionary<int, int> TargetSideByPlayfield = new Dictionary<int, int>();

        /// <summary>Live instance PF → captured shape PF (layout / doors / ACG payload).</summary>
        private static readonly Dictionary<int, int> ShapeSourceByPlayfield = new Dictionary<int, int>();

        /// <summary>
        /// Outdoor return stamped at enter (character id → marker/outdoor). Survives mission
        /// complete deleting AcceptedStore — otherwise exit falls back to Rome Blue.
        /// </summary>
        private static readonly Dictionary<int, OutdoorReturn> ReturnByCharacter =
            new Dictionary<int, OutdoorReturn>();

        private sealed class OutdoorReturn
        {
            public int Playfield;

            public float X;

            public float Y;

            public float Z;
        }


        internal static bool IsMissionInstancePlayfield(int playfieldInstance)
        {
            if (playfieldInstance == InstancePlayfieldId || playfieldInstance == 1419307
                || playfieldInstance == 1419360)
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

        internal static void StampMissionQuality(int playfieldInstance, int quality)
        {
            if (playfieldInstance == 0)
            {
                return;
            }

            int ql = quality > 0 ? quality : 1;
            lock (ObjectiveGate)
            {
                QualityByPlayfield[playfieldInstance] = ql;
            }
        }

        internal static void StampTargetName(int playfieldInstance, string name)
        {
            if (playfieldInstance == 0 || string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (ObjectiveGate)
            {
                TargetNameByPlayfield[playfieldInstance] = name.Trim();
            }
        }

        internal static void StampTargetSide(int playfieldInstance, int side)
        {
            if (playfieldInstance == 0)
            {
                return;
            }

            lock (ObjectiveGate)
            {
                TargetSideByPlayfield[playfieldInstance] = side;
            }
        }

        internal static bool TryGetStampedMissionQuality(int playfieldInstance, out int quality)
        {
            lock (ObjectiveGate)
            {
                return QualityByPlayfield.TryGetValue(playfieldInstance, out quality) && quality > 0;
            }
        }

        internal static bool TryGetStampedObjective(int playfieldInstance, out MissionRollType type)
        {
            lock (ObjectiveGate)
            {
                return ObjectiveByPlayfield.TryGetValue(playfieldInstance, out type);
            }
        }

        internal static bool TryGetStampedTargetName(int playfieldInstance, out string name)
        {
            lock (ObjectiveGate)
            {
                return TargetNameByPlayfield.TryGetValue(playfieldInstance, out name)
                       && !string.IsNullOrEmpty(name);
            }
        }

        internal static bool TryGetStampedTargetSide(int playfieldInstance, out int side)
        {
            lock (ObjectiveGate)
            {
                return TargetSideByPlayfield.TryGetValue(playfieldInstance, out side);
            }
        }

        internal static void StampShapeSource(int livePlayfieldInstance, int capturedShapePlayfieldId)
        {
            if (livePlayfieldInstance == 0 || capturedShapePlayfieldId == 0)
            {
                return;
            }

            lock (ObjectiveGate)
            {
                ShapeSourceByPlayfield[livePlayfieldInstance] = capturedShapePlayfieldId;
            }
        }

        internal static bool TryGetShapeSource(int livePlayfieldInstance, out int capturedShapePlayfieldId)
        {
            lock (ObjectiveGate)
            {
                return ShapeSourceByPlayfield.TryGetValue(livePlayfieldInstance, out capturedShapePlayfieldId)
                       && capturedShapePlayfieldId > 0;
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

        /// <summary>
        /// Fog gold 20260725-184103: Playfield2 = 1419349 with ACG D7418B.
        /// </summary>
        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            int[] doorShapes = MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorShapes == null || doorShapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            // Exact gold PF id + building; remap / foreign ACG → open grey map.
            const int fogShapePf = 1419349;
            StampShapeSource(fogShapePf, fogShapePf);
            return fogShapePf;
        }

        private static int NextLiveMissionPf;

        /// <summary>
        /// Allocates a live playfield2 starting at gold 0x160008 (captures 080425 / 135121).
        /// Keeps shape ACG building id (unique building patch made ACG fail → full grey map).
        /// </summary>
        private static int AllocateLiveMissionPlayfieldId(int shapePf, int salt)
        {
            lock (ObjectiveGate)
            {
                for (int n = 0; n < 0x800; n++)
                {
                    NextLiveMissionPf = (NextLiveMissionPf + 1) & 0x3FF;
                    // Include gold 0x160008 — remapped far ids / unique buildings lost fog.
                    int livePf = 0x160008 + NextLiveMissionPf;
                    if (livePf == shapePf)
                    {
                        continue;
                    }

                    if (AORebirth.Core.Playfields.MissionInstanceShapeCatalog.IsCapturedShapePlayfield(livePf))
                    {
                        continue;
                    }

                    ShapeSourceByPlayfield[livePf] = shapePf;
                    return livePf;
                }

                int fallback = unchecked(0x160500 + (Math.Abs(salt) & 0x2FF));
                if (fallback == shapePf
                    || AORebirth.Core.Playfields.MissionInstanceShapeCatalog.IsCapturedShapePlayfield(fallback))
                {
                    fallback = 0x1607FE;
                }

                ShapeSourceByPlayfield[fallback] = shapePf;
                return fallback;
            }
        }

        /// <summary>
        /// Shape ACG payload for the live instance (must match stamped shape doors/spawns).
        /// </summary>
        internal static byte[] GetLiveGeneratorPayload(int livePlayfieldInstance)
        {
            MissionAcgBindingRecord binding;
            if (MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                livePlayfieldInstance,
                out binding))
            {
                MissionAcgLayoutBundle bundle =
                    MissionAcgBindingRuntime.Catalog.FindByLayoutId(
                        binding.Binding.SelectedBundleId);
                return bundle == null ? new byte[0] : bundle.CopyGeneratorPayload();
            }

            int shapePf;
            if (TryGetShapeSource(livePlayfieldInstance, out shapePf) && shapePf > 0)
            {
                return AORebirth.Core.Playfields.MissionInstanceShapeCatalog.GetGeneratorPayload(
                    shapePf);
            }

            return AORebirth.Core.Playfields.MissionInstanceShapeCatalog.GetGeneratorPayload(
                livePlayfieldInstance);
        }

        internal static int GetLiveBuildingInstance(int livePlayfieldInstance)
        {
            MissionAcgBindingRecord binding;
            if (MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                livePlayfieldInstance,
                out binding))
            {
                return binding.Binding.AcgBuildingIdentity.Instance;
            }

            // Must follow ShapeSource stamp — looking up livePf alone returns default/foreign
            // building (capture 20260725-202953: D74192) and opens the grey map.
            byte[] payload = GetLiveGeneratorPayload(livePlayfieldInstance);
            return AORebirth.Core.Playfields.MissionInstanceShapeCatalog.GetBuildingInstance(payload);
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

        internal static int ResolveCharacterMissionQuality(ICharacter character)
        {
            if (character == null)
            {
                return 1;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all == null || all.Count == 0)
            {
                return 1;
            }

            MissionAcceptedStore.AcceptedMission latest = all[all.Count - 1];
            if (latest == null || latest.Quality <= 0)
            {
                return 1;
            }

            return latest.Quality;
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

            if (MissionAcgBindingRuntime.HasAnyBindingForOwner(
                character.Identity.Instance))
            {
                MissionAcgBindingRecord exact;
                bool resolved =
                    MissionAcgBindingRuntime.TryResolveByExteriorMarker(
                        character.Identity.Instance,
                        character.Playfield.Identity.Instance,
                        character.RawCoordinates.X,
                        character.RawCoordinates.Y,
                        character.RawCoordinates.Z,
                        10.0,
                        14.0,
                        DateTime.UtcNow,
                        out exact);
                if (!resolved
                    || !MissionKeyGrantService.HasMissionKeyInstance(
                        character,
                        exact.Binding.MissionKeyIdentity.Instance))
                {
                    MissionDiagnostics.Log(
                        "ENTRY-REJECT char={0} reason=missing-or-ambiguous-exact-binding",
                        character.Identity.Instance);
                    return false;
                }

                MissionAcgEntryPlan plan;
                if (!MissionAcgEntryResolver.TryCreatePlan(exact, out plan))
                {
                    MissionDiagnostics.Log(
                        "ENTRY-REJECT char={0} accepted={1}:{2} reason=invalid-binding-plan",
                        character.Identity.Instance,
                        exact.Binding.AcceptedQuestIdentity.Type,
                        exact.Binding.AcceptedQuestIdentity.Instance);
                    return false;
                }

                MissionAcgMaterializedInstance materialized;
                string materializeFailure;
                if (!MissionAcgRuntimeManager.TryGetOrMaterialize(
                    exact,
                    out materialized,
                    out materializeFailure))
                {
                    MissionDiagnostics.Log(
                        "ENTRY-REJECT char={0} accepted={1}:{2} reason=materialization-failed detail={3}",
                        character.Identity.Instance,
                        exact.Binding.AcceptedQuestIdentity.Type,
                        exact.Binding.AcceptedQuestIdentity.Instance,
                        materializeFailure);
                    return false;
                }

                MissionAcgOperationalState operational;
                if (!MissionAcgOperationalRuntime.TryEnsureState(
                    exact,
                    out operational,
                    out materializeFailure))
                {
                    MissionDiagnostics.Log(
                        "ENTRY-REJECT char={0} accepted={1}:{2} reason=operational-materialization-failed detail={3}",
                        character.Identity.Instance,
                        exact.Binding.AcceptedQuestIdentity.Type,
                        exact.Binding.AcceptedQuestIdentity.Instance,
                        materializeFailure);
                    return false;
                }

                if (exact.State.LifecycleState == MissionAcgLifecycleState.Accepted)
                {
                    MissionAcgBindingRecord active;
                    string transitionFailure;
                    if (!MissionAcgBindingRuntime.TryTransition(
                        exact,
                        MissionAcgLifecycleState.Active,
                        MissionAcgCleanupState.None,
                        DateTime.UtcNow,
                        out active,
                        out transitionFailure))
                    {
                        MissionDiagnostics.Log(
                            "ENTRY-REJECT char={0} accepted={1}:{2} reason=active-transition-failed detail={3}",
                            character.Identity.Instance,
                            exact.Binding.AcceptedQuestIdentity.Type,
                            exact.Binding.AcceptedQuestIdentity.Instance,
                            transitionFailure);
                        return false;
                    }

                    exact = active;
                }

                int fromPlayfield = character.Playfield.Identity.Instance;
                StampObjective(
                    exact.Binding.AllocatedLivePlayfield2,
                    exact.Binding.MissionType);
                StampMissionQuality(
                    exact.Binding.AllocatedLivePlayfield2,
                    exact.Binding.MissionQuality);
                StampShapeSource(
                    exact.Binding.AllocatedLivePlayfield2,
                    materialized.Bundle.SourcePlayfield2);
                StampOutdoorReturn(
                    character.Identity.Instance,
                    exact.Binding.ExteriorEntranceIdentity.Instance,
                    exact.Binding.ExteriorX,
                    exact.Binding.ExteriorY,
                    exact.Binding.ExteriorZ);
                MissionTokenProgressTracker.BindCharacter(
                    exact.Binding.AllocatedLivePlayfield2,
                    character.Identity.Instance);
                MissionAcgRuntimeManager.ClearSent(character);

                var exactPlayfield =
                    new Identity
                    {
                        Type = IdentityType.Playfield,
                        Instance = exact.Binding.AllocatedLivePlayfield2
                    };
                character.DoNotDoTimers = false;
                character.Teleport(
                    new Coordinate
                    {
                        x = materialized.Spawn.X,
                        y = materialized.Spawn.Y,
                        z = materialized.Spawn.Z
                    },
                    character.Heading,
                    exactPlayfield);
                AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);

                MissionDiagnostics.Log(
                    "ENTRY-TELEPORT char={0} fromPf={1} accepted={2}:{3} key={4} bundle={5} building={6}:{7} livePf2={8} spawn=({9},{10},{11})",
                    character.Identity.Instance,
                    fromPlayfield,
                    plan.AcceptedQuestIdentity.Type,
                    plan.AcceptedQuestIdentity.Instance,
                    plan.MissionKeyIdentity.Instance,
                    plan.BundleId,
                    plan.BuildingIdentity.Type,
                    plan.BuildingIdentity.Instance,
                    plan.AllocatedLivePlayfield2,
                    materialized.Spawn.X,
                    materialized.Spawn.Y,
                    materialized.Spawn.Z);
                return true;
            }

            if (!MissionKeyGrantService.HasMissionKey(character))
            {
                return false;
            }

            int fromPf = character.Playfield.Identity.Instance;
            int pfId = ResolveInstancePlayfieldId(character);
            MissionRollType objective = ResolveCharacterObjective(character);
            StampObjective(pfId, objective);

            int missionQl = 1;
            MissionAcceptedStore.AcceptedMission latest = null;
            List<MissionAcceptedStore.AcceptedMission> accepted =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (accepted.Count > 0 && accepted[accepted.Count - 1] != null)
            {
                latest = accepted[accepted.Count - 1];
                if (latest.Quality > 0)
                {
                    missionQl = latest.Quality;
                }
            }

            StampMissionQuality(pfId, missionQl);

            string targetName = null;
            int targetSide = (int)Side.Neutral;
            if (latest != null && !string.IsNullOrEmpty(latest.TargetName))
            {
                targetName = latest.TargetName;
                targetSide = latest.TargetSide;
            }
            else if (!TryExtractObjectiveIdentity(latest, objective, out targetName, out targetSide))
            {
                // Accept QFU template always names Suzie Mirabelli for Kill-Person.
                if (objective == MissionRollType.KillPerson)
                {
                    targetName = MissionAcceptCaptureTemplate.TemplateKillTargetName;
                    targetSide = (int)Side.Clan;
                }
                else if (objective == MissionRollType.FindPerson)
                {
                    targetName = MissionTargetNameCatalog.PickFindName(
                        new Random(unchecked(character.Identity.Instance * 733)));
                    targetSide = ResolveFindPersonSideForCharacter(character);
                }
            }

            if (objective == MissionRollType.FindPerson)
            {
                // Contact must match player side (Omni→Omni blue, Clan→Clan yellow) — not Neutral/Monster.
                int playerSide = ResolveFindPersonSideForCharacter(character);
                if (targetSide != (int)Side.Omni && targetSide != (int)Side.Clan)
                {
                    targetSide = playerSide;
                }
            }

            if (!string.IsNullOrEmpty(targetName))
            {
                StampTargetName(pfId, targetName);
                StampTargetSide(pfId, targetSide);
            }

            float sx;
            float sy;
            float sz;
            ResolveInteriorSpawn(pfId, out sx, out sy, out sz);

            var pfIdentity = new Identity { Type = IdentityType.Playfield, Instance = pfId };

            MissionTokenProgressTracker.BindCharacter(pfId, character.Identity.Instance);

            MissionInstanceDoorReplay.ClearSent(character);

            // Remember outdoor return before teleport. Prefer accepted marker (Omni Trade door);
            // fall back to current outdoor coords. Must survive Find Person complete clearing store.
            if (latest != null && latest.MarkerPlayfield != 0)
            {
                StampOutdoorReturn(
                    character.Identity.Instance,
                    latest.MarkerPlayfield,
                    latest.MarkerX,
                    latest.MarkerY,
                    latest.MarkerZ);
            }
            else
            {
                Coordinate outdoor = character.Coordinates();
                StampOutdoorReturn(
                    character.Identity.Instance,
                    fromPf,
                    (float)outdoor.x,
                    (float)outdoor.y,
                    (float)outdoor.z);
            }

            character.DoNotDoTimers = false;
            character.Teleport(
                new Coordinate { x = sx, y = sy, z = sz },
                character.Heading,
                pfIdentity);
            AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);

            int building = GetLiveBuildingInstance(pfId);
            int shapePf;
            if (!TryGetShapeSource(pfId, out shapePf))
            {
                shapePf = 0;
            }

            MissionDiagnostics.Log(
                "ENTRY-TELEPORT char={0} fromPf={1} destPf={2} shapePf={3} acgBuilding={4:X8} objective={5} ql={6} target={7} spawn=({8},{9},{10}) teleDest=(545.43,8.51,350.52)",
                character.Identity.Instance,
                fromPf,
                pfId,
                shapePf,
                building,
                MissionTypeCatalog.TypeName(objective),
                missionQl,
                targetName ?? string.Empty,
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
            MissionAcgBindingRecord exactBinding;
            if (MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                character.Playfield.Identity.Instance,
                out exactBinding))
            {
                if (exactBinding.Binding.OwnerIdentity.Instance
                    != character.Identity.Instance)
                {
                    return false;
                }

                destPf = exactBinding.Binding.ExteriorEntranceIdentity.Instance;
                destX = exactBinding.Binding.ExteriorX + OutdoorExitMarkerStandoff;
                destY = exactBinding.Binding.ExteriorY;
                destZ = exactBinding.Binding.ExteriorZ;
            }
            else
            {
                ResolveOutdoorExitDestination(
                    character,
                    out destPf,
                    out destX,
                    out destY,
                    out destZ);
            }

            var pfIdentity = new Identity { Type = IdentityType.Playfield, Instance = destPf };
            character.DoNotDoTimers = false;
            character.Teleport(
                new Coordinate { x = destX, y = destY, z = destZ },
                character.Heading,
                pfIdentity);
            AORebirth.Core.Playfields.Playfield.ArmPostZoneCollisionGrace(character);

            ClearOutdoorReturn(character.Identity.Instance);

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
            MissionAcgMaterializedInstance materialized;
            if (MissionAcgRuntimeManager.TryResolveByPlayfield(
                playfieldId,
                out materialized)
                && materialized.Exit != null
                && materialized.Exit.Position != null)
            {
                x = materialized.Exit.Position.X;
                y = materialized.Exit.Position.Y;
                z = materialized.Exit.Position.Z;
                return;
            }

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
                case 1443840:
                case 1460226:
                case 1456133:
                    // Gold Find Person spawns face into mish along -X (L7 + L220 captures).
                    x -= 6.0f;
                    break;
                case 1419310:
                case 1419335:
                case 1419382:
                case 1441804:
                    x += 6.0f;
                    break;
                case 1419349:
                    // Gold 184103 PAF spawn (1.8,95) already clear of exit door — no nudge.
                    break;
                default:
                    z += OutdoorExitMarkerStandoff;
                    break;
            }
        }

        /// <summary>
        /// Outdoor exit: enter-stamped return → accepted marker → side hub (Omni Trade / Rome) last resort.
        /// Never dump Omni Trade enters into Rome Blue when the stamp was lost (engine restart / complete).
        /// </summary>
        internal static void ResolveOutdoorExitDestination(
            ICharacter character,
            out int destPf,
            out float destX,
            out float destY,
            out float destZ)
        {
            ApplySideHubFallback(character, out destPf, out destX, out destY, out destZ);

            if (character != null)
            {
                OutdoorReturn stamped;
                if (TryGetOutdoorReturn(character.Identity.Instance, out stamped)
                    && stamped != null
                    && stamped.Playfield != 0)
                {
                    destPf = stamped.Playfield;
                    destX = stamped.X;
                    destY = stamped.Y;
                    destZ = stamped.Z;
                }
                else
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
            }

            destX += OutdoorExitMarkerStandoff;
        }

        /// <summary>
        /// Re-stamp outdoor return from accepted marker (zone reconnect / engine restart clears memory).
        /// </summary>
        internal static void TryRestampOutdoorReturnFromAccepted(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            OutdoorReturn existing;
            if (TryGetOutdoorReturn(character.Identity.Instance, out existing)
                && existing != null
                && existing.Playfield != 0)
            {
                return;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (entry != null && entry.MarkerPlayfield != 0)
                {
                    StampOutdoorReturn(
                        character.Identity.Instance,
                        entry.MarkerPlayfield,
                        entry.MarkerX,
                        entry.MarkerY,
                        entry.MarkerZ);
                    MissionDiagnostics.Log(
                        "OUTDOOR-RETURN-RESTAMP char={0} pf={1} xz=({2:0.#},{3:0.#})",
                        character.Identity.Instance,
                        entry.MarkerPlayfield,
                        entry.MarkerX,
                        entry.MarkerZ);
                    return;
                }
            }
        }

        private static void ApplySideHubFallback(
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

            int side = 0;
            if (character != null && character.Stats != null)
            {
                try
                {
                    side = character.Stats[StatIds.side].Value;
                }
                catch
                {
                    side = 0;
                }
            }

            if (side == (int)Side.Omni)
            {
                destPf = OmniTradePlayfieldInstance;
                destX = OmniTradeFallbackSpot[0];
                destY = OmniTradeFallbackSpot[1];
                destZ = OmniTradeFallbackSpot[2];
            }
        }

        internal static int ResolveFindPersonSideForCharacter(ICharacter character)
        {
            int side = (int)Side.Clan;
            if (character != null && character.Stats != null)
            {
                try
                {
                    int raw = character.Stats[StatIds.side].Value;
                    if (raw == (int)Side.Omni || raw == (int)Side.Clan)
                    {
                        return raw;
                    }
                }
                catch
                {
                }
            }

            return side;
        }

        private static void StampOutdoorReturn(int characterInstance, int playfield, float x, float y, float z)
        {
            if (characterInstance == 0 || playfield == 0)
            {
                return;
            }

            lock (ObjectiveGate)
            {
                ReturnByCharacter[characterInstance] = new OutdoorReturn
                                                       {
                                                           Playfield = playfield,
                                                           X = x,
                                                           Y = y,
                                                           Z = z
                                                       };
            }
        }

        private static bool TryGetOutdoorReturn(int characterInstance, out OutdoorReturn value)
        {
            lock (ObjectiveGate)
            {
                return ReturnByCharacter.TryGetValue(characterInstance, out value) && value != null;
            }
        }

        private static void ClearOutdoorReturn(int characterInstance)
        {
            if (characterInstance == 0)
            {
                return;
            }

            lock (ObjectiveGate)
            {
                ReturnByCharacter.Remove(characterInstance);
            }
        }

        /// <summary>
        /// Pull Kill/Find person name + side from the accepted offer text (capture shells name the target
        /// in Info / CharInfos). Spawn must use this — not a random catalog name.
        /// </summary>
        private static bool TryExtractObjectiveIdentity(
            MissionAcceptedStore.AcceptedMission latest,
            MissionRollType objective,
            out string name,
            out int side)
        {
            name = null;
            side = (int)Side.Neutral;
            if (latest == null
                || (objective != MissionRollType.KillPerson && objective != MissionRollType.FindPerson))
            {
                return false;
            }

            QuestInfo offer = latest.Offer;
            string info = offer != null ? offer.Info : null;
            if (string.IsNullOrEmpty(info))
            {
                info = latest.ShortInfo;
            }

            if (!string.IsNullOrEmpty(info))
            {
                if (info.IndexOf("clan side", StringComparison.OrdinalIgnoreCase) >= 0
                    || info.IndexOf("clan officer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    side = (int)Side.Clan;
                }
                else if (info.IndexOf("omni side", StringComparison.OrdinalIgnoreCase) >= 0
                         || info.IndexOf("omni officer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    side = (int)Side.Omni;
                }
            }

            if (offer != null && offer.CharInfos != null)
            {
                for (int i = 0; i < offer.CharInfos.Length; i++)
                {
                    QuestCharInfo ci = offer.CharInfos[i];
                    if (ci != null && !string.IsNullOrEmpty(ci.CharacterName))
                    {
                        name = ci.CharacterName.Trim();
                        return true;
                    }
                }
            }

            // Capture Info: "... (Suzie Mirabelli) is about to ..."
            if (!string.IsNullOrEmpty(info))
            {
                int open = info.IndexOf('(');
                while (open >= 0 && open + 1 < info.Length)
                {
                    int close = info.IndexOf(')', open + 1);
                    if (close <= open + 1)
                    {
                        break;
                    }

                    string candidate = info.Substring(open + 1, close - open - 1).Trim();
                    if (LooksLikePersonName(candidate))
                    {
                        name = candidate;
                        return true;
                    }

                    open = info.IndexOf('(', close + 1);
                }
            }

            return false;
        }

        private static bool LooksLikePersonName(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 3 || value.Length > 40)
            {
                return false;
            }

            int spaces = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == ' ')
                {
                    spaces++;
                    continue;
                }

                if (!char.IsLetter(c) && c != '-' && c != '\'')
                {
                    return false;
                }
            }

            return spaces >= 1 && spaces <= 3;
        }
    }
}
