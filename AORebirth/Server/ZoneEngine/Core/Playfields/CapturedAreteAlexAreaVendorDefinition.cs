using System.Collections.ObjectModel;

namespace ZoneEngine.Core.Playfields
{
	internal sealed class CapturedAreteAlexAreaVendorDefinition
	{
		internal string DisplayName { get; private set; }

		internal int SourceVendorInstance { get; private set; }

		internal int TemplateId { get; private set; }

		internal float X { get; private set; }

		internal float Y { get; private set; }

		internal float Z { get; private set; }

		internal float HeadingX { get; private set; }

		internal float HeadingY { get; private set; }

		internal float HeadingZ { get; private set; }

		internal float HeadingW { get; private set; }

		internal ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition> Stock { get; private set; }

		internal CapturedAreteAlexAreaVendorDefinition(
			string displayName,
			int sourceVendorInstance,
			int templateId,
			float x,
			float y,
			float z,
			CapturedAreteAlexAreaVendorStockDefinition[] stock)
			: this(displayName, sourceVendorInstance, templateId, x, y, z, 0f, 0f, 0f, 1f, stock)
		{
		}

		internal CapturedAreteAlexAreaVendorDefinition(
			string displayName,
			int sourceVendorInstance,
			int templateId,
			float x,
			float y,
			float z,
			float headingX,
			float headingY,
			float headingZ,
			float headingW,
			CapturedAreteAlexAreaVendorStockDefinition[] stock)
		{
			DisplayName = displayName;
			SourceVendorInstance = sourceVendorInstance;
			TemplateId = templateId;
			X = x;
			Y = y;
			Z = z;
			HeadingX = headingX;
			HeadingY = headingY;
			HeadingZ = headingZ;
			HeadingW = headingW;
			Stock = new ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition>(stock);
		}
	}
}
