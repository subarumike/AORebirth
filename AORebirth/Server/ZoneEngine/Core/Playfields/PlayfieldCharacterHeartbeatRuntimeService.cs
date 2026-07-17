namespace ZoneEngine.Core.Playfields

{

    #region Usings ...



    using System;

    using System.Collections.Concurrent;



    using AORebirth.Core.Entities;

    using AORebirth.Core.Playfields;

    using AORebirth.Enums;

    using AORebirth.ObjectManager;

    using AORebirth.Stats.SpecialStats;



    using ZoneEngine.Core;

    using ZoneEngine.Core.Controllers;



    #endregion



    internal sealed class PlayfieldCharacterHeartbeatRuntimeService

    {

        private readonly ConcurrentDictionary<int, DateTime> nextNpcHealthRegenUtc =

            new ConcurrentDictionary<int, DateTime>();



        private readonly ConcurrentDictionary<int, byte> npcRegenSuspendedForCombat =

            new ConcurrentDictionary<int, byte>();



        internal void ProcessRegeneration(ICharacter dynel, Action<ICharacter> sendChangedStats)

        {

            Require(sendChangedStats, "sendChangedStats");



            if (PetCombatRules.IsPlayerOwnedPet(dynel))

            {

                PetRuntimeService.Default.ProcessPetPassiveRegen(dynel);

                return;

            }



            if (dynel.Controller is NPCController)

            {

                this.ProcessNpcPassiveRegen(dynel, sendChangedStats);

                return;

            }



            bool changed = false;

            StatHealInterval healInterval = (StatHealInterval)dynel.Stats[StatIds.healinterval];

            int healIntervalSeconds = healInterval.Value;

            int healDelta = dynel.Stats[StatIds.healdelta].Value;

            if (healIntervalSeconds > 0

                && healDelta != 0

                && healInterval.LastTick < DateTime.UtcNow)

            {

                dynel.Stats[StatIds.health].Value =

                    Math.Min(dynel.Stats[StatIds.life].Value, dynel.Stats[StatIds.health].Value + healDelta);

                healInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(healIntervalSeconds);

                changed = true;

            }



            StatNanoInterval nanoInterval = (StatNanoInterval)dynel.Stats[StatIds.nanointerval];

            int nanoIntervalSeconds = nanoInterval.Value;

            int nanoDelta = dynel.Stats[StatIds.nanodelta].Value;

            if (nanoIntervalSeconds > 0

                && nanoDelta != 0

                && nanoInterval.LastTick < DateTime.UtcNow)

            {

                dynel.Stats[StatIds.currentnano].Value += nanoDelta;

                nanoInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(nanoIntervalSeconds);

                changed = true;

            }



            if (changed)

            {

                sendChangedStats(dynel);

            }

        }



        internal void SuspendNpcRegen(ICharacter npc)

        {

            if (npc == null || npc.Identity.Instance == 0)

            {

                return;

            }



            this.npcRegenSuspendedForCombat[npc.Identity.Instance] = 1;

        }



        internal void NotifyNpcDamaged(ICharacter npc)

        {

            this.SuspendNpcRegen(npc);

        }



        private void ProcessNpcPassiveRegen(ICharacter npc, Action<ICharacter> sendChangedStats)

        {

            int npcInstance = npc.Identity.Instance;

            if (npcInstance == 0)

            {

                return;

            }



            int currentHealth = npc.Stats[StatIds.health].Value;

            if (!PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(
                currentHealth))
            {

                this.nextNpcHealthRegenUtc.TryRemove(npcInstance, out _);

                this.npcRegenSuspendedForCombat.TryRemove(npcInstance, out _);

                return;

            }



            double regenIntervalSeconds = PetCombatRules.NpcHealthRegenIntervalSeconds;
            int maxHealth = npc.Stats[StatIds.life].Value;
            int healDelta = PetCombatRules.ResolveNpcHealthRegenDelta(maxHealth);
            bool regenerateHealthWhileInCombat = false;
            OrdinaryEnemyRuntimeDefinition ordinaryDefinition;
            if (OrdinaryEnemyRuntimeRegistry.TryGet(npcInstance, out ordinaryDefinition)
                && ordinaryDefinition.Profile.Combat.HealthRegenIntervalSeconds.HasValue
                && ordinaryDefinition.Profile.Combat.HealthRegenDelta.HasValue)
            {
                regenIntervalSeconds = ordinaryDefinition.Profile.Combat.HealthRegenIntervalSeconds.Value;
                healDelta = ordinaryDefinition.Profile.Combat.HealthRegenDelta.Value;
                regenerateHealthWhileInCombat = ordinaryDefinition.Profile.Combat.RegenerateHealthWhileInCombat;
            }

            if (!regenerateHealthWhileInCombat && this.IsNpcRegenBlocked(npc))

            {

                return;

            }

            if (regenerateHealthWhileInCombat)
            {
                byte ignored;
                this.npcRegenSuspendedForCombat.TryRemove(npcInstance, out ignored);
            }



            if (!PlayfieldCharacterHeartbeatHealthRules.CanRegenerateNpcHealth(
                currentHealth,
                maxHealth))
            {

                return;

            }



            DateTime now = DateTime.UtcNow;

            DateTime nextHealth = this.nextNpcHealthRegenUtc.GetOrAdd(npcInstance, now);

            if (now < nextHealth)

            {

                return;

            }



            npc.Stats[StatIds.health].Value = Math.Min(maxHealth, currentHealth + healDelta);

            this.nextNpcHealthRegenUtc[npcInstance] =

                now.AddSeconds(regenIntervalSeconds);

            sendChangedStats(npc);

        }



        private bool IsNpcRegenBlocked(ICharacter npc)

        {

            if (this.IsNpcUnderAttack(npc))

            {

                this.SuspendNpcRegen(npc);

                return true;

            }



            byte ignored;

            this.npcRegenSuspendedForCombat.TryRemove(npc.Identity.Instance, out ignored);

            return false;

        }



        private bool IsNpcUnderAttack(ICharacter npc)

        {

            if (npc == null || npc.Playfield == null)

            {

                return false;

            }



            if (npc.FightingTarget.Instance != 0 && npc.Stats[StatIds.health].Value > 0)

            {

                return true;

            }



            int targetInstance = npc.Identity.Instance;

            foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(npc.Playfield.Identity))

            {

                if (character == null || character.Identity.Instance == targetInstance)

                {

                    continue;

                }



                bool targetsNpc = character.FightingTarget.Instance == targetInstance
                    || character.SelectedTarget.Instance == targetInstance;
                // Missing or duplicate health is upstream stat corruption and must remain observable.
                if (PlayfieldCharacterHeartbeatHealthRules.IsLivingNpcAttackCandidate(
                    character,
                    targetsNpc,
                    candidate => candidate.Stats[StatIds.health].Value))
                {
                    return true;
                }
            }



            return false;

        }



        internal void ProcessFollow(ICharacter dynel)

        {

            if (dynel.Controller.IsFollowing())

            {

                dynel.Controller.DoFollow();

            }

        }



        internal void ProcessPlayerCollisionChecks(

            ICharacter dynel,

            Action<ICharacter> checkWallCollision,

            Action<ICharacter> checkStatelCollision)

        {

            Require(checkWallCollision, "checkWallCollision");

            Require(checkStatelCollision, "checkStatelCollision");



            checkWallCollision(dynel);

            checkStatelCollision(dynel);

        }



        private static void Require(Delegate callback, string name)

        {

            if (callback == null)

            {

                throw new ArgumentNullException(name);

            }

        }

    }

}

