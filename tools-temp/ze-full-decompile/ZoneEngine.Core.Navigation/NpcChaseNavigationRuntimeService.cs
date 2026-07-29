using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Navigation;

internal sealed class NpcChaseNavigationRuntimeService : IDisposable
{
	internal const double TargetMovementReplanDistance = 3.0;

	internal const double FailureStartMovementRetryDistance = 1.0;

	internal const double DirectDestinationRefreshDistance = 1.0;

	internal static readonly TimeSpan EvaluationInterval = TimeSpan.FromMilliseconds(100.0);

	internal static readonly TimeSpan FailedRouteRetryDelay = TimeSpan.FromSeconds(2.0);

	private readonly IPlayfieldChaseNavigationProvider provider;

	private readonly ChaseRouteSearchLimits searchLimits;

	private readonly NpcChaseRouteFollower follower;

	private readonly Dictionary<int, NpcChaseRouteState> states = new Dictionary<int, NpcChaseRouteState>();

	private bool disposed;

	internal ChaseNavigationCapability Capability => provider.Capability;

	internal string GeometryVersion => provider.GeometryVersion;

	internal int TotalRouteRequests { get; private set; }

	internal int ActiveStateCount => states.Count;

	internal NpcChaseNavigationRuntimeService(IPlayfieldChaseNavigationProvider provider)
		: this(provider, ChaseRouteSearchLimits.Default, new NpcChaseRouteFollower())
	{
	}

	internal NpcChaseNavigationRuntimeService(IPlayfieldChaseNavigationProvider provider, ChaseRouteSearchLimits searchLimits, NpcChaseRouteFollower follower)
	{
		if (provider == null)
		{
			throw new ArgumentNullException("provider");
		}
		this.provider = provider;
		this.searchLimits = searchLimits ?? throw new ArgumentNullException("searchLimits");
		this.follower = follower ?? throw new ArgumentNullException("follower");
	}

	internal bool HasActivePursuit(int npcInstance)
	{
		NpcChaseRouteState value;
		return states.TryGetValue(npcInstance, out value) && value.Kind != NpcChaseRouteStateKind.Failed;
	}

	internal bool HasState(int npcInstance)
	{
		return states.ContainsKey(npcInstance);
	}

	internal bool IsAttackPathTraversable(ChaseNavigationPoint start, ChaseNavigationPoint target)
	{
		if (provider.Capability == ChaseNavigationCapability.Unsupported)
		{
			return true;
		}
		return provider.Capability == ChaseNavigationCapability.Supported && provider.IsAttackLineTraversable(start, target);
	}

	internal NpcChaseUpdateResult UpdatePursuit(int npcInstance, int targetInstance, ChaseNavigationPoint current, ChaseNavigationPoint target, double stopDistance, DateTime utcNow)
	{
		if (disposed || npcInstance <= 0 || targetInstance <= 0 || !current.IsFinite || !target.IsFinite)
		{
			return NpcChaseUpdateResult.Unavailable();
		}
		if (provider.Capability == ChaseNavigationCapability.Unsupported)
		{
			states.Remove(npcInstance);
			return NpcChaseUpdateResult.Unsupported();
		}
		if (provider.Capability != ChaseNavigationCapability.Supported)
		{
			states.Remove(npcInstance);
			return NpcChaseUpdateResult.Unavailable();
		}
		states.TryGetValue(npcInstance, out var value);
		NpcChaseInvalidationReason npcChaseInvalidationReason = NpcChaseInvalidationReason.None;
		if (value != null)
		{
			if (value.TargetInstance != targetInstance)
			{
				npcChaseInvalidationReason = NpcChaseInvalidationReason.TargetReplaced;
				states.Remove(npcInstance);
				value = null;
			}
			else if (!string.Equals(value.GeometryVersion, provider.GeometryVersion, StringComparison.Ordinal))
			{
				npcChaseInvalidationReason = NpcChaseInvalidationReason.GeometryVersionChanged;
				states.Remove(npcInstance);
				value = null;
			}
			else if (value.TargetSample.Distance2D(target) > 3.0)
			{
				npcChaseInvalidationReason = NpcChaseInvalidationReason.TargetMoved;
				states.Remove(npcInstance);
				value = null;
			}
			else if (utcNow < value.NextEvaluationUtc)
			{
				return NpcChaseUpdateResult.Hold(routeRequested: false, NpcChaseInvalidationReason.None);
			}
		}
		if (provider.IsSegmentTraversable(current, target))
		{
			if (value != null && value.Kind == NpcChaseRouteStateKind.Route)
			{
				npcChaseInvalidationReason = NpcChaseInvalidationReason.DirectPathRestored;
			}
			return UpdateDirect(npcInstance, targetInstance, current, target, Math.Max(0.0, stopDistance), utcNow, value, npcChaseInvalidationReason);
		}
		if (value != null && value.Kind == NpcChaseRouteStateKind.Failed)
		{
			if (!(value.StartSample.Distance2D(current) > 1.0) && utcNow < value.RetryAtUtc)
			{
				value.NextEvaluationUtc = utcNow + EvaluationInterval;
				return NpcChaseUpdateResult.Hold(routeRequested: false, npcChaseInvalidationReason);
			}
			states.Remove(npcInstance);
			value = null;
		}
		if (value != null && value.Kind == NpcChaseRouteStateKind.Route)
		{
			if (follower.TrySelectDestination(provider, value, current, utcNow, out var destination, out var shouldIssueMovement, out var invalidationReason))
			{
				value.NextEvaluationUtc = utcNow + EvaluationInterval;
				return NpcChaseUpdateResult.Move(NpcChaseMovementKind.Route, destination, shouldIssueMovement, routeRequested: false, npcChaseInvalidationReason);
			}
			npcChaseInvalidationReason = invalidationReason;
			states.Remove(npcInstance);
			value = null;
		}
		TotalRouteRequests++;
		ChaseRoutePlan chaseRoutePlan = provider.RequestRoute(current, target, searchLimits);
		if (!chaseRoutePlan.IsSuccess)
		{
			states[npcInstance] = new NpcChaseRouteState
			{
				NpcInstance = npcInstance,
				TargetInstance = targetInstance,
				Kind = NpcChaseRouteStateKind.Failed,
				GeometryVersion = provider.GeometryVersion,
				StartSample = current,
				TargetSample = target,
				RetryAtUtc = utcNow + FailedRouteRetryDelay,
				NextEvaluationUtc = utcNow + EvaluationInterval,
				LastFailureStatus = chaseRoutePlan.Status,
				LastInvalidationReason = npcChaseInvalidationReason
			};
			return NpcChaseUpdateResult.Hold(routeRequested: true, npcChaseInvalidationReason);
		}
		value = new NpcChaseRouteState
		{
			NpcInstance = npcInstance,
			TargetInstance = targetInstance,
			Kind = NpcChaseRouteStateKind.Route,
			GeometryVersion = chaseRoutePlan.GeometryVersion,
			StartSample = current,
			TargetSample = target,
			Route = chaseRoutePlan,
			RouteIndex = 0,
			LastIssuedRouteIndex = -1,
			LastProgressPoint = current,
			LastProgressUtc = utcNow,
			NextEvaluationUtc = utcNow + EvaluationInterval,
			LastInvalidationReason = npcChaseInvalidationReason
		};
		states[npcInstance] = value;
		if (!follower.TrySelectDestination(provider, value, current, utcNow, out var destination2, out var shouldIssueMovement2, out var invalidationReason2))
		{
			states.Remove(npcInstance);
			return NpcChaseUpdateResult.Hold(routeRequested: true, invalidationReason2);
		}
		return NpcChaseUpdateResult.Move(NpcChaseMovementKind.Route, destination2, shouldIssueMovement2, routeRequested: true, npcChaseInvalidationReason);
	}

