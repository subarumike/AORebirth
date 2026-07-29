namespace AORebirth.Core.Playfields;

internal sealed class CreditsPolicyDefinition
{
	internal CreditsPolicyMode Mode { get; set; }

	internal int MinimumCredits { get; set; }

	internal int MaximumCredits { get; set; }

	internal int[] ObservedCredits { get; set; }

	internal LootEvidenceConfidence Evidence { get; set; }
}
