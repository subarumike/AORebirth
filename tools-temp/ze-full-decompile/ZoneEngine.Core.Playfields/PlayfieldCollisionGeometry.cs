using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldCollisionGeometry
{
	private struct BvhBounds
	{
		internal double MinimumX { get; private set; }

		internal double MinimumY { get; private set; }

		internal double MinimumZ { get; private set; }

		internal double MaximumX { get; private set; }

		internal double MaximumY { get; private set; }

		internal double MaximumZ { get; private set; }

		internal BvhBounds(double minimumX, double minimumY, double minimumZ, double maximumX, double maximumY, double maximumZ)
		{
			MinimumX = minimumX;
			MinimumY = minimumY;
			MinimumZ = minimumZ;
			MaximumX = maximumX;
			MaximumY = maximumY;
			MaximumZ = maximumZ;
		}

		internal static BvhBounds FromTriangle(CollisionTriangle triangle)
		{
			return new BvhBounds(triangle.MinimumX, triangle.MinimumY, triangle.MinimumZ, triangle.MaximumX, triangle.MaximumY, triangle.MaximumZ);
		}

		internal static BvhBounds Union(BvhBounds left, BvhBounds right)
		{
			return new BvhBounds(Math.Min(left.MinimumX, right.MinimumX), Math.Min(left.MinimumY, right.MinimumY), Math.Min(left.MinimumZ, right.MinimumZ), Math.Max(left.MaximumX, right.MaximumX), Math.Max(left.MaximumY, right.MaximumY), Math.Max(left.MaximumZ, right.MaximumZ));
		}
	}

	private struct BvhNode
	{
		internal BvhBounds Bounds { get; private set; }

		internal int StartIndex { get; private set; }

		internal int TriangleCount { get; private set; }

		internal int LeftChildIndex { get; private set; }

		internal int RightChildIndex { get; private set; }

		internal bool IsLeaf => TriangleCount > 0;

		private BvhNode(BvhBounds bounds, int startIndex, int triangleCount, int leftChildIndex, int rightChildIndex)
		{
			Bounds = bounds;
			StartIndex = startIndex;
			TriangleCount = triangleCount;
			LeftChildIndex = leftChildIndex;
			RightChildIndex = rightChildIndex;
		}

		internal static BvhNode Leaf(BvhBounds bounds, int startIndex, int triangleCount)
		{
			return new BvhNode(bounds, startIndex, triangleCount, -1, -1);
		}

		internal static BvhNode Branch(BvhBounds bounds, int leftChildIndex, int rightChildIndex)
		{
			return new BvhNode(bounds, 0, 0, leftChildIndex, rightChildIndex);
		}
	}

	private struct BvhOrderingKey
	{
		internal int TriangleIndex { get; private set; }

		internal int TriangleId { get; private set; }

		internal uint MortonCode { get; private set; }

		internal double CentroidX { get; private set; }

		internal double CentroidY { get; private set; }

		internal double CentroidZ { get; private set; }

		internal BvhOrderingKey(int triangleIndex, int triangleId, uint mortonCode, double centroidX, double centroidY, double centroidZ)
		{
			TriangleIndex = triangleIndex;
			TriangleId = triangleId;
			MortonCode = mortonCode;
			CentroidX = centroidX;
			CentroidY = centroidY;
			CentroidZ = centroidZ;
		}
	}

	private sealed class BvhOrderingKeyComparer : IComparer<BvhOrderingKey>
	{
		internal static readonly BvhOrderingKeyComparer Instance = new BvhOrderingKeyComparer();

		private BvhOrderingKeyComparer()
		{
		}

		public int Compare(BvhOrderingKey left, BvhOrderingKey right)
		{
			int num = left.MortonCode.CompareTo(right.MortonCode);
			if (num != 0)
			{
				return num;
			}
			num = left.CentroidX.CompareTo(right.CentroidX);
			if (num != 0)
			{
				return num;
			}
			num = left.CentroidY.CompareTo(right.CentroidY);
			if (num != 0)
			{
				return num;
			}
			num = left.CentroidZ.CompareTo(right.CentroidZ);
			return (num != 0) ? num : left.TriangleId.CompareTo(right.TriangleId);
		}
	}

	private struct CollisionPoint2
	{
		internal double X { get; private set; }

		internal double Y { get; private set; }

		internal CollisionPoint2(double x, double y)
		{
			X = x;
			Y = y;
		}
	}

	internal const int SupportedSchemaVersion = 1;

	internal const double MaximumDamageLineOfSightProbeHeight = 10.0;

	private const double CoordinateTolerance = 1E-08;

	private const double DirectionToleranceSquared = 1E-20;

	private const double EndpointFractionTolerance = 1E-07;

	private const double ParallelTolerance = 1E-10;

	private const int BvhLeafTriangleCount = 8;

	private readonly CollisionTriangle[] triangles;

	private readonly BvhNode[] bvhNodes;

	private readonly int[] bvhTriangleIndices;

	private readonly int traversalStackCapacity;

	internal int SchemaVersion { get; private set; }

	internal int PlayfieldResource { get; private set; }

	internal string Source { get; private set; }

	internal string SourceSha256 { get; private set; }

	internal double DamageLineOfSightProbeHeight { get; private set; }

	internal string DamageLineOfSightProbeHeightEvidence { get; private set; }

	internal int TriangleCount => triangles.Length;

	internal PlayfieldCollisionGeometry(int schemaVersion, int playfieldResource, string source, string sourceSha256, double damageLineOfSightProbeHeight, string damageLineOfSightProbeHeightEvidence, IEnumerable<CollisionTriangle> triangles)
	{
		if (schemaVersion != 1)
		{
			throw new ArgumentOutOfRangeException("schemaVersion");
		}
		if (playfieldResource <= 0)
		{
			throw new ArgumentOutOfRangeException("playfieldResource");
		}
		if (triangles == null)
		{
			throw new ArgumentNullException("triangles");
		}
		if (double.IsNaN(damageLineOfSightProbeHeight) || double.IsInfinity(damageLineOfSightProbeHeight) || damageLineOfSightProbeHeight < 0.0 || damageLineOfSightProbeHeight > 10.0)
		{
			throw new ArgumentOutOfRangeException("damageLineOfSightProbeHeight");
		}
		if (string.IsNullOrWhiteSpace(damageLineOfSightProbeHeightEvidence))
		{
			throw new ArgumentException("Damage line-of-sight probe height evidence is required.", "damageLineOfSightProbeHeightEvidence");
		}
		List<CollisionTriangle> list = new List<CollisionTriangle>();
		HashSet<int> hashSet = new HashSet<int>();
		foreach (CollisionTriangle triangle in triangles)
		{
			CollisionTriangle item = new CollisionTriangle(triangle.Id, triangle.A, triangle.B, triangle.C);
			if (!hashSet.Add(item.Id))
			{
				throw new ArgumentException("Triangle ids must be unique.", "triangles");
			}
			list.Add(item);
		}
		if (list.Count == 0)
		{
			throw new ArgumentException("Collision geometry must contain at least one triangle.", "triangles");
		}
		SchemaVersion = schemaVersion;
		PlayfieldResource = playfieldResource;
		Source = source ?? string.Empty;
		SourceSha256 = sourceSha256 ?? string.Empty;
		DamageLineOfSightProbeHeight = damageLineOfSightProbeHeight;
		DamageLineOfSightProbeHeightEvidence = damageLineOfSightProbeHeightEvidence.Trim();
		this.triangles = list.ToArray();
		BuildBvh(this.triangles, out bvhNodes, out bvhTriangleIndices, out var maximumDepth);
		traversalStackCapacity = maximumDepth + 2;
	}

	internal bool TryFindFirstBlockingHit(CollisionPoint3 start, CollisionPoint3 end, out SegmentTriangleHit hit)
	{
		int examinedTriangleCount;
		return TryFindFirstBlockingHit(start, end, out hit, out examinedTriangleCount);
	}

	internal bool TryFindFirstBlockingHit(CollisionPoint3 start, CollisionPoint3 end, out SegmentTriangleHit hit, out int examinedTriangleCount)
	{
		ValidateSegment(start, end);
		bool found = false;
		double nearestFraction = double.MaxValue;
		SegmentTriangleHit nearestHit = default(SegmentTriangleHit);
		examinedTriangleCount = 0;
		int[] array = new int[traversalStackCapacity];
		int num = 1;
		array[0] = 0;
		while (num > 0)
		{
			BvhNode bvhNode = bvhNodes[array[--num]];
			if (!SegmentIntersectsBounds(start, end, bvhNode.Bounds))
			{
				continue;
			}
			if (!bvhNode.IsLeaf)
			{
				array[num++] = bvhNode.RightChildIndex;
				array[num++] = bvhNode.LeftChildIndex;
				continue;
			}
			int num2 = bvhNode.StartIndex + bvhNode.TriangleCount;
			for (int i = bvhNode.StartIndex; i < num2; i++)
			{
				examinedTriangleCount++;
				CollisionTriangle triangle = triangles[bvhTriangleIndices[i]];
				ConsiderTriangleHit(start, end, triangle, ref found, ref nearestFraction, ref nearestHit);
			}
		}
		hit = nearestHit;
		return found;
	}

	internal bool TryFindFirstBlockingHitBruteForce(CollisionPoint3 start, CollisionPoint3 end, out SegmentTriangleHit hit)
	{
		ValidateSegment(start, end);
		bool found = false;
		double nearestFraction = double.MaxValue;
		SegmentTriangleHit nearestHit = default(SegmentTriangleHit);
		for (int i = 0; i < triangles.Length; i++)
		{
			ConsiderTriangleHit(start, end, triangles[i], ref found, ref nearestFraction, ref nearestHit);
		}
		hit = nearestHit;
		return found;
	}

	private static void ConsiderTriangleHit(CollisionPoint3 start, CollisionPoint3 end, CollisionTriangle triangle, ref bool found, ref double nearestFraction, ref SegmentTriangleHit nearestHit)
	{
		if (SegmentIntersectsBounds(start, end, triangle) && TryIntersectTriangle(start, end, triangle, out var fraction, out var point) && (!found || (!(fraction > nearestFraction) && (fraction != nearestFraction || triangle.Id < nearestHit.TriangleId))))
		{
			found = true;
			nearestFraction = fraction;
			nearestHit = new SegmentTriangleHit(triangle.Id, fraction, point);
		}
	}

	private static void BuildBvh(CollisionTriangle[] sourceTriangles, out BvhNode[] nodes, out int[] orderedTriangleIndices, out int maximumDepth)
	{
		BvhOrderingKey[] array = CreateBvhOrderingKeys(sourceTriangles);
		Array.Sort(array, BvhOrderingKeyComparer.Instance);
		orderedTriangleIndices = new int[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			orderedTriangleIndices[i] = array[i].TriangleIndex;
		}
		List<BvhNode> list = new List<BvhNode>(Math.Max(1, sourceTriangles.Length / 2));
		maximumDepth = 0;
		BuildBvhNode(sourceTriangles, orderedTriangleIndices, list, 0, orderedTriangleIndices.Length, 1, ref maximumDepth);
		nodes = list.ToArray();
	}

	private static BvhOrderingKey[] CreateBvhOrderingKeys(CollisionTriangle[] sourceTriangles)
	{
		double num = double.MaxValue;
		double num2 = double.MaxValue;
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		double num5 = double.MinValue;
		double num6 = double.MinValue;
		double[] array = new double[sourceTriangles.Length];
		double[] array2 = new double[sourceTriangles.Length];
		double[] array3 = new double[sourceTriangles.Length];
		for (int i = 0; i < sourceTriangles.Length; i++)
		{
			CollisionTriangle collisionTriangle = sourceTriangles[i];
			double num7 = Midpoint(collisionTriangle.MinimumX, collisionTriangle.MaximumX);
			double num8 = Midpoint(collisionTriangle.MinimumY, collisionTriangle.MaximumY);
			double num9 = Midpoint(collisionTriangle.MinimumZ, collisionTriangle.MaximumZ);
			array[i] = num7;
			array2[i] = num8;
			array3[i] = num9;
			num = Math.Min(num, num7);
			num2 = Math.Min(num2, num8);
			num3 = Math.Min(num3, num9);
			num4 = Math.Max(num4, num7);
			num5 = Math.Max(num5, num8);
			num6 = Math.Max(num6, num9);
		}
		BvhOrderingKey[] array4 = new BvhOrderingKey[sourceTriangles.Length];
		for (int j = 0; j < sourceTriangles.Length; j++)
		{
			uint x = QuantizeCentroid(array[j], num, num4);
			uint y = QuantizeCentroid(array2[j], num2, num5);
			uint z = QuantizeCentroid(array3[j], num3, num6);
			array4[j] = new BvhOrderingKey(j, sourceTriangles[j].Id, MortonCode(x, y, z), array[j], array2[j], array3[j]);
		}
		return array4;
	}

	private static int BuildBvhNode(CollisionTriangle[] sourceTriangles, int[] orderedTriangleIndices, IList<BvhNode> nodes, int startIndex, int triangleCount, int depth, ref int maximumDepth)
	{
		int count = nodes.Count;
		nodes.Add(default(BvhNode));
		maximumDepth = Math.Max(maximumDepth, depth);
		if (triangleCount <= 8)
		{
			BvhBounds bounds = BoundsForRange(sourceTriangles, orderedTriangleIndices, startIndex, triangleCount);
			nodes[count] = BvhNode.Leaf(bounds, startIndex, triangleCount);
			return count;
		}
		int num = triangleCount / 2;
		int triangleCount2 = triangleCount - num;
		int num2 = BuildBvhNode(sourceTriangles, orderedTriangleIndices, nodes, startIndex, num, depth + 1, ref maximumDepth);
		int num3 = BuildBvhNode(sourceTriangles, orderedTriangleIndices, nodes, startIndex + num, triangleCount2, depth + 1, ref maximumDepth);
		nodes[count] = BvhNode.Branch(BvhBounds.Union(nodes[num2].Bounds, nodes[num3].Bounds), num2, num3);
		return count;
	}

	private static BvhBounds BoundsForRange(CollisionTriangle[] sourceTriangles, int[] orderedTriangleIndices, int startIndex, int triangleCount)
	{
		BvhBounds bvhBounds = BvhBounds.FromTriangle(sourceTriangles[orderedTriangleIndices[startIndex]]);
		int num = startIndex + triangleCount;
		for (int i = startIndex + 1; i < num; i++)
		{
			bvhBounds = BvhBounds.Union(bvhBounds, BvhBounds.FromTriangle(sourceTriangles[orderedTriangleIndices[i]]));
		}
		return bvhBounds;
	}

	private static double Midpoint(double minimum, double maximum)
	{
		return minimum * 0.5 + maximum * 0.5;
	}

	private static uint QuantizeCentroid(double value, double minimum, double maximum)
	{
		if (maximum <= minimum)
		{
			return 0u;
		}
		double val = (value - minimum) / (maximum - minimum);
		val = Math.Max(0.0, Math.Min(1.0, val));
		return (uint)Math.Round(val * 1023.0, MidpointRounding.AwayFromZero);
	}

	private static uint MortonCode(uint x, uint y, uint z)
	{
		uint num = 0u;
		for (int i = 0; i < 10; i++)
		{
			num |= ((x >> i) & 1) << i * 3;
			num |= ((y >> i) & 1) << i * 3 + 1;
			num |= ((z >> i) & 1) << i * 3 + 2;
		}
		return num;
	}

	private static void ValidateSegment(CollisionPoint3 start, CollisionPoint3 end)
	{
		if (!start.IsFinite || !end.IsFinite)
		{
			throw new ArgumentOutOfRangeException("segment", "Segment coordinates must be finite.");
		}
		if (start.DistanceSquared(end) <= 1E-20)
		{
			throw new ArgumentException("Segment must have nonzero length.", "segment");
		}
	}

	private static bool SegmentIntersectsBounds(CollisionPoint3 start, CollisionPoint3 end, CollisionTriangle triangle)
	{
		return SegmentIntersectsBounds(start, end, new BvhBounds(triangle.MinimumX, triangle.MinimumY, triangle.MinimumZ, triangle.MaximumX, triangle.MaximumY, triangle.MaximumZ));
	}

	private static bool SegmentIntersectsBounds(CollisionPoint3 start, CollisionPoint3 end, BvhBounds bounds)
	{
		double minimumFraction = 0.0;
		double maximumFraction = 1.0;
		return ClipSegmentAxis(start.X, end.X - start.X, bounds.MinimumX - 1E-08, bounds.MaximumX + 1E-08, ref minimumFraction, ref maximumFraction) && ClipSegmentAxis(start.Y, end.Y - start.Y, bounds.MinimumY - 1E-08, bounds.MaximumY + 1E-08, ref minimumFraction, ref maximumFraction) && ClipSegmentAxis(start.Z, end.Z - start.Z, bounds.MinimumZ - 1E-08, bounds.MaximumZ + 1E-08, ref minimumFraction, ref maximumFraction);
	}

	private static bool ClipSegmentAxis(double origin, double direction, double minimum, double maximum, ref double minimumFraction, ref double maximumFraction)
	{
		if (Math.Abs(direction) <= 1E-08)
		{
			return origin >= minimum && origin <= maximum;
		}
		double num = (minimum - origin) / direction;
		double num2 = (maximum - origin) / direction;
		if (num > num2)
		{
			double num3 = num;
			num = num2;
			num2 = num3;
		}
		minimumFraction = Math.Max(minimumFraction, num);
		maximumFraction = Math.Min(maximumFraction, num2);
		return minimumFraction <= maximumFraction + 1E-08;
	}

	private static bool TryIntersectTriangle(CollisionPoint3 start, CollisionPoint3 end, CollisionTriangle triangle, out double fraction, out CollisionPoint3 point)
	{
		CollisionPoint3 left = Subtract(triangle.B, triangle.A);
		CollisionPoint3 right = Subtract(triangle.C, triangle.A);
		CollisionPoint3 collisionPoint = Cross(left, right);
		CollisionPoint3 collisionPoint2 = Subtract(end, start);
		double num = Math.Sqrt(Dot(collisionPoint, collisionPoint));
		double num2 = Math.Sqrt(Dot(collisionPoint2, collisionPoint2));
		double num3 = Dot(collisionPoint, Subtract(start, triangle.A));
		double num4 = Dot(collisionPoint, Subtract(end, triangle.A));
		double value = num3 / num;
		double value2 = num4 / num;
		double num5 = Dot(collisionPoint, collisionPoint2);
		double num6 = 1E-10 * num * num2;
		if (Math.Abs(num5) <= num6 && Math.Abs(value) <= 1E-08 && Math.Abs(value2) <= 1E-08)
		{
			return TryIntersectCoplanar(start, end, triangle, collisionPoint, out fraction, out point);
		}
		if (Math.Abs(num5) <= double.Epsilon)
		{
			fraction = 0.0;
			point = default(CollisionPoint3);
			return false;
		}
		fraction = (0.0 - num3) / num5;
		if (!IsInteriorSegmentFraction(fraction))
		{
			point = default(CollisionPoint3);
			return false;
		}
		point = Lerp(start, end, fraction);
		return PointInTriangle(point, triangle);
	}

	private static bool PointInTriangle(CollisionPoint3 point, CollisionTriangle triangle)
	{
		CollisionPoint3 collisionPoint = Subtract(triangle.B, triangle.A);
		CollisionPoint3 collisionPoint2 = Subtract(triangle.C, triangle.A);
		CollisionPoint3 left = Subtract(point, triangle.A);
		double num = Dot(collisionPoint, collisionPoint);
		double num2 = Dot(collisionPoint, collisionPoint2);
		double num3 = Dot(collisionPoint2, collisionPoint2);
		double num4 = Dot(left, collisionPoint);
		double num5 = Dot(left, collisionPoint2);
		double num6 = num * num3 - num2 * num2;
		if (Math.Abs(num6) <= double.Epsilon)
		{
			return false;
		}
		double num7 = (num3 * num4 - num2 * num5) / num6;
		double num8 = (num * num5 - num2 * num4) / num6;
		return num7 >= -1E-08 && num8 >= -1E-08 && num7 + num8 <= 1.00000001;
	}

	private static bool TryIntersectCoplanar(CollisionPoint3 start, CollisionPoint3 end, CollisionTriangle triangle, CollisionPoint3 normal, out double fraction, out CollisionPoint3 point)
	{
		int droppedAxis = DominantAxis(normal);
		CollisionPoint2 start2 = Project(start, droppedAxis);
		CollisionPoint2 end2 = Project(end, droppedAxis);
		CollisionPoint2 collisionPoint = Project(triangle.A, droppedAxis);
		CollisionPoint2 collisionPoint2 = Project(triangle.B, droppedAxis);
		CollisionPoint2 collisionPoint3 = Project(triangle.C, droppedAxis);
		double nearest = double.MaxValue;
		double num = 2E-07;
		if (PointInTriangle2(Lerp(start2, end2, num), collisionPoint, collisionPoint2, collisionPoint3))
		{
			nearest = num;
		}
		ConsiderCoplanarEdge(start2, end2, collisionPoint, collisionPoint2, ref nearest);
		ConsiderCoplanarEdge(start2, end2, collisionPoint2, collisionPoint3, ref nearest);
		ConsiderCoplanarEdge(start2, end2, collisionPoint3, collisionPoint, ref nearest);
		double num2 = 1.0 - num;
		if (PointInTriangle2(Lerp(start2, end2, num2), collisionPoint, collisionPoint2, collisionPoint3))
		{
			nearest = Math.Min(nearest, num2);
		}
		if (nearest == double.MaxValue || !IsInteriorSegmentFraction(nearest))
		{
			fraction = 0.0;
			point = default(CollisionPoint3);
			return false;
		}
		fraction = nearest;
		point = Lerp(start, end, nearest);
		return true;
	}

	private static void ConsiderCoplanarEdge(CollisionPoint2 start, CollisionPoint2 end, CollisionPoint2 edgeStart, CollisionPoint2 edgeEnd, ref double nearest)
	{
		if (TryIntersectSegments2(start, end, edgeStart, edgeEnd, out var fraction) && IsInteriorSegmentFraction(fraction))
		{
			nearest = Math.Min(nearest, fraction);
		}
	}

	private static bool TryIntersectSegments2(CollisionPoint2 start, CollisionPoint2 end, CollisionPoint2 edgeStart, CollisionPoint2 edgeEnd, out double fraction)
	{
		CollisionPoint2 collisionPoint = Subtract(end, start);
		CollisionPoint2 right = Subtract(edgeEnd, edgeStart);
		CollisionPoint2 left = Subtract(edgeStart, start);
		double num = Cross2(collisionPoint, right);
		if (Math.Abs(num) > 1E-08)
		{
			double num2 = Cross2(left, right) / num;
			double num3 = Cross2(left, collisionPoint) / num;
			if (num2 >= -1E-08 && num2 <= 1.00000001 && num3 >= -1E-08 && num3 <= 1.00000001)
			{
				fraction = Math.Max(0.0, Math.Min(1.0, num2));
				return true;
			}
			fraction = 0.0;
			return false;
		}
		if (Math.Abs(Cross2(left, collisionPoint)) > 1E-08)
		{
			fraction = 0.0;
			return false;
		}
		double num4 = Dot2(collisionPoint, collisionPoint);
		if (num4 <= 1E-20)
		{
			fraction = 0.0;
			return false;
		}
		double val = Dot2(left, collisionPoint) / num4;
		double val2 = Dot2(Subtract(edgeEnd, start), collisionPoint) / num4;
		double num5 = Math.Max(0.0, Math.Min(val, val2));
		double num6 = Math.Min(1.0, Math.Max(val, val2));
		if (num6 < num5 - 1E-08)
		{
			fraction = 0.0;
			return false;
		}
		fraction = ((num5 <= 1E-07) ? Math.Min(num6, 2E-07) : num5);
		return num6 - num5 > 1E-08;
	}

	private static bool PointInTriangle2(CollisionPoint2 point, CollisionPoint2 a, CollisionPoint2 b, CollisionPoint2 c)
	{
		double num = Cross2(Subtract(b, a), Subtract(point, a));
		double num2 = Cross2(Subtract(c, b), Subtract(point, b));
		double num3 = Cross2(Subtract(a, c), Subtract(point, c));
		bool flag = num < -1E-08 || num2 < -1E-08 || num3 < -1E-08;
		bool flag2 = num > 1E-08 || num2 > 1E-08 || num3 > 1E-08;
		return !(flag && flag2);
	}

	private static int DominantAxis(CollisionPoint3 normal)
	{
		double num = Math.Abs(normal.X);
		double num2 = Math.Abs(normal.Y);
		double num3 = Math.Abs(normal.Z);
		if (num >= num2 && num >= num3)
		{
			return 0;
		}
		return (num2 >= num3) ? 1 : 2;
	}

	private static CollisionPoint2 Project(CollisionPoint3 point, int droppedAxis)
	{
		CollisionPoint2 result;
		switch (droppedAxis)
		{
		case 0:
			return new CollisionPoint2(point.Y, point.Z);
		default:
			result = new CollisionPoint2(point.X, point.Y);
			break;
		case 1:
			result = new CollisionPoint2(point.X, point.Z);
			break;
		}
		return result;
	}

	private static bool IsInteriorSegmentFraction(double fraction)
	{
		return fraction > 1E-07 && fraction < 0.9999999;
	}

	private static CollisionPoint3 Subtract(CollisionPoint3 left, CollisionPoint3 right)
	{
		return new CollisionPoint3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
	}

	private static CollisionPoint2 Subtract(CollisionPoint2 left, CollisionPoint2 right)
	{
		return new CollisionPoint2(left.X - right.X, left.Y - right.Y);
	}

	private static CollisionPoint3 Cross(CollisionPoint3 left, CollisionPoint3 right)
	{
		return new CollisionPoint3(left.Y * right.Z - left.Z * right.Y, left.Z * right.X - left.X * right.Z, left.X * right.Y - left.Y * right.X);
	}

	private static double Dot(CollisionPoint3 left, CollisionPoint3 right)
	{
		return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
	}

	private static double Cross2(CollisionPoint2 left, CollisionPoint2 right)
	{
		return left.X * right.Y - left.Y * right.X;
	}

	private static double Dot2(CollisionPoint2 left, CollisionPoint2 right)
	{
		return left.X * right.X + left.Y * right.Y;
	}

	private static CollisionPoint3 Lerp(CollisionPoint3 start, CollisionPoint3 end, double fraction)
	{
		return new CollisionPoint3(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction, start.Z + (end.Z - start.Z) * fraction);
	}

	private static CollisionPoint2 Lerp(CollisionPoint2 start, CollisionPoint2 end, double fraction)
	{
		return new CollisionPoint2(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction);
	}
}
