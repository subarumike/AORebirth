namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    internal sealed class PlayfieldObjectLifecycleRuntimeService
    {
        internal void RemoveInstancedEntity(IInstancedEntity entity)
        {
            Pool.Instance.RemoveObject(entity);
        }

        internal int DespawnCorpses<TCorpseState>(
            IDictionary<int, TCorpseState> pendingCorpseSpawns,
            IDictionary<int, TCorpseState> corpses,
            Func<string, Identity, bool> shouldDespawn,
            Func<TCorpseState, string> corpseName,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Action<int> despawnCorpse)
        {
            if (shouldDespawn == null)
            {
                return 0;
            }

            int removed = 0;
            List<KeyValuePair<int, TCorpseState>> pendingSnapshot;
            lock (pendingCorpseSpawns)
            {
                pendingSnapshot = pendingCorpseSpawns.ToList();
            }

            var missingStateCandidates = new List<KeyValuePair<int, TCorpseState>>();
            var despawnCandidates = new List<KeyValuePair<int, TCorpseState>>();
            foreach (KeyValuePair<int, TCorpseState> pendingCorpseSpawn in pendingSnapshot)
            {
                TCorpseState corpse = pendingCorpseSpawn.Value;
                if (ReferenceEquals(corpse, null))
                {
                    missingStateCandidates.Add(pendingCorpseSpawn);
                    continue;
                }

                if (shouldDespawn(corpseName(corpse), deadNpcIdentity(corpse)))
                {
                    despawnCandidates.Add(pendingCorpseSpawn);
                }
            }

            lock (pendingCorpseSpawns)
            {
                foreach (KeyValuePair<int, TCorpseState> candidate in missingStateCandidates)
                {
                    TCorpseState current;
                    if (pendingCorpseSpawns.TryGetValue(candidate.Key, out current)
                        && IsSamePendingCorpseState(current, candidate.Value))
                    {
                        pendingCorpseSpawns.Remove(candidate.Key);
                    }
                }

                foreach (KeyValuePair<int, TCorpseState> candidate in despawnCandidates)
                {
                    TCorpseState current;
                    if (pendingCorpseSpawns.TryGetValue(candidate.Key, out current)
                        && IsSamePendingCorpseState(current, candidate.Value))
                    {
                        pendingCorpseSpawns.Remove(candidate.Key);
                        removed++;
                    }
                }
            }

            foreach (int corpseInstance in corpses
                .Where(x => shouldDespawn(corpseName(x.Value), deadNpcIdentity(x.Value)))
                .Select(x => x.Key)
                .ToList())
            {
                despawnCorpse(corpseInstance);
                removed++;
            }

            return removed;
        }

        internal void DespawnCorpse(
            int corpseInstance,
            Action<Identity> sendDespawn,
            Action<int> clearNpcCorpseDespawn,
            Action<int> removeCorpseState,
            Action<int> removePendingCorpseCreditAward)
        {
            Identity corpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = corpseInstance };
            sendDespawn(corpseIdentity);
            clearNpcCorpseDespawn(corpseInstance);
            removeCorpseState(corpseInstance);
            removePendingCorpseCreditAward(corpseInstance);

            LogUtil.Debug(DebugInfoDetail.Engine, string.Format("Corpse despawned corpse={0}", corpseIdentity));
        }

        internal void ProcessPendingCorpseSpawns<TCorpseState>(
            IDictionary<int, TCorpseState> pendingCorpseSpawns,
            Func<TCorpseState, DateTime> spawnsAtUtc,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Func<Identity, ICharacter> findDeadNpc,
            Func<ICharacter, Identity, bool> registerCorpse,
            Action<Identity, Identity> corpseSpawnFailed,
            Action<Identity, Identity> traceCorpseFullUpdate,
            Action<ICharacter, Identity> sendCorpseFullUpdate)
        {
            var dueCorpseSpawns = new List<TCorpseState>();
            var missingStateKeys = new List<int>();
            List<KeyValuePair<int, TCorpseState>> pendingSnapshot;
            DateTime utcNow = DateTime.UtcNow;
            lock (pendingCorpseSpawns)
            {
                pendingSnapshot = pendingCorpseSpawns.ToList();
            }

            var missingStateCandidates = new List<KeyValuePair<int, TCorpseState>>();
            var dueCandidates = new List<KeyValuePair<int, TCorpseState>>();
            foreach (KeyValuePair<int, TCorpseState> pendingCorpseSpawn in pendingSnapshot)
            {
                TCorpseState corpse = pendingCorpseSpawn.Value;
                if (ReferenceEquals(corpse, null))
                {
                    missingStateCandidates.Add(pendingCorpseSpawn);
                    continue;
                }

                if (spawnsAtUtc(corpse) <= utcNow)
                {
                    dueCandidates.Add(pendingCorpseSpawn);
                }
            }

            lock (pendingCorpseSpawns)
            {
                foreach (KeyValuePair<int, TCorpseState> candidate in missingStateCandidates)
                {
                    TCorpseState current;
                    if (pendingCorpseSpawns.TryGetValue(candidate.Key, out current)
                        && IsSamePendingCorpseState(current, candidate.Value))
                    {
                        pendingCorpseSpawns.Remove(candidate.Key);
                        missingStateKeys.Add(candidate.Key);
                    }
                }

                foreach (KeyValuePair<int, TCorpseState> candidate in dueCandidates)
                {
                    TCorpseState current;
                    if (pendingCorpseSpawns.TryGetValue(candidate.Key, out current)
                        && IsSamePendingCorpseState(current, candidate.Value))
                    {
                        pendingCorpseSpawns.Remove(candidate.Key);
                        dueCorpseSpawns.Add(candidate.Value);
                    }
                }
            }

            foreach (int missingStateKey in missingStateKeys)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "Skipping corpse spawn; pending corpse state is missing deadNpcInstance={0}",
                        missingStateKey));
            }

            foreach (TCorpseState corpse in dueCorpseSpawns)
            {
                Identity corpseId = corpseIdentity(corpse);
                Identity deadNpcId = deadNpcIdentity(corpse);

                ICharacter target = findDeadNpc(deadNpcId);
                if (target == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            "Skipping corpse spawn corpse={0}; dead NPC no longer exists deadNpc={1}",
                            corpseId,
                            deadNpcId));
                    if (corpseSpawnFailed != null)
                    {
                        corpseSpawnFailed(deadNpcId, corpseId);
                    }
                    continue;
                }

                if (!registerCorpse(target, corpseId))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            "Skipping corpse visibility corpse={0}; registration failed deadNpc={1}",
                            corpseId,
                            deadNpcId));
                    if (corpseSpawnFailed != null)
                    {
                        corpseSpawnFailed(deadNpcId, corpseId);
                    }
                    continue;
                }

                traceCorpseFullUpdate(corpseId, deadNpcId);
                sendCorpseFullUpdate(target, corpseId);
            }
        }

        private static bool IsSamePendingCorpseState<TCorpseState>(
            TCorpseState current,
            TCorpseState snapshot)
        {
            return typeof(TCorpseState).IsValueType
                ? EqualityComparer<TCorpseState>.Default.Equals(current, snapshot)
                : ReferenceEquals(current, snapshot);
        }
    }
}
