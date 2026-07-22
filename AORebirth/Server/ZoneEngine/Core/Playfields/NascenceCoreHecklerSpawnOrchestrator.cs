namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// Spawns capture-backed Hecklers on Nascence Core (PF 4312) with 10-minute respawn.
    /// Capture 20260716-071407.
    /// </summary>
    internal sealed class NascenceCoreHecklerSpawnOrchestrator
    {
        private readonly Action<ICharacter> activateNpc;

        private readonly Dictionary<int, NascenceCoreHecklerSpawnDefinition> spawnBySource =
            new Dictionary<int, NascenceCoreHecklerSpawnDefinition>();

        private readonly Dictionary<int, int> runtimeToSource = new Dictionary<int, int>();

        private readonly Dictionary<int, DateTime> respawnDueBySource = new Dictionary<int, DateTime>();

        private readonly object sync = new object();

        private Playfield playfield;

        private Identity playfieldIdentity;

        internal NascenceCoreHecklerSpawnOrchestrator(Action<ICharacter> activateNpc)
        {
            this.activateNpc = activateNpc;
            foreach (NascenceCoreHecklerSpawnDefinition spawn in NascenceCoreHecklerContentProvider.GetSpawns())
            {
                this.spawnBySource[spawn.SourceIdentity] = spawn;
            }
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != NascenceCoreHecklerContentProvider.PlayfieldInstance)
            {
                return;
            }

            this.playfield = playfield;
            this.playfieldIdentity = playfieldIdentity;

            int spawned = 0;
            foreach (NascenceCoreHecklerSpawnDefinition spawn in NascenceCoreHecklerContentProvider.GetSpawns())
            {
                if (this.TrySpawn(spawn))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceCoreHeckler spawn complete pf="
                + playfieldIdentity.Instance
                + " spawned="
                + spawned
                + " capture="
                + NascenceCoreHecklerContentProvider.CaptureId);
        }

        internal void NotifyDeath(ICharacter target, DateTime diedAtUtc)
        {
            if (target == null)
            {
                return;
            }

            int sourceIdentity;
            DateTime dueUtc;
            lock (this.sync)
            {
                if (!this.runtimeToSource.TryGetValue(target.Identity.Instance, out sourceIdentity))
                {
                    return;
                }

                this.runtimeToSource.Remove(target.Identity.Instance);
                dueUtc = diedAtUtc.AddSeconds(NascenceCoreHecklerContentProvider.RespawnDelaySeconds);
                this.respawnDueBySource[sourceIdentity] = dueUtc;
            }

            CapturedEnemyCombatRuntimeRegistry.Remove(target.Identity.Instance);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NascenceCoreHeckler death scheduled respawn source=0x{0:X8} dueUtc={1:o}",
                    sourceIdentity,
                    dueUtc));
        }

        internal void NotifyNpcDespawn(ICharacter target)
        {
            if (target == null)
            {
                return;
            }

            lock (this.sync)
            {
                int sourceIdentity;
                if (this.runtimeToSource.TryGetValue(target.Identity.Instance, out sourceIdentity))
                {
                    this.runtimeToSource.Remove(target.Identity.Instance);
                    if (!this.respawnDueBySource.ContainsKey(sourceIdentity))
                    {
                        this.respawnDueBySource[sourceIdentity] =
                            DateTime.UtcNow.AddSeconds(NascenceCoreHecklerContentProvider.RespawnDelaySeconds);
                    }
                }
            }

            CapturedEnemyCombatRuntimeRegistry.Remove(target.Identity.Instance);
        }

        internal void ProcessDue(DateTime utcNow)
        {
            if (this.playfield == null
                || this.playfieldIdentity.Instance != NascenceCoreHecklerContentProvider.PlayfieldInstance)
            {
                return;
            }

            int[] dueSources;
            lock (this.sync)
            {
                var list = new List<int>();
                foreach (KeyValuePair<int, DateTime> entry in this.respawnDueBySource)
                {
                    if (entry.Value <= utcNow)
                    {
                        list.Add(entry.Key);
                    }
                }

                dueSources = list.ToArray();
            }

            foreach (int sourceIdentity in dueSources)
            {
                NascenceCoreHecklerSpawnDefinition spawn;
                if (!this.spawnBySource.TryGetValue(sourceIdentity, out spawn))
                {
                    continue;
                }

                if (this.TrySpawn(spawn))
                {
                    lock (this.sync)
                    {
                        this.respawnDueBySource.Remove(sourceIdentity);
                    }
                }
            }
        }

        private bool TrySpawn(NascenceCoreHecklerSpawnDefinition spawn)
        {
            if (this.playfield == null || spawn == null)
            {
                return false;
            }

            lock (this.sync)
            {
                foreach (KeyValuePair<int, int> pair in this.runtimeToSource)
                {
                    if (pair.Value == spawn.SourceIdentity)
                    {
                        return false;
                    }
                }
            }

            var controller = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                NascenceCoreHecklerContentProvider.TemplateHash,
                this.playfieldIdentity,
                new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z },
                new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1),
                controller,
                spawn.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NascenceCoreHeckler spawn FAILED source=0x{0:X8} name={1}",
                        spawn.SourceIdentity,
                        spawn.Name));
                return false;
            }

            mob.Name = spawn.Name;
            mob.Playfield = this.playfield;
            SetStat(mob, StatIds.level, spawn.Level);
            SetStat(mob, StatIds.life, spawn.Health);
            SetStat(mob, StatIds.health, spawn.Health);
            SetStat(mob, StatIds.runspeed, spawn.RunSpeed);
            SetStat(mob, StatIds.monsterdata, NascenceCoreHecklerContentProvider.MonsterData);
            SetStat(mob, StatIds.monsterscale, NascenceCoreHecklerContentProvider.MonsterScale);
            SetStat(mob, StatIds.npcfamily, NascenceCoreHecklerContentProvider.NpcFamily);
            SetStat(mob, StatIds.visualflags, NascenceCoreHecklerContentProvider.VisualFlags);
            SetStat(mob, StatIds.mindamage, NascenceCoreHecklerContentProvider.MinDamage);
            SetStat(mob, StatIds.maxdamage, NascenceCoreHecklerContentProvider.MaxDamage);
            mob.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });

            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttack(
                NascenceCoreHecklerContentProvider.CaptureId + ": Heckler of Earth fight 796C7244",
                NascenceCoreHecklerContentProvider.MinDamage,
                NascenceCoreHecklerContentProvider.MaxDamage,
                NascenceCoreHecklerContentProvider.RechargeSeconds,
                3,
                0,
                NascenceCoreHecklerContentProvider.PrimaryWeaponInstance,
                0,
                0,
                0);

            string combatFailure;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out combatFailure);

            mob.DoNotDoTimers = false;
            this.activateNpc(mob);
            this.playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);

            lock (this.sync)
            {
                this.runtimeToSource[mob.Identity.Instance] = spawn.SourceIdentity;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NascenceCoreHeckler spawned source=0x{0:X8} server={1} name={2} pos=({3},{4},{5})",
                    spawn.SourceIdentity,
                    mob.Identity,
                    spawn.Name,
                    spawn.X,
                    spawn.Y,
                    spawn.Z));
            return true;
        }

        private static void SetStat(Character character, StatIds stat, int value)
        {
            try
            {
                character.Stats[(int)stat].BaseValue = (uint)value;
                character.Stats[(int)stat].Value = value;
            }
            catch
            {
            }
        }
    }
}
