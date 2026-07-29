using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal static class OrdinaryEnemyProfileValidator
{
	internal static void Validate(IEnumerable<OrdinaryEnemyProfile> profiles, IEnumerable<OrdinaryEnemySpawnDefinition> spawns)
	{
		OrdinaryEnemyProfile[] array = (profiles ?? Enumerable.Empty<OrdinaryEnemyProfile>()).ToArray();
		OrdinaryEnemySpawnDefinition[] array2 = (spawns ?? Enumerable.Empty<OrdinaryEnemySpawnDefinition>()).ToArray();
		Dictionary<string, OrdinaryEnemyProfile> dictionary = new Dictionary<string, OrdinaryEnemyProfile>(StringComparer.Ordinal);
		string text = null;
		OrdinaryEnemyProfile[] array3 = array;
		int num = 0;
		while (num < array3.Length)
		{
			OrdinaryEnemyProfile ordinaryEnemyProfile = array3[num];
			if (ordinaryEnemyProfile == null || string.IsNullOrWhiteSpace(ordinaryEnemyProfile.ProfileKey))
			{
				throw new InvalidOperationException("Ordinary enemy profile key is required.");
			}
			if (text != null && StringComparer.Ordinal.Compare(text, ordinaryEnemyProfile.ProfileKey) >= 0)
			{
				throw new InvalidOperationException("Ordinary enemy profiles must use unique deterministic key ordering.");
			}
			text = ordinaryEnemyProfile.ProfileKey;
			if (dictionary.ContainsKey(ordinaryEnemyProfile.ProfileKey))
			{
				throw new InvalidOperationException("Duplicate ordinary enemy profile key: " + ordinaryEnemyProfile.ProfileKey);
			}
			dictionary.Add(ordinaryEnemyProfile.ProfileKey, ordinaryEnemyProfile);
			if (ordinaryEnemyProfile.BossOrScripted || ordinaryEnemyProfile.OwnedSummon)
			{
				throw new InvalidOperationException("Bosses, scripted encounters, and owned summons cannot enter the ordinary enemy catalog: " + ordinaryEnemyProfile.ProfileKey);
			}
			if (ordinaryEnemyProfile.ConstructionMode == OrdinaryEnemyConstructionMode.Unresolved || ordinaryEnemyProfile.Appearance == null || ordinaryEnemyProfile.Aggression == null || ordinaryEnemyProfile.Aggression.Mode == OrdinaryEnemyAggressionMode.Unresolved || ordinaryEnemyProfile.Combat == null || ordinaryEnemyProfile.Combat.Contract == null || ordinaryEnemyProfile.Loot == null || ordinaryEnemyProfile.Loot.Evidence == OrdinaryEnemyLootEvidence.Invalid || ordinaryEnemyProfile.Corpse == null)
			{
				throw new InvalidOperationException("Ordinary enemy profile has an unresolved required runtime component: " + ordinaryEnemyProfile.ProfileKey);
			}
			if ((ordinaryEnemyProfile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked && string.IsNullOrWhiteSpace(ordinaryEnemyProfile.TemplateHash)) || ordinaryEnemyProfile.Corpse.EmptyLifetimeSeconds <= 0.0 || ordinaryEnemyProfile.Corpse.UnlootedLifetimeSeconds <= 0.0 || ordinaryEnemyProfile.Corpse.LootedCleanupSeconds <= 0.0 || (ordinaryEnemyProfile.Corpse.CapturedCatMesh.HasValue && (ordinaryEnemyProfile.Corpse.CapturedCatMesh.Value <= 0 || ordinaryEnemyProfile.Corpse.CapturedCatMesh.Value == 1234567890 || string.IsNullOrWhiteSpace(ordinaryEnemyProfile.Corpse.VisualEvidence))) || (!ordinaryEnemyProfile.Corpse.CapturedCatMesh.HasValue && !string.IsNullOrWhiteSpace(ordinaryEnemyProfile.Corpse.VisualEvidence)))
			{
				throw new InvalidOperationException("Ordinary enemy construction or corpse lifecycle data is invalid: " + ordinaryEnemyProfile.ProfileKey);
			}
			ValidateLootProfile(ordinaryEnemyProfile.ProfileKey, ordinaryEnemyProfile.Loot);
			if (ordinaryEnemyProfile.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto && (!ordinaryEnemyProfile.Aggression.AutomaticAggroRadius.HasValue || ordinaryEnemyProfile.Aggression.AutomaticAggroRadius.Value <= 0.0))
			{
				throw new InvalidOperationException("Automatic aggression requires a positive captured radius: " + ordinaryEnemyProfile.ProfileKey);
			}
			OrdinaryEnemySupportNanoProfile supportNano = ordinaryEnemyProfile.SupportNano;
			bool flag = supportNano != null && !supportNano.HasPeriodicStatHit;
			bool flag2 = supportNano?.HasPeriodicStatHit ?? false;
			if (!flag)
			{
				goto IL_0414;
			}
			bool num2;
			if (!supportNano.ResolvePrimaryModifierFromNanoData)
			{
				if (supportNano.HasTriggeredSelfEffect && supportNano.PrimaryNanoId != supportNano.TriggeredSelfNanoId && supportNano.PrimaryModifierDelta != 0 && supportNano.TriggeredSelfModifierDelta != 0 && supportNano.AffectedStatIds.Length != 0 && !supportNano.AffectedStatIds.Any((int value) => value <= 0))
				{
					num2 = supportNano.AffectedStatIds.Distinct().Count() != supportNano.AffectedStatIds.Length;
					goto IL_0412;
				}
			}
			else if (!supportNano.HasTriggeredSelfEffect && supportNano.PrimaryModifierDelta == 0 && supportNano.TriggeredSelfModifierDelta == 0)
			{
				num2 = supportNano.AffectedStatIds.Length != 0;
				goto IL_0412;
			}
			goto IL_0447;
			IL_0448:
			int num3;
			bool flag3 = (byte)num3 != 0;
			bool flag4 = flag2 && (supportNano.ResolvePrimaryModifierFromNanoData || supportNano.HasTriggeredSelfEffect || supportNano.PeriodicStatId != 214 || supportNano.TriggeredSelfStrain != 0 || supportNano.PrimaryModifierDelta != 0 || supportNano.TriggeredSelfModifierDelta != 0 || supportNano.AffectedStatIds.Length != 0 || supportNano.PeriodicStatDelta <= 0 || supportNano.PeriodicTickCount <= 0 || supportNano.PeriodicTickSeconds <= 0.0);
			bool flag5 = supportNano?.SpawnNanoPoolByLevel.Any((KeyValuePair<int, int> value) => value.Key <= 0 || value.Value <= 0) ?? false;
			if (supportNano != null && (supportNano.PrimaryNanoId <= 0 || supportNano.InitialDelaySeconds < 0.0 || supportNano.CastSeconds <= 0.0 || supportNano.RepeatSeconds <= supportNano.CastSeconds || supportNano.DurationParameter <= 0 || supportNano.EffectLifetimeSeconds <= 0.0 || supportNano.TargetRange <= 0.0 || supportNano.PrimaryStrain < 0 || supportNano.TriggeredSelfStrain < 0 || supportNano.NanoCost < 0 || supportNano.NcuCost < 0 || supportNano.CastChanceBasisPoints <= 0 || supportNano.CastChanceBasisPoints > 10000 || supportNano.SelfTargetChanceBasisPoints < 0 || supportNano.SelfTargetChanceBasisPoints > 10000 || flag3 || flag4 || flag5 || supportNano.EvidenceState == OrdinaryEnemyEvidenceState.Invalid || supportNano.EvidenceState == OrdinaryEnemyEvidenceState.Unresolved || string.IsNullOrWhiteSpace(supportNano.Evidence)))
			{
				throw new InvalidOperationException("Ordinary enemy support nano data is invalid: " + ordinaryEnemyProfile.ProfileKey);
			}
			bool hasValue = ordinaryEnemyProfile.Combat.HealthRegenIntervalSeconds.HasValue;
			bool hasValue2 = ordinaryEnemyProfile.Combat.HealthRegenDelta.HasValue;
			if (hasValue != hasValue2 || (hasValue && (ordinaryEnemyProfile.Combat.HealthRegenIntervalSeconds.Value <= 0.0 || ordinaryEnemyProfile.Combat.HealthRegenDelta.Value <= 0)) || (ordinaryEnemyProfile.Combat.RegenerateHealthWhileInCombat && !hasValue))
			{
				throw new InvalidOperationException("Ordinary enemy health regeneration data is invalid: " + ordinaryEnemyProfile.ProfileKey);
			}
			if (ordinaryEnemyProfile.Aggression.Mode == OrdinaryEnemyAggressionMode.Scripted || ordinaryEnemyProfile.Combat.Mode == OrdinaryEnemyCombatMode.Scripted)
			{
				throw new InvalidOperationException("Scripted behavior must use a custom encounter module: " + ordinaryEnemyProfile.ProfileKey);
			}
			num++;
			continue;
			IL_0414:
			num3 = ((flag && (supportNano.PeriodicStatDelta != 0 || supportNano.PeriodicTickCount != 0 || supportNano.PeriodicTickSeconds != 0.0)) ? 1 : 0);
			goto IL_0448;
			IL_0447:
			num3 = 1;
			goto IL_0448;
			IL_0412:
			if (!num2)
			{
				goto IL_0414;
			}
			goto IL_0447;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		HashSet<int> hashSet2 = new HashSet<int>();
		int num4 = int.MinValue;
		OrdinaryEnemySpawnDefinition[] array4 = array2;
		foreach (OrdinaryEnemySpawnDefinition ordinaryEnemySpawnDefinition in array4)
		{
			if (ordinaryEnemySpawnDefinition == null || string.IsNullOrWhiteSpace(ordinaryEnemySpawnDefinition.SpawnKey))
			{
				throw new InvalidOperationException("Ordinary enemy spawn key is required.");
			}
			if (!hashSet.Add(ordinaryEnemySpawnDefinition.SpawnKey))
			{
				throw new InvalidOperationException("Duplicate ordinary enemy spawn key: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if (ordinaryEnemySpawnDefinition.SourceIdentity <= 0 || !hashSet2.Add(ordinaryEnemySpawnDefinition.SourceIdentity))
			{
				throw new InvalidOperationException("Duplicate or invalid ordinary enemy source identity: " + ordinaryEnemySpawnDefinition.SourceIdentity);
			}
			if (ordinaryEnemySpawnDefinition.SourceIdentity <= num4)
			{
				throw new InvalidOperationException("Ordinary enemy spawns must use deterministic numeric identity ordering.");
			}
			num4 = ordinaryEnemySpawnDefinition.SourceIdentity;
			if (!dictionary.ContainsKey(ordinaryEnemySpawnDefinition.ProfileKey))
			{
				throw new InvalidOperationException("Ordinary enemy spawn references a missing profile: " + ordinaryEnemySpawnDefinition.ProfileKey);
			}
			if (ordinaryEnemySpawnDefinition.PlayfieldInstance <= 0 || ordinaryEnemySpawnDefinition.Disposition == OrdinaryEnemyRuntimeDisposition.Invalid || ordinaryEnemySpawnDefinition.MovementMode == OrdinaryEnemyMovementMode.Unresolved || !string.IsNullOrEmpty(ordinaryEnemySpawnDefinition.SourceOwnerIdentity))
			{
				throw new InvalidOperationException("Ordinary enemy spawn has an invalid playfield, disposition, or owner: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if ((ordinaryEnemySpawnDefinition.MovementMode == OrdinaryEnemyMovementMode.Patrol || ordinaryEnemySpawnDefinition.MovementMode == OrdinaryEnemyMovementMode.Roam) && ordinaryEnemySpawnDefinition.Waypoints.Length < 2 && !ordinaryEnemySpawnDefinition.UseCapturedPatrolReplay)
			{
				throw new InvalidOperationException("Patrol and roam spawns require captured movement data: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if (ordinaryEnemySpawnDefinition.MovementMode == OrdinaryEnemyMovementMode.Scripted)
			{
				throw new InvalidOperationException("Scripted movement must use a custom encounter module: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if ((ordinaryEnemySpawnDefinition.RespawnEvidence == OrdinaryEnemyEvidenceState.Observed || ordinaryEnemySpawnDefinition.RespawnEvidence == OrdinaryEnemyEvidenceState.Policy) && !ordinaryEnemySpawnDefinition.HasRespawnDelay)
			{
				throw new InvalidOperationException("Observed or policy respawn requires a positive delay: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if (ordinaryEnemySpawnDefinition.LevelDefinition == null || !ordinaryEnemySpawnDefinition.LevelDefinition.IsValid)
			{
				throw new InvalidOperationException("Ordinary enemy spawn level definition is invalid: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if (!ordinaryEnemySpawnDefinition.LevelDefinition.ContainsSourceRow(ordinaryEnemySpawnDefinition.Level, ordinaryEnemySpawnDefinition.Health, ordinaryEnemySpawnDefinition.HealthDamage, ordinaryEnemySpawnDefinition.MonsterScale, ordinaryEnemySpawnDefinition.RunSpeed))
			{
				throw new InvalidOperationException("Ordinary enemy spawn level definition does not preserve its source row: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
			if (ordinaryEnemySpawnDefinition.RespawnPolicy == null || !Enum.IsDefined(typeof(WorldRespawnPolicyAssignmentMode), ordinaryEnemySpawnDefinition.RespawnPolicy.Mode) || ordinaryEnemySpawnDefinition.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Invalid || ordinaryEnemySpawnDefinition.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Unresolved || (ordinaryEnemySpawnDefinition.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.NoRespawn && string.IsNullOrWhiteSpace(ordinaryEnemySpawnDefinition.RespawnPolicy.PolicyKey)) || (ordinaryEnemySpawnDefinition.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit && !WorldRespawnPolicyValidator.IsSchedulable(ordinaryEnemySpawnDefinition.RespawnPolicy.ExplicitPolicy)))
			{
				throw new InvalidOperationException("Ordinary enemy spawn respawn policy is invalid: " + ordinaryEnemySpawnDefinition.SpawnKey);
			}
		}
	}

	internal static void ValidateLootProfile(string profileKey, OrdinaryEnemyLootProfile loot)
	{
		string text = (string.IsNullOrWhiteSpace(profileKey) ? "<unknown>" : profileKey);
		if (loot == null || loot.Evidence == OrdinaryEnemyLootEvidence.Invalid || loot.PoolMode == OrdinaryEnemyLootPoolMode.Invalid || loot.EmptyWeight < 0 || loot.ObservedCompleteInventories < 0 || loot.ObservedEmptyInventories < 0 || loot.ObservedEmptyInventories > loot.ObservedCompleteInventories)
		{
			throw new InvalidOperationException("Ordinary enemy loot profile is invalid: " + text);
		}
		OrdinaryEnemyLootEntry[] array = loot.Entries ?? new OrdinaryEnemyLootEntry[0];
		int[] array2 = loot.ObservedCreditOutcomes ?? new int[0];
		OrdinaryEnemyLevelCreditRule[] source = loot.LevelCreditRules ?? new OrdinaryEnemyLevelCreditRule[0];
		if (source.Any((OrdinaryEnemyLevelCreditRule value) => value == null || value.EnemyLevel <= 0 || value.MinimumCredits < 0 || value.MaximumCredits < value.MinimumCredits || string.IsNullOrWhiteSpace(value.Evidence) || (value.EvidenceState == OrdinaryEnemyEvidenceState.Observed && value.ObservedCorpses <= 0) || (value.EvidenceState == OrdinaryEnemyEvidenceState.Policy && value.ObservedCorpses < 0) || (value.EvidenceState != OrdinaryEnemyEvidenceState.Observed && value.EvidenceState != OrdinaryEnemyEvidenceState.Policy)) || (from value in source
			group value by value.EnemyLevel).Any((IGrouping<int, OrdinaryEnemyLevelCreditRule> value) => value.Count() > 1))
		{
			throw new InvalidOperationException("Ordinary enemy level-credit rules are invalid: " + text);
		}
		if (array2.Length != 0)
		{
			if (loot.CreditEvidence != OrdinaryEnemyEvidenceState.Observed || !loot.MinimumCredits.HasValue || !loot.MaximumCredits.HasValue || loot.MinimumCredits.Value != array2.Min() || loot.MaximumCredits.Value != array2.Max() || array2.Any((int value) => value < 0) || loot.LevelCreditRules.Length != 0 || string.IsNullOrWhiteSpace(loot.CreditEvidenceReference))
			{
				throw new InvalidOperationException("Observed ordinary enemy credit outcomes are invalid: " + text);
			}
		}
		else if (!string.IsNullOrWhiteSpace(loot.CreditEvidenceReference))
		{
			throw new InvalidOperationException("Ordinary enemy credit evidence has no captured outcomes: " + text);
		}
		if (array.Length == 0)
		{
			if (loot.PoolMode != OrdinaryEnemyLootPoolMode.IndependentEntries || loot.EmptyWeight != 0 || (loot.Evidence != OrdinaryEnemyLootEvidence.Unresolved && loot.Evidence != OrdinaryEnemyLootEvidence.NoneProven && loot.Evidence != OrdinaryEnemyLootEvidence.ProfileInherited) || (loot.ItemPoolComplete && loot.Evidence != OrdinaryEnemyLootEvidence.NoneProven))
			{
				throw new InvalidOperationException("Empty ordinary enemy loot profile has active pool semantics: " + text);
			}
			if (loot.ObservedCompleteInventories > 0 && string.IsNullOrWhiteSpace(loot.ItemEvidenceReference))
			{
				throw new InvalidOperationException("Observed empty loot requires an evidence reference: " + text);
			}
			return;
		}
		if (string.IsNullOrWhiteSpace(loot.ItemEvidenceReference) || loot.ObservedCompleteInventories <= 0 || array.Any((OrdinaryEnemyLootEntry value) => value == null || value.LowId <= 0 || value.HighId <= 0 || value.QualityLevel <= 0 || value.Slot < 0 || value.Quantity <= 0 || value.ObservedCount <= 0 || value.ObservedCorpses <= 0 || value.ObservedCorpses > loot.ObservedCompleteInventories || string.IsNullOrWhiteSpace(value.EvidenceReference) || (value.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven && value.Evidence != OrdinaryEnemyLootEvidence.ObservedAvailableLoot) || (value.LinkageEvidence != OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem && value.LinkageEvidence != OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem && value.LinkageEvidence != OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence) || (value.ProbabilityEvidence != OrdinaryEnemyLootProbabilityEvidence.GuaranteedProven && value.ProbabilityEvidence != OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy && value.ProbabilityEvidence != OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy)))
		{
			throw new InvalidOperationException("Ordinary enemy loot entry lacks proven evidence: " + text);
		}
		if (array.GroupBy((OrdinaryEnemyLootEntry value) => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}", value.LowId, value.HighId, value.QualityLevel), StringComparer.Ordinal).Any((IGrouping<string, OrdinaryEnemyLootEntry> value) => value.Count() > 1))
		{
			throw new InvalidOperationException("Duplicate ordinary enemy loot item identity: " + text);
		}
		if (loot.PoolMode == OrdinaryEnemyLootPoolMode.IndependentEntries)
		{
			if (loot.EmptyWeight != 0 || (from value in array
				group value by value.Slot).Any((IGrouping<int, OrdinaryEnemyLootEntry> value) => value.Count() > 1) || array.Any((OrdinaryEnemyLootEntry value) => value.Weight != 0 || value.DropChanceBasisPoints <= 0 || value.DropChanceBasisPoints > 10000))
			{
				throw new InvalidOperationException("Independent ordinary enemy loot semantics are invalid: " + text);
			}
		}
		else
		{
			if (loot.PoolMode != OrdinaryEnemyLootPoolMode.WeightedOne)
			{
				throw new InvalidOperationException("Unsupported ordinary enemy loot pool mode: " + text);
			}
			int slot = array[0].Slot;
			if (array.Any((OrdinaryEnemyLootEntry value) => value.Slot != slot || value.Weight <= 0 || value.DropChanceBasisPoints != 0))
			{
				throw new InvalidOperationException("Weighted ordinary enemy loot semantics are invalid: " + text);
			}
		}
		if (loot.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven || (loot.PoolMode == OrdinaryEnemyLootPoolMode.IndependentEntries && !array.Any((OrdinaryEnemyLootEntry value) => value.Evidence != OrdinaryEnemyLootEvidence.GuaranteedProven || value.DropChanceBasisPoints != 10000 || value.ProbabilityEvidence != OrdinaryEnemyLootProbabilityEvidence.GuaranteedProven)))
		{
			return;
		}
		throw new InvalidOperationException("Observed loot cannot be promoted to guaranteed loot: " + text);
	}
}
