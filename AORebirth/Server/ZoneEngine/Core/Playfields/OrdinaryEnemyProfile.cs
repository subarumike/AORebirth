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

    internal enum OrdinaryEnemyEvidenceState
    {
        Invalid = 0,
        Observed = 1,
        Unresolved = 2,
        Conflicting = 3
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
            OrdinaryEnemyEvidenceState evidenceState)
        {
            this.Mode = mode;
            this.DamageSource = damageSource;
            this.VisibleWeapon = visibleWeapon;
            this.Contract = contract;
            this.EvidenceState = evidenceState;
        }

        internal OrdinaryEnemyCombatMode Mode { get; private set; }
        internal OrdinaryEnemyDamageSource DamageSource { get; private set; }
        internal bool VisibleWeapon { get; private set; }
        internal CapturedEnemyCombatContract Contract { get; private set; }
        internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }
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
            int quality,
            int slot,
            int basisPoints,
            OrdinaryEnemyLootEvidence evidence)
        {
            this.LowId = lowId;
            this.HighId = highId;
            this.Quality = quality;
            this.Slot = slot;
            this.BasisPoints = basisPoints;
            this.Evidence = evidence;
        }

        internal int LowId { get; private set; }
        internal int HighId { get; private set; }
        internal int Quality { get; private set; }
        internal int Slot { get; private set; }
        internal int BasisPoints { get; private set; }
        internal OrdinaryEnemyLootEvidence Evidence { get; private set; }
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
        {
            this.Evidence = evidence;
            this.Entries = entries ?? new OrdinaryEnemyLootEntry[0];
            this.CreditEvidence = creditEvidence;
            this.MinimumCredits = minimumCredits;
            this.MaximumCredits = maximumCredits;
            this.LevelCreditRules = levelCreditRules ?? new OrdinaryEnemyLevelCreditRule[0];
        }

        internal OrdinaryEnemyLootEvidence Evidence { get; private set; }
        internal OrdinaryEnemyLootEntry[] Entries { get; private set; }
        internal OrdinaryEnemyEvidenceState CreditEvidence { get; private set; }
        internal int? MinimumCredits { get; private set; }
        internal int? MaximumCredits { get; private set; }
        internal OrdinaryEnemyLevelCreditRule[] LevelCreditRules { get; private set; }
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
        {
            this.PacketProfile = packetProfile;
            this.EmptyLifetimeSeconds = emptyLifetimeSeconds;
            this.UnlootedLifetimeSeconds = unlootedLifetimeSeconds;
            this.LootedCleanupSeconds = lootedCleanupSeconds;
        }

        internal OrdinaryEnemyCorpsePacketProfile PacketProfile { get; private set; }
        internal double EmptyLifetimeSeconds { get; private set; }
        internal double UnlootedLifetimeSeconds { get; private set; }
        internal double LootedCleanupSeconds { get; private set; }
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
            string sourceTimestamp)
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

        internal bool HasRespawnDelay
        {
            get
            {
                return this.RespawnEvidence == OrdinaryEnemyEvidenceState.Observed
                       && this.RespawnDelaySeconds.HasValue
                       && this.RespawnDelaySeconds.Value > 0.0;
            }
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
                    || profile.Corpse.LootedCleanupSeconds <= 0.0)
                {
                    throw new InvalidOperationException("Ordinary enemy construction or corpse lifecycle data is invalid: " + profile.ProfileKey);
                }

                if (profile.Loot.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven
                    && (profile.Loot.Entries.Length == 0
                        || profile.Loot.Entries.Any(
                            value => value.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven)))
                {
                    throw new InvalidOperationException("Observed loot cannot be promoted to guaranteed loot: " + profile.ProfileKey);
                }

                if (profile.Loot.Entries.GroupBy(value => value.Slot).Any(value => value.Count() > 1)
                    || profile.Loot.Entries.Any(
                        value => value.Slot < 0
                                 || value.BasisPoints <= 0
                                 || value.BasisPoints > 10000
                                 || value.Evidence == OrdinaryEnemyLootEvidence.Invalid))
                {
                    throw new InvalidOperationException("Ordinary enemy loot profile has invalid slots or evidence: " + profile.ProfileKey);
                }

                if (profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto
                    && (!profile.Aggression.AutomaticAggroRadius.HasValue
                        || profile.Aggression.AutomaticAggroRadius.Value <= 0.0))
                {
                    throw new InvalidOperationException("Automatic aggression requires a positive captured radius: " + profile.ProfileKey);
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

                if (spawn.RespawnEvidence == OrdinaryEnemyEvidenceState.Observed
                    && !spawn.HasRespawnDelay)
                {
                    throw new InvalidOperationException("Observed respawn requires a positive delay: " + spawn.SpawnKey);
                }
            }
        }
    }
}
