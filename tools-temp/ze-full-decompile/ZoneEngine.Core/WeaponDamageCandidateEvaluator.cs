using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public static class WeaponDamageCandidateEvaluator
{
	public static WeaponDamageCandidateEvaluation Evaluate(WeaponDamageObservation observation, WeaponDamageCandidateFormula formula)
	{
		WeaponDamageCandidateEvaluation weaponDamageCandidateEvaluation = new WeaponDamageCandidateEvaluation
		{
			FormulaName = ((formula == null) ? string.Empty : formula.Name)
		};
		if (observation == null || formula == null)
		{
			weaponDamageCandidateEvaluation.UnknownInputs.Add("observation or formula missing");
			return weaponDamageCandidateEvaluation;
		}
		WeaponDamageObservationInput input = observation.Input;
		Require(input.BaseRoll, "base roll", weaponDamageCandidateEvaluation);
		Require(input.AttackRating, "attack rating", weaponDamageCandidateEvaluation);
		Require(input.AddAllOff, "Add All Off", weaponDamageCandidateEvaluation);
		Require(input.TargetArmor, "target armor", weaponDamageCandidateEvaluation);
		Require(input.WeaponMinimum, "weapon minimum", weaponDamageCandidateEvaluation);
		Require(observation.Result.ObservedDamage, "observed damage", weaponDamageCandidateEvaluation);
		if (observation.Result.HitKind == WeaponDamageHitKind.KnownCritical)
		{
			Require(input.CriticalBonus, "critical bonus", weaponDamageCandidateEvaluation);
		}
		if (weaponDamageCandidateEvaluation.UnknownInputs.Count > 0 || observation.ValidationStatus == WeaponDamageObservationValidationStatus.Rejected)
		{
			weaponDamageCandidateEvaluation.Evaluable = false;
			return weaponDamageCandidateEvaluation;
		}
		int num = input.BaseRoll.Value;
		weaponDamageCandidateEvaluation.Stages.Add(new WeaponDamageCandidateStage("BaseRoll", null, num, "observation supplied base roll"));
		if (observation.Result.HitKind == WeaponDamageHitKind.KnownCritical)
		{
			num = ApplyCritical(num, input, formula, weaponDamageCandidateEvaluation);
		}
		int effectiveAr = ResolveEffectiveAr(input, formula, weaponDamageCandidateEvaluation);
		int current = ApplyAr(num, effectiveAr, formula, weaponDamageCandidateEvaluation);
		int num2 = ApplyAc(current, input, formula, weaponDamageCandidateEvaluation);
		int num3 = ((formula.MinimumFloorAfterAc && input.WeaponMinimum.HasValue) ? Math.Max(input.WeaponMinimum.Value, num2) : num2);
		weaponDamageCandidateEvaluation.Stages.Add(new WeaponDamageCandidateStage("MinimumFloor", num2, num3, formula.MinimumFloorAfterAc ? "minimum floor after AC" : "minimum floor disabled before add damage"));
		int num4 = ApplyAddDamage(num3, input, formula, weaponDamageCandidateEvaluation);
		weaponDamageCandidateEvaluation.Evaluable = true;
		weaponDamageCandidateEvaluation.PredictedDamage = num4;
		weaponDamageCandidateEvaluation.DifferenceFromObservation = num4 - observation.Result.ObservedDamage.Value;
		weaponDamageCandidateEvaluation.ExactMatch = weaponDamageCandidateEvaluation.DifferenceFromObservation == 0;
		return weaponDamageCandidateEvaluation;
	}

	public static IList<WeaponDamageCandidateEvaluation> EvaluateAll(WeaponDamageObservation observation, IEnumerable<WeaponDamageCandidateFormula> formulas)
	{
		IList<WeaponDamageCandidateEvaluation> list = (formulas ?? new WeaponDamageCandidateFormula[0]).Select((WeaponDamageCandidateFormula x) => Evaluate(observation, x)).ToList();
		int num = list.Count((WeaponDamageCandidateEvaluation x) => x.ExactMatch);
		foreach (WeaponDamageCandidateEvaluation item in list)
		{
			item.MultipleCandidatesAlsoMatched = item.ExactMatch && num > 1;
		}
		return list;
	}

	private static void Require(int? value, string name, WeaponDamageCandidateEvaluation evaluation)
	{
		if (!value.HasValue)
		{
			evaluation.UnknownInputs.Add(name);
		}
	}

	private static int ResolveEffectiveAr(WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
	{
		int num = input.AttackRating.Value;
		int value = input.AddAllOff.Value;
		if (input.AmsCapPresent == false && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.MissingCapMeansNoCap)
		{
			evaluation.Assumptions.Add("missing AMSCap means no cap");
		}
		else if (input.AmsCapPresent == true && input.AmsCap == 0 && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.ZeroCapMeansLiteralZero)
		{
			num = 0;
			evaluation.Assumptions.Add("zero AMSCap means literal zero");
		}
		else if (input.AmsCapPresent == true && input.AmsCap == 0 && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.ZeroCapMeansNoCap)
		{
			evaluation.Assumptions.Add("zero AMSCap means no cap");
		}
		else if (input.AmsCapPresent == true && input.AmsCap.HasValue && input.AmsCap.Value > 0)
		{
			num = Math.Min(num, input.AmsCap.Value);
			evaluation.Assumptions.Add("positive AMSCap applied before AR stage");
		}
		int num2 = num + value + input.TemporaryOffensiveModifiers.GetValueOrDefault();
		evaluation.Stages.Add(new WeaponDamageCandidateStage("ResolveEffectiveAR", num, num2, "Add All Off and temporary offensive modifiers applied after cap in report-only model"));
		return num2;
	}

	private static int ApplyAr(int baseDamage, int effectiveAr, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
	{
		switch (formula.ArOrdering)
		{
		case WeaponDamageCandidateArOrdering.TruncateBaseTimes400PlusArOver400:
		{
			int num = baseDamage * (400 + effectiveAr) / 400;
			evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, num, "truncate(base * (400 + AR) / 400)"));
			return num;
		}
		case WeaponDamageCandidateArOrdering.TruncateBaseTimesMultiplier:
		{
			int num2 = ((formula.MultiplierDenominator == 0) ? 1 : formula.MultiplierDenominator);
			int num = baseDamage * formula.MultiplierNumerator / num2;
			evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, num, "truncate(base * multiplierNumerator / multiplierDenominator)"));
			return num;
		}
		default:
		{
			int num = baseDamage + baseDamage * effectiveAr / 400;
			evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, num, "base + truncate(base * AR / 400)"));
			return num;
		}
		}
	}

	private static int ApplyAc(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
	{
		if (formula.AcOrdering == WeaponDamageCandidateAcOrdering.None)
		{
			evaluation.Stages.Add(new WeaponDamageCandidateStage("AC", current, current, "AC not applied"));
			return current;
		}
		int num = input.TargetArmor.Value / 10;
		int num2 = Math.Max(0, current - num);
		evaluation.Stages.Add(new WeaponDamageCandidateStage("AC", current, num2, formula.AcOrdering.ToString()));
		return num2;
	}

	private static int ApplyAddDamage(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
	{
		int num = input.TypeSpecificAddDamage.GetValueOrDefault() + input.UniversalAddDamage.GetValueOrDefault();
		int num2 = current;
		WeaponDamageCandidateAddDamageOrdering addDamageOrdering = formula.AddDamageOrdering;
		WeaponDamageCandidateAddDamageOrdering weaponDamageCandidateAddDamageOrdering = addDamageOrdering;
		num2 = ((weaponDamageCandidateAddDamageOrdering != WeaponDamageCandidateAddDamageOrdering.ArScaled) ? (current + num) : (current + num + num * input.AttackRating.GetValueOrDefault() / 400));
		evaluation.Stages.Add(new WeaponDamageCandidateStage("AddDamage", current, num2, formula.AddDamageOrdering.ToString()));
		return num2;
	}

	private static int ApplyCritical(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
	{
		int num = input.WeaponMaximum ?? current;
		int valueOrDefault = input.CriticalBonus.GetValueOrDefault();
		int num2 = formula.CriticalOrdering switch
		{
			WeaponDamageCandidateCriticalOrdering.MaximumPlusCriticalBonus => num + valueOrDefault, 
			WeaponDamageCandidateCriticalOrdering.CriticalBonusArScaled => current + valueOrDefault + valueOrDefault * input.AttackRating.GetValueOrDefault() / 400, 
			WeaponDamageCandidateCriticalOrdering.CriticalBonusAcReduced => current + Math.Max(0, valueOrDefault - input.TargetArmor.GetValueOrDefault() / 10), 
			_ => current + valueOrDefault, 
		};
		evaluation.Stages.Add(new WeaponDamageCandidateStage("Critical", current, num2, formula.CriticalOrdering.ToString()));
		return num2;
	}
}
