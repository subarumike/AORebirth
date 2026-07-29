using System;

namespace ZoneEngine.Core.Navigation;

internal struct NpcChaseUpdateResult
{
	internal NpcChaseMovementKind Kind { get; private set; }

	internal bool HasDestination { get; private set; }

	internal bool ShouldIssueMovement { get; private set; }

	internal ChaseNavigationPoint Destination { get; private set; }

	internal bool RouteRequested { get; private set; }

	internal NpcChaseInvalidationReason InvalidationReason { get; private set; }

	private NpcChaseUpdateResult(NpcChaseMovementKind kind, bool hasDestination, bool shouldIssueMovement, ChaseNavigationPoint destination, bool routeRequested, NpcChaseInvalidationReason invalidationReason)
	{
		Kind = kind;
		HasDestination = hasDestination;
		ShouldIssueMovement = shouldIssueMovement;
		Destination = destination;
		RouteRequested = routeRequested;
		InvalidationReason = invalidationReason;
	}

	internal static NpcChaseUpdateResult Unsupported()
	{
		return new NpcChaseUpdateResult(NpcChaseMovementKind.Unsupported, hasDestination: false, shouldIssueMovement: false, default(ChaseNavigationPoint), routeRequested: false, NpcChaseInvalidationReason.None);
	}

	internal static NpcChaseUpdateResult Unavailable()
	{
		return new NpcChaseUpdateResult(NpcChaseMovementKind.Unavailable, hasDestination: false, shouldIssueMovement: false, default(ChaseNavigationPoint), routeRequested: false, NpcChaseInvalidationReason.None);
	}

	internal static NpcChaseUpdateResult Hold(bool routeRequested, NpcChaseInvalidationReason invalidationReason)
	{
		return new NpcChaseUpdateResult(NpcChaseMovementKind.Hold, hasDestination: false, shouldIssueMovement: false, default(ChaseNavigationPoint), routeRequested, invalidationReason);
	}

	internal static NpcChaseUpdateResult Move(NpcChaseMovementKind kind, ChaseNavigationPoint destination, bool shouldIssueMovement, bool routeRequested, NpcChaseInvalidationReason invalidationReason)
	{
		if (kind != NpcChaseMovementKind.Direct && kind != NpcChaseMovementKind.Route)
		{
			throw new ArgumentOutOfRangeException("kind");
		}
		return new NpcChaseUpdateResult(kind, hasDestination: true, shouldIssueMovement, destination, routeRequested, invalidationReason);
	}
}
