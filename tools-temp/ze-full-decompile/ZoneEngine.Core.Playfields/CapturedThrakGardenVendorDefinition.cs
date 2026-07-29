using System;
using System.Collections.ObjectModel;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedThrakGardenVendorDefinition
{
	internal int SourceNpcInstance { get; private set; }

	internal int SourceVendorInstance { get; private set; }

	internal string DisplayName { get; private set; }

	internal int VendorTemplateId { get; private set; }

	internal bool RequiresCompletedGardenKeyQuest { get; private set; }

	internal ReadOnlyCollection<CapturedThrakGardenVendorStockDefinition> Stock { get; private set; }

	internal string Evidence { get; private set; }

	internal bool HasCapturedStock => Stock != null && Stock.Count > 0;

	internal CapturedThrakGardenVendorDefinition(int sourceNpcInstance, int sourceVendorInstance, string displayName, int vendorTemplateId, bool requiresCompletedGardenKeyQuest, CapturedThrakGardenVendorStockDefinition[] stock, string evidence)
	{
		SourceNpcInstance = sourceNpcInstance;
		SourceVendorInstance = sourceVendorInstance;
		DisplayName = displayName;
		VendorTemplateId = vendorTemplateId;
		RequiresCompletedGardenKeyQuest = requiresCompletedGardenKeyQuest;
		Stock = Array.AsReadOnly(stock ?? new CapturedThrakGardenVendorStockDefinition[0]);
		Evidence = evidence ?? string.Empty;
	}
}
