using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal static class CapturedEncounterRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, CapturedEncounterRuntimeDefinition> Definitions = new Dictionary<int, CapturedEncounterRuntimeDefinition>();

	internal static void Register(int runtimeInstance, CapturedEncounterRuntimeDefinition definition)
	{
		lock (Sync)
		{
			Definitions[runtimeInstance] = definition;
		}
	}

	internal static bool TryGet(int runtimeInstance, out CapturedEncounterRuntimeDefinition definition)
	{
		lock (Sync)
		{
			return Definitions.TryGetValue(runtimeInstance, out definition);
		}
	}

	internal static void Remove(int runtimeInstance)
	{
		lock (Sync)
		{
			Definitions.Remove(runtimeInstance);
		}
	}

	internal static void RemoveForPlayfield(int playfieldInstance)
	{
		if (playfieldInstance != 127)
		{
			return;
		}
		lock (Sync)
		{
			Definitions.Clear();
		}
	}
}
