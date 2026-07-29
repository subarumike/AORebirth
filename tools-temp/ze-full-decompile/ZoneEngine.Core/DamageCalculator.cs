using System;
using System.Linq;

namespace ZoneEngine.Core;

public static class DamageCalculator
{
	public static bool TryGetArmorStatForDamageType(DamageType damageType, out int statId)
	{
		switch (damageType)
		{
		case DamageType.Projectile:
			statId = 90;
			return true;
		case DamageType.Melee:
			statId = 91;
			return true;
		case DamageType.Energy:
			statId = 92;
			return true;
		case DamageType.Chemical:
			statId = 93;
			return true;
		case DamageType.Radiation:
			statId = 94;
			return true;
		case DamageType.Cold:
			statId = 95;
			return true;
		case DamageType.Poison:
			statId = 96;
			return true;
		case DamageType.Fire:
			statId = 97;
			return true;
		case DamageType.Nano:
			statId = 168;
			return true;
		default:
			statId = 0;
			return false;
		}
	}

	public static bool TryGetAddDamageStatForDamageType(DamageType damageType, out int statId)
	{
		switch (damageType)
		{
		case DamageType.Projectile:
			statId = 278;
			return true;
		case DamageType.Melee:
			statId = 279;
			return true;
		case DamageType.Energy:
			statId = 280;
			return true;
		case DamageType.Chemical:
			statId = 281;
			return true;
		case DamageType.Radiation:
			statId = 282;
			return true;
		case DamageType.Cold:
			statId = 311;
			return true;
		case DamageType.Nano:
			statId = 315;
			return true;
		case DamageType.Fire:
			statId = 316;
			return true;
		case DamageType.Poison:
			statId = 317;
			return true;
		default:
			statId = 0;
			return false;
		}
	}

