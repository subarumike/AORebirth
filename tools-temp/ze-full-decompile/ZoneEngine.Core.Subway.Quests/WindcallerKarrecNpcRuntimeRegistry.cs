using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecNpcRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, WindcallerKarrecNpcRuntimeDefinition> Entries = new Dictionary<int, WindcallerKarrecNpcRuntimeDefinition>();

	internal static void Register(WindcallerKarrecNpcRuntimeDefinition runtime)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			Dictionary<int, WindcallerKarrecNpcRuntimeDefinition> entries = Entries;
			Identity npcIdentity = runtime.NpcIdentity;
			entries[((Identity)(ref npcIdentity)).Instance] = runtime;
		}
	}

	internal static bool TryGet(int npcInstance, out WindcallerKarrecNpcRuntimeDefinition runtime)
	{
		lock (Sync)
		{
			return Entries.TryGetValue(npcInstance, out runtime);
		}
	}

	internal static bool TryGet(Identity playfieldIdentity, Identity npcIdentity, out WindcallerKarrecNpcRuntimeDefinition runtime)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			if (!Entries.TryGetValue(((Identity)(ref npcIdentity)).Instance, out var value) || !Same(value.PlayfieldIdentity, playfieldIdentity) || !Same(value.NpcIdentity, npcIdentity))
			{
				runtime = null;
				return false;
			}
			runtime = value;
			return true;
		}
	}

	internal static bool ContainsPlayfield(Identity playfieldIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return CountForPlayfield(playfieldIdentity) > 0;
	}

	internal static int CountForPlayfield(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			return Entries.Values.Count((WindcallerKarrecNpcRuntimeDefinition runtime) => Same(runtime.PlayfieldIdentity, playfieldIdentity));
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

	private static bool Same(Identity left, Identity right)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return ((Identity)(ref left)).Type == ((Identity)(ref right)).Type && ((Identity)(ref left)).Instance == ((Identity)(ref right)).Instance;
	}
}
