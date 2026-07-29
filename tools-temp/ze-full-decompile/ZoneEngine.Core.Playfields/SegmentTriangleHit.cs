namespace ZoneEngine.Core.Playfields;

internal struct SegmentTriangleHit
{
	internal int TriangleId { get; private set; }

	internal double SegmentFraction { get; private set; }

	internal CollisionPoint3 Point { get; private set; }

	internal SegmentTriangleHit(int triangleId, double segmentFraction, CollisionPoint3 point)
	{
		TriangleId = triangleId;
		SegmentFraction = segmentFraction;
		Point = point;
	}
}
