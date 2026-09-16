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

    internal static class CapturedEnemyCombatProfileCatalog
    {
        private const string FilthFleaSemanticProfileId =
            "218eb3509f2be66b-12f99a4c2f732061";

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

            if (current.AttackModel == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary
                && current.IsCombatReady)
            {
                resolved = current;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(current.EvidenceProfileSelectorHint))
            {
                CapturedEnemyCombatProfileDefinition[] selectorMatches = Profiles.Where(
                    value => value.MatchesKey(resourceId, name, monsterData, level)
                             && string.Equals(
                                 value.ProfileId,
                                 current.EvidenceProfileSelectorHint,
                                 StringComparison.Ordinal)).ToArray();
                if (selectorMatches.Length != 1)
                {
                    failure = string.Format(
                        "captured profile selector {0} does not identify one exact profile for resource={1} name={2} MonsterData={3} level={4}",
                        current.EvidenceProfileSelectorHint,
                        resourceId,
                        name,
                        monsterData,
                        level);
                    return false;
                }

                if (sourceIdentityHint == 0 || !selectorMatches[0].ContainsSource(sourceIdentityHint))
                {
                    failure = string.Format(
                        "captured source {0:X8} is not evidence for selected profile {1}",
                        sourceIdentityHint,
                        current.EvidenceProfileSelectorHint);
                    return false;
                }

                CapturedEnemyCombatProfileDefinition selectedProfile = selectorMatches[0];
                if (selectedProfile.WeaponDefinition == null
                    && selectedProfile.GetReusableNaturalAttackStreams().Length > 0)
                {
                    CapturedEnemyCombatContract parallelContract;
                    if (!TryResolveCapturedProfileSelectorParallelSequence(
                            selectedProfile,
                            sourceIdentityHint,
                            current,
                            out parallelContract,
                            out failure))
                    {
                        return false;
                    }

                    resolved = parallelContract;
                    return true;
                }
            }

            CapturedEnemyCombatContract generatedResultDomainContract;
            if (TryResolveMathematicallyGeneratedResultDomain(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    current,
                    out generatedResultDomainContract))
            {
                resolved = generatedResultDomainContract;
                return true;
            }

            CapturedEnemyCombatContract generatedNaturalAttackContract;
            if (TryResolveMathematicallyGeneratedNaturalAttackArchetype(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    current,
                    out generatedNaturalAttackContract))
            {
                resolved = generatedNaturalAttackContract;
                return true;
            }

            CapturedEnemyCombatContract generatedSpecializedAttackContract;
            if (TryResolveMathematicallyGeneratedSpecializedAttackArchetype(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    current,
                    out generatedSpecializedAttackContract))
            {
                resolved = generatedSpecializedAttackContract;
                return true;
            }

            CapturedEnemyCombatContract naturalAttackContract;
            if (TryResolveProductionOwnedNaturalAttackProfile(
                    resourceId,
                    name,
                    monsterData,
                    current,
                    out naturalAttackContract))
            {
                resolved = naturalAttackContract;
                return true;
            }

            CapturedEnemyCombatContract generatedEquippedAttackContract;
            if (TryResolveMathematicallyGeneratedEquippedWeaponArchetype(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    sourceIdentityHint,
                    current,
                    out generatedEquippedAttackContract))
            {
                resolved = generatedEquippedAttackContract;
                return true;
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

            if (!string.IsNullOrWhiteSpace(current.EvidenceProfileSelectorHint))
            {
                keyMatches = keyMatches.Where(
                    value => string.Equals(
                        value.ProfileId,
                        current.EvidenceProfileSelectorHint,
                        StringComparison.Ordinal)).ToArray();
                if (keyMatches.Length != 1)
                {
                    failure = string.Format(
                        "captured profile selector {0} does not identify one exact profile for resource={1} name={2} MonsterData={3} level={4}",
                        current.EvidenceProfileSelectorHint,
                        resourceId,
                        name,
                        monsterData,
                        level);
                    return false;
                }

                if (sourceIdentityHint == 0 || !keyMatches[0].ContainsSource(sourceIdentityHint))
                {
                    failure = string.Format(
                        "captured source {0:X8} is not evidence for selected profile {1}",
                        sourceIdentityHint,
                        current.EvidenceProfileSelectorHint);
                    return false;
                }

                CapturedEnemyCombatProfileDefinition selectedProfile = keyMatches[0];
                if (selectedProfile.WeaponDefinition == null
                    && selectedProfile.GetReusableNaturalAttackStreams().Length > 0)
                {
                    CapturedEnemyCombatContract parallelContract;
                    if (!TryResolveCapturedProfileSelectorParallelSequence(
                            selectedProfile,
                            sourceIdentityHint,
                            current,
                            out parallelContract,
                            out failure))
                    {
                        return false;
                    }

                    resolved = parallelContract;
                    return true;
                }
            }

            CapturedEnemyCombatProfileDefinition[] compatibleMatches = keyMatches.Where(
                value => value.CaptureRuntimeEvidenceSafe
                         || (current.AttackModel == CapturedEnemyAttackModel.Specialized
                             && current.UsesProductionSpecializedValues
                             && value.MatchesSpecialized(current))).ToArray();
            if (compatibleMatches.Length == 0)
            {
                failure = "exact generated profiles are explicitly unsafe for runtime replay";
                return false;
            }

            if (current.AttackModel == CapturedEnemyAttackModel.EquippedWeapon
                && current.WeaponLowId > 0)
            {
                CapturedEnemyCombatProfileDefinition[] stableWeaponMatches =
                    compatibleMatches.Where(
                        value => value.MatchesStableWeaponProfile(current)).ToArray();
                if (stableWeaponMatches.Length == 0)
                {
                    failure = string.Format(
                        "no exact stable weapon profile for low={0} high={1} QL={2} slot={3}",
                        current.WeaponLowId,
                        current.WeaponHighId,
                        current.WeaponQuality,
                        current.WeaponInventorySlot);
                    return false;
                }

                compatibleMatches = stableWeaponMatches;
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
            if (!profile.CaptureRuntimeEvidenceSafe
                && !(current.AttackModel == CapturedEnemyAttackModel.Specialized
                     && current.UsesProductionSpecializedValues
                     && profile.MatchesSpecialized(current)))
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
                        profile.SpecialAttackWeaponUnknown5Observations)
                    .WithCaptureProvenArchetype(profile.ProfileId);
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
                stream.CapturedAttackRange ?? current.CapturedAttackRange,
                stream.CapturedSendAttackInfo.Value);
            if (weapon != null)
            {
                resolved = resolved.WithCapturedWeapon(weapon);
            }

            if (current.UsesProductionEquippedWeaponValues)
            {
                resolved = resolved.WithProductionEquippedWeaponValues();
            }

            resolved = resolved.WithCapturedSpecialAttackWeaponUnknown5Observations(
                profile.SpecialAttackWeaponUnknown5Observations)
                .WithCaptureProvenArchetype(profile.ProfileId);
            if (!resolved.IsCombatReady)
            {
                failure = "selected raw profile failed shared contract readiness: "
                          + resolved.QuarantineReason;
                return false;
            }

            return true;
        }

        private static bool TryResolveCapturedProfileSelectorParallelSequence(
            CapturedEnemyCombatProfileDefinition profile,
            int sourceIdentityHint,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved,
            out string failure)
        {
            resolved = current;
            failure = string.Empty;
            if (profile == null
                || current == null
                || !profile.CaptureEvidenceSafe
                || profile.WeaponDefinition != null)
            {
                failure = "selected profile is not a capture-safe natural-attack profile";
                return false;
            }

            // The generated profile constructor retains one representative Unknown5 value,
            // while the active-coverage generator certifies the selector hint against this
            // exact source identity. Do not collapse a source-local value back to the profile
            // representative (Cleanmeister is 82 while another source in its profile is 0).
            if (!current.EvidenceSpecialAttackWeaponUnknown5Hint.HasValue)
            {
                failure = "selected profile has no exact source-local SpecialAttackWeapon Unknown5 state";
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition[] cadenceStreams =
                profile.GetReusableNaturalAttackStreams();
            if (cadenceStreams.Length == 0
                || cadenceStreams.Any(
                    stream => !stream.HasCompleteFixedRuntimeEvidence
                              || stream.CapturedUsesEquippedWeapon != false
                              || stream.CapturedSendAttackInfo != true
                              || stream.CapturedFirstHitDelayObservationsSeconds.Length == 0))
            {
                failure = "selected profile lacks a complete captured attack stream";
                return false;
            }

            double[] capturedAttackRanges = cadenceStreams.Select(
                stream => stream.CapturedAttackRange ?? double.NaN).ToArray();
            bool hasSingleGeneratedAttackRange =
                capturedAttackRanges.All(
                    value => !double.IsNaN(value)
                             && !double.IsInfinity(value)
                             && value > 0.0d)
                && capturedAttackRanges.All(
                    value => Math.Abs(value - capturedAttackRanges[0]) < 0.000001d);
            double? selectedAttackRange = hasSingleGeneratedAttackRange
                                              ? (double?)capturedAttackRanges[0]
                                              : current.CapturedAttackRange;
            if ((profile.ResourceId == 6553 && !hasSingleGeneratedAttackRange)
                || !selectedAttackRange.HasValue
                || double.IsNaN(selectedAttackRange.Value)
                || double.IsInfinity(selectedAttackRange.Value)
                || selectedAttackRange.Value <= 0.0d)
            {
                failure = "selected natural-attack profile has no single generated capture-backed attack range";
                return false;
            }
            double capturedAttackRange = selectedAttackRange.Value;

            double[] attackStartDelays = cadenceStreams.SelectMany(
                stream => stream.CapturedAttackStartDelayObservationsSeconds).ToArray();
            if (!current.EvidenceAttackStartDelaySecondsHint.HasValue
                || double.IsNaN(current.EvidenceAttackStartDelaySecondsHint.Value)
                || double.IsInfinity(current.EvidenceAttackStartDelaySecondsHint.Value)
                || current.EvidenceAttackStartDelaySecondsHint.Value < 0.0d
                || attackStartDelays.Length == 0
                || attackStartDelays.Any(
                    value => double.IsNaN(value)
                             || double.IsInfinity(value)
                             || value < 0.0d)
                || !attackStartDelays.Any(
                    value => Math.Abs(
                        value - current.EvidenceAttackStartDelaySecondsHint.Value)
                             < 0.000001d))
            {
                failure = "selected profile has no exact source-local captured attack-start delay";
                return false;
            }

            var parallelStreams = new List<CapturedEnemyParallelAttackStreamDefinition>();
            foreach (CapturedEnemyCombatProfileStreamDefinition stream in cadenceStreams)
            {
                double[] landedIntervals = profile.ResolveLandedIntervalObservations(stream);
                if (landedIntervals.Any(
                    value => double.IsNaN(value)
                             || double.IsInfinity(value)
                             || value <= 0.0d))
                {
                    failure = "selected profile contains an invalid captured landed interval";
                    return false;
                }

                bool repeats = landedIntervals.Length > 0;
                parallelStreams.Add(
                    new CapturedEnemyParallelAttackStreamDefinition(
                        stream.CapturedFirstHitDelayObservationsSeconds[0],
                        new CapturedEnemyCombatAttackDefinition(
                            stream.CapturedDamageObservations.Min(),
                            stream.CapturedDamageObservations.Max(),
                            stream.CapturedDamageBonus.Value,
                            capturedAttackRange,
                            repeats ? landedIntervals[0] : 0.0d,
                            false,
                            stream.InitialAmmoCount,
                            stream.WeaponSlot,
                            stream.DamageTypeWire,
                            stream.HitTypeWire,
                            stream.WeaponInstance,
                            stream.N3Unknown,
                            true,
                            stream.CapturedDamageObservations),
                        repeats));
            }

            if (!parallelStreams.Any(stream => stream.Repeats))
            {
                failure = "selected profile has no capture-backed repeating attack cadence";
                return false;
            }

            CapturedEnemyCombatProfileStreamDefinition[] terminalStreams =
                profile.Streams.Where(stream => stream.CapturedTerminalHitOnly).ToArray();
            foreach (CapturedEnemyCombatProfileStreamDefinition terminalStream in terminalStreams)
            {
                int[] compatible = Enumerable.Range(0, parallelStreams.Count).Where(
                    index => terminalStream.MatchesCapturedTerminalOutcome(
                        parallelStreams[index].Attack)).ToArray();
                if (compatible.Length != 1)
                {
                    failure = "captured terminal attack outcome does not map to one selected stream";
                    return false;
                }

                int streamIndex = compatible[0];
                CapturedEnemyParallelAttackStreamDefinition existing =
                    parallelStreams[streamIndex];
                parallelStreams[streamIndex] =
                    new CapturedEnemyParallelAttackStreamDefinition(
                        existing.InitialDelaySeconds,
                        existing.Attack.WithCapturedDamageObservations(
                            existing.Attack.CapturedDamageObservations,
                            terminalStream.DamageTypeWire),
                        existing.Repeats);
            }

            CapturedEnemyCombatContract captured =
                CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                    profile.Evidence + "; selector=" + current.Evidence,
                    new CapturedEnemyParallelAttackSequenceDefinition(
                        parallelStreams.ToArray(),
                        profile.SpecialAttacks,
                        profile.SpecialAttackWeaponUnknown1,
                        profile.SpecialAttackWeaponUnknown2,
                        profile.SpecialAttackWeaponUnknown3,
                        profile.SpecialAttackWeaponUnknown4,
                        current.EvidenceSpecialAttackWeaponUnknown5Hint.Value,
                        profile.SpecialAttackWeaponN3Unknown,
                        profile.AttackN3Unknown,
                        profile.AttackAction,
                        current.EvidenceAttackStartDelaySecondsHint.Value),
                    current.RequiresDamageLineOfSight,
                    current.AiProfile);
            resolved = captured
                .WithCaptureCertification(profile.Evidence, sourceIdentityHint, null)
                .WithCapturedSpecialAttackWeaponUnknown5Observations(
                    new[] { current.EvidenceSpecialAttackWeaponUnknown5Hint.Value })
                .WithCaptureProvenArchetype(profile.ProfileId)
                .WithCapturedAttackRange(capturedAttackRange);
            if (!resolved.IsCombatReady)
            {
                failure = "selected parallel profile failed shared readiness: "
                          + resolved.QuarantineReason;
                return false;
            }

            return true;
        }

        private static bool TryResolveMathematicallyGeneratedResultDomain(
            int resourceId,
            string name,
            int monsterData,
            int level,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            OrdinaryEnemyCombatNumericSetup generated;
            OrdinaryEnemyCombatResultDomain domain;
            if (!OrdinaryEnemyCombatSetupGenerator.MatchesGeneratedEquippedSetup(
                    monsterData,
                    level,
                    current,
                    out generated)
                || generated.FormulaId
                   != OrdinaryEnemyCombatSetupGenerator.ViolentVagabondFormulaId
                || !OrdinaryEnemyCombatResultDomainRegistry.TryResolve(
                    resourceId,
                    name,
                    monsterData,
                    current,
                    out domain)
                || current.WeaponDefinition == null
                || !current.WeaponDefinition.IsValid)
            {
                return false;
            }

            string archetypeId = string.Format(
                "formula={0}|resultDomain={1}|resource={2}|name={3}|MonsterData={4}|weaponFamily=130590",
                generated.FormulaId,
                domain.DomainId,
                resourceId,
                name,
                monsterData);
            resolved = current
                .WithCaptureCertification(
                    current.Evidence + "; result-domain evidence=" + domain.Evidence,
                    current.EvidenceSourceIdentity,
                    current.WeaponDefinition)
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
        }

        private static bool TryResolveMathematicallyGeneratedNaturalAttackArchetype(
            int resourceId,
            string name,
            int monsterData,
            int level,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            OrdinaryEnemyCombatNumericSetup generated;
            if (!OrdinaryEnemyCombatSetupGenerator.MatchesGeneratedSetup(
                    monsterData,
                    level,
                    current,
                    out generated))
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] family = Profiles.Where(
                value => value.MatchesArchetypeKey(resourceId, name, monsterData)
                         && value
                             .SupportsCaptureProvenNaturalAttackPacketSemantics)
                .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
                .ToArray();
            if (family.Length == 0)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition profile = family[0];
            if (family.Any(
                value => !profile
                    .MatchesCaptureProvenNaturalAttackPacketSemantics(value)))
            {
                return false;
            }

            if (!profile.MatchesSpecialized(current))
            {
                return false;
            }

            int evidenceSourceIdentity =
                profile.RepresentativeEvidenceSourceIdentity;
            string evidence = string.Join(
                "; ",
                family.Select(value => value.Evidence).Distinct().ToArray());
            string archetypeId = string.Format(
                "formula={0}|resource={1}|name={2}|MonsterData={3}|profiles={4}",
                generated.FormulaId,
                resourceId,
                name,
                monsterData,
                string.Join(
                    ",",
                    family.Select(value => value.ProfileId).ToArray()));
            resolved = current
                .WithCaptureCertification(
                    evidence + "; generated numeric setup=" + generated.FormulaId,
                    evidenceSourceIdentity,
                    null)
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
        }

        private static bool TryResolveMathematicallyGeneratedSpecializedAttackArchetype(
            int resourceId,
            string name,
            int monsterData,
            int level,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            OrdinaryEnemyCombatNumericSetup generated;
            if (!OrdinaryEnemyCombatSetupGenerator.MatchesGeneratedSetup(
                    monsterData,
                    level,
                    current,
                    out generated)
                || generated.FormulaId
                   != OrdinaryEnemyCombatSetupGenerator.FilthFleaFormulaId)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] compatibleFamily =
                Profiles.Where(
                    value => value.MatchesArchetypeKey(
                                 resourceId,
                                 name,
                                 monsterData)
                             && value.CaptureEvidenceSafe
                             && value.MatchesSpecialized(current))
                    .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
                    .ToArray();
            CapturedEnemyCombatProfileDefinition profile =
                compatibleFamily.SingleOrDefault(
                    value => value.ProfileId == FilthFleaSemanticProfileId);
            if (profile == null)
            {
                return false;
            }

            CapturedEnemyCombatContract enriched;
            if (!profile.TryEnrichSpecialized(current, out enriched))
            {
                return false;
            }

            int evidenceSourceIdentity =
                profile.RepresentativeEvidenceSourceIdentity;
            string archetypeId = string.Format(
                "formula={0}|resource={1}|name={2}|MonsterData={3}|profile={4}",
                generated.FormulaId,
                resourceId,
                name,
                monsterData,
                profile.ProfileId);
            resolved = enriched
                .WithCaptureCertification(
                    profile.Evidence
                    + "; generated numeric setup=" + generated.FormulaId,
                    evidenceSourceIdentity,
                    null)
                .WithCapturedSpecialAttackWeaponUnknown5Observations(
                    profile.SpecialAttackWeaponUnknown5Observations)
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
        }

        private static bool TryResolveMathematicallyGeneratedEquippedWeaponArchetype(
            int resourceId,
            string name,
            int monsterData,
            int level,
            int sourceIdentityHint,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            OrdinaryEnemyCombatNumericSetup generated;
            if (!OrdinaryEnemyCombatSetupGenerator.MatchesGeneratedEquippedSetup(
                    monsterData,
                    level,
                    current,
                    out generated))
            {
                return false;
            }

            OrdinaryEnemyEquippedFormulaDomain domain;
            if (!OrdinaryEnemyCombatSetupGenerator.TryGetEquippedFormulaDomain(
                    monsterData,
                    out domain))
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] family = Profiles.Where(
                    value => value.MatchesArchetypeKey(resourceId, name, monsterData)
                             && value
                                 .SupportsGeneratedEquippedWeaponPacketSemantics(
                                     domain))
                .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
                .ToArray();
            if (family.Length == 0)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition archetype = family[0];
            if (family.Any(
                value => !archetype
                    .MatchesGeneratedEquippedWeaponPacketSemantics(
                        value,
                        domain)))
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] exactLoadout = family.Where(
                value => value.MatchesStableWeaponProfile(current)).ToArray();
            CapturedEnemyCombatProfileDefinition[] exactPair = family.Where(
                value => value.MatchesStableWeaponFamily(current)).ToArray();
            CapturedEnemyCombatProfileDefinition packetContext =
                exactLoadout.FirstOrDefault(
                    value => value.Level == level
                             && value.ContainsSource(sourceIdentityHint))
                ?? exactLoadout.FirstOrDefault(value => value.Level == level)
                ?? exactLoadout.FirstOrDefault(
                    value => value.ContainsSource(sourceIdentityHint))
                ?? exactLoadout.FirstOrDefault()
                ?? exactPair.FirstOrDefault(
                    value => value.Level == level
                             && value.ContainsSource(sourceIdentityHint))
                ?? exactPair.FirstOrDefault(value => value.Level == level)
                ?? exactPair.FirstOrDefault(
                    value => value.ContainsSource(sourceIdentityHint))
                ?? exactPair.FirstOrDefault()
                ?? family[0];
            int evidenceSourceIdentity =
                packetContext.ContainsSource(sourceIdentityHint)
                    ? sourceIdentityHint
                    : packetContext.RepresentativeEvidenceSourceIdentity;
            CapturedEnemyWeaponDefinition weapon =
                packetContext.WeaponDefinition
                    .WithEvidenceSourceIdentity(evidenceSourceIdentity)
                    .WithProductionWeaponLoadout(
                        current.WeaponLowId,
                        current.WeaponHighId,
                        current.WeaponQuality);
            CapturedEnemyCombatProfileStreamDefinition stream =
                packetContext.Streams[0];
            int initialAmmo = weapon.InitialEnergy > 0
                ? weapon.InitialEnergy - 1
                : weapon.InitialEnergy;
            string evidence = string.Join(
                "; ",
                family.Select(value => value.Evidence).Distinct().ToArray());
            string archetypeId = string.Format(
                "formula={0}|resource={1}|name={2}|MonsterData={3}|weaponFamily={4}|profiles={5}",
                generated.FormulaId,
                resourceId,
                name,
                monsterData,
                domain.WeaponFamilyId,
                string.Join(
                    ",",
                    family.Select(value => value.ProfileId).ToArray()));

            resolved = CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    evidence + "; generated numeric setup=" + generated.FormulaId,
                    evidenceSourceIdentity,
                    current.WeaponLowId,
                    current.WeaponHighId,
                    current.WeaponQuality,
                    current.WeaponInventorySlot,
                    true,
                    0,
                    0,
                    0,
                    null,
                    0.0d,
                    0.0d,
                    0.0d,
                    0.0d,
                    false,
                    false,
                    initialAmmo,
                    stream.DamageTypeWire,
                    generated.SpecialAttackWeaponUnknown1,
                    generated.SpecialAttackWeaponUnknown2,
                    generated.SpecialAttackWeaponUnknown3,
                    generated.SpecialAttackWeaponUnknown4,
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
                .WithProductionEquippedWeaponValues()
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
        }

        private static bool TryResolveProductionOwnedNaturalAttackProfile(
            int resourceId,
            string name,
            int monsterData,
            CapturedEnemyCombatContract current,
            out CapturedEnemyCombatContract resolved)
        {
            resolved = null;
            if (current == null
                || !current.UsesProductionSpecializedValues
                || current.AttackModel == CapturedEnemyAttackModel.Specialized
                || current.MinDamage <= 0
                || current.MaxDamage < current.MinDamage
                || current.RechargeSeconds <= 0.0d)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition[] family = Profiles.Where(
                value => value.MatchesArchetypeKey(resourceId, name, monsterData)
                         && value
                             .SupportsCaptureProvenNaturalAttackPacketSemantics)
                .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
                .ToArray();
            if (family.Length == 0)
            {
                return false;
            }

            CapturedEnemyCombatProfileDefinition profile = family[0];
            if (family.Any(
                value => !profile
                    .MatchesCaptureProvenNaturalAttackPacketSemantics(value)))
            {
                return false;
            }

            int evidenceSourceIdentity = profile.RepresentativeEvidenceSourceIdentity;
            CapturedEnemyCombatProfileStreamDefinition stream =
                profile.GetReusableNaturalAttackStreams()[0];
            string evidence = string.Join(
                "; ",
                family.Select(value => value.Evidence).Distinct().ToArray());
            string archetypeId = string.Format(
                "resource={0}|name={1}|MonsterData={2}|profiles={3}",
                resourceId,
                name,
                monsterData,
                string.Join(
                    ",",
                    family.Select(value => value.ProfileId).ToArray()));
            resolved = CapturedEnemyCombatContract
                .CapturedSpecialSequence(
                    evidence,
                    new CapturedEnemySpecialAttackSequenceDefinition(
                        0.0d,
                        null,
                        new CapturedEnemyCombatAttackDefinition(
                            current.MinDamage,
                            current.MaxDamage,
                            current.CapturedDamageBonus,
                            current.CapturedAttackRange
                            ?? ZoneEngine.Core.Playfields.NpcCombatAttackRules
                                .MaxMeleeCombatDistance,
                            current.RechargeSeconds,
                            false,
                            current.AttackInfoAmmoCount,
                            stream.WeaponSlot,
                            stream.DamageTypeWire,
                            stream.HitTypeWire,
                            stream.WeaponInstance,
                            stream.N3Unknown,
                            true),
                        profile.SpecialAttacks,
                        current.SpecialAttackWeaponUnknown1,
                        current.SpecialAttackWeaponUnknown2,
                        current.SpecialAttackWeaponUnknown3,
                        current.SpecialAttackWeaponUnknown4,
                        current.SpecialAttackWeaponUnknown5,
                        profile.SpecialAttackWeaponN3Unknown,
                        profile.AttackN3Unknown,
                        profile.AttackAction))
                .WithProductionSpecializedValues()
                .WithCaptureCertification(
                    evidence,
                    evidenceSourceIdentity,
                    null)
                .WithCaptureProvenArchetype(archetypeId);
            return resolved.IsCombatReady;
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
            OrdinaryEnemyEquippedFormulaDomain generatedDomain;
            if (current.UsesProductionEquippedWeaponValues
                && OrdinaryEnemyCombatSetupGenerator.TryGetEquippedFormulaDomain(
                    monsterData,
                    out generatedDomain)
                && current.AttackModel
                   == CapturedEnemyAttackModel.EquippedWeapon)
            {
                return false;
            }

            if (current.UsesProductionEquippedWeaponValues)
            {
                bool hasSelectedProductionLoadout =
                    current.AttackModel
                    == CapturedEnemyAttackModel.EquippedWeapon
                    && current.WeaponLowId > 0
                    && current.WeaponHighId > 0
                    && current.WeaponInventorySlot > 0;
                CapturedEnemyCombatProfileDefinition[] exactProductionFamily = family.Where(
                    value => value.Level == level
                             && value
                                 .SupportsCaptureProvenEquippedWeaponPacketSemantics).ToArray();
                if (exactProductionFamily.Length == 0)
                {
                    return false;
                }

                if (hasSelectedProductionLoadout)
                {
                    CapturedEnemyCombatProfileDefinition[] selectedWeaponFamily =
                        exactProductionFamily.Where(
                            value => value.MatchesStableWeaponFamily(current)).ToArray();
                    if (selectedWeaponFamily.Length > 0)
                    {
                        exactProductionFamily = selectedWeaponFamily;
                    }
                }

                CapturedEnemyCombatProfileDefinition productionProfile;
                if (exactProductionFamily.Length == 1)
                {
                    productionProfile = exactProductionFamily[0];
                }
                else
                {
                    CapturedEnemyCombatProfileDefinition compatibleArchetype =
                        exactProductionFamily[0];
                    if (exactProductionFamily.Any(
                        value => !compatibleArchetype
                            .MatchesCaptureProvenEquippedWeaponPacketSemantics(
                                value)))
                    {
                        return false;
                    }

                    CapturedEnemyCombatProfileDefinition[] exactLoadoutMatches =
                        exactProductionFamily.Where(
                            value => value.MatchesStableWeaponProfile(current))
                            .ToArray();
                    CapturedEnemyCombatProfileDefinition[] sourceMatches =
                        exactProductionFamily.Where(
                            value => value.ContainsSource(sourceIdentityHint)).ToArray();
                    productionProfile = exactLoadoutMatches.Length == 1
                        ? exactLoadoutMatches[0]
                        : sourceMatches.Length == 1
                            ? sourceMatches[0]
                            : exactProductionFamily
                                .OrderBy(
                                    value => value.ProfileId,
                                    StringComparer.Ordinal)
                                .First();
                }

                int productionEvidenceSourceIdentity =
                    productionProfile.ContainsSource(sourceIdentityHint)
                        ? sourceIdentityHint
                        : productionProfile.RepresentativeEvidenceSourceIdentity;
                CapturedEnemyWeaponDefinition productionWeapon =
                    productionProfile.WeaponDefinition.WithEvidenceSourceIdentity(
                        productionEvidenceSourceIdentity);
                if (hasSelectedProductionLoadout)
                {
                    productionWeapon =
                        productionWeapon.WithProductionWeaponLoadout(
                            current.WeaponLowId,
                            current.WeaponHighId,
                            current.WeaponQuality);
                }

                CapturedEnemyCombatProfileStreamDefinition productionStream =
                    productionProfile.Streams[0];
                string productionArchetypeId = string.Format(
                    "resource={0}|name={1}|MonsterData={2}|level={3}|weapon={4}/{5}|profile={6}",
                    resourceId,
                    name,
                    monsterData,
                    level,
                    productionWeapon.LowId,
                    productionWeapon.HighId,
                    productionProfile.ProfileId);

                resolved = CapturedEnemyCombatContract
                    .EquippedWeaponWithCapturedPacketSequence(
                        productionProfile.Evidence,
                        productionEvidenceSourceIdentity,
                        productionWeapon.LowId,
                        productionWeapon.HighId,
                        productionWeapon.Quality,
                        productionWeapon.InventorySlot,
                        true,
                        0,
                        0,
                        0,
                        productionStream.CapturedAttackRange,
                        0.0d,
                        0.0d,
                        0.0d,
                        0.0d,
                        false,
                        false,
                        productionStream.InitialAmmoCount,
                        productionStream.DamageTypeWire,
                        productionProfile.SpecialAttackWeaponUnknown1,
                        productionProfile.SpecialAttackWeaponUnknown2,
                        productionProfile.SpecialAttackWeaponUnknown3,
                        productionProfile.SpecialAttackWeaponUnknown4,
                        productionProfile.SpecialAttackWeaponUnknown5,
                        productionStream.HitTypeWire,
                        productionStream.N3Unknown,
                        productionProfile.SpecialAttackWeaponN3Unknown,
                        productionProfile.AttackN3Unknown,
                        productionProfile.AttackAction,
                        current.RequiresDamageLineOfSight,
                        true,
                        current.AiProfile)
                    .WithCapturedWeapon(productionWeapon)
                    .WithCapturedSpecialAttackWeaponUnknown5Observations(
                        productionProfile.SpecialAttackWeaponUnknown5Observations)
                    .WithProductionEquippedWeaponValues()
                    .WithCaptureProvenArchetype(productionArchetypeId);
                return resolved.IsCombatReady;
            }

            if (current.UsesProductionWeaponQuality)
            {
                CapturedEnemyCombatProfileDefinition[] compatibleLevelFamily = family.Where(
                    value => value.Level == level
                             && value.SupportsCaptureProvenEquippedWeaponPacketSemantics
                             && value.MatchesStableWeaponFamily(current)).ToArray();
                if (compatibleLevelFamily.Length == 0)
                {
                    return false;
                }

                CapturedEnemyCombatProfileDefinition compatibleArchetype =
                    compatibleLevelFamily[0];
                if (compatibleLevelFamily.Any(
                    value => !compatibleArchetype
                        .MatchesCaptureProvenEquippedWeaponPacketSemantics(value)))
                {
                    return false;
                }

                CapturedEnemyCombatProfileDefinition[] exactWeaponProfiles =
                    compatibleLevelFamily.Where(
                        value => value.MatchesStableWeaponProfile(current)).ToArray();
                CapturedEnemyCombatProfileDefinition productionPacketContext =
                    exactWeaponProfiles.FirstOrDefault(
                        value => value.ContainsSource(sourceIdentityHint))
                    ?? (exactWeaponProfiles.Length == 1
                            ? exactWeaponProfiles[0]
                            : null)
                    ?? compatibleLevelFamily.FirstOrDefault(
                        value => value.ContainsSource(sourceIdentityHint))
                    ?? compatibleLevelFamily.OrderBy(
                        value => value.ProfileId,
                        StringComparer.Ordinal).First();
                int productionEvidenceSourceIdentity =
                    productionPacketContext.ContainsSource(sourceIdentityHint)
                        ? sourceIdentityHint
                        : productionPacketContext.RepresentativeEvidenceSourceIdentity;
                CapturedEnemyWeaponDefinition productionQualityWeapon =
                    productionPacketContext.WeaponDefinition
                        .WithEvidenceSourceIdentity(productionEvidenceSourceIdentity)
                        .WithProductionWeaponQuality(current.WeaponQuality);
                CapturedEnemyCombatProfileStreamDefinition compatibleStream =
                    productionPacketContext.Streams[0];
                string compatibleArchetypeId = string.Format(
                    "resource={0}|name={1}|MonsterData={2}|level={3}|weapon={4}/{5}|profiles={6}",
                    resourceId,
                    name,
                    monsterData,
                    level,
                    current.WeaponLowId,
                    current.WeaponHighId,
                    string.Join(
                        ",",
                        compatibleLevelFamily.Select(value => value.ProfileId)
                            .OrderBy(value => value, StringComparer.Ordinal)));

                resolved = CapturedEnemyCombatContract
                    .EquippedWeaponWithCapturedPacketSequence(
                        productionPacketContext.Evidence,
                        productionEvidenceSourceIdentity,
                        current.WeaponLowId,
                        current.WeaponHighId,
                        current.WeaponQuality,
                        current.WeaponInventorySlot,
                        true,
                        0,
                        0,
                        0,
                        compatibleStream.CapturedAttackRange,
                        0.0d,
                        0.0d,
                        0.0d,
                        0.0d,
                        false,
                        false,
                        compatibleStream.InitialAmmoCount,
                        compatibleStream.DamageTypeWire,
                        productionPacketContext.SpecialAttackWeaponUnknown1,
                        productionPacketContext.SpecialAttackWeaponUnknown2,
                        productionPacketContext.SpecialAttackWeaponUnknown3,
                        productionPacketContext.SpecialAttackWeaponUnknown4,
                        productionPacketContext.SpecialAttackWeaponUnknown5,
                        compatibleStream.HitTypeWire,
                        compatibleStream.N3Unknown,
                        productionPacketContext.SpecialAttackWeaponN3Unknown,
                        productionPacketContext.AttackN3Unknown,
                        productionPacketContext.AttackAction,
                        current.RequiresDamageLineOfSight,
                        true,
                        current.AiProfile)
                    .WithCapturedWeapon(productionQualityWeapon)
                    .WithCapturedSpecialAttackWeaponUnknown5Observations(
                        productionPacketContext.SpecialAttackWeaponUnknown5Observations)
                    .WithCaptureProvenArchetype(compatibleArchetypeId);
                return resolved.IsCombatReady;
            }

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
