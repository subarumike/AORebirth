using System;
using System.Text.RegularExpressions;

namespace ZoneEngine.Core;

public static class WeaponDamageObservationJsonImporter
{
	public const string SupportedSchemaVersion = "1.0";

	public static WeaponDamageObservationImportResult Import(string json)
	{
		WeaponDamageObservationImportResult weaponDamageObservationImportResult = new WeaponDamageObservationImportResult();
		if (string.IsNullOrWhiteSpace(json))
		{
			weaponDamageObservationImportResult.Diagnostics.Add("empty JSON");
			return weaponDamageObservationImportResult;
		}
		string text = ExtractString(json, "schemaVersion");
		if (text != "1.0")
		{
			weaponDamageObservationImportResult.Diagnostics.Add("unsupported schemaVersion");
			return weaponDamageObservationImportResult;
		}
		WeaponDamageObservationDraft weaponDamageObservationDraft = new WeaponDamageObservationDraft();
		weaponDamageObservationDraft.Source.ObservationId = ExtractString(json, "observationId");
		weaponDamageObservationDraft.Source.CaptureDate = ExtractString(json, "captureDate");
		weaponDamageObservationDraft.Source.Environment = ExtractString(json, "environment");
		weaponDamageObservationDraft.Source.PacketEvidenceReference = ExtractString(json, "packetReference");
		weaponDamageObservationDraft.Source.LogEvidenceReference = ExtractString(json, "logReference");
		weaponDamageObservationDraft.Source.Classification = DamageEvidenceClassification.Unknown;
		weaponDamageObservationDraft.Input.WeaponTemplateIdentity = ExtractString(json, "weaponTemplateIdentity");
		weaponDamageObservationDraft.Input.WeaponMinimum = ExtractInt(json, "weaponMinimum");
		weaponDamageObservationDraft.Input.WeaponMaximum = ExtractInt(json, "weaponMaximum");
		weaponDamageObservationDraft.Input.BaseRoll = ExtractInt(json, "baseRoll");
		weaponDamageObservationDraft.Input.AttackRating = ExtractInt(json, "attackRating");
		weaponDamageObservationDraft.Input.AddAllOff = ExtractInt(json, "addAllOff");
		weaponDamageObservationDraft.Input.TargetArmor = ExtractInt(json, "targetArmor");
		weaponDamageObservationDraft.Input.AmsCap = ExtractInt(json, "amsCap");
		weaponDamageObservationDraft.Input.AmsCapPresent = json.Contains("\"amsCap\"");
		weaponDamageObservationDraft.Input.MappedDamageType = ParseDamageType(ExtractString(json, "mappedDamageType"));
		weaponDamageObservationDraft.Input.PacketOrderComplete = ExtractBool(json, "packetOrderComplete");
		weaponDamageObservationDraft.Input.CriticalStateEvidencePresent = ExtractBool(json, "criticalStateEvidencePresent");
		weaponDamageObservationDraft.Result.HitKind = ParseHitKind(ExtractString(json, "hitKind"));
		weaponDamageObservationDraft.Result.ObservedDamage = ExtractInt(json, "observedDamage");
		weaponDamageObservationDraft.Result.TargetHealthBefore = ExtractInt(json, "targetHealthBefore");
		weaponDamageObservationDraft.Result.TargetHealthAfter = ExtractInt(json, "targetHealthAfter");
		weaponDamageObservationImportResult.Observation = WeaponDamageObservationValidator.Validate(weaponDamageObservationDraft);
		weaponDamageObservationImportResult.Success = weaponDamageObservationImportResult.Observation.ValidationStatus != WeaponDamageObservationValidationStatus.Rejected;
		foreach (WeaponDamageObservationIssue issue in weaponDamageObservationImportResult.Observation.Issues)
		{
			weaponDamageObservationImportResult.Diagnostics.Add(issue.Kind.ToString() + ": " + issue.Detail);
		}
		return weaponDamageObservationImportResult;
	}

	private static string ExtractString(string json, string property)
	{
		Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*\"([^\"]*)\"");
		return match.Success ? match.Groups[1].Value : string.Empty;
	}

	private static int? ExtractInt(string json, string property)
	{
		Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*(-?\\d+)");
		if (!match.Success)
		{
			return null;
		}
		int result;
		return int.TryParse(match.Groups[1].Value, out result) ? new int?(result) : null;
	}

	private static bool? ExtractBool(string json, string property)
	{
		Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*(true|false)");
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value == "true";
	}

	private static DamageType ParseDamageType(string value)
	{
		DamageType result;
		return Enum.TryParse<DamageType>(value, ignoreCase: true, out result) ? result : DamageType.Unknown;
	}

	private static WeaponDamageHitKind ParseHitKind(string value)
	{
		WeaponDamageHitKind result;
		return Enum.TryParse<WeaponDamageHitKind>(value, ignoreCase: true, out result) ? result : WeaponDamageHitKind.UnknownHitKind;
	}
}
