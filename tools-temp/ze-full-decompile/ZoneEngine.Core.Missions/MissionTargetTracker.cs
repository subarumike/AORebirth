using System.Collections.Generic;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Missions;

internal static class MissionTargetTracker
{
	private static readonly object Sync = new object();

	private static readonly HashSet<long> Targets = new HashSet<long>();

	private static long Key(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return ((long)((Identity)(ref identity)).Type << 32) | (uint)((Identity)(ref identity)).Instance;
	}

	public static void Register(Identity npcIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref npcIdentity)).Type == 0 || ((Identity)(ref npcIdentity)).Instance == 0)
		{
			return;
		}
		lock (Sync)
		{
			Targets.Add(Key(npcIdentity));
		}
	}

	public static bool IsMissionTarget(Identity npcIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			return Targets.Contains(Key(npcIdentity));
		}
	}

	public static void Unregister(Identity npcIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			Targets.Remove(Key(npcIdentity));
		}
	}

	public static void Clear()
	{
		lock (Sync)
		{
			Targets.Clear();
		}
	}
}