	internal NpcChaseUpdateResult UpdateReturnToHome(int npcInstance, ChaseNavigationPoint current, ChaseNavigationPoint home, double stopDistance, DateTime utcNow)
	{
		return UpdatePursuit(npcInstance, npcInstance, current, home, stopDistance, utcNow);
	}

	internal void Clear(int npcInstance, NpcChaseInvalidationReason reason)
	{
		states.Remove(npcInstance);
	}

	internal void ClearAll(NpcChaseInvalidationReason reason)
	{
		states.Clear();
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			ClearAll(NpcChaseInvalidationReason.RuntimeDisposed);
		}
	}

	private NpcChaseUpdateResult UpdateDirect(int npcInstance, int targetInstance, ChaseNavigationPoint current, ChaseNavigationPoint target, double stopDistance, DateTime utcNow, NpcChaseRouteState previous, NpcChaseInvalidationReason invalidationReason)
	{
		double num = current.Distance2D(target);
		if (num <= stopDistance + 0.3)
		{
			states.Remove(npcInstance);
			return NpcChaseUpdateResult.Hold(routeRequested: false, invalidationReason);
		}
		ChaseNavigationPoint chaseNavigationPoint = MoveToward(current, target, Math.Max(0.0, num - stopDistance));
		bool shouldIssueMovement = previous == null || previous.Kind != 0 || previous.DirectDestination.Distance2D(chaseNavigationPoint) >= 1.0;
		NpcChaseRouteState npcChaseRouteState = previous;
		if (npcChaseRouteState == null || npcChaseRouteState.Kind != 0)
		{
			npcChaseRouteState = new NpcChaseRouteState();
		}
		npcChaseRouteState.NpcInstance = npcInstance;
		npcChaseRouteState.TargetInstance = targetInstance;
		npcChaseRouteState.Kind = NpcChaseRouteStateKind.Direct;
		npcChaseRouteState.GeometryVersion = provider.GeometryVersion;
		npcChaseRouteState.StartSample = current;
		npcChaseRouteState.TargetSample = target;
		npcChaseRouteState.DirectDestination = chaseNavigationPoint;
		npcChaseRouteState.Route = null;
		npcChaseRouteState.RouteIndex = 0;
		npcChaseRouteState.LastIssuedRouteIndex = -1;
		npcChaseRouteState.LastProgressPoint = current;
		npcChaseRouteState.LastProgressUtc = utcNow;
		npcChaseRouteState.NextEvaluationUtc = utcNow + EvaluationInterval;
		npcChaseRouteState.RetryAtUtc = default(DateTime);
		npcChaseRouteState.LastFailureStatus = ChaseRoutePlanStatus.Success;
		npcChaseRouteState.LastInvalidationReason = invalidationReason;
		states[npcInstance] = npcChaseRouteState;
		return NpcChaseUpdateResult.Move(NpcChaseMovementKind.Direct, chaseNavigationPoint, shouldIssueMovement, routeRequested: false, invalidationReason);
	}

	private static ChaseNavigationPoint MoveToward(ChaseNavigationPoint start, ChaseNavigationPoint destination, double distance)
	{
		double num = start.Distance2D(destination);
		if (num <= 1E-08 || distance <= 0.0)
		{
			return start;
		}
		double num2 = Math.Min(num, distance) / num;
		return new ChaseNavigationPoint(start.X + (destination.X - start.X) * num2, start.Y + (destination.Y - start.Y) * num2, start.Z + (destination.Z - start.Z) * num2);
	}
}
