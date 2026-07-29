using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public static class WeaponDamageObservationValidator
{
	public static WeaponDamageObservation Validate(WeaponDamageObservationDraft draft)
	{
		if (draft == null)
		{
			draft = new WeaponDamageObservationDraft();
		}
		if (draft.Source == null)
		{
			draft.Source = new WeaponDamageObservationSource();
		}
		if (draft.Input == null)
		{
			draft.Input = new WeaponDamageObservationInput();
		}
		if (draft.Result == null)
		{
			draft.Result = new WeaponDamageObservationResult();
		}
		List<WeaponDamageObservationIssue> list = new List<WeaponDamageObservationIssue>();
		if (draft.Result.TargetHealthBefore.HasValue && draft.Result.TargetHealthAfter.HasValue && draft.Result.ObservedDamage.HasValue && !ObservedDamageMatchesHealthDelta(draft.Result.TargetHealthBefore.Value, draft.Result.TargetHealthAfter.Value, draft.Result.ObservedDamage.Value))
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.HealthDeltaMismatch, "health delta does not match observed damage"));
		}
		if (string.IsNullOrEmpty(draft.Input.WeaponTemplateIdentity))
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.AmbiguousWeaponIdentity, "weapon template identity is missing or ambiguous"));
		}
		if (draft.Input.MappedDamageType == DamageType.Unknown)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.UnknownDamageType, "mapped damage type is unknown"));
		}
		if ((from x in draft.Input.AttackSkillDefinitions
			group x by x.StatId).Any((IGrouping<int, AttackSkillContribution> x) => x.Count() > 1))
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.ContradictoryAttackerStats, "duplicate attack-skill definitions"));
		}
		if (!draft.Input.TargetArmor.HasValue)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingArmor, "target matching armor was not supplied"));
		}
		if (draft.Input.MultipleDamageSourcesPossible == true)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MultipleDamageSourcesPossible, "multiple attacks could have contributed to the health change"));
		}
		if (draft.Input.ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible == true)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.ExternalDamagePossible, "reflect, absorb, shield, proc, nano, DoT, or environmental damage may be present"));
		}
		if (draft.Result.HitKind == WeaponDamageHitKind.KnownCritical && draft.Input.CriticalStateEvidencePresent != true)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.CriticalStateClaimedWithoutEvidence, "critical state was claimed without evidence"));
		}
		if (draft.Result.HitKind == WeaponDamageHitKind.UnknownHitKind)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.UnknownHitKind, "hit kind is unknown"));
		}
		if (draft.Input.PacketOrderComplete != true)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.IncompletePacketOrder, "packet order is incomplete or unproven"));
		}
		if (!draft.Input.AddAllOff.HasValue)
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingAddAllOff, "Add All Off was not supplied"));
		}
		if (!draft.Input.AmsCapPresent.HasValue || (draft.Input.AmsCapPresent == true && draft.Input.AmsCap == 0))
		{
			list.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingAmsCapSemantics, "AMSCap absence or zero semantics are not distinguished"));
		}
		WeaponDamageObservationValidationStatus status = (list.Any((WeaponDamageObservationIssue x) => x.Kind == WeaponDamageObservationIssueKind.HealthDeltaMismatch) ? WeaponDamageObservationValidationStatus.Rejected : ((list.Count != 0) ? WeaponDamageObservationValidationStatus.Incomplete : WeaponDamageObservationValidationStatus.Complete));
		return new WeaponDamageObservation(CloneSource(draft.Source), CloneInput(draft.Input), CloneResult(draft.Result), list, status);
	}

	private static bool ObservedDamageMatchesHealthDelta(int targetHealthBefore, int targetHealthAfter, int observedDamage)
	{
		int num = targetHealthBefore - targetHealthAfter;
		int num2 = Math.Min(observedDamage, targetHealthBefore);
		return num == num2;
	}

	private static WeaponDamageObservationSource CloneSource(WeaponDamageObservationSource source)
	{
		return new WeaponDamageObservationSource
		{
			ObservationId = source.ObservationId,
			SourceKind = source.SourceKind,
			CaptureDate = source.CaptureDate,
			Environment = source.Environment,
			PacketEvidenceReference = source.PacketEvidenceReference,
			LogEvidenceReference = source.LogEvidenceReference,
			TimingReference = source.TimingReference,
			Classification = source.Classification
		};
	}

	private static WeaponDamageObservationInput CloneInput(WeaponDamageObservationInput input)
	{
		WeaponDamageObservationInput weaponDamageObservationInput = new WeaponDamageObservationInput
		{
			AttackerIdentity = input.AttackerIdentity,
			AttackerCategory = input.AttackerCategory,
			TargetIdentity = input.TargetIdentity,
			WeaponTemplateIdentity = input.WeaponTemplateIdentity,
			WeaponInstanceIdentity = input.WeaponInstanceIdentity,
			WeaponQualityLevel = input.WeaponQualityLevel,
			WeaponMinimum = input.WeaponMinimum,
			WeaponMaximum = input.WeaponMaximum,
			BaseRoll = input.BaseRoll,
			LegacyDamageBonus = input.LegacyDamageBonus,
			CriticalBonus = input.CriticalBonus,
			RawDamageType = input.RawDamageType,
			MappedDamageType = input.MappedDamageType,
			AttackRating = input.AttackRating,
			AddAllOff = input.AddAllOff,
			TemporaryOffensiveModifiers = input.TemporaryOffensiveModifiers,
			AmsCap = input.AmsCap,
			AmsCapPresent = input.AmsCapPresent,
			TargetArmor = input.TargetArmor,
			TypeSpecificAddDamage = input.TypeSpecificAddDamage,
			UniversalAddDamage = input.UniversalAddDamage,
			MultipleDamageSourcesPossible = input.MultipleDamageSourcesPossible,
			ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible = input.ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible,
			PacketOrderComplete = input.PacketOrderComplete,
			CriticalStateEvidencePresent = input.CriticalStateEvidencePresent
		};
		foreach (AttackSkillContribution attackSkillDefinition in input.AttackSkillDefinitions)
		{
			weaponDamageObservationInput.AttackSkillDefinitions.Add(new AttackSkillContribution
			{
				StatId = attackSkillDefinition.StatId,
				Percentage = attackSkillDefinition.Percentage,
				Value = attackSkillDefinition.Value
			});
		}
		foreach (string knownUncertainty in input.KnownUncertainties)
		{
			weaponDamageObservationInput.KnownUncertainties.Add(knownUncertainty);
		}
		return weaponDamageObservationInput;
	}

	private static WeaponDamageObservationResult CloneResult(WeaponDamageObservationResult result)
	{
		return new WeaponDamageObservationResult
		{
			HitKind = result.HitKind,
			ObservedDamage = result.ObservedDamage,
			TargetHealthBefore = result.TargetHealthBefore,
			TargetHealthAfter = result.TargetHealthAfter
		};
	}
}
