namespace ZoneEngine.Core.Missions
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    #endregion

    internal enum MissionAcgNpcRole
    {
        Ambient = 1,
        KillTarget = 2,
        FindPerson = 3
    }

    internal enum MissionAcgNpcLifeState
    {
        Alive = 1,
        Dead = 2,
        Cleaned = 3,
        Unresolved = 4
    }

    internal enum MissionAcgNpcCombatState
    {
        Stationary = 1,
        CombatReady = 2,
        Fighting = 3,
        Dead = 4,
        NonCombat = 5
    }

    internal enum MissionAcgCorpseState
    {
        None = 0,
        Pending = 1,
        Available = 2,
        Despawned = 3,
        Cleaned = 4
    }

    internal enum MissionAcgLootAuthority
    {
        CaptureProvenFixed = 1,
        CaptureProvenFiniteSet = 2,
        CaptureProvenEmpty = 3,
        ExplicitNoLoot = 4,
        UnresolvedEmpty = 5
    }

    internal enum MissionAcgOperationalCleanupState
    {
        Active = 1,
        Pending = 2,
        Completed = 3
    }

    /// <summary>
    /// Mutable combat state for one captured NPC slot. Immutable placement and appearance fields
    /// are copied from the selected bundle solely to validate durable restoration.
    /// </summary>
    internal sealed class MissionAcgNpcRuntimeState
    {
        internal MissionAcgNpcRuntimeState(
            int capturedSlot,
            MissionAcgIdentityRecord capturedIdentity,
            MissionAcgIdentityRecord runtimeIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            int monsterData,
            int level,
            int maximumHealth,
            int currentHealth,
            int monsterScale,
            int? headMesh,
            string name,
            MissionAcgNpcRole role,
            MissionAcgNpcLifeState lifeState,
            MissionAcgNpcCombatState combatState,
            MissionAcgIdentityRecord corpseIdentity,
            MissionAcgCorpseState corpseState,
            int spawnGeneration,
            bool cleanupCompleted)
        {
            if (capturedSlot < 0
                || capturedIdentity == null
                || runtimeIdentity == null
                || position == null
                || heading == null
                || !Enum.IsDefined(typeof(MissionAcgNpcRole), role)
                || !Enum.IsDefined(typeof(MissionAcgNpcLifeState), lifeState)
                || !Enum.IsDefined(typeof(MissionAcgNpcCombatState), combatState)
                || !Enum.IsDefined(typeof(MissionAcgCorpseState), corpseState)
                || level < 0
                || maximumHealth < 0
                || currentHealth < 0
                || currentHealth > maximumHealth
                || spawnGeneration < 1
                || ((corpseState == MissionAcgCorpseState.None) != (corpseIdentity == null)))
            {
                throw new ArgumentException("Mission ACG NPC runtime state is invalid.");
            }

            this.CapturedSlot = capturedSlot;
            this.CapturedIdentity = capturedIdentity;
            this.RuntimeIdentity = runtimeIdentity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.MonsterData = monsterData;
            this.Level = level;
            this.MaximumHealth = maximumHealth;
            this.CurrentHealth = currentHealth;
            this.MonsterScale = monsterScale;
            this.HeadMesh = headMesh;
            this.Name = (name ?? string.Empty).Trim();
            this.Role = role;
            this.LifeState = lifeState;
            this.CombatState = combatState;
            this.CorpseIdentity = corpseIdentity;
            this.CorpseState = corpseState;
            this.SpawnGeneration = spawnGeneration;
            this.CleanupCompleted = cleanupCompleted;
        }

        internal int CapturedSlot { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal MissionAcgIdentityRecord RuntimeIdentity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal int MonsterData { get; private set; }

        internal int Level { get; private set; }

        internal int MaximumHealth { get; private set; }

        internal int CurrentHealth { get; private set; }

        internal int MonsterScale { get; private set; }

        internal int? HeadMesh { get; private set; }

        internal string Name { get; private set; }

        internal MissionAcgNpcRole Role { get; private set; }

        internal MissionAcgNpcLifeState LifeState { get; private set; }

        internal MissionAcgNpcCombatState CombatState { get; private set; }

        internal MissionAcgIdentityRecord CorpseIdentity { get; private set; }

        internal MissionAcgCorpseState CorpseState { get; private set; }

        internal int SpawnGeneration { get; private set; }

        internal bool CleanupCompleted { get; private set; }

        internal bool IsMaterializable
        {
            get
            {
                return this.LifeState != MissionAcgNpcLifeState.Unresolved
                       && this.TemplateId > 0
                       && this.MonsterData > 0
                       && this.Level > 0
                       && this.MaximumHealth > 0
                       && MissionAcgSpatialValidator.IsFinite(this.Position)
                       && MissionAcgSpatialValidator.IsFinite(this.Heading);
            }
        }

        internal MissionAcgNpcRuntimeState WithHealth(
            int currentHealth,
            MissionAcgNpcCombatState combatState)
        {
            int bounded = Math.Max(0, Math.Min(this.MaximumHealth, currentHealth));
            return this.Copy(
                this.LifeState,
                combatState,
                this.CorpseIdentity,
                this.CorpseState,
                bounded,
                this.CleanupCompleted);
        }

        internal MissionAcgNpcRuntimeState WithDeath(
            MissionAcgIdentityRecord corpseIdentity)
        {
            return this.Copy(
                MissionAcgNpcLifeState.Dead,
                MissionAcgNpcCombatState.Dead,
                corpseIdentity,
                corpseIdentity == null
                    ? MissionAcgCorpseState.None
                    : MissionAcgCorpseState.Pending,
                0,
                this.CleanupCompleted);
        }

        internal MissionAcgNpcRuntimeState WithCorpseState(
            MissionAcgCorpseState corpseState)
        {
            return this.Copy(
                this.LifeState,
                this.CombatState,
                corpseState == MissionAcgCorpseState.None ? null : this.CorpseIdentity,
                corpseState,
                this.CurrentHealth,
                this.CleanupCompleted);
        }

        internal MissionAcgNpcRuntimeState WithCleanup()
        {
            return this.Copy(
                MissionAcgNpcLifeState.Cleaned,
                MissionAcgNpcCombatState.Dead,
                this.CorpseIdentity,
                this.CorpseIdentity == null
                    ? MissionAcgCorpseState.None
                    : MissionAcgCorpseState.Cleaned,
                0,
                true);
        }

        private MissionAcgNpcRuntimeState Copy(
            MissionAcgNpcLifeState lifeState,
            MissionAcgNpcCombatState combatState,
            MissionAcgIdentityRecord corpseIdentity,
            MissionAcgCorpseState corpseState,
            int currentHealth,
            bool cleanupCompleted)
        {
            return new MissionAcgNpcRuntimeState(
                this.CapturedSlot,
                this.CapturedIdentity,
                this.RuntimeIdentity,
                this.Position,
                this.Heading,
                this.TemplateId,
                this.MonsterData,
                this.Level,
                this.MaximumHealth,
                currentHealth,
                this.MonsterScale,
                this.HeadMesh,
                this.Name,
                this.Role,
                lifeState,
                combatState,
                corpseIdentity,
                corpseState,
                this.SpawnGeneration,
                cleanupCompleted);
        }
    }

    internal sealed class MissionAcgChestRuntimeState
    {
        internal MissionAcgChestRuntimeState(
            int capturedSlot,
            MissionAcgIdentityRecord capturedIdentity,
            MissionAcgIdentityRecord runtimeIdentity,
            MissionAcgLootAuthority lootAuthority,
            bool isOpen,
            bool isExhausted,
            int transferredItemCount,
            bool cleanupCompleted)
        {
            if (capturedSlot < 0
                || capturedIdentity == null
                || runtimeIdentity == null
                || !Enum.IsDefined(typeof(MissionAcgLootAuthority), lootAuthority)
                || transferredItemCount < 0
                || (!isOpen && isExhausted)
                || (lootAuthority == MissionAcgLootAuthority.UnresolvedEmpty
                    && transferredItemCount != 0))
            {
                throw new ArgumentException("Mission ACG chest runtime state is invalid.");
            }

            this.CapturedSlot = capturedSlot;
            this.CapturedIdentity = capturedIdentity;
            this.RuntimeIdentity = runtimeIdentity;
            this.LootAuthority = lootAuthority;
            this.IsOpen = isOpen;
            this.IsExhausted = isExhausted;
            this.TransferredItemCount = transferredItemCount;
            this.CleanupCompleted = cleanupCompleted;
        }

        internal int CapturedSlot { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal MissionAcgIdentityRecord RuntimeIdentity { get; private set; }

        internal MissionAcgLootAuthority LootAuthority { get; private set; }

        internal bool IsOpen { get; private set; }

        internal bool IsExhausted { get; private set; }

        internal int TransferredItemCount { get; private set; }

        internal bool CleanupCompleted { get; private set; }

        internal MissionAcgChestRuntimeState WithOpen(bool isOpen)
        {
            return new MissionAcgChestRuntimeState(
                this.CapturedSlot,
                this.CapturedIdentity,
                this.RuntimeIdentity,
                this.LootAuthority,
                isOpen,
                isOpen && this.IsExhausted,
                this.TransferredItemCount,
                this.CleanupCompleted);
        }

        internal MissionAcgChestRuntimeState WithCleanup()
        {
            return new MissionAcgChestRuntimeState(
                this.CapturedSlot,
                this.CapturedIdentity,
                this.RuntimeIdentity,
                this.LootAuthority,
                true,
                true,
                this.TransferredItemCount,
                true);
        }
    }

    /// <summary>
    /// Versioned mutable Stage 5 state. The layout payload and captured slot records remain in the
    /// immutable catalog and are never serialized into this state as new authority.
    /// </summary>
    internal sealed class MissionAcgOperationalState
    {
        internal const int CurrentFormatVersion = 1;

        private readonly Dictionary<int, MissionAcgNpcRuntimeState> npcByRuntime;

        private readonly Dictionary<int, MissionAcgChestRuntimeState> chestByRuntime;

        internal MissionAcgOperationalState(
            int formatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            int allocatedLivePlayfield2,
            string bundleId,
            string bundlePayloadSha256,
            MissionAcgIdentityRecord buildingIdentity,
            IEnumerable<MissionAcgNpcRuntimeState> npcs,
            IEnumerable<MissionAcgChestRuntimeState> chests,
            MissionAcgOperationalCleanupState cleanupState,
            DateTime updatedUtc)
        {
            if (formatVersion != CurrentFormatVersion
                || acceptedQuestIdentity == null
                || ownerIdentity == null
                || buildingIdentity == null
                || allocatedLivePlayfield2 <= 0
                || string.IsNullOrWhiteSpace(bundleId)
                || string.IsNullOrWhiteSpace(bundlePayloadSha256)
                || !Enum.IsDefined(typeof(MissionAcgOperationalCleanupState), cleanupState)
                || updatedUtc == DateTime.MinValue)
            {
                throw new ArgumentException("Mission ACG operational state identity is invalid.");
            }

            this.FormatVersion = formatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.OwnerIdentity = ownerIdentity;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.BundleId = bundleId.Trim();
            this.BundlePayloadSha256 = bundlePayloadSha256.Trim().ToLowerInvariant();
            this.BuildingIdentity = buildingIdentity;
            this.CleanupState = cleanupState;
            this.UpdatedUtc =
                updatedUtc.Kind == DateTimeKind.Utc
                    ? updatedUtc
                    : updatedUtc.ToUniversalTime();

            this.npcByRuntime = new Dictionary<int, MissionAcgNpcRuntimeState>();
            var npcList = new List<MissionAcgNpcRuntimeState>();
            foreach (MissionAcgNpcRuntimeState npc in npcs ?? new MissionAcgNpcRuntimeState[0])
            {
                if (this.npcByRuntime.ContainsKey(npc.RuntimeIdentity.Instance))
                {
                    throw new ArgumentException("Duplicate mission ACG NPC runtime identity.");
                }

                this.npcByRuntime.Add(npc.RuntimeIdentity.Instance, npc);
                npcList.Add(npc);
            }

            this.chestByRuntime = new Dictionary<int, MissionAcgChestRuntimeState>();
            var chestList = new List<MissionAcgChestRuntimeState>();
            foreach (MissionAcgChestRuntimeState chest in
                chests ?? new MissionAcgChestRuntimeState[0])
            {
                if (this.chestByRuntime.ContainsKey(chest.RuntimeIdentity.Instance))
                {
                    throw new ArgumentException("Duplicate mission ACG chest runtime identity.");
                }

                this.chestByRuntime.Add(chest.RuntimeIdentity.Instance, chest);
                chestList.Add(chest);
            }

            this.Npcs = new ReadOnlyCollection<MissionAcgNpcRuntimeState>(npcList);
            this.Chests = new ReadOnlyCollection<MissionAcgChestRuntimeState>(chestList);
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal string BundleId { get; private set; }

        internal string BundlePayloadSha256 { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal IList<MissionAcgNpcRuntimeState> Npcs { get; private set; }

        internal IList<MissionAcgChestRuntimeState> Chests { get; private set; }

        internal MissionAcgOperationalCleanupState CleanupState { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal bool TryGetNpc(int runtimeInstance, out MissionAcgNpcRuntimeState npc)
        {
            return this.npcByRuntime.TryGetValue(runtimeInstance, out npc);
        }

        internal bool TryGetChest(int runtimeInstance, out MissionAcgChestRuntimeState chest)
        {
            return this.chestByRuntime.TryGetValue(runtimeInstance, out chest);
        }

        internal MissionAcgOperationalState ReplaceNpc(
            MissionAcgNpcRuntimeState replacement,
            DateTime updatedUtc)
        {
            var next = new List<MissionAcgNpcRuntimeState>(this.Npcs.Count);
            bool replaced = false;
            for (int i = 0; i < this.Npcs.Count; i++)
            {
                MissionAcgNpcRuntimeState current = this.Npcs[i];
                if (current.RuntimeIdentity.Instance == replacement.RuntimeIdentity.Instance)
                {
                    next.Add(replacement);
                    replaced = true;
                }
                else
                {
                    next.Add(current);
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException("Mission ACG NPC is not registered.");
            }

            return this.Copy(next, this.Chests, this.CleanupState, updatedUtc);
        }

        internal MissionAcgOperationalState ReplaceChest(
            MissionAcgChestRuntimeState replacement,
            DateTime updatedUtc)
        {
            var next = new List<MissionAcgChestRuntimeState>(this.Chests.Count);
            bool replaced = false;
            for (int i = 0; i < this.Chests.Count; i++)
            {
                MissionAcgChestRuntimeState current = this.Chests[i];
                if (current.RuntimeIdentity.Instance == replacement.RuntimeIdentity.Instance)
                {
                    next.Add(replacement);
                    replaced = true;
                }
                else
                {
                    next.Add(current);
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException("Mission ACG chest is not registered.");
            }

            return this.Copy(this.Npcs, next, this.CleanupState, updatedUtc);
        }

        internal MissionAcgOperationalState BeginCleanup(DateTime updatedUtc)
        {
            var npcs = new List<MissionAcgNpcRuntimeState>(this.Npcs.Count);
            for (int i = 0; i < this.Npcs.Count; i++)
            {
                npcs.Add(this.Npcs[i].WithCleanup());
            }

            var chests = new List<MissionAcgChestRuntimeState>(this.Chests.Count);
            for (int i = 0; i < this.Chests.Count; i++)
            {
                chests.Add(this.Chests[i].WithCleanup());
            }

            return this.Copy(
                npcs,
                chests,
                MissionAcgOperationalCleanupState.Pending,
                updatedUtc);
        }

        internal MissionAcgOperationalState CompleteCleanup(DateTime updatedUtc)
        {
            return this.Copy(
                this.Npcs,
                this.Chests,
                MissionAcgOperationalCleanupState.Completed,
                updatedUtc);
        }

        private MissionAcgOperationalState Copy(
            IEnumerable<MissionAcgNpcRuntimeState> npcs,
            IEnumerable<MissionAcgChestRuntimeState> chests,
            MissionAcgOperationalCleanupState cleanupState,
            DateTime updatedUtc)
        {
            return new MissionAcgOperationalState(
                this.FormatVersion,
                this.AcceptedQuestIdentity,
                this.OwnerIdentity,
                this.AllocatedLivePlayfield2,
                this.BundleId,
                this.BundlePayloadSha256,
                this.BuildingIdentity,
                npcs,
                chests,
                cleanupState,
                updatedUtc);
        }
    }

    internal static class MissionAcgSpatialValidator
    {
        internal static bool IsFinite(MissionAcgPointRecord point)
        {
            return point != null
                   && IsFinite(point.X)
                   && IsFinite(point.Y)
                   && IsFinite(point.Z);
        }

        internal static bool IsFinite(MissionAcgRotationRecord rotation)
        {
            return rotation != null
                   && IsFinite(rotation.X)
                   && IsFinite(rotation.Y)
                   && IsFinite(rotation.Z)
                   && IsFinite(rotation.W);
        }

        internal static bool TryValidate(
            MissionAcgLayoutBundle bundle,
            MissionAcgMaterializedInstance instance,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            failure = string.Empty;
            if (bundle == null || instance == null)
            {
                failure = "Layout bundle and materialized instance are required.";
                return false;
            }

            if (!IsFinite(bundle.EntryPoint) || !IsFinite(bundle.Exit.Position))
            {
                failure = "Captured entry or exit coordinate is missing or non-finite.";
                return false;
            }

            var slots = new HashSet<int>();
            var captured = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                MissionAcgNpcSlotRecord npc = bundle.NpcSlots[i];
                if (!slots.Add(npc.Slot)
                    || !captured.Add(IdentityKey(npc.CapturedIdentity))
                    || !IsFinite(npc.Position)
                    || !IsFinite(npc.Heading))
                {
                    failure = "Captured NPC slots are duplicate or spatially invalid.";
                    return false;
                }
            }

            for (int i = 0; i < bundle.Dynels.Count; i++)
            {
                MissionAcgDynelRecord dynel = bundle.Dynels[i];
                if (!IsFinite(dynel.Position) || !IsFinite(dynel.Heading))
                {
                    failure = "Captured dynel coordinate is non-finite.";
                    return false;
                }
            }

            if (objective != null)
            {
                MissionAcgRuntimeObject runtimeObject;
                MissionAcgMaterializedInstance resolved;
                MissionAcgIdentityRecord runtimeIdentity =
                    objective.Binding.RuntimeObjectiveIdentity;
                bool found = false;
                for (int i = 0; i < instance.Objects.Count; i++)
                {
                    runtimeObject = instance.Objects[i];
                    if (runtimeObject.Identity.RuntimeIdentity.Equals(runtimeIdentity)
                        && IsFinite(runtimeObject.Position))
                    {
                        found = true;
                        break;
                    }
                }

                resolved = instance;
                if (!found || resolved == null)
                {
                    failure = "Objective is assigned to a missing runtime slot.";
                    return false;
                }
            }

            return true;
        }

        internal static bool IsWithinDistance(
            MissionAcgPointRecord first,
            MissionAcgPointRecord second,
            double maximumDistance)
        {
            if (!IsFinite(first) || !IsFinite(second) || maximumDistance < 0.0d)
            {
                return false;
            }

            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            double dz = first.Z - second.Z;
            return ((dx * dx) + (dy * dy) + (dz * dz))
                   <= maximumDistance * maximumDistance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }
}
