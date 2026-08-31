namespace ZoneEngine.Core.Playfields

{

    #region Usings ...



    using System;

    using System.Collections.Concurrent;



    using AORebirth.Core.Entities;

    using AORebirth.Core.Playfields;

    using AORebirth.Enums;

    using AORebirth.ObjectManager;

    using ZoneEngine.Core;

    using ZoneEngine.Core.Controllers;



    #endregion



    internal sealed class PlayfieldCharacterHeartbeatRuntimeService

    {

        private readonly ConcurrentDictionary<int, byte> npcRegenSuspendedForCombat =

            new ConcurrentDictionary<int, byte>();



        internal void ProcessRegeneration(ICharacter dynel, double deltaTime, Action<ICharacter> sendChangedStats)

        {

            Require(sendChangedStats, "sendChangedStats");

            if (deltaTime <= 0.0)

            {

                return;

            }



            if (PetCombatRules.IsPlayerOwnedPet(dynel))

            {

                PetRuntimeService.Default.ProcessPetPassiveRegen(dynel);

                return;

            }



            if (dynel.Controller is NPCController)

            {

                this.ProcessNpcPassiveRegen(dynel, deltaTime, sendChangedStats);

                return;

            }



            bool changed = false;

            if (MongoSlamRuntimeService.ProcessHotTick(dynel))
            {
                changed = true;
            }

            AmbientRestorationAuraRuntime.ProcessTick(dynel);

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



        private void ProcessNpcPassiveRegen(ICharacter npc, double deltaTime, Action<ICharacter> sendChangedStats)

        {

            int npcInstance = npc.Identity.Instance;

            if (npcInstance == 0)

            {

                return;

            }



            Character character = npc as Character;

            int currentHealth = npc.Stats[StatIds.health].Value;

            if (!PlayfieldCharacterHeartbeatHealthRules.IsLivingHealth(
                currentHealth))
            {

                if (character != null)
                {
                    character.NpcHealthRegenElapsed = 0.0;
                }

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



            if (character == null || regenIntervalSeconds <= 0.0)

            {

                return;

            }



            character.NpcHealthRegenElapsed += deltaTime;

            if (character.NpcHealthRegenElapsed < regenIntervalSeconds)

            {

                return;

            }



            npc.Stats[StatIds.health].Value = Math.Min(maxHealth, currentHealth + healDelta);

            character.NpcHealthRegenElapsed = 0.0;

            sendChangedStats(npc);

        }



        private bool IsNpcRegenBlocked(ICharacter npc)

        {
            // Capture 20260722-134750: Wounded Dockworkers stay at 12/32 HP while Sit.
            if (npc != null
                && string.Equals(npc.Name, "Wounded Dockworker", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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

