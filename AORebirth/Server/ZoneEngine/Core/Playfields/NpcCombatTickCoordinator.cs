namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NpcCombatTickCoordinator
    {
        private const int MissingItemStatValue = 1234567890;

        private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, int> lastNpcCombatWeaponSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, int> lastNpcUnarmedAttackInfoSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, int> lastNpcSpecialAttackWeaponTargets = new Dictionary<int, int>();

        private readonly HashSet<int> completedCapturedOpeningAttacks = new HashSet<int>();

        private readonly Dictionary<int, DateTime> pendingCapturedAttackStarts =
            new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime> pendingCapturedMovementTransitions =
            new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime[]> nextCapturedParallelAttackTicks =
            new Dictionary<int, DateTime[]>();

        private readonly HashSet<int> startedCapturedParallelAttackClocks = new HashSet<int>();

        private readonly Dictionary<int, DateTime[]> nextBasicCaptureBackedAttackTicks =
            new Dictionary<int, DateTime[]>();

        private readonly HashSet<int> startedBasicCaptureBackedAttackClocks = new HashSet<int>();

        private readonly Dictionary<int, DateTime> nextLineOfSightRetryTicks =
            new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime> nextLineOfSightDiagnosticTicks =
            new Dictionary<int, DateTime>();

        private readonly CapturedIntObservationCursor capturedDamageObservationCursor =
            new CapturedIntObservationCursor();

        private readonly CapturedIntObservationCursor capturedSpecialAttackWeaponStateCursor =
            new CapturedIntObservationCursor();

        private readonly Dictionary<int, int> nextCapturedAttackStartDelayObservationIndexes =
            new Dictionary<int, int>();

        private readonly Dictionary<int, int> nextCapturedFirstHitDelayObservationIndexes =
            new Dictionary<int, int>();

        private readonly Dictionary<int, int> nextCapturedLandedIntervalObservationIndexes =
            new Dictionary<int, int>();

        private readonly Dictionary<int, int[]> nextBasicInitialDelayObservationIndexes =
            new Dictionary<int, int[]>();

        private readonly Dictionary<int, int[]> nextBasicDamageObservationIndexes =
            new Dictionary<int, int[]>();

        private readonly Dictionary<int, int[]> nextBasicLandedIntervalObservationIndexes =
            new Dictionary<int, int[]>();

        private readonly Playfield playfield;

        private readonly NpcDamageLineOfSightRuntimeService damageLineOfSight;

        internal NpcCombatTickCoordinator(Playfield playfield)
        {
            this.playfield = playfield;
            this.damageLineOfSight =
                new NpcDamageLineOfSightRuntimeService(playfield.Identity.Instance);
        }

        internal void ResetCombatTick(ICharacter attacker)
        {
            this.nextLineOfSightRetryTicks.Remove(attacker.Identity.Instance);
            this.nextLineOfSightDiagnosticTicks.Remove(attacker.Identity.Instance);
            CapturedEnemyCombatContract capturedContract;
            bool hasRegisteredCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                attacker.Identity.Instance,
                out capturedContract);
            if (hasRegisteredCapturedContract && !capturedContract.IsCombatReady)
            {
                this.ClearTracking(attacker.Identity);
                return;
            }

            if (hasRegisteredCapturedContract
                && capturedContract.IsCombatReady
                && !this.ValidateRequiredCapturedWeapon(attacker, capturedContract))
            {
                return;
            }

            bool hasCapturedContract = hasRegisteredCapturedContract && capturedContract.IsCombatReady;
            this.SendNpcWeaponDefinitionsToPlayerTarget(attacker);
            if (hasCapturedContract
                && capturedContract.AttackModel
                   == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary)
            {
                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                this.lastNpcSpecialAttackWeaponTargets.Remove(attacker.Identity.Instance);
                this.completedCapturedOpeningAttacks.Remove(attacker.Identity.Instance);
                this.StartBasicCaptureBackedAttackClocks(
                    attacker.Identity.Instance,
                    capturedContract.BasicCombat,
                    DateTime.UtcNow);
                return;
            }

            CombatAttackSource capturedAttackSource =
                hasCapturedContract ? this.GetCombatAttackSource(attacker) : null;
            if (hasCapturedContract && capturedAttackSource == null)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence =
                hasCapturedContract ? capturedContract.SpecialAttackSequence : null;
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence =
                hasCapturedContract ? capturedContract.ParallelAttackSequence : null;
            bool hasCapturedAttackStart = hasCapturedContract
                                          && capturedContract.HasCapturedAttackStartContext;
            double initialDelaySeconds = specialAttackSequence != null
                                             ? specialAttackSequence.InitialAttackDelaySeconds
                                             : Playfield.IsCapturedCleaningRobot(attacker)
                                                   ? NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds
                                                   : NpcCombatAttackRules.DefaultCombatTickSeconds;
            DateTime now = DateTime.UtcNow;
            if (parallelAttackSequence != null)
            {
                // The captured XOPZ and DENW clocks are independent of target
                // selection. Preserve them when Abmouth retargets mid-fight;
                // ClearTracking owns the true combat-end reset.
                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                this.lastNpcSpecialAttackWeaponTargets.Remove(attacker.Identity.Instance);
                this.completedCapturedOpeningAttacks.Remove(attacker.Identity.Instance);
                this.AnnounceCapturedParallelAttackSequenceContext(attacker, parallelAttackSequence);
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets.Remove(attacker.Identity.Instance);
            this.completedCapturedOpeningAttacks.Remove(attacker.Identity.Instance);
            double attackStartDelaySeconds = capturedContract == null
                                                 ? 0.0d
                                                 : capturedContract.AttackStartDelaySeconds;
            double firstHitDelaySeconds = capturedContract == null
                                              ? 0.0d
                                              : capturedContract.FirstHitDelaySeconds;
            if (hasCapturedAttackStart)
            {
                if (capturedContract.UsesEquippedWeaponTiming)
                {
                    firstHitDelaySeconds = capturedAttackSource.RechargeSeconds;
                }

                if (capturedContract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                {
                    attackStartDelaySeconds = SelectCapturedDoubleObservation(
                        this.nextCapturedAttackStartDelayObservationIndexes,
                        attacker.Identity.Instance,
                        capturedContract.CapturedAttackStartDelayObservationsSeconds);
                }

                firstHitDelaySeconds = capturedContract.AttackModel
                                       == CapturedEnemyAttackModel.FixedAttackInfo
                                           ? SelectCapturedDoubleObservation(
                                               this.nextCapturedFirstHitDelayObservationIndexes,
                                               attacker.Identity.Instance,
                                               capturedContract.CapturedFirstHitDelayObservationsSeconds)
                                           : capturedContract.FirstHitDelaySeconds;
                bool usesSplitFixedAttackStartPackets = capturedContract.AttackModel
                                                        == CapturedEnemyAttackModel.FixedAttackInfo;
                DateTime attackSequenceStartedAt = now;
                if (usesSplitFixedAttackStartPackets)
                {
                    this.AnnounceCapturedEnemySpecialAttackWeaponContext(
                        attacker,
                        capturedContract);
                    attackSequenceStartedAt = DateTime.UtcNow;
                }

                if (attackStartDelaySeconds > 0)
                {
                    this.pendingCapturedAttackStarts[attacker.Identity.Instance] =
                        attackSequenceStartedAt + TimeSpan.FromSeconds(
                            attackStartDelaySeconds);
                }
                else
                {
                    this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                }

                if (capturedContract.HasCapturedCombatStopSequence)
                {
                    this.pendingCapturedMovementTransitions[attacker.Identity.Instance] =
                        attackSequenceStartedAt + TimeSpan.FromSeconds(
                            attackStartDelaySeconds
                            + capturedContract.MovementTransitionDelaySeconds);
                }
                else
                {
                    this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                }

                this.nextCombatTicks[attacker.Identity.Instance] =
                    attackSequenceStartedAt + TimeSpan.FromSeconds(
                        attackStartDelaySeconds + firstHitDelaySeconds);
            }
            else
            {
                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                this.nextCombatTicks[attacker.Identity.Instance] =
                    now + TimeSpan.FromSeconds(initialDelaySeconds);
            }

            if (specialAttackSequence != null)
            {
                this.AnnounceCapturedSpecialAttackSequenceContext(attacker, specialAttackSequence);
            }
            else if (!Playfield.IsCapturedCleaningRobot(attacker))
            {
                if (hasCapturedAttackStart && attackStartDelaySeconds <= 0)
                {
                    if (capturedContract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                    {
                        this.AnnounceCapturedEnemyAttackStartContext(attacker, capturedContract);
                        this.nextCombatTicks[attacker.Identity.Instance] =
                            DateTime.UtcNow + TimeSpan.FromSeconds(firstHitDelaySeconds);
                    }
                    else
                    {
                        this.AnnounceCapturedEnemyAttackStartContext(attacker, capturedContract);
                    }
                }
            }

            this.EnsureNpcCharacterWeapon(attacker, initialDelaySeconds);
        }

        private void EnsureNpcCharacterWeapon(ICharacter attacker, double initialAttackDelaySeconds)
        {
            if (this.playfield == null || attacker == null)
            {
                return;
            }

            this.playfield.ConfigureWeaponsFromEquipment(attacker);

            Character character = attacker as Character;
            if (character == null)
            {
                return;
            }

            CapturedEnemyCombatContract capturedContract;
            bool hasCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                                           attacker.Identity.Instance,
                                           out capturedContract)
                                       && capturedContract.IsCombatReady;
            if (hasCapturedContract)
            {
                // Captured schedules own hits; keep inventory (incl. 44008) but do not arm CharacterWeapon clocks.
                character.ClearWeapons();
                return;
            }

            if (initialAttackDelaySeconds > 0.0)
            {
                CharacterWeapon main;
                if (character.Weapons.TryGetValue(WeaponSlot.MainHand, out main) && main != null)
                {
                    main.ConfigureSpeeds(initialAttackDelaySeconds, main.RechargeSpeed);
                }
            }

            character.ResetAllWeaponAttacks();
        }

        private void SendNpcWeaponDefinitionsToPlayerTarget(ICharacter attacker)
        {
            ICharacter target = this.playfield.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null || !(target.Controller is PlayerController))
            {
                return;
            }

            foreach (WeaponItemFullUpdateMessage message in
                WeaponItemFullUpdate.CreateWeaponDefinitionMessages(attacker))
            {
                target.Send(message);
                WeaponItemFullUpdate.LogObserverWeaponDefinition(attacker, target, message);
            }
        }

        internal void ClearTracking(Identity identity)
        {
            this.nextCombatTicks.Remove(identity.Instance);
            this.lastNpcCombatWeaponSlots.Remove(identity.Instance);
            this.lastNpcUnarmedAttackInfoSlots.Remove(identity.Instance);
            this.lastNpcSpecialAttackWeaponTargets.Remove(identity.Instance);
            this.completedCapturedOpeningAttacks.Remove(identity.Instance);
            this.pendingCapturedAttackStarts.Remove(identity.Instance);
            this.pendingCapturedMovementTransitions.Remove(identity.Instance);
            this.nextCapturedParallelAttackTicks.Remove(identity.Instance);
            this.startedCapturedParallelAttackClocks.Remove(identity.Instance);
            this.nextBasicCaptureBackedAttackTicks.Remove(identity.Instance);
            this.startedBasicCaptureBackedAttackClocks.Remove(identity.Instance);
            this.nextLineOfSightRetryTicks.Remove(identity.Instance);
            this.nextLineOfSightDiagnosticTicks.Remove(identity.Instance);
            this.capturedDamageObservationCursor.Clear(identity.Instance);
            this.nextCapturedAttackStartDelayObservationIndexes.Remove(identity.Instance);
            this.nextCapturedFirstHitDelayObservationIndexes.Remove(identity.Instance);
            this.nextCapturedLandedIntervalObservationIndexes.Remove(identity.Instance);
            this.nextBasicInitialDelayObservationIndexes.Remove(identity.Instance);
            this.nextBasicDamageObservationIndexes.Remove(identity.Instance);
            this.nextBasicLandedIntervalObservationIndexes.Remove(identity.Instance);

            if (this.playfield != null)
            {
                ICharacter character = this.playfield.FindByIdentity<ICharacter>(identity);
                Character c = character as Character;
                if (c != null)
                {
                    c.ClearWeapons();
                }
            }
        }

        internal void ClearRuntimeState()
        {
            this.nextCombatTicks.Clear();
            this.lastNpcCombatWeaponSlots.Clear();
            this.lastNpcUnarmedAttackInfoSlots.Clear();
            this.lastNpcSpecialAttackWeaponTargets.Clear();
            this.completedCapturedOpeningAttacks.Clear();
            this.pendingCapturedAttackStarts.Clear();
            this.pendingCapturedMovementTransitions.Clear();
            this.nextCapturedParallelAttackTicks.Clear();
            this.startedCapturedParallelAttackClocks.Clear();
            this.nextBasicCaptureBackedAttackTicks.Clear();
            this.startedBasicCaptureBackedAttackClocks.Clear();
            this.nextLineOfSightRetryTicks.Clear();
            this.nextLineOfSightDiagnosticTicks.Clear();
            this.capturedDamageObservationCursor.ClearAll();
            this.capturedSpecialAttackWeaponStateCursor.ClearAll();
            this.nextCapturedAttackStartDelayObservationIndexes.Clear();
            this.nextCapturedFirstHitDelayObservationIndexes.Clear();
            this.nextCapturedLandedIntervalObservationIndexes.Clear();
            this.nextBasicInitialDelayObservationIndexes.Clear();
            this.nextBasicDamageObservationIndexes.Clear();
            this.nextBasicLandedIntervalObservationIndexes.Clear();
        }

        internal void ProcessCombatTick(ICharacter attacker)
        {
            if (attacker == null || this.playfield == null)
            {
                return;
            }

            CapturedEnemyCombatContract registeredCapturedContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    attacker.Identity.Instance,
                    out registeredCapturedContract)
                && !registeredCapturedContract.IsCombatReady)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            if (registeredCapturedContract != null
                && registeredCapturedContract.IsCombatReady
                && !this.ValidateRequiredCapturedWeapon(attacker, registeredCapturedContract))
            {
                return;
            }

            if (attacker.FightingTarget.Instance == 0)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null
                || !target.InPlayfield(this.playfield.Identity)
                || target.Stats[StatIds.health].Value <= 0
                || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(attacker, target))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "CombatTickTargetInvalid attacker={0} target={1} found={2} inPlayfield={3} health={4}",
                        attacker.Identity,
                        attacker.FightingTarget,
                        target != null,
                        target != null && target.InPlayfield(this.playfield.Identity),
                        target == null ? 0 : target.Stats[StatIds.health].Value));
                double invalidDistance = target == null
                                             ? -1.0
                                             : Playfield.GetCombatDistance(attacker, target);
                Playfield.LogNpcBrain("Idle", "target-invalid", attacker, target, 0.0, invalidDistance);

                this.playfield.ClearInvalidNpcCombatTarget(attacker);
                return;
            }

            string missionSpatialFailure;
            if (!ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.TryValidateCombatPair(
                attacker,
                target,
                out missionSpatialFailure))
            {
                this.playfield.ClearInvalidNpcCombatTarget(attacker);
                return;
            }

            DateTime pendingAttackStart;
            if (this.pendingCapturedAttackStarts.TryGetValue(
                    attacker.Identity.Instance,
                    out pendingAttackStart))
            {
                if (pendingAttackStart > DateTime.UtcNow)
                {
                    return;
                }

                CapturedEnemyCombatContract pendingContract;
                bool capturedAttackStartReleased = false;
                if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                        attacker.Identity.Instance,
                        out pendingContract)
                    && pendingContract.IsCombatReady)
                {
                    if (pendingContract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                    {
                        DateTime scheduledFirstHit;
                        if (!this.nextCombatTicks.TryGetValue(
                                attacker.Identity.Instance,
                                out scheduledFirstHit)
                            || scheduledFirstHit < pendingAttackStart)
                        {
                            CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                                attacker,
                                "captured Attack-to-first-hit schedule is unavailable or invalid");
                            this.ClearTracking(attacker.Identity);
                            return;
                        }

                        TimeSpan capturedAttackToFirstHit = scheduledFirstHit - pendingAttackStart;
                        this.AnnounceCapturedEnemyAttackStartContext(attacker, pendingContract);
                        this.nextCombatTicks[attacker.Identity.Instance] =
                            DateTime.UtcNow + capturedAttackToFirstHit;
                        capturedAttackStartReleased = true;
                    }
                    else if (pendingContract.ParallelAttackSequence != null)
                    {
                        this.AnnounceCapturedParallelAttackPacket(
                            attacker,
                            pendingContract.ParallelAttackSequence);
                        this.StartCapturedParallelAttackClocks(
                            attacker.Identity.Instance,
                            pendingContract.ParallelAttackSequence,
                            DateTime.UtcNow);
                        capturedAttackStartReleased = true;
                    }
                    else
                    {
                        this.AnnounceCapturedEnemyAttackStartContext(attacker, pendingContract);
                    }
                }

                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                if (!capturedAttackStartReleased)
                {
                    return;
                }
            }

            DateTime pendingMovementTransition;
            if (this.pendingCapturedMovementTransitions.TryGetValue(
                    attacker.Identity.Instance,
                    out pendingMovementTransition))
            {
                if (pendingMovementTransition > DateTime.UtcNow)
                {
                    return;
                }

                CapturedEnemyCombatContract movementContract;
                NPCController npcController = attacker.Controller as NPCController;
                if (npcController != null
                    && CapturedEnemyCombatRuntimeRegistry.TryGet(
                        attacker.Identity.Instance,
                        out movementContract)
                    && movementContract.IsCombatReady
                    && movementContract.HasCapturedCombatStopSequence)
                {
                    CombatAttackSource movementAttackSource = this.GetCombatAttackSource(attacker);
                    if (movementAttackSource == null)
                    {
                        this.playfield.ClearNpcCombatTracking(attacker.Identity);
                        return;
                    }

                    AORebirth.Core.Vector.Vector3 movementDestination;
                    if (!this.playfield.TryResolveCapturedNpcMovementDestination(
                            attacker,
                            target,
                            movementAttackSource.Range,
                            DateTime.UtcNow,
                            out movementDestination))
                    {
                        movementDestination = attacker.CalculatePredictedPosition().coordinate;
                    }

                    npcController.StopFollowForCapturedCombatRange(
                        target.CalculatePredictedPosition().coordinate,
                        movementDestination);
                }

                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                return;
            }

            CapturedEnemyCombatContract parallelContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    attacker.Identity.Instance,
                    out parallelContract)
                && parallelContract.IsCombatReady
                && parallelContract.AttackModel
                   == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary
                && parallelContract.BasicCombat != null)
            {
                this.ProcessBasicCaptureBackedOrdinaryAttackTicks(
                    attacker,
                    target,
                    parallelContract);
                return;
            }

            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    attacker.Identity.Instance,
                    out parallelContract)
                && parallelContract.IsCombatReady
                && parallelContract.ParallelAttackSequence != null)
            {
                this.ProcessCapturedParallelAttackTicks(
                    attacker,
                    target,
                    parallelContract);
                return;
            }

            CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);
            if (attackSource == null)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            CapturedEnemyCombatContract activeCapturedContract;
            bool hasActiveCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                                                 attacker.Identity.Instance,
                                                 out activeCapturedContract)
                                             && activeCapturedContract.IsCombatReady;

            // CharacterWeapon clocks drive ordinary swings. Captured contracts keep legacy clocks.
            if (!hasActiveCapturedContract)
            {
                this.ProcessNpcCombatMovementMaintenance(attacker, target, attackSource);
                return;
            }

            bool maintainMovementDuringRecharge =
                Playfield.IsCapturedCleaningRobot(attacker)
                || PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker)
                || hasActiveCapturedContract;
            DateTime nextTick;
            DateTime now = DateTime.UtcNow;
            if (this.nextCombatTicks.TryGetValue(attacker.Identity.Instance, out nextTick)
                && nextTick > now)
            {
                if (maintainMovementDuringRecharge
                    && (this.playfield.HasActiveNpcChaseNavigation(attacker)
                        || !this.playfield.IsInCombatRange(attacker, target, attackSource.Range)))
                {
                    this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                }
                else if (maintainMovementDuringRecharge
                         && attackSource.Range <= NpcCombatAttackRules.MaxMeleeCombatDistance)
                {
                    this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
                }

                return;
            }

            if (!this.playfield.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                this.nextCombatTicks[attacker.Identity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(NpcCombatAttackRules.OutOfRangeRetrySeconds);
                return;
            }

            if (!this.CanApplyNpcDamage(
                    attacker,
                    target,
                    activeCapturedContract,
                    now))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                return;
            }

            this.ApplyNpcCombatHitCore(attacker, target, attackSource, activeCapturedContract);

            if (attacker.FightingTarget.Instance != 0)
            {
                this.nextCombatTicks[attacker.Identity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(
                        this.ResolveLandedRechargeSeconds(attacker, attackSource));
            }
        }

        /// <summary>
        /// Apply one NPC auto-attack hit. Called from CharacterWeapon Attacked (no nextCombatTicks gate).
        /// </summary>
        internal void ApplyCombatHit(ICharacter attacker)
        {
            if (attacker == null || this.playfield == null || attacker.FightingTarget.Instance == 0)
            {
                return;
            }

            CapturedEnemyCombatContract registeredCapturedContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    attacker.Identity.Instance,
                    out registeredCapturedContract)
                && registeredCapturedContract.IsCombatReady)
            {
                // Captured combat keeps legacy ProcessCombatTick timing.
                return;
            }

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null
                || !target.InPlayfield(this.playfield.Identity)
                || target.Stats[StatIds.health].Value <= 0
                || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(attacker, target))
            {
                this.playfield.ClearInvalidNpcCombatTarget(attacker);
                return;
            }

            CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);
            if (attackSource == null)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            if (!this.playfield.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                return;
            }

            if (!this.CanApplyNpcDamage(attacker, target, null, DateTime.UtcNow))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                return;
            }

            this.ApplyNpcCombatHitCore(attacker, target, attackSource, null);
        }

        private void ProcessNpcCombatMovementMaintenance(
            ICharacter attacker,
            ICharacter target,
            CombatAttackSource attackSource)
        {
            bool maintainMovement =
                Playfield.IsCapturedCleaningRobot(attacker)
                || PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker)
                || true;

            if (!this.playfield.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                return;
            }

            this.playfield.HoldNpcAtCombatPosition(attacker, target);
            if (maintainMovement && attackSource.Range <= NpcCombatAttackRules.MaxMeleeCombatDistance)
            {
                this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
            }
        }

        private void ApplyNpcCombatHitCore(
            ICharacter attacker,
            ICharacter target,
            CombatAttackSource attackSource,
            CapturedEnemyCombatContract activeCapturedContract)
        {
            this.playfield.HoldNpcAtCombatPosition(attacker, target);

            if (attackSource.Range <= NpcCombatAttackRules.MaxMeleeCombatDistance)
            {
                this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
            }

            if (!this.TryApplyCapturedWeaponAmmo(attacker, attackSource))
            {
                return;
            }

            Character attackerCharacter = attacker as Character;
            if (attackerCharacter == null)
            {
                return;
            }

            AORebirth.Core.Combat.CombatStrikeContext strikeContext =
                this.BuildStrikeContext(attackerCharacter, attackSource);
            if (strikeContext == null)
            {
                return;
            }

            this.AnnounceNpcSpecialAttackWeaponContextIfNeeded(attacker, target, attackSource);

            AORebirth.Core.Combat.CombatStrikeResult strikeResult =
                attackerCharacter.Strike(target, strikeContext);

            if (strikeResult.Outcome != AORebirth.Core.Combat.StrikeOutcome.Applied)
            {
                return;
            }

            if (attackSource.CompletesCapturedOpeningAttack)
            {
                this.completedCapturedOpeningAttacks.Add(attacker.Identity.Instance);
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Combat hit attacker={0} target={1} damage={2} health={3}/{4} weaponBased={5} slot={6}",
                    attacker.Identity,
                    target.Identity,
                    strikeResult.Damage,
                    strikeResult.NewHealth,
                    target.Stats[StatIds.life].Value,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attackSource.AttackInfoWeaponSlot));

            if (strikeResult.KillingHit)
            {
                if (PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker))
                {
                    this.playfield.Announce(
                        new StopFightMessage
                        {
                            Identity = attacker.Identity,
                            Unknown1 = 1
                        });
                }
            }
        }

        private AORebirth.Core.Combat.CombatStrikeContext BuildStrikeContext(
            Character attackerCharacter,
            CombatAttackSource attackSource,
            int? fixedDamageOverride = null)
        {
            if (attackerCharacter == null || attackSource == null)
            {
                return null;
            }

            AORebirth.Core.Combat.CombatStrikeContext strikeContext;
            if (attackSource.UsesEquippedWeapon)
            {
                strikeContext = AORebirth.Core.Combat.CharacterCombatStrikeBuilder.Build(
                    attackerCharacter,
                    AORebirth.Core.Entities.WeaponSlot.MainHand);
            }
            else
            {
                strikeContext = new AORebirth.Core.Combat.CombatStrikeContext
                                {
                                    MinDamage = attackSource.MinDamage,
                                    MaxDamage = Math.Max(attackSource.MinDamage, attackSource.MaxDamage),
                                    DamageBonus = attackSource.DamageBonus,
                                    UsesEquippedWeapon = false,
                                    WeaponSlot = AORebirth.Core.Entities.WeaponSlot.MainHand,
                                    RawDamageType = attackerCharacter.Stats[StatIds.damagetype].Value
                                };
            }

            if (strikeContext == null)
            {
                return null;
            }

            if (attackSource.MinDamage > 0)
            {
                strikeContext.MinDamage = attackSource.MinDamage;
            }

            if (attackSource.MaxDamage > 0)
            {
                strikeContext.MaxDamage = Math.Max(strikeContext.MinDamage, attackSource.MaxDamage);
            }

            strikeContext.DamageBonus = attackSource.DamageBonus;
            strikeContext.Range = attackSource.Range > 0.0
                                    ? attackSource.Range
                                    : strikeContext.Range;
            strikeContext.AttackInfoAmmoCount = attackSource.AttackInfoAmmoCount;
            strikeContext.AttackInfoWeaponSlot = attackSource.AttackInfoWeaponSlot;
            strikeContext.AttackInfoHitType = attackSource.AttackInfoHitType;
            strikeContext.AttackInfoWeaponInstance = attackSource.AttackInfoWeaponInstance;
            strikeContext.AttackInfoUnknown = attackSource.AttackInfoUnk1;
            strikeContext.AttackInfoN3Unknown = attackSource.AttackInfoN3Unknown;
            strikeContext.LethalAttackInfoUnknown = attackSource.LethalAttackInfoUnknown;
            strikeContext.PreserveAttackInfoWireValues = true;
            strikeContext.SendAttackInfo = attackSource.SendAttackInfo;
            strikeContext.DamageSource = attackSource.UsesEquippedWeapon
                                             ? AORebirth.Core.Combat.CombatDamageSource.WeaponAutoAttack
                                             : AORebirth.Core.Combat.CombatDamageSource.UnarmedAutoAttack;
            if (fixedDamageOverride.HasValue && fixedDamageOverride.Value > 0)
            {
                strikeContext.FixedDamage = fixedDamageOverride.Value;
            }
            else if (attackSource.CapturedDamageObservations != null
                     && attackSource.CapturedDamageObservations.Length > 0)
            {
                strikeContext.FixedDamage = this.capturedDamageObservationCursor.Select(
                    attackerCharacter.Identity.Instance,
                    attackSource.CapturedDamageObservations);
            }

            return strikeContext;
        }

        private bool TryApplyCapturedWeaponAmmo(
            ICharacter attacker,
            CombatAttackSource attackSource)
        {
            if (!attackSource.UsesCapturedWeaponEnergy)
            {
                return true;
            }

            int ammoCount;
            if (CapturedEnemyCombatRuntimeRegistry.TryConsumeCapturedWeaponAmmo(
                    attacker.Identity.Instance,
                    out ammoCount))
            {
                attackSource.AttackInfoAmmoCount = ammoCount;
                return true;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                "CapturedEnemyCombatAmmoQuarantined attacker=" + attacker.Identity
                + " reason=captured weapon Energy is exhausted or unavailable");
            CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                attacker,
                "captured weapon Energy is exhausted or unavailable");
            this.playfield.ClearNpcCombatTracking(attacker.Identity);
            return false;
        }

        private bool ValidateRequiredCapturedWeapon(
            ICharacter attacker,
            CapturedEnemyCombatContract contract)
        {
            if (contract == null || !contract.RequiresPhysicalWeaponPresentation)
            {
                return true;
            }

            IItem item;
            string failure;
            if (CapturedEnemyCombatRuntime.TryValidateLiveCapturedWeapon(
                    attacker,
                    contract,
                    out item,
                    out failure))
            {
                return true;
            }

            CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(attacker, failure);
            this.ClearTracking(attacker.Identity);
            return false;
        }

        private bool TryResolveCapturedWeaponAttackRange(
            ICharacter attacker,
            CapturedEnemyCombatContract contract,
            out double range)
        {
            range = 0.0d;
            IItem item;
            string failure;
            if (!CapturedEnemyCombatRuntime.TryValidateLiveCapturedWeapon(
                    attacker,
                    contract,
                    out item,
                    out failure))
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(attacker, failure);
                this.ClearTracking(attacker.Identity);
                return false;
            }

            int rawRange = NormalizeCombatItemStat(
                item.GetAttribute((int)StatIds.attackrange),
                0);
            if (rawRange <= 0)
            {
                return false;
            }

            range = rawRange > 1000 ? rawRange / 100.0d : rawRange;
            return range > 0.0d && !double.IsNaN(range) && !double.IsInfinity(range);
        }

        private static bool UsesCapturedPhysicalWeapon(
            CapturedEnemyCombatContract contract,
            CapturedEnemyCombatAttackDefinition attack)
        {
            return contract != null
                   && attack != null
                   && contract.WeaponDefinition != null
                   && attack.AttackInfoWeaponSlot == contract.WeaponDefinition.InventorySlot
                   && attack.AttackInfoWeaponInstance == 0;
        }

        private bool IsLineOfSightRetryPending(ICharacter attacker, DateTime utcNow)
        {
            DateTime retryAt;
            return this.nextLineOfSightRetryTicks.TryGetValue(
                       attacker.Identity.Instance,
                       out retryAt)
                   && retryAt > utcNow;
        }

        private bool CanApplyNpcDamage(
            ICharacter attacker,
            ICharacter target,
            CapturedEnemyCombatContract capturedContract,
            DateTime utcNow)
        {
            bool requiresDamageLineOfSight =
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    NpcDamageLineOfSightRuntimeService.Pf127DamageLineOfSightActivated,
                    attacker.Stats[StatIds.monsterdata].Value,
                    capturedContract == null
                        ? (bool?)null
                        : capturedContract.RequiresDamageLineOfSight);
            if (this.IsLineOfSightRetryPending(attacker, utcNow))
            {
                return false;
            }

            if (requiresDamageLineOfSight)
            {
                var start = new CollisionPoint3(
                    (float)attacker.Position.x,
                    (float)attacker.Position.y,
                    (float)attacker.Position.z);
                var end = new CollisionPoint3(
                    (float)target.Position.x,
                    (float)target.Position.y,
                    (float)target.Position.z);
                SegmentTriangleHit hit;
                NpcDamageLineOfSightDecision decision = this.damageLineOfSight.EvaluateAttackLine(
                    true,
                    start,
                    end,
                    out hit);
                if (decision != NpcDamageLineOfSightDecision.AllowedClear
                    && decision != NpcDamageLineOfSightDecision.AllowedNotRequired)
                {
                    this.nextLineOfSightRetryTicks[attacker.Identity.Instance] =
                        utcNow + TimeSpan.FromSeconds(NpcCombatAttackRules.OutOfRangeRetrySeconds);
                    this.LogLineOfSightDenied(attacker, target, decision, hit, utcNow);
                    return false;
                }
            }

            if (!this.playfield.IsNpcAttackPathTraversable(attacker, target))
            {
                this.nextLineOfSightRetryTicks[attacker.Identity.Instance] =
                    utcNow + TimeSpan.FromSeconds(NpcCombatAttackRules.OutOfRangeRetrySeconds);
                this.LogNavigationDenied(attacker, target, utcNow);
                return false;
            }

            if (!this.playfield.EnsureNpcCombatVisibility(attacker, target))
            {
                this.nextLineOfSightRetryTicks[attacker.Identity.Instance] =
                    utcNow + TimeSpan.FromSeconds(NpcCombatAttackRules.OutOfRangeRetrySeconds);
                return false;
            }

            this.nextLineOfSightRetryTicks.Remove(attacker.Identity.Instance);
            return true;
        }

        private void LogNavigationDenied(
            ICharacter attacker,
            ICharacter target,
            DateTime utcNow)
        {
            DateTime nextDiagnostic;
            if (this.nextLineOfSightDiagnosticTicks.TryGetValue(
                    attacker.Identity.Instance,
                    out nextDiagnostic)
                && nextDiagnostic > utcNow)
            {
                return;
            }

            this.nextLineOfSightDiagnosticTicks[attacker.Identity.Instance] =
                utcNow + TimeSpan.FromSeconds(10.0);
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NpcChaseNavigationAttackDenied attacker={0} target={1} reason=movement-segment-blocked",
                    attacker.Identity,
                    target.Identity));
        }

        private void LogLineOfSightDenied(
            ICharacter attacker,
            ICharacter target,
            NpcDamageLineOfSightDecision decision,
            SegmentTriangleHit hit,
            DateTime utcNow)
        {
            DateTime nextDiagnostic;
            if (this.nextLineOfSightDiagnosticTicks.TryGetValue(
                    attacker.Identity.Instance,
                    out nextDiagnostic)
                && nextDiagnostic > utcNow)
            {
                return;
            }

            this.nextLineOfSightDiagnosticTicks[attacker.Identity.Instance] =
                utcNow + TimeSpan.FromSeconds(10.0);
            string detail = decision == NpcDamageLineOfSightDecision.DeniedBlocked
                                ? string.Format(
                                    CultureInfo.InvariantCulture,
                                    "triangle={0} fraction={1:0.000000}",
                                    hit.TriangleId,
                                    hit.SegmentFraction)
                                : "geometryError=" + this.damageLineOfSight.GeometryError;
            LogUtil.Debug(
                decision == NpcDamageLineOfSightDecision.DeniedBlocked
                    ? DebugInfoDetail.Network
                    : DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NpcDamageLineOfSightDenied attacker={0} target={1} decision={2} {3}",
                    attacker.Identity,
                    target.Identity,
                    decision,
                    detail));
        }

        private double ResolveLandedRechargeSeconds(
            ICharacter attacker,
            CombatAttackSource attackSource)
        {
            // Player-owned attack pets always use pet cadence — never weapon itemdelay/recharge
            // (MEW templates can resolve to ~20s and stall swings).
            if (attacker != null
                && PetCombatRules.IsPlayerOwnedPet(attacker)
                && !PetCombatRules.IsPlayerOwnedHealingPet(attacker))
            {
                return PetCombatRules.AttackPetRechargeSeconds;
            }

            if (attackSource.CapturedLandedIntervalObservationsSeconds != null
                && attackSource.CapturedLandedIntervalObservationsSeconds.Length > 0)
            {
                return SelectCapturedDoubleObservation(
                    this.nextCapturedLandedIntervalObservationIndexes,
                    attacker.Identity.Instance,
                    attackSource.CapturedLandedIntervalObservationsSeconds);
            }

            return attackSource.RechargeSeconds;
        }

        private static double SelectCapturedDoubleObservation(
            IDictionary<int, int> nextIndexes,
            int attackerInstance,
            double[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                throw new InvalidOperationException("Captured timing observations are required.");
            }

            int index;
            if (!nextIndexes.TryGetValue(attackerInstance, out index)
                || index < 0
                || index >= observations.Length)
            {
                index = 0;
            }

            double selected = observations[index];
            nextIndexes[attackerInstance] = (index + 1) % observations.Length;
            return selected;
        }

        private void AnnounceNpcSpecialAttackWeaponContextIfNeeded(
            ICharacter attacker,
            ICharacter target,
            CombatAttackSource attackSource)
        {
            int attackerInstance = attacker.Identity.Instance;
            int targetInstance = target.Identity.Instance;
            int previousTargetInstance;
            int? previousTarget = this.lastNpcSpecialAttackWeaponTargets.TryGetValue(
                                      attackerInstance,
                                      out previousTargetInstance)
                                      ? previousTargetInstance
                                      : (int?)null;
            if (!NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    Playfield.IsCapturedCleaningRobot(attacker),
                    attackSource.UsesEquippedWeapon,
                    previousTarget,
                    targetInstance)
                && !NpcCombatAttackRules.ShouldSendPlayerOwnedAttackPetAttackStartContext(
                    PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker),
                    previousTarget,
                    targetInstance))
            {
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets[attackerInstance] = targetInstance;
            if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
            {
                // Guardians use one equipped right-hand sword (not MEW dual-wield templates).
                // Empty SpecialAttackWeapon + AttackMessage matches equipped-weapon NPC combat start.
                this.playfield.Announce(
                    new SpecialAttackWeaponMessage
                    {
                        Identity = attacker.Identity,
                        Unknown = 0,
                        Specials = new SpecialAttack[0],
                        MeleeInit = 0,
                        RangedInit = 0,
                        PhysicalInit = 0,
                        NanoInit = 0,
                        AggDef = 0
                    });
                this.playfield.Announce(
                    new AttackMessage
                    {
                        Identity = attacker.Identity,
                        Target = target.Identity,
                        Action = 0
                    });
                return;
            }

            if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker)
                || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker))
            {
                this.AnnouncePlayerOwnedAttackPetAttackStartContext(attacker, target);
                return;
            }

            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    attacker.Identity,
                    CreateCapturedCleaningRobotSpecialAttacks(),
                    0,
                    NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponLastValue));
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                PlayfieldLifecycleTrace.MessageSpecialAttackWeapon,
                attacker.Identity,
                "target=" + target.Identity);

            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    attacker.Identity,
                    target.Identity,
                    0,
                    0));
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                PlayfieldLifecycleTrace.MessageAttack,
                attacker.Identity,
                "target=" + target.Identity);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatNpcAttackStartContextSend attacker={0} target={1} monsterData={2}",
                    attacker.Identity,
                    target.Identity,
                    attacker.Stats[StatIds.monsterdata].Value));
        }

        private void AnnouncePlayerOwnedAttackPetAttackStartContext(ICharacter attacker, ICharacter target)
        {
            this.playfield.Announce(
                new SpecialAttackWeaponMessage
                {
                    Identity = attacker.Identity,
                    Specials = CreatePlayerOwnedAttackPetSpecialAttacks(),
                    MeleeInit = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    RangedInit = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    PhysicalInit = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    NanoInit = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    AggDef = 0
                });
            this.playfield.Announce(
                new AttackMessage
                {
                    Identity = attacker.Identity,
                    Target = target.Identity,
                    Action = 0
                });

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatPetAttackStartContextSend attacker={0} target={1}",
                    attacker.Identity,
                    target.Identity));
        }

        private void AnnounceCapturedSpecialAttackSequenceContext(
            ICharacter attacker,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            if (attacker.FightingTarget.Instance == 0 || specialAttackSequence == null)
            {
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets[attacker.Identity.Instance] = attacker.FightingTarget.Instance;
            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    attacker.Identity,
                    specialAttackSequence));
            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    attacker.Identity,
                    attacker.FightingTarget,
                    specialAttackSequence));
        }

        private void AnnounceCapturedParallelAttackSequenceContext(
            ICharacter attacker,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence)
        {
            if (attacker.FightingTarget.Instance == 0 || parallelAttackSequence == null)
            {
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets[attacker.Identity.Instance] = attacker.FightingTarget.Instance;
            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    attacker.Identity,
                    parallelAttackSequence));
            DateTime attackSequenceStartedAt = DateTime.UtcNow;
            if (parallelAttackSequence.AttackStartDelaySeconds > 0.0d)
            {
                this.pendingCapturedAttackStarts[attacker.Identity.Instance] =
                    attackSequenceStartedAt + TimeSpan.FromSeconds(
                        parallelAttackSequence.AttackStartDelaySeconds);
                return;
            }

            this.AnnounceCapturedParallelAttackPacket(attacker, parallelAttackSequence);
            this.StartCapturedParallelAttackClocks(
                attacker.Identity.Instance,
                parallelAttackSequence,
                DateTime.UtcNow);
        }

        private void AnnounceCapturedParallelAttackPacket(
            ICharacter attacker,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence)
        {
            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    attacker.Identity,
                    attacker.FightingTarget,
                    parallelAttackSequence));
        }

        private void StartCapturedParallelAttackClocks(
            int attackerInstance,
            CapturedEnemyParallelAttackSequenceDefinition sequence,
            DateTime attackStartedAt)
        {
            if (this.startedCapturedParallelAttackClocks.Contains(attackerInstance))
            {
                return;
            }

            this.nextCapturedParallelAttackTicks[attackerInstance] = sequence.Streams
                .Select(value => attackStartedAt + TimeSpan.FromSeconds(value.InitialDelaySeconds))
                .ToArray();
            this.startedCapturedParallelAttackClocks.Add(attackerInstance);
        }

        private void StartBasicCaptureBackedAttackClocks(
            int attackerInstance,
            CapturedBasicCombatContractDefinition basicCombat,
            DateTime attackStartedAt)
        {
            if (basicCombat == null || !basicCombat.IsValid)
            {
                return;
            }

            if (this.startedBasicCaptureBackedAttackClocks.Contains(attackerInstance))
            {
                return;
            }

            CapturedBasicCombatStreamDefinition[] streams = basicCombat.Streams;
            DateTime[] nextTicks = new DateTime[streams.Length];
            for (int index = 0; index < streams.Length; index++)
            {
                nextTicks[index] = attackStartedAt + TimeSpan.FromSeconds(
                    this.SelectBasicInitialDelayObservation(
                        attackerInstance,
                        index,
                        streams[index],
                        streams.Length));
            }

            this.nextBasicCaptureBackedAttackTicks[attackerInstance] = nextTicks;
            this.startedBasicCaptureBackedAttackClocks.Add(attackerInstance);
        }

        private void ProcessBasicCaptureBackedOrdinaryAttackTicks(
            ICharacter attacker,
            ICharacter target,
            CapturedEnemyCombatContract contract)
        {
            CapturedBasicCombatContractDefinition basicCombat = contract.BasicCombat;
            if (basicCombat == null || !basicCombat.IsValid)
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                    attacker,
                    "basic captured ordinary combat contract is unavailable at runtime");
                this.ClearTracking(attacker.Identity);
                return;
            }

            CapturedBasicCombatStreamDefinition[] streams = basicCombat.Streams;
            DateTime now = DateTime.UtcNow;
            double attackRange = NpcCombatSpatialPolicy.GenericBasicMeleeAttackRange;
            if (!IsResolvedAttackRange(attackRange))
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                    attacker,
                    "generic basic melee spatial policy did not resolve a valid attack range");
                this.ClearTracking(attacker.Identity);
                return;
            }

            if (!this.playfield.IsInCombatRange(attacker, target, attackRange))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackRange);
                return;
            }

            if (!this.CanApplyNpcDamage(attacker, target, contract, now))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackRange);
                return;
            }

            this.playfield.HoldNpcAtCombatPosition(attacker, target);
            this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackRange);

            DateTime[] nextTicks;
            if (!this.startedBasicCaptureBackedAttackClocks.Contains(attacker.Identity.Instance)
                || !this.nextBasicCaptureBackedAttackTicks.TryGetValue(
                    attacker.Identity.Instance,
                    out nextTicks)
                || nextTicks.Length != streams.Length)
            {
                this.StartBasicCaptureBackedAttackClocks(
                    attacker.Identity.Instance,
                    basicCombat,
                    now);
                if (!this.nextBasicCaptureBackedAttackTicks.TryGetValue(
                        attacker.Identity.Instance,
                        out nextTicks)
                    || nextTicks.Length != streams.Length)
                {
                    return;
                }
            }

            int dueIndex = -1;
            DateTime dueAt = DateTime.MaxValue;
            for (int index = 0; index < nextTicks.Length; index++)
            {
                if (nextTicks[index] <= now && nextTicks[index] < dueAt)
                {
                    dueIndex = index;
                    dueAt = nextTicks[index];
                }
            }

            if (dueIndex < 0)
            {
                return;
            }

            CapturedBasicCombatStreamDefinition stream = streams[dueIndex];
            CapturedBasicCombatDamageObservation observation =
                this.SelectBasicDamageObservation(
                    attacker.Identity.Instance,
                    dueIndex,
                    stream,
                    streams.Length);
            Character attackerCharacter = attacker as Character;
            if (attackerCharacter == null)
            {
                return;
            }

            var attackSource = new CombatAttackSource
            {
                MinDamage = observation.Amount,
                MaxDamage = observation.Amount,
                DamageBonus = 0,
                Range = attackRange,
                RechargeSeconds = 0.0d,
                UsesEquippedWeapon = false,
                AttackInfoAmmoCount = stream.AttackInfoAmmoCount,
                AttackInfoWeaponSlot = stream.AttackInfoWeaponSlot,
                AttackInfoUnk1 = observation.AttackInfoDamageTypeWire,
                AttackInfoHitType = stream.AttackInfoHitTypeWire,
                AttackInfoWeaponInstance = stream.AttackInfoWeaponInstance,
                AttackInfoN3Unknown = stream.AttackInfoN3Byte,
                SendAttackInfo = true
            };

            AORebirth.Core.Combat.CombatStrikeContext strikeContext =
                this.BuildStrikeContext(
                    attackerCharacter,
                    attackSource,
                    observation.Amount);
            if (strikeContext == null)
            {
                return;
            }

            AORebirth.Core.Combat.CombatStrikeResult strikeResult =
                attackerCharacter.Strike(target, strikeContext);
            if (strikeResult.Outcome != AORebirth.Core.Combat.StrikeOutcome.Applied)
            {
                return;
            }

            nextTicks[dueIndex] = now + TimeSpan.FromSeconds(
                this.SelectBasicLandedIntervalObservation(
                    attacker.Identity.Instance,
                    dueIndex,
                    stream,
                    streams.Length));

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Combat basic capture-backed hit attacker={0} target={1} stream={2} slot={3} damage={4} damageTypeWire={5} health={6}/{7}",
                    attacker.Identity,
                    target.Identity,
                    stream.StreamId,
                    stream.AttackInfoWeaponSlot,
                    strikeResult.Damage,
                    stream.AttackInfoHitTypeWire,
                    strikeResult.NewHealth,
                    target.Stats[StatIds.life].Value));
        }

        private double SelectBasicInitialDelayObservation(
            int attackerInstance,
            int streamIndex,
            CapturedBasicCombatStreamDefinition stream,
            int streamCount)
        {
            return SelectBasicDoubleObservation(
                this.nextBasicInitialDelayObservationIndexes,
                attackerInstance,
                streamIndex,
                streamCount,
                stream.InitialDelayObservationsSeconds);
        }

        private CapturedBasicCombatDamageObservation SelectBasicDamageObservation(
            int attackerInstance,
            int streamIndex,
            CapturedBasicCombatStreamDefinition stream,
            int streamCount)
        {
            int[] indexes = ResolveBasicObservationIndexes(
                this.nextBasicDamageObservationIndexes,
                attackerInstance,
                streamCount);
            int selected = indexes[streamIndex];
            indexes[streamIndex] = (selected + 1) % stream.DamageObservations.Length;
            return stream.DamageObservations[selected];
        }

        private double SelectBasicLandedIntervalObservation(
            int attackerInstance,
            int streamIndex,
            CapturedBasicCombatStreamDefinition stream,
            int streamCount)
        {
            return SelectBasicDoubleObservation(
                this.nextBasicLandedIntervalObservationIndexes,
                attackerInstance,
                streamIndex,
                streamCount,
                stream.LandedIntervalObservationsSeconds);
        }

        private static double SelectBasicDoubleObservation(
            Dictionary<int, int[]> observationIndexes,
            int attackerInstance,
            int streamIndex,
            int streamCount,
            double[] observations)
        {
            int[] indexes = ResolveBasicObservationIndexes(
                observationIndexes,
                attackerInstance,
                streamCount);
            int selected = indexes[streamIndex];
            indexes[streamIndex] = (selected + 1) % observations.Length;
            return observations[selected];
        }

        private static int[] ResolveBasicObservationIndexes(
            Dictionary<int, int[]> observationIndexes,
            int attackerInstance,
            int streamCount)
        {
            int[] indexes;
            if (!observationIndexes.TryGetValue(attackerInstance, out indexes)
                || indexes.Length != streamCount)
            {
                indexes = new int[streamCount];
                observationIndexes[attackerInstance] = indexes;
            }

            return indexes;
        }

        private void ProcessCapturedParallelAttackTicks(
            ICharacter attacker,
            ICharacter target,
            CapturedEnemyCombatContract contract)
        {
            CapturedEnemyParallelAttackSequenceDefinition sequence = contract.ParallelAttackSequence;
            CapturedEnemyParallelAttackStreamDefinition[] streams = sequence.Streams;
            DateTime now = DateTime.UtcNow;
            var resolvedRanges = new double[streams.Length];
            for (int index = 0; index < streams.Length; index++)
            {
                CapturedEnemyCombatAttackDefinition candidate = streams[index].Attack;
                resolvedRanges[index] = candidate.Range;
                if (UsesCapturedPhysicalWeapon(contract, candidate)
                    && !this.TryResolveCapturedWeaponAttackRange(
                        attacker,
                        contract,
                        out resolvedRanges[index]))
                {
                    return;
                }
            }

            double maximumRange = resolvedRanges.Max();
            if (!this.playfield.IsInCombatRange(attacker, target, maximumRange))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, maximumRange);
                return;
            }

            if (!this.CanApplyNpcDamage(attacker, target, contract, now))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, maximumRange);
                return;
            }

            this.playfield.HoldNpcAtCombatPosition(attacker, target);
            this.playfield.UpdateNpcMeleeFollowHold(attacker, target, maximumRange);
            DateTime[] nextTicks;
            if (!this.startedCapturedParallelAttackClocks.Contains(attacker.Identity.Instance)
                || !this.nextCapturedParallelAttackTicks.TryGetValue(
                    attacker.Identity.Instance,
                    out nextTicks)
                || nextTicks.Length != streams.Length)
            {
                nextTicks = streams
                    .Select(value => now + TimeSpan.FromSeconds(value.InitialDelaySeconds))
                    .ToArray();
                this.nextCapturedParallelAttackTicks[attacker.Identity.Instance] = nextTicks;
                this.startedCapturedParallelAttackClocks.Add(attacker.Identity.Instance);
            }

            int dueIndex = -1;
            DateTime dueAt = DateTime.MaxValue;
            for (int index = 0; index < nextTicks.Length; index++)
            {
                if (nextTicks[index] <= now && nextTicks[index] < dueAt)
                {
                    dueIndex = index;
                    dueAt = nextTicks[index];
                }
            }

            if (dueIndex < 0)
            {
                return;
            }

            CapturedEnemyCombatAttackDefinition attack = streams[dueIndex].Attack;
            var attackSource = new CombatAttackSource
            {
                MinDamage = attack.MinDamage,
                MaxDamage = attack.MaxDamage,
                DamageBonus = attack.DamageBonus,
                Range = resolvedRanges[dueIndex],
                RechargeSeconds = attack.RechargeSeconds,
                UsesEquippedWeapon = attack.UsesEquippedWeapon,
                AttackInfoAmmoCount = attack.AttackInfoAmmoCount,
                AttackInfoWeaponSlot = attack.AttackInfoWeaponSlot,
                AttackInfoUnk1 = attack.AttackInfoUnknown,
                AttackInfoHitType = attack.AttackInfoHitType,
                AttackInfoWeaponInstance = attack.AttackInfoWeaponInstance,
                AttackInfoN3Unknown = attack.AttackInfoN3Unknown,
                LethalAttackInfoUnknown = attack.LethalAttackInfoUnknown,
                UsesCapturedWeaponEnergy = contract.WeaponDefinition != null
                                           && attack.AttackInfoWeaponSlot
                                              == contract.WeaponDefinition.InventorySlot
                                           && attack.AttackInfoWeaponInstance == 0,
                SendAttackInfo = attack.SendAttackInfo,
                CapturedDamageObservations = attack.CapturedDamageObservations
            };

            if (!this.TryApplyCapturedWeaponAmmo(attacker, attackSource))
            {
                return;
            }

            Character attackerCharacter = attacker as Character;
            if (attackerCharacter == null)
            {
                return;
            }

            AORebirth.Core.Combat.CombatStrikeContext strikeContext =
                this.BuildStrikeContext(attackerCharacter, attackSource);
            if (strikeContext == null)
            {
                return;
            }

            AORebirth.Core.Combat.CombatStrikeResult strikeResult =
                attackerCharacter.Strike(target, strikeContext);
            if (strikeResult.Outcome != AORebirth.Core.Combat.StrikeOutcome.Applied)
            {
                return;
            }

            nextTicks[dueIndex] = streams[dueIndex].ResolveNextTickAfterHit(now);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Combat parallel hit attacker={0} target={1} stream={2} damage={3} health={4}/{5}",
                    attacker.Identity,
                    target.Identity,
                    dueIndex,
                    strikeResult.Damage,
                    strikeResult.NewHealth,
                    target.Stats[StatIds.life].Value));
        }

        private void AnnounceCapturedEnemyAttackStartContext(
            ICharacter attacker,
            CapturedEnemyCombatContract capturedContract)
        {
            this.AnnounceCapturedEnemySpecialAttackWeaponContext(attacker, capturedContract);
            this.AnnounceCapturedEnemyAttackPacket(attacker, capturedContract);
        }

        private void AnnounceCapturedEnemySpecialAttackWeaponContext(
            ICharacter attacker,
            CapturedEnemyCombatContract capturedContract)
        {
            if (attacker == null || capturedContract == null || attacker.FightingTarget.Instance == 0)
            {
                return;
            }

            if (capturedContract.HasCapturedSpecialAttackWeaponContext)
            {
                this.lastNpcSpecialAttackWeaponTargets[attacker.Identity.Instance] =
                    attacker.FightingTarget.Instance;
                int specialAttackWeaponUnknown5 =
                    capturedContract.CapturedSpecialAttackWeaponUnknown5Observations == null
                    || capturedContract.CapturedSpecialAttackWeaponUnknown5Observations.Length == 0
                        ? capturedContract.SpecialAttackWeaponUnknown5
                        : this.capturedSpecialAttackWeaponStateCursor.Select(
                            attacker.Identity.Instance,
                            capturedContract.CapturedSpecialAttackWeaponUnknown5Observations);
                this.playfield.Announce(
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        attacker.Identity,
                        capturedContract,
                        specialAttackWeaponUnknown5));
            }
        }

        private void AnnounceCapturedEnemyAttackPacket(
            ICharacter attacker,
            CapturedEnemyCombatContract capturedContract)
        {
            if (attacker == null || capturedContract == null || attacker.FightingTarget.Instance == 0)
            {
                return;
            }

            this.playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    attacker.Identity,
                    attacker.FightingTarget,
                    capturedContract));

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatCapturedEnemyAttackStartContextSend attacker={0} target={1}",
                    attacker.Identity,
                    attacker.FightingTarget));
        }

        private static SpecialAttack[] CreatePlayerOwnedAttackPetSpecialAttacks()
        {
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = PetCombatRules.AttackPetLeftWeaponTemplate,
                           Unknown2 = PetCombatRules.AttackPetRightWeaponTemplate,
                           Unknown3 = PetCombatRules.AttackPetLeftWeaponTag,
                           Unknown4 = PetCombatRules.AttackPetLeftWeaponName
                       },
                       new SpecialAttack
                       {
                           Unknown1 = PetCombatRules.AttackPetLeftWeaponHighTemplate,
                           Unknown2 = PetCombatRules.AttackPetRightWeaponHighTemplate,
                           Unknown3 = PetCombatRules.AttackPetRightWeaponTag,
                           Unknown4 = PetCombatRules.AttackPetRightWeaponName
                       }
                   };
        }

        private static CapturedEnemySpecialAttackDefinition[] CreateCapturedCleaningRobotSpecialAttacks()
        {
            return new[]
                   {
                       new CapturedEnemySpecialAttackDefinition(
                           NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTemplate,
                           NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTemplate,
                           NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTag,
                           "LIW2"),
                       new CapturedEnemySpecialAttackDefinition(
                           NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTemplate,
                           NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTemplate,
                           NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTag,
                           "LIW1")
                   };
        }

        private void AnnounceCombatDamage(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatAttackSource attackSource,
            CombatDamageSource source)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackInfoSend source={0} attacker={1} target={2} dmg={3} u2={4} u3={5} u4={6} u5={7} u6={8} weaponBased={9} atkDefault={10} atkDamageType={11} atkWeaponType={12} atkEquippedWeapons={13}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage,
                    attackSource.AttackInfoAmmoCount,
                    attackSource.AttackInfoWeaponSlot,
                    attackSource.AttackInfoUnk1,
                    attackSource.AttackInfoHitType,
                    attackSource.AttackInfoWeaponInstance,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attacker.Stats[StatIds.defaultattacktype].Value,
                    attacker.Stats[StatIds.damagetype].Value,
                    attacker.Stats[StatIds.weapontype].Value,
                    attacker.Stats[StatIds.equippedweapons].Value));

            if (attackSource.SendAttackInfo)
            {
                this.playfield.Announce(
                    CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                        attacker.Identity,
                        target.Identity,
                        damage,
                        attackSource.AttackInfoAmmoCount,
                        attackSource.AttackInfoWeaponSlot,
                        attackSource.AttackInfoUnk1,
                        attackSource.AttackInfoHitType,
                        attackSource.AttackInfoWeaponInstance,
                        attackSource.AttackInfoN3Unknown));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo,
                    PlayfieldLifecycleTrace.MessageAttackInfo,
                    attacker.Identity,
                    "target=" + target.Identity);
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatAttackInfoSkip source={0} attacker={1} target={2} dmg={3} reason=no_captured_or_equipped_context",
                        source,
                        attacker.Identity,
                        target.Identity,
                        damage));
            }

            this.AnnounceHealthDamageIfNeeded(attacker, target, damage, source);

            // Capture/live AO: AttackInfo alone drives "hit you for N points of melee damage"
            // into the Combat chat window. Do not also send ChatText — that duplicates the line
            // into General.
        }

        private void AnnounceHealthDamageIfNeeded(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatDamageSource source)
        {
            if (!ShouldSendHealthDamage(source))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatHealthDamageSkip source={0} attacker={1} target={2} dmg={3}",
                        source,
                        attacker.Identity,
                        target.Identity,
                        damage));
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatHealthDamageSend source={0} attacker={1} target={2} dmg={3}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage));

            this.playfield.Announce(
                new HealthDamageMessage
                {
                    Identity = attacker.Identity,
                    Unknown1 = damage,
                    Unknown2 = 0,
                    Unknown3 = 0,
                    Unknown4 = 0,
                    Target = target.Identity,
                    Unknown5 = 0
                });
        }

        private static bool ShouldSendHealthDamage(CombatDamageSource source)
        {
            return source != CombatDamageSource.WeaponAutoAttack
                   && source != CombatDamageSource.UnarmedAutoAttack;
        }

        private CombatAttackSource GetCombatAttackSource(ICharacter attacker)
        {
            if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
            {
                int rawMinDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0);
                int rawMaxDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0);
                int fallbackMinDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(
                    attacker.Stats[StatIds.level].Value);
                int fallbackMaxDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(
                    attacker.Stats[StatIds.level].Value);
                int petMinDamage = rawMinDamage > 0
                                       ? rawMinDamage
                                       : (rawMaxDamage > 0 ? rawMaxDamage : fallbackMinDamage);
                int petMaxDamage = rawMaxDamage > 0
                                       ? rawMaxDamage
                                       : (rawMinDamage > 0 ? rawMinDamage : fallbackMaxDamage);

                return new CombatAttackSource
                       {
                           MinDamage = petMinDamage,
                           MaxDamage = petMaxDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = PetCombatRules.AttackPetRechargeSeconds,
                           UsesEquippedWeapon = true,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = (int)WeaponSlots.Righthand,
                           AttackInfoUnk1 = PetCombatRules.AttackPetAttackInfoUnk1,
                           AttackInfoHitType = PetCombatRules.AttackPetAttackInfoHitType,
                           AttackInfoWeaponInstance = 0,
                           SendAttackInfo = true
                       };
            }

            if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker)
                || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker)
                || (PetCombatRules.IsPlayerOwnedPet(attacker)
                    && !PetCombatRules.IsPlayerOwnedHealingPet(attacker)
                    && !PetBureaucratGuardianAppearance.IsGuardianPet(attacker)
                    && !PetCombatRules.UsesBureaucratWorkerBuw1CombatPackets(attacker)))
            {
                int rawMinDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0);
                int rawMaxDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0);
                int fallbackMinDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(
                    attacker.Stats[StatIds.level].Value);
                int fallbackMaxDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(
                    attacker.Stats[StatIds.level].Value);
                int petMinDamage = rawMinDamage > 0
                                       ? rawMinDamage
                                       : (rawMaxDamage > 0 ? rawMaxDamage : fallbackMinDamage);
                int petMaxDamage = rawMaxDamage > 0
                                       ? rawMaxDamage
                                       : (rawMinDamage > 0 ? rawMinDamage : fallbackMaxDamage);
                int attackInfoWeaponInstance = this.GetAttackPetAttackInfoWeaponInstance(attacker);

                return new CombatAttackSource
                       {
                           MinDamage = petMinDamage,
                           MaxDamage = petMaxDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = PetCombatRules.AttackPetRechargeSeconds,
                           UsesEquippedWeapon = true,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = PetCombatRules.AttackPetAttackInfoWeaponSlot,
                           AttackInfoUnk1 = PetCombatRules.AttackPetAttackInfoUnk1,
                           AttackInfoHitType = PetCombatRules.AttackPetAttackInfoHitType,
                           AttackInfoWeaponInstance = attackInfoWeaponInstance,
                           SendAttackInfo = true
                       };
            }

            CapturedEnemyCombatContract capturedContract;
            bool hasCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                                           attacker.Identity.Instance,
                                           out capturedContract)
                                       && capturedContract.IsCombatReady;
            if (hasCapturedContract
                && !this.ValidateRequiredCapturedWeapon(attacker, capturedContract))
            {
                return null;
            }

            if (hasCapturedContract
                && capturedContract.AttackModel == CapturedEnemyAttackModel.Specialized
                && capturedContract.SpecialAttackSequence != null)
            {
                CapturedEnemySpecialAttackSequenceDefinition sequence =
                    capturedContract.SpecialAttackSequence;
                bool openingAttackCompleted = sequence.OpeningAttack == null
                                              || this.completedCapturedOpeningAttacks.Contains(
                                                  attacker.Identity.Instance);
                CapturedEnemyCombatAttackDefinition attack = openingAttackCompleted
                                                                  ? sequence.RepeatingAttack
                                                                  : sequence.OpeningAttack;
                double attackRange = attack.Range;
                if (UsesCapturedPhysicalWeapon(capturedContract, attack)
                    && !this.TryResolveCapturedWeaponAttackRange(
                        attacker,
                        capturedContract,
                        out attackRange))
                {
                    return null;
                }

                if (!IsResolvedAttackRange(attackRange))
                {
                    return null;
                }

                return new CombatAttackSource
                       {
                           MinDamage = attack.MinDamage,
                           MaxDamage = attack.MaxDamage,
                           DamageBonus = attack.DamageBonus,
                           Range = attackRange,
                           RechargeSeconds = attack.RechargeSeconds,
                           UsesEquippedWeapon = attack.UsesEquippedWeapon
                                                || capturedContract.WeaponDefinition != null,
                           AttackInfoAmmoCount = attack.AttackInfoAmmoCount,
                           AttackInfoWeaponSlot = attack.AttackInfoWeaponSlot,
                           AttackInfoUnk1 = attack.AttackInfoUnknown,
                           AttackInfoHitType = attack.AttackInfoHitType,
                           AttackInfoWeaponInstance = attack.AttackInfoWeaponInstance,
                           AttackInfoN3Unknown = attack.AttackInfoN3Unknown,
                           LethalAttackInfoUnknown = attack.LethalAttackInfoUnknown,
                           UsesCapturedWeaponEnergy = capturedContract.WeaponDefinition != null
                                                      && attack.AttackInfoWeaponSlot
                                                         == capturedContract.WeaponDefinition.InventorySlot
                                                      && attack.AttackInfoWeaponInstance == 0,
                           SendAttackInfo = attack.SendAttackInfo,
                           CompletesCapturedOpeningAttack = !openingAttackCompleted,
                           CapturedDamageObservations = attack.CapturedDamageObservations
                       };
            }

            if (hasCapturedContract
                && capturedContract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            {
                double attackRange;
                if (capturedContract.CapturedAttackRange.HasValue)
                {
                    attackRange = capturedContract.CapturedAttackRange.Value;
                }
                else if (capturedContract.CapturedUsesEquippedWeapon)
                {
                    if (!this.TryResolveCapturedWeaponAttackRange(
                            attacker,
                            capturedContract,
                            out attackRange))
                    {
                        return null;
                    }
                }
                else
                {
                    // Authored FixedAttackOnSight / unarmed fixed damage — melee range.
                    attackRange = NpcCombatAttackRules.MaxMeleeCombatDistance;
                }

                if (!IsResolvedAttackRange(attackRange))
                {
                    return null;
                }

                bool unarmedFixed = !capturedContract.CapturedUsesEquippedWeapon;
                bool useCapturedAttackInfoWire = capturedContract.CapturedUsesEquippedWeapon
                                                 || capturedContract.AttackInfoWeaponInstance != 0
                                                 || capturedContract.AttackInfoHitType != 0;
                int attackInfoWeaponSlot = useCapturedAttackInfoWire
                                               ? capturedContract.AttackInfoWeaponSlot
                                               : this.GetUnarmedAttackInfoWeaponSlot(attacker);
                int attackInfoWeaponInstance = useCapturedAttackInfoWire
                                                   ? capturedContract.AttackInfoWeaponInstance
                                                   : this.GetUnarmedAttackInfoWeaponInstance(attacker);
                int attackInfoAmmoCount = capturedContract.AttackInfoAmmoCount != 0
                                              ? capturedContract.AttackInfoAmmoCount
                                              : (unarmedFixed
                                                     ? NpcCombatAttackRules.UnarmedAttackInfoAmmoCount
                                                     : 0);
                int attackInfoHitType = capturedContract.AttackInfoHitType != 0
                                            ? capturedContract.AttackInfoHitType
                                            : (unarmedFixed
                                                   ? NpcCombatAttackRules.NormalAttackInfoHitType
                                                   : 0);

                return new CombatAttackSource
                       {
                           MinDamage = capturedContract.MinDamage,
                           MaxDamage = capturedContract.MaxDamage,
                           DamageBonus = capturedContract.CapturedDamageBonus,
                           Range = attackRange,
                           RechargeSeconds = capturedContract.RechargeSeconds,
                           UsesEquippedWeapon = capturedContract.CapturedUsesEquippedWeapon,
                           AttackInfoAmmoCount = attackInfoAmmoCount,
                           AttackInfoWeaponSlot = attackInfoWeaponSlot,
                           AttackInfoUnk1 = capturedContract.AttackInfoUnknown,
                           AttackInfoHitType = attackInfoHitType,
                           AttackInfoWeaponInstance = attackInfoWeaponInstance,
                           AttackInfoN3Unknown = capturedContract.AttackInfoN3Unknown,
                           UsesCapturedWeaponEnergy = capturedContract.WeaponDefinition != null
                                                      && capturedContract.AttackInfoWeaponSlot
                                                         == capturedContract.WeaponDefinition.InventorySlot
                                                      && capturedContract.AttackInfoWeaponInstance == 0,
                           SendAttackInfo = true,
                           CapturedDamageObservations = capturedContract.CapturedDamageObservations,
                           CapturedLandedIntervalObservationsSeconds =
                               capturedContract.CapturedLandedIntervalObservationsSeconds
                       };
            }

            EquippedCombatWeapon equippedWeapon = this.GetEquippedCombatWeapon(attacker);
            if (equippedWeapon == null)
            {
                if (hasCapturedContract
                    && capturedContract.RequiresPhysicalWeaponPresentation)
                {
                    CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                        attacker,
                        "required captured weapon is missing from the live inventory");
                    return null;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatAttackSource unarmed attacker={0} mindmg={1} maxdmg={2} bonus={3} defaultattack={4} damagetype={5} weapontype={6} equippedweapons={7}",
                        attacker.Identity,
                        attacker.Stats[StatIds.mindamage].Value,
                        attacker.Stats[StatIds.maxdamage].Value,
                        attacker.Stats[StatIds.damagebonus].Value,
                        attacker.Stats[StatIds.defaultattacktype].Value,
                        attacker.Stats[StatIds.damagetype].Value,
                        attacker.Stats[StatIds.weapontype].Value,
                        attacker.Stats[StatIds.equippedweapons].Value));
                int attackInfoWeaponSlot = this.GetUnarmedAttackInfoWeaponSlot(attacker);
                int attackInfoDamage = this.GetUnarmedAttackDamage(attacker, attackInfoWeaponSlot);
                return new CombatAttackSource
                       {
                           MinDamage = attackInfoDamage,
                           MaxDamage = attackInfoDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = Playfield.IsCapturedCleaningRobot(attacker)
                                                 ? NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds
                                                 : NpcCombatAttackRules.DefaultCombatTickSeconds,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = attackInfoWeaponSlot,
                           AttackInfoUnk1 = 0,
                           AttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = this.GetUnarmedAttackInfoWeaponInstance(attacker),
                           SendAttackInfo = true
                        };
            }

            IItem weapon = equippedWeapon.Item;
            if (hasCapturedContract
                && capturedContract.RequiresPhysicalWeaponPresentation
                && (equippedWeapon.Slot != capturedContract.WeaponInventorySlot
                    || !capturedContract.MatchesCapturedWeapon(weapon)))
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                    attacker,
                    "live weapon does not match required captured slot/templates/QL"
                    + "; expectedSlot=" + capturedContract.WeaponInventorySlot
                    + "; actualSlot=" + equippedWeapon.Slot
                    + "; expected=" + capturedContract.WeaponLowId + "/"
                    + capturedContract.WeaponHighId + "/" + capturedContract.WeaponQuality
                    + "; actual=" + weapon.LowID + "/" + weapon.HighID + "/" + weapon.Quality);
                return null;
            }

            bool hasCapturedEquippedAttackInfo = capturedContract != null
                                                  && capturedContract.HasCapturedEquippedAttackInfo;
            bool usesCapturedDamageOverride = hasCapturedEquippedAttackInfo
                                                  && !capturedContract.UsesEquippedWeaponDamage;
            bool usesActorValuesForPresentationWeapon =
                hasCapturedEquippedAttackInfo
                && capturedContract.UsesProductionActorValuesForPresentationWeapon;
            double equippedAttackRange;
            if (usesActorValuesForPresentationWeapon)
            {
                equippedAttackRange = NpcCombatAttackRules.MaxMeleeCombatDistance;
            }
            else if (hasCapturedEquippedAttackInfo
                && capturedContract.CapturedAttackRange.HasValue)
            {
                equippedAttackRange = capturedContract.CapturedAttackRange.Value;
            }
            else if (hasCapturedEquippedAttackInfo)
            {
                if (!this.TryResolveCapturedWeaponAttackRange(
                        attacker,
                        capturedContract,
                        out equippedAttackRange))
                {
                    return null;
                }
            }
            else
            {
                equippedAttackRange = NormalizeCombatRange(
                    weapon.GetAttribute((int)StatIds.attackrange));
            }

            if (!IsResolvedAttackRange(equippedAttackRange))
            {
                return null;
            }

            int minDamage = usesActorValuesForPresentationWeapon
                                ? NormalizeCombatItemStat(
                                    attacker.Stats[StatIds.mindamage].Value,
                                    0)
                                : usesCapturedDamageOverride
                                    ? capturedContract.MinDamage
                                    : NormalizeCombatItemStat(
                                        weapon.GetAttribute((int)StatIds.mindamage),
                                        0);
            int maxDamage = usesActorValuesForPresentationWeapon
                                ? NormalizeCombatItemStat(
                                    attacker.Stats[StatIds.maxdamage].Value,
                                    minDamage)
                                : usesCapturedDamageOverride
                                    ? capturedContract.MaxDamage
                                    : NormalizeCombatItemStat(
                                        weapon.GetAttribute((int)StatIds.maxdamage),
                                        0);
            int damageBonus = usesActorValuesForPresentationWeapon
                                  ? NormalizeCombatItemStat(
                                      attacker.Stats[StatIds.damagebonus].Value,
                                      0)
                                  : usesCapturedDamageOverride
                                      ? capturedContract.CapturedDamageBonus
                                      : NormalizeCombatItemStat(
                                          weapon.GetAttribute((int)StatIds.damagebonus),
                                          0);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackSource weapon attacker={0} item={1}/{2} slot={3} min={4} max={5} damageBonus={6} rangeRaw={7}",
                    attacker.Identity,
                    weapon.LowID,
                    weapon.HighID,
                    equippedWeapon.Slot,
                    minDamage,
                    maxDamage,
                    damageBonus,
                    weapon.GetAttribute((int)StatIds.attackrange)));

            return new CombatAttackSource
                   {
                       MinDamage = minDamage,
                       MaxDamage = maxDamage,
                       DamageBonus = damageBonus,
                       Range = equippedAttackRange,
                       RechargeSeconds = hasCapturedEquippedAttackInfo
                                             && capturedContract.UsesEquippedWeaponTiming
                                                 ? NormalizeCombatDelaySeconds(
                                                     weapon.GetAttribute(
                                                         (int)StatIds.itemdelay),
                                                     weapon.GetAttribute(
                                                         (int)StatIds.rechargedelay))
                                             : hasCapturedEquippedAttackInfo
                                               && capturedContract.RechargeSeconds > 0
                                             ? capturedContract.RechargeSeconds
                                             : NormalizeCombatDelaySeconds(
                                                 weapon.GetAttribute((int)StatIds.itemdelay),
                                                 weapon.GetAttribute((int)StatIds.rechargedelay)),
                       AttackSpeedSeconds = NormalizeDelayCentisecondsToSeconds(
                           weapon.GetAttribute((int)StatIds.itemdelay),
                           CharacterWeapon.DefaultAttackSpeedSeconds),
                       RechargeOnlySeconds = NormalizeDelayCentisecondsToSeconds(
                           weapon.GetAttribute((int)StatIds.rechargedelay),
                           CharacterWeapon.DefaultRechargeSpeedSeconds),
                       UsesEquippedWeapon = true,
                       AttackInfoAmmoCount = hasCapturedEquippedAttackInfo
                                                 ? capturedContract.AttackInfoAmmoCount
                                                 : 40,
                       AttackInfoWeaponSlot = hasCapturedEquippedAttackInfo
                                                  ? capturedContract.AttackInfoWeaponSlot
                                                  : equippedWeapon.Slot,
                       AttackInfoUnk1 = hasCapturedEquippedAttackInfo
                                            ? capturedContract.AttackInfoUnknown
                                            : 4,
                       AttackInfoHitType = hasCapturedEquippedAttackInfo
                                               ? capturedContract.AttackInfoHitType
                                               : NpcCombatAttackRules.NormalAttackInfoHitType,
                       AttackInfoWeaponInstance = hasCapturedEquippedAttackInfo
                                                       ? capturedContract.AttackInfoWeaponInstance
                                                       : 0,
                       AttackInfoN3Unknown = hasCapturedEquippedAttackInfo
                                                 ? capturedContract.AttackInfoN3Unknown
                                                 : (byte)0,
                       UsesCapturedWeaponEnergy = hasCapturedEquippedAttackInfo
                                                  && capturedContract.WeaponDefinition != null,
                       SendAttackInfo = true
                   };
        }

        private static bool IsResolvedAttackRange(double range)
        {
            return range > 0.0d && !double.IsNaN(range) && !double.IsInfinity(range);
        }

        private int GetAttackPetAttackInfoWeaponInstance(ICharacter attacker)
        {
            int attackerInstance = attacker.Identity.Instance;
            int lastSlot;
            if (this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attackerInstance, out lastSlot)
                && lastSlot == PetCombatRules.AttackPetAttackInfoWeaponSlot)
            {
                this.lastNpcUnarmedAttackInfoSlots[attackerInstance] =
                    NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
                return PetCombatRules.AttackPetRightWeaponTag;
            }

            this.lastNpcUnarmedAttackInfoSlots[attackerInstance] = PetCombatRules.AttackPetAttackInfoWeaponSlot;
            return PetCombatRules.AttackPetLeftWeaponTag;
        }

        private int GetUnarmedAttackInfoWeaponSlot(ICharacter attacker)
        {
            int lastSlot;
            if (this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attacker.Identity.Instance, out lastSlot)
                && lastSlot == NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot)
            {
                this.lastNpcUnarmedAttackInfoSlots[attacker.Identity.Instance] =
                    NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot;
                return NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot;
            }

            this.lastNpcUnarmedAttackInfoSlots[attacker.Identity.Instance] =
                NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
            return NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
        }

        private int GetUnarmedAttackDamage(ICharacter attacker, int attackInfoWeaponSlot)
        {
            if (Playfield.IsCapturedCleaningRobot(attacker))
            {
                return attackInfoWeaponSlot == NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot
                           ? NpcCombatAttackRules.CapturedCleaningRobotLeftHandDamage
                           : NpcCombatAttackRules.CapturedCleaningRobotRightHandDamage;
            }

            return Math.Max(
                NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0),
                NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0));
        }

        private int GetUnarmedAttackInfoWeaponInstance(ICharacter attacker)
        {
            int slot;
            if (!this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attacker.Identity.Instance, out slot)
                || slot == NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot)
            {
                return NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponInstance;
            }

            return NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponInstance;
        }

        private EquippedCombatWeapon GetEquippedCombatWeapon(ICharacter attacker)
        {
            if (attacker.BaseInventory == null
                || !attacker.BaseInventory.Pages.ContainsKey((int)IdentityType.WeaponPage))
            {
                this.lastNpcCombatWeaponSlots.Remove(attacker.Identity.Instance);
                return null;
            }

            IInventoryPage weaponPage = attacker.BaseInventory.Pages[(int)IdentityType.WeaponPage];
            IItem rightHand = weaponPage[(int)WeaponSlots.Righthand];
            IItem leftHand = weaponPage[(int)WeaponSlots.LeftHand];
            bool rightHandUsable = IsWieldableCombatWeapon(rightHand);
            bool leftHandUsable = IsWieldableCombatWeapon(leftHand);

            if (rightHandUsable && leftHandUsable)
            {
                int attackerInstance = attacker.Identity.Instance;
                int lastSlot;
                if (this.lastNpcCombatWeaponSlots.TryGetValue(attackerInstance, out lastSlot)
                    && lastSlot == (int)WeaponSlots.Righthand)
                {
                    this.lastNpcCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.LeftHand;
                    return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
                }

                this.lastNpcCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (rightHandUsable)
            {
                this.lastNpcCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (leftHandUsable)
            {
                this.lastNpcCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.LeftHand;
                return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
            }

            this.lastNpcCombatWeaponSlots.Remove(attacker.Identity.Instance);
            return null;
        }

        private static int NormalizeCombatItemStat(int value, int fallback)
        {
            return value == MissingItemStatValue ? fallback : value;
        }

        private static bool IsWieldableCombatWeapon(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.ItemActions != null && item.ItemActions.Any(x => x.ActionType == ActionType.ToWield))
            {
                return true;
            }

            return NormalizeCombatItemStat(item.GetAttribute((int)StatIds.mindamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.maxdamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.attackrange), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.itemdelay), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.rechargedelay), 0) > 0;
        }

        private static double NormalizeCombatRange(int range)
        {
            int normalizedRange = NormalizeCombatItemStat(range, 0);
            if (normalizedRange <= 0)
            {
                return NpcCombatAttackRules.MaxMeleeCombatDistance;
            }

            return normalizedRange > 1000 ? normalizedRange / 100.0 : normalizedRange;
        }

        private static double NormalizeCombatDelaySeconds(int attackDelay, int rechargeDelay)
        {
            int normalizedAttackDelay = NormalizeCombatItemStat(attackDelay, 0);
            int normalizedRechargeDelay = NormalizeCombatItemStat(rechargeDelay, 0);
            int totalCentiseconds = normalizedAttackDelay + normalizedRechargeDelay;

            if (totalCentiseconds <= 0)
            {
                return NpcCombatAttackRules.DefaultCombatTickSeconds;
            }

            return Math.Max(0.25, totalCentiseconds / 100.0);
        }

        private static double NormalizeDelayCentisecondsToSeconds(int delayCentiseconds, double fallbackSeconds)
        {
            int normalized = NormalizeCombatItemStat(delayCentiseconds, 0);
            if (normalized <= 0)
            {
                return fallbackSeconds;
            }

            if (normalized > 500)
            {
                normalized = 100;
            }

            return Math.Max(0.05, normalized / 100.0);
        }

        private sealed class CombatAttackSource
        {
            public int MinDamage { get; set; }

            public int MaxDamage { get; set; }

            public int DamageBonus { get; set; }

            public double Range { get; set; }

            public double RechargeSeconds { get; set; }

            /// <summary>Itemdelay only (seconds). Used by CharacterWeapon AttackSpeed.</summary>
            public double AttackSpeedSeconds { get; set; }

            /// <summary>Rechargedelay only (seconds). Used by CharacterWeapon RechargeSpeed.</summary>
            public double RechargeOnlySeconds { get; set; }

            public bool UsesEquippedWeapon { get; set; }

            public int AttackInfoAmmoCount { get; set; }

            public int AttackInfoWeaponSlot { get; set; }

            public int AttackInfoUnk1 { get; set; }

            public int AttackInfoHitType { get; set; }

            public int AttackInfoWeaponInstance { get; set; }

            public byte AttackInfoN3Unknown { get; set; }

            public int? LethalAttackInfoUnknown { get; set; }

            public bool UsesCapturedWeaponEnergy { get; set; }

            public bool SendAttackInfo { get; set; }

            public bool CompletesCapturedOpeningAttack { get; set; }

            public int[] CapturedDamageObservations { get; set; }

            public double[] CapturedLandedIntervalObservationsSeconds { get; set; }
        }

        private enum CombatDamageSource
        {
            WeaponAutoAttack,
            UnarmedAutoAttack,
            BasicCaptureBackedOrdinaryAutoAttack,
            DamageOverTime,
            HealOverTime,
            Nano,
            Environment
        }

        private sealed class EquippedCombatWeapon
        {
            public IItem Item { get; set; }

            public int Slot { get; set; }
        }
    }
}
