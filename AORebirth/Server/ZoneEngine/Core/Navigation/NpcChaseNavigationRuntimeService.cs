namespace ZoneEngine.Core.Navigation
{
    using System;
    using System.Collections.Generic;

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

        private readonly Dictionary<int, NpcChaseRouteState> states =
            new Dictionary<int, NpcChaseRouteState>();

        private bool disposed;

        internal NpcChaseNavigationRuntimeService(IPlayfieldChaseNavigationProvider provider)
            : this(provider, ChaseRouteSearchLimits.Default, new NpcChaseRouteFollower())
        {
        }

        internal NpcChaseNavigationRuntimeService(
            IPlayfieldChaseNavigationProvider provider,
            ChaseRouteSearchLimits searchLimits,
            NpcChaseRouteFollower follower)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            this.provider = provider;
            this.searchLimits = searchLimits ?? throw new ArgumentNullException("searchLimits");
            this.follower = follower ?? throw new ArgumentNullException("follower");
        }

        internal ChaseNavigationCapability Capability
        {
            get { return this.provider.Capability; }
        }

        internal string GeometryVersion
        {
            get { return this.provider.GeometryVersion; }
        }

        internal int TotalRouteRequests { get; private set; }

        internal int ActiveStateCount
        {
            get { return this.states.Count; }
        }

        internal bool HasActivePursuit(int npcInstance)
        {
            NpcChaseRouteState state;
            return this.states.TryGetValue(npcInstance, out state)
                   && state.Kind != NpcChaseRouteStateKind.Failed;
        }

        internal bool HasState(int npcInstance)
        {
            return this.states.ContainsKey(npcInstance);
        }

        internal bool IsAttackPathTraversable(
            ChaseNavigationPoint start,
            ChaseNavigationPoint target)
        {
            if (this.provider.Capability == ChaseNavigationCapability.Unsupported)
            {
                return true;
            }

            return this.provider.Capability == ChaseNavigationCapability.Supported
                   && this.provider.IsAttackLineTraversable(start, target);
        }

        internal NpcChaseUpdateResult UpdatePursuit(
            int npcInstance,
            int targetInstance,
            ChaseNavigationPoint current,
            ChaseNavigationPoint target,
            double stopDistance,
            DateTime utcNow)
        {
            if (this.disposed || npcInstance <= 0 || targetInstance <= 0 || !current.IsFinite || !target.IsFinite)
            {
                return NpcChaseUpdateResult.Unavailable();
            }

            if (this.provider.Capability == ChaseNavigationCapability.Unsupported)
            {
                this.states.Remove(npcInstance);
                return NpcChaseUpdateResult.Unsupported();
            }

            if (this.provider.Capability != ChaseNavigationCapability.Supported)
            {
                this.states.Remove(npcInstance);
                return NpcChaseUpdateResult.Unavailable();
            }

            NpcChaseRouteState state;
            this.states.TryGetValue(npcInstance, out state);
            NpcChaseInvalidationReason invalidationReason = NpcChaseInvalidationReason.None;
            if (state != null)
            {
                if (state.TargetInstance != targetInstance)
                {
                    invalidationReason = NpcChaseInvalidationReason.TargetReplaced;
                    this.states.Remove(npcInstance);
                    state = null;
                }
                else if (!string.Equals(
                             state.GeometryVersion,
                             this.provider.GeometryVersion,
                             StringComparison.Ordinal))
                {
                    invalidationReason = NpcChaseInvalidationReason.GeometryVersionChanged;
                    this.states.Remove(npcInstance);
                    state = null;
                }
                else if (state.TargetSample.Distance2D(target) > TargetMovementReplanDistance)
                {
                    invalidationReason = NpcChaseInvalidationReason.TargetMoved;
                    this.states.Remove(npcInstance);
                    state = null;
                }
                else if (utcNow < state.NextEvaluationUtc)
                {
                    return NpcChaseUpdateResult.Hold(false, NpcChaseInvalidationReason.None);
                }
            }

            bool directTraversable = this.provider.IsSegmentTraversable(current, target);
            if (directTraversable)
            {
                if (state != null && state.Kind == NpcChaseRouteStateKind.Route)
                {
                    invalidationReason = NpcChaseInvalidationReason.DirectPathRestored;
                }

                return this.UpdateDirect(
                    npcInstance,
                    targetInstance,
                    current,
                    target,
                    Math.Max(0.0, stopDistance),
                    utcNow,
                    state,
                    invalidationReason);
            }

            if (state != null && state.Kind == NpcChaseRouteStateKind.Failed)
            {
                bool meaningfulStartChange =
                    state.StartSample.Distance2D(current) > FailureStartMovementRetryDistance;
                if (!meaningfulStartChange && utcNow < state.RetryAtUtc)
                {
                    state.NextEvaluationUtc = utcNow + EvaluationInterval;
                    return NpcChaseUpdateResult.Hold(false, invalidationReason);
                }

                this.states.Remove(npcInstance);
                state = null;
            }

            if (state != null && state.Kind == NpcChaseRouteStateKind.Route)
            {
                ChaseNavigationPoint destination;
                bool shouldIssueMovement;
                NpcChaseInvalidationReason followerInvalidation;
                if (this.follower.TrySelectDestination(
                    this.provider,
                    state,
                    current,
                    utcNow,
                    out destination,
                    out shouldIssueMovement,
                    out followerInvalidation))
                {
                    state.NextEvaluationUtc = utcNow + EvaluationInterval;
                    return NpcChaseUpdateResult.Move(
                        NpcChaseMovementKind.Route,
                        destination,
                        shouldIssueMovement,
                        false,
                        invalidationReason);
                }

                invalidationReason = followerInvalidation;
                this.states.Remove(npcInstance);
                state = null;
            }

            this.TotalRouteRequests++;
            ChaseRoutePlan route = this.provider.RequestRoute(
                current,
                target,
                this.searchLimits);
            if (!route.IsSuccess)
            {
                this.states[npcInstance] =
                    new NpcChaseRouteState
                    {
                        NpcInstance = npcInstance,
                        TargetInstance = targetInstance,
                        Kind = NpcChaseRouteStateKind.Failed,
                        GeometryVersion = this.provider.GeometryVersion,
                        StartSample = current,
                        TargetSample = target,
                        RetryAtUtc = utcNow + FailedRouteRetryDelay,
                        NextEvaluationUtc = utcNow + EvaluationInterval,
                        LastFailureStatus = route.Status,
                        LastInvalidationReason = invalidationReason
                    };
                return NpcChaseUpdateResult.Hold(true, invalidationReason);
            }

            state = new NpcChaseRouteState
                    {
                        NpcInstance = npcInstance,
                        TargetInstance = targetInstance,
                        Kind = NpcChaseRouteStateKind.Route,
                        GeometryVersion = route.GeometryVersion,
                        StartSample = current,
                        TargetSample = target,
                        Route = route,
                        RouteIndex = 0,
                        LastIssuedRouteIndex = -1,
                        LastProgressPoint = current,
                        LastProgressUtc = utcNow,
                        NextEvaluationUtc = utcNow + EvaluationInterval,
                        LastInvalidationReason = invalidationReason
                    };
            this.states[npcInstance] = state;

            ChaseNavigationPoint firstDestination;
            bool issueFirstMovement;
            NpcChaseInvalidationReason firstInvalidation;
            if (!this.follower.TrySelectDestination(
                this.provider,
                state,
                current,
                utcNow,
                out firstDestination,
                out issueFirstMovement,
                out firstInvalidation))
            {
                this.states.Remove(npcInstance);
                return NpcChaseUpdateResult.Hold(true, firstInvalidation);
            }

            return NpcChaseUpdateResult.Move(
                NpcChaseMovementKind.Route,
                firstDestination,
                issueFirstMovement,
                true,
                invalidationReason);
        }

        internal NpcChaseUpdateResult UpdateReturnToHome(
            int npcInstance,
            ChaseNavigationPoint current,
            ChaseNavigationPoint home,
            double stopDistance,
            DateTime utcNow)
        {
            return this.UpdatePursuit(
                npcInstance,
                npcInstance,
                current,
                home,
                stopDistance,
                utcNow);
        }

        internal void Clear(int npcInstance, NpcChaseInvalidationReason reason)
        {
            this.states.Remove(npcInstance);
        }

        internal void ClearAll(NpcChaseInvalidationReason reason)
        {
            this.states.Clear();
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.ClearAll(NpcChaseInvalidationReason.RuntimeDisposed);
        }

        private NpcChaseUpdateResult UpdateDirect(
            int npcInstance,
            int targetInstance,
            ChaseNavigationPoint current,
            ChaseNavigationPoint target,
            double stopDistance,
            DateTime utcNow,
            NpcChaseRouteState previous,
            NpcChaseInvalidationReason invalidationReason)
        {
            double distance = current.Distance2D(target);
            if (distance <= stopDistance + 0.3)
            {
                this.states.Remove(npcInstance);
                return NpcChaseUpdateResult.Hold(false, invalidationReason);
            }

            ChaseNavigationPoint destination = MoveToward(
                current,
                target,
                Math.Max(0.0, distance - stopDistance));
            bool shouldIssue = previous == null
                               || previous.Kind != NpcChaseRouteStateKind.Direct
                               || previous.DirectDestination.Distance2D(destination)
                                  >= DirectDestinationRefreshDistance;
            NpcChaseRouteState state = previous;
            if (state == null || state.Kind != NpcChaseRouteStateKind.Direct)
            {
                state = new NpcChaseRouteState();
            }

            state.NpcInstance = npcInstance;
            state.TargetInstance = targetInstance;
            state.Kind = NpcChaseRouteStateKind.Direct;
            state.GeometryVersion = this.provider.GeometryVersion;
            state.StartSample = current;
            state.TargetSample = target;
            state.DirectDestination = destination;
            state.Route = null;
            state.RouteIndex = 0;
            state.LastIssuedRouteIndex = -1;
            state.LastProgressPoint = current;
            state.LastProgressUtc = utcNow;
            state.NextEvaluationUtc = utcNow + EvaluationInterval;
            state.RetryAtUtc = default(DateTime);
            state.LastFailureStatus = ChaseRoutePlanStatus.Success;
            state.LastInvalidationReason = invalidationReason;
            this.states[npcInstance] = state;
            return NpcChaseUpdateResult.Move(
                NpcChaseMovementKind.Direct,
                destination,
                shouldIssue,
                false,
                invalidationReason);
        }

        private static ChaseNavigationPoint MoveToward(
            ChaseNavigationPoint start,
            ChaseNavigationPoint destination,
            double distance)
        {
            double total = start.Distance2D(destination);
            if (total <= 1.0e-8 || distance <= 0.0)
            {
                return start;
            }

            double factor = Math.Min(total, distance) / total;
            return new ChaseNavigationPoint(
                start.X + ((destination.X - start.X) * factor),
                start.Y + ((destination.Y - start.Y) * factor),
                start.Z + ((destination.Z - start.Z) * factor));
        }
    }
}
