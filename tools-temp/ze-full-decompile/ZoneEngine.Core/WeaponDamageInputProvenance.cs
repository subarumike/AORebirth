namespace ZoneEngine.Core;

public sealed class WeaponDamageInputProvenance
{
	public string InputName { get; set; }

	public string StorageSource { get; set; }

	public string DatabaseSource { get; set; }

	public string FieldOrStatId { get; set; }

	public string LoadPath { get; set; }

	public string RuntimeOwner { get; set; }

	public string LookupPath { get; set; }

	public string DataType { get; set; }

	public string Signedness { get; set; }

	public string DefaultBehavior { get; set; }

	public string MissingDataBehavior { get; set; }

	public string DuplicateDataBehavior { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public string ActiveCallerAvailability { get; set; }

	public string ValueState { get; set; }

	public int? ResolvedValue { get; set; }

	public WeaponDamageInputProvenance()
	{
		InputName = string.Empty;
		StorageSource = string.Empty;
		DatabaseSource = string.Empty;
		FieldOrStatId = string.Empty;
		LoadPath = string.Empty;
		RuntimeOwner = string.Empty;
		LookupPath = string.Empty;
		DataType = string.Empty;
		Signedness = string.Empty;
		DefaultBehavior = string.Empty;
		MissingDataBehavior = string.Empty;
		DuplicateDataBehavior = string.Empty;
		ActiveCallerAvailability = string.Empty;
		ValueState = string.Empty;
	}
}
