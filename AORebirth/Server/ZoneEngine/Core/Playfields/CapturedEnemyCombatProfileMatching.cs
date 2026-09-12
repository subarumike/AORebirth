namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed partial class CapturedEnemyCombatProfileStreamDefinition
    {
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

        internal bool MatchesProductionOwnedValues(
            CapturedEnemyCombatAttackDefinition attack)
        {
            return attack != null
                   && this.HasCompleteFixedRuntimeEvidence
                   && attack.UsesEquippedWeapon == this.CapturedUsesEquippedWeapon.Value
                   && attack.SendAttackInfo == this.CapturedSendAttackInfo.Value
                   && attack.AttackInfoWeaponSlot == this.WeaponSlot
                   && attack.AttackInfoUnknown == this.DamageTypeWire
                   && attack.AttackInfoHitType == this.HitTypeWire
                   && attack.AttackInfoWeaponInstance == this.WeaponInstance
                   && attack.AttackInfoN3Unknown == this.N3Unknown;
        }

        internal bool MatchesCapturedTerminalOutcome(
            CapturedEnemyCombatAttackDefinition attack)
        {
            return this.CapturedTerminalHitOnly
                   && attack != null
                   && this.HasCompleteFixedRuntimeEvidence
                   && attack.UsesEquippedWeapon == this.CapturedUsesEquippedWeapon.Value
                   && attack.SendAttackInfo == this.CapturedSendAttackInfo.Value
                   && attack.AttackInfoWeaponSlot == this.WeaponSlot
                   && attack.AttackInfoHitType == this.HitTypeWire
                   && attack.AttackInfoWeaponInstance == this.WeaponInstance
                   && attack.AttackInfoN3Unknown == this.N3Unknown;
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

    internal sealed partial class CapturedEnemyCombatProfileDefinition
    {
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
                       && this.SupportsCaptureProvenEquippedWeaponPacketSemantics;
            }
        }

        internal bool SupportsCaptureProvenEquippedWeaponPacketSemantics
        {
            get
            {
                return this.CaptureEvidenceSafe
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

        internal bool MatchesStableWeaponFamily(CapturedEnemyCombatContract contract)
        {
            return contract != null
                   && this.WeaponDefinition != null
                   && contract.WeaponLowId > 0
                   && contract.WeaponHighId > 0
                   && contract.WeaponInventorySlot > 0
                   && this.WeaponDefinition.LowId == contract.WeaponLowId
                   && this.WeaponDefinition.HighId == contract.WeaponHighId
                   && this.WeaponDefinition.InventorySlot == contract.WeaponInventorySlot;
        }

        internal bool MatchesCaptureProvenEquippedWeaponPacketSemantics(
            CapturedEnemyCombatProfileDefinition other)
        {
            if (!this.SupportsCaptureProvenEquippedWeaponPacketSemantics
                || other == null
                || !other.SupportsCaptureProvenEquippedWeaponPacketSemantics
                || this.SpecialAttackWeaponN3Unknown != other.SpecialAttackWeaponN3Unknown
                || this.SpecialAttackWeaponUnknown1 != other.SpecialAttackWeaponUnknown1
                || this.SpecialAttackWeaponUnknown2 != other.SpecialAttackWeaponUnknown2
                || this.SpecialAttackWeaponUnknown3 != other.SpecialAttackWeaponUnknown3
                || this.SpecialAttackWeaponUnknown4 != other.SpecialAttackWeaponUnknown4
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !WeaponSemanticsMatchExceptQuality(
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
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo;
        }

        internal bool SupportsMeldedPatternsMathematicalPacketSemantics
        {
            get
            {
                return this.SupportsCaptureProvenEquippedWeaponPacketSemantics
                       && OrdinaryEnemyCombatSetupGenerator
                           .IsMeldedPatternsWeaponLoadout(
                               this.WeaponDefinition.LowId,
                               this.WeaponDefinition.HighId,
                               this.WeaponDefinition.Quality);
            }
        }

        internal bool MatchesMeldedPatternsMathematicalPacketSemantics(
            CapturedEnemyCombatProfileDefinition other)
        {
            if (!this.SupportsMeldedPatternsMathematicalPacketSemantics
                || other == null
                || !other.SupportsMeldedPatternsMathematicalPacketSemantics
                || this.SpecialAttackWeaponN3Unknown
                   != other.SpecialAttackWeaponN3Unknown
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !WeaponPacketStructureMatches(
                    this.WeaponDefinition,
                    other.WeaponDefinition))
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition left = this.Streams[0];
            CapturedEnemyCombatProfileStreamDefinition right = other.Streams[0];
            return left.WeaponSlot == right.WeaponSlot
                   && left.DamageTypeWire == right.DamageTypeWire
                   && left.HitTypeWire == right.HitTypeWire
                   && left.WeaponInstance == right.WeaponInstance
                   && left.N3Unknown == right.N3Unknown
                   && left.CapturedUsesEquippedWeapon
                      == right.CapturedUsesEquippedWeapon
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo;
        }

        internal bool SupportsFragmentedSoulMathematicalPacketSemantics
        {
            get
            {
                return this.SupportsCaptureProvenEquippedWeaponPacketSemantics
                       && OrdinaryEnemyCombatSetupGenerator
                           .IsFragmentedSoulWeaponLoadout(
                               this.WeaponDefinition.LowId,
                               this.WeaponDefinition.HighId,
                               this.WeaponDefinition.Quality);
            }
        }

        internal bool MatchesFragmentedSoulMathematicalPacketSemantics(
            CapturedEnemyCombatProfileDefinition other)
        {
            if (!this.SupportsFragmentedSoulMathematicalPacketSemantics
                || other == null
                || !other.SupportsFragmentedSoulMathematicalPacketSemantics
                || this.SpecialAttackWeaponN3Unknown
                   != other.SpecialAttackWeaponN3Unknown
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !WeaponPacketStructureMatches(
                    this.WeaponDefinition,
                    other.WeaponDefinition))
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition left = this.Streams[0];
            CapturedEnemyCombatProfileStreamDefinition right = other.Streams[0];
            return left.WeaponSlot == right.WeaponSlot
                   && left.DamageTypeWire == right.DamageTypeWire
                   && left.HitTypeWire == right.HitTypeWire
                   && left.WeaponInstance == right.WeaponInstance
                   && left.N3Unknown == right.N3Unknown
                   && left.CapturedUsesEquippedWeapon
                      == right.CapturedUsesEquippedWeapon
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo;
        }

        internal bool SupportsGeneratedEquippedWeaponPacketSemantics(
            OrdinaryEnemyEquippedFormulaDomain domain)
        {
            return domain != null
                   && this.SupportsCaptureProvenEquippedWeaponPacketSemantics
                   && domain.MatchesWeaponLoadout(
                       this.WeaponDefinition.LowId,
                       this.WeaponDefinition.HighId,
                       this.WeaponDefinition.Quality);
        }

        internal bool MatchesGeneratedEquippedWeaponPacketSemantics(
            CapturedEnemyCombatProfileDefinition other,
            OrdinaryEnemyEquippedFormulaDomain domain)
        {
            if (!this.SupportsGeneratedEquippedWeaponPacketSemantics(domain)
                || other == null
                || !other.SupportsGeneratedEquippedWeaponPacketSemantics(domain)
                || this.SpecialAttackWeaponN3Unknown
                   != other.SpecialAttackWeaponN3Unknown
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !WeaponPacketStructureMatches(
                    this.WeaponDefinition,
                    other.WeaponDefinition))
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition left = this.Streams[0];
            CapturedEnemyCombatProfileStreamDefinition right = other.Streams[0];
            return left.WeaponSlot == right.WeaponSlot
                   && left.DamageTypeWire == right.DamageTypeWire
                   && left.HitTypeWire == right.HitTypeWire
                   && left.WeaponInstance == right.WeaponInstance
                   && left.N3Unknown == right.N3Unknown
                   && left.CapturedUsesEquippedWeapon
                      == right.CapturedUsesEquippedWeapon
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo;
        }

        internal bool SupportsCaptureProvenNaturalAttackPacketSemantics
        {
            get
            {
                return this.CaptureEvidenceSafe
                       && this.WeaponDefinition == null
                       && this.GetReusableNaturalAttackStreams().Length == 1;
            }
        }

        internal bool MatchesCaptureProvenNaturalAttackPacketSemantics(
            CapturedEnemyCombatProfileDefinition other)
        {
            if (!this.SupportsCaptureProvenNaturalAttackPacketSemantics
                || other == null
                || !other.SupportsCaptureProvenNaturalAttackPacketSemantics
                || this.SpecialAttackWeaponN3Unknown != other.SpecialAttackWeaponN3Unknown
                || this.AttackN3Unknown != other.AttackN3Unknown
                || this.AttackAction != other.AttackAction
                || !SpecialsMatch(this.SpecialAttacks, other.SpecialAttacks))
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition left =
                this.GetReusableNaturalAttackStreams()[0];
            CapturedEnemyCombatProfileStreamDefinition right =
                other.GetReusableNaturalAttackStreams()[0];
            return left.WeaponSlot == right.WeaponSlot
                   && left.DamageTypeWire == right.DamageTypeWire
                   && left.HitTypeWire == right.HitTypeWire
                   && left.WeaponInstance == right.WeaponInstance
                   && left.N3Unknown == right.N3Unknown
                   && left.CapturedUsesEquippedWeapon == right.CapturedUsesEquippedWeapon
                   && left.CapturedSendAttackInfo == right.CapturedSendAttackInfo;
        }

        internal CapturedEnemyCombatProfileStreamDefinition[]
            GetReusableNaturalAttackStreams()
        {
            return this.Streams.Where(
                value => value.CapturedUsesEquippedWeapon == false
                         && value.CapturedSendAttackInfo == true
                         && !value.CapturedTerminalHitOnly).ToArray();
        }

        internal bool MatchesSpecialized(CapturedEnemyCombatContract contract)
        {
            int[][] ignoredObservations;
            int?[] ignoredTerminalUnknowns;
            return this.TryMatchSpecialized(
                contract,
                out ignoredObservations,
                out ignoredTerminalUnknowns);
        }

        internal bool TryEnrichSpecialized(
            CapturedEnemyCombatContract contract,
            out CapturedEnemyCombatContract enriched)
        {
            enriched = null;
            int[][] capturedDamageObservationsByAttack;
            int?[] lethalAttackInfoUnknownByAttack;
            if (!this.TryMatchSpecialized(
                    contract,
                    out capturedDamageObservationsByAttack,
                    out lethalAttackInfoUnknownByAttack))
            {
                return false;
            }

            enriched = contract.WithCapturedSpecializedDamageObservations(
                capturedDamageObservationsByAttack,
                lethalAttackInfoUnknownByAttack);
            return enriched != null;
        }

        private bool TryMatchSpecialized(
            CapturedEnemyCombatContract contract,
            out int[][] capturedDamageObservationsByAttack,
            out int?[] lethalAttackInfoUnknownByAttack)
        {
            capturedDamageObservationsByAttack = null;
            lethalAttackInfoUnknownByAttack = null;
            if (contract == null
                || contract.AttackModel != CapturedEnemyAttackModel.Specialized)
            {
                return false;
            }

            bool productionOwnsVariableValues =
                contract.UsesProductionSpecializedValues;
            if (!this.CaptureRuntimeEvidenceSafe
                && (!productionOwnsVariableValues || !this.CaptureEvidenceSafe))
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
                || (!productionOwnsVariableValues
                    && (saw1 != this.SpecialAttackWeaponUnknown1
                        || saw2 != this.SpecialAttackWeaponUnknown2
                        || saw3 != this.SpecialAttackWeaponUnknown3
                        || saw4 != this.SpecialAttackWeaponUnknown4
                        || saw5 != this.SpecialAttackWeaponUnknown5))
                || attackUnknown != this.AttackN3Unknown
                || attackAction != this.AttackAction
                || !SpecialsMatch(currentSpecials, this.SpecialAttacks)
                || attacks.Count == 0)
            {
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition[] cadenceStreams =
                this.Streams.Where(stream => !stream.CapturedTerminalHitOnly).ToArray();
            CapturedEnemyCombatProfileStreamDefinition[] terminalStreams =
                this.Streams.Where(stream => stream.CapturedTerminalHitOnly).ToArray();
            if (attacks.Count != cadenceStreams.Length)
            {
                return false;
            }

            var matches = new bool[attacks.Count, cadenceStreams.Length];
            for (int phaseIndex = 0; phaseIndex < attacks.Count; phaseIndex++)
            {
                for (int streamIndex = 0; streamIndex < cadenceStreams.Length; streamIndex++)
                {
                    CapturedEnemyCombatProfileStreamDefinition stream = cadenceStreams[streamIndex];
                    matches[phaseIndex, streamIndex] =
                        productionOwnsVariableValues
                            ? stream.MatchesProductionOwnedValues(attacks[phaseIndex])
                            : stream.Matches(
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
                streamIndex => cadenceStreams[streamIndex].CapturedDamageObservations.ToArray()).ToArray();
            lethalAttackInfoUnknownByAttack = new int?[attacks.Count];
            foreach (CapturedEnemyCombatProfileStreamDefinition terminalStream in terminalStreams)
            {
                int[] compatiblePhases = Enumerable.Range(0, attacks.Count).Where(
                    phaseIndex => terminalStream.MatchesCapturedTerminalOutcome(
                        attacks[phaseIndex])).ToArray();
                if (compatiblePhases.Length != 1)
                {
                    capturedDamageObservationsByAttack = null;
                    lethalAttackInfoUnknownByAttack = null;
                    return false;
                }

                int phase = compatiblePhases[0];
                if (lethalAttackInfoUnknownByAttack[phase].HasValue
                    && lethalAttackInfoUnknownByAttack[phase].Value
                       != terminalStream.DamageTypeWire)
                {
                    capturedDamageObservationsByAttack = null;
                    lethalAttackInfoUnknownByAttack = null;
                    return false;
                }

                lethalAttackInfoUnknownByAttack[phase] =
                    terminalStream.DamageTypeWire;
            }

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

        private static bool WeaponSemanticsMatchExceptQuality(
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
                    || (leftStat.Stat != CharacterStat.ACGItemLevel
                        && leftStat.Value != rightStat.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WeaponPacketStructureMatches(
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
                    || leftStat.Stat != rightStat.Stat)
                {
                    return false;
                }

                switch (leftStat.Stat)
                {
                    case CharacterStat.StaticInstance:
                    case CharacterStat.ACGItemLevel:
                    case CharacterStat.ACGItemTemplateID:
                    case CharacterStat.ACGItemTemplateID2:
                    case CharacterStat.Energy:
                        continue;
                }

                if (leftStat.Value != rightStat.Value)
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

}
