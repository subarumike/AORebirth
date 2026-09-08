namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine.Core;

    internal enum CapturedEnemyAttackModel
    {
        Unresolved,
        FixedAttackInfo,
        EquippedWeapon,
        Specialized,
        BasicCaptureBackedOrdinary
    }

    internal enum CapturedBasicCombatFieldAuthority
    {
        Captured,
        GovernedDerived,
        GenericRuntimePolicy,
        OptionalPositiveBehavior
    }

    internal sealed class CapturedBasicCombatDamageObservation
    {
        internal CapturedBasicCombatDamageObservation(
            int amount,
            int attackInfoDamageTypeWire,
            string evidence)
        {
            this.Amount = amount;
            this.AttackInfoDamageTypeWire = attackInfoDamageTypeWire;
            this.Evidence = evidence ?? string.Empty;
        }

        internal int Amount { get; private set; }

        internal int AttackInfoDamageTypeWire { get; private set; }

        internal string Evidence { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.Amount > 0
                       && this.AttackInfoDamageTypeWire >= 0
                       && !string.IsNullOrWhiteSpace(this.Evidence);
            }
        }
    }

    internal sealed class CapturedBasicCombatStreamDefinition
    {
        internal CapturedBasicCombatStreamDefinition(
            int streamId,
            int attackInfoWeaponSlot,
            int attackInfoAmmoCount,
            int attackInfoHitTypeWire,
            int attackInfoWeaponInstance,
            byte attackInfoN3Byte,
            double[] initialDelayObservationsSeconds,
            double[] landedIntervalObservationsSeconds,
            CapturedBasicCombatDamageObservation[] damageObservations)
        {
            this.StreamId = streamId;
            this.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            this.AttackInfoAmmoCount = attackInfoAmmoCount;
            this.AttackInfoHitTypeWire = attackInfoHitTypeWire;
            this.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            this.AttackInfoN3Byte = attackInfoN3Byte;
            this.InitialDelayObservationsSeconds =
                initialDelayObservationsSeconds == null
                    ? new double[0]
                    : initialDelayObservationsSeconds.ToArray();
            this.LandedIntervalObservationsSeconds =
                landedIntervalObservationsSeconds == null
                    ? new double[0]
                    : landedIntervalObservationsSeconds.ToArray();
            this.DamageObservations = damageObservations == null
                                          ? new CapturedBasicCombatDamageObservation[0]
                                          : damageObservations.ToArray();
        }

        internal int StreamId { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoHitTypeWire { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal byte AttackInfoN3Byte { get; private set; }

        internal double[] InitialDelayObservationsSeconds { get; private set; }

        internal double[] LandedIntervalObservationsSeconds { get; private set; }

        internal CapturedBasicCombatDamageObservation[] DamageObservations { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.StreamId >= 0
                       && this.AttackInfoWeaponSlot >= 0
                       && (this.AttackInfoAmmoCount == -1 || this.AttackInfoAmmoCount >= 0)
                       && this.AttackInfoHitTypeWire > 0
                       && this.AttackInfoWeaponInstance >= 0
                       && this.InitialDelayObservationsSeconds.Length > 0
                       && this.InitialDelayObservationsSeconds.All(IsFiniteNonNegative)
                       && this.LandedIntervalObservationsSeconds.Length > 0
                       && this.LandedIntervalObservationsSeconds.All(IsFinitePositive)
                       && this.DamageObservations.Length > 0
                       && this.DamageObservations.All(value => value != null && value.IsValid);
            }
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return value >= 0.0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0.0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class CapturedBasicCombatContractDefinition
    {
        internal CapturedBasicCombatContractDefinition(
            string cohortName,
            int playfieldId,
            int monsterData,
            int level,
            string aggregateAuditSha256,
            string[] sourceCaptures,
            int directCombatRows,
            int attackHits,
            int damageEvents,
            int ordinaryAttackInfoObservationCount,
            int ordinaryCadenceStreamCount,
            int ordinaryCadenceIntervalCount,
            bool usesGenericBasicMeleeSpatialPolicy,
            CapturedBasicCombatFieldAuthority damageObservationAuthority,
            CapturedBasicCombatFieldAuthority attackInfoPacketFieldAuthority,
            CapturedBasicCombatFieldAuthority initialDelayAuthority,
            CapturedBasicCombatFieldAuthority cadenceAuthority,
            CapturedBasicCombatFieldAuthority attackRangeAuthority,
            CapturedBasicCombatFieldAuthority spawnAttachmentAuthority,
            CapturedBasicCombatStreamDefinition[] streams)
        {
            this.CohortName = cohortName ?? string.Empty;
            this.PlayfieldId = playfieldId;
            this.MonsterData = monsterData;
            this.Level = level;
            this.AggregateAuditSha256 = aggregateAuditSha256 ?? string.Empty;
            this.SourceCaptures = sourceCaptures == null
                                      ? new string[0]
                                      : sourceCaptures.ToArray();
            this.DirectCombatRows = directCombatRows;
            this.AttackHits = attackHits;
            this.DamageEvents = damageEvents;
            this.OrdinaryAttackInfoObservationCount = ordinaryAttackInfoObservationCount;
            this.OrdinaryCadenceStreamCount = ordinaryCadenceStreamCount;
            this.OrdinaryCadenceIntervalCount = ordinaryCadenceIntervalCount;
            this.UsesGenericBasicMeleeSpatialPolicy = usesGenericBasicMeleeSpatialPolicy;
            this.DamageObservationAuthority = damageObservationAuthority;
            this.AttackInfoPacketFieldAuthority = attackInfoPacketFieldAuthority;
            this.InitialDelayAuthority = initialDelayAuthority;
            this.CadenceAuthority = cadenceAuthority;
            this.AttackRangeAuthority = attackRangeAuthority;
            this.SpawnAttachmentAuthority = spawnAttachmentAuthority;
            this.Streams = streams == null
                               ? new CapturedBasicCombatStreamDefinition[0]
                               : streams.ToArray();
        }

        internal string CohortName { get; private set; }

        internal int PlayfieldId { get; private set; }

        internal int MonsterData { get; private set; }

        internal int Level { get; private set; }

        internal string AggregateAuditSha256 { get; private set; }

        internal string[] SourceCaptures { get; private set; }

        internal int DirectCombatRows { get; private set; }

        internal int AttackHits { get; private set; }

        internal int DamageEvents { get; private set; }

        internal int OrdinaryAttackInfoObservationCount { get; private set; }

        internal int OrdinaryCadenceStreamCount { get; private set; }

        internal int OrdinaryCadenceIntervalCount { get; private set; }

        internal bool UsesGenericBasicMeleeSpatialPolicy { get; private set; }

        internal CapturedBasicCombatFieldAuthority DamageObservationAuthority { get; private set; }

        internal CapturedBasicCombatFieldAuthority AttackInfoPacketFieldAuthority { get; private set; }

        internal CapturedBasicCombatFieldAuthority InitialDelayAuthority { get; private set; }

        internal CapturedBasicCombatFieldAuthority CadenceAuthority { get; private set; }

        internal CapturedBasicCombatFieldAuthority AttackRangeAuthority { get; private set; }

        internal CapturedBasicCombatFieldAuthority SpawnAttachmentAuthority { get; private set; }

        internal CapturedBasicCombatStreamDefinition[] Streams { get; private set; }

        internal bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.CohortName)
                    || this.PlayfieldId <= 0
                    || this.MonsterData <= 0
                    || this.Level <= 0
                    || string.IsNullOrWhiteSpace(this.AggregateAuditSha256)
                    || this.SourceCaptures.Length == 0
                    || this.SourceCaptures.Any(string.IsNullOrWhiteSpace)
                    || this.Streams.Length == 0
                    || this.Streams.Any(stream => stream == null || !stream.IsValid))
                {
                    return false;
                }

                int observedDamageCount = this.Streams.Sum(
                    stream => stream.DamageObservations.Length);
                int observedCadenceStreamCount = this.Streams.Length;
                int observedCadenceIntervalCount = this.Streams.Sum(
                    stream => stream.LandedIntervalObservationsSeconds.Length);
                return this.DirectCombatRows > 0
                       && this.AttackHits >= this.DamageEvents
                       && this.DamageEvents == observedDamageCount
                       && this.OrdinaryAttackInfoObservationCount == observedDamageCount
                       && this.OrdinaryCadenceStreamCount == observedCadenceStreamCount
                       && this.OrdinaryCadenceIntervalCount == observedCadenceIntervalCount
                       && this.UsesGenericBasicMeleeSpatialPolicy
                       && this.DamageObservationAuthority == CapturedBasicCombatFieldAuthority.Captured
                       && this.AttackInfoPacketFieldAuthority == CapturedBasicCombatFieldAuthority.Captured
                       && this.InitialDelayAuthority == CapturedBasicCombatFieldAuthority.Captured
                       && this.CadenceAuthority == CapturedBasicCombatFieldAuthority.GovernedDerived
                       && this.AttackRangeAuthority == CapturedBasicCombatFieldAuthority.GenericRuntimePolicy
                       && this.SpawnAttachmentAuthority
                          == CapturedBasicCombatFieldAuthority.OptionalPositiveBehavior;
            }
        }

        internal string QuarantineReason
        {
            get
            {
                if (this.Streams == null || this.Streams.Length == 0)
                {
                    return "basic captured ordinary combat streams are missing";
                }

                if (this.Streams.Any(stream => stream == null || !stream.IsValid))
                {
                    return "basic captured ordinary combat stream evidence is incomplete";
                }

                if (!this.UsesGenericBasicMeleeSpatialPolicy
                    || this.AttackRangeAuthority
                       != CapturedBasicCombatFieldAuthority.GenericRuntimePolicy)
                {
                    return "basic captured ordinary combat is missing generic melee spatial policy authority";
                }

                return "basic captured ordinary combat contract is incomplete";
            }
        }
    }

    internal sealed partial class CapturedEnemyCombatContract
    {
        private CapturedEnemyCombatContract()
        {
        }

        internal string Evidence { get; private set; }

        internal bool Retaliates { get; private set; }

        internal NpcAiProfile AiProfile { get; private set; }

        internal CapturedEnemyAttackModel AttackModel { get; private set; }

        internal int EvidenceSourceIdentity { get; private set; }

        internal int EvidenceSourceIdentityHint { get; private set; }

        internal string EvidenceProfileSelectorHint { get; private set; }

        internal int? EvidenceSpecialAttackWeaponUnknown5Hint { get; private set; }

        internal double? EvidenceAttackStartDelaySecondsHint { get; private set; }

        internal bool HasCapturedRequiredPacketFields { get; private set; }

        internal bool UsesEquippedWeaponDamage { get; private set; }

        internal bool UsesEquippedWeaponTiming { get; private set; }

        internal bool UsesProductionWeaponQuality { get; private set; }

        internal bool UsesProductionSpecializedValues { get; private set; }

        internal bool UsesProductionEquippedWeaponValues { get; private set; }

        internal bool UsesProductionActorValuesForPresentationWeapon { get; private set; }

        internal bool UsesCaptureProvenArchetype { get; private set; }

        internal string CaptureProvenArchetypeId { get; private set; }

        internal int CapturedDamageBonus { get; private set; }

        internal double? CapturedAttackRange { get; private set; }

        internal int[] CapturedDamageObservations { get; private set; }

        internal double[] CapturedAttackStartDelayObservationsSeconds { get; private set; }

        internal double[] CapturedFirstHitDelayObservationsSeconds { get; private set; }

        internal double[] CapturedLandedIntervalObservationsSeconds { get; private set; }

        internal bool CapturedUsesEquippedWeapon { get; private set; }

        internal bool SendCapturedAttackInfo { get; private set; }

        internal bool HasCapturedFixedAttackBehavior { get; private set; }

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal int WeaponLowId { get; private set; }

        internal int WeaponHighId { get; private set; }

        internal int WeaponQuality { get; private set; }

        internal int WeaponInventorySlot { get; private set; }

        internal CapturedEnemyWeaponDefinition WeaponDefinition { get; private set; }

        internal bool HasEmptySpecialAttackWeaponContext { get; private set; }

        internal bool HasCapturedSpecialAttackWeaponContext { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] CapturedSpecialAttacks { get; private set; }

        internal bool HasCapturedAttackStartContext { get; private set; }

        internal bool HasCapturedEquippedAttackInfo { get; private set; }

        internal bool HasCapturedCombatStopSequence { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoHitType { get; private set; }

        internal byte AttackInfoN3Unknown { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal int[] CapturedSpecialAttackWeaponUnknown5Observations { get; private set; }

        internal double AttackStartDelaySeconds { get; private set; }

        internal double MovementTransitionDelaySeconds { get; private set; }

        internal double FirstHitDelaySeconds { get; private set; }

        internal bool SendStopFightOnDeath { get; private set; }

        internal bool RequiresDamageLineOfSight { get; private set; }

        internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; private set; }

        internal CapturedEnemyParallelAttackSequenceDefinition ParallelAttackSequence { get; private set; }

        internal CapturedBasicCombatContractDefinition BasicCombat { get; private set; }

        internal bool IsCombatReady
        {
            get
            {
                if (!this.Retaliates)
                {
                    return false;
                }

                switch (this.AttackModel)
                {
                    case CapturedEnemyAttackModel.FixedAttackInfo:
                        // Authored FixedAttackOnSight (mission/Arete/Lorelei): combat-ready without
                        // full corpus observations so mobs can retaliate with real AttackInfo.
                        if (this.IsAuthoredFixedAttackFallback())
                        {
                            return true;
                        }

                        return this.EvidenceSourceIdentity > 0
                               && this.HasCapturedRequiredPacketFields
                               && this.HasCapturedSpecialAttackWeaponContext
                               && this.HasCapturedAttackStartContext
                               && this.MinDamage > 0
                               && this.MaxDamage >= this.MinDamage
                               && this.RechargeSeconds > 0
                               && this.HasCompleteCapturedFixedRuntimeObservations()
                               && this.FixedAttackHasCompleteSource()
                               && (this.WeaponDefinition == null
                                   || (this.WeaponDefinition.IsValid
                                       && this.WeaponDefinition.EvidenceSourceIdentity
                                          == this.EvidenceSourceIdentity
                                       && this.AttackInfoAmmoMatchesCapturedEnergy()));
                    case CapturedEnemyAttackModel.EquippedWeapon:
                        return this.EvidenceSourceIdentity > 0
                               && this.HasCapturedRequiredPacketFields
                               && this.HasCapturedEquippedAttackInfo
                               && this.HasCapturedAttackStartContext
                               && this.WeaponDefinition != null
                               && this.WeaponDefinition.IsValid
                               && this.WeaponDefinition.EvidenceSourceIdentity
                               == this.EvidenceSourceIdentity
                               && this.WeaponLowId > 0
                               && this.WeaponHighId > 0
                               && this.WeaponQuality > 0
                               && this.WeaponInventorySlot > 0
                               && this.WeaponDefinition.LowId == this.WeaponLowId
                               && this.WeaponDefinition.HighId == this.WeaponHighId
                               && this.WeaponDefinition.Quality == this.WeaponQuality
                               && this.WeaponDefinition.InventorySlot == this.WeaponInventorySlot
                               && this.AttackInfoWeaponSlot == this.WeaponInventorySlot
                               && this.AttackInfoWeaponInstance == 0
                               && (this.UsesEquippedWeaponTiming
                                   || this.FirstHitDelaySeconds > 0)
                               && (this.UsesEquippedWeaponTiming
                                   || this.RechargeSeconds > 0)
                               && (this.UsesEquippedWeaponDamage
                                   || (this.MinDamage > 0
                                       && this.MaxDamage >= this.MinDamage))
                               && this.AttackInfoAmmoMatchesCapturedEnergy();
                    case CapturedEnemyAttackModel.Specialized:
                        return this.EvidenceSourceIdentity > 0
                               && (this.HasCompleteSpecialAttackSequence()
                                   || this.HasCompleteParallelAttackSequence());
                    case CapturedEnemyAttackModel.BasicCaptureBackedOrdinary:
                        return this.EvidenceSourceIdentity > 0
                               && this.BasicCombat != null
                               && this.BasicCombat.IsValid;
                    default:
                        return false;
                }
            }
        }

        internal bool IsQuarantined
        {
            get { return !this.IsCombatReady; }
        }

        internal string QuarantineReason
        {
            get
            {
                if (!this.Retaliates)
                {
                    return "retaliation is not capture-proven";
                }

                if (this.AttackModel == CapturedEnemyAttackModel.Unresolved)
                {
                    return "captured attack contract is unresolved";
                }

                if (this.AttackModel == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary)
                {
                    if (this.EvidenceSourceIdentity <= 0)
                    {
                        return "basic captured ordinary packet source identity is missing";
                    }

                    if (this.BasicCombat == null)
                    {
                        return "basic captured ordinary combat contract is missing";
                    }

                    if (!this.BasicCombat.IsValid)
                    {
                        return this.BasicCombat.QuarantineReason;
                    }

                    return "basic captured ordinary combat contract is incomplete";
                }

                if (this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                {
                    if (this.EvidenceSourceIdentity <= 0)
                    {
                        return "fixed packet source identity is missing";
                    }

                    if (!this.HasCapturedRequiredPacketFields)
                    {
                        return "fixed packet required fields are incomplete";
                    }

                    if (!this.HasCapturedSpecialAttackWeaponContext)
                    {
                        return "fixed packet SpecialAttackWeapon context is incomplete";
                    }

                    if (!this.HasCapturedAttackStartContext)
                    {
                        return "fixed packet Attack context is incomplete";
                    }

                    if (this.MinDamage <= 0 || this.MaxDamage < this.MinDamage)
                    {
                        return "fixed packet captured damage observations are invalid";
                    }

                    if (this.RechargeSeconds <= 0)
                    {
                        return "fixed packet captured landed interval is invalid";
                    }

                    if (!this.HasCompleteCapturedFixedRuntimeObservations())
                    {
                        return "fixed packet captured timing, damage, or attack-mode observations are incomplete";
                    }

                    if (!this.FixedAttackHasCompleteSource())
                    {
                        return "fixed packet attack source is incomplete";
                    }

                    if (this.WeaponDefinition != null
                        && (!this.WeaponDefinition.IsValid
                            || this.WeaponDefinition.EvidenceSourceIdentity
                               != this.EvidenceSourceIdentity
                            || !this.AttackInfoAmmoMatchesCapturedEnergy()))
                    {
                        return "fixed packet owner-linked weapon state is incomplete";
                    }

                    return "fixed packet contract is incomplete";
                }

                if (this.EvidenceSourceIdentity == 0)
                {
                    return "capture source identity is missing";
                }

                if (this.RequiresPhysicalWeaponDefinition() && this.WeaponDefinition == null)
                {
                    return "owner-linked WeaponItemFullUpdate evidence is missing";
                }

                if (this.WeaponDefinition != null && !this.WeaponDefinition.IsValid)
                {
                    return "owner-linked WeaponItemFullUpdate evidence is invalid";
                }

                return "captured attack packet context is incomplete";
            }
        }

        internal CapturedEnemyCombatContract WithCapturedWeapon(
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            this.WeaponDefinition = weaponDefinition;
            this.ApplyCapturedWeaponIdentity(weaponDefinition);
            return this;
        }

        internal CapturedEnemyCombatContract WithEvidenceSourceHint(int sourceIdentity)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.EvidenceSourceIdentityHint = sourceIdentity;
            return clone;
        }

        internal CapturedEnemyCombatContract WithEvidenceProfileSelectorHint(
            string profileSelector)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.EvidenceProfileSelectorHint = profileSelector ?? string.Empty;
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureProvenRetaliationEligibility(
            string eligibilityEvidence)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.Retaliates = true;
            clone.AiProfile = NpcAiProfile.Passive;
            if (!string.IsNullOrWhiteSpace(eligibilityEvidence)
                && (string.IsNullOrWhiteSpace(clone.Evidence)
                    || clone.Evidence.IndexOf(
                        eligibilityEvidence,
                        StringComparison.Ordinal) < 0))
            {
                clone.Evidence = string.IsNullOrWhiteSpace(clone.Evidence)
                                     ? eligibilityEvidence
                                     : clone.Evidence + "; " + eligibilityEvidence;
            }
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureProvenArchetype(
            string archetypeId)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesCaptureProvenArchetype = true;
            clone.CaptureProvenArchetypeId = archetypeId ?? string.Empty;
            if (!clone.CapturedAttackRange.HasValue)
            {
                clone.CapturedAttackRange = SharedParallelAttackRange(
                    clone.ParallelAttackSequence);
            }

            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedAttackRange(
            double capturedAttackRange)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.CapturedAttackRange = capturedAttackRange;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionWeaponQuality()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionWeaponQuality = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionSpecializedValues()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionSpecializedValues = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionEquippedWeaponValues()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionEquippedWeaponValues = true;
            clone.UsesEquippedWeaponDamage = true;
            clone.UsesEquippedWeaponTiming = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionActorValuesForPresentationWeapon()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionActorValuesForPresentationWeapon = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedSpecialAttackWeaponUnknown5Observations(
            int[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                return this;
            }

            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.CapturedSpecialAttackWeaponUnknown5Observations = observations.ToArray();
            clone.SpecialAttackWeaponUnknown5 = observations[0];
            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedSpecializedDamageObservations(
            int[][] capturedDamageObservationsByAttack,
            int?[] lethalAttackInfoUnknownByAttack)
        {
            if (this.AttackModel != CapturedEnemyAttackModel.Specialized
                || capturedDamageObservationsByAttack == null
                || lethalAttackInfoUnknownByAttack == null
                || lethalAttackInfoUnknownByAttack.Length
                   != capturedDamageObservationsByAttack.Length)
            {
                return null;
            }

            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            if (this.SpecialAttackSequence != null)
            {
                CapturedEnemySpecialAttackSequenceDefinition sequence = this.SpecialAttackSequence;
                int expectedAttackCount = sequence.OpeningAttack == null ? 1 : 2;
                if (capturedDamageObservationsByAttack.Length != expectedAttackCount)
                {
                    return null;
                }

                int observationIndex = 0;
                CapturedEnemyCombatAttackDefinition openingAttack = sequence.OpeningAttack == null
                                                                          ? null
                                                                          : sequence.OpeningAttack.WithCapturedDamageObservations(
                                                                              capturedDamageObservationsByAttack[observationIndex],
                                                                              lethalAttackInfoUnknownByAttack[observationIndex++]);
                CapturedEnemyCombatAttackDefinition repeatingAttack =
                    sequence.RepeatingAttack.WithCapturedDamageObservations(
                        capturedDamageObservationsByAttack[observationIndex],
                        lethalAttackInfoUnknownByAttack[observationIndex]);
                clone.SpecialAttackSequence = new CapturedEnemySpecialAttackSequenceDefinition(
                    sequence.InitialAttackDelaySeconds,
                    openingAttack,
                    repeatingAttack,
                    sequence.SpecialAttacks,
                    sequence.SpecialAttackWeaponUnknown1,
                    sequence.SpecialAttackWeaponUnknown2,
                    sequence.SpecialAttackWeaponUnknown3,
                    sequence.SpecialAttackWeaponUnknown4,
                    sequence.SpecialAttackWeaponUnknown5,
                    sequence.SpecialAttackWeaponN3Unknown,
                    sequence.AttackN3Unknown,
                    sequence.AttackAction);
                return clone;
            }

            if (this.ParallelAttackSequence == null
                || capturedDamageObservationsByAttack.Length
                   != this.ParallelAttackSequence.Streams.Length)
            {
                return null;
            }

            CapturedEnemyParallelAttackSequenceDefinition parallelSequence =
                this.ParallelAttackSequence;
            var enrichedStreams = new CapturedEnemyParallelAttackStreamDefinition[
                parallelSequence.Streams.Length];
            for (int index = 0; index < parallelSequence.Streams.Length; index++)
            {
                CapturedEnemyParallelAttackStreamDefinition stream = parallelSequence.Streams[index];
                enrichedStreams[index] = new CapturedEnemyParallelAttackStreamDefinition(
                    stream.InitialDelaySeconds,
                    stream.Attack.WithCapturedDamageObservations(
                        capturedDamageObservationsByAttack[index],
                        lethalAttackInfoUnknownByAttack[index]),
                    stream.Repeats);
            }

            clone.ParallelAttackSequence = new CapturedEnemyParallelAttackSequenceDefinition(
                enrichedStreams,
                parallelSequence.SpecialAttacks,
                parallelSequence.SpecialAttackWeaponUnknown1,
                parallelSequence.SpecialAttackWeaponUnknown2,
                parallelSequence.SpecialAttackWeaponUnknown3,
                parallelSequence.SpecialAttackWeaponUnknown4,
                parallelSequence.SpecialAttackWeaponUnknown5,
                parallelSequence.SpecialAttackWeaponN3Unknown,
                parallelSequence.AttackN3Unknown,
                parallelSequence.AttackAction,
                parallelSequence.AttackStartDelaySeconds);
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureCertification(
            string generatedEvidence,
            int evidenceSourceIdentity,
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.Evidence = string.IsNullOrWhiteSpace(generatedEvidence)
                                 ? this.Evidence
                                 : generatedEvidence;
            clone.EvidenceSourceIdentity = evidenceSourceIdentity;
            clone.WeaponDefinition = weaponDefinition;
            clone.ApplyCapturedWeaponIdentity(weaponDefinition);
            return clone;
        }


        internal bool RequiresPhysicalWeaponPresentation
        {
            get { return this.RequiresPhysicalWeaponDefinition(); }
        }

        private bool AttackInfoAmmoMatchesCapturedEnergy()
        {
            if (this.WeaponDefinition == null)
            {
                return false;
            }

            int energy = this.WeaponDefinition.InitialEnergy;
            return energy == -1
                       ? this.AttackInfoAmmoCount == -1
                       : energy == 0
                             ? this.AttackInfoAmmoCount == 0
                             : energy > 0 && this.AttackInfoAmmoCount == energy - 1;
        }

        private bool HasCompleteCapturedFixedRuntimeObservations()
        {
            return this.HasCapturedFixedAttackBehavior
                   && this.SendCapturedAttackInfo
                   && this.CapturedDamageObservations != null
                   && this.CapturedDamageObservations.Length > 0
                   && this.CapturedDamageObservations.All(value => value > 0)
                   && this.CapturedDamageObservations.Min() == this.MinDamage
                   && this.CapturedDamageObservations.Max() == this.MaxDamage
                   && this.CapturedAttackStartDelayObservationsSeconds != null
                   && this.CapturedAttackStartDelayObservationsSeconds.Length > 0
                   && this.CapturedAttackStartDelayObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value >= 0.0d)
                   && this.CapturedFirstHitDelayObservationsSeconds != null
                   && this.CapturedFirstHitDelayObservationsSeconds.Length > 0
                   && this.CapturedFirstHitDelayObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value >= 0.0d)
                   && this.CapturedAttackStartDelayObservationsSeconds.Length
                      == this.CapturedFirstHitDelayObservationsSeconds.Length
                   && this.CapturedLandedIntervalObservationsSeconds != null
                   && this.CapturedLandedIntervalObservationsSeconds.Length > 0
                   && this.CapturedLandedIntervalObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value > 0.0d)
                   && (this.CapturedUsesEquippedWeapon
                       || this.HasExplicitCapturedAttackRange())
                   && Math.Abs(
                       this.AttackStartDelaySeconds
                       - this.CapturedAttackStartDelayObservationsSeconds[0]) < 0.000001d
                   && Math.Abs(
                       this.FirstHitDelaySeconds
                       - this.CapturedFirstHitDelayObservationsSeconds[0]) < 0.000001d
                   && Math.Abs(
                       this.RechargeSeconds
                       - this.CapturedLandedIntervalObservationsSeconds[0]) < 0.000001d;
        }

        /// <summary>
        /// Production FixedAttackOnSight: damage + attack-start without corpus WIFU observations.
        /// </summary>
        internal bool IsAuthoredFixedAttackFallback()
        {
            return this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo
                   && this.Retaliates
                   && this.MinDamage > 0
                   && this.MaxDamage >= this.MinDamage
                   && this.RechargeSeconds > 0
                   && this.HasCapturedAttackStartContext
                   && this.HasCapturedSpecialAttackWeaponContext
                   && this.EvidenceSourceIdentity <= 0
                   && !this.HasCapturedRequiredPacketFields;
        }

        private bool HasExplicitCapturedAttackRange()
        {
            return this.CapturedAttackRange.HasValue
                   && this.CapturedAttackRange.Value > 0.0d
                   && !double.IsNaN(this.CapturedAttackRange.Value)
                   && !double.IsInfinity(this.CapturedAttackRange.Value);
        }

        private void ApplyCapturedWeaponIdentity(CapturedEnemyWeaponDefinition weaponDefinition)
        {
            if (weaponDefinition == null)
            {
                return;
            }

            this.WeaponLowId = weaponDefinition.LowId;
            this.WeaponHighId = weaponDefinition.HighId;
            this.WeaponQuality = weaponDefinition.Quality;
            this.WeaponInventorySlot = weaponDefinition.InventorySlot;
        }

        private bool HasCompleteSpecialAttackSequence()
        {
            if (this.SpecialAttackSequence == null || !this.SpecialAttackSequence.IsValid)
            {
                return false;
            }

            return this.AttackHasCompleteSource(
                       this.SpecialAttackSequence.OpeningAttack,
                       this.SpecialAttackSequence.SpecialAttacks)
                   && this.AttackHasCompleteSource(
                       this.SpecialAttackSequence.RepeatingAttack,
                       this.SpecialAttackSequence.SpecialAttacks);
        }

        private bool FixedAttackHasCompleteSource()
        {
            if (this.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                && this.AttackInfoWeaponInstance == 0)
            {
                return this.WeaponDefinition != null
                       && this.WeaponDefinition.IsValid
                       && this.WeaponDefinition.InventorySlot == this.AttackInfoWeaponSlot;
            }

            if (this.AttackInfoWeaponInstance == 0)
            {
                return this.AttackInfoWeaponSlot == 0;
            }

            return this.CapturedSpecialAttacks != null
                   && this.CapturedSpecialAttacks.Any(
                       value => value != null && value.Tag == this.AttackInfoWeaponInstance);
        }

        private bool HasCompleteParallelAttackSequence()
        {
            if (this.ParallelAttackSequence == null || !this.ParallelAttackSequence.IsValid)
            {
                return false;
            }

            return this.ParallelAttackSequence.Streams.All(
                stream => this.AttackHasCompleteSource(
                    stream.Attack,
                    this.ParallelAttackSequence.SpecialAttacks));
        }

        private bool AttackHasCompleteSource(
            CapturedEnemyCombatAttackDefinition attack,
            CapturedEnemySpecialAttackDefinition[] specials)
        {
            if (attack == null)
            {
                return true;
            }

            if (attack.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                && attack.AttackInfoWeaponInstance == 0)
            {
                return this.WeaponDefinition != null
                       && this.WeaponDefinition.IsValid
                       && this.WeaponDefinition.InventorySlot == attack.AttackInfoWeaponSlot;
            }

            if (attack.AttackInfoWeaponInstance == 0)
            {
                return attack.AttackInfoWeaponSlot == 0;
            }

            return specials != null
                   && specials.Any(value => value != null && value.Tag == attack.AttackInfoWeaponInstance);
        }

        private bool RequiresPhysicalWeaponDefinition()
        {
            if (this.AttackModel == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary)
            {
                return false;
            }

            if (this.AttackModel == CapturedEnemyAttackModel.EquippedWeapon)
            {
                return true;
            }

            if (this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            {
                return this.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                       && this.AttackInfoWeaponInstance == 0;
            }

            if (this.SpecialAttackSequence != null)
            {
                return (this.SpecialAttackSequence.OpeningAttack != null
                        && this.SpecialAttackSequence.OpeningAttack.AttackInfoWeaponSlot
                        == (int)WeaponSlots.Righthand
                        && this.SpecialAttackSequence.OpeningAttack.AttackInfoWeaponInstance == 0)
                       || (this.SpecialAttackSequence.RepeatingAttack != null
                           && this.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot
                           == (int)WeaponSlots.Righthand
                           && this.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance == 0);
            }

            return this.ParallelAttackSequence != null
                   && this.ParallelAttackSequence.Streams.Any(
                       stream => stream.Attack.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                                 && stream.Attack.AttackInfoWeaponInstance == 0);
        }

        internal static CapturedEnemyCombatContract FixedAttack(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance,
            int attackInfoAmmoCount,
            int attackInfoHitType,
            byte attackInfoN3Unknown)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown
            };
        }

        /// <summary>
        /// Fixed damage + attack-on-sight (mission interiors).
        /// Enables AttackMessage start context so the client plays a real melee swing,
        /// and uses unarmed AttackInfo tags (not zeros) so hits are not "UNKNOWN damage".
        /// </summary>
        internal static CapturedEnemyCombatContract FixedAttackOnSight(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance,
            int attackInfoAmmoCount,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Aggressive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown,
                SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown,
                AttackN3Unknown = attackN3Unknown,
                AttackAction = attackAction,
                HasCapturedAttackStartContext = true,
                HasEmptySpecialAttackWeaponContext = true,
                HasCapturedSpecialAttackWeaponContext = true,
                SendCapturedAttackInfo = true,
                CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0]
            };
        }

        internal static CapturedEnemyCombatContract CapturedFixedPacketSequence(
            string evidence,
            int evidenceSourceIdentity,
            NpcAiProfile aiProfile,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            byte specialAttackWeaponN3Unknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte attackN3Unknown,
            byte attackAction,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoDamageTypeWire,
            int attackInfoHitTypeWire,
            int attackInfoWeaponInstance,
            byte attackInfoN3Unknown,
            bool requiresDamageLineOfSight,
            int[] capturedDamageObservations = null,
            double[] capturedAttackStartDelayObservationsSeconds = null,
            double[] capturedFirstHitDelayObservationsSeconds = null,
            double[] capturedLandedIntervalObservationsSeconds = null,
            int? capturedDamageBonus = null,
            bool? capturedUsesEquippedWeapon = null,
            double? capturedAttackRange = null,
            bool? capturedSendAttackInfo = null)
        {
            int[] damageObservations = capturedDamageObservations == null
                                           ? new int[0]
                                           : capturedDamageObservations.ToArray();
            double[] attackStartDelayObservations =
                capturedAttackStartDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedAttackStartDelayObservationsSeconds.ToArray();
            double[] firstHitDelayObservations =
                capturedFirstHitDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedFirstHitDelayObservationsSeconds.ToArray();
            double[] landedIntervalObservations =
                capturedLandedIntervalObservationsSeconds == null
                    ? new double[0]
                    : capturedLandedIntervalObservationsSeconds.ToArray();
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                EvidenceSourceIdentity = evidenceSourceIdentity,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                CapturedDamageObservations = damageObservations,
                CapturedAttackStartDelayObservationsSeconds = attackStartDelayObservations,
                CapturedFirstHitDelayObservationsSeconds = firstHitDelayObservations,
                CapturedLandedIntervalObservationsSeconds = landedIntervalObservations,
                AttackStartDelaySeconds = attackStartDelayObservations.Length == 0
                                              ? 0.0d
                                              : attackStartDelayObservations[0],
                FirstHitDelaySeconds = firstHitDelayObservations.Length == 0
                                           ? 0.0d
                                           : firstHitDelayObservations[0],
                CapturedDamageBonus = capturedDamageBonus ?? 0,
                CapturedUsesEquippedWeapon = capturedUsesEquippedWeapon ?? false,
                CapturedAttackRange = capturedAttackRange,
                SendCapturedAttackInfo = capturedSendAttackInfo ?? false,
                HasCapturedFixedAttackBehavior = capturedDamageBonus.HasValue
                                                 && capturedUsesEquippedWeapon.HasValue
                                                 && capturedSendAttackInfo.HasValue,
                CapturedSpecialAttacks = specialAttacks
                                           ?? new CapturedEnemySpecialAttackDefinition[0],
                HasCapturedRequiredPacketFields = true,
                HasCapturedSpecialAttackWeaponContext = true,
                HasEmptySpecialAttackWeaponContext = specialAttacks == null
                                                     || specialAttacks.Length == 0,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5,
                AttackN3Unknown = attackN3Unknown,
                AttackAction = attackAction,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = attackInfoWeaponSlot,
                AttackInfoUnknown = attackInfoDamageTypeWire,
                AttackInfoHitType = attackInfoHitTypeWire,
                AttackInfoWeaponInstance = attackInfoWeaponInstance,
                AttackInfoN3Unknown = attackInfoN3Unknown,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeapon(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                WeaponLowId = lowId,
                WeaponHighId = highId,
                WeaponQuality = quality,
                WeaponInventorySlot = inventorySlot,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedAttackInfo(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoWeaponInstance,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasCapturedEquippedAttackInfo = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedPacketSequence(
            string evidence,
            int evidenceSourceIdentity,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            bool usesEquippedWeaponDamage,
            int minDamage,
            int maxDamage,
            int damageBonus,
            double? attackRange,
            double attackStartDelaySeconds,
            double movementTransitionDelaySeconds,
            double firstHitDelaySeconds,
            double rechargeSeconds,
            bool hasCapturedCombatStopSequence,
            bool sendStopFightOnDeath,
            int attackInfoAmmoCount,
            int attackInfoUnknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            bool requiresDamageLineOfSight = false,
            bool usesEquippedWeaponTiming = false,
            NpcAiProfile aiProfile = NpcAiProfile.Passive)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.EvidenceSourceIdentity = evidenceSourceIdentity;
            contract.HasCapturedRequiredPacketFields = true;
            contract.UsesEquippedWeaponDamage = usesEquippedWeaponDamage;
            contract.UsesEquippedWeaponTiming = usesEquippedWeaponTiming;
            contract.AiProfile = aiProfile;
            contract.MinDamage = minDamage;
            contract.MaxDamage = maxDamage;
            contract.CapturedDamageBonus = damageBonus;
            contract.CapturedAttackRange = attackRange;
            contract.HasEmptySpecialAttackWeaponContext = true;
            contract.HasCapturedSpecialAttackWeaponContext = true;
            contract.CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0];
            contract.HasCapturedAttackStartContext = true;
            contract.HasCapturedEquippedAttackInfo = true;
            contract.HasCapturedCombatStopSequence = hasCapturedCombatStopSequence;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = inventorySlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = 0;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            contract.AttackN3Unknown = attackN3Unknown;
            contract.AttackAction = attackAction;
            contract.AttackStartDelaySeconds = attackStartDelaySeconds;
            contract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
            contract.FirstHitDelaySeconds = firstHitDelaySeconds;
            contract.RechargeSeconds = rechargeSeconds;
            contract.SendStopFightOnDeath = sendStopFightOnDeath;
            contract.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            contract.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            contract.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            contract.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            contract.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithEmptySpecialAttackContext(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int minDamage,
            int maxDamage,
            double attackStartDelaySeconds,
            double movementTransitionDelaySeconds,
            double firstHitDelaySeconds,
            double rechargeSeconds,
            bool sendStopFightOnDeath,
            int attackInfoAmmoCount,
            int attackInfoUnknown,
            int unknown1,
            int unknown2,
            int unknown3,
            int unknown4,
            int unknown5,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasEmptySpecialAttackWeaponContext = true;
            contract.HasCapturedSpecialAttackWeaponContext = true;
            contract.CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0];
            contract.HasCapturedAttackStartContext = true;
            contract.HasCapturedEquippedAttackInfo = true;
            contract.HasCapturedCombatStopSequence = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = inventorySlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = 0;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            contract.AttackN3Unknown = attackN3Unknown;
            contract.AttackAction = attackAction;
            contract.MinDamage = minDamage;
            contract.MaxDamage = maxDamage;
            contract.AttackStartDelaySeconds = attackStartDelaySeconds;
            contract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
            contract.FirstHitDelaySeconds = firstHitDelaySeconds;
            contract.RechargeSeconds = rechargeSeconds;
            contract.SendStopFightOnDeath = sendStopFightOnDeath;
            contract.SpecialAttackWeaponUnknown1 = unknown1;
            contract.SpecialAttackWeaponUnknown2 = unknown2;
            contract.SpecialAttackWeaponUnknown3 = unknown3;
            contract.SpecialAttackWeaponUnknown4 = unknown4;
            contract.SpecialAttackWeaponUnknown5 = unknown5;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract CapturedSpecialSequence(
            string evidence,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                SpecialAttackSequence = specialAttackSequence,
                HasEmptySpecialAttackWeaponContext =
                    specialAttackSequence.SpecialAttacks.Length == 0,
                HasCapturedSpecialAttackWeaponContext = true,
                CapturedSpecialAttacks = specialAttackSequence.SpecialAttacks,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown =
                    specialAttackSequence.SpecialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 =
                    specialAttackSequence.SpecialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 =
                    specialAttackSequence.SpecialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 =
                    specialAttackSequence.SpecialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 =
                    specialAttackSequence.SpecialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 =
                    specialAttackSequence.SpecialAttackWeaponUnknown5,
                AttackN3Unknown = specialAttackSequence.AttackN3Unknown,
                AttackAction = specialAttackSequence.AttackAction
            };
        }

        internal static CapturedEnemyCombatContract CapturedParallelAttackSequence(
            string evidence,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence,
            bool requiresDamageLineOfSight = false,
            NpcAiProfile aiProfile = NpcAiProfile.Passive)
        {
            double? capturedAttackRange = SharedParallelAttackRange(parallelAttackSequence);
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                ParallelAttackSequence = parallelAttackSequence,
                RequiresDamageLineOfSight = requiresDamageLineOfSight,
                HasEmptySpecialAttackWeaponContext =
                    parallelAttackSequence.SpecialAttacks.Length == 0,
                HasCapturedSpecialAttackWeaponContext = true,
                CapturedSpecialAttacks = parallelAttackSequence.SpecialAttacks,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown =
                    parallelAttackSequence.SpecialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown5,
                AttackN3Unknown = parallelAttackSequence.AttackN3Unknown,
                AttackAction = parallelAttackSequence.AttackAction,
                CapturedAttackRange = capturedAttackRange
            };
        }

        private static double? SharedParallelAttackRange(
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence)
        {
            if (parallelAttackSequence == null || parallelAttackSequence.Streams == null)
            {
                return null;
            }

            double[] ranges = parallelAttackSequence.Streams
                .Where(stream => stream != null && stream.Attack != null)
                .Select(stream => stream.Attack.Range)
                .ToArray();
            if (ranges.Length == 0
                || ranges.Any(value => value <= 0.0d || double.IsNaN(value) || double.IsInfinity(value))
                || ranges.Any(value => Math.Abs(value - ranges[0]) > 0.000001d))
            {
                return null;
            }

            return ranges[0];
        }

        internal static CapturedEnemyCombatContract CapturedBasicOrdinaryCombat(
            string evidence,
            int evidenceSourceIdentity,
            CapturedBasicCombatContractDefinition basicCombat,
            bool requiresDamageLineOfSight = false,
            NpcAiProfile aiProfile = NpcAiProfile.Passive)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence ?? string.Empty,
                EvidenceSourceIdentity = evidenceSourceIdentity,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.BasicCaptureBackedOrdinary,
                BasicCombat = basicCombat,
                HasCapturedRequiredPacketFields = true,
                SendCapturedAttackInfo = true,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract CapturedProfileSelector(
            string evidence,
            int evidenceSourceIdentityHint,
            string profileSelectorHint,
            NpcAiProfile aiProfile,
            double? capturedAttackRange,
            int? specialAttackWeaponUnknown5,
            double? attackStartDelaySeconds,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence ?? string.Empty,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.Unresolved,
                EvidenceSourceIdentityHint = evidenceSourceIdentityHint,
                EvidenceProfileSelectorHint = profileSelectorHint ?? string.Empty,
                EvidenceSpecialAttackWeaponUnknown5Hint = specialAttackWeaponUnknown5,
                EvidenceAttackStartDelaySecondsHint = attackStartDelaySeconds,
                CapturedAttackRange = capturedAttackRange,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract Unresolved(string evidence, bool retaliationObserved)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = retaliationObserved,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Unresolved
            };
        }
    }

}
