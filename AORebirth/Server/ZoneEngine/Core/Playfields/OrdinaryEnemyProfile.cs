namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum OrdinaryEnemyConstructionMode
    {
        Unresolved = 0,
        TemplateBacked = 1,
        CapturedDirect = 2
    }

    internal enum OrdinaryEnemyAggressionMode
    {
        Unresolved = 0,
        Passive = 1,
        Retaliate = 2,
        Auto = 3,
        Scripted = 4
    }

    internal enum OrdinaryEnemyMovementMode
    {
        Unresolved = 0,
        Static = 1,
        Patrol = 2,
        Roam = 3,
        Scripted = 4
    }

    internal enum OrdinaryEnemyCombatMode
    {
        Unresolved = 0,
        UnarmedMelee = 1,
        NaturalMelee = 2,
        EquippedMelee = 3,
        EquippedRanged = 4,
        Nano = 5,
        Hybrid = 6,
        Scripted = 7
    }

    internal enum OrdinaryEnemyDamageSource
    {
        Unresolved = 0,
        CapturedFixed = 1,
        WeaponRoll = 2,
        ProfileRange = 3,
        NaturalAttack = 4,
        Scripted = 5
    }

    internal enum OrdinaryEnemyLootEvidence
    {
        Invalid = 0,
        GuaranteedProven = 1,
        ObservedAvailableLoot = 2,
        ProfileInherited = 3,
        NoneProven = 4,
        Unresolved = 5
    }

    internal enum OrdinaryEnemyLootPoolMode
    {
        Invalid = 0,
        IndependentEntries = 1,
        WeightedOne = 2
    }

    internal enum OrdinaryEnemyLootLinkageEvidence
    {
        Invalid = 0,
        ProvenEnemyCorpseItem = 1,
        ProvenTransferredEnemyCorpseItem = 2,
        ImportedCaptureEvidence = 3,
        Ambiguous = 4,
        Unresolved = 5
    }

    internal enum OrdinaryEnemyLootProbabilityEvidence
    {
        Invalid = 0,
        GuaranteedProven = 1,
        ExistingCapturePolicy = 2,
        ProvisionalProjectPolicy = 3,
        Unresolved = 4
    }

    internal enum OrdinaryEnemyEvidenceState
    {
        Invalid = 0,
        Observed = 1,
        Unresolved = 2,
        Conflicting = 3,
        Policy = 4
    }

    internal enum OrdinaryEnemyRuntimeDisposition
    {
        Invalid = 0,
        Active = 1,
        Quarantined = 2
    }

    internal enum OrdinaryEnemyScfuProfile
    {
        Generic = 0,
        CapturedThief = 1,
        CapturedFilthFlea = 2,
        CapturedExact = 3
    }

    internal enum OrdinaryEnemyCorpsePacketProfile
    {
        Generic = 0,
        CapturedThief = 1,
        CapturedFilthFlea = 2
    }

    internal sealed class OrdinaryEnemyAggressionProfile
    {
        internal OrdinaryEnemyAggressionProfile(
            OrdinaryEnemyAggressionMode mode,
            double? automaticAggroRadius,
            bool chase,
            bool returnToSpawn,
            OrdinaryEnemyEvidenceState evidenceState)
        {
            this.Mode = mode;
            this.AutomaticAggroRadius = automaticAggroRadius;
            this.Chase = chase;
            this.ReturnToSpawn = returnToSpawn;
            this.EvidenceState = evidenceState;
        }

        internal OrdinaryEnemyAggressionMode Mode { get; private set; }
        internal double? AutomaticAggroRadius { get; private set; }
        internal bool Chase { get; private set; }
        internal bool ReturnToSpawn { get; private set; }
        internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }
    }

    internal sealed class OrdinaryEnemyCombatProfile
    {
        internal OrdinaryEnemyCombatProfile(
            OrdinaryEnemyCombatMode mode,
            OrdinaryEnemyDamageSource damageSource,
            bool visibleWeapon,
            CapturedEnemyCombatContract contract,
            OrdinaryEnemyEvidenceState evidenceState,
            double? healthRegenIntervalSeconds = null,
            int? healthRegenDelta = null,
            bool regenerateHealthWhileInCombat = false)
        {
            this.Mode = mode;
            this.DamageSource = damageSource;
            this.VisibleWeapon = visibleWeapon;
            this.Contract = contract;
            this.EvidenceState = evidenceState;
            this.HealthRegenIntervalSeconds = healthRegenIntervalSeconds;
            this.HealthRegenDelta = healthRegenDelta;
            this.RegenerateHealthWhileInCombat = regenerateHealthWhileInCombat;
        }

        internal OrdinaryEnemyCombatMode Mode { get; private set; }
        internal OrdinaryEnemyDamageSource DamageSource { get; private set; }
        internal bool VisibleWeapon { get; private set; }
        internal CapturedEnemyCombatContract Contract { get; private set; }
        internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }
        internal double? HealthRegenIntervalSeconds { get; private set; }
        internal int? HealthRegenDelta { get; private set; }
        internal bool RegenerateHealthWhileInCombat { get; private set; }
    }

    internal sealed class OrdinaryEnemyTextureProfile
    {
        internal OrdinaryEnemyTextureProfile(int place, int id, int unknown)
        {
            this.Place = place;
            this.Id = id;
            this.Unknown = unknown;
        }

        internal int Place { get; private set; }
        internal int Id { get; private set; }
        internal int Unknown { get; private set; }
    }

    internal sealed class OrdinaryEnemyMeshProfile
    {
        internal OrdinaryEnemyMeshProfile(int position, uint id, int overrideTextureId, int layer)
        {
            this.Position = position;
            this.Id = id;
            this.OverrideTextureId = overrideTextureId;
            this.Layer = layer;
        }

        internal int Position { get; private set; }
        internal uint Id { get; private set; }
        internal int OverrideTextureId { get; private set; }
        internal int Layer { get; private set; }
    }

    internal sealed class OrdinaryEnemyAppearanceProfile
    {
        internal OrdinaryEnemyAppearanceProfile(
            int side,
            int fatness,
            int breed,
            int sex,
            int race,
            int characterFlags,
            int accountFlags,
            int expansions,
            int npcFamily,
            int npcLosHeight,
            int visualFlags,
            int visibleTitle,
            uint appearanceValue,
            int headMesh,
            bool replaceTextures,
            bool clearTemplateHeadWhenZero,
            OrdinaryEnemyTextureProfile[] textures,
            OrdinaryEnemyMeshProfile[] meshes,
            OrdinaryEnemyScfuProfile scfuProfile)
        {
            this.Side = side;
            this.Fatness = fatness;
            this.Breed = breed;
            this.Sex = sex;
            this.Race = race;
            this.CharacterFlags = characterFlags;
            this.AccountFlags = accountFlags;
            this.Expansions = expansions;
            this.NpcFamily = npcFamily;
            this.NpcLosHeight = npcLosHeight;
            this.VisualFlags = visualFlags;
            this.VisibleTitle = visibleTitle;
            this.AppearanceValue = appearanceValue;
            this.HeadMesh = headMesh;
            this.ReplaceTextures = replaceTextures;
            this.ClearTemplateHeadWhenZero = clearTemplateHeadWhenZero;
            this.Textures = textures ?? new OrdinaryEnemyTextureProfile[0];
            this.Meshes = meshes ?? new OrdinaryEnemyMeshProfile[0];
            this.ScfuProfile = scfuProfile;
        }

        internal int Side { get; private set; }
        internal int Fatness { get; private set; }
        internal int Breed { get; private set; }
        internal int Sex { get; private set; }
        internal int Race { get; private set; }
        internal int CharacterFlags { get; private set; }
        internal int AccountFlags { get; private set; }
        internal int Expansions { get; private set; }
        internal int NpcFamily { get; private set; }
        internal int NpcLosHeight { get; private set; }
        internal int VisualFlags { get; private set; }
        internal int VisibleTitle { get; private set; }
        internal uint AppearanceValue { get; private set; }
        internal int HeadMesh { get; private set; }
        internal bool ReplaceTextures { get; private set; }
        internal bool ClearTemplateHeadWhenZero { get; private set; }
        internal OrdinaryEnemyTextureProfile[] Textures { get; private set; }
        internal OrdinaryEnemyMeshProfile[] Meshes { get; private set; }
        internal OrdinaryEnemyScfuProfile ScfuProfile { get; private set; }
    }

    internal sealed class OrdinaryEnemyLootEntry
    {
        internal OrdinaryEnemyLootEntry(
            int lowId,
            int highId,
            int qualityLevel,
            int slot,
            int quantity,
            int weight,
            int dropChanceBasisPoints,
            OrdinaryEnemyLootEvidence evidence,
            OrdinaryEnemyLootLinkageEvidence linkageEvidence,
            OrdinaryEnemyLootProbabilityEvidence probabilityEvidence,
            int observedCount,
            int observedCorpses,
            string evidenceReference)
        {
            this.LowId = lowId;
            this.HighId = highId;
            this.QualityLevel = qualityLevel;
            this.Slot = slot;
            this.Quantity = quantity;
            this.Weight = weight;
            this.DropChanceBasisPoints = dropChanceBasisPoints;
            this.Evidence = evidence;
            this.LinkageEvidence = linkageEvidence;
            this.ProbabilityEvidence = probabilityEvidence;
            this.ObservedCount = observedCount;
            this.ObservedCorpses = observedCorpses;
            this.EvidenceReference = evidenceReference ?? string.Empty;
        }

        internal int LowId { get; private set; }
        internal int HighId { get; private set; }
        internal int QualityLevel { get; private set; }
        internal int Quality { get { return this.QualityLevel; } }
        internal int Slot { get; private set; }
        internal int Quantity { get; private set; }
        internal int Weight { get; private set; }
        internal int DropChanceBasisPoints { get; private set; }
        internal int BasisPoints { get { return this.DropChanceBasisPoints; } }
        internal OrdinaryEnemyLootEvidence Evidence { get; private set; }
        internal OrdinaryEnemyLootLinkageEvidence LinkageEvidence { get; private set; }
        internal OrdinaryEnemyLootProbabilityEvidence ProbabilityEvidence { get; private set; }
        internal int ObservedCount { get; private set; }
        internal int ObservedCorpses { get; private set; }
        internal string EvidenceReference { get; private set; }
    }

    internal sealed class OrdinaryEnemyLootProfile
    {
        internal OrdinaryEnemyLootProfile(
            OrdinaryEnemyLootEvidence evidence,
            OrdinaryEnemyLootEntry[] entries,
            OrdinaryEnemyEvidenceState creditEvidence,
            int? minimumCredits,
            int? maximumCredits)
            : this(
                evidence,
                entries,
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                0,
                entries != null && entries.Length > 0,
                0,
                0,
                string.Empty,
                creditEvidence,
                minimumCredits,
                maximumCredits,
                new OrdinaryEnemyLevelCreditRule[0])
        {
        }

        internal OrdinaryEnemyLootProfile(
            OrdinaryEnemyLootEvidence evidence,
            OrdinaryEnemyLootEntry[] entries,
            OrdinaryEnemyEvidenceState creditEvidence,
            int? minimumCredits,
            int? maximumCredits,
            OrdinaryEnemyLevelCreditRule[] levelCreditRules)
            : this(
                evidence,
                entries,
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                0,
                entries != null && entries.Length > 0,
                0,
                0,
                string.Empty,
                creditEvidence,
                minimumCredits,
                maximumCredits,
                levelCreditRules)
        {
        }

        internal OrdinaryEnemyLootProfile(
            OrdinaryEnemyLootEvidence evidence,
            OrdinaryEnemyLootEntry[] entries,
            OrdinaryEnemyLootPoolMode poolMode,
            int emptyWeight,
            bool itemPoolComplete,
            int observedCompleteInventories,
            int observedEmptyInventories,
            string itemEvidenceReference,
            OrdinaryEnemyEvidenceState creditEvidence,
            int? minimumCredits,
            int? maximumCredits,
            OrdinaryEnemyLevelCreditRule[] levelCreditRules)
            : this(
                evidence,
                entries,
                poolMode,
                emptyWeight,
                itemPoolComplete,
                observedCompleteInventories,
                observedEmptyInventories,
                itemEvidenceReference,
                creditEvidence,
                minimumCredits,
                maximumCredits,
                levelCreditRules,
                new int[0],
                string.Empty)
        {
        }

        internal OrdinaryEnemyLootProfile(
            OrdinaryEnemyLootEvidence evidence,
            OrdinaryEnemyLootEntry[] entries,
            OrdinaryEnemyLootPoolMode poolMode,
            int emptyWeight,
            bool itemPoolComplete,
            int observedCompleteInventories,
            int observedEmptyInventories,
            string itemEvidenceReference,
            OrdinaryEnemyEvidenceState creditEvidence,
            int? minimumCredits,
            int? maximumCredits,
            OrdinaryEnemyLevelCreditRule[] levelCreditRules,
            int[] observedCreditOutcomes,
            string creditEvidenceReference)
        {
            this.Evidence = evidence;
            this.Entries = entries ?? new OrdinaryEnemyLootEntry[0];
            this.PoolMode = poolMode;
            this.EmptyWeight = emptyWeight;
            this.ItemPoolComplete = itemPoolComplete;
            this.ObservedCompleteInventories = observedCompleteInventories;
            this.ObservedEmptyInventories = observedEmptyInventories;
            this.ItemEvidenceReference = itemEvidenceReference ?? string.Empty;
            this.CreditEvidence = creditEvidence;
            this.MinimumCredits = minimumCredits;
            this.MaximumCredits = maximumCredits;
            this.LevelCreditRules = levelCreditRules ?? new OrdinaryEnemyLevelCreditRule[0];
            this.ObservedCreditOutcomes = observedCreditOutcomes ?? new int[0];
            this.CreditEvidenceReference = creditEvidenceReference ?? string.Empty;
        }

        internal OrdinaryEnemyLootEvidence Evidence { get; private set; }
        internal OrdinaryEnemyLootEntry[] Entries { get; private set; }
        internal OrdinaryEnemyLootPoolMode PoolMode { get; private set; }
        internal int EmptyWeight { get; private set; }
        internal bool ItemPoolComplete { get; private set; }
        internal int ObservedCompleteInventories { get; private set; }
        internal int ObservedEmptyInventories { get; private set; }
        internal string ItemEvidenceReference { get; private set; }
        internal OrdinaryEnemyEvidenceState CreditEvidence { get; private set; }
        internal int? MinimumCredits { get; private set; }
        internal int? MaximumCredits { get; private set; }
        internal OrdinaryEnemyLevelCreditRule[] LevelCreditRules { get; private set; }
        internal int[] ObservedCreditOutcomes { get; private set; }
        internal string CreditEvidenceReference { get; private set; }
    }

    internal sealed class OrdinaryEnemyLevelCreditRule
    {
        internal OrdinaryEnemyLevelCreditRule(
            int enemyLevel,
            int minimumCredits,
            int maximumCredits,
            int observedCorpses,
            string evidence)
        {
            if (enemyLevel <= 0)
            {
                throw new ArgumentOutOfRangeException("enemyLevel");
            }

            if (minimumCredits < 0 || maximumCredits < minimumCredits)
            {
                throw new ArgumentOutOfRangeException("minimumCredits");
            }

            if (observedCorpses <= 0)
            {
                throw new ArgumentOutOfRangeException("observedCorpses");
            }

            if (string.IsNullOrWhiteSpace(evidence))
            {
                throw new ArgumentException("Credit evidence is required.", "evidence");
            }

            this.EnemyLevel = enemyLevel;
            this.MinimumCredits = minimumCredits;
            this.MaximumCredits = maximumCredits;
            this.ObservedCorpses = observedCorpses;
            this.Evidence = evidence;
        }

        internal int EnemyLevel { get; private set; }
        internal int MinimumCredits { get; private set; }
        internal int MaximumCredits { get; private set; }
        internal int ObservedCorpses { get; private set; }
        internal string Evidence { get; private set; }
    }

    internal sealed class OrdinaryEnemyCorpseProfile
    {
        internal OrdinaryEnemyCorpseProfile(
            OrdinaryEnemyCorpsePacketProfile packetProfile,
            double emptyLifetimeSeconds,
            double unlootedLifetimeSeconds,
            double lootedCleanupSeconds)
            : this(
                packetProfile,
                emptyLifetimeSeconds,
                unlootedLifetimeSeconds,
                lootedCleanupSeconds,
                null,
                string.Empty)
        {
        }

        internal OrdinaryEnemyCorpseProfile(
            OrdinaryEnemyCorpsePacketProfile packetProfile,
            double emptyLifetimeSeconds,
            double unlootedLifetimeSeconds,
            double lootedCleanupSeconds,
            int? capturedCatMesh,
            string visualEvidence)
        {
            this.PacketProfile = packetProfile;
            this.EmptyLifetimeSeconds = emptyLifetimeSeconds;
            this.UnlootedLifetimeSeconds = unlootedLifetimeSeconds;
            this.LootedCleanupSeconds = lootedCleanupSeconds;
            this.CapturedCatMesh = capturedCatMesh;
            this.VisualEvidence = visualEvidence ?? string.Empty;
        }

        internal OrdinaryEnemyCorpsePacketProfile PacketProfile { get; private set; }
        internal double EmptyLifetimeSeconds { get; private set; }
        internal double UnlootedLifetimeSeconds { get; private set; }
        internal double LootedCleanupSeconds { get; private set; }
        internal int? CapturedCatMesh { get; private set; }
        internal string VisualEvidence { get; private set; }
    }

    internal sealed class OrdinaryEnemyProfile
    {
        internal OrdinaryEnemyProfile(
            string profileKey,
            string familyKey,
            string displayName,
            int monsterData,
            OrdinaryEnemyConstructionMode constructionMode,
            string templateHash,
            OrdinaryEnemyAppearanceProfile appearance,
            OrdinaryEnemyAggressionProfile aggression,
            OrdinaryEnemyCombatProfile combat,
            OrdinaryEnemyLootProfile loot,
            OrdinaryEnemyCorpseProfile corpse,
            string[] evidence,
            bool bossOrScripted,
            bool ownedSummon)
        {
            this.ProfileKey = profileKey;
            this.FamilyKey = familyKey;
            this.DisplayName = displayName;
            this.MonsterData = monsterData;
            this.ConstructionMode = constructionMode;
            this.TemplateHash = templateHash;
            this.Appearance = appearance;
            this.Aggression = aggression;
            this.Combat = combat;
            this.Loot = loot;
            this.Corpse = corpse;
            this.Evidence = evidence ?? new string[0];
            this.BossOrScripted = bossOrScripted;
            this.OwnedSummon = ownedSummon;
        }

        internal string ProfileKey { get; private set; }
        internal string FamilyKey { get; private set; }
        internal string DisplayName { get; private set; }
        internal int MonsterData { get; private set; }
        internal OrdinaryEnemyConstructionMode ConstructionMode { get; private set; }
        internal string TemplateHash { get; private set; }
        internal OrdinaryEnemyAppearanceProfile Appearance { get; private set; }
        internal OrdinaryEnemyAggressionProfile Aggression { get; private set; }
        internal OrdinaryEnemyCombatProfile Combat { get; private set; }
        internal OrdinaryEnemyLootProfile Loot { get; private set; }
        internal OrdinaryEnemyCorpseProfile Corpse { get; private set; }
        internal string[] Evidence { get; private set; }
        internal bool BossOrScripted { get; private set; }
        internal bool OwnedSummon { get; private set; }
    }

    internal sealed class OrdinaryEnemyWaypoint
    {
        internal OrdinaryEnemyWaypoint(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
    }

    internal sealed class OrdinaryEnemySpawnVariant
    {
        internal OrdinaryEnemySpawnVariant(
            int level,
            int health,
            int healthDamage,
            int monsterScale,
            int runSpeed,
            string evidence)
        {
            this.Level = level;
            this.Health = health;
            this.HealthDamage = healthDamage;
            this.MonsterScale = monsterScale;
            this.RunSpeed = runSpeed;
            this.Evidence = evidence ?? string.Empty;
        }

        internal int Level { get; private set; }
        internal int Health { get; private set; }
        internal int HealthDamage { get; private set; }
        internal int MonsterScale { get; private set; }
        internal int RunSpeed { get; private set; }
        internal string Evidence { get; private set; }
    }

    internal enum OrdinaryEnemySpawnLevelMode
    {
        Invalid = 0,
        Fixed = 1,
        InclusiveRange = 2
    }

    internal enum OrdinaryEnemyLevelRerollPolicy
    {
        Invalid = 0,
        Never = 1,
        NewPopulationGeneration = 2
    }

    internal sealed class OrdinaryEnemySpawnLevelDefinition
    {
        internal OrdinaryEnemySpawnLevelDefinition(
            OrdinaryEnemySpawnLevelMode mode,
            int minimumLevel,
            int maximumLevel,
            int referenceLevel,
            int referenceHealth,
            int healthPerLevel,
            int healthDamage,
            int monsterScale,
            int referenceRunSpeed,
            int runSpeedPerLevel,
            OrdinaryEnemyLevelRerollPolicy rerollPolicy,
            OrdinaryEnemyEvidenceState evidenceState,
            string evidence)
        {
            this.Mode = mode;
            this.MinimumLevel = minimumLevel;
            this.MaximumLevel = maximumLevel;
            this.ReferenceLevel = referenceLevel;
            this.ReferenceHealth = referenceHealth;
            this.HealthPerLevel = healthPerLevel;
            this.HealthDamage = healthDamage;
            this.MonsterScale = monsterScale;
            this.ReferenceRunSpeed = referenceRunSpeed;
            this.RunSpeedPerLevel = runSpeedPerLevel;
            this.RerollPolicy = rerollPolicy;
            this.EvidenceState = evidenceState;
            this.Evidence = evidence ?? string.Empty;
        }

        internal OrdinaryEnemySpawnLevelMode Mode { get; private set; }
        internal int MinimumLevel { get; private set; }
        internal int MaximumLevel { get; private set; }
        internal int ReferenceLevel { get; private set; }
        internal int ReferenceHealth { get; private set; }
        internal int HealthPerLevel { get; private set; }
        internal int HealthDamage { get; private set; }
        internal int MonsterScale { get; private set; }
        internal int ReferenceRunSpeed { get; private set; }
        internal int RunSpeedPerLevel { get; private set; }
        internal OrdinaryEnemyLevelRerollPolicy RerollPolicy { get; private set; }
        internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }
        internal string Evidence { get; private set; }

        internal static OrdinaryEnemySpawnLevelDefinition Fixed(
            OrdinaryEnemySpawnVariant variant,
            OrdinaryEnemyEvidenceState evidenceState,
            string evidence)
        {
            if (variant == null)
            {
                throw new ArgumentNullException("variant");
            }

            return new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.Fixed,
                variant.Level,
                variant.Level,
                variant.Level,
                variant.Health,
                0,
                variant.HealthDamage,
                variant.MonsterScale,
                variant.RunSpeed,
                0,
                OrdinaryEnemyLevelRerollPolicy.Never,
                evidenceState,
                evidence);
        }

        internal bool IsValid
        {
            get
            {
                if ((this.Mode != OrdinaryEnemySpawnLevelMode.Fixed
                     && this.Mode != OrdinaryEnemySpawnLevelMode.InclusiveRange)
                    || this.MinimumLevel <= 0
                    || this.MaximumLevel < this.MinimumLevel
                    || this.ReferenceLevel < this.MinimumLevel
                    || this.ReferenceLevel > this.MaximumLevel
                    || this.ReferenceHealth <= 0
                    || this.HealthPerLevel < 0
                    || this.HealthDamage < 0
                    || this.MonsterScale <= 0
                    || this.ReferenceRunSpeed <= 0
                    || this.RunSpeedPerLevel < 0
                    || (this.RerollPolicy != OrdinaryEnemyLevelRerollPolicy.Never
                        && this.RerollPolicy != OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration)
                    || (this.EvidenceState != OrdinaryEnemyEvidenceState.Observed
                        && this.EvidenceState != OrdinaryEnemyEvidenceState.Policy)
                    || string.IsNullOrWhiteSpace(this.Evidence))
                {
                    return false;
                }

                if (this.Mode == OrdinaryEnemySpawnLevelMode.Fixed
                    && (this.MinimumLevel != this.MaximumLevel
                        || this.ReferenceLevel != this.MinimumLevel
                        || this.HealthPerLevel != 0
                        || this.RunSpeedPerLevel != 0
                        || this.RerollPolicy != OrdinaryEnemyLevelRerollPolicy.Never))
                {
                    return false;
                }

                if (this.Mode == OrdinaryEnemySpawnLevelMode.InclusiveRange
                    && this.MaximumLevel == this.MinimumLevel)
                {
                    return false;
                }

                long minimumHealth = this.HealthAt(this.MinimumLevel);
                long maximumHealth = this.HealthAt(this.MaximumLevel);
                long minimumRunSpeed = this.RunSpeedAt(this.MinimumLevel);
                long maximumRunSpeed = this.RunSpeedAt(this.MaximumLevel);
                return minimumHealth > 0
                       && minimumHealth <= int.MaxValue
                       && this.HealthDamage < minimumHealth
                       && maximumHealth > 0
                       && maximumHealth <= int.MaxValue
                       && this.HealthDamage < maximumHealth
                       && minimumRunSpeed > 0
                       && minimumRunSpeed <= int.MaxValue
                       && maximumRunSpeed > 0
                       && maximumRunSpeed <= int.MaxValue;
            }
        }

        internal OrdinaryEnemySpawnVariant SelectVariant(Func<int, int> nextRandom)
        {
            if (!this.IsValid)
            {
                throw new InvalidOperationException("Ordinary enemy spawn level definition is invalid.");
            }

            if (this.Mode == OrdinaryEnemySpawnLevelMode.Fixed)
            {
                return this.Resolve(this.MinimumLevel);
            }

            if (nextRandom == null)
            {
                throw new ArgumentNullException("nextRandom");
            }

            int levelCount = this.MaximumLevel - this.MinimumLevel + 1;
            int offset = nextRandom(levelCount);
            if (offset < 0 || offset >= levelCount)
            {
                throw new ArgumentOutOfRangeException("nextRandom");
            }

            return this.Resolve(this.MinimumLevel + offset);
        }

        internal OrdinaryEnemySpawnVariant Resolve(int level)
        {
            if (!this.IsValid)
            {
                throw new InvalidOperationException("Ordinary enemy spawn level definition is invalid.");
            }

            if (level < this.MinimumLevel || level > this.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException("level");
            }

            return new OrdinaryEnemySpawnVariant(
                level,
                (int)this.HealthAt(level),
                this.HealthDamage,
                this.MonsterScale,
                (int)this.RunSpeedAt(level),
                this.Evidence);
        }

        private long HealthAt(int level)
        {
            return (long)this.ReferenceHealth + ((long)(level - this.ReferenceLevel) * this.HealthPerLevel);
        }

        private long RunSpeedAt(int level)
        {
            return (long)this.ReferenceRunSpeed + ((long)(level - this.ReferenceLevel) * this.RunSpeedPerLevel);
        }
    }

    internal sealed class OrdinaryEnemySpawnGeneration
    {
        internal OrdinaryEnemySpawnGeneration(int number, OrdinaryEnemySpawnVariant selectedVariant)
        {
            if (number <= 0)
            {
                throw new ArgumentOutOfRangeException("number");
            }

            if (selectedVariant == null)
            {
                throw new ArgumentNullException("selectedVariant");
            }

            this.Number = number;
            this.SelectedVariant = selectedVariant;
        }

        internal int Number { get; private set; }
        internal OrdinaryEnemySpawnVariant SelectedVariant { get; private set; }
    }

    internal sealed class OrdinaryEnemyLevelSelectionState
    {
        private OrdinaryEnemySpawnGeneration current;

        internal OrdinaryEnemySpawnGeneration ResolveForGeneration(
            OrdinaryEnemySpawnLevelDefinition definition,
            int generation,
            Func<int, int> nextRandom)
        {
            if (definition == null || !definition.IsValid)
            {
                throw new InvalidOperationException("A valid ordinary enemy level definition is required.");
            }

            if (generation <= 0)
            {
                throw new ArgumentOutOfRangeException("generation");
            }

            if (this.current != null)
            {
                if (generation < this.current.Number)
                {
                    throw new InvalidOperationException(
                        "A stale population generation cannot replace the current level selection.");
                }

                if (generation == this.current.Number)
                {
                    return this.current;
                }
            }

            OrdinaryEnemySpawnVariant variant =
                this.current != null
                && definition.RerollPolicy == OrdinaryEnemyLevelRerollPolicy.Never
                    ? definition.Resolve(this.current.SelectedVariant.Level)
                    : definition.SelectVariant(nextRandom);
            this.current = new OrdinaryEnemySpawnGeneration(generation, variant);
            return this.current;
        }

        internal OrdinaryEnemySpawnGeneration Current
        {
            get { return this.current; }
        }
    }

    internal sealed class OrdinaryEnemySpawnDefinition
    {
        internal OrdinaryEnemySpawnDefinition(
            string spawnKey,
            int sourceIdentity,
            string profileKey,
            int playfieldInstance,
            int level,
            int health,
            int healthDamage,
            int monsterScale,
            int runSpeed,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            OrdinaryEnemyMovementMode movementMode,
            OrdinaryEnemyWaypoint[] waypoints,
            bool useCapturedPatrolReplay,
            bool useSpawnAsPatrolStart,
            bool hasCapturedScfuOverride,
            uint capturedScfuFlags,
            int capturedScfuFlags2,
            byte[] capturedScfuUnknown1,
            int capturedScfuUnknown2,
            OrdinaryEnemyEvidenceState respawnEvidence,
            double? respawnDelaySeconds,
            OrdinaryEnemyRuntimeDisposition disposition,
            string sourceOwnerIdentity,
            string sourceCapture,
            string sourceTimestamp,
            OrdinaryEnemySpawnLevelDefinition levelDefinition = null,
            WorldRespawnPolicyAssignment respawnPolicy = null)
        {
            this.SpawnKey = spawnKey;
            this.SourceIdentity = sourceIdentity;
            this.ProfileKey = profileKey;
            this.PlayfieldInstance = playfieldInstance;
            this.Level = level;
            this.Health = health;
            this.HealthDamage = healthDamage;
            this.MonsterScale = monsterScale;
            this.RunSpeed = runSpeed;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.HeadingX = headingX;
            this.HeadingY = headingY;
            this.HeadingZ = headingZ;
            this.HeadingW = headingW;
            this.MovementMode = movementMode;
            this.Waypoints = waypoints ?? new OrdinaryEnemyWaypoint[0];
            this.UseCapturedPatrolReplay = useCapturedPatrolReplay;
            this.UseSpawnAsPatrolStart = useSpawnAsPatrolStart;
            this.HasCapturedScfuOverride = hasCapturedScfuOverride;
            this.CapturedScfuFlags = capturedScfuFlags;
            this.CapturedScfuFlags2 = capturedScfuFlags2;
            this.CapturedScfuUnknown1 = capturedScfuUnknown1 ?? new byte[0];
            this.CapturedScfuUnknown2 = capturedScfuUnknown2;
            this.RespawnEvidence = respawnEvidence;
            this.RespawnDelaySeconds = respawnDelaySeconds;
            this.Disposition = disposition;
            this.SourceOwnerIdentity = sourceOwnerIdentity;
            this.SourceCapture = sourceCapture;
            this.SourceTimestamp = sourceTimestamp;
            this.DefaultVariant = new OrdinaryEnemySpawnVariant(
                level,
                health,
                healthDamage,
                monsterScale,
                runSpeed,
                sourceCapture);
            this.LevelDefinition = levelDefinition
                                   ?? OrdinaryEnemySpawnLevelDefinition.Fixed(
                                       this.DefaultVariant,
                                       OrdinaryEnemyEvidenceState.Observed,
                                       string.IsNullOrWhiteSpace(sourceCapture)
                                           ? "captured-fixed:" + spawnKey
                                           : sourceCapture);
            this.RespawnPolicy = respawnPolicy
                                 ?? BuildCompatibilityRespawnPolicy(
                                     spawnKey,
                                     sourceIdentity,
                                     respawnEvidence,
                                     respawnDelaySeconds,
                                     sourceCapture);
        }

        internal string SpawnKey { get; private set; }
        internal int SourceIdentity { get; private set; }
        internal string ProfileKey { get; private set; }
        internal int PlayfieldInstance { get; private set; }
        internal int Level { get; private set; }
        internal int Health { get; private set; }
        internal int HealthDamage { get; private set; }
        internal int MonsterScale { get; private set; }
        internal int RunSpeed { get; private set; }
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
        internal float HeadingX { get; private set; }
        internal float HeadingY { get; private set; }
        internal float HeadingZ { get; private set; }
        internal float HeadingW { get; private set; }
        internal OrdinaryEnemyMovementMode MovementMode { get; private set; }
        internal OrdinaryEnemyWaypoint[] Waypoints { get; private set; }
        internal bool UseCapturedPatrolReplay { get; private set; }
        internal bool UseSpawnAsPatrolStart { get; private set; }
        internal bool HasCapturedScfuOverride { get; private set; }
        internal uint CapturedScfuFlags { get; private set; }
        internal int CapturedScfuFlags2 { get; private set; }
        internal byte[] CapturedScfuUnknown1 { get; private set; }
        internal int CapturedScfuUnknown2 { get; private set; }
        internal OrdinaryEnemyEvidenceState RespawnEvidence { get; private set; }
        internal double? RespawnDelaySeconds { get; private set; }
        internal OrdinaryEnemyRuntimeDisposition Disposition { get; private set; }
        internal string SourceOwnerIdentity { get; private set; }
        internal string SourceCapture { get; private set; }
        internal string SourceTimestamp { get; private set; }
        internal OrdinaryEnemySpawnLevelDefinition LevelDefinition { get; private set; }
        internal WorldRespawnPolicyAssignment RespawnPolicy { get; private set; }

        private OrdinaryEnemySpawnVariant DefaultVariant { get; set; }

        internal OrdinaryEnemySpawnVariant SelectVariant(Func<int, int> nextRandom)
        {
            return this.LevelDefinition.SelectVariant(nextRandom);
        }

        internal bool HasRespawnDelay
        {
            get
            {
                return (this.RespawnEvidence == OrdinaryEnemyEvidenceState.Observed
                        || this.RespawnEvidence == OrdinaryEnemyEvidenceState.Policy)
                       && this.RespawnDelaySeconds.HasValue
                       && this.RespawnDelaySeconds.Value > 0.0;
            }
        }

        private static WorldRespawnPolicyAssignment BuildCompatibilityRespawnPolicy(
            string spawnKey,
            int sourceIdentity,
            OrdinaryEnemyEvidenceState respawnEvidence,
            double? respawnDelaySeconds,
            string sourceCapture)
        {
            if (respawnEvidence == OrdinaryEnemyEvidenceState.Observed
                || respawnEvidence == OrdinaryEnemyEvidenceState.Policy)
            {
                return WorldRespawnPolicyAssignment.Explicit(
                    new RespawnPolicyDefinition
                    {
                        RespawnPolicyKey = "ordinary.explicit."
                                           + sourceIdentity.ToString(
                                               System.Globalization.CultureInfo.InvariantCulture),
                        Mode = WorldRespawnMode.FixedDelay,
                        FixedDelaySeconds = respawnDelaySeconds,
                        RespawnAtOriginalPosition = true,
                        ResetHealth = true,
                        ResetMovementState = true,
                        ResetAggressionState = true,
                        DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
                        Evidence = sourceCapture,
                        Confidence = respawnEvidence.ToString(),
                        Enabled = true
                    });
            }

            return WorldRespawnPolicyAssignment.Inherit(
                string.IsNullOrWhiteSpace(sourceCapture)
                    ? "ordinary-default:" + spawnKey
                    : sourceCapture);
        }
    }

    internal static class OrdinaryEnemyProfileValidator
    {
        internal static void Validate(
            IEnumerable<OrdinaryEnemyProfile> profiles,
            IEnumerable<OrdinaryEnemySpawnDefinition> spawns)
        {
            OrdinaryEnemyProfile[] profileRows = (profiles ?? Enumerable.Empty<OrdinaryEnemyProfile>()).ToArray();
            OrdinaryEnemySpawnDefinition[] spawnRows = (spawns ?? Enumerable.Empty<OrdinaryEnemySpawnDefinition>()).ToArray();
            var profilesByKey = new Dictionary<string, OrdinaryEnemyProfile>(StringComparer.Ordinal);
            string previousProfileKey = null;
            foreach (OrdinaryEnemyProfile profile in profileRows)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileKey))
                {
                    throw new InvalidOperationException("Ordinary enemy profile key is required.");
                }

                if (previousProfileKey != null
                    && StringComparer.Ordinal.Compare(previousProfileKey, profile.ProfileKey) >= 0)
                {
                    throw new InvalidOperationException("Ordinary enemy profiles must use unique deterministic key ordering.");
                }

                previousProfileKey = profile.ProfileKey;
                if (profilesByKey.ContainsKey(profile.ProfileKey))
                {
                    throw new InvalidOperationException("Duplicate ordinary enemy profile key: " + profile.ProfileKey);
                }

                profilesByKey.Add(profile.ProfileKey, profile);

                if (profile.BossOrScripted || profile.OwnedSummon)
                {
                    throw new InvalidOperationException("Bosses, scripted encounters, and owned summons cannot enter the ordinary enemy catalog: " + profile.ProfileKey);
                }

                if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.Unresolved
                    || profile.Appearance == null
                    || profile.Aggression == null
                    || profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Unresolved
                    || profile.Combat == null
                    || profile.Combat.Contract == null
                    || profile.Loot == null
                    || profile.Loot.Evidence == OrdinaryEnemyLootEvidence.Invalid
                    || profile.Corpse == null)
                {
                    throw new InvalidOperationException("Ordinary enemy profile has an unresolved required runtime component: " + profile.ProfileKey);
                }

                if ((profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked
                     && string.IsNullOrWhiteSpace(profile.TemplateHash))
                    || profile.Corpse.EmptyLifetimeSeconds <= 0.0
                    || profile.Corpse.UnlootedLifetimeSeconds <= 0.0
                    || profile.Corpse.LootedCleanupSeconds <= 0.0
                    || (profile.Corpse.CapturedCatMesh.HasValue
                        && (profile.Corpse.CapturedCatMesh.Value <= 0
                            || profile.Corpse.CapturedCatMesh.Value == 1234567890
                            || string.IsNullOrWhiteSpace(profile.Corpse.VisualEvidence)))
                    || (!profile.Corpse.CapturedCatMesh.HasValue
                        && !string.IsNullOrWhiteSpace(profile.Corpse.VisualEvidence)))
                {
                    throw new InvalidOperationException("Ordinary enemy construction or corpse lifecycle data is invalid: " + profile.ProfileKey);
                }

                ValidateLootProfile(profile.ProfileKey, profile.Loot);

                if (profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto
                    && (!profile.Aggression.AutomaticAggroRadius.HasValue
                        || profile.Aggression.AutomaticAggroRadius.Value <= 0.0))
                {
                    throw new InvalidOperationException("Automatic aggression requires a positive captured radius: " + profile.ProfileKey);
                }

                bool hasHealthRegenInterval = profile.Combat.HealthRegenIntervalSeconds.HasValue;
                bool hasHealthRegenDelta = profile.Combat.HealthRegenDelta.HasValue;
                if (hasHealthRegenInterval != hasHealthRegenDelta
                    || (hasHealthRegenInterval
                        && (profile.Combat.HealthRegenIntervalSeconds.Value <= 0.0
                            || profile.Combat.HealthRegenDelta.Value <= 0))
                    || (profile.Combat.RegenerateHealthWhileInCombat && !hasHealthRegenInterval))
                {
                    throw new InvalidOperationException("Ordinary enemy health regeneration data is invalid: " + profile.ProfileKey);
                }

                if (profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Scripted
                    || profile.Combat.Mode == OrdinaryEnemyCombatMode.Scripted)
                {
                    throw new InvalidOperationException("Scripted behavior must use a custom encounter module: " + profile.ProfileKey);
                }
            }

            var spawnKeys = new HashSet<string>(StringComparer.Ordinal);
            var sourceIdentities = new HashSet<int>();
            int previousSourceIdentity = int.MinValue;
            foreach (OrdinaryEnemySpawnDefinition spawn in spawnRows)
            {
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.SpawnKey))
                {
                    throw new InvalidOperationException("Ordinary enemy spawn key is required.");
                }

                if (!spawnKeys.Add(spawn.SpawnKey))
                {
                    throw new InvalidOperationException("Duplicate ordinary enemy spawn key: " + spawn.SpawnKey);
                }

                if (spawn.SourceIdentity <= 0 || !sourceIdentities.Add(spawn.SourceIdentity))
                {
                    throw new InvalidOperationException("Duplicate or invalid ordinary enemy source identity: " + spawn.SourceIdentity);
                }

                if (spawn.SourceIdentity <= previousSourceIdentity)
                {
                    throw new InvalidOperationException("Ordinary enemy spawns must use deterministic numeric identity ordering.");
                }

                previousSourceIdentity = spawn.SourceIdentity;
                if (!profilesByKey.ContainsKey(spawn.ProfileKey))
                {
                    throw new InvalidOperationException("Ordinary enemy spawn references a missing profile: " + spawn.ProfileKey);
                }

                if (spawn.PlayfieldInstance <= 0
                    || spawn.Disposition == OrdinaryEnemyRuntimeDisposition.Invalid
                    || spawn.MovementMode == OrdinaryEnemyMovementMode.Unresolved
                    || !string.IsNullOrEmpty(spawn.SourceOwnerIdentity))
                {
                    throw new InvalidOperationException("Ordinary enemy spawn has an invalid playfield, disposition, or owner: " + spawn.SpawnKey);
                }

                if ((spawn.MovementMode == OrdinaryEnemyMovementMode.Patrol
                     || spawn.MovementMode == OrdinaryEnemyMovementMode.Roam)
                    && spawn.Waypoints.Length < 2
                    && !spawn.UseCapturedPatrolReplay)
                {
                    throw new InvalidOperationException("Patrol and roam spawns require captured movement data: " + spawn.SpawnKey);
                }

                if (spawn.MovementMode == OrdinaryEnemyMovementMode.Scripted)
                {
                    throw new InvalidOperationException("Scripted movement must use a custom encounter module: " + spawn.SpawnKey);
                }

                if ((spawn.RespawnEvidence == OrdinaryEnemyEvidenceState.Observed
                     || spawn.RespawnEvidence == OrdinaryEnemyEvidenceState.Policy)
                    && !spawn.HasRespawnDelay)
                {
                    throw new InvalidOperationException("Observed or policy respawn requires a positive delay: " + spawn.SpawnKey);
                }

                if (spawn.LevelDefinition == null || !spawn.LevelDefinition.IsValid)
                {
                    throw new InvalidOperationException(
                        "Ordinary enemy spawn level definition is invalid: " + spawn.SpawnKey);
                }

                OrdinaryEnemySpawnVariant sourceVariant =
                    spawn.Level >= spawn.LevelDefinition.MinimumLevel
                    && spawn.Level <= spawn.LevelDefinition.MaximumLevel
                        ? spawn.LevelDefinition.Resolve(spawn.Level)
                        : null;
                if (sourceVariant == null
                    || sourceVariant.Health != spawn.Health
                    || sourceVariant.HealthDamage != spawn.HealthDamage
                    || sourceVariant.MonsterScale != spawn.MonsterScale
                    || sourceVariant.RunSpeed != spawn.RunSpeed)
                {
                    throw new InvalidOperationException(
                        "Ordinary enemy spawn level definition does not preserve its source row: "
                        + spawn.SpawnKey);
                }

                if (spawn.RespawnPolicy == null
                    || !Enum.IsDefined(typeof(WorldRespawnPolicyAssignmentMode), spawn.RespawnPolicy.Mode)
                    || spawn.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Invalid
                    || spawn.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Unresolved
                    || (spawn.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.NoRespawn
                        && string.IsNullOrWhiteSpace(spawn.RespawnPolicy.PolicyKey))
                    || (spawn.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit
                        && !WorldRespawnPolicyValidator.IsSchedulable(
                            spawn.RespawnPolicy.ExplicitPolicy)))
                {
                    throw new InvalidOperationException(
                        "Ordinary enemy spawn respawn policy is invalid: " + spawn.SpawnKey);
                }
            }
        }

        internal static void ValidateLootProfile(
            string profileKey,
            OrdinaryEnemyLootProfile loot)
        {
            string key = string.IsNullOrWhiteSpace(profileKey) ? "<unknown>" : profileKey;
            if (loot == null
                || loot.Evidence == OrdinaryEnemyLootEvidence.Invalid
                || loot.PoolMode == OrdinaryEnemyLootPoolMode.Invalid
                || loot.EmptyWeight < 0
                || loot.ObservedCompleteInventories < 0
                || loot.ObservedEmptyInventories < 0
                || loot.ObservedEmptyInventories > loot.ObservedCompleteInventories)
            {
                throw new InvalidOperationException("Ordinary enemy loot profile is invalid: " + key);
            }

            OrdinaryEnemyLootEntry[] entries = loot.Entries ?? new OrdinaryEnemyLootEntry[0];
            int[] observedCreditOutcomes = loot.ObservedCreditOutcomes ?? new int[0];
            if (observedCreditOutcomes.Length > 0)
            {
                if (loot.CreditEvidence != OrdinaryEnemyEvidenceState.Observed
                    || !loot.MinimumCredits.HasValue
                    || !loot.MaximumCredits.HasValue
                    || loot.MinimumCredits.Value != observedCreditOutcomes.Min()
                    || loot.MaximumCredits.Value != observedCreditOutcomes.Max()
                    || observedCreditOutcomes.Any(value => value < 0)
                    || loot.LevelCreditRules.Length > 0
                    || string.IsNullOrWhiteSpace(loot.CreditEvidenceReference))
                {
                    throw new InvalidOperationException(
                        "Observed ordinary enemy credit outcomes are invalid: " + key);
                }
            }
            else if (!string.IsNullOrWhiteSpace(loot.CreditEvidenceReference))
            {
                throw new InvalidOperationException(
                    "Ordinary enemy credit evidence has no captured outcomes: " + key);
            }

            if (entries.Length == 0)
            {
                if (loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries
                    || loot.EmptyWeight != 0
                    || (loot.Evidence != OrdinaryEnemyLootEvidence.Unresolved
                        && loot.Evidence != OrdinaryEnemyLootEvidence.NoneProven
                        && loot.Evidence != OrdinaryEnemyLootEvidence.ProfileInherited)
                    || (loot.ItemPoolComplete
                        && loot.Evidence != OrdinaryEnemyLootEvidence.NoneProven))
                {
                    throw new InvalidOperationException("Empty ordinary enemy loot profile has active pool semantics: " + key);
                }

                if (loot.ObservedCompleteInventories > 0
                    && string.IsNullOrWhiteSpace(loot.ItemEvidenceReference))
                {
                    throw new InvalidOperationException("Observed empty loot requires an evidence reference: " + key);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(loot.ItemEvidenceReference)
                || loot.ObservedCompleteInventories <= 0
                || entries.Any(
                    value => value == null
                             || value.LowId <= 0
                             || value.HighId <= 0
                             || value.QualityLevel <= 0
                             || value.Slot < 0
                             || value.Quantity <= 0
                             || value.ObservedCount <= 0
                             || value.ObservedCorpses <= 0
                             || value.ObservedCorpses > loot.ObservedCompleteInventories
                             || string.IsNullOrWhiteSpace(value.EvidenceReference)
                             || (value.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven
                                 && value.Evidence != OrdinaryEnemyLootEvidence.ObservedAvailableLoot)
                             || (value.LinkageEvidence
                                     != OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem
                                 && value.LinkageEvidence
                                     != OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem
                                 && value.LinkageEvidence
                                     != OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence)
                             || (value.ProbabilityEvidence
                                     != OrdinaryEnemyLootProbabilityEvidence.GuaranteedProven
                                 && value.ProbabilityEvidence
                                     != OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy
                                 && value.ProbabilityEvidence
                                     != OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy)))
            {
                throw new InvalidOperationException("Ordinary enemy loot entry lacks proven evidence: " + key);
            }

            if (entries.GroupBy(
                    value => string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}",
                        value.LowId,
                        value.HighId,
                        value.QualityLevel),
                    StringComparer.Ordinal)
                .Any(value => value.Count() > 1))
            {
                throw new InvalidOperationException("Duplicate ordinary enemy loot item identity: " + key);
            }

            if (loot.PoolMode == OrdinaryEnemyLootPoolMode.IndependentEntries)
            {
                if (loot.EmptyWeight != 0
                    || entries.GroupBy(value => value.Slot).Any(value => value.Count() > 1)
                    || entries.Any(
                        value => value.Weight != 0
                                 || value.DropChanceBasisPoints <= 0
                                 || value.DropChanceBasisPoints > 10000))
                {
                    throw new InvalidOperationException("Independent ordinary enemy loot semantics are invalid: " + key);
                }
            }
            else if (loot.PoolMode == OrdinaryEnemyLootPoolMode.WeightedOne)
            {
                int slot = entries[0].Slot;
                if (entries.Any(
                    value => value.Slot != slot
                             || value.Weight <= 0
                             || value.DropChanceBasisPoints != 0))
                {
                    throw new InvalidOperationException("Weighted ordinary enemy loot semantics are invalid: " + key);
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported ordinary enemy loot pool mode: " + key);
            }

            if (loot.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven
                && (loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries
                    || entries.Any(
                        value => value.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven
                                 || value.DropChanceBasisPoints != 10000
                                 || value.ProbabilityEvidence
                                     != OrdinaryEnemyLootProbabilityEvidence.GuaranteedProven)))
            {
                throw new InvalidOperationException("Observed loot cannot be promoted to guaranteed loot: " + key);
            }
        }

    }
}
