using System.Collections.Generic;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Missions;

internal static class MissionMachineTracker
{
	private static readonly object Sync = new object();

	private static readonly HashSet<long> Machines = new HashSet<long>();

	private static long Key(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return ((long)((Identity)(ref identity)).Type << 32) | (uint)((Identity)(ref identity)).Instance;
	}

	public static void Register(Identity machineIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref machineIdentity)).Type == 0 || ((Identity)(ref machineIdentity)).Instance == 0)
		{
			return;
		}
		lock (Sync)
		{
			Machines.Add(Key(machineIdentity));
		}
	}

	public static bool IsMissionMachine(Identity machineIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			return Machines.Contains(Key(machineIdentity));
		}
	}

	public static void Unregister(Identity machineIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			Machines.Remove(Key(machineIdentity));
		}
	}
}
