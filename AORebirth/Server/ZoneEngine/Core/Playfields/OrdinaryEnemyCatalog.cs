namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    internal sealed class OrdinaryEnemyCatalog
    {
        internal const int SubwayPlayfieldInstance = 127;

        private const string QuarantinedOrdinaryCapture = "20260710-202132";

        private const int BloodcreeperMonsterData =
            NpcCombatAttackRules.CapturedSubwayBloodcreeperMonsterData;

        private const double BloodcreeperAutomaticAggroRadius = 7.0;

        private static readonly Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration>
            CapturedOrdinarySpawnPolicies = BuildCapturedOrdinarySpawnPolicies();

        private static readonly OrdinaryEnemyAggressionProfile RetaliateChasingAggression =
            new OrdinaryEnemyAggressionProfile(
                OrdinaryEnemyAggressionMode.Retaliate,
                null,
                true,
                false,
                OrdinaryEnemyEvidenceState.Observed);

        private static readonly OrdinaryEnemyAggressionProfile BloodcreeperAutomaticAggression =
            new OrdinaryEnemyAggressionProfile(
                OrdinaryEnemyAggressionMode.Auto,
                BloodcreeperAutomaticAggroRadius,
                true,
                false,
                OrdinaryEnemyEvidenceState.Observed);

        private static readonly OrdinaryEnemyCorpseProfile StandardGenericCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.Generic,
                3.0,
                300.0,
                3.0);

        private static readonly OrdinaryEnemyCorpseProfile CapturedThiefCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.CapturedThief,
                3.0,
                300.0,
                3.0);

        private static readonly OrdinaryEnemyCorpseProfile CapturedFilthFleaCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea,
                3.0,
                300.0,
                3.0);

        private readonly Dictionary<string, OrdinaryEnemyProfile> profilesByKey;

        private readonly OrdinaryEnemyProfile[] profiles;

        private readonly OrdinaryEnemySpawnDefinition[] spawns;

        internal OrdinaryEnemyCatalog(
            CapturedSubwayContentProvider supportedContent,
            CapturedSubwayOrdinaryContentProvider ordinaryContent)
        {
            if (supportedContent == null)
            {
                throw new ArgumentNullException("supportedContent");
            }

            if (ordinaryContent == null)
            {
                throw new ArgumentNullException("ordinaryContent");
            }

            var profileRows = new List<OrdinaryEnemyProfile>();
            var spawnRows = new List<OrdinaryEnemySpawnDefinition>();
            BuildSupportedRows(supportedContent, profileRows, spawnRows);
            BuildCapturedOrdinaryRows(ordinaryContent, profileRows, spawnRows);

            this.profiles = profileRows
                .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                .ToArray();
            this.spawns = spawnRows
                .OrderBy(value => value.SourceIdentity)
                .ToArray();
            OrdinaryEnemyProfileValidator.Validate(this.profiles, this.spawns);
            this.profilesByKey = this.profiles.ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
        }

        internal OrdinaryEnemyProfile[] GetProfiles()
        {
            return (OrdinaryEnemyProfile[])this.profiles.Clone();
        }

        internal OrdinaryEnemySpawnDefinition[] GetSpawns()
        {
            return (OrdinaryEnemySpawnDefinition[])this.spawns.Clone();
        }

        internal OrdinaryEnemySpawnDefinition[] GetRuntimeSpawns(int playfieldInstance)
        {
            return this.spawns
                .Where(
                    spawn => spawn.PlayfieldInstance == playfieldInstance
                             && (spawn.Disposition == OrdinaryEnemyRuntimeDisposition.Active
                                 || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(
                                     spawn.SourceIdentity)))
                .ToArray();
        }

        internal bool TryGetProfile(string profileKey, out OrdinaryEnemyProfile profile)
        {
            return this.profilesByKey.TryGetValue(profileKey, out profile);
        }

        internal CombatLootTableEntry[] BuildCombatLootTableEntries()
        {
            var result = new List<CombatLootTableEntry>();
            foreach (OrdinaryEnemyProfile profile in this.profiles)
            {
                if (profile.Loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries)
                {
                    continue;
                }

                foreach (OrdinaryEnemyLootEntry loot in profile.Loot.Entries)
                {
                    result.Add(
                        new CombatLootTableEntry
                        {
                            ExactName = profile.DisplayName,
                            MonsterData = profile.MonsterData,
                            NpcFamily = profile.Appearance.NpcFamily,
                            Slot = loot.Slot,
                            DropChanceBasisPoints = loot.BasisPoints,
                            ItemTemplates =
                                new[]
                                {
                                    new CombatLootItemTemplate
                                    {
                                        LowId = loot.LowId,
                                        HighId = loot.HighId,
                                        MinQuality = loot.Quality,
                                        MaxQuality = loot.Quality,
                                        RangeCheck = 0,
                                        DropGroupHash = "ordinary-enemy-profile"
                                    }
                                }
                        });
                }
            }

            return result.ToArray();
        }

        private static void BuildSupportedRows(
            CapturedSubwayContentProvider content,
            ICollection<OrdinaryEnemyProfile> profiles,
            ICollection<OrdinaryEnemySpawnDefinition> spawns)
        {
            CapturedSubwaySpawnDefinition[] sourceSpawns = content.GetAllSpawnDefinitions();
            CapturedSubwayLootDefinition[] sourceLoot = content.GetLootDefinitions();
            foreach (IGrouping<int, CapturedSubwaySpawnDefinition> group in sourceSpawns
                .GroupBy(value => value.MonsterData)
                .OrderBy(value => SupportedProfileKey(value.First()), StringComparer.Ordinal))
            {
                CapturedSubwaySpawnDefinition first = group.First();
                OrdinaryEnemyLootEntry[] lootEntries = sourceLoot
                    .Where(value => value.MonsterData == first.MonsterData)
                    .Select(
                        value =>
                            new OrdinaryEnemyLootEntry(
                                value.LowId,
                                value.HighId,
                                value.Quality,
                                value.Slot,
                                value.Quantity,
                                value.RuntimeWeight,
                                value.ObservedBasisPoints,
                                value.ObservedBasisPoints == 10000
                                    ? OrdinaryEnemyLootEvidence.GuaranteedProven
                                    : OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                                value.LinkageEvidence,
                                value.ProbabilityEvidence,
                                value.ObservedCount,
                                value.ObservedCorpses,
                                value.EvidenceReference))
                    .ToArray();
                CapturedEnemyCombatContract contract = first.Combat;
                profiles.Add(
                    new OrdinaryEnemyProfile(
                        SupportedProfileKey(first),
                        "subway.supported",
                        first.Name,
                        first.MonsterData,
                        OrdinaryEnemyConstructionMode.TemplateBacked,
                        first.TemplateHash,
                        BuildSupportedAppearance(first),
                        RetaliateAggression(),
                        BuildCombatProfile(contract, first.MonsterData),
                        BuildLootProfile(first.MonsterData, lootEntries),
                        StandardCorpseProfile(first.MonsterData),
                        group.Select(value => value.ContentSection)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray(),
                        false,
                        false));
            }

            foreach (CapturedSubwaySpawnDefinition source in sourceSpawns)
            {
                CapturedSubwayPatrolReplaySegment[] replay =
                    content.GetPatrolReplaySegments(source.SourceInstance);
                OrdinaryEnemyWaypoint[] waypoints = source.HasPatrolWaypoint
                    ? new[]
                        {
                            new OrdinaryEnemyWaypoint(source.X, source.Y, source.Z),
                            new OrdinaryEnemyWaypoint(
                                source.PatrolX.Value,
                                source.PatrolY.Value,
                                source.PatrolZ.Value)
                        }
                    : new OrdinaryEnemyWaypoint[0];
                bool patrol = source.HasPatrolWaypoint || replay.Length > 0;
                spawns.Add(
                    new OrdinaryEnemySpawnDefinition(
                        SpawnKey(source.SourceInstance),
                        source.SourceInstance,
                        SupportedProfileKey(source),
                        SubwayPlayfieldInstance,
                        source.Level,
                        source.Health,
                        0,
                        source.MonsterScale,
                        source.RunSpeed,
                        source.X,
                        source.Y,
                        source.Z,
                        0.0f,
                        0.0f,
                        0.0f,
                        1.0f,
                        patrol ? OrdinaryEnemyMovementMode.Patrol : OrdinaryEnemyMovementMode.Static,
                        waypoints,
                        replay.Length > 0,
                        source.UseSpawnAsPatrolStart,
                        false,
                        0,
                        0,
                        new byte[0],
                        0,
                        source.HasRespawnDelay
                            ? OrdinaryEnemyEvidenceState.Observed
                            : OrdinaryEnemyEvidenceState.Unresolved,
                        source.RespawnDelaySeconds,
                        CapturedSubwayContentProvider.IsRuntimeQuarantined(source.SourceInstance)
                            ? OrdinaryEnemyRuntimeDisposition.Quarantined
                            : OrdinaryEnemyRuntimeDisposition.Active,
                        string.Empty,
                        source.ContentSection,
                        string.Empty));
            }
        }

        private static void BuildCapturedOrdinaryRows(
            CapturedSubwayOrdinaryContentProvider content,
            ICollection<OrdinaryEnemyProfile> profiles,
            ICollection<OrdinaryEnemySpawnDefinition> spawns)
        {
            foreach (CapturedSubwayOrdinaryArchetypeDefinition archetype in content.GetArchetypes())
            {
                uint appearance = archetype.AppearanceValue;
                CapturedEnemyCombatContract contract = CapturedSubwayCombatCatalog.ForOrdinary(archetype);
                OrdinaryEnemyLootEntry[] lootEntries = archetype.LootEvidence
                    .Select(
                        (value, index) =>
                            new OrdinaryEnemyLootEntry(
                                value.LowId,
                                value.HighId,
                                value.Quality,
                                index,
                                1,
                                0,
                                value.ObservedBasisPoints,
                                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                                OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence,
                                OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy,
                                value.ObservedCount,
                                value.ObservedCorpses,
                                string.Join(",", archetype.EvidenceCaptures)))
                    .ToArray();
                profiles.Add(
                    new OrdinaryEnemyProfile(
                        OrdinaryProfileKey(archetype.Key),
                        "subway.ordinary." + archetype.FamilyKey,
                        archetype.Name,
                        archetype.MonsterData,
                        OrdinaryEnemyConstructionMode.CapturedDirect,
                        string.Empty,
                        new OrdinaryEnemyAppearanceProfile(
                            (int)(appearance & 7),
                            (int)((appearance & 31) >> 3),
                            Math.Max(1, Math.Min(7, (int)((appearance & 255) >> 5))),
                            (int)((appearance & 1023) >> 8),
                            (int)(appearance >> 10),
                            archetype.CharacterFlags,
                            archetype.AccountFlags,
                            archetype.Expansions,
                            archetype.NpcFamily,
                            archetype.NpcLosHeight,
                            archetype.VisualFlags,
                            archetype.VisibleTitle,
                            appearance,
                            archetype.HeadMesh,
                            true,
                            false,
                            archetype.Textures.Select(
                                value => new OrdinaryEnemyTextureProfile(value.Place, value.Id, value.Unknown))
                                .ToArray(),
                            archetype.Meshes.Select(
                                value =>
                                    new OrdinaryEnemyMeshProfile(
                                        value.Position,
                                        value.Id,
                                        value.OverrideTextureId,
                                        value.Layer))
                                .ToArray(),
                            OrdinaryEnemyScfuProfile.CapturedExact),
                        AggressionFor(archetype.MonsterData),
                        BuildCombatProfile(contract, archetype.MonsterData),
                        BuildLootProfile(archetype.MonsterData, lootEntries),
                        StandardCorpseProfile(archetype.MonsterData),
                        archetype.EvidenceCaptures,
                        false,
                        false));
            }

            foreach (CapturedSubwayOrdinarySpawnDefinition source in content.GetAllSpawns())
            {
                OrdinaryEnemySpawnPolicyConfiguration policyConfiguration;
                CapturedOrdinarySpawnPolicies.TryGetValue(
                    source.ArchetypeKey,
                    out policyConfiguration);
                OrdinaryEnemyWaypoint[] waypoints = source.Waypoints
                    .Select(value => new OrdinaryEnemyWaypoint(value.X, value.Y, value.Z))
                    .ToArray();
                spawns.Add(
                    new OrdinaryEnemySpawnDefinition(
                        SpawnKey(source.SourceInstance),
                        source.SourceInstance,
                        OrdinaryProfileKey(source.ArchetypeKey),
                        SubwayPlayfieldInstance,
                        source.Level,
                        source.Health,
                        source.HealthDamage,
                        source.MonsterScale,
                        source.RunSpeed,
                        source.X,
                        source.Y,
                        source.Z,
                        source.HeadingX,
                        source.HeadingY,
                        source.HeadingZ,
                        source.HeadingW,
                        waypoints.Length > 1
                            ? OrdinaryEnemyMovementMode.Patrol
                            : OrdinaryEnemyMovementMode.Static,
                        waypoints,
                        false,
                        false,
                        true,
                        (uint)source.CapturedFlags,
                        source.CapturedFlags2,
                        source.Unknown1,
                        source.Unknown2,
                        policyConfiguration != null
                        && policyConfiguration.RespawnPolicy.Mode
                           == WorldRespawnPolicyAssignmentMode.Explicit
                            ? OrdinaryEnemyEvidenceState.Policy
                            : OrdinaryEnemyEvidenceState.Unresolved,
                        policyConfiguration != null
                        && policyConfiguration.RespawnPolicy.ExplicitPolicy != null
                            ? policyConfiguration.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds
                            : null,
                        string.Equals(
                            source.EvidenceCapture,
                            QuarantinedOrdinaryCapture,
                            StringComparison.Ordinal)
                            ? OrdinaryEnemyRuntimeDisposition.Quarantined
                            : OrdinaryEnemyRuntimeDisposition.Active,
                        source.SourceOwnerIdentity,
                        source.EvidenceCapture,
                        source.EvidenceTimestamp,
                        policyConfiguration == null
                            ? null
                            : policyConfiguration.LevelDefinition,
                        policyConfiguration == null
                            ? null
                            : policyConfiguration.RespawnPolicy));
            }
        }

        private static Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration>
            BuildCapturedOrdinarySpawnPolicies()
        {
            var result = new Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration>(
                StringComparer.Ordinal);
            result.Add(
                "bloodcreeper",
                new OrdinaryEnemySpawnPolicyConfiguration(
                    new OrdinaryEnemySpawnLevelDefinition(
                        OrdinaryEnemySpawnLevelMode.InclusiveRange,
                        15,
                        25,
                        24,
                        691,
                        33,
                        0,
                        70,
                        83,
                        3,
                        OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration,
                        OrdinaryEnemyEvidenceState.Policy,
                        "community-range:docs/generated/enemy_catalog/enemy_catalog.csv;"
                        + "captured-anchor:20260709-222339;"
                        + "focused-combat:20260716-033326,20260716-034104"),
                    WorldRespawnPolicyAssignment.Explicit(
                        new RespawnPolicyDefinition
                        {
                            RespawnPolicyKey = "ordinary.bloodcreeper.240",
                            Mode = WorldRespawnMode.FixedDelay,
                            FixedDelaySeconds = 240.0,
                            RespawnAtOriginalPosition = true,
                            ResetHealth = true,
                            ResetMovementState = true,
                            ResetAggressionState = true,
                            DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
                            Evidence = "private-regular-enemy-policy;20260716-033326;20260716-034104",
                            Confidence = "POLICY",
                            Enabled = true
                        })));
            return result;
        }

        private static OrdinaryEnemyAppearanceProfile BuildSupportedAppearance(
            CapturedSubwaySpawnDefinition source)
        {
            var textures = new OrdinaryEnemyTextureProfile[0];
            var meshes = new OrdinaryEnemyMeshProfile[0];
            bool replaceTextures = false;
            OrdinaryEnemyScfuProfile scfuProfile = OrdinaryEnemyScfuProfile.Generic;
            if (source.MonsterData == 26092 || source.MonsterData == 203734)
            {
                replaceTextures = true;
                textures = new[]
                    {
                        new OrdinaryEnemyTextureProfile(0, 0x24CA, 0),
                        new OrdinaryEnemyTextureProfile(1, 0x2219, 0),
                        new OrdinaryEnemyTextureProfile(2, 0x24CC, 0),
                        new OrdinaryEnemyTextureProfile(3, 0x24CB, 0),
                        new OrdinaryEnemyTextureProfile(4, 0x24CD, 0)
                    };
                meshes = new[]
                    {
                        new OrdinaryEnemyMeshProfile(0, 160561u, 0, 2),
                        new OrdinaryEnemyMeshProfile(0, (uint)source.HeadMesh, 0, 4),
                        new OrdinaryEnemyMeshProfile(1, 7777u, 0, 2)
                    };
                if (source.MonsterData == 26092)
                {
                    scfuProfile = OrdinaryEnemyScfuProfile.CapturedThief;
                }
            }
            else if (source.MonsterData == 203733)
            {
                replaceTextures = true;
                textures = new[]
                    {
                        new OrdinaryEnemyTextureProfile(0, 0, 0),
                        new OrdinaryEnemyTextureProfile(1, 21824, 0),
                        new OrdinaryEnemyTextureProfile(2, 0, 0),
                        new OrdinaryEnemyTextureProfile(3, 21819, 0),
                        new OrdinaryEnemyTextureProfile(4, 21831, 0)
                    };
                meshes = new[]
                    {
                        new OrdinaryEnemyMeshProfile(0, (uint)source.HeadMesh, 0, 4),
                        new OrdinaryEnemyMeshProfile(1, 136583u, 0, 2)
                    };
            }
            else if (source.MonsterData == 17657)
            {
                scfuProfile = OrdinaryEnemyScfuProfile.CapturedFilthFlea;
            }

            return new OrdinaryEnemyAppearanceProfile(
                3,
                1,
                Math.Max(1, Math.Min(7, source.Breed)),
                source.Sex,
                1,
                source.CharacterFlags,
                0,
                0,
                source.NpcFamily,
                0,
                31,
                0,
                source.MonsterData == 26092 ? 0x00122002u : 0u,
                source.HeadMesh,
                replaceTextures,
                source.HeadMesh == 0,
                textures,
                meshes,
                scfuProfile);
        }

        private static OrdinaryEnemyAggressionProfile RetaliateAggression()
        {
            return RetaliateChasingAggression;
        }

        private static OrdinaryEnemyAggressionProfile AggressionFor(int monsterData)
        {
            return monsterData == BloodcreeperMonsterData
                       ? BloodcreeperAutomaticAggression
                       : RetaliateAggression();
        }

        private static OrdinaryEnemyCombatProfile BuildCombatProfile(
            CapturedEnemyCombatContract contract,
            int monsterData)
        {
            OrdinaryEnemyCombatMode mode = OrdinaryEnemyCombatMode.Unresolved;
            OrdinaryEnemyDamageSource damageSource = OrdinaryEnemyDamageSource.Unresolved;
            bool visibleWeapon = false;
            switch (contract.AttackModel)
            {
                case CapturedEnemyAttackModel.FixedAttackInfo:
                    mode = OrdinaryEnemyCombatMode.UnarmedMelee;
                    damageSource = OrdinaryEnemyDamageSource.CapturedFixed;
                    break;

                case CapturedEnemyAttackModel.EquippedWeapon:
                    mode = monsterData == 26092
                        ? OrdinaryEnemyCombatMode.EquippedRanged
                        : OrdinaryEnemyCombatMode.Unresolved;
                    damageSource = OrdinaryEnemyDamageSource.WeaponRoll;
                    visibleWeapon = true;
                    break;

                case CapturedEnemyAttackModel.Specialized:
                    mode = OrdinaryEnemyCombatMode.NaturalMelee;
                    damageSource = OrdinaryEnemyDamageSource.NaturalAttack;
                    break;
            }

            return new OrdinaryEnemyCombatProfile(
                mode,
                damageSource,
                visibleWeapon,
                contract,
                contract.IsCombatReady
                    ? OrdinaryEnemyEvidenceState.Observed
                    : OrdinaryEnemyEvidenceState.Unresolved);
        }

        private static OrdinaryEnemyLootProfile BuildLootProfile(
            int monsterData,
            OrdinaryEnemyLootEntry[] entries)
        {
            OrdinaryEnemyLootEvidence evidence = entries.Length == 0
                ? OrdinaryEnemyLootEvidence.Unresolved
                : entries.All(value => value.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven)
                    ? OrdinaryEnemyLootEvidence.GuaranteedProven
                    : OrdinaryEnemyLootEvidence.ObservedAvailableLoot;
            int observedCompleteInventories = entries
                .Select(value => value.ObservedCorpses)
                .DefaultIfEmpty(0)
                .Max();
            string itemEvidenceReference = string.Join(
                ",",
                entries
                    .Select(value => value.EvidenceReference)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal));
            if (monsterData == 17657)
            {
                return new OrdinaryEnemyLootProfile(
                    evidence,
                    entries,
                    OrdinaryEnemyLootPoolMode.IndependentEntries,
                    0,
                    true,
                    Math.Max(8, observedCompleteInventories),
                    0,
                    itemEvidenceReference,
                    OrdinaryEnemyEvidenceState.Observed,
                    29,
                    79,
                    new OrdinaryEnemyLevelCreditRule[0]);
            }

            if (monsterData == 17649)
            {
                // Identity-correlated official-live corpses prove level-conditioned values.
                // Keep unobserved levels unresolved instead of inventing a global range or formula.
                return new OrdinaryEnemyLootProfile(
                    evidence,
                    entries,
                    OrdinaryEnemyLootPoolMode.WeightedOne,
                    5,
                    false,
                    7,
                    5,
                    itemEvidenceReference,
                    OrdinaryEnemyEvidenceState.Observed,
                    null,
                    null,
                    new[]
                        {
                            new OrdinaryEnemyLevelCreditRule(5, 6, 6, 2, "20260709-210452"),
                            new OrdinaryEnemyLevelCreditRule(6, 8, 8, 2, "20260709-210452,20260712-153918"),
                            new OrdinaryEnemyLevelCreditRule(8, 10, 10, 4, "20260708-143600,20260709-205921,20260713-033511"),
                            new OrdinaryEnemyLevelCreditRule(9, 11, 11, 3, "20260709-220439,20260712-160257,20260713-014714"),
                            new OrdinaryEnemyLevelCreditRule(10, 12, 12, 2, "20260709-220439")
                        });
            }

            if (monsterData == BloodcreeperMonsterData)
            {
                // Both completed level-24 official-live fights carried 150 credits.
                // Preserve that value across the configured private level range as policy;
                // level 24 retains its exact observed rule and evidence.
                return new OrdinaryEnemyLootProfile(
                    evidence,
                    entries,
                    OrdinaryEnemyLootPoolMode.IndependentEntries,
                    0,
                    false,
                    2,
                    2,
                    "20260716-033326:Corpse:F69003,20260716-034104:Corpse:F69004",
                    OrdinaryEnemyEvidenceState.Policy,
                    150,
                    150,
                    new[]
                        {
                            new OrdinaryEnemyLevelCreditRule(
                                24,
                                150,
                                150,
                                2,
                                "20260716-033326,20260716-034104")
                        });
            }

            return new OrdinaryEnemyLootProfile(
                evidence,
                entries,
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                0,
                entries.Length > 0,
                observedCompleteInventories,
                0,
                itemEvidenceReference,
                OrdinaryEnemyEvidenceState.Unresolved,
                null,
                null,
                new OrdinaryEnemyLevelCreditRule[0]);
        }

        private static OrdinaryEnemyCorpseProfile StandardCorpseProfile(int monsterData)
        {
            if (monsterData == 26092)
            {
                return CapturedThiefCorpse;
            }

            if (monsterData == 17657)
            {
                return CapturedFilthFleaCorpse;
            }

            return StandardGenericCorpse;
        }

        private static string SupportedProfileKey(CapturedSubwaySpawnDefinition source)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "subway.supported.{0}",
                source.MonsterData);
        }

        private static string OrdinaryProfileKey(string archetypeKey)
        {
            return "subway.ordinary." + archetypeKey;
        }

        private static string SpawnKey(int sourceIdentity)
        {
            return string.Format(CultureInfo.InvariantCulture, "subway.{0:X8}", sourceIdentity);
        }

        private sealed class OrdinaryEnemySpawnPolicyConfiguration
        {
            internal OrdinaryEnemySpawnPolicyConfiguration(
                OrdinaryEnemySpawnLevelDefinition levelDefinition,
                WorldRespawnPolicyAssignment respawnPolicy)
            {
                this.LevelDefinition = levelDefinition;
                this.RespawnPolicy = respawnPolicy;
            }

            internal OrdinaryEnemySpawnLevelDefinition LevelDefinition { get; private set; }
            internal WorldRespawnPolicyAssignment RespawnPolicy { get; private set; }
        }
    }
}
