namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    internal sealed class CapturedEnemyCombatProfileStreamDefinition
    {
        internal CapturedEnemyCombatProfileStreamDefinition(
            int minimumObservedDamage,
            int maximumObservedDamage,
            int initialAmmoCount,
            int weaponSlot,
            int damageTypeWire,
            int hitTypeWire,
            int weaponInstance,
            byte n3Unknown,
            double observedRechargeSeconds,
            int[] capturedDamageObservations = null,
            double[] capturedAttackStartDelayObservationsSeconds = null,
            double[] capturedFirstHitDelayObservationsSeconds = null,
            double[] capturedLandedIntervalObservationsSeconds = null,
            int? capturedDamageBonus = null,
            bool? capturedUsesEquippedWeapon = null,
            double? capturedAttackRange = null,
            bool? capturedSendAttackInfo = null)
        {
            this.MinimumObservedDamage = minimumObservedDamage;
            this.MaximumObservedDamage = maximumObservedDamage;
            this.InitialAmmoCount = initialAmmoCount;
            this.WeaponSlot = weaponSlot;
            this.DamageTypeWire = damageTypeWire;
            this.HitTypeWire = hitTypeWire;
            this.WeaponInstance = weaponInstance;
            this.N3Unknown = n3Unknown;
            this.ObservedRechargeSeconds = observedRechargeSeconds;
            this.CapturedDamageObservations = capturedDamageObservations == null
                                                  ? new int[0]
                                                  : capturedDamageObservations.ToArray();
            this.CapturedAttackStartDelayObservationsSeconds =
                capturedAttackStartDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedAttackStartDelayObservationsSeconds.ToArray();
            this.CapturedFirstHitDelayObservationsSeconds =
                capturedFirstHitDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedFirstHitDelayObservationsSeconds.ToArray();
            this.CapturedLandedIntervalObservationsSeconds =
                capturedLandedIntervalObservationsSeconds == null
                    ? new double[0]
                    : capturedLandedIntervalObservationsSeconds.ToArray();
            this.CapturedDamageBonus = capturedDamageBonus;
            this.CapturedUsesEquippedWeapon = capturedUsesEquippedWeapon;
            this.CapturedAttackRange = capturedAttackRange;
            this.CapturedSendAttackInfo = capturedSendAttackInfo;
        }

        internal int MinimumObservedDamage { get; private set; }
        internal int MaximumObservedDamage { get; private set; }
        internal int InitialAmmoCount { get; private set; }
        internal int WeaponSlot { get; private set; }
        internal int DamageTypeWire { get; private set; }
        internal int HitTypeWire { get; private set; }
        internal int WeaponInstance { get; private set; }
        internal byte N3Unknown { get; private set; }
        internal double ObservedRechargeSeconds { get; private set; }
        internal int[] CapturedDamageObservations { get; private set; }
        internal double[] CapturedAttackStartDelayObservationsSeconds { get; private set; }
        internal double[] CapturedFirstHitDelayObservationsSeconds { get; private set; }
        internal double[] CapturedLandedIntervalObservationsSeconds { get; private set; }
        internal int? CapturedDamageBonus { get; private set; }
        internal bool? CapturedUsesEquippedWeapon { get; private set; }
        internal double? CapturedAttackRange { get; private set; }
        internal bool? CapturedSendAttackInfo { get; private set; }

        internal bool HasCompleteFixedRuntimeEvidence
        {
            get
            {
                return this.CapturedDamageObservations.Length > 0
                       && this.CapturedDamageObservations.All(value => value > 0)
                       && this.CapturedDamageObservations.Min() == this.MinimumObservedDamage
                       && this.CapturedDamageObservations.Max() == this.MaximumObservedDamage
                       && this.CapturedAttackStartDelayObservationsSeconds.Length > 0
                       && this.CapturedAttackStartDelayObservationsSeconds.All(IsValidDelay)
                       && this.CapturedFirstHitDelayObservationsSeconds.Length > 0
                       && this.CapturedFirstHitDelayObservationsSeconds.All(IsValidDelay)
                       && this.CapturedAttackStartDelayObservationsSeconds.Length
                          == this.CapturedFirstHitDelayObservationsSeconds.Length
                       && this.CapturedDamageBonus.HasValue
                       && this.CapturedUsesEquippedWeapon.HasValue
                       && this.CapturedSendAttackInfo.HasValue
                       && this.CapturedSendAttackInfo.Value;
            }
        }

        internal bool Matches(
            CapturedEnemyCombatAttackDefinition attack,
            double attackStartDelaySeconds,
            double firstHitDelaySeconds,
            double[] landedIntervalObservationsSeconds,
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            if (attack == null
                || !this.HasCompleteFixedRuntimeEvidence)
            {
                return false;
            }

            landedIntervalObservationsSeconds = landedIntervalObservationsSeconds
                                                   ?? new double[0];
            if (landedIntervalObservationsSeconds.Length == 0
                || !landedIntervalObservationsSeconds.All(IsValidInterval))
            {
                return false;
            }

            bool fixedRechargeMatchesCapturedObservation =
                landedIntervalObservationsSeconds.Any(
                    value => NearlyEqual(value, attack.RechargeSeconds));
            bool fixedAttackStartDelayMatchesCapturedObservation =
                this.CapturedAttackStartDelayObservationsSeconds.Any(
                    value => NearlyEqual(value, attackStartDelaySeconds));
            bool fixedFirstHitDelayMatchesCapturedObservation =
                this.CapturedFirstHitDelayObservationsSeconds.Any(
                    value => NearlyEqual(value, firstHitDelaySeconds));
            return fixedRechargeMatchesCapturedObservation
                   && fixedAttackStartDelayMatchesCapturedObservation
                   && fixedFirstHitDelayMatchesCapturedObservation
                   && attack.MinDamage == this.MinimumObservedDamage
                   && attack.MaxDamage == this.MaximumObservedDamage
                   && attack.DamageBonus == this.CapturedDamageBonus.Value
                   && (!this.CapturedAttackRange.HasValue
                       || NearlyEqual(attack.Range, this.CapturedAttackRange.Value))
                   && attack.UsesEquippedWeapon == this.CapturedUsesEquippedWeapon.Value
                   && attack.SendAttackInfo == this.CapturedSendAttackInfo.Value
                   && attack.AttackInfoAmmoCount == this.InitialAmmoCount
                   && attack.AttackInfoWeaponSlot == this.WeaponSlot
                   && attack.AttackInfoUnknown == this.DamageTypeWire
                   && attack.AttackInfoHitType == this.HitTypeWire
                   && attack.AttackInfoWeaponInstance == this.WeaponInstance
                   && attack.AttackInfoN3Unknown == this.N3Unknown;
        }

        private static bool IsValidDelay(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0d;
        }

        private static bool IsValidInterval(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0d;
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.000001d;
        }
    }

    internal sealed class CapturedEnemyCombatProfileDefinition
    {
        internal CapturedEnemyCombatProfileDefinition(
            string profileId,
            string evidence,
            int resourceId,
            string name,
            int monsterData,
            int level,
            bool semanticFallbackCaptureProven,
            bool captureEvidenceSafe,
            bool deterministicRuntimeInitializationProven,
            int[] sourceIdentities,
            int representativeEvidenceSourceIdentity,
            CapturedEnemyWeaponDefinition weaponDefinition,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            byte specialAttackWeaponN3Unknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte attackN3Unknown,
            byte attackAction,
            CapturedEnemyCombatProfileStreamDefinition[] streams,
            int[] specialAttackWeaponUnknown5Observations = null)
        {
            this.ProfileId = profileId ?? string.Empty;
            this.Evidence = evidence ?? string.Empty;
            this.ResourceId = resourceId;
            this.Name = name ?? string.Empty;
            this.MonsterData = monsterData;
            this.Level = level;
            this.SemanticFallbackCaptureProven = semanticFallbackCaptureProven;
            this.CaptureEvidenceSafe = captureEvidenceSafe;
            this.DeterministicRuntimeInitializationProven =
                deterministicRuntimeInitializationProven;
            this.SourceIdentities = sourceIdentities ?? new int[0];
            this.RepresentativeEvidenceSourceIdentity = representativeEvidenceSourceIdentity;
            this.WeaponDefinition = weaponDefinition;
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponUnknown5Observations =
                specialAttackWeaponUnknown5Observations == null
                    ? new[] { specialAttackWeaponUnknown5 }
                    : specialAttackWeaponUnknown5Observations.ToArray();
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
            this.Streams = streams ?? new CapturedEnemyCombatProfileStreamDefinition[0];
        }

        internal string ProfileId { get; private set; }
        internal string Evidence { get; private set; }
        internal int ResourceId { get; private set; }
        internal string Name { get; private set; }
        internal int MonsterData { get; private set; }
        internal int Level { get; private set; }
        internal bool SemanticFallbackCaptureProven { get; private set; }
        internal bool CaptureEvidenceSafe { get; private set; }
        internal bool DeterministicRuntimeInitializationProven { get; private set; }
        internal bool CaptureRuntimeEvidenceSafe
        {
            get
            {
                return this.CaptureEvidenceSafe
                       && (this.DeterministicRuntimeInitializationProven
                           || this.HasCapturedOrderedSpecialAttackWeaponState);
            }
        }
        internal int[] SourceIdentities { get; private set; }
        internal int RepresentativeEvidenceSourceIdentity { get; private set; }
        internal CapturedEnemyWeaponDefinition WeaponDefinition { get; private set; }
        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }
        internal byte SpecialAttackWeaponN3Unknown { get; private set; }
        internal int SpecialAttackWeaponUnknown1 { get; private set; }
        internal int SpecialAttackWeaponUnknown2 { get; private set; }
        internal int SpecialAttackWeaponUnknown3 { get; private set; }
        internal int SpecialAttackWeaponUnknown4 { get; private set; }
        internal int SpecialAttackWeaponUnknown5 { get; private set; }
        internal int[] SpecialAttackWeaponUnknown5Observations { get; private set; }
        internal bool HasCapturedOrderedSpecialAttackWeaponState
        {
            get
            {
                return this.SpecialAttackWeaponUnknown5Observations.Length > 1
                       && this.SpecialAttackWeaponUnknown5Observations[0]
                          == this.SpecialAttackWeaponUnknown5;
            }
        }
        internal byte AttackN3Unknown { get; private set; }
        internal byte AttackAction { get; private set; }
        internal CapturedEnemyCombatProfileStreamDefinition[] Streams { get; private set; }

        internal bool MatchesKey(int resourceId, string name, int monsterData, int level)
        {
            return this.ResourceId == resourceId
                   && this.MonsterData == monsterData
                   && this.Level == level
                   && string.Equals(this.Name, name, StringComparison.Ordinal);
        }

        internal bool MatchesArchetypeKey(int resourceId, string name, int monsterData)
        {
            return this.ResourceId == resourceId
                   && this.MonsterData == monsterData
                   && string.Equals(this.Name, name, StringComparison.Ordinal);
        }

        internal bool SupportsCaptureProvenEquippedWeaponArchetype
        {
            get
            {
                return this.SemanticFallbackCaptureProven
                       && this.CaptureRuntimeEvidenceSafe
                       && this.WeaponDefinition != null
                       && this.WeaponDefinition.IsValid
                       && this.SpecialAttacks.Length == 0
                       && this.Streams.Length == 1
                       && this.Streams[0].HasCompleteFixedRuntimeEvidence
                       && this.Streams[0].CapturedUsesEquippedWeapon == true
                       && this.Streams[0].CapturedSendAttackInfo == true
                       && this.Streams[0].WeaponSlot == this.WeaponDefinition.InventorySlot
                       && this.Streams[0].WeaponInstance == 0;
            }
        }

        internal bool MatchesCaptureProvenEquippedWeaponArchetype(
            CapturedEnemyCombatProfileDefinition other)
        {
            if (!this.SupportsCaptureProvenEquippedWeaponArchetype
                || other == null
                || !other.SupportsCaptureProvenEquippedWeaponArchetype
                || this.SpecialAttackWeaponN3Unknown != other.SpecialAttackWeaponN3Unknown
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !this.SpecialAttackWeaponUnknown5Observations.SequenceEqual(
                    other.SpecialAttackWeaponUnknown5Observations)
                || !WeaponSemanticsMatch(
                    this.WeaponDefinition,
                    other.WeaponDefinition))
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition left = this.Streams[0];
            CapturedEnemyCombatProfileStreamDefinition right = other.Streams[0];
            return left.InitialAmmoCount == right.InitialAmmoCount
                   && left.WeaponSlot == right.WeaponSlot
                   && left.DamageTypeWire == right.DamageTypeWire
                   && left.HitTypeWire == right.HitTypeWire
                   && left.WeaponInstance == right.WeaponInstance
                   && left.N3Unknown == right.N3Unknown
                   && left.CapturedUsesEquippedWeapon == right.CapturedUsesEquippedWeapon
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo
                   && NullableDoubleEquals(
                       left.CapturedAttackRange,
                       right.CapturedAttackRange);
        }

        internal bool ContainsSource(int sourceIdentity)
        {
            return Array.IndexOf(this.SourceIdentities, sourceIdentity) >= 0;
        }

        internal bool MatchesStableWeaponProfile(CapturedEnemyCombatContract contract)
        {
            return contract != null
                   && this.WeaponDefinition != null
                   && contract.WeaponLowId > 0
                   && contract.WeaponHighId > 0
                   && contract.WeaponQuality > 0
                   && contract.WeaponInventorySlot > 0
                   && this.WeaponDefinition.LowId == contract.WeaponLowId
                   && this.WeaponDefinition.HighId == contract.WeaponHighId
                   && this.WeaponDefinition.Quality == contract.WeaponQuality
                   && this.WeaponDefinition.InventorySlot == contract.WeaponInventorySlot;
        }

        internal bool MatchesSpecialized(CapturedEnemyCombatContract contract)
        {
            int[][] ignoredObservations;
            return this.TryMatchSpecialized(contract, out ignoredObservations);
        }

        internal bool TryEnrichSpecialized(
            CapturedEnemyCombatContract contract,
            out CapturedEnemyCombatContract enriched)
        {
            enriched = null;
            int[][] capturedDamageObservationsByAttack;
            if (!this.TryMatchSpecialized(
                    contract,
                    out capturedDamageObservationsByAttack))
            {
                return false;
            }

            enriched = contract.WithCapturedSpecializedDamageObservations(
                capturedDamageObservationsByAttack);
            return enriched != null;
        }

        private bool TryMatchSpecialized(
            CapturedEnemyCombatContract contract,
            out int[][] capturedDamageObservationsByAttack)
        {
            capturedDamageObservationsByAttack = null;
            if (!this.CaptureRuntimeEvidenceSafe
                || contract == null
                || contract.AttackModel != CapturedEnemyAttackModel.Specialized)
            {
                return false;
            }

            CapturedEnemySpecialAttackDefinition[] currentSpecials;
            byte sawUnknown;
            int saw1;
            int saw2;
            int saw3;
            int saw4;
            int saw5;
            byte attackUnknown;
            byte attackAction;
            var attacks = new List<CapturedEnemyCombatAttackDefinition>();
            var firstHitDelays = new List<double>();
            if (contract.SpecialAttackSequence != null)
            {
                CapturedEnemySpecialAttackSequenceDefinition sequence = contract.SpecialAttackSequence;
                currentSpecials = sequence.SpecialAttacks;
                sawUnknown = sequence.SpecialAttackWeaponN3Unknown;
                saw1 = sequence.SpecialAttackWeaponUnknown1;
                saw2 = sequence.SpecialAttackWeaponUnknown2;
                saw3 = sequence.SpecialAttackWeaponUnknown3;
                saw4 = sequence.SpecialAttackWeaponUnknown4;
                saw5 = sequence.SpecialAttackWeaponUnknown5;
                attackUnknown = sequence.AttackN3Unknown;
                attackAction = sequence.AttackAction;
                if (sequence.OpeningAttack != null)
                {
                    attacks.Add(sequence.OpeningAttack);
                    firstHitDelays.Add(sequence.InitialAttackDelaySeconds);
                }

                if (sequence.RepeatingAttack != null)
                {
                    attacks.Add(sequence.RepeatingAttack);
                    firstHitDelays.Add(
                        sequence.InitialAttackDelaySeconds
                        + (sequence.OpeningAttack == null
                               ? 0.0d
                               : sequence.OpeningAttack.RechargeSeconds));
                }
            }
            else if (contract.ParallelAttackSequence != null)
            {
                CapturedEnemyParallelAttackSequenceDefinition sequence = contract.ParallelAttackSequence;
                currentSpecials = sequence.SpecialAttacks;
                sawUnknown = sequence.SpecialAttackWeaponN3Unknown;
                saw1 = sequence.SpecialAttackWeaponUnknown1;
                saw2 = sequence.SpecialAttackWeaponUnknown2;
                saw3 = sequence.SpecialAttackWeaponUnknown3;
                saw4 = sequence.SpecialAttackWeaponUnknown4;
                saw5 = sequence.SpecialAttackWeaponUnknown5;
                attackUnknown = sequence.AttackN3Unknown;
                attackAction = sequence.AttackAction;
                foreach (CapturedEnemyParallelAttackStreamDefinition stream in sequence.Streams)
                {
                    attacks.Add(stream.Attack);
                    firstHitDelays.Add(stream.InitialDelaySeconds);
                }
            }
            else
            {
                return false;
            }

            if (sawUnknown != this.SpecialAttackWeaponN3Unknown
                || saw1 != this.SpecialAttackWeaponUnknown1
                || saw2 != this.SpecialAttackWeaponUnknown2
                || saw3 != this.SpecialAttackWeaponUnknown3
                || saw4 != this.SpecialAttackWeaponUnknown4
                || saw5 != this.SpecialAttackWeaponUnknown5
                || attackUnknown != this.AttackN3Unknown
                || attackAction != this.AttackAction
                || !SpecialsMatch(currentSpecials, this.SpecialAttacks)
                || attacks.Count == 0
                || attacks.Count != this.Streams.Length)
            {
                return false;
            }

            var matches = new bool[attacks.Count, this.Streams.Length];
            for (int phaseIndex = 0; phaseIndex < attacks.Count; phaseIndex++)
            {
                for (int streamIndex = 0; streamIndex < this.Streams.Length; streamIndex++)
                {
                    CapturedEnemyCombatProfileStreamDefinition stream = this.Streams[streamIndex];
                    matches[phaseIndex, streamIndex] = stream.Matches(
                        attacks[phaseIndex],
                        0.0d,
                        firstHitDelays[phaseIndex],
                        this.ResolveLandedIntervalObservations(stream),
                        this.WeaponDefinition);
                }
            }

            int[] streamForPhase;
            if (!TryGetBijectivePhaseToStreamMatch(matches, out streamForPhase))
            {
                return false;
            }

            capturedDamageObservationsByAttack = streamForPhase.Select(
                streamIndex => this.Streams[streamIndex].CapturedDamageObservations.ToArray()).ToArray();
            return true;
        }

        private static bool TryGetBijectivePhaseToStreamMatch(
            bool[,] matches,
            out int[] streamForPhase)
        {
            int phaseCount = matches.GetLength(0);
            int streamCount = matches.GetLength(1);
            streamForPhase = null;
            if (phaseCount == 0 || phaseCount != streamCount)
            {
                return false;
            }

            int[] phaseForStream = Enumerable.Repeat(-1, streamCount).ToArray();
            for (int phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
            {
                if (!TryAssignPhaseToDistinctStream(
                        phaseIndex,
                        matches,
                        new bool[streamCount],
                        phaseForStream))
                {
                    return false;
                }
            }

            streamForPhase = Enumerable.Repeat(-1, phaseCount).ToArray();
            for (int streamIndex = 0; streamIndex < streamCount; streamIndex++)
            {
                int phaseIndex = phaseForStream[streamIndex];
                if (phaseIndex < 0 || streamForPhase[phaseIndex] >= 0)
                {
                    streamForPhase = null;
                    return false;
                }

                streamForPhase[phaseIndex] = streamIndex;
            }

            return true;
        }

        private static bool TryAssignPhaseToDistinctStream(
            int phaseIndex,
            bool[,] matches,
            bool[] visitedStreams,
            int[] phaseForStream)
        {
            for (int streamIndex = 0; streamIndex < matches.GetLength(1); streamIndex++)
            {
                if (!matches[phaseIndex, streamIndex] || visitedStreams[streamIndex])
                {
                    continue;
                }

                visitedStreams[streamIndex] = true;
                if (phaseForStream[streamIndex] < 0
                    || TryAssignPhaseToDistinctStream(
                        phaseForStream[streamIndex],
                        matches,
                        visitedStreams,
                        phaseForStream))
                {
                    phaseForStream[streamIndex] = phaseIndex;
                    return true;
                }
            }

            return false;
        }

        internal double[] ResolveLandedIntervalObservations(
            CapturedEnemyCombatProfileStreamDefinition stream)
        {
            if (stream == null)
            {
                return new double[0];
            }

            if (this.WeaponDefinition != null
                && stream.WeaponSlot == this.WeaponDefinition.InventorySlot
                && stream.WeaponInstance == 0)
            {
                int attackDelay = this.WeaponDefinition.SignedStatValue(CharacterStat.AttackDelay);
                int rechargeDelay = this.WeaponDefinition.SignedStatValue(CharacterStat.RechargeDelay);
                int totalCentiseconds = attackDelay + rechargeDelay;
                if (attackDelay > 0 && rechargeDelay > 0 && totalCentiseconds > 0)
                {
                    return new[] { totalCentiseconds / 100.0d };
                }
            }

            return stream.CapturedLandedIntervalObservationsSeconds.Length > 0
                       ? stream.CapturedLandedIntervalObservationsSeconds.ToArray()
                       : new double[0];
        }

        private static bool SpecialsMatch(
            CapturedEnemySpecialAttackDefinition[] left,
            CapturedEnemySpecialAttackDefinition[] right)
        {
            left = left ?? new CapturedEnemySpecialAttackDefinition[0];
            right = right ?? new CapturedEnemySpecialAttackDefinition[0];
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] == null
                    || right[index] == null
                    || left[index].LowTemplate != right[index].LowTemplate
                    || left[index].HighTemplate != right[index].HighTemplate
                    || left[index].Tag != right[index].Tag
                    || !string.Equals(left[index].Name, right[index].Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WeaponSemanticsMatch(
            CapturedEnemyWeaponDefinition left,
            CapturedEnemyWeaponDefinition right)
        {
            if (left == null
                || right == null
                || left.N3Unknown != right.N3Unknown
                || left.Unknown1 != right.Unknown1
                || left.InventorySlot != right.InventorySlot
                || left.StateMachineType != right.StateMachineType
                || left.StateMachineInstance != right.StateMachineInstance
                || left.Unknown2 != right.Unknown2
                || left.Unknown3 != right.Unknown3
                || left.Stats.Length != right.Stats.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Stats.Length; index++)
            {
                CapturedEnemyWeaponStatDefinition leftStat = left.Stats[index];
                CapturedEnemyWeaponStatDefinition rightStat = right.Stats[index];
                if (leftStat == null
                    || rightStat == null
                    || leftStat.Stat != rightStat.Stat
                    || leftStat.Value != rightStat.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NullableDoubleEquals(double? left, double? right)
        {
            return left.HasValue == right.HasValue
                   && (!left.HasValue
                       || Math.Abs(left.Value - right.Value) < 0.000001d);
        }
    }

    internal static class CapturedEnemyCombatProfileCatalog
    {
        private static readonly CapturedEnemyCombatProfileDefinition[] Profiles =
            CapturedEnemyCombatGeneratedProfiles.Create();

        internal static bool TryResolve(
            Character character,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved,
            out string failure)
        {
            resolved = current;
            failure = string.Empty;
            if (character == null || character.Playfield == null)
            {
                failure = "runtime character or playfield is unavailable";
                return false;
            }

            return TryResolve(
                character.Playfield.Identity.Instance,
                character.Name,
                unchecked((int)character.Stats[StatIds.monsterdata].Value),
                unchecked((int)character.Stats[StatIds.level].Value),
                current == null ? 0 : current.EvidenceSourceIdentityHint,
                current,
                out resolved,
                out failure);
        }

        internal static bool TryResolve(
            int resourceId,
            string name,
            int monsterData,
            int level,
            int sourceIdentityHint,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved,
            out string failure)
        {
            resolved = current;
            failure = string.Empty;
            if (current == null)
            {
                failure = "runtime combat policy is unavailable";
                return false;
            }

            if (!current.Retaliates)
            {
                failure = "runtime actor is explicitly non-retaliatory";
                return false;
            }

            CapturedEnemyCombatContract archetypeContract;
            if (TryResolveCaptureProvenEquippedWeaponArchetype(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    sourceIdentityHint,
                    current,
                    out archetypeContract))
            {
                resolved = archetypeContract;
                return true;
            }

            CapturedEnemyCombatProfileDefinition[] keyMatches = Profiles.Where(
                value => value.MatchesKey(resourceId, name, monsterData, level)).ToArray();
            if (keyMatches.Length == 0)
            {
                failure = string.Format(
                    "no canonical raw combat profile for resource={0} name={1} MonsterData={2} level={3}",
                    resourceId,
                    name,
                    monsterData,
                    level);
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] compatibleMatches = keyMatches.Where(
                value => value.CaptureRuntimeEvidenceSafe).ToArray();
            if (compatibleMatches.Length == 0)
            {
                failure = "exact generated profiles are explicitly unsafe for runtime replay";
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] selected;
            bool exactSourceSelected = false;
            if (compatibleMatches.Length == 1)
            {
                selected = compatibleMatches;
                exactSourceSelected = sourceIdentityHint != 0
                                      && selected[0].ContainsSource(sourceIdentityHint);
            }
            else
            {
                CapturedEnemyCombatProfileDefinition[] exactSpecializedMatches =
                    current.AttackModel == CapturedEnemyAttackModel.Specialized
                        ? compatibleMatches.Where(
                            value => value.MatchesSpecialized(current)).ToArray()
                        : new CapturedEnemyCombatProfileDefinition[0];
                CapturedEnemyCombatProfileDefinition[] stableWeaponMatches =
                    compatibleMatches.Where(
                        value => value.MatchesStableWeaponProfile(current)).ToArray();
                CapturedEnemyCombatProfileDefinition[] compatibleSelection =
                    exactSpecializedMatches.Length > 0
                        ? exactSpecializedMatches
                        : stableWeaponMatches.Length == 0
                            ? compatibleMatches
                            : stableWeaponMatches;
                if (compatibleSelection.Length == 1)
                {
                    selected = compatibleSelection;
                }
                else if (sourceIdentityHint != 0)
                {
                    selected = compatibleSelection.Where(
                        value => value.ContainsSource(sourceIdentityHint)).ToArray();
                    if (selected.Length != 1)
                    {
                        failure = string.Format(
                            "captured source {0:X8} does not distinguish {1} compatible exact contracts for resource={2} name={3} MonsterData={4} level={5}",
                            sourceIdentityHint,
                            compatibleSelection.Length,
                            resourceId,
                            name,
                            monsterData,
                            level);
                        return false;
                    }

                    exactSourceSelected = true;
                }
                else
                {
                    failure = string.Format(
                        "exact generated combat profile is ambiguous: {0} compatible contracts for resource={1} name={2} MonsterData={3} level={4}; captured source identity is required",
                        compatibleSelection.Length,
                        resourceId,
                        name,
                        monsterData,
                        level);
                    return false;
                }
            }

            CapturedEnemyCombatProfileDefinition profile = selected[0];
            if (!profile.CaptureRuntimeEvidenceSafe)
            {
                failure = "selected raw profile has capture evidence that is explicitly unsafe for runtime replay";
                return false;
            }

            int evidenceSourceIdentity = exactSourceSelected
                                             ? sourceIdentityHint
                                             : profile.RepresentativeEvidenceSourceIdentity;
            CapturedEnemyWeaponDefinition weapon = profile.WeaponDefinition == null
                                                       ? null
                                                       : profile.WeaponDefinition.WithEvidenceSourceIdentity(
                                                           evidenceSourceIdentity);
            if (current != null && current.AttackModel == CapturedEnemyAttackModel.Specialized)
            {
                CapturedEnemyCombatContract enrichedSpecialized;
                if (!profile.TryEnrichSpecialized(current, out enrichedSpecialized))
                {
                    failure = "existing specialized sequence does not reproduce every selected raw packet stream";
                    return false;
                }

                resolved = enrichedSpecialized.WithCaptureCertification(
                    profile.Evidence,
                    evidenceSourceIdentity,
                    weapon)
                    .WithCapturedSpecialAttackWeaponUnknown5Observations(
                        profile.SpecialAttackWeaponUnknown5Observations);
                return resolved.IsCombatReady;
            }

            if (profile.Streams.Length != 1)
            {
                failure = "multiple captured attack streams require an exact existing specialized sequence";
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition stream = profile.Streams[0];
            double[] landedIntervalObservations =
                profile.ResolveLandedIntervalObservations(stream);
            if (!stream.HasCompleteFixedRuntimeEvidence
                || landedIntervalObservations.Length == 0)
            {
                failure = "captured profile lacks exact damage, SAW-to-Attack, first-hit, landed-interval, or attack-mode observations";
                return false;
            }

            if (landedIntervalObservations.Any(
                    value => double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0d))
            {
                failure = "captured profile has an invalid landed-interval observation";
                return false;
            }

            resolved = CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                profile.Evidence,
                evidenceSourceIdentity,
                current.AiProfile,
                stream.CapturedDamageObservations.Min(),
                stream.CapturedDamageObservations.Max(),
                landedIntervalObservations[0],
                profile.SpecialAttacks,
                profile.SpecialAttackWeaponN3Unknown,
                profile.SpecialAttackWeaponUnknown1,
                profile.SpecialAttackWeaponUnknown2,
                profile.SpecialAttackWeaponUnknown3,
                profile.SpecialAttackWeaponUnknown4,
                profile.SpecialAttackWeaponUnknown5,
                profile.AttackN3Unknown,
                profile.AttackAction,
                stream.InitialAmmoCount,
                stream.WeaponSlot,
                stream.DamageTypeWire,
                stream.HitTypeWire,
                stream.WeaponInstance,
                stream.N3Unknown,
                current.RequiresDamageLineOfSight,
                stream.CapturedDamageObservations,
                stream.CapturedAttackStartDelayObservationsSeconds,
                stream.CapturedFirstHitDelayObservationsSeconds,
                landedIntervalObservations,
                stream.CapturedDamageBonus.Value,
                stream.CapturedUsesEquippedWeapon.Value,
                stream.CapturedAttackRange,
                stream.CapturedSendAttackInfo.Value);
            if (weapon != null)
            {
                resolved = resolved.WithCapturedWeapon(weapon);
            }

            resolved = resolved.WithCapturedSpecialAttackWeaponUnknown5Observations(
                profile.SpecialAttackWeaponUnknown5Observations);
            if (!resolved.IsCombatReady)
            {
                failure = "selected raw profile failed shared contract readiness: "
                          + resolved.QuarantineReason;
                return false;
            }

            return true;
        }

        private static bool TryResolveCaptureProvenEquippedWeaponArchetype(
            int resourceId,
            string name,
            int monsterData,
            int level,
            int sourceIdentityHint,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            if (current == null
                || current.AttackModel == CapturedEnemyAttackModel.Specialized)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] family = Profiles.Where(
                value => value.MatchesArchetypeKey(
                    resourceId,
                    name,
                    monsterData)).ToArray();
            if (family.Length < 2
                || family.Select(value => value.Level).Distinct().Count() < 2
                || family.Any(
                    value => !value.SupportsCaptureProvenEquippedWeaponArchetype))
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition archetype = family[0];
            if (family.Any(
                    value => !archetype.MatchesCaptureProvenEquippedWeaponArchetype(
                        value))
                || (current.AttackModel == CapturedEnemyAttackModel.EquippedWeapon
                    && current.WeaponLowId > 0
                    && !archetype.MatchesStableWeaponProfile(current)))
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] exactLevel = family.Where(
                value => value.Level == level).ToArray();
            CapturedEnemyCombatProfileDefinition packetContext =
                exactLevel.FirstOrDefault(value => value.ContainsSource(sourceIdentityHint))
                ?? exactLevel.OrderBy(value => value.ProfileId, StringComparer.Ordinal).FirstOrDefault()
                ?? family.OrderBy(value => value.ProfileId, StringComparer.Ordinal).First();
            int evidenceSourceIdentity =
                packetContext.ContainsSource(sourceIdentityHint)
                    ? sourceIdentityHint
                    : packetContext.RepresentativeEvidenceSourceIdentity;
            CapturedEnemyWeaponDefinition weapon =
                packetContext.WeaponDefinition.WithEvidenceSourceIdentity(
                    evidenceSourceIdentity);
            CapturedEnemyCombatProfileStreamDefinition stream = packetContext.Streams[0];
            string archetypeId = string.Format(
                "resource={0}|name={1}|MonsterData={2}|profiles={3}",
                resourceId,
                name,
                monsterData,
                string.Join(
                    ",",
                    family.Select(value => value.ProfileId)
                        .OrderBy(value => value, StringComparer.Ordinal)));

            resolved = CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                packetContext.Evidence,
                evidenceSourceIdentity,
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                weapon.InventorySlot,
                true,
                0,
                0,
                0,
                stream.CapturedAttackRange,
                0.0d,
                0.0d,
                0.0d,
                0.0d,
                false,
                false,
                stream.InitialAmmoCount,
                stream.DamageTypeWire,
                packetContext.SpecialAttackWeaponUnknown1,
                packetContext.SpecialAttackWeaponUnknown2,
                packetContext.SpecialAttackWeaponUnknown3,
                packetContext.SpecialAttackWeaponUnknown4,
                packetContext.SpecialAttackWeaponUnknown5,
                stream.HitTypeWire,
                stream.N3Unknown,
                packetContext.SpecialAttackWeaponN3Unknown,
                packetContext.AttackN3Unknown,
                packetContext.AttackAction,
                current.RequiresDamageLineOfSight,
                true,
                current.AiProfile)
                .WithCapturedWeapon(weapon)
                .WithCapturedSpecialAttackWeaponUnknown5Observations(
                    packetContext.SpecialAttackWeaponUnknown5Observations)
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
        }

        internal static CapturedEnemyCombatProfileDefinition[] GetProfilesForTests()
        {
            return Profiles.ToArray();
        }
    }
}
