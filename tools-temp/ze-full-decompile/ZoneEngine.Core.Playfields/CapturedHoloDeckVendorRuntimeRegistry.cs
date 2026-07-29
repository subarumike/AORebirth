using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal static class CapturedHoloDeckVendorRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, CapturedHoloDeckVendorRuntimeDefinition> Entries = new Dictionary<int, CapturedHoloDeckVendorRuntimeDefinition>();

	internal static void Register(CapturedHoloDeckVendorRuntimeDefinition runtime)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			Dictionary<int, CapturedHoloDeckVendorRuntimeDefinition> entries = Entries;
			Identity vendorIdentity = runtime.VendorIdentity;
			entries[((Identity)(ref vendorIdentity)).Instance] = runtime;
		}
	}

	internal static bool TryGet(int vendorInstance, out CapturedHoloDeckVendorRuntimeDefinition runtime)
	{
		lock (Sync)
		{
			return Entries.TryGetValue(vendorInstance, out runtime);
		}
	}

	internal static bool ContainsPlayfield(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			return Entries.Values.Any((CapturedHoloDeckVendorRuntimeDefinition runtime) => Same(runtime.PlayfieldIdentity, playfieldIdentity));
		}
	}

	internal static void RemoveForPlayfield(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			int[] array = (from pair in Entries
				where Same(pair.Value.PlayfieldIdentity, playfieldIdentity)
				select pair.Key).ToArray();
			int[] array2 = array;
			foreach (int key in array2)
			{
				Entries.Remove(key);
			}
		}
	}

	internal static bool Same(Identity left, Identity right)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return ((Identity)(ref left)).Type == ((Identity)(ref right)).Type && ((Identity)(ref left)).Instance == ((Identity)(ref right)).Instance;
	}
}
