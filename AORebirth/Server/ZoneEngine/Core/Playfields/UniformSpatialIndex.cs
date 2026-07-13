namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal struct VisibilityPosition
    {
        internal VisibilityPosition(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
    }

    internal sealed class UniformSpatialIndex<TValue>
        where TValue : class
    {
        private readonly object sync = new object();
        private readonly float cellSize;
        private readonly Dictionary<Identity, IndexedValue> values =
            new Dictionary<Identity, IndexedValue>();
        private readonly Dictionary<CellKey, Dictionary<Identity, IndexedValue>> cells =
            new Dictionary<CellKey, Dictionary<Identity, IndexedValue>>();

        private int lastCandidateInspectionCount;

        internal UniformSpatialIndex(float cellSize)
        {
            if (float.IsNaN(cellSize)
                || float.IsInfinity(cellSize)
                || cellSize < PlayfieldVisibilityInterestPolicy.MinimumCellSize
                || cellSize > PlayfieldVisibilityInterestPolicy.MaximumCellSize)
            {
                throw new ArgumentOutOfRangeException("cellSize");
            }

            this.cellSize = cellSize;
        }

        internal int Count
        {
            get
            {
                lock (this.sync)
                {
                    return this.values.Count;
                }
            }
        }

        internal int LastCandidateInspectionCount
        {
            get
            {
                lock (this.sync)
                {
                    return this.lastCandidateInspectionCount;
                }
            }
        }

        internal void Upsert(Identity identity, VisibilityPosition position, TValue value)
        {
            ValidateIdentity(identity);
            ValidatePosition(position, "position");
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var newCell = this.CellFor(position.X, position.Z);
            lock (this.sync)
            {
                IndexedValue existing;
                if (this.values.TryGetValue(identity, out existing))
                {
                    if (!ReferenceEquals(existing.Value, value))
                    {
                        throw new InvalidOperationException(
                            "Spatial visibility identity is already indexed by another character: "
                            + identity);
                    }

                    if (!existing.Cell.Equals(newCell))
                    {
                        this.RemoveFromCell(existing);
                        existing.Cell = newCell;
                        this.AddToCell(existing);
                    }

                    existing.X = position.X;
                    existing.Z = position.Z;
                    return;
                }

                var indexed = new IndexedValue(
                    identity,
                    position.X,
                    position.Z,
                    newCell,
                    value);
                this.values.Add(identity, indexed);
                this.AddToCell(indexed);
            }
        }

        internal bool Remove(Identity identity)
        {
            lock (this.sync)
            {
                IndexedValue existing;
                if (!this.values.TryGetValue(identity, out existing))
                {
                    return false;
                }

                this.RemoveFromCell(existing);
                this.values.Remove(identity);
                return true;
            }
        }

        internal IReadOnlyList<TValue> Query(VisibilityPosition center, float radius)
        {
            ValidatePosition(center, "center");
            if (float.IsNaN(radius)
                || float.IsInfinity(radius)
                || radius <= 0.0f
                || radius > PlayfieldVisibilityInterestPolicy.MaximumLeaveRadius)
            {
                throw new ArgumentOutOfRangeException("radius");
            }

            int minimumX = this.CellCoordinate((double)center.X - radius);
            int maximumX = this.CellCoordinate((double)center.X + radius);
            int minimumZ = this.CellCoordinate((double)center.Z - radius);
            int maximumZ = this.CellCoordinate((double)center.Z + radius);
            double radiusSquared = (double)radius * radius;
            var matches = new List<QueryMatch>();
            int inspected = 0;

            lock (this.sync)
            {
                for (long x = minimumX; x <= maximumX; x++)
                {
                    for (long z = minimumZ; z <= maximumZ; z++)
                    {
                        Dictionary<Identity, IndexedValue> bucket;
                        if (!this.cells.TryGetValue(
                                new CellKey((int)x, (int)z),
                                out bucket))
                        {
                            continue;
                        }

                        inspected += bucket.Count;
                        foreach (IndexedValue candidate in bucket.Values)
                        {
                            double distanceSquared = DistanceSquared(center, candidate);
                            if (distanceSquared <= radiusSquared)
                            {
                                matches.Add(new QueryMatch(candidate, distanceSquared));
                            }
                        }
                    }
                }

                this.lastCandidateInspectionCount = inspected;
            }

            return matches
                .OrderBy(value => value.DistanceSquared)
                .ThenBy(value => (int)value.Indexed.Identity.Type)
                .ThenBy(value => value.Indexed.Identity.Instance)
                .Select(value => value.Indexed.Value)
                .ToArray();
        }

        internal void Clear()
        {
            lock (this.sync)
            {
                this.values.Clear();
                this.cells.Clear();
                this.lastCandidateInspectionCount = 0;
            }
        }

        private static void ValidateIdentity(Identity identity)
        {
            if (identity == Identity.None || identity.Instance <= 0)
            {
                throw new ArgumentException("Spatial visibility identity is required.", "identity");
            }
        }

        private static void ValidatePosition(VisibilityPosition position, string name)
        {
            if (float.IsNaN(position.X)
                || float.IsInfinity(position.X)
                || float.IsNaN(position.Y)
                || float.IsInfinity(position.Y)
                || float.IsNaN(position.Z)
                || float.IsInfinity(position.Z))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static double DistanceSquared(VisibilityPosition center, IndexedValue candidate)
        {
            double x = center.X - candidate.X;
            double z = center.Z - candidate.Z;
            return (x * x) + (z * z);
        }

        private CellKey CellFor(float x, float z)
        {
            return new CellKey(this.CellCoordinate(x), this.CellCoordinate(z));
        }

        private int CellCoordinate(double value)
        {
            double cell = Math.Floor(value / this.cellSize);
            if (cell < int.MinValue || cell > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("position");
            }

            return (int)cell;
        }

        private void AddToCell(IndexedValue indexed)
        {
            Dictionary<Identity, IndexedValue> bucket;
            if (!this.cells.TryGetValue(indexed.Cell, out bucket))
            {
                bucket = new Dictionary<Identity, IndexedValue>();
                this.cells.Add(indexed.Cell, bucket);
            }

            bucket.Add(indexed.Identity, indexed);
        }

        private void RemoveFromCell(IndexedValue indexed)
        {
            Dictionary<Identity, IndexedValue> bucket;
            if (!this.cells.TryGetValue(indexed.Cell, out bucket))
            {
                throw new InvalidOperationException("Spatial visibility cell membership is corrupt.");
            }

            bucket.Remove(indexed.Identity);
            if (bucket.Count == 0)
            {
                this.cells.Remove(indexed.Cell);
            }
        }

        private sealed class IndexedValue
        {
            internal IndexedValue(
                Identity identity,
                float x,
                float z,
                CellKey cell,
                TValue value)
            {
                this.Identity = identity;
                this.X = x;
                this.Z = z;
                this.Cell = cell;
                this.Value = value;
            }

            internal Identity Identity { get; private set; }
            internal float X { get; set; }
            internal float Z { get; set; }
            internal CellKey Cell { get; set; }
            internal TValue Value { get; private set; }
        }

        private sealed class QueryMatch
        {
            internal QueryMatch(IndexedValue indexed, double distanceSquared)
            {
                this.Indexed = indexed;
                this.DistanceSquared = distanceSquared;
            }

            internal IndexedValue Indexed { get; private set; }
            internal double DistanceSquared { get; private set; }
        }

        private struct CellKey
        {
            internal CellKey(int x, int z)
            {
                this.X = x;
                this.Z = z;
            }

            private int X { get; set; }
            private int Z { get; set; }

            public override bool Equals(object obj)
            {
                if (!(obj is CellKey))
                {
                    return false;
                }

                var other = (CellKey)obj;
                return this.X == other.X && this.Z == other.Z;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = (hashCode * 23) + this.X;
                    hashCode = (hashCode * 23) + this.Z;
                    return hashCode;
                }
            }
        }
    }
}
