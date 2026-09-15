namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    internal struct CollisionPoint3
    {
        internal CollisionPoint3(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal double X { get; private set; }

        internal double Y { get; private set; }

        internal double Z { get; private set; }

        internal bool IsFinite
        {
            get
            {
                return IsFiniteValue(this.X)
                       && IsFiniteValue(this.Y)
                       && IsFiniteValue(this.Z);
            }
        }

        internal double DistanceSquared(CollisionPoint3 other)
        {
            double x = this.X - other.X;
            double y = this.Y - other.Y;
            double z = this.Z - other.Z;
            return (x * x) + (y * y) + (z * z);
        }

        private static bool IsFiniteValue(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal struct CollisionTriangle
    {
        private const double MinimumAreaSquared = 1.0e-20;

        internal CollisionTriangle(
            int id,
            CollisionPoint3 a,
            CollisionPoint3 b,
            CollisionPoint3 c)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException("id");
            }

            if (!a.IsFinite || !b.IsFinite || !c.IsFinite)
            {
                throw new ArgumentOutOfRangeException("triangle", "Triangle coordinates must be finite.");
            }

            double edge1X = b.X - a.X;
            double edge1Y = b.Y - a.Y;
            double edge1Z = b.Z - a.Z;
            double edge2X = c.X - a.X;
            double edge2Y = c.Y - a.Y;
            double edge2Z = c.Z - a.Z;
            double normalX = (edge1Y * edge2Z) - (edge1Z * edge2Y);
            double normalY = (edge1Z * edge2X) - (edge1X * edge2Z);
            double normalZ = (edge1X * edge2Y) - (edge1Y * edge2X);
            double areaSquared = (normalX * normalX) + (normalY * normalY) + (normalZ * normalZ);
            if (areaSquared <= MinimumAreaSquared)
            {
                throw new ArgumentException("Triangle must not be degenerate.", "triangle");
            }

            this.Id = id;
            this.A = a;
            this.B = b;
            this.C = c;
            this.MinimumX = Math.Min(a.X, Math.Min(b.X, c.X));
            this.MinimumY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
            this.MinimumZ = Math.Min(a.Z, Math.Min(b.Z, c.Z));
            this.MaximumX = Math.Max(a.X, Math.Max(b.X, c.X));
            this.MaximumY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            this.MaximumZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
        }

        internal int Id { get; private set; }

        internal CollisionPoint3 A { get; private set; }

        internal CollisionPoint3 B { get; private set; }

        internal CollisionPoint3 C { get; private set; }

        internal double MinimumX { get; private set; }

        internal double MinimumY { get; private set; }

        internal double MinimumZ { get; private set; }

        internal double MaximumX { get; private set; }

        internal double MaximumY { get; private set; }

        internal double MaximumZ { get; private set; }
    }

    internal struct SegmentTriangleHit
    {
        internal SegmentTriangleHit(int triangleId, double segmentFraction, CollisionPoint3 point)
        {
            this.TriangleId = triangleId;
            this.SegmentFraction = segmentFraction;
            this.Point = point;
        }

        internal int TriangleId { get; private set; }

        internal double SegmentFraction { get; private set; }

        internal CollisionPoint3 Point { get; private set; }
    }

    internal sealed class PlayfieldCollisionGeometry
    {
        internal const int SupportedSchemaVersion = 1;

        internal const double MaximumDamageLineOfSightProbeHeight = 10.0;

        private const double CoordinateTolerance = 1.0e-8;

        private const double DirectionToleranceSquared = 1.0e-20;

        private const double EndpointFractionTolerance = 1.0e-7;

        private const double ParallelTolerance = 1.0e-10;

        private const int BvhLeafTriangleCount = 8;

        private readonly CollisionTriangle[] triangles;

        private readonly BvhNode[] bvhNodes;

        private readonly int[] bvhTriangleIndices;

        private readonly int traversalStackCapacity;

        internal PlayfieldCollisionGeometry(
            int schemaVersion,
            int playfieldResource,
            string source,
            string sourceSha256,
            double damageLineOfSightProbeHeight,
            string damageLineOfSightProbeHeightEvidence,
            IEnumerable<CollisionTriangle> triangles)
        {
            if (schemaVersion != SupportedSchemaVersion)
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

            if (double.IsNaN(damageLineOfSightProbeHeight)
                || double.IsInfinity(damageLineOfSightProbeHeight)
                || damageLineOfSightProbeHeight < 0.0
                || damageLineOfSightProbeHeight > MaximumDamageLineOfSightProbeHeight)
            {
                throw new ArgumentOutOfRangeException("damageLineOfSightProbeHeight");
            }

            if (string.IsNullOrWhiteSpace(damageLineOfSightProbeHeightEvidence))
            {
                throw new ArgumentException(
                    "Damage line-of-sight probe height evidence is required.",
                    "damageLineOfSightProbeHeightEvidence");
            }

            var copy = new List<CollisionTriangle>();
            var identities = new HashSet<int>();
            foreach (CollisionTriangle triangle in triangles)
            {
                var validated = new CollisionTriangle(
                    triangle.Id,
                    triangle.A,
                    triangle.B,
                    triangle.C);
                if (!identities.Add(validated.Id))
                {
                    throw new ArgumentException("Triangle ids must be unique.", "triangles");
                }

                copy.Add(validated);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("Collision geometry must contain at least one triangle.", "triangles");
            }

            this.SchemaVersion = schemaVersion;
            this.PlayfieldResource = playfieldResource;
            this.Source = source ?? string.Empty;
            this.SourceSha256 = sourceSha256 ?? string.Empty;
            this.DamageLineOfSightProbeHeight = damageLineOfSightProbeHeight;
            this.DamageLineOfSightProbeHeightEvidence =
                damageLineOfSightProbeHeightEvidence.Trim();
            this.triangles = copy.ToArray();

            int maximumDepth;
            BuildBvh(
                this.triangles,
                out this.bvhNodes,
                out this.bvhTriangleIndices,
                out maximumDepth);
            this.traversalStackCapacity = maximumDepth + 2;
        }

        internal int SchemaVersion { get; private set; }

        internal int PlayfieldResource { get; private set; }

        internal string Source { get; private set; }

        internal string SourceSha256 { get; private set; }

        internal double DamageLineOfSightProbeHeight { get; private set; }

        internal string DamageLineOfSightProbeHeightEvidence { get; private set; }

        internal int TriangleCount
        {
            get
            {
                return this.triangles.Length;
            }
        }

        internal bool TryFindFirstBlockingHit(
            CollisionPoint3 start,
            CollisionPoint3 end,
            out SegmentTriangleHit hit)
        {
            int examinedTriangleCount;
            return this.TryFindFirstBlockingHit(
                start,
                end,
                out hit,
                out examinedTriangleCount);
        }

        internal bool TryFindFirstBlockingHit(
            CollisionPoint3 start,
            CollisionPoint3 end,
            out SegmentTriangleHit hit,
            out int examinedTriangleCount)
        {
            ValidateSegment(start, end);

            bool found = false;
            double nearestFraction = double.MaxValue;
            SegmentTriangleHit nearestHit = default(SegmentTriangleHit);
            examinedTriangleCount = 0;
            var traversalStack = new int[this.traversalStackCapacity];
            int stackCount = 1;
            traversalStack[0] = 0;
            while (stackCount > 0)
            {
                BvhNode node = this.bvhNodes[traversalStack[--stackCount]];
                if (!SegmentIntersectsBounds(start, end, node.Bounds))
                {
                    continue;
                }

                if (!node.IsLeaf)
                {
                    // The lower child index is visited first. The final nearest-hit
                    // result is nevertheless order independent because equal hit
                    // fractions are resolved by triangle id.
                    traversalStack[stackCount++] = node.RightChildIndex;
                    traversalStack[stackCount++] = node.LeftChildIndex;
                    continue;
                }

                int endIndex = node.StartIndex + node.TriangleCount;
                for (int orderIndex = node.StartIndex; orderIndex < endIndex; orderIndex++)
                {
                    examinedTriangleCount++;
                    CollisionTriangle triangle =
                        this.triangles[this.bvhTriangleIndices[orderIndex]];
                    ConsiderTriangleHit(
                        start,
                        end,
                        triangle,
                        ref found,
                        ref nearestFraction,
                        ref nearestHit);
                }
            }

            hit = nearestHit;
            return found;
        }

        internal bool TryFindFirstBlockingHitBruteForce(
            CollisionPoint3 start,
            CollisionPoint3 end,
            out SegmentTriangleHit hit)
        {
            ValidateSegment(start, end);

            bool found = false;
            double nearestFraction = double.MaxValue;
            SegmentTriangleHit nearestHit = default(SegmentTriangleHit);
            for (int index = 0; index < this.triangles.Length; index++)
            {
                ConsiderTriangleHit(
                    start,
                    end,
                    this.triangles[index],
                    ref found,
                    ref nearestFraction,
                    ref nearestHit);
            }

            hit = nearestHit;
            return found;
        }

        private static void ConsiderTriangleHit(
            CollisionPoint3 start,
            CollisionPoint3 end,
            CollisionTriangle triangle,
            ref bool found,
            ref double nearestFraction,
            ref SegmentTriangleHit nearestHit)
        {
            if (!SegmentIntersectsBounds(start, end, triangle))
            {
                return;
            }

            double fraction;
            CollisionPoint3 point;
            if (!TryIntersectTriangle(start, end, triangle, out fraction, out point)
                || (found
                    && (fraction > nearestFraction
                        || (fraction == nearestFraction
                            && triangle.Id >= nearestHit.TriangleId))))
            {
                return;
            }

            found = true;
            nearestFraction = fraction;
            nearestHit = new SegmentTriangleHit(triangle.Id, fraction, point);
        }

        private static void BuildBvh(
            CollisionTriangle[] sourceTriangles,
            out BvhNode[] nodes,
            out int[] orderedTriangleIndices,
            out int maximumDepth)
        {
            BvhOrderingKey[] orderingKeys = CreateBvhOrderingKeys(sourceTriangles);
            Array.Sort(orderingKeys, BvhOrderingKeyComparer.Instance);
            orderedTriangleIndices = new int[orderingKeys.Length];
            for (int index = 0; index < orderingKeys.Length; index++)
            {
                orderedTriangleIndices[index] = orderingKeys[index].TriangleIndex;
            }

            var nodeList = new List<BvhNode>(Math.Max(1, sourceTriangles.Length / 2));
            maximumDepth = 0;
            BuildBvhNode(
                sourceTriangles,
                orderedTriangleIndices,
                nodeList,
                0,
                orderedTriangleIndices.Length,
                1,
                ref maximumDepth);
            nodes = nodeList.ToArray();
        }

        private static BvhOrderingKey[] CreateBvhOrderingKeys(
            CollisionTriangle[] sourceTriangles)
        {
            double minimumX = double.MaxValue;
            double minimumY = double.MaxValue;
            double minimumZ = double.MaxValue;
            double maximumX = double.MinValue;
            double maximumY = double.MinValue;
            double maximumZ = double.MinValue;
            var centroidX = new double[sourceTriangles.Length];
            var centroidY = new double[sourceTriangles.Length];
            var centroidZ = new double[sourceTriangles.Length];
            for (int index = 0; index < sourceTriangles.Length; index++)
            {
                CollisionTriangle triangle = sourceTriangles[index];
                double x = Midpoint(triangle.MinimumX, triangle.MaximumX);
                double y = Midpoint(triangle.MinimumY, triangle.MaximumY);
                double z = Midpoint(triangle.MinimumZ, triangle.MaximumZ);
                centroidX[index] = x;
                centroidY[index] = y;
                centroidZ[index] = z;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                minimumZ = Math.Min(minimumZ, z);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                maximumZ = Math.Max(maximumZ, z);
            }

            var result = new BvhOrderingKey[sourceTriangles.Length];
            for (int index = 0; index < sourceTriangles.Length; index++)
            {
                uint x = QuantizeCentroid(centroidX[index], minimumX, maximumX);
                uint y = QuantizeCentroid(centroidY[index], minimumY, maximumY);
                uint z = QuantizeCentroid(centroidZ[index], minimumZ, maximumZ);
                result[index] = new BvhOrderingKey(
                    index,
                    sourceTriangles[index].Id,
                    MortonCode(x, y, z),
                    centroidX[index],
                    centroidY[index],
                    centroidZ[index]);
            }

            return result;
        }

        private static int BuildBvhNode(
            CollisionTriangle[] sourceTriangles,
            int[] orderedTriangleIndices,
            IList<BvhNode> nodes,
            int startIndex,
            int triangleCount,
            int depth,
            ref int maximumDepth)
        {
            int nodeIndex = nodes.Count;
            nodes.Add(default(BvhNode));
            maximumDepth = Math.Max(maximumDepth, depth);
            if (triangleCount <= BvhLeafTriangleCount)
            {
                BvhBounds bounds = BoundsForRange(
                    sourceTriangles,
                    orderedTriangleIndices,
                    startIndex,
                    triangleCount);
                nodes[nodeIndex] = BvhNode.Leaf(bounds, startIndex, triangleCount);
                return nodeIndex;
            }

            int leftCount = triangleCount / 2;
            int rightCount = triangleCount - leftCount;
            int leftChildIndex = BuildBvhNode(
                sourceTriangles,
                orderedTriangleIndices,
                nodes,
                startIndex,
                leftCount,
                depth + 1,
                ref maximumDepth);
            int rightChildIndex = BuildBvhNode(
                sourceTriangles,
                orderedTriangleIndices,
                nodes,
                startIndex + leftCount,
                rightCount,
                depth + 1,
                ref maximumDepth);
            nodes[nodeIndex] = BvhNode.Branch(
                BvhBounds.Union(nodes[leftChildIndex].Bounds, nodes[rightChildIndex].Bounds),
                leftChildIndex,
                rightChildIndex);
            return nodeIndex;
        }

        private static BvhBounds BoundsForRange(
            CollisionTriangle[] sourceTriangles,
            int[] orderedTriangleIndices,
            int startIndex,
            int triangleCount)
        {
            BvhBounds bounds = BvhBounds.FromTriangle(
                sourceTriangles[orderedTriangleIndices[startIndex]]);
            int endIndex = startIndex + triangleCount;
            for (int index = startIndex + 1; index < endIndex; index++)
            {
                bounds = BvhBounds.Union(
                    bounds,
                    BvhBounds.FromTriangle(sourceTriangles[orderedTriangleIndices[index]]));
            }

            return bounds;
        }

        private static double Midpoint(double minimum, double maximum)
        {
            return (minimum * 0.5) + (maximum * 0.5);
        }

        private static uint QuantizeCentroid(double value, double minimum, double maximum)
        {
            if (maximum <= minimum)
            {
                return 0;
            }

            double normalized = (value - minimum) / (maximum - minimum);
            normalized = Math.Max(0.0, Math.Min(1.0, normalized));
            return (uint)Math.Round(normalized * 1023.0, MidpointRounding.AwayFromZero);
        }

        private static uint MortonCode(uint x, uint y, uint z)
        {
            uint result = 0;
            for (int bit = 0; bit < 10; bit++)
            {
                result |= ((x >> bit) & 1U) << (bit * 3);
                result |= ((y >> bit) & 1U) << ((bit * 3) + 1);
                result |= ((z >> bit) & 1U) << ((bit * 3) + 2);
            }

            return result;
        }

        private static void ValidateSegment(CollisionPoint3 start, CollisionPoint3 end)
        {
            if (!start.IsFinite || !end.IsFinite)
            {
                throw new ArgumentOutOfRangeException("segment", "Segment coordinates must be finite.");
            }

            if (start.DistanceSquared(end) <= DirectionToleranceSquared)
            {
                throw new ArgumentException("Segment must have nonzero length.", "segment");
            }
        }

        private static bool SegmentIntersectsBounds(
            CollisionPoint3 start,
            CollisionPoint3 end,
            CollisionTriangle triangle)
        {
            return SegmentIntersectsBounds(
                start,
                end,
                new BvhBounds(
                    triangle.MinimumX,
                    triangle.MinimumY,
                    triangle.MinimumZ,
                    triangle.MaximumX,
                    triangle.MaximumY,
                    triangle.MaximumZ));
        }

        private static bool SegmentIntersectsBounds(
            CollisionPoint3 start,
            CollisionPoint3 end,
            BvhBounds bounds)
        {
            double minimumFraction = 0.0;
            double maximumFraction = 1.0;
            return ClipSegmentAxis(
                       start.X,
                       end.X - start.X,
                       bounds.MinimumX - CoordinateTolerance,
                       bounds.MaximumX + CoordinateTolerance,
                       ref minimumFraction,
                       ref maximumFraction)
                   && ClipSegmentAxis(
                       start.Y,
                       end.Y - start.Y,
                       bounds.MinimumY - CoordinateTolerance,
                       bounds.MaximumY + CoordinateTolerance,
                       ref minimumFraction,
                       ref maximumFraction)
                   && ClipSegmentAxis(
                       start.Z,
                       end.Z - start.Z,
                       bounds.MinimumZ - CoordinateTolerance,
                       bounds.MaximumZ + CoordinateTolerance,
                       ref minimumFraction,
                       ref maximumFraction);
        }

        private static bool ClipSegmentAxis(
            double origin,
            double direction,
            double minimum,
            double maximum,
            ref double minimumFraction,
            ref double maximumFraction)
        {
            if (Math.Abs(direction) <= CoordinateTolerance)
            {
                return origin >= minimum && origin <= maximum;
            }

            double first = (minimum - origin) / direction;
            double second = (maximum - origin) / direction;
            if (first > second)
            {
                double temporary = first;
                first = second;
                second = temporary;
            }

            minimumFraction = Math.Max(minimumFraction, first);
            maximumFraction = Math.Min(maximumFraction, second);
            return minimumFraction <= maximumFraction + CoordinateTolerance;
        }

        private static bool TryIntersectTriangle(
            CollisionPoint3 start,
            CollisionPoint3 end,
            CollisionTriangle triangle,
            out double fraction,
            out CollisionPoint3 point)
        {
            CollisionPoint3 edge1 = Subtract(triangle.B, triangle.A);
            CollisionPoint3 edge2 = Subtract(triangle.C, triangle.A);
            CollisionPoint3 normal = Cross(edge1, edge2);
            CollisionPoint3 direction = Subtract(end, start);
            double normalLength = Math.Sqrt(Dot(normal, normal));
            double directionLength = Math.Sqrt(Dot(direction, direction));
            double signedStart = Dot(normal, Subtract(start, triangle.A));
            double signedEnd = Dot(normal, Subtract(end, triangle.A));
            double startPlaneDistance = signedStart / normalLength;
            double endPlaneDistance = signedEnd / normalLength;
            double denominator = Dot(normal, direction);
            double parallelThreshold = ParallelTolerance * normalLength * directionLength;

            if (Math.Abs(denominator) <= parallelThreshold
                && Math.Abs(startPlaneDistance) <= CoordinateTolerance
                && Math.Abs(endPlaneDistance) <= CoordinateTolerance)
            {
                return TryIntersectCoplanar(
                    start,
                    end,
                    triangle,
                    normal,
                    out fraction,
                    out point);
            }

            if (Math.Abs(denominator) <= double.Epsilon)
            {
                fraction = 0.0;
                point = default(CollisionPoint3);
                return false;
            }

            fraction = -signedStart / denominator;
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
            CollisionPoint3 edge0 = Subtract(triangle.B, triangle.A);
            CollisionPoint3 edge1 = Subtract(triangle.C, triangle.A);
            CollisionPoint3 relative = Subtract(point, triangle.A);
            double dot00 = Dot(edge0, edge0);
            double dot01 = Dot(edge0, edge1);
            double dot11 = Dot(edge1, edge1);
            double dot20 = Dot(relative, edge0);
            double dot21 = Dot(relative, edge1);
            double denominator = (dot00 * dot11) - (dot01 * dot01);
            if (Math.Abs(denominator) <= double.Epsilon)
            {
                return false;
            }

            double first = ((dot11 * dot20) - (dot01 * dot21)) / denominator;
            double second = ((dot00 * dot21) - (dot01 * dot20)) / denominator;
            return first >= -CoordinateTolerance
                   && second >= -CoordinateTolerance
                   && first + second <= 1.0 + CoordinateTolerance;
        }

        private static bool TryIntersectCoplanar(
            CollisionPoint3 start,
            CollisionPoint3 end,
            CollisionTriangle triangle,
            CollisionPoint3 normal,
            out double fraction,
            out CollisionPoint3 point)
        {
            int droppedAxis = DominantAxis(normal);
            CollisionPoint2 start2 = Project(start, droppedAxis);
            CollisionPoint2 end2 = Project(end, droppedAxis);
            CollisionPoint2 a = Project(triangle.A, droppedAxis);
            CollisionPoint2 b = Project(triangle.B, droppedAxis);
            CollisionPoint2 c = Project(triangle.C, droppedAxis);
            double nearest = double.MaxValue;

            double interiorProbe = EndpointFractionTolerance * 2.0;
            if (PointInTriangle2(Lerp(start2, end2, interiorProbe), a, b, c))
            {
                nearest = interiorProbe;
            }

            ConsiderCoplanarEdge(start2, end2, a, b, ref nearest);
            ConsiderCoplanarEdge(start2, end2, b, c, ref nearest);
            ConsiderCoplanarEdge(start2, end2, c, a, ref nearest);

            double endProbe = 1.0 - interiorProbe;
            if (PointInTriangle2(Lerp(start2, end2, endProbe), a, b, c))
            {
                nearest = Math.Min(nearest, endProbe);
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

        private static void ConsiderCoplanarEdge(
            CollisionPoint2 start,
            CollisionPoint2 end,
            CollisionPoint2 edgeStart,
            CollisionPoint2 edgeEnd,
            ref double nearest)
        {
            double fraction;
            if (TryIntersectSegments2(start, end, edgeStart, edgeEnd, out fraction)
                && IsInteriorSegmentFraction(fraction))
            {
                nearest = Math.Min(nearest, fraction);
            }
        }

        private static bool TryIntersectSegments2(
            CollisionPoint2 start,
            CollisionPoint2 end,
            CollisionPoint2 edgeStart,
            CollisionPoint2 edgeEnd,
            out double fraction)
        {
            CollisionPoint2 direction = Subtract(end, start);
            CollisionPoint2 edge = Subtract(edgeEnd, edgeStart);
            CollisionPoint2 offset = Subtract(edgeStart, start);
            double denominator = Cross2(direction, edge);
            if (Math.Abs(denominator) > CoordinateTolerance)
            {
                double segmentFraction = Cross2(offset, edge) / denominator;
                double edgeFraction = Cross2(offset, direction) / denominator;
                if (segmentFraction >= -CoordinateTolerance
                    && segmentFraction <= 1.0 + CoordinateTolerance
                    && edgeFraction >= -CoordinateTolerance
                    && edgeFraction <= 1.0 + CoordinateTolerance)
                {
                    fraction = Math.Max(0.0, Math.Min(1.0, segmentFraction));
                    return true;
                }

                fraction = 0.0;
                return false;
            }

            if (Math.Abs(Cross2(offset, direction)) > CoordinateTolerance)
            {
                fraction = 0.0;
                return false;
            }

            double directionLengthSquared = Dot2(direction, direction);
            if (directionLengthSquared <= DirectionToleranceSquared)
            {
                fraction = 0.0;
                return false;
            }

            double first = Dot2(offset, direction) / directionLengthSquared;
            double second = Dot2(Subtract(edgeEnd, start), direction) / directionLengthSquared;
            double overlapStart = Math.Max(0.0, Math.Min(first, second));
            double overlapEnd = Math.Min(1.0, Math.Max(first, second));
            if (overlapEnd < overlapStart - CoordinateTolerance)
            {
                fraction = 0.0;
                return false;
            }

            fraction = overlapStart <= EndpointFractionTolerance
                           ? Math.Min(overlapEnd, EndpointFractionTolerance * 2.0)
                           : overlapStart;
            return overlapEnd - overlapStart > CoordinateTolerance;
        }

        private static bool PointInTriangle2(
            CollisionPoint2 point,
            CollisionPoint2 a,
            CollisionPoint2 b,
            CollisionPoint2 c)
        {
            double first = Cross2(Subtract(b, a), Subtract(point, a));
            double second = Cross2(Subtract(c, b), Subtract(point, b));
            double third = Cross2(Subtract(a, c), Subtract(point, c));
            bool hasNegative = first < -CoordinateTolerance
                               || second < -CoordinateTolerance
                               || third < -CoordinateTolerance;
            bool hasPositive = first > CoordinateTolerance
                               || second > CoordinateTolerance
                               || third > CoordinateTolerance;
            return !(hasNegative && hasPositive);
        }

        private static int DominantAxis(CollisionPoint3 normal)
        {
            double x = Math.Abs(normal.X);
            double y = Math.Abs(normal.Y);
            double z = Math.Abs(normal.Z);
            if (x >= y && x >= z)
            {
                return 0;
            }

            return y >= z ? 1 : 2;
        }

        private static CollisionPoint2 Project(CollisionPoint3 point, int droppedAxis)
        {
            if (droppedAxis == 0)
            {
                return new CollisionPoint2(point.Y, point.Z);
            }

            return droppedAxis == 1
                       ? new CollisionPoint2(point.X, point.Z)
                       : new CollisionPoint2(point.X, point.Y);
        }

        private static bool IsInteriorSegmentFraction(double fraction)
        {
            return fraction > EndpointFractionTolerance
                   && fraction < 1.0 - EndpointFractionTolerance;
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
            return new CollisionPoint3(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
        }

        private static double Dot(CollisionPoint3 left, CollisionPoint3 right)
        {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        }

        private static double Cross2(CollisionPoint2 left, CollisionPoint2 right)
        {
            return (left.X * right.Y) - (left.Y * right.X);
        }

        private static double Dot2(CollisionPoint2 left, CollisionPoint2 right)
        {
            return (left.X * right.X) + (left.Y * right.Y);
        }

        private static CollisionPoint3 Lerp(CollisionPoint3 start, CollisionPoint3 end, double fraction)
        {
            return new CollisionPoint3(
                start.X + ((end.X - start.X) * fraction),
                start.Y + ((end.Y - start.Y) * fraction),
                start.Z + ((end.Z - start.Z) * fraction));
        }

        private static CollisionPoint2 Lerp(CollisionPoint2 start, CollisionPoint2 end, double fraction)
        {
            return new CollisionPoint2(
                start.X + ((end.X - start.X) * fraction),
                start.Y + ((end.Y - start.Y) * fraction));
        }

        private struct BvhBounds
        {
            internal BvhBounds(
                double minimumX,
                double minimumY,
                double minimumZ,
                double maximumX,
                double maximumY,
                double maximumZ)
            {
                this.MinimumX = minimumX;
                this.MinimumY = minimumY;
                this.MinimumZ = minimumZ;
                this.MaximumX = maximumX;
                this.MaximumY = maximumY;
                this.MaximumZ = maximumZ;
            }

            internal double MinimumX { get; private set; }

            internal double MinimumY { get; private set; }

            internal double MinimumZ { get; private set; }

            internal double MaximumX { get; private set; }

            internal double MaximumY { get; private set; }

            internal double MaximumZ { get; private set; }

            internal static BvhBounds FromTriangle(CollisionTriangle triangle)
            {
                return new BvhBounds(
                    triangle.MinimumX,
                    triangle.MinimumY,
                    triangle.MinimumZ,
                    triangle.MaximumX,
                    triangle.MaximumY,
                    triangle.MaximumZ);
            }

            internal static BvhBounds Union(BvhBounds left, BvhBounds right)
            {
                return new BvhBounds(
                    Math.Min(left.MinimumX, right.MinimumX),
                    Math.Min(left.MinimumY, right.MinimumY),
                    Math.Min(left.MinimumZ, right.MinimumZ),
                    Math.Max(left.MaximumX, right.MaximumX),
                    Math.Max(left.MaximumY, right.MaximumY),
                    Math.Max(left.MaximumZ, right.MaximumZ));
            }
        }

        private struct BvhNode
        {
            private BvhNode(
                BvhBounds bounds,
                int startIndex,
                int triangleCount,
                int leftChildIndex,
                int rightChildIndex)
            {
                this.Bounds = bounds;
                this.StartIndex = startIndex;
                this.TriangleCount = triangleCount;
                this.LeftChildIndex = leftChildIndex;
                this.RightChildIndex = rightChildIndex;
            }

            internal BvhBounds Bounds { get; private set; }

            internal int StartIndex { get; private set; }

            internal int TriangleCount { get; private set; }

            internal int LeftChildIndex { get; private set; }

            internal int RightChildIndex { get; private set; }

            internal bool IsLeaf
            {
                get { return this.TriangleCount > 0; }
            }

            internal static BvhNode Leaf(
                BvhBounds bounds,
                int startIndex,
                int triangleCount)
            {
                return new BvhNode(bounds, startIndex, triangleCount, -1, -1);
            }

            internal static BvhNode Branch(
                BvhBounds bounds,
                int leftChildIndex,
                int rightChildIndex)
            {
                return new BvhNode(bounds, 0, 0, leftChildIndex, rightChildIndex);
            }
        }

        private struct BvhOrderingKey
        {
            internal BvhOrderingKey(
                int triangleIndex,
                int triangleId,
                uint mortonCode,
                double centroidX,
                double centroidY,
                double centroidZ)
            {
                this.TriangleIndex = triangleIndex;
                this.TriangleId = triangleId;
                this.MortonCode = mortonCode;
                this.CentroidX = centroidX;
                this.CentroidY = centroidY;
                this.CentroidZ = centroidZ;
            }

            internal int TriangleIndex { get; private set; }

            internal int TriangleId { get; private set; }

            internal uint MortonCode { get; private set; }

            internal double CentroidX { get; private set; }

            internal double CentroidY { get; private set; }

            internal double CentroidZ { get; private set; }
        }

        private sealed class BvhOrderingKeyComparer : IComparer<BvhOrderingKey>
        {
            internal static readonly BvhOrderingKeyComparer Instance =
                new BvhOrderingKeyComparer();

            private BvhOrderingKeyComparer()
            {
            }

            public int Compare(BvhOrderingKey left, BvhOrderingKey right)
            {
                int result = left.MortonCode.CompareTo(right.MortonCode);
                if (result != 0)
                {
                    return result;
                }

                result = left.CentroidX.CompareTo(right.CentroidX);
                if (result != 0)
                {
                    return result;
                }

                result = left.CentroidY.CompareTo(right.CentroidY);
                if (result != 0)
                {
                    return result;
                }

                result = left.CentroidZ.CompareTo(right.CentroidZ);
                return result != 0 ? result : left.TriangleId.CompareTo(right.TriangleId);
            }
        }

        private struct CollisionPoint2
        {
            internal CollisionPoint2(double x, double y)
            {
                this.X = x;
                this.Y = y;
            }

            internal double X { get; private set; }

            internal double Y { get; private set; }
        }
    }
}
