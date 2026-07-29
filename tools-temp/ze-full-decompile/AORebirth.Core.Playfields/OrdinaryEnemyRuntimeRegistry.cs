using System.Collections.Generic;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal static class OrdinaryEnemyRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> Definitions = new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

	internal static void Register(int serverInstance, OrdinaryEnemyRuntimeDefinition definition)
	{
		lock (Sync)
		{
			Definitions[serverInstance] = definition;
		}
	}

	internal static bool TryGet(int serverInstance, out OrdinaryEnemyRuntimeDefinition definition)
	{
		lock (Sync)
		{
			return Definitions.TryGetValue(serverInstance, out definition);
		}
	}

	internal static void Remove(int serverInstance)
	{
		lock (Sync)
		{
			Definitions.Remove(serverInstance);
		}
	}

	internal static void RemoveForPlayfield(int playfieldInstance)
	{
		lock (Sync)
		{
			int[] array = (from value in Definitions
				where value.Value.Spawn.PlayfieldInstance == playfieldInstance
				select value.Key).ToArray();
			foreach (int key in array)
			{
				Definitions.Remove(key);
			}
		}
	}
}
