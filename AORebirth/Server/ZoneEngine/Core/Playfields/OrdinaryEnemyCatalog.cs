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

        private const double SubwayOrdinaryRespawnSeconds = 240.0;

        private const int BloodcreeperMonsterData =
            NpcCombatAttackRules.CapturedSubwayBloodcreeperMonsterData;

        private const int DerangedShopperMonsterData = 203736;

        private const int LooterMonsterData = 203745;

        private const int MuggerMonsterData = 203734;

        private const int RedundantScanMonsterData = 204178;

        private const int IncompleteRebuildMonsterData = 203728;

        private const int FragmentedSoulMonsterData = 203729;

        private const int PrematurePatternMonsterData = 203727;

        private const int PrematurePatternVariantSource = 0x79545356;

        private const int WorkmanStrikerMonsterData = 203854;

        private const int ViolentVagabondMonsterData = 203733;

        private const double BloodcreeperAutomaticAggroRadius = 7.0;

        private const double IncompleteRebuildAutomaticAggroRadius = 7.0;

        private const double MuggerAutomaticAggroRadius = 7.0;

        private const double MuggerSocialAggroRadius = 7.0;

        private const double RedundantScanAutomaticAggroRadius = 7.0;

        private static readonly Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration>
            CapturedOrdinarySpawnPolicies = BuildCapturedOrdinarySpawnPolicies();

        private static readonly HashSet<int> CoherentSubwayOrdinaryCombatSources =
            new HashSet<int>
            {
                unchecked((int)0x7953A9BDu), unchecked((int)0x7953AFDAu),
                unchecked((int)0x795451C5u), unchecked((int)0x79574527u),
                unchecked((int)0x7954519Bu), unchecked((int)0x79513A8Fu),
                unchecked((int)0x7954516Au), unchecked((int)0x795451AEu),
                unchecked((int)0x795451BCu), unchecked((int)0x7953AA1Au),
                unchecked((int)0x79545150u), unchecked((int)0x7954514Fu),
                unchecked((int)0x79545153u), unchecked((int)0x795451A6u),
                unchecked((int)0x795451C9u), unchecked((int)0x79545190u),
                unchecked((int)0x79545196u), unchecked((int)0x79545187u),
                unchecked((int)0x79545198u), unchecked((int)0x795451DDu),
                unchecked((int)0x7954517Bu), unchecked((int)0x79545174u),
                unchecked((int)0x795451B5u), unchecked((int)0x795451C2u),
                unchecked((int)0x7953AA11u), unchecked((int)0x7957E5C7u),
                unchecked((int)0x7957E5C8u), unchecked((int)0x7957E5C6u),
                unchecked((int)0x7957E5CAu), unchecked((int)0x7954516Bu),
                unchecked((int)0x7954516Cu), unchecked((int)0x7952880Bu),
                unchecked((int)0x7952882Au), unchecked((int)0x7953AA55u),
                unchecked((int)0x79528817u), unchecked((int)0x79528828u),
                unchecked((int)0x7953AA1Cu), unchecked((int)0x7953AA53u),
                unchecked((int)0x7953AA56u), unchecked((int)0x7953AA2Au),
                unchecked((int)0x7953AA33u), unchecked((int)0x7953AA2Bu),
                unchecked((int)0x7953AFF7u), unchecked((int)0x79545201u),
                unchecked((int)0x7953A993u), unchecked((int)0x7953AF7Bu),
                unchecked((int)0x7954506Fu), unchecked((int)0x79545083u),
                unchecked((int)0x795450F8u), unchecked((int)0x7953AA4Bu),
                unchecked((int)0x79545202u), unchecked((int)0x7953AA16u),
                unchecked((int)0x7953AABEu), unchecked((int)0x7953AB03u),
                unchecked((int)0x7953AFB8u), unchecked((int)0x7953AA77u),
                unchecked((int)0x7953AFBCu), unchecked((int)0x7953AFDDu),
                unchecked((int)0x79545000u)
            };

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

        private static readonly OrdinaryEnemyAggressionProfile
            IncompleteRebuildAutomaticAggression =
                new OrdinaryEnemyAggressionProfile(
                    OrdinaryEnemyAggressionMode.Auto,
                    IncompleteRebuildAutomaticAggroRadius,
                    true,
                    true,
                    OrdinaryEnemyEvidenceState.Policy);

        private static readonly OrdinaryEnemyAggressionProfile MuggerAutomaticSocialAggression =
            new OrdinaryEnemyAggressionProfile(
                OrdinaryEnemyAggressionMode.Auto,
                MuggerAutomaticAggroRadius,
                true,
                false,
                OrdinaryEnemyEvidenceState.Policy,
                true,
                MuggerSocialAggroRadius);

        private static readonly OrdinaryEnemyAggressionProfile RedundantScanAutomaticAggression =
            new OrdinaryEnemyAggressionProfile(
                OrdinaryEnemyAggressionMode.Auto,
                RedundantScanAutomaticAggroRadius,
                true,
                false,
                OrdinaryEnemyEvidenceState.Policy);

        private static readonly OrdinaryEnemyCorpseProfile StandardGenericCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.Generic,
                0.0,
                60.0,
                0.0);

        private static readonly OrdinaryEnemyCorpseProfile CapturedThiefCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.CapturedThief,
                0.0,
                60.0,
                0.0);

        private static readonly OrdinaryEnemyCorpseProfile CapturedFilthFleaCorpse =
            new OrdinaryEnemyCorpseProfile(
                OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea,
                0.0,
                60.0,
                0.0);

        private readonly Dictionary<string, OrdinaryEnemyProfile> profilesByKey;

        private readonly OrdinaryEnemyProfile[] profiles;

        private readonly OrdinaryEnemySpawnDefinition[] spawns;

        internal OrdinaryEnemyCatalog(
            CapturedSubwayContentProvider supportedContent,
            CapturedSubwayOrdinaryContentProvider ordinaryContent)
            : this(supportedContent, ordinaryContent, null)
        {
        }

        internal OrdinaryEnemyCatalog(
            CapturedSubwayContentProvider supportedContent,
            CapturedSubwayOrdinaryContentProvider ordinaryContent,
            CapturedTempleOfThreeWindsContentProvider templeContent)
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
            BuildSupportedRows(supportedContent, ordinaryContent, profileRows, spawnRows);
            BuildCapturedOrdinaryRows(ordinaryContent, profileRows, spawnRows);
            if (templeContent != null)
            {
                profileRows.AddRange(templeContent.GetProfiles());
                spawnRows.AddRange(templeContent.GetSpawns());
            }

            this.profiles = profileRows
                .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                .ToArray();
            this.spawns = spawnRows
                .OrderBy(value => value.SourceIdentity)
                .ToArray();
            OrdinaryEnemyProfileValidator.Validate(this.profiles, this.spawns);
            ValidateViolentVagabondEvidenceBoundary(this.profiles, this.spawns);
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
            CapturedSubwayOrdinaryContentProvider ordinaryContent,
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
                Func<int, CapturedEnemyCombatContract> contractResolver =
                    first.MonsterData == NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData
                        ? new Func<int, CapturedEnemyCombatContract>(
                            level => CapturedSubwayCombatCatalog.For(first.Name, first.MonsterData, level))
                        : null;
                CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence =
                    ordinaryContent.GetSourceWeaponEvidence(first.MonsterData);
                Func<int, int, CapturedEnemyCombatContract> sourceContractResolver =
                    first.MonsterData
                    != NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData
                    && sourceWeaponEvidence.Length > 0
                        ? new Func<int, int, CapturedEnemyCombatContract>(
                            (sourceIdentity, level) =>
                                CapturedSubwayCombatCatalog.ForSupportedSourceWeapon(
                                    first.Name,
                                    first.MonsterData,
                                    sourceWeaponEvidence,
                                    sourceIdentity))
                        : null;
                CapturedSubwayStrictLootProfileDefinition strictLootProfile =
                    ordinaryContent.GetStrictLootProfile(first.MonsterData);
                OrdinaryEnemyLootEntry[] lootEntries = strictLootProfile == null
                    ? sourceLoot
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
                        .ToArray()
                    : BuildStrictLootEntries(strictLootProfile);
                CapturedEnemyCombatContract contract = first.Combat;
                CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence =
                    ordinaryContent.GetCorpseEvidence(first.MonsterData);
                profiles.Add(
                    new OrdinaryEnemyProfile(
                        SupportedProfileKey(first),
                        "subway.supported",
                        first.Name,
                        first.MonsterData,
                        OrdinaryEnemyConstructionMode.TemplateBacked,
                        first.TemplateHash,
                        BuildSupportedAppearance(first),
                        AggressionFor(first.MonsterData),
                        BuildCombatProfile(
                            contract,
                            first.MonsterData,
                            contractResolver,
                            sourceContractResolver),
                        BuildLootProfile(
                            first.MonsterData,
                            lootEntries,
                            corpseEvidence,
                            strictLootProfile),
                        StandardCorpseProfile(first.MonsterData, corpseEvidence),
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
                        source.HealthDamage,
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
                        OrdinaryEnemyEvidenceState.Policy,
                        SubwayOrdinaryRespawnSeconds,
                        CapturedSubwayContentProvider.IsRuntimeQuarantined(source.SourceInstance)
                            ? OrdinaryEnemyRuntimeDisposition.Quarantined
                            : OrdinaryEnemyRuntimeDisposition.Active,
                        string.Empty,
                        source.ContentSection,
                        string.Empty,
                        null,
                        SubwayOrdinaryRespawnPolicy()));
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
                Func<int, int, CapturedEnemyCombatContract> sourceContractResolver =
                    archetype.SourceWeaponEvidence.Length > 0
                    || archetype.MonsterData
                       == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData
                        ? new Func<int, int, CapturedEnemyCombatContract>(
                            (sourceIdentity, level) =>
                                archetype.MonsterData
                                == NpcCombatAttackRules
                                    .CapturedSubwayMeldedPatternsMonsterData
                                && level == 25
                                    ? CapturedSubwayCombatCatalog.ForOrdinary(
                                            archetype,
                                            sourceIdentity)
                                        .WithProductionWeaponQuality()
                                    : archetype.MonsterData == DerangedShopperMonsterData
                                      || archetype.MonsterData == LooterMonsterData
                                      || CoherentSubwayOrdinaryCombatSources.Contains(sourceIdentity)
                                    ? CapturedSubwayCombatCatalog.ForOrdinary(
                                        archetype,
                                        sourceIdentity)
                                    : CapturedEnemyCombatContract.Unresolved(
                                        string.Format(
                                            CultureInfo.InvariantCulture,
                                            "No coherent same-capture Subway attack chain for source 0x{0:X8}",
                                            sourceIdentity),
                                        archetype.Combat != null && archetype.Combat.Observed))
                        : null;
                Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract>
                    sourceVariantContractResolver =
                        archetype.MonsterData == WorkmanStrikerMonsterData
                        || archetype.MonsterData == IncompleteRebuildMonsterData
                        || archetype.MonsterData == RedundantScanMonsterData
                        || archetype.MonsterData == FragmentedSoulMonsterData
                            ? new Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract>(
                                (sourceIdentity, variant) =>
                                    CapturedSubwayCombatCatalog.ForOrdinary(
                                        archetype,
                                        sourceIdentity,
                                        variant,
                                        content.GetGenerationVariants(
                                            archetype.MonsterData,
                                            sourceIdentity)))
                            : null;
                CapturedSubwayStrictLootProfileDefinition strictLootProfile =
                    content.GetStrictLootProfile(archetype.MonsterData);
                OrdinaryEnemyLootEntry[] lootEntries = strictLootProfile == null
                    ? archetype.LootEvidence
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
                        .ToArray()
                    : BuildStrictLootEntries(strictLootProfile);
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
                        BuildCombatProfile(
                            contract,
                            archetype.MonsterData,
                            null,
                            sourceContractResolver,
                            sourceVariantContractResolver,
                            archetype.Combat != null && archetype.Combat.Observed),
                        BuildLootProfile(
                            archetype.MonsterData,
                            lootEntries,
                            archetype.CorpseEvidence,
                            strictLootProfile),
                        StandardCorpseProfile(
                            archetype.MonsterData,
                            archetype.CorpseEvidence),
                        archetype.EvidenceCaptures,
                        false,
                        false,
                        SupportNanoFor(archetype.MonsterData)));
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
                OrdinaryEnemySpawnLevelDefinition levelDefinition =
                    BuildCapturedLevelDefinition(
                        content,
                        source,
                        policyConfiguration == null
                            ? null
                            : policyConfiguration.LevelDefinition);
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
                        OrdinaryEnemyEvidenceState.Policy,
                        SubwayOrdinaryRespawnSeconds,
                        OrdinaryEnemyRuntimeDisposition.Active,
                        source.SourceOwnerIdentity,
                        source.EvidenceCapture,
                        source.EvidenceTimestamp,
                        levelDefinition,
                        SubwayOrdinaryRespawnPolicy()));
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
                    SubwayOrdinaryRespawnPolicy()));
            return result;
        }

        private static WorldRespawnPolicyAssignment SubwayOrdinaryRespawnPolicy()
        {
            return WorldRespawnPolicyAssignment.Inherit(
                "PF127 Subway regular mobs use the shared 240-second respawn policy");
        }

        private static OrdinaryEnemySpawnLevelDefinition BuildCapturedLevelDefinition(
            CapturedSubwayOrdinaryContentProvider content,
            CapturedSubwayOrdinarySpawnDefinition source,
            OrdinaryEnemySpawnLevelDefinition configuredDefinition)
        {
            int expectedMonsterData = string.Equals(
                source.ArchetypeKey,
                "workman_striker",
                StringComparison.Ordinal)
                ? WorkmanStrikerMonsterData
                : string.Equals(
                    source.ArchetypeKey,
                    "incomplete_rebuild",
                    StringComparison.Ordinal)
                    ? IncompleteRebuildMonsterData
                    : string.Equals(
                        source.ArchetypeKey,
                        "redundant_scan",
                        StringComparison.Ordinal)
                        ? RedundantScanMonsterData
                        : string.Equals(
                            source.ArchetypeKey,
                            "fragmented_soul",
                            StringComparison.Ordinal)
                            ? FragmentedSoulMonsterData
                            : string.Equals(
                                source.ArchetypeKey,
                                "premature_pattern",
                                StringComparison.Ordinal)
                              && source.SourceInstance == PrematurePatternVariantSource
                                ? PrematurePatternMonsterData
                                : 0;
            CapturedSubwayGenerationVariantDefinition[] capturedVariants =
                expectedMonsterData == 0
                    ? new CapturedSubwayGenerationVariantDefinition[0]
                    : content.GetGenerationVariants(expectedMonsterData, source.SourceInstance);
            if (capturedVariants.Length == 0)
            {
                if (expectedMonsterData != 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Captured atomic-generation source 0x{0:X8} has no reviewed variants.",
                            source.SourceInstance));
                }

                return configuredDefinition;
            }

            if (expectedMonsterData == 0
                || capturedVariants.Any(
                    value => value.MonsterData != expectedMonsterData
                             || value.SourceInstance != source.SourceInstance
                             || !((value.WeaponLowId > 0
                                   && value.WeaponHighId > 0
                                   && value.WeaponQuality > 0)
                                  || (value.WeaponLowId == 0
                                      && value.WeaponHighId == 0
                                      && value.WeaponQuality == 0))))
            {
                throw new InvalidOperationException(
                    "Captured ordinary generation variants are attached to an unexpected source.");
            }

            bool variantsHaveWeapon = capturedVariants.All(
                value => value.WeaponLowId > 0
                         && value.WeaponHighId > 0
                         && value.WeaponQuality > 0);
            if (!variantsHaveWeapon
                && capturedVariants.Any(
                    value => value.WeaponLowId != 0
                             || value.WeaponHighId != 0
                             || value.WeaponQuality != 0))
            {
                throw new InvalidOperationException(
                    "Captured ordinary generation variants mix weapon and weaponless rows.");
            }

            OrdinaryEnemySpawnVariant[] variants = capturedVariants
                .Select(
                    value => new OrdinaryEnemySpawnVariant(
                        value.Level,
                        value.Health,
                        value.HealthDamage,
                        value.MonsterScale,
                        value.RunSpeed,
                        value.Evidence,
                        variantsHaveWeapon
                            ? new OrdinaryEnemySpawnWeaponLoadout(
                                value.WeaponLowId,
                                value.WeaponHighId,
                                value.WeaponQuality,
                                value.Evidence)
                            : null))
                .ToArray();
            return OrdinaryEnemySpawnLevelDefinition.ExplicitObservedVariants(
                variants,
                "uniform-selection-private-policy;"
                + (variantsHaveWeapon
                    ? "capture-reviewed atomic level/stat/weapon generations;"
                    : "capture-reviewed atomic level/stat generations;no weapon loadout captured;")
                + string.Join(
                    ",",
                    capturedVariants.Select(value => value.Evidence)
                        .Distinct(StringComparer.Ordinal)));
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
            if (monsterData == MuggerMonsterData)
            {
                return MuggerAutomaticSocialAggression;
            }

            if (monsterData == BloodcreeperMonsterData)
            {
                return BloodcreeperAutomaticAggression;
            }

            if (monsterData == IncompleteRebuildMonsterData)
            {
                return IncompleteRebuildAutomaticAggression;
            }

            return monsterData == RedundantScanMonsterData
                       ? RedundantScanAutomaticAggression
                       : RetaliateAggression();
        }

        private static void ValidateViolentVagabondEvidenceBoundary(
            IEnumerable<OrdinaryEnemyProfile> profiles,
            IEnumerable<OrdinaryEnemySpawnDefinition> spawns)
        {
            OrdinaryEnemyProfile profile = profiles.Single(
                value => value.MonsterData == ViolentVagabondMonsterData);
            CapturedEnemyCombatContract contract = profile.Combat.Contract;
            if (contract == null
                || contract.IsCombatReady
                || contract.AttackModel != CapturedEnemyAttackModel.Unresolved
                || profile.Aggression.Mode != OrdinaryEnemyAggressionMode.Retaliate
                || profile.Aggression.AutomaticAggroRadius.HasValue
                || !profile.Aggression.Chase
                || profile.Aggression.ReturnToSpawn
                || profile.Aggression.EvidenceState != OrdinaryEnemyEvidenceState.Observed)
            {
                throw new InvalidOperationException(
                    "Violent Vagabond combat/aggression evidence boundary drifted");
            }

            OrdinaryEnemySpawnDefinition[] rows = spawns
                .Where(value => value.ProfileKey == profile.ProfileKey)
                .ToArray();
            if (rows.Length != 22
                || rows.Any(
                    value => value.Disposition
                             != OrdinaryEnemyRuntimeDisposition.Active)
                || rows.Any(
                    value => value.RespawnEvidence != OrdinaryEnemyEvidenceState.Policy
                             || value.RespawnDelaySeconds != SubwayOrdinaryRespawnSeconds
                             || value.RespawnPolicy.Mode
                                != WorldRespawnPolicyAssignmentMode.Inherit))
            {
                throw new InvalidOperationException(
                    "Violent Vagabond population/respawn evidence boundary drifted");
            }
        }

        private static OrdinaryEnemySupportNanoProfile SupportNanoFor(int monsterData)
        {
            if (monsterData == IncompleteRebuildMonsterData)
            {
                return OrdinaryEnemySupportNanoProfile.CapturedIncompleteRebuild90405();
            }

            if (monsterData == FragmentedSoulMonsterData)
            {
                return OrdinaryEnemySupportNanoProfile.CapturedFragmentedSoul95447();
            }

            if (monsterData != RedundantScanMonsterData)
            {
                return null;
            }

            return new OrdinaryEnemySupportNanoProfile(
                121336,
                121248,
                60.0,
                1.400106,
                25.590325,
                18000,
                180.0,
                7.5,
                true,
                220,
                0,
                9,
                -13,
                new[]
                    {
                        113, 102, 107, 103, 105, 104, 106, 100, 109, 133, 110, 112,
                        130, 114, 115, 116, 108, 128, 122, 129, 127, 131, 111
                    },
                OrdinaryEnemyEvidenceState.Policy,
                "20260709-222339,20260716-033326,20260716-034104,"
                + "20260716-221358,20260717-214751;"
                + "primary=121336;triggered-self=121248;duration-centiseconds=18000;"
                + "primary-modify=+9;triggered-self-modify=-13;"
                + "nearest-observed-ordinary-target-with-self-fallback");
        }

        private static OrdinaryEnemyCombatProfile BuildCombatProfile(
            CapturedEnemyCombatContract contract,
            int monsterData,
            Func<int, CapturedEnemyCombatContract> contractResolver = null,
            Func<int, int, CapturedEnemyCombatContract> sourceContractResolver = null,
            Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract>
                sourceVariantContractResolver = null,
            bool capturedCombatEvidenceObserved = false)
        {
            OrdinaryEnemyCombatMode mode = OrdinaryEnemyCombatMode.Unresolved;
            OrdinaryEnemyDamageSource damageSource = OrdinaryEnemyDamageSource.Unresolved;
            bool visibleWeapon = false;
            if (sourceContractResolver != null || sourceVariantContractResolver != null)
            {
                mode = OrdinaryEnemyCombatMode.EquippedRanged;
                damageSource = OrdinaryEnemyDamageSource.WeaponRoll;
                visibleWeapon = true;
            }
            else
            {
                switch (contract.AttackModel)
                {
                    case CapturedEnemyAttackModel.FixedAttackInfo:
                        mode = OrdinaryEnemyCombatMode.UnarmedMelee;
                        damageSource = OrdinaryEnemyDamageSource.CapturedFixed;
                        break;

                    case CapturedEnemyAttackModel.EquippedWeapon:
                        mode = monsterData == 26092
                               || monsterData
                                  == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData
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
            }

            return new OrdinaryEnemyCombatProfile(
                mode,
                damageSource,
                visibleWeapon,
                contract,
                contract.IsCombatReady
                || sourceContractResolver != null
                || sourceVariantContractResolver != null
                || capturedCombatEvidenceObserved
                    ? OrdinaryEnemyEvidenceState.Observed
                    : OrdinaryEnemyEvidenceState.Unresolved,
                monsterData == 26092 ? 1.0 : (double?)null,
                monsterData == 26092 ? 1 : (int?)null,
                monsterData == 26092,
                contractResolver,
                sourceContractResolver,
                sourceVariantContractResolver);
        }

        private static OrdinaryEnemyLootEntry[] BuildStrictLootEntries(
            CapturedSubwayStrictLootProfileDefinition strictLootProfile)
        {
            string evidenceReference = string.Join(",", strictLootProfile.EvidenceCaptures);
            return strictLootProfile.Entries
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
                            evidenceReference))
                .ToArray();
        }

        private static OrdinaryEnemyLootProfile BuildLootProfile(
            int monsterData,
            OrdinaryEnemyLootEntry[] entries)
        {
            return BuildLootProfile(
                monsterData,
                entries,
                new CapturedSubwayCorpseEvidenceDefinition[0],
                null);
        }

        private static OrdinaryEnemyLootProfile BuildLootProfile(
            int monsterData,
            OrdinaryEnemyLootEntry[] entries,
            CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence)
        {
            return BuildLootProfile(monsterData, entries, corpseEvidence, null);
        }

        private static OrdinaryEnemyLootProfile BuildLootProfile(
            int monsterData,
            OrdinaryEnemyLootEntry[] entries,
            CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence,
            CapturedSubwayStrictLootProfileDefinition strictLootProfile)
        {
            corpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0];
            OrdinaryEnemyLootEvidence evidence = entries.Length == 0
                ? OrdinaryEnemyLootEvidence.Unresolved
                : strictLootProfile != null
                    ? OrdinaryEnemyLootEvidence.ObservedAvailableLoot
                    : entries.All(value => value.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven)
                        ? OrdinaryEnemyLootEvidence.GuaranteedProven
                        : OrdinaryEnemyLootEvidence.ObservedAvailableLoot;
            int observedCompleteInventories = strictLootProfile == null
                ? entries
                    .Select(value => value.ObservedCorpses)
                    .DefaultIfEmpty(0)
                    .Max()
                : strictLootProfile.ObservedCompleteInventories;
            int observedEmptyInventories;
            if (strictLootProfile != null)
            {
                observedEmptyInventories = strictLootProfile.ObservedEmptyInventories;
            }
            else if (monsterData == 17657)
            {
                observedEmptyInventories = 5;
            }
            else
            {
                observedEmptyInventories = 0;
            }
            string itemEvidenceReference = string.Join(
                ",",
                entries
                    .Select(value => value.EvidenceReference)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal));
            if (monsterData == 17649)
            {
                // Identity-correlated official-live corpses prove level-conditioned values.
                // Keep unobserved levels unresolved instead of inventing a global range or formula.
                // Newly decoded corpse rows also supply the exact visual shape, but do not alter
                // the separately accepted weighted-one item policy or its full evidence set.
                return new OrdinaryEnemyLootProfile(
                    evidence,
                    entries,
                    OrdinaryEnemyLootPoolMode.WeightedOne,
                    5,
                    false,
                    8,
                    5,
                    itemEvidenceReference,
                    OrdinaryEnemyEvidenceState.Observed,
                    null,
                    null,
                    new[]
                        {
                            new OrdinaryEnemyLevelCreditRule(5, 6, 6, 2, "20260709-210452"),
                            new OrdinaryEnemyLevelCreditRule(6, 8, 8, 3, "20260709-210452,20260712-153918,20260719-020104"),
                            new OrdinaryEnemyLevelCreditRule(8, 10, 10, 4, "20260708-143600,20260709-205921,20260713-033511"),
                            new OrdinaryEnemyLevelCreditRule(9, 11, 11, 3, "20260709-220439,20260712-160257,20260713-014714"),
                            new OrdinaryEnemyLevelCreditRule(10, 12, 12, 2, "20260709-220439")
                        });
            }

            if (corpseEvidence.Length > 0 && monsterData != BloodcreeperMonsterData)
            {
                OrdinaryEnemyLevelCreditRule[] levelCreditRules = corpseEvidence
                    .GroupBy(value => value.EnemyLevel)
                    .OrderBy(value => value.Key)
                    .Select(
                        group =>
                            new OrdinaryEnemyLevelCreditRule(
                                group.Key,
                                group.Min(value => value.Credits),
                                group.Max(value => value.Credits),
                                group.Count(),
                                string.Join(
                                    ",",
                                    group.Select(
                                        value => string.Format(
                                            CultureInfo.InvariantCulture,
                                            "{0}:{1}>{2}",
                                            value.Capture,
                                            value.DeadNpcIdentity,
                                            value.CorpseIdentity)))))
                    .ToArray();
                bool preserveIncompleteRebuildCreditProgression =
                    monsterData == IncompleteRebuildMonsterData;
                bool preserveFragmentedSoulCreditProgression =
                    monsterData == FragmentedSoulMonsterData;
                if (preserveIncompleteRebuildCreditProgression)
                {
                    // Four exact levels follow floor((13 * level - 11) / 2).
                    // Fill only the two selectable missing levels as explicit private policy;
                    // exact captured levels retain observed evidence and confidence.
                    levelCreditRules = levelCreditRules
                        .Concat(
                            new[]
                                {
                                    new OrdinaryEnemyLevelCreditRule(
                                        20,
                                        124,
                                        124,
                                        0,
                                        "policy:floor((13*level-11)/2);captured-levels=17,18,19,21",
                                        OrdinaryEnemyEvidenceState.Policy),
                                    new OrdinaryEnemyLevelCreditRule(
                                        22,
                                        137,
                                        137,
                                        0,
                                        "policy:floor((13*level-11)/2);captured-levels=17,18,19,21",
                                        OrdinaryEnemyEvidenceState.Policy)
                                })
                        .OrderBy(value => value.EnemyLevel)
                        .ToArray();
                }
                else if (preserveFragmentedSoulCreditProgression)
                {
                    // Three exact levels follow floor((13 * level - 11) / 2).
                    // Fill only the two selectable missing levels as explicit private policy;
                    // exact captured levels retain observed evidence and confidence.
                    levelCreditRules = levelCreditRules
                        .Concat(
                            new[]
                                {
                                    new OrdinaryEnemyLevelCreditRule(
                                        19,
                                        118,
                                        118,
                                        0,
                                        "policy:floor((13*level-11)/2);captured-levels=17,18,21",
                                        OrdinaryEnemyEvidenceState.Policy),
                                    new OrdinaryEnemyLevelCreditRule(
                                        20,
                                        124,
                                        124,
                                        0,
                                        "policy:floor((13*level-11)/2);captured-levels=17,18,21",
                                        OrdinaryEnemyEvidenceState.Policy)
                                })
                        .OrderBy(value => value.EnemyLevel)
                        .ToArray();
                }
                // Exact L4/L5 rules win first. Other captured Flea spawn levels retain
                // the previously accepted observed-outcome range as private policy;
                // the new 23-credit outcome expands its lower bound.
                bool preserveFilthFleaFallback = monsterData == 17657;
                return new OrdinaryEnemyLootProfile(
                    evidence,
                    entries,
                    OrdinaryEnemyLootPoolMode.IndependentEntries,
                    0,
                    entries.Length > 0
                        && (strictLootProfile == null || strictLootProfile.ItemPoolComplete),
                    observedCompleteInventories,
                    observedEmptyInventories,
                    itemEvidenceReference,
                    preserveFilthFleaFallback
                    || preserveIncompleteRebuildCreditProgression
                    || preserveFragmentedSoulCreditProgression
                        ? OrdinaryEnemyEvidenceState.Policy
                        : OrdinaryEnemyEvidenceState.Observed,
                    preserveFilthFleaFallback ? 23 : (int?)null,
                    preserveFilthFleaFallback ? 79 : (int?)null,
                    levelCreditRules);
            }

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
                    strictLootProfile != null && strictLootProfile.ItemPoolComplete,
                    observedCompleteInventories,
                    observedEmptyInventories,
                    itemEvidenceReference,
                    OrdinaryEnemyEvidenceState.Policy,
                    150,
                    150,
                    new[]
                        {
                            new OrdinaryEnemyLevelCreditRule(
                                24,
                                150,
                                150,
                                3,
                                "20260712-223719,20260716-033326,20260716-034104")
                        });
            }

            return new OrdinaryEnemyLootProfile(
                evidence,
                entries,
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                0,
                entries.Length > 0
                    && (strictLootProfile == null || strictLootProfile.ItemPoolComplete),
                observedCompleteInventories,
                observedEmptyInventories,
                itemEvidenceReference,
                OrdinaryEnemyEvidenceState.Unresolved,
                null,
                null,
                new OrdinaryEnemyLevelCreditRule[0]);
        }

        private static OrdinaryEnemyCorpseProfile StandardCorpseProfile(int monsterData)
        {
            return StandardCorpseProfile(
                monsterData,
                new CapturedSubwayCorpseEvidenceDefinition[0]);
        }

        private static OrdinaryEnemyCorpseProfile StandardCorpseProfile(
            int monsterData,
            CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence)
        {
            corpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0];
            if (corpseEvidence.Length > 0)
            {
                OrdinaryEnemyCorpsePacketProfile packetProfile = monsterData == 26092
                    ? OrdinaryEnemyCorpsePacketProfile.CapturedThief
                    : monsterData == 17657
                        ? OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea
                        : OrdinaryEnemyCorpsePacketProfile.Generic;
                int[] catMeshes = corpseEvidence
                    .Select(value => value.CatMesh)
                    .Distinct()
                    .ToArray();
                if (catMeshes.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Captured ordinary corpse evidence has conflicting CATMesh values: "
                        + monsterData.ToString(CultureInfo.InvariantCulture));
                }

                return new OrdinaryEnemyCorpseProfile(
                    packetProfile,
                    0.0,
                    60.0,
                    0.0,
                    catMeshes[0],
                    string.Join(
                        ",",
                        corpseEvidence.Select(
                            value => string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}:{1}>{2}",
                                value.Capture,
                                value.DeadNpcIdentity,
                                value.CorpseIdentity))));
            }

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
