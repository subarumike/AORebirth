namespace ZoneEngine.Core.Playfields
{
    using System;

    using ZoneEngine.Core.Navigation;

    internal enum NpcDamageLineOfSightDecision
    {
        AllowedNotRequired = 0,
        AllowedClear = 1,
        DeniedBlocked = 2,
        DeniedGeometryUnavailable = 3,
        DeniedInvalidSegment = 4
    }

    internal sealed class NpcDamageLineOfSightRuntimeService
    {
        internal const bool Pf127DamageLineOfSightActivated = true;

        internal const int VergilAeneidMonsterData = 203748;

        private const double MinimumSegmentLengthSquared = 1.0e-20;

        private readonly int playfieldResource;

        private readonly PlayfieldCollisionGeometryLoadResult geometryLoadResult;

        internal NpcDamageLineOfSightRuntimeService(int playfieldResource)
            : this(
                playfieldResource,
                playfieldResource == Pf127CollisionGeometryLoader.SubwayPlayfieldResource
                    ? Pf127CollisionGeometryLoader.Current
                    : PlayfieldCollisionGeometryLoadResult.Failed(
                        "No collision geometry is registered for playfield " + playfieldResource + "."))
        {
        }

        internal NpcDamageLineOfSightRuntimeService(
            int playfieldResource,
            PlayfieldCollisionGeometryLoadResult geometryLoadResult)
        {
            if (playfieldResource <= 0)
            {
                throw new ArgumentOutOfRangeException("playfieldResource");
            }

            this.playfieldResource = playfieldResource;
            this.geometryLoadResult = geometryLoadResult
                                      ?? PlayfieldCollisionGeometryLoadResult.Failed(
                                          "Collision geometry load result is missing.");
        }

        internal string GeometryError
        {
            get
            {
                return this.geometryLoadResult.Error;
            }
        }

        internal static bool IsDamageLineOfSightRequired(
            bool activationEnabled,
            int monsterData,
            bool? capturedContractRequiresDamageLineOfSight)
        {
            return activationEnabled
                   && (monsterData == VergilAeneidMonsterData
                       || capturedContractRequiresDamageLineOfSight == true);
        }

        internal NpcDamageLineOfSightDecision Evaluate(
            bool requiresDamageLineOfSight,
            CollisionPoint3 start,
            CollisionPoint3 end,
            out SegmentTriangleHit hit)
        {
            double probeHeight = this.geometryLoadResult.IsLoaded
                                     ? this.geometryLoadResult.Geometry.DamageLineOfSightProbeHeight
                                     : 0.0;
            return this.EvaluateAtProbeHeight(
                requiresDamageLineOfSight,
                start,
                end,
                probeHeight,
                out hit);
        }

        internal NpcDamageLineOfSightDecision EvaluateAttackLine(
            bool requiresDamageLineOfSight,
            CollisionPoint3 start,
            CollisionPoint3 end,
            out SegmentTriangleHit hit)
        {
            return this.EvaluateAtProbeHeight(
                requiresDamageLineOfSight,
                start,
                end,
                Pf127ChaseNavigationProvider.AttackLineProbeHeight,
                out hit);
        }

        private NpcDamageLineOfSightDecision EvaluateAtProbeHeight(
            bool requiresDamageLineOfSight,
            CollisionPoint3 start,
            CollisionPoint3 end,
            double probeHeight,
            out SegmentTriangleHit hit)
        {
            hit = default(SegmentTriangleHit);
            if (!requiresDamageLineOfSight)
            {
                return NpcDamageLineOfSightDecision.AllowedNotRequired;
            }

            if (this.playfieldResource != Pf127CollisionGeometryLoader.SubwayPlayfieldResource
                || !this.geometryLoadResult.IsLoaded)
            {
                return NpcDamageLineOfSightDecision.DeniedGeometryUnavailable;
            }

            if (!start.IsFinite
                || !end.IsFinite
                || start.DistanceSquared(end) <= MinimumSegmentLengthSquared)
            {
                return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
            }

            var adjustedStart = new CollisionPoint3(
                start.X,
                start.Y + probeHeight,
                start.Z);
            var adjustedEnd = new CollisionPoint3(
                end.X,
                end.Y + probeHeight,
                end.Z);
            if (!adjustedStart.IsFinite || !adjustedEnd.IsFinite)
            {
                return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
            }

            try
            {
                return this.geometryLoadResult.Geometry.TryFindFirstBlockingHit(
                           adjustedStart,
                           adjustedEnd,
                           out hit)
                           ? NpcDamageLineOfSightDecision.DeniedBlocked
                           : NpcDamageLineOfSightDecision.AllowedClear;
            }
            catch (Exception)
            {
                return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
            }
        }
    }
}
