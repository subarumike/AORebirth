using System;

namespace ZoneEngine.Core.Playfields;

internal sealed class NpcDamageLineOfSightRuntimeService
{
	internal const bool Pf127DamageLineOfSightActivated = true;

	internal const int VergilAeneidMonsterData = 203748;

	private const double MinimumSegmentLengthSquared = 1E-20;

	private readonly int playfieldResource;

	private readonly PlayfieldCollisionGeometryLoadResult geometryLoadResult;

	internal string GeometryError => geometryLoadResult.Error;

	internal NpcDamageLineOfSightRuntimeService(int playfieldResource)
		: this(playfieldResource, (playfieldResource == 127) ? Pf127CollisionGeometryLoader.Current : PlayfieldCollisionGeometryLoadResult.Failed("No collision geometry is registered for playfield " + playfieldResource + "."))
	{
	}

	internal NpcDamageLineOfSightRuntimeService(int playfieldResource, PlayfieldCollisionGeometryLoadResult geometryLoadResult)
	{
		if (playfieldResource <= 0)
		{
			throw new ArgumentOutOfRangeException("playfieldResource");
		}
		this.playfieldResource = playfieldResource;
		this.geometryLoadResult = geometryLoadResult ?? PlayfieldCollisionGeometryLoadResult.Failed("Collision geometry load result is missing.");
	}

	internal static bool IsDamageLineOfSightRequired(bool activationEnabled, int monsterData, bool? capturedContractRequiresDamageLineOfSight)
	{
		return activationEnabled && (monsterData == 203748 || capturedContractRequiresDamageLineOfSight == true);
	}

	internal NpcDamageLineOfSightDecision Evaluate(bool requiresDamageLineOfSight, CollisionPoint3 start, CollisionPoint3 end, out SegmentTriangleHit hit)
	{
		double probeHeight = (geometryLoadResult.IsLoaded ? geometryLoadResult.Geometry.DamageLineOfSightProbeHeight : 0.0);
		return EvaluateAtProbeHeight(requiresDamageLineOfSight, start, end, probeHeight, out hit);
	}

	internal NpcDamageLineOfSightDecision EvaluateAttackLine(bool requiresDamageLineOfSight, CollisionPoint3 start, CollisionPoint3 end, out SegmentTriangleHit hit)
	{
		return EvaluateAtProbeHeight(requiresDamageLineOfSight, start, end, 1.0, out hit);
	}

	private NpcDamageLineOfSightDecision EvaluateAtProbeHeight(bool requiresDamageLineOfSight, CollisionPoint3 start, CollisionPoint3 end, double probeHeight, out SegmentTriangleHit hit)
	{
		hit = default(SegmentTriangleHit);
		if (!requiresDamageLineOfSight)
		{
			return NpcDamageLineOfSightDecision.AllowedNotRequired;
		}
		if (playfieldResource != 127 || !geometryLoadResult.IsLoaded)
		{
			return NpcDamageLineOfSightDecision.DeniedGeometryUnavailable;
		}
		if (!start.IsFinite || !end.IsFinite || start.DistanceSquared(end) <= 1E-20)
		{
			return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
		}
		CollisionPoint3 start2 = new CollisionPoint3(start.X, start.Y + probeHeight, start.Z);
		CollisionPoint3 end2 = new CollisionPoint3(end.X, end.Y + probeHeight, end.Z);
		if (!start2.IsFinite || !end2.IsFinite)
		{
			return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
		}
		try
		{
			return (!geometryLoadResult.Geometry.TryFindFirstBlockingHit(start2, end2, out hit)) ? NpcDamageLineOfSightDecision.AllowedClear : NpcDamageLineOfSightDecision.DeniedBlocked;
		}
		catch (Exception)
		{
			return NpcDamageLineOfSightDecision.DeniedInvalidSegment;
		}
	}
}
