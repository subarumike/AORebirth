using System;

namespace ZoneEngine.Core.Navigation;

internal sealed class NpcChaseRouteState
{
	internal int NpcInstance { get; set; }

	internal int TargetInstance { get; set; }

	internal NpcChaseRouteStateKind Kind { get; set; }

	internal string GeometryVersion { get; set; }

	internal ChaseNavigationPoint StartSample { get; set; }

	internal ChaseNavigationPoint TargetSample { get; set; }

	internal ChaseNavigationPoint DirectDestination { get; set; }

	internal ChaseRoutePlan Route { get; set; }

	internal int RouteIndex { get; set; }

	internal int LastIssuedRouteIndex { get; set; }

	internal ChaseNavigationPoint LastProgressPoint { get; set; }

	internal DateTime LastProgressUtc { get; set; }

	internal DateTime NextEvaluationUtc { get; set; }

	internal DateTime RetryAtUtc { get; set; }

	internal ChaseRoutePlanStatus LastFailureStatus { get; set; }

	internal NpcChaseInvalidationReason LastInvalidationReason { get; set; }
}
