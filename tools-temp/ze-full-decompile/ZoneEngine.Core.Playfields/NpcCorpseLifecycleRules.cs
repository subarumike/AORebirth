using System;

namespace ZoneEngine.Core.Playfields;

public static class NpcCorpseLifecycleRules
{
	public static readonly TimeSpan DeadNpcDespawnDelay = TimeSpan.FromSeconds(10.0);

	public static readonly TimeSpan CorpseSpawnDelay = TimeSpan.FromMilliseconds(600.0);

	public const int CapturedCleaningRobotDeathActionParameter2 = 500;
}
