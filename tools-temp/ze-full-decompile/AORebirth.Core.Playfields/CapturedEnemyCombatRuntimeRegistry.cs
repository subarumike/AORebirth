using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal static class CapturedEnemyCombatRuntimeRegistry
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, CapturedEnemyCombatContract> Contracts = new Dictionary<int, CapturedEnemyCombatContract>();

	internal static void Register(int serverInstance, CapturedEnemyCombatContract contract)
	{
		lock (Sync)
		{
			Contracts[serverInstance] = contract;
		}
	}

	internal static bool TryGet(int serverInstance, out CapturedEnemyCombatContract contract)
	{
		lock (Sync)
		{
			return Contracts.TryGetValue(serverInstance, out contract);
		}
	}

	internal static void Remove(int serverInstance)
	{
		lock (Sync)
		{
			Contracts.Remove(serverInstance);
		}
	}
}
