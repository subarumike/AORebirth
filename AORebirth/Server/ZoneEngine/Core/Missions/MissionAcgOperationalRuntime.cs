namespace ZoneEngine.Core.Missions
{
    #region Usings

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Stage 5 runtime boundary for combat-capable captured NPCs and explicitly unresolved-empty
    /// containers. All resolution begins with one durable binding and its isolated live PF2.
    /// </summary>
    internal static class MissionAcgOperationalRuntime
    {
        private const string ProductionShellTemplate = "BART";

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionAcgOperationalState> ByAccepted =
            new Dictionary<int, MissionAcgOperationalState>();

        private static readonly Dictionary<int, MissionAcgOperationalState> ByPlayfield =
            new Dictionary<int, MissionAcgOperationalState>();

        private static readonly HashSet<int> InvalidAccepted = new HashSet<int>();

        private static MissionAcgLayoutCatalog catalog;

        private static MissionAcgOperationalStateStore store;

        private static bool initialized;

        internal static void Initialize(
            IList<MissionAcgBindingRecord> bindings,
            MissionAcgLayoutCatalog layoutCatalog,
            string missionStateDirectory)
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                catalog = layoutCatalog;
                store = new MissionAcgOperationalStateStore(missionStateDirectory);
                ByAccepted.Clear();
                ByPlayfield.Clear();
                InvalidAccepted.Clear();

                foreach (MissionAcgBindingRecord record in
                    bindings ?? new MissionAcgBindingRecord[0])
                {
                    if (!ShouldRestore(record))
                    {
                        continue;
                    }

                    MissionAcgOperationalState ignored;
                    string failure;
                    if (!TryEnsureStateLocked(record, out ignored, out failure))
                    {
                        InvalidAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance);
                        MissionDiagnostics.Log(
                            "ACG-OPERATIONAL-RESTORE-FAIL accepted={0}:{1} path={2} reason={3}",
                            record.Binding.AcceptedQuestIdentity.Type,
                            record.Binding.AcceptedQuestIdentity.Instance,
                            store.PathFor(record.Binding.AcceptedQuestIdentity),
                            failure);
                    }
                }

                initialized = true;
            }
        }

        internal static bool TryEnsureState(
            MissionAcgBindingRecord record,
            out MissionAcgOperationalState state,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                return TryEnsureStateLocked(record, out state, out failure);
            }
        }

        /// <summary>
        /// Returns true when this PF2 belongs to a bound ACG mission, including fail-closed cases.
        /// Legacy mission spawning must run only when this method returns false.
        /// </summary>
        internal static bool TrySpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            MissionAcgBindingRecord bindingRecord;
            if (!MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                playfieldIdentity.Instance,
                out bindingRecord))
            {
                return false;
            }

            MissionAcgOperationalState state;
            string failure;
            if (!TryEnsureState(bindingRecord, out state, out failure))
            {
                MissionDiagnostics.Log(
                    "ACG-OPERATIONAL-SPAWN-BLOCK accepted={0}:{1} livePf2={2} reason={3}",
                    bindingRecord.Binding.AcceptedQuestIdentity.Type,
                    bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                    bindingRecord.Binding.AllocatedLivePlayfield2,
                    failure);
                return true;
            }

            MissionAcgLayoutBundle bundle =
                catalog.FindByLayoutId(bindingRecord.Binding.SelectedBundleId);
            int spawned = 0;
            for (int i = 0; i < state.Npcs.Count; i++)
            {
                MissionAcgNpcRuntimeState npcState = state.Npcs[i];
                if (!npcState.IsMaterializable
                    || npcState.LifeState != MissionAcgNpcLifeState.Alive
                    || npcState.CleanupCompleted)
                {
                    continue;
                }

                Identity runtimeIdentity = ToIdentity(npcState.RuntimeIdentity);
                ICharacter existing =
                    Pool.Instance.GetObject<ICharacter>(playfield.Identity, runtimeIdentity);
                if (existing != null)
                {
                    continue;
                }

                MissionAcgNpcSlotRecord captured = FindNpcSlot(bundle, npcState.CapturedSlot);
                if (captured == null
                    || !captured.CapturedIdentity.Equals(npcState.CapturedIdentity))
                {
                    MissionDiagnostics.Log(
                        "ACG-OPERATIONAL-NPC-BLOCK accepted={0}:{1} livePf2={2} slot={3} reason=immutable-slot-mismatch",
                        bindingRecord.Binding.AcceptedQuestIdentity.Type,
                        bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        bindingRecord.Binding.AllocatedLivePlayfield2,
                        npcState.CapturedSlot);
                    continue;
                }

                var controller = new NPCController();
                Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplateWithIdentity(
                    ProductionShellTemplate,
                    playfield.Identity,
                    new Coordinate
                    {
                        x = captured.Position.X,
                        y = captured.Position.Y,
                        z = captured.Position.Z
                    },
                    new Quaternion(
                        captured.Heading.X,
                        captured.Heading.Y,
                        captured.Heading.Z,
                        captured.Heading.W),
                    controller,
                    captured.CapturedLevel,
                    runtimeIdentity);
                if (mob == null)
                {
                    MissionDiagnostics.Log(
                        "ACG-OPERATIONAL-NPC-BLOCK accepted={0}:{1} livePf2={2} slot={3} reason=production-shell-unavailable",
                        bindingRecord.Binding.AcceptedQuestIdentity.Type,
                        bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        bindingRecord.Binding.AllocatedLivePlayfield2,
                        npcState.CapturedSlot);
                    continue;
                }

                ApplyCapturedNpc(mob, captured, npcState);
                bool nonCombat = npcState.Role == MissionAcgNpcRole.FindPerson;
                if (nonCombat)
                {
                    controller.AiProfile = NpcAiProfile.Passive;
                }
                else if (!MissionInstanceMobCombat.TryPrepareCombat(
                    mob,
                    controller,
                    captured.CapturedLevel))
                {
                    controller.AiProfile = NpcAiProfile.Passive;
                    mob.Stats.SetBaseValueWithoutTriggering(
                        (int)StatIds.flags,
                        mob.Stats[StatIds.flags].BaseValue | 0x00800000u);
                    MissionDiagnostics.Log(
                        "ACG-OPERATIONAL-NPC-COMBAT-BLOCK accepted={0}:{1} livePf2={2} runtime={3}",
                        bindingRecord.Binding.AcceptedQuestIdentity.Type,
                        bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        bindingRecord.Binding.AllocatedLivePlayfield2,
                        npcState.RuntimeIdentity.Instance);
                }

                mob.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.life,
                    (uint)npcState.MaximumHealth);
                mob.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.health,
                    (uint)npcState.CurrentHealth);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
                mob.DoNotDoTimers = false;
                mob.SetFightingTarget(Identity.None);
                playfield.SuspendNpcRegen(mob);
                activateNpc(mob);
                playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
                spawned++;
            }

            MissionDiagnostics.Log(
                "ACG-OPERATIONAL-SPAWN accepted={0}:{1} bundle={2} livePf2={3} capturedNpcSlots={4} spawned={5}",
                bindingRecord.Binding.AcceptedQuestIdentity.Type,
                bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                bindingRecord.Binding.SelectedBundleId,
                bindingRecord.Binding.AllocatedLivePlayfield2,
                state.Npcs.Count,
                spawned);
            return true;
        }

        internal static bool TryValidateCombatTarget(
            ICharacter attacker,
            ICharacter target,
            out string failure)
        {
            failure = string.Empty;
            if (target == null || target.Playfield == null)
            {
                failure = "Target is missing its playfield.";
                return false;
            }

            EnsureInitialized();
            MissionAcgOperationalState state;
            lock (Sync)
            {
                if (!ByPlayfield.TryGetValue(target.Playfield.Identity.Instance, out state))
                {
                    return true;
                }

                MissionAcgNpcRuntimeState npc;
                if (!state.TryGetNpc(target.Identity.Instance, out npc))
                {
                    if (MissionAcgRuntimeManager.IsRuntimeIdentityCandidate(
                        target.Playfield.Identity.Instance,
                        target.Identity))
                    {
                        failure = "Runtime identity is not an operational NPC in this instance.";
                        return false;
                    }

                    return true;
                }

                if (attacker == null
                    || attacker.Playfield == null
                    || attacker.Identity.Instance != state.OwnerIdentity.Instance
                    || attacker.Playfield.Identity.Instance != state.AllocatedLivePlayfield2
                    || npc.LifeState != MissionAcgNpcLifeState.Alive
                    || npc.CurrentHealth <= 0
                    || npc.CleanupCompleted
                    || npc.Role == MissionAcgNpcRole.FindPerson
                    || state.CleanupState != MissionAcgOperationalCleanupState.Active)
                {
                    failure = "Attacker, PF2, runtime NPC, or lifecycle ownership does not match.";
                    return false;
                }

                return true;
            }
        }

        internal static bool IsOperationalNpc(
            int allocatedLivePlayfield2,
            Identity runtimeIdentity)
        {
            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                MissionAcgNpcRuntimeState npc;
                return runtimeIdentity != null
                       && ByPlayfield.TryGetValue(allocatedLivePlayfield2, out state)
                       && state.TryGetNpc(runtimeIdentity.Instance, out npc);
            }
        }

        internal static bool ShouldSuppressCapturedNpcPacket(
            MissionAcgMaterializedInstance instance,
            MissionAcgRuntimeObject runtimeObject)
        {
            if (instance == null || runtimeObject == null)
            {
                return false;
            }

            if (runtimeObject.Identity.Kind != MissionAcgRuntimeObjectKind.AmbientNpc
                && runtimeObject.Identity.Kind != MissionAcgRuntimeObjectKind.ObjectiveNpc)
            {
                return false;
            }

            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                return ByPlayfield.TryGetValue(
                           instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                           out state)
                       || InvalidAccepted.Contains(
                           instance.BindingRecord.Binding.AcceptedQuestIdentity.Instance);
            }
        }

        internal static void NotifyHealthChanged(ICharacter target, int currentHealth)
        {
            if (target == null || target.Playfield == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                MissionAcgNpcRuntimeState npc;
                if (!ByPlayfield.TryGetValue(target.Playfield.Identity.Instance, out state)
                    || !state.TryGetNpc(target.Identity.Instance, out npc)
                    || npc.LifeState != MissionAcgNpcLifeState.Alive)
                {
                    return;
                }

                MissionAcgNpcRuntimeState replacement =
                    npc.WithHealth(
                        currentHealth,
                        currentHealth <= 0
                            ? MissionAcgNpcCombatState.Dead
                            : MissionAcgNpcCombatState.Fighting);
                TryReplaceStateLocked(
                    state,
                    state.ReplaceNpc(replacement, DateTime.UtcNow),
                    "health");
            }
        }

        internal static bool TryPrepareNpcDeath(
            ICharacter target,
            bool createCorpse,
            out Identity corpseIdentity,
            out bool isOperationalNpc)
        {
            corpseIdentity = Identity.None;
            isOperationalNpc = false;
            if (target == null || target.Playfield == null)
            {
                return false;
            }

            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                MissionAcgNpcRuntimeState npc;
                if (!ByPlayfield.TryGetValue(target.Playfield.Identity.Instance, out state)
                    || !state.TryGetNpc(target.Identity.Instance, out npc))
                {
                    return true;
                }

                isOperationalNpc = true;
                if (npc.LifeState == MissionAcgNpcLifeState.Dead
                    && npc.CorpseIdentity != null)
                {
                    corpseIdentity = ToIdentity(npc.CorpseIdentity);
                    return true;
                }

                if (npc.LifeState != MissionAcgNpcLifeState.Alive)
                {
                    return false;
                }

                MissionAcgIdentityRecord corpse =
                    createCorpse
                        ? new MissionAcgIdentityRecord(
                            (int)IdentityType.Corpse,
                            npc.RuntimeIdentity.Instance)
                        : null;
                MissionAcgOperationalState replacement =
                    state.ReplaceNpc(npc.WithDeath(corpse), DateTime.UtcNow);
                if (!TryReplaceStateLocked(state, replacement, "death"))
                {
                    return false;
                }

                corpseIdentity = ToIdentity(corpse);
                return true;
            }
        }

        internal static void NotifyCorpseAvailable(
            ICharacter sourceNpc,
            Identity corpseIdentity)
        {
            if (sourceNpc == null || sourceNpc.Playfield == null || corpseIdentity == null)
            {
                return;
            }

            UpdateCorpseState(
                sourceNpc.Playfield.Identity.Instance,
                sourceNpc.Identity.Instance,
                corpseIdentity,
                MissionAcgCorpseState.Available);
        }

        internal static void NotifyCorpseRemoved(
            int allocatedLivePlayfield2,
            Identity corpseIdentity)
        {
            if (corpseIdentity == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                if (!ByPlayfield.TryGetValue(allocatedLivePlayfield2, out state))
                {
                    return;
                }

                for (int i = 0; i < state.Npcs.Count; i++)
                {
                    MissionAcgNpcRuntimeState npc = state.Npcs[i];
                    if (npc.CorpseIdentity != null
                        && npc.CorpseIdentity.Type == (int)corpseIdentity.Type
                        && npc.CorpseIdentity.Instance == corpseIdentity.Instance)
                    {
                        TryReplaceStateLocked(
                            state,
                            state.ReplaceNpc(
                                npc.WithCorpseState(MissionAcgCorpseState.Despawned),
                                DateTime.UtcNow),
                            "corpse-despawn");
                        return;
                    }
                }
            }
        }

        internal static void NotifyChestOpened(
            MissionAcgMaterializedInstance instance,
            int runtimeInstance)
        {
            if (instance == null)
            {
                return;
            }

            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                MissionAcgChestRuntimeState chest;
                if (!ByPlayfield.TryGetValue(
                        instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                        out state)
                    || !state.TryGetChest(runtimeInstance, out chest)
                    || chest.IsOpen)
                {
                    return;
                }

                TryReplaceStateLocked(
                    state,
                    state.ReplaceChest(chest.WithOpen(true), DateTime.UtcNow),
                    "chest-open");
            }
        }

        internal static void OnBindingStateChanged(MissionAcgBindingRecord record)
        {
            if (!initialized || record == null)
            {
                return;
            }

            if (record.State.LifecycleState == MissionAcgLifecycleState.Abandoned
                || record.State.LifecycleState == MissionAcgLifecycleState.Expired
                || record.State.LifecycleState == MissionAcgLifecycleState.CleanupPending
                || record.State.LifecycleState == MissionAcgLifecycleState.Cleaned
                || record.State.LifecycleState == MissionAcgLifecycleState.Invalid)
            {
                string failure;
                if (!Cleanup(record, out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-OPERATIONAL-CLEANUP-FAIL accepted={0}:{1} livePf2={2} reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.Binding.AllocatedLivePlayfield2,
                        failure);
                }
            }
        }

        internal static bool Cleanup(
            MissionAcgBindingRecord record,
            out string failure)
        {
            EnsureInitialized();
            lock (Sync)
            {
                failure = string.Empty;
                MissionAcgOperationalState state;
                if (!ByAccepted.TryGetValue(
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out state))
                {
                    return store.TryDelete(
                        record.Binding.AcceptedQuestIdentity,
                        out failure);
                }

                MissionAcgOperationalState pending = state.BeginCleanup(DateTime.UtcNow);
                if (!store.TryWrite(pending, true, out failure))
                {
                    return false;
                }

                IPlayfield playfield =
                    Pool.Instance.GetObject<IPlayfield>(
                        Identity.None,
                        new Identity
                        {
                            Type = IdentityType.Playfield2,
                            Instance = state.AllocatedLivePlayfield2
                        });
                Playfield concrete = playfield as Playfield;
                for (int i = 0; i < state.Npcs.Count; i++)
                {
                    MissionAcgNpcRuntimeState npc = state.Npcs[i];
                    CapturedEnemyCombatRuntimeRegistry.Remove(npc.RuntimeIdentity.Instance);
                    MissionInstanceMobCombat.UnregisterAggressive(ToIdentity(npc.RuntimeIdentity));
                    if (concrete != null)
                    {
                        ICharacter character =
                            Pool.Instance.GetObject<ICharacter>(
                                concrete.Identity,
                                ToIdentity(npc.RuntimeIdentity));
                        if (character != null)
                        {
                            concrete.DespawnNpcImmediately(character);
                        }
                    }
                }

                MissionAcgOperationalState completed =
                    pending.CompleteCleanup(DateTime.UtcNow);
                if (!store.TryWrite(completed, true, out failure)
                    || !store.TryDelete(record.Binding.AcceptedQuestIdentity, out failure))
                {
                    ByAccepted[record.Binding.AcceptedQuestIdentity.Instance] = pending;
                    ByPlayfield[record.Binding.AllocatedLivePlayfield2] = pending;
                    return false;
                }

                ByAccepted.Remove(record.Binding.AcceptedQuestIdentity.Instance);
                ByPlayfield.Remove(record.Binding.AllocatedLivePlayfield2);
                InvalidAccepted.Remove(record.Binding.AcceptedQuestIdentity.Instance);
                return true;
            }
        }

        internal static MissionAcgOperationalState CreateInitialState(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgMaterializedInstance instance,
            MissionAcgObjectiveRecord objective,
            DateTime nowUtc)
        {
            MissionAcgInstanceBinding binding = bindingRecord.Binding;
            MissionAcgLayoutBundle bundle = instance.Bundle;
            var byCaptured = new Dictionary<string, MissionAcgRuntimeIdentityEntry>(
                StringComparer.Ordinal);
            for (int i = 0; i < instance.State.IdentityEntries.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry entry = instance.State.IdentityEntries[i];
                byCaptured.Add(IdentityKey(entry.CapturedIdentity), entry);
            }

            var npcs = new List<MissionAcgNpcRuntimeState>(bundle.NpcSlots.Count);
            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                MissionAcgNpcSlotRecord captured = bundle.NpcSlots[i];
                MissionAcgRuntimeIdentityEntry runtime;
                if (!byCaptured.TryGetValue(IdentityKey(captured.CapturedIdentity), out runtime))
                {
                    throw new InvalidOperationException("Captured NPC has no deterministic identity.");
                }

                MissionAcgNpcRole role = MissionAcgNpcRole.Ambient;
                if (objective != null
                    && objective.Binding.RuntimeObjectiveIdentity.Equals(runtime.RuntimeIdentity))
                {
                    role =
                        objective.Binding.MissionType == MissionRollType.KillPerson
                            ? MissionAcgNpcRole.KillTarget
                            : MissionAcgNpcRole.FindPerson;
                }

                int maximumHealth = Math.Max(0, captured.CapturedHealth);
                int currentHealth =
                    Math.Max(
                        0,
                        maximumHealth - Math.Max(0, captured.CapturedHealthDamage));
                bool valid =
                    captured.TemplateId > 0
                    && captured.MonsterData > 0
                    && captured.CapturedLevel > 0
                    && maximumHealth > 0
                    && MissionAcgSpatialValidator.IsFinite(captured.Position)
                    && MissionAcgSpatialValidator.IsFinite(captured.Heading);
                npcs.Add(
                    new MissionAcgNpcRuntimeState(
                        captured.Slot,
                        captured.CapturedIdentity,
                        runtime.RuntimeIdentity,
                        captured.Position,
                        captured.Heading,
                        captured.TemplateId,
                        captured.MonsterData,
                        captured.CapturedLevel,
                        maximumHealth,
                        currentHealth,
                        captured.Scale,
                        captured.HeadMesh,
                        captured.Name,
                        role,
                        valid
                            ? (currentHealth > 0
                                ? MissionAcgNpcLifeState.Alive
                                : MissionAcgNpcLifeState.Dead)
                            : MissionAcgNpcLifeState.Unresolved,
                        role == MissionAcgNpcRole.FindPerson
                            ? MissionAcgNpcCombatState.NonCombat
                            : MissionAcgNpcCombatState.Stationary,
                        null,
                        MissionAcgCorpseState.None,
                        1,
                        false));
            }

            var chests = new List<MissionAcgChestRuntimeState>();
            for (int i = 0; i < instance.State.IdentityEntries.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry entry = instance.State.IdentityEntries[i];
                if (entry.Kind != MissionAcgRuntimeObjectKind.Chest)
                {
                    continue;
                }

                MissionAcgRuntimeChestState stage3;
                bool isOpen =
                    instance.State.TryGetChest(entry.RuntimeIdentity.Instance, out stage3)
                    && stage3.IsOpen;
                chests.Add(
                    new MissionAcgChestRuntimeState(
                        entry.Slot,
                        entry.CapturedIdentity,
                        entry.RuntimeIdentity,
                        MissionAcgLootAuthority.UnresolvedEmpty,
                        isOpen,
                        isOpen,
                        0,
                        false));
            }

            return new MissionAcgOperationalState(
                MissionAcgOperationalState.CurrentFormatVersion,
                binding.AcceptedQuestIdentity,
                binding.OwnerIdentity,
                binding.AllocatedLivePlayfield2,
                binding.SelectedBundleId,
                binding.SelectedBundlePayloadSha256,
                binding.AcgBuildingIdentity,
                npcs,
                chests,
                MissionAcgOperationalCleanupState.Active,
                nowUtc);
        }

        internal static bool ValidateRestoredState(
            MissionAcgOperationalState restored,
            MissionAcgOperationalState expected,
            out string failure)
        {
            failure = string.Empty;
            if (restored == null
                || expected == null
                || !restored.AcceptedQuestIdentity.Equals(expected.AcceptedQuestIdentity)
                || !restored.OwnerIdentity.Equals(expected.OwnerIdentity)
                || restored.AllocatedLivePlayfield2 != expected.AllocatedLivePlayfield2
                || !string.Equals(restored.BundleId, expected.BundleId, StringComparison.Ordinal)
                || !string.Equals(
                    restored.BundlePayloadSha256,
                    expected.BundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !restored.BuildingIdentity.Equals(expected.BuildingIdentity)
                || restored.Npcs.Count != expected.Npcs.Count
                || restored.Chests.Count != expected.Chests.Count)
            {
                failure = "Operational state identity or slot counts changed.";
                return false;
            }

            for (int i = 0; i < expected.Npcs.Count; i++)
            {
                MissionAcgNpcRuntimeState expectedNpc = expected.Npcs[i];
                MissionAcgNpcRuntimeState restoredNpc;
                if (!restored.TryGetNpc(expectedNpc.RuntimeIdentity.Instance, out restoredNpc)
                    || restoredNpc.CapturedSlot != expectedNpc.CapturedSlot
                    || !restoredNpc.CapturedIdentity.Equals(expectedNpc.CapturedIdentity)
                    || !restoredNpc.RuntimeIdentity.Equals(expectedNpc.RuntimeIdentity)
                    || restoredNpc.TemplateId != expectedNpc.TemplateId
                    || restoredNpc.MonsterData != expectedNpc.MonsterData
                    || restoredNpc.Level != expectedNpc.Level
                    || restoredNpc.MaximumHealth != expectedNpc.MaximumHealth
                    || restoredNpc.MonsterScale != expectedNpc.MonsterScale
                    || restoredNpc.HeadMesh != expectedNpc.HeadMesh
                    || !string.Equals(restoredNpc.Name, expectedNpc.Name, StringComparison.Ordinal)
                    || restoredNpc.Role != expectedNpc.Role
                    || !SamePoint(restoredNpc.Position, expectedNpc.Position)
                    || !SameRotation(restoredNpc.Heading, expectedNpc.Heading))
                {
                    failure = "Operational NPC immutable identity or captured attributes changed.";
                    return false;
                }
            }

            for (int i = 0; i < expected.Chests.Count; i++)
            {
                MissionAcgChestRuntimeState expectedChest = expected.Chests[i];
                MissionAcgChestRuntimeState restoredChest;
                if (!restored.TryGetChest(
                        expectedChest.RuntimeIdentity.Instance,
                        out restoredChest)
                    || restoredChest.CapturedSlot != expectedChest.CapturedSlot
                    || !restoredChest.CapturedIdentity.Equals(expectedChest.CapturedIdentity)
                    || !restoredChest.RuntimeIdentity.Equals(expectedChest.RuntimeIdentity)
                    || restoredChest.LootAuthority != expectedChest.LootAuthority)
                {
                    failure = "Operational chest identity or loot authority changed.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryEnsureStateLocked(
            MissionAcgBindingRecord record,
            out MissionAcgOperationalState state,
            out string failure)
        {
            state = null;
            failure = string.Empty;
            if (record == null || !ShouldRestore(record))
            {
                failure = "Binding lifecycle does not allow Stage 5 restoration.";
                return false;
            }

            if (InvalidAccepted.Contains(record.Binding.AcceptedQuestIdentity.Instance))
            {
                failure = "Stage 5 state is invalid and remains fail-closed.";
                return false;
            }

            if (ByAccepted.TryGetValue(record.Binding.AcceptedQuestIdentity.Instance, out state))
            {
                return true;
            }

            MissionAcgMaterializedInstance instance;
            if (!MissionAcgRuntimeManager.TryResolveByPlayfield(
                record.Binding.AllocatedLivePlayfield2,
                out instance)
                && !MissionAcgRuntimeManager.TryGetOrMaterialize(
                    record,
                    out instance,
                    out failure))
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                record.Binding.OwnerIdentity.Instance,
                record.Binding.AcceptedQuestIdentity.Instance,
                out objective))
            {
                failure = "Exact Stage 4 objective binding is unavailable.";
                return false;
            }

            if (!MissionAcgSpatialValidator.TryValidate(
                instance.Bundle,
                instance,
                objective,
                out failure))
            {
                return false;
            }

            MissionAcgOperationalState expected =
                CreateInitialState(record, instance, objective, DateTime.UtcNow);
            MissionAcgOperationalState restored;
            bool exists;
            if (!store.TryLoad(record.Binding, out restored, out exists, out failure))
            {
                return false;
            }

            if (exists && !ValidateRestoredState(restored, expected, out failure))
            {
                return false;
            }

            state = restored ?? expected;
            if (!exists && !store.TryWrite(state, false, out failure))
            {
                state = null;
                return false;
            }

            ByAccepted.Add(record.Binding.AcceptedQuestIdentity.Instance, state);
            ByPlayfield.Add(record.Binding.AllocatedLivePlayfield2, state);
            return true;
        }

        private static bool TryReplaceStateLocked(
            MissionAcgOperationalState previous,
            MissionAcgOperationalState replacement,
            string operation)
        {
            string failure;
            if (!store.TryWrite(replacement, true, out failure))
            {
                MissionDiagnostics.Log(
                    "ACG-OPERATIONAL-PERSIST-FAIL accepted={0}:{1} livePf2={2} operation={3} reason={4}",
                    previous.AcceptedQuestIdentity.Type,
                    previous.AcceptedQuestIdentity.Instance,
                    previous.AllocatedLivePlayfield2,
                    operation,
                    failure);
                InvalidAccepted.Add(previous.AcceptedQuestIdentity.Instance);
                return false;
            }

            ByAccepted[replacement.AcceptedQuestIdentity.Instance] = replacement;
            ByPlayfield[replacement.AllocatedLivePlayfield2] = replacement;
            return true;
        }

        private static void UpdateCorpseState(
            int allocatedLivePlayfield2,
            int runtimeNpcInstance,
            Identity corpseIdentity,
            MissionAcgCorpseState corpseState)
        {
            EnsureInitialized();
            lock (Sync)
            {
                MissionAcgOperationalState state;
                MissionAcgNpcRuntimeState npc;
                if (!ByPlayfield.TryGetValue(allocatedLivePlayfield2, out state)
                    || !state.TryGetNpc(runtimeNpcInstance, out npc)
                    || npc.CorpseIdentity == null
                    || npc.CorpseIdentity.Type != (int)corpseIdentity.Type
                    || npc.CorpseIdentity.Instance != corpseIdentity.Instance)
                {
                    return;
                }

                TryReplaceStateLocked(
                    state,
                    state.ReplaceNpc(npc.WithCorpseState(corpseState), DateTime.UtcNow),
                    "corpse-state");
            }
        }

        private static void ApplyCapturedNpc(
            Character mob,
            MissionAcgNpcSlotRecord captured,
            MissionAcgNpcRuntimeState state)
        {
            mob.Name = captured.Name;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.level,
                (uint)captured.CapturedLevel);
            mob.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.monsterdata,
                (uint)captured.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.monsterscale,
                (uint)Math.Max(0, captured.Scale));
            if (captured.HeadMesh.HasValue)
            {
                mob.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.headmesh,
                    (uint)captured.HeadMesh.Value);
            }

            mob.Textures.Clear();
            for (int i = 0; i < captured.Textures.Count; i++)
            {
                MissionAcgNpcTextureRecord texture = captured.Textures[i];
                if (texture.TextureId > 0)
                {
                    mob.Textures.Add(new AOTextures(texture.Slot, texture.TextureId));
                }
            }

            mob.MeshLayer.Clear();
            mob.SocialMeshLayer.Clear();
            for (int i = 0; i < captured.Meshes.Count; i++)
            {
                MissionAcgNpcMeshRecord mesh = captured.Meshes[i];
                if (mesh.MeshId > 0)
                {
                    mob.MeshLayer.AddMesh(
                        mesh.Position,
                        mesh.MeshId,
                        mesh.Unknown1,
                        mesh.Unknown2);
                    mob.SocialMeshLayer.AddMesh(
                        mesh.Position,
                        mesh.MeshId,
                        mesh.Unknown1,
                        mesh.Unknown2);
                }
            }

            mob.Coordinates(
                new Coordinate
                {
                    x = state.Position.X,
                    y = state.Position.Y,
                    z = state.Position.Z
                });
            mob.RawHeading =
                new Quaternion(
                    state.Heading.X,
                    state.Heading.Y,
                    state.Heading.Z,
                    state.Heading.W);
        }

        private static MissionAcgNpcSlotRecord FindNpcSlot(
            MissionAcgLayoutBundle bundle,
            int slot)
        {
            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                if (bundle.NpcSlots[i].Slot == slot)
                {
                    return bundle.NpcSlots[i];
                }
            }

            return null;
        }

        private static bool ShouldRestore(MissionAcgBindingRecord record)
        {
            return record != null
                   && record.State.ReservesPlayfield
                   && record.State.LifecycleState != MissionAcgLifecycleState.Abandoned
                   && record.State.LifecycleState != MissionAcgLifecycleState.Expired
                   && record.State.LifecycleState != MissionAcgLifecycleState.CleanupPending
                   && record.State.LifecycleState != MissionAcgLifecycleState.Cleaned
                   && record.State.LifecycleState != MissionAcgLifecycleState.Invalid;
        }

        private static bool SamePoint(
            MissionAcgPointRecord first,
            MissionAcgPointRecord second)
        {
            return first.X.Equals(second.X)
                   && first.Y.Equals(second.Y)
                   && first.Z.Equals(second.Z);
        }

        private static bool SameRotation(
            MissionAcgRotationRecord first,
            MissionAcgRotationRecord second)
        {
            return first.X.Equals(second.X)
                   && first.Y.Equals(second.Y)
                   && first.Z.Equals(second.Z)
                   && first.W.Equals(second.W);
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord identity)
        {
            return new Identity
                   {
                       Type = (IdentityType)identity.Type,
                       Instance = identity.Instance
                   };
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                MissionAcgBindingRuntime.Initialize();
            }
        }
    }
}
