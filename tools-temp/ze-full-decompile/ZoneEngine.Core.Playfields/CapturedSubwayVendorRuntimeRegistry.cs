using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal static class CapturedSubwayVendorRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, CapturedSubwayVendorRuntimeDefinition> Entries = new Dictionary<int, CapturedSubwayVendorRuntimeDefinition>();

	internal static void Register(CapturedSubwayVendorRuntimeDefinition runtime)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			Dictionary<int, CapturedSubwayVendorRuntimeDefinition> entries = Entries;
			Identity npcIdentity = runtime.NpcIdentity;
			entries[((Identity)(ref npcIdentity)).Instance] = runtime;
		}
	}

	internal static bool TryGet(int npcInstance, out CapturedSubwayVendorRuntimeDefinition runtime)
	{
		lock (Sync)
		{
			return Entries.TryGetValue(npcInstance, out runtime);
		}
	}

	internal static bool ContainsPlayfield(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			return Entries.Values.Any((CapturedSubwayVendorRuntimeDefinition runtime) => Same(runtime.PlayfieldIdentity, playfieldIdentity));
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
