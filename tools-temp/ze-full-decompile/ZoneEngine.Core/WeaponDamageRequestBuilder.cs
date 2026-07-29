using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public static class WeaponDamageRequestBuilder
{
	private const int AddAllOffStatId = 276;

	public static WeaponDamageRequestBuildResult Build(WeaponDamageRequestBuildInput input)
	{
		if (input == null)
		{
			input = new WeaponDamageRequestBuildInput();
		}
		if (input.Weapon == null)
		{
			input.Weapon = new WeaponDamageWeaponSnapshot();
		}
		if (input.Attacker == null)
		{
			input.Attacker = new WeaponDamageActorSnapshot();
		}
		if (input.Target == null)
		{
			input.Target = new WeaponDamageActorSnapshot();
		}
		WeaponDamageRequestBuildResult result = new WeaponDamageRequestBuildResult();
		AddCommonProvenance(result, input);
		if (input.IsFixedCapturedDamage)
		{
			BuildFixedCaptured(input, result);
			return result;
		}
		ValidateWeaponTemplate(input, result);
		ValidateDamageRange(input, result);
		ValidateDamageType(input, result);
		ValidateAttackSkills(input, result);
		ResolveAttackerStat(input.Attacker, 276, "Add All Off", result);
		ValidateAmsCap(input, result);
		ValidateArmor(input, result);
		ValidateAddDamage(input, result);
		ValidateCriticalState(input, result);
		BuildDiagnosticRequest(input, result);
		Classify(result);
		return result;
	}

	private static void BuildFixedCaptured(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		result.Classification = WeaponDamageRequestBuildClassification.FixedCaptured;
		result.ExpectedActiveStrategy = DamageCalculationStrategyKind.FixedCapturedDamage;
		result.Request = new DamageCalculationRequest
		{
			Source = new DamageSourceSnapshot
			{
				Category = input.Attacker.Category
			},
			Definition = new DamageDefinition
			{
				FixedDamage = input.FixedCapturedDamage,
				BaseMinimum = input.FixedCapturedDamage,
				BaseMaximum = input.FixedCapturedDamage,
				DamageType = input.Weapon.DamageType,
				EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
			},
			Policy = DamageCalculationPolicy.CapturedFixedDamage(input.CallerName),
			EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
		};
	}

	private static void ValidateWeaponTemplate(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (string.IsNullOrEmpty(input.Weapon.TemplateIdentity))
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingWeaponTemplate, "equipped weapon identity", "no weapon template identity was supplied");
		}
	}

	private static void ValidateDamageRange(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!input.Weapon.HasMinimumDamage)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingMinimum, "minimum damage", "weapon template has no explicit minimum damage stat");
		}
		if (!input.Weapon.HasMaximumDamage)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingMaximum, "maximum damage", "weapon template has no explicit maximum damage stat");
		}
		if (input.Weapon.HasMinimumDamage && input.Weapon.HasMaximumDamage && input.Weapon.MinimumDamage > input.Weapon.MaximumDamage)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MinimumGreaterThanMaximum, "minimum damage", "minimum damage is greater than maximum damage");
		}
	}

	private static void ValidateDamageType(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!input.Weapon.HasDamageType)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingDamageType, "damage type", "weapon template has no explicit damage type stat");
		}
		else if (input.Weapon.DamageType == DamageType.Unknown)
		{
			AddIssue(result, WeaponDamageInputIssueKind.UnknownDamageType, "damage type", "damage type stat does not map to a supported calculator damage type");
		}
	}

	private static void ValidateAttackSkills(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (input.Weapon.AttackSkillContributions.Count == 0)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAttackSkill, "attack skills", "weapon template has no attack-skill contribution entries");
			return;
		}
		int num = 0;
		foreach (AttackSkillContribution attackSkillContribution in input.Weapon.AttackSkillContributions)
		{
			num += attackSkillContribution.Percentage;
			if (attackSkillContribution.StatId <= 0)
			{
				AddIssue(result, WeaponDamageInputIssueKind.UnknownAttackStat, "attack skills", "attack-skill stat id is missing or unsupported");
				continue;
			}
			if (attackSkillContribution.Percentage <= 0 || attackSkillContribution.Percentage > 100)
			{
				AddIssue(result, WeaponDamageInputIssueKind.InvalidSkillWeight, "attack skills", "attack-skill percentage must be between 1 and 100");
			}
			ResolveAttackerStat(input.Attacker, attackSkillContribution.StatId, "attack skill " + attackSkillContribution.StatId, result);
		}
		if (num != 100)
		{
			AddIssue(result, WeaponDamageInputIssueKind.InvalidSkillWeight, "attack skills", "attack-skill percentages must total 100 for formula readiness");
		}
	}

	private static void ValidateAmsCap(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!input.Weapon.HasAmsCap)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAmsCapSemantics, "AMS cap", "weapon AMS cap is absent; absence is not proven equivalent to unlimited");
			return;
		}
		if (input.Weapon.AmsCap == 0)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAmsCapSemantics, "AMS cap", "zero AMS cap semantics are not proven");
		}
		if (input.Weapon.AmsCap < 0)
		{
			AddIssue(result, WeaponDamageInputIssueKind.NegativeAmsCap, "AMS cap", "negative AMS cap is malformed for formula request construction");
		}
	}

	private static void ValidateArmor(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!DamageCalculator.TryGetArmorStatForDamageType(input.Weapon.DamageType, out var statId))
		{
			AddIssue(result, WeaponDamageInputIssueKind.UnknownArmorMapping, "matching armor", "no proven armor stat mapping exists for the selected damage type");
		}
		else
		{
			ResolveTargetStat(input.Target, statId, "matching armor", WeaponDamageInputIssueKind.MissingArmorStat, result);
		}
	}

	private static void ValidateAddDamage(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!DamageCalculator.TryGetAddDamageStatForDamageType(input.Weapon.DamageType, out var statId))
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAddDamageSource, "type-specific add damage", "no proven type-specific add-damage stat mapping exists for the selected damage type");
		}
		else
		{
			ResolveAttackerStat(input.Attacker, statId, "type-specific add damage", result);
		}
		if (!input.HasUniversalAddDamageSource)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAddDamageSource, "universal add damage", "no universal weapon add-damage stat contract is proven in this repository");
		}
	}

	private static void ValidateCriticalState(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		if (!input.HasCriticalState)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingCriticalState, "critical state", "current ordinary weapon callers do not provide a resolved normal/critical state");
		}
		if (input.IsCritical && !input.Weapon.HasCriticalBonus)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingCriticalBonus, "critical bonus", "critical hit requested without a proven critical bonus input");
		}
	}

	private static int? ResolveAttackerStat(WeaponDamageActorSnapshot actor, int statId, string inputName, WeaponDamageRequestBuildResult result)
	{
		IList<WeaponDamageStatSnapshot> list = actor.Stats.Where((WeaponDamageStatSnapshot x) => x.StatId == statId).ToList();
		if (list.Count == 0)
		{
			AddIssue(result, WeaponDamageInputIssueKind.MissingAttackerStat, inputName, "attacker stat " + statId + " is not available");
			return null;
		}
		if (list.Count > 1)
		{
			AddIssue(result, WeaponDamageInputIssueKind.DuplicateAttackerStat, inputName, "attacker stat " + statId + " has duplicate entries");
			return null;
		}
		return list[0].Value;
	}

	private static int? ResolveTargetStat(WeaponDamageActorSnapshot actor, int statId, string inputName, WeaponDamageInputIssueKind missingKind, WeaponDamageRequestBuildResult result)
	{
		IList<WeaponDamageStatSnapshot> list = actor.Stats.Where((WeaponDamageStatSnapshot x) => x.StatId == statId).ToList();
		if (list.Count == 0)
		{
			AddIssue(result, missingKind, inputName, "target stat " + statId + " is not available");
			return null;
		}
		if (list.Count > 1)
		{
			AddIssue(result, WeaponDamageInputIssueKind.DuplicateAttackerStat, inputName, "target stat " + statId + " has duplicate entries");
			return null;
		}
		return list[0].Value;
	}

	private static void BuildDiagnosticRequest(WeaponDamageRequestBuildInput input, WeaponDamageRequestBuildResult result)
	{
		DamageCalculationRequest damageCalculationRequest = new DamageCalculationRequest
		{
			Source = new DamageSourceSnapshot
			{
				Category = input.Attacker.Category,
				Identity = input.Attacker.Identity
			},
			Definition = new DamageDefinition
			{
				BaseMinimum = input.Weapon.MinimumDamage,
				BaseMaximum = input.Weapon.MaximumDamage,
				CriticalBonus = input.Weapon.CriticalBonus,
				DamageType = input.Weapon.DamageType,
				HasAttackRatingCap = input.Weapon.HasAmsCap,
				AttackRatingCap = input.Weapon.AmsCap,
				HasCriticalState = input.HasCriticalState,
				IsCritical = input.IsCritical,
				HasCriticalBonus = input.Weapon.HasCriticalBonus
			},
			Policy = DamageCalculationPolicy.EvidenceBackedWeaponFormula(input.CallerName),
			EvidenceClassification = DamageEvidenceClassification.Unknown
		};
		foreach (AttackSkillContribution attackSkillContribution in input.Weapon.AttackSkillContributions)
		{
			int? num = ResolveStatValueOnly(input.Attacker, attackSkillContribution.StatId);
			damageCalculationRequest.Source.AttackSkillContributions.Add(new AttackSkillContribution
			{
				StatId = attackSkillContribution.StatId,
				Percentage = attackSkillContribution.Percentage,
				Value = (num.HasValue ? num.Value : attackSkillContribution.Value)
			});
		}
		int? num2 = ResolveStatValueOnly(input.Attacker, 276);
		damageCalculationRequest.Source.AddAllOff = (num2.HasValue ? num2.Value : 0);
		if (DamageCalculator.TryGetArmorStatForDamageType(input.Weapon.DamageType, out var statId))
		{
			int? num3 = ResolveStatValueOnly(input.Target, statId);
			if (num3.HasValue)
			{
				damageCalculationRequest.Mitigation.HasMatchingArmor = true;
				damageCalculationRequest.Mitigation.MatchingArmor = num3.Value;
			}
		}
		if (DamageCalculator.TryGetAddDamageStatForDamageType(input.Weapon.DamageType, out var statId2))
		{
			int? num4 = ResolveStatValueOnly(input.Attacker, statId2);
			damageCalculationRequest.Modifiers.TypeSpecificAddDamage = (num4.HasValue ? num4.Value : 0);
		}
		if (input.HasUniversalAddDamageSource)
		{
			damageCalculationRequest.Modifiers.UniversalAddDamage = input.UniversalAddDamage;
		}
		result.Request = damageCalculationRequest;
		result.ExpectedActiveStrategy = DamageCalculationStrategyKind.LegacyFallback;
	}

	private static int? ResolveStatValueOnly(WeaponDamageActorSnapshot actor, int statId)
	{
		IList<WeaponDamageStatSnapshot> list = actor.Stats.Where((WeaponDamageStatSnapshot x) => x.StatId == statId).ToList();
		if (list.Count == 1)
		{
			return list[0].Value;
		}
		return null;
	}

	private static void AddCommonProvenance(WeaponDamageRequestBuildResult result, WeaponDamageRequestBuildInput input)
	{
		AddProvenance(result, "minimum damage", "ItemTemplate.Stats", "items.dat", "StatIds.mindamage / 286", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(286)", "int", "signed", "StatNamesDefaults fallback if not explicit", "formula builder reports missing explicit stat", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasMinimumDamage ? "available" : "missing", input.Weapon.MinimumDamage);
		AddProvenance(result, "maximum damage", "ItemTemplate.Stats", "items.dat", "StatIds.maxdamage / 285", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(285)", "int", "signed", "StatNamesDefaults fallback if not explicit", "formula builder reports missing explicit stat", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasMaximumDamage ? "available" : "missing", input.Weapon.MaximumDamage);
		AddProvenance(result, "legacy DamageBonus", "ItemTemplate.Stats or character stats", "items.dat or character stats table", "StatIds.damagebonus / 284", "Item.GetAttribute or character.Stats", "legacy CombatDamageRules facade", "damageBonus parameter", "int", "signed then clamped by legacy rule", "missing item stat falls back through StatNamesDefaults", "kept separate from AO add damage", "stat-list duplicates surface through Single-based accessors", DamageEvidenceClassification.ProvenRepositoryBehavior, "active legacy only", null);
		AddProvenance(result, "damage type", "ItemTemplate.Stats", "items.dat", "StatIds.damagetype / 436", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(436)", "int enum", "signed", "missing explicit stat is not formula-ready", "formula builder reports missing damage type", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasDamageType ? "available" : "missing", input.Weapon.RawDamageTypeStat);
		AddProvenance(result, "attack skills", "ItemTemplate.Attack dictionary", "items.dat", "attack stat id -> percentage", "ItemLoader.CacheAllItems", "not used by active combat callers", "template.Attack", "int/int", "signed", "missing dictionary entries are absent", "formula builder reports missing attack skill", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenDatabaseContract, (input.Weapon.AttackSkillContributions.Count > 0) ? "available diagnostic-only" : "missing", input.Weapon.AttackSkillContributions.Count);
		AddProvenance(result, "Add All Off", "character stat", "character stats table plus runtime modifiers", "AMSModifier / 276", "Stats.ReadStatsfromSql -> Stat.Value", "attacker stat owner", "character.Stats[276].Value", "int", "signed calculated", "stat default exists in Stats list", "builder reports missing supplied stat snapshot", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenRepositoryBehavior, "not used by active damage", null);
		AddProvenance(result, "AMS cap", "ItemTemplate.Stats", "items.dat", "AMSCap / 538", "ItemLoader.CacheAllItems -> Item.GetAttribute", "not used by active combat callers", "weapon.GetAttribute(538)", "int", "signed", "zero/absence semantics unresolved", "builder reports missing or zero cap semantics", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenDatabaseContract, input.Weapon.HasAmsCap ? "available diagnostic-only" : "missing", input.Weapon.AmsCap);
		AddProvenance(result, "matching armor", "target stats", "character stats table plus runtime modifiers", "damage-type AC stat mapping", "Stats.ReadStatsfromSql -> Stat.Value", "target stat owner", "target.Stats[armorStat].Value", "int", "signed calculated", "missing is not proven zero", "builder reports missing armor stat", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenDatabaseContract, "not used by active damage", null);
		AddProvenance(result, "type-specific add damage", "attacker stats", "character stats table plus runtime modifiers", "damage-type add-damage stat mapping", "Stats.ReadStatsfromSql -> Stat.Value", "attacker stat owner", "attacker.Stats[addDamageStat].Value", "int", "signed calculated", "missing is not proven zero", "builder reports missing add-damage source", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenDatabaseContract, "not used by active damage", null);
		AddProvenance(result, "universal add damage", "unknown", "unknown", "none proven", "none", "none", "none", "unknown", "unknown", "no default allowed", "builder reports missing add-damage source", "unknown", DamageEvidenceClassification.Unknown, "unavailable", null);
		AddProvenance(result, "critical state", "unknown active caller state", "none", "normal/critical resolution", "none", "combat hit resolution seam", "not supplied by ordinary callers", "bool", "n/a", "no random criticals introduced", "builder reports missing critical state", "n/a", DamageEvidenceClassification.Unknown, input.HasCriticalState ? "supplied diagnostic-only" : "missing", input.HasCriticalState ? 1 : 0);
	}

	private static void AddProvenance(WeaponDamageRequestBuildResult result, string inputName, string storageSource, string databaseSource, string fieldOrStatId, string loadPath, string runtimeOwner, string lookupPath, string dataType, string signedness, string defaultBehavior, string missingDataBehavior, string duplicateDataBehavior, DamageEvidenceClassification evidenceClassification, string valueState, int? resolvedValue)
	{
		result.Provenance.Add(new WeaponDamageInputProvenance
		{
			InputName = inputName,
			StorageSource = storageSource,
			DatabaseSource = databaseSource,
			FieldOrStatId = fieldOrStatId,
			LoadPath = loadPath,
			RuntimeOwner = runtimeOwner,
			LookupPath = lookupPath,
			DataType = dataType,
			Signedness = signedness,
			DefaultBehavior = defaultBehavior,
			MissingDataBehavior = missingDataBehavior,
			DuplicateDataBehavior = duplicateDataBehavior,
			EvidenceClassification = evidenceClassification,
			ActiveCallerAvailability = valueState,
			ValueState = valueState,
			ResolvedValue = resolvedValue
		});
	}

	private static void AddIssue(WeaponDamageRequestBuildResult result, WeaponDamageInputIssueKind kind, string inputName, string detail)
	{
		result.Issues.Add(new WeaponDamageInputIssue
		{
			Kind = kind,
			InputName = inputName,
			Detail = detail,
			EvidenceClassification = DamageEvidenceClassification.Unknown
		});
	}

	private static void Classify(WeaponDamageRequestBuildResult result)
	{
		if (result.HasIssue(WeaponDamageInputIssueKind.MinimumGreaterThanMaximum) || result.HasIssue(WeaponDamageInputIssueKind.NegativeAmsCap) || result.HasIssue(WeaponDamageInputIssueKind.DuplicateAttackerStat))
		{
			result.Classification = WeaponDamageRequestBuildClassification.MalformedData;
		}
		else if (result.HasIssue(WeaponDamageInputIssueKind.MissingWeaponTemplate))
		{
			result.Classification = WeaponDamageRequestBuildClassification.LegacyRequired;
		}
		else
		{
			result.Classification = ((result.Issues.Count != 0) ? WeaponDamageRequestBuildClassification.FormulaInputIncomplete : WeaponDamageRequestBuildClassification.FormulaInputComplete);
		}
	}
}
