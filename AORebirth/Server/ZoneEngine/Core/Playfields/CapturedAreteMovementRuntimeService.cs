namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class CapturedAreteMovementRuntimeService
    {
        private const double FleeHealthFraction = 0.20;

        private readonly CapturedAreteMovementCatalog catalog;

        private readonly CapturedAreteMovementRuntimeCoordinator coordinator;

        private readonly Dictionary<string, int> spawnGenerations =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<int, int> runtimeSpawnGenerations =
            new Dictionary<int, int>();

        private readonly HashSet<int> completedSpawnMovement = new HashSet<int>();

        internal CapturedAreteMovementRuntimeService()
            : this(CapturedAreteMovementCatalog.LoadDefault())
        {
        }

        internal CapturedAreteMovementRuntimeService(CapturedAreteMovementCatalog catalog)
        {
            this.catalog = catalog;
            this.coordinator = new CapturedAreteMovementRuntimeCoordinator(catalog);
        }

        internal bool IsAvailable
        {
            get { return this.catalog != null && this.catalog.IsValid; }
        }

        internal string FailureReason
        {
            get { return this.catalog == null ? "catalog-null" : this.catalog.FailureReason; }
        }

        internal bool Activate(ICharacter character)
        {
            if (character == null || !this.IsAvailable)
            {
                return false;
            }

            CapturedAreteMovementPoint position = ToPoint(character.Coordinates().coordinate);
            string generationKey = BuildSpawnGenerationKey(character, position);
            int generation;
            if (!this.spawnGenerations.TryGetValue(generationKey, out generation))
            {
                generation = 0;
            }

            generation++;
            this.spawnGenerations[generationKey] = generation;
            this.runtimeSpawnGenerations[character.Identity.Instance] = generation;
            this.completedSpawnMovement.Remove(character.Identity.Instance);
            return this.coordinator.Activate(this.BuildEvidence(character, generation, false, null, null));
        }

        internal bool TryProcessSpawn(ICharacter character, DateTime utcNow)
        {
            if (character == null || this.completedSpawnMovement.Contains(character.Identity.Instance))
            {
                return false;
            }

            CapturedAreteMovementDecisionKind decision = this.Process(
                character,
                CapturedAreteMovementBehavior.Spawn,
                false,
                null,
                null,
                utcNow);
            if (decision == CapturedAreteMovementDecisionKind.Fallback)
            {
                this.completedSpawnMovement.Add(character.Identity.Instance);
                return false;
            }

            return true;
        }

        internal bool TryProcessPatrol(ICharacter character, DateTime utcNow)
        {
            NPCController controller = character == null ? null : character.Controller as NPCController;
            if (controller == null
                || controller.State != CharacterState.Patrolling
                || character.FightingTarget.Instance != 0)
            {
                return false;
            }

            return this.Process(
                       character,
                       CapturedAreteMovementBehavior.Patrol,
                       false,
                       null,
                       null,
                       utcNow)
                   != CapturedAreteMovementDecisionKind.Fallback;
        }

        internal bool TryProcessCombat(
            ICharacter character,
            ICharacter target,
            DateTime utcNow)
        {
            if (character == null
                || target == null
                || character.FightingTarget.Instance == 0)
            {
                return false;
            }

            int maximumHealth = character.Stats[StatIds.life].Value;
            int currentHealth = character.Stats[StatIds.health].Value;
            bool shouldFlee =
                maximumHealth > 0
                && currentHealth > 0
                && currentHealth <= (int)Math.Ceiling(maximumHealth * FleeHealthFraction);
            CapturedAreteMovementBehavior behavior = shouldFlee
                                                        ? CapturedAreteMovementBehavior.Flee
                                                        : CapturedAreteMovementBehavior.Chase;
            return this.Process(
                       character,
                       behavior,
                       false,
                       ToPoint(target.Coordinates().coordinate),
                       null,
                       utcNow)
                   != CapturedAreteMovementDecisionKind.Fallback;
        }

        internal bool TryProcessLeash(
            ICharacter character,
            Vector3 home,
            DateTime utcNow)
        {
            return this.Process(
                       character,
                       CapturedAreteMovementBehavior.Leash,
                       true,
                       null,
                       ToPoint(home),
                       utcNow)
                   != CapturedAreteMovementDecisionKind.Fallback;
        }

        internal void Interrupt(ICharacter character)
        {
            if (character != null)
            {
                this.coordinator.Interrupt(character.Identity.Instance);
            }
        }

        internal void Remove(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            int runtimeIdentity = character.Identity.Instance;
            this.coordinator.Remove(runtimeIdentity);
            this.runtimeSpawnGenerations.Remove(runtimeIdentity);
            this.completedSpawnMovement.Remove(runtimeIdentity);
        }

        internal void Clear()
        {
            this.coordinator.Clear();
            this.runtimeSpawnGenerations.Clear();
            this.completedSpawnMovement.Clear();
        }

        private CapturedAreteMovementDecisionKind Process(
            ICharacter character,
            CapturedAreteMovementBehavior behavior,
            bool returningHome,
            CapturedAreteMovementPoint target,
            CapturedAreteMovementPoint home,
            DateTime utcNow)
        {
            int generation;
            if (character == null
                || !this.runtimeSpawnGenerations.TryGetValue(
                    character.Identity.Instance,
                    out generation))
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            NPCController activeController = character.Controller as NPCController;
            if (activeController != null)
            {
                activeController.SnapshotCurrentMotionPosition();
            }

            CapturedAreteMovementActorEvidence evidence =
                this.BuildEvidence(character, generation, returningHome, target, home);
            CapturedAreteMovementObservation observation;
            CapturedAreteMovementDecisionKind decision =
                this.coordinator.Select(evidence, behavior, utcNow, out observation);
            if (decision != CapturedAreteMovementDecisionKind.Movement)
            {
                return decision;
            }

            NPCController controller = character.Controller as NPCController;
            if (controller == null)
            {
                this.coordinator.Interrupt(character.Identity.Instance);
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            controller.SendCapturedAreteMovementSegment(
                behavior,
                ToVector(observation.Start),
                ToVector(observation.End),
                utcNow,
                observation.DelayAfterSeconds);
            return CapturedAreteMovementDecisionKind.Movement;
        }

        private CapturedAreteMovementActorEvidence BuildEvidence(
            ICharacter character,
            int spawnGeneration,
            bool returningHome,
            CapturedAreteMovementPoint target,
            CapturedAreteMovementPoint home)
        {
            return new CapturedAreteMovementActorEvidence
                   {
                       RuntimeIdentity = character.Identity.Instance,
                       SpawnGeneration = spawnGeneration,
                       NpcFamily = character.Stats[StatIds.npcfamily].Value,
                       MonsterData = character.Stats[StatIds.monsterdata].Value,
                       Level = character.Stats[StatIds.level].Value,
                       PlayfieldId = character.Playfield.Identity.Instance,
                       Name = character.Name,
                       Position = ToPoint(character.Coordinates().coordinate),
                       Fighting = character.FightingTarget.Instance != 0,
                       ReturningHome = returningHome,
                       TargetPosition = target,
                       HomePosition = home
                   };
        }

        private static string BuildSpawnGenerationKey(
            ICharacter character,
            CapturedAreteMovementPoint point)
        {
            return string.Join(
                "|",
                new[]
                {
                    character.Playfield.Identity.Instance.ToString(CultureInfo.InvariantCulture),
                    character.Stats[StatIds.npcfamily].Value.ToString(CultureInfo.InvariantCulture),
                    character.Stats[StatIds.monsterdata].Value.ToString(CultureInfo.InvariantCulture),
                    character.Stats[StatIds.level].Value.ToString(CultureInfo.InvariantCulture),
                    character.Name ?? string.Empty,
                    Math.Round(point.X, 1).ToString("F1", CultureInfo.InvariantCulture),
                    Math.Round(point.Y, 1).ToString("F1", CultureInfo.InvariantCulture),
                    Math.Round(point.Z, 1).ToString("F1", CultureInfo.InvariantCulture)
                });
        }

        private static CapturedAreteMovementPoint ToPoint(Vector3 point)
        {
            return new CapturedAreteMovementPoint(point.x, point.y, point.z);
        }

        private static Vector3 ToVector(CapturedAreteMovementPoint point)
        {
            return new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        }
    }
}
