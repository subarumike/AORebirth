using System.Collections.Generic;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySupportNanoProfile
{
	private readonly Dictionary<int, int> spawnNanoPoolByLevel;

	internal int PrimaryNanoId { get; private set; }

	internal int TriggeredSelfNanoId { get; private set; }

	internal double InitialDelaySeconds { get; private set; }

	internal double CastSeconds { get; private set; }

	internal double RepeatSeconds { get; private set; }

	internal int DurationParameter { get; private set; }

	internal double EffectLifetimeSeconds { get; private set; }

	internal double TargetRange { get; private set; }

	internal bool FallbackToSelf { get; private set; }

	internal int PrimaryStrain { get; private set; }

	internal int TriggeredSelfStrain { get; private set; }

	internal int PrimaryModifierDelta { get; private set; }

	internal int TriggeredSelfModifierDelta { get; private set; }

	internal int[] AffectedStatIds { get; private set; }

	internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }

	internal string Evidence { get; private set; }

	internal int PeriodicStatId { get; private set; }

	internal int PeriodicStatDelta { get; private set; }

	internal int PeriodicTickCount { get; private set; }

	internal double PeriodicTickSeconds { get; private set; }

	internal int NanoCost { get; private set; }

	internal bool CastWhileFighting { get; private set; }

	internal bool AllowCombatActionsDuringCast { get; private set; }

	internal int CastChanceBasisPoints { get; private set; }

	internal int SelfTargetChanceBasisPoints { get; private set; }

	internal bool RandomizeInitialDelay { get; private set; }

	internal int NcuCost { get; private set; }

	internal bool ResolvePrimaryModifierFromNanoData { get; private set; }

	internal bool HasPeriodicStatHit => PeriodicStatId > 0;

	internal bool HasTriggeredSelfEffect => TriggeredSelfNanoId > 0;

	internal KeyValuePair<int, int>[] SpawnNanoPoolByLevel => spawnNanoPoolByLevel.OrderBy((KeyValuePair<int, int> value) => value.Key).ToArray();

	internal OrdinaryEnemySupportNanoProfile(int primaryNanoId, int triggeredSelfNanoId, double initialDelaySeconds, double castSeconds, double repeatSeconds, int durationParameter, double effectLifetimeSeconds, double targetRange, bool fallbackToSelf, int primaryStrain, int triggeredSelfStrain, int primaryModifierDelta, int triggeredSelfModifierDelta, int[] affectedStatIds, OrdinaryEnemyEvidenceState evidenceState, string evidence, int periodicStatId = 0, int periodicStatDelta = 0, int periodicTickCount = 0, double periodicTickSeconds = 0.0, int nanoCost = 0, bool castWhileFighting = false, bool allowCombatActionsDuringCast = false, int castChanceBasisPoints = 10000, int selfTargetChanceBasisPoints = 0, bool randomizeInitialDelay = false, int ncuCost = 0, IDictionary<int, int> spawnNanoPoolByLevel = null, bool resolvePrimaryModifierFromNanoData = false)
	{
		PrimaryNanoId = primaryNanoId;
		TriggeredSelfNanoId = triggeredSelfNanoId;
		InitialDelaySeconds = initialDelaySeconds;
		CastSeconds = castSeconds;
		RepeatSeconds = repeatSeconds;
		DurationParameter = durationParameter;
		EffectLifetimeSeconds = effectLifetimeSeconds;
		TargetRange = targetRange;
		FallbackToSelf = fallbackToSelf;
		PrimaryStrain = primaryStrain;
		TriggeredSelfStrain = triggeredSelfStrain;
		PrimaryModifierDelta = primaryModifierDelta;
		TriggeredSelfModifierDelta = triggeredSelfModifierDelta;
		AffectedStatIds = affectedStatIds ?? new int[0];
		EvidenceState = evidenceState;
		Evidence = evidence ?? string.Empty;
		PeriodicStatId = periodicStatId;
		PeriodicStatDelta = periodicStatDelta;
		PeriodicTickCount = periodicTickCount;
		PeriodicTickSeconds = periodicTickSeconds;
		NanoCost = nanoCost;
		CastWhileFighting = castWhileFighting;
		AllowCombatActionsDuringCast = allowCombatActionsDuringCast;
		CastChanceBasisPoints = castChanceBasisPoints;
		SelfTargetChanceBasisPoints = selfTargetChanceBasisPoints;
		RandomizeInitialDelay = randomizeInitialDelay;
		NcuCost = ncuCost;
		ResolvePrimaryModifierFromNanoData = resolvePrimaryModifierFromNanoData;
		this.spawnNanoPoolByLevel = ((spawnNanoPoolByLevel == null) ? new Dictionary<int, int>() : new Dictionary<int, int>(spawnNanoPoolByLevel));
	}

	internal int ResolveSpawnNanoPool(int level)
	{
		int value;
		return spawnNanoPoolByLevel.TryGetValue(level, out value) ? value : 0;
	}

	internal static OrdinaryEnemySupportNanoProfile CapturedIncompleteRebuild90405()
	{
		return new OrdinaryEnemySupportNanoProfile(90405, 0, 5.0, 2.5, 5.0, 1440000, 14400.0, 20.0, fallbackToSelf: true, 14, 0, 0, 0, new int[0], OrdinaryEnemyEvidenceState.Policy, "20260709-222339,20260709-225408,20260716-034104,20260716-221358;nano=90405;hit-currentnano=+21;tick-count=960;tick-seconds=15;nano-cost=47;ncu=6;duration-centiseconds=1440000;range=20;policy=5-second-decisions-at-25-percent,50-percent-self,random-initial-phase,combat-casting;spawn-nano-pools=inferred-from-captured-currentnano-plateaus", 214, 21, 960, 15.0, 47, castWhileFighting: true, allowCombatActionsDuringCast: true, 2500, 5000, randomizeInitialDelay: true, 6, new Dictionary<int, int>
		{
			{ 17, 918 },
			{ 18, 985 },
			{ 19, 1051 },
			{ 20, 1117 },
			{ 21, 1183 },
			{ 22, 1250 }
		});
	}

	internal static OrdinaryEnemySupportNanoProfile CapturedFragmentedSoul95447()
	{
		return new OrdinaryEnemySupportNanoProfile(95447, 0, 10.0, 2.5, 10.0, 1440000, 14400.0, 20.0, fallbackToSelf: true, 181, 0, 0, 0, new int[0], OrdinaryEnemyEvidenceState.Policy, "20260709-222339,20260717-215250;nano=95447;nanos.dat strain=181,ncu=7,cost=44,duration-centiseconds=1440000,range=20,on-use-skill-stat=381,delta=+42;capture-completion=2.209564..2.599639;repeat-decision=10-second-private-policy;self-or-nearest-ordinary-ally-with-self-fallback;spawn-nano-pools=minimum-observed-current-nano-only;levels-17-and-18-remain-unresolved", 0, 0, 0, 0.0, 44, castWhileFighting: false, allowCombatActionsDuringCast: false, 10000, 5000, randomizeInitialDelay: true, 7, new Dictionary<int, int>
		{
			{ 19, 665 },
			{ 20, 782 },
			{ 21, 829 }
		}, resolvePrimaryModifierFromNanoData: true);
	}
}