	public static DamageCalculationResult Calculate(DamageCalculationRequest request, IDamageRandomSource randomSource)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (randomSource == null)
		{
			throw new ArgumentNullException("randomSource");
		}
		NormalizeRequest(request);
		DamageCalculationResult damageCalculationResult = new DamageCalculationResult();
		damageCalculationResult.EvidenceClassification = request.EvidenceClassification;
		damageCalculationResult.HitOutcome = request.HitOutcome;
		DamageEvidenceClassification damageEvidenceClassification = ResolveEvidence(request);
		damageCalculationResult.Strategy = SelectStrategy(request, out var reason);
		damageCalculationResult.StrategyReason = reason;
		damageCalculationResult.Trace.Add("ValidateRequest", DamageCalculationStageStatus.Applied, 0, 0, damageEvidenceClassification, "request and deterministic random source present");
		damageCalculationResult.Trace.Add("ResolveModeAndPolicy", DamageCalculationStageStatus.Applied, (int)request.Context.Mode, (int)request.Context.Mode, request.Policy.EvidenceClassification, request.Policy.Name);
		damageCalculationResult.Trace.Add("SelectDamageStrategy", (damageCalculationResult.Strategy == DamageCalculationStrategyKind.EvidenceBlocked) ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Applied, 0, (int)damageCalculationResult.Strategy, damageEvidenceClassification, damageCalculationResult.StrategyReason);
		DamageType damageType2 = (damageCalculationResult.SelectedDamageType = ((request.DamageTypeOverride == DamageType.Unknown) ? request.Definition.DamageType : request.DamageTypeOverride));
		damageCalculationResult.Trace.Add("ResolveDamageType", DamageCalculationStageStatus.Applied, (int)request.Definition.DamageType, (int)damageType2, damageEvidenceClassification, "override wins when present");
		if (request.Mitigation.Immune || request.Mitigation.Invulnerable)
		{
			damageCalculationResult.FinalTargetDamage = 0;
			damageCalculationResult.Trace.Add("ResolveImmunity", DamageCalculationStageStatus.Applied, 0, 0, damageEvidenceClassification, "immune or invulnerable target takes no damage");
			return damageCalculationResult;
		}
		if (request.HitOutcome != 0)
		{
			damageCalculationResult.FinalTargetDamage = 0;
			damageCalculationResult.Trace.Add("ResolveHitOutcome", DamageCalculationStageStatus.Applied, 0, 0, damageEvidenceClassification, "non-hit outcome stops damage");
			return damageCalculationResult;
		}
		damageCalculationResult.Trace.Add("ResolveHitOutcome", DamageCalculationStageStatus.Preserved, 1, 1, damageEvidenceClassification, "current repository callers enter only after hit eligibility");
		damageCalculationResult.Trace.Add("ResolveCritical", (request.Definition.IsCritical && !request.Policy.EnableCriticalDamage) ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, (!request.Definition.IsCritical) ? damageEvidenceClassification : DamageEvidenceClassification.Unknown, request.Definition.IsCritical ? "critical formula not proven for migrated callers" : "not critical");
		int num = request.Source.AttackRating + request.Source.AddAllOff;
		if (request.Source.AttackSkillContributions.Count > 0)
		{
			num = request.Source.AttackSkillContributions.Sum((AttackSkillContribution x) => x.Contribution) + request.Source.AddAllOff;
		}
		damageCalculationResult.EffectiveAttackRating = num;
		damageCalculationResult.Trace.Add("ResolveEffectiveAttackRating", DamageCalculationStageStatus.Preserved, request.Source.AttackRating, num, damageEvidenceClassification, (request.Source.AttackSkillContributions.Count > 0) ? "weighted attack-skill contributions are represented but not active for migrated callers" : "current migrated callers do not scale by attack rating");
		if (request.Definition.HasAttackRatingCap)
		{
			if (request.Definition.AttackRatingCap > 0)
			{
				damageCalculationResult.AttackRatingCapResult = Math.Min(num, request.Definition.AttackRatingCap);
				damageCalculationResult.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.Applied, num, damageCalculationResult.AttackRatingCapResult, DamageEvidenceClassification.ControlledTestConfirmed, "cap arithmetic only; scaling remains policy-gated");
			}
			else
			{
				damageCalculationResult.AttackRatingCapResult = num;
				damageCalculationResult.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.EvidenceBlocked, num, num, DamageEvidenceClassification.Unknown, "zero or invalid cap semantics are unresolved");
			}
		}
		else
		{
			damageCalculationResult.AttackRatingCapResult = num;
			damageCalculationResult.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.Skipped, num, num, damageEvidenceClassification, "missing cap preserves effective attack rating");
		}
		damageCalculationResult.Trace.Add("ApplyPre1000AttackRatingScaling", request.Policy.EnableAttackRatingScaling ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, DamageEvidenceClassification.Unknown, "no proven production AR multiplier in migrated callers");
		damageCalculationResult.Trace.Add("ApplyPost1000AttackRatingScaling", request.Policy.EnableAttackRatingScaling ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, DamageEvidenceClassification.Unknown, "profession and NPC post-1000 factors are unresolved");
		int num2 = Math.Max(0, request.Definition.BaseMinimum);
		int num3 = Math.Max(num2, request.Definition.BaseMaximum);
		int num4 = ResolveFallbackFloor(request);
		int num7 = (damageCalculationResult.ScaledBaseDamage = (damageCalculationResult.BaseRoll = ResolveBaseDamage(request, randomSource, num2, num3, num4, damageCalculationResult)));
		damageCalculationResult.FinalAttackRatingMultiplierBasisPoints = 10000;
		damageCalculationResult.Trace.Add("ApplyCriticalContribution", (request.Definition.IsCritical && !request.Policy.EnableCriticalDamage) ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, num7, num7, DamageEvidenceClassification.Unknown, "critical contribution is not proven for migrated callers");
		int num8 = num7;
		if (request.Mitigation.MatchingArmor != 0)
		{
			damageCalculationResult.Trace.Add("ApplyArmorMitigation", request.Policy.EnableArmorMitigation ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.EvidenceBlocked, num8, num8, DamageEvidenceClassification.Unknown, "AC ordering and division are unresolved for migrated production damage");
			if (TryGetArmorStatForDamageType(damageType2, out var statId))
			{
				damageCalculationResult.Trace.Add("ResolveArmorStat", DamageCalculationStageStatus.Applied, (int)damageType2, statId, DamageEvidenceClassification.ProvenDatabaseContract, "damage-type to AC stat mapping only; no AC formula activated");
			}
		}
		else
		{
			damageCalculationResult.Trace.Add("ApplyArmorMitigation", DamageCalculationStageStatus.Skipped, num8, num8, damageEvidenceClassification, "no matching armor supplied by migrated callers");
		}
		bool flag = num3 > 0 && !request.Policy.IsFixedCapturedDamage && request.Definition.FixedDamage <= 0;
		int val = (damageCalculationResult.MinimumDamageFloor = ((!flag) ? num4 : 0));
		int num10 = Math.Max(0, request.Modifiers.LegacyDamageBonus);
		if (num10 == 0 && request.Modifiers.FlatAddDamage != 0)
		{
			num10 = Math.Max(0, request.Modifiers.FlatAddDamage);
		}
		damageCalculationResult.LegacyDamageBonusContribution = num10;
		damageCalculationResult.TypeSpecificAddDamageContribution = request.Modifiers.TypeSpecificAddDamage;
		damageCalculationResult.UniversalAddDamageContribution = request.Modifiers.UniversalAddDamage;
		damageCalculationResult.FlatAddDamageContribution = num10;
		int num11 = num8 + num10;
		damageCalculationResult.Trace.Add("ApplyFlatDamageModifiers", DamageCalculationStageStatus.Preserved, num8, num11, damageEvidenceClassification, "legacy damagebonus is kept separate from type-specific and universal add damage");
		if ((request.Modifiers.TypeSpecificAddDamage != 0 || request.Modifiers.UniversalAddDamage != 0) && TryGetAddDamageStatForDamageType(damageType2, out var statId2))
		{
			damageCalculationResult.Trace.Add("ResolveAddDamageStat", DamageCalculationStageStatus.Applied, (int)damageType2, statId2, DamageEvidenceClassification.ProvenDatabaseContract, "type-specific add-damage stat mapping only; add-damage formula remains inactive");
		}
		damageCalculationResult.Trace.Add("ApplyMinimumDamageFloor", DamageCalculationStageStatus.Preserved, num11, Math.Max(val, num11), damageEvidenceClassification, flag ? "real min/max damage range preserves rolled weapon damage" : "repository legacy fallback floor");
		int num12 = Math.Max(val, num11);
		TraceBlockedIfNeeded(damageCalculationResult, request.Definition.BulletCount > 1 || request.Context.SpecialAttackCategory != SpecialAttackCategory.None, "ResolveSpecialSubHits", num12, "special attack formulas are represented but not active without evidence");
		TraceBlockedIfNeeded(damageCalculationResult, request.Definition.BulletCount > 1, "AggregateSubHits", num12, "multi-hit aggregation ordering is unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Definition.BulletCount > 1, "ApplySpecialCompression", num12, "special compression thresholds are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Definition.AttackSpecificCap > 0, "ApplyAttackSpecificCap", num12, "attack-specific cap ordering is unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Context.Mode == DamageCalculationMode.PvP, "ApplyPvPConversion", num12, "PvP conversion ratio and rounding are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Context.Mode == DamageCalculationMode.PvP, "ApplyPvPMaximumHealthCap", num12, "PvP maximum-health cap semantics are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Mitigation.ReflectPercentage != 0 || request.Mitigation.ReflectCap != 0, "ApplyReflect", num12, "reflect prevention and cap ordering are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Mitigation.TypedAbsorbPool != 0, "ConsumeTypedAbsorbs", num12, "typed absorb ordering and mutation are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Mitigation.UniversalAbsorbPool != 0, "ConsumeUniversalAbsorbs", num12, "universal absorb ordering and mutation are unresolved");
		TraceBlockedIfNeeded(damageCalculationResult, request.Mitigation.ReflectPercentage != 0, "ResolveReflectedReturnDamage", num12, "returned reflect damage is not wired to production events");
		TraceBlockedIfNeeded(damageCalculationResult, request.Mitigation.DamageShield != 0, "ResolveDamageShieldReturnDamage", num12, "damage-shield return events are not wired to production events");
		damageCalculationResult.FinalTargetDamage = ((num12 >= 0) ? num12 : 0);
		damageCalculationResult.Trace.Add("ClampFinalValues", DamageCalculationStageStatus.Applied, num12, damageCalculationResult.FinalTargetDamage, damageEvidenceClassification, "final target damage cannot be negative");
		damageCalculationResult.Trace.Add("ReturnTrace", DamageCalculationStageStatus.Applied, damageCalculationResult.FinalTargetDamage, damageCalculationResult.FinalTargetDamage, damageEvidenceClassification, "side-effect-free result only");
		return damageCalculationResult;
	}

	private static DamageCalculationStrategyKind SelectStrategy(DamageCalculationRequest request, out string reason)
	{
		if (request.Policy.IsFixedCapturedDamage || request.Definition.FixedDamage > 0)
		{
			reason = "fixed captured damage selected; AR, AC, criticals, and add-damage are not applied";
			return DamageCalculationStrategyKind.FixedCapturedDamage;
		}
		if (request.Policy.EnableEvidenceBackedWeaponFormula)
		{
			if (!IsFormulaBackedWeaponRequestComplete(request, out reason))
			{
				return DamageCalculationStrategyKind.EvidenceBlocked;
			}
			reason = "formula-ready request shape is present, but production AO weapon ordering is not proven in this repository";
			return DamageCalculationStrategyKind.EvidenceBlocked;
		}
		reason = "legacy repository damage selected because AO weapon formula evidence is incomplete";
		return DamageCalculationStrategyKind.LegacyFallback;
	}

	private static bool IsFormulaBackedWeaponRequestComplete(DamageCalculationRequest request, out string reason)
	{
		if (request.Definition.BaseMinimum < 0 || request.Definition.BaseMaximum <= 0)
		{
			reason = "missing or invalid weapon min/max damage";
			return false;
		}
		if (request.Definition.DamageType == DamageType.Unknown)
		{
			reason = "missing proven weapon damage type";
			return false;
		}
		if (request.Source.AttackRating == 0 && request.Source.AttackSkillContributions.Count == 0)
		{
			reason = "missing proven effective attack rating or weighted skill contributions";
			return false;
		}
		if (!request.Mitigation.HasMatchingArmor)
		{
			reason = "missing explicit target matching AC input; zero AC must be supplied as known zero";
			return false;
		}
		if (!request.Definition.HasCriticalState)
		{
			reason = "missing resolved critical state";
			return false;
		}
		if (request.Definition.IsCritical && !request.Definition.HasCriticalBonus)
		{
			reason = "critical hit is requested without a proven critical bonus input";
			return false;
		}
		reason = string.Empty;
		return true;
	}

	private static void NormalizeRequest(DamageCalculationRequest request)
	{
		if (request.Context == null)
		{
			request.Context = new DamageCalculationContext();
		}
		if (request.Source == null)
		{
			request.Source = new DamageSourceSnapshot();
		}
		if (request.Target == null)
		{
			request.Target = new DamageTargetSnapshot();
		}
		if (request.Definition == null)
		{
			request.Definition = new DamageDefinition();
		}
		if (request.Modifiers == null)
		{
			request.Modifiers = new DamageModifierSet();
		}
		if (request.Mitigation == null)
		{
			request.Mitigation = new DamageMitigationSet();
		}
		if (request.Policy == null)
		{
			request.Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(isPlayer: false);
		}
	}

	private static DamageEvidenceClassification ResolveEvidence(DamageCalculationRequest request)
	{
		if (request.EvidenceClassification != 0)
		{
			return request.EvidenceClassification;
		}
		if (request.Definition.EvidenceClassification != 0)
		{
			return request.Definition.EvidenceClassification;
		}
		return request.Policy.EvidenceClassification;
	}

	private static int ResolveFallbackFloor(DamageCalculationRequest request)
	{
		return (request.Source.Category == DamageSourceCategory.Player) ? request.Policy.PlayerFallbackDamage : request.Policy.NpcFallbackDamage;
	}

	private static int ResolveBaseDamage(DamageCalculationRequest request, IDamageRandomSource randomSource, int normalizedMinimum, int normalizedMaximum, int fallbackFloor, DamageCalculationResult result)
	{
		if (request.Policy.IsFixedCapturedDamage || request.Definition.FixedDamage > 0)
		{
			int num = ((request.Definition.FixedDamage > 0) ? request.Definition.FixedDamage : normalizedMaximum);
			result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Applied, num, num, DamageEvidenceClassification.ProvenCapturedBehavior, "fixed captured damage bypasses unproven AR and AC formulas");
			return num;
		}
		if (normalizedMaximum > 0)
		{
			int num2 = ((normalizedMinimum == normalizedMaximum) ? normalizedMaximum : randomSource.NextInclusive(normalizedMinimum, normalizedMaximum));
			result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Preserved, normalizedMinimum, num2, DamageEvidenceClassification.ProvenRepositoryBehavior, "inclusive repository min/max roll");
			return num2;
		}
		int num3 = Math.Max(fallbackFloor, request.Source.Level);
		result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Preserved, request.Source.Level, num3, DamageEvidenceClassification.ProvenRepositoryBehavior, "repository level fallback when no max damage exists");
		return num3;
	}

	private static void TraceBlockedIfNeeded(DamageCalculationResult result, bool condition, string stage, int currentDamage, string note)
	{
		if (condition)
		{
			result.Trace.Add(stage, DamageCalculationStageStatus.EvidenceBlocked, currentDamage, currentDamage, DamageEvidenceClassification.Unknown, note);
		}
		else
		{
			result.Trace.Add(stage, DamageCalculationStageStatus.Skipped, currentDamage, currentDamage, DamageEvidenceClassification.ProvenRepositoryBehavior, "not active for migrated repository behavior");
		}
	}
}
