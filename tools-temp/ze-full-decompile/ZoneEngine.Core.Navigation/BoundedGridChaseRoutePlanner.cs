using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Navigation;

internal sealed class BoundedGridChaseRoutePlanner
{
	private sealed class PlannerContext
	{
		private readonly IPlayfieldChaseNavigationProvider provider;

		private readonly ChaseRouteSearchLimits limits;

		private readonly double minimumX;

		private readonly double maximumX;

		private readonly double minimumZ;

		private readonly double maximumZ;

		private readonly Dictionary<SurfaceKey, SurfaceProjection> projections = new Dictionary<SurfaceKey, SurfaceProjection>();

		internal int ExpandedNodes { get; set; }

		internal int SegmentChecks { get; private set; }

		internal PlannerContext(IPlayfieldChaseNavigationProvider provider, ChaseRouteSearchLimits limits, ChaseNavigationPoint start, ChaseNavigationPoint goal)
		{
			this.provider = provider;
			this.limits = limits;
			minimumX = Math.Min(start.X, goal.X) - limits.DetourMargin;
			maximumX = Math.Max(start.X, goal.X) + limits.DetourMargin;
			minimumZ = Math.Min(start.Z, goal.Z) - limits.DetourMargin;
			maximumZ = Math.Max(start.Z, goal.Z) + limits.DetourMargin;
		}

		internal bool IsWithinBounds(GridKey key)
		{
			double num = (double)key.X * limits.CellSize;
			double num2 = (double)key.Z * limits.CellSize;
			return num >= minimumX && num <= maximumX && num2 >= minimumZ && num2 <= maximumZ;
		}

		internal bool TryProject(GridKey key, ChaseNavigationPoint reference, out ChaseNavigationPoint projected)
		{
			int layer = (int)Math.Round(reference.Y / limits.MaximumVerticalStep);
			SurfaceKey key2 = new SurfaceKey(key, layer);
			if (projections.TryGetValue(key2, out var value))
			{
				projected = value.Point;
				return value.Found;
			}
			bool flag = provider.TryProjectToSurface(reference, (double)key.X * limits.CellSize, (double)key.Z * limits.CellSize, out projected);
			projections[key2] = new SurfaceProjection(flag, projected);
			return flag;
		}

		internal bool TrySegment(ChaseNavigationPoint start, ChaseNavigationPoint end)
		{
			if (SegmentChecks >= limits.MaximumSegmentChecks)
			{
				return false;
			}
			SegmentChecks++;
			return provider.IsSegmentTraversable(start, end);
		}

		internal ChaseNavigationPoint[] Smooth(ChaseNavigationPoint start, ChaseNavigationPoint[] route)
		{
			if (route == null || route.Length < 2 || limits.MaximumSmoothingChecks == 0)
			{
				return route ?? new ChaseNavigationPoint[0];
			}
			List<ChaseNavigationPoint> list = new List<ChaseNavigationPoint>();
			ChaseNavigationPoint start2 = start;
			int num = 0;
			int num2 = 0;
			while (num < route.Length)
			{
				int num3 = num;
				int num4 = route.Length - 1;
				while (num4 > num && num2 < limits.MaximumSmoothingChecks)
				{
					num2++;
					if (TrySegment(start2, route[num4]))
					{
						num3 = num4;
						break;
					}
					num4--;
				}
				list.Add(route[num3]);
				start2 = route[num3];
				num = num3 + 1;
			}
			return list.ToArray();
		}
	}

	private struct GridOffset
	{
		internal int X { get; private set; }

		internal int Z { get; private set; }

		internal GridOffset(int x, int z)
		{
			X = x;
			Z = z;
		}
	}

	private struct GridKey : IEquatable<GridKey>
	{
		internal int X { get; private set; }

		internal int Z { get; private set; }

		internal GridKey(int x, int z)
		{
			X = x;
			Z = z;
		}

		internal static GridKey FromPoint(ChaseNavigationPoint point, double cellSize)
		{
			return new GridKey((int)Math.Round(point.X / cellSize), (int)Math.Round(point.Z / cellSize));
		}

		public bool Equals(GridKey other)
		{
			return X == other.X && Z == other.Z;
		}

		public override bool Equals(object obj)
		{
			return obj is GridKey && Equals((GridKey)obj);
		}

		public override int GetHashCode()
		{
			return (X * 397) ^ Z;
		}
	}

	private struct SurfaceKey : IEquatable<SurfaceKey>
	{
		internal GridKey Key { get; private set; }

		internal int Layer { get; private set; }

		internal SurfaceKey(GridKey key, int layer)
		{
			Key = key;
			Layer = layer;
		}

		public bool Equals(SurfaceKey other)
		{
			return Key.Equals(other.Key) && Layer == other.Layer;
		}

		public override bool Equals(object obj)
		{
			return obj is SurfaceKey && Equals((SurfaceKey)obj);
		}

		public override int GetHashCode()
		{
			return (Key.GetHashCode() * 397) ^ Layer;
		}
	}

	private struct SurfaceProjection
	{
		internal bool Found { get; private set; }

		internal ChaseNavigationPoint Point { get; private set; }

		internal SurfaceProjection(bool found, ChaseNavigationPoint point)
		{
			Found = found;
			Point = point;
		}
	}

	private struct OpenEntry
	{
		internal GridKey Key { get; private set; }

		internal double Priority { get; private set; }

		internal double Cost { get; private set; }

		internal OpenEntry(GridKey key, double priority, double cost)
		{
			Key = key;
			Priority = priority;
			Cost = cost;
		}
	}

	private sealed class OpenHeap
	{
		private readonly List<OpenEntry> entries = new List<OpenEntry>();

		internal int Count => entries.Count;

		internal void Push(OpenEntry entry)
		{
			entries.Add(entry);
			int num = entries.Count - 1;
			while (num > 0)
			{
				int num2 = (num - 1) / 2;
				if (Compare(entries[num2], entry) <= 0)
				{
					break;
				}
				entries[num] = entries[num2];
				num = num2;
			}
			entries[num] = entry;
		}

		internal OpenEntry Pop()
		{
			OpenEntry result = entries[0];
			int num = entries.Count - 1;
			OpenEntry openEntry = entries[num];
			entries.RemoveAt(num);
			if (num == 0)
			{
				return result;
			}
			int num2 = 0;
			while (true)
			{
				int num3 = num2 * 2 + 1;
				if (num3 >= entries.Count)
				{
					break;
				}
				int num4 = num3 + 1;
				int num5 = ((num4 < entries.Count && Compare(entries[num4], entries[num3]) < 0) ? num4 : num3);
				if (Compare(openEntry, entries[num5]) <= 0)
				{
					break;
				}
				entries[num2] = entries[num5];
				num2 = num5;
			}
			entries[num2] = openEntry;
			return result;
		}

		private static int Compare(OpenEntry left, OpenEntry right)
		{
			int num = left.Priority.CompareTo(right.Priority);
			if (num != 0)
			{
				return num;
			}
			num = left.Cost.CompareTo(right.Cost);
			if (num != 0)
			{
				return num;
			}
			num = left.Key.X.CompareTo(right.Key.X);
			return (num != 0) ? num : left.Key.Z.CompareTo(right.Key.Z);
		}
	}

	private static readonly GridOffset[] NeighborOffsets = new GridOffset[8]
	{
		new GridOffset(1, 0),
		new GridOffset(0, 1),
		new GridOffset(-1, 0),
		new GridOffset(0, -1),
		new GridOffset(1, 1),
		new GridOffset(-1, 1),
		new GridOffset(-1, -1),
		new GridOffset(1, -1)
	};

	internal ChaseRoutePlan Plan(IPlayfieldChaseNavigationProvider provider, ChaseNavigationPoint start, ChaseNavigationPoint goal, ChaseRouteSearchLimits limits)
	{
		if (provider == null || limits == null || !start.IsFinite || !goal.IsFinite)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.InvalidRequest, (provider == null) ? string.Empty : provider.GeometryVersion, 0, 0);
		}
		if (provider.Capability == ChaseNavigationCapability.Unsupported)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.Unsupported, provider.GeometryVersion, 0, 0);
		}
		if (provider.Capability != ChaseNavigationCapability.Supported)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.Unavailable, provider.GeometryVersion, 0, 0);
		}
		if (start.Distance2D(goal) > limits.MaximumStartGoalDistance)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.SearchLimitReached, provider.GeometryVersion, 0, 0);
		}
		PlannerContext plannerContext = new PlannerContext(provider, limits, start, goal);
		if (plannerContext.TrySegment(start, goal))
		{
			return ChaseRoutePlan.Success(new ChaseNavigationPoint[1] { goal }, provider.GeometryVersion, 0, plannerContext.SegmentChecks);
		}
		GridKey gridKey = GridKey.FromPoint(start, limits.CellSize);
		OpenHeap openHeap = new OpenHeap();
		HashSet<GridKey> hashSet = new HashSet<GridKey>();
		Dictionary<GridKey, double> dictionary = new Dictionary<GridKey, double>();
		Dictionary<GridKey, GridKey> dictionary2 = new Dictionary<GridKey, GridKey>();
		Dictionary<GridKey, ChaseNavigationPoint> dictionary3 = new Dictionary<GridKey, ChaseNavigationPoint>();
		dictionary[gridKey] = 0.0;
		dictionary3[gridKey] = start;
		openHeap.Push(new OpenEntry(gridKey, start.Distance2D(goal), 0.0));
		while (openHeap.Count > 0 && plannerContext.ExpandedNodes < limits.MaximumExpandedNodes && plannerContext.SegmentChecks < limits.MaximumSegmentChecks)
		{
			OpenEntry openEntry = openHeap.Pop();
			if (hashSet.Contains(openEntry.Key) || !dictionary.TryGetValue(openEntry.Key, out var value) || openEntry.Cost > value + 1E-08)
			{
				continue;
			}
			hashSet.Add(openEntry.Key);
			plannerContext.ExpandedNodes++;
			ChaseNavigationPoint chaseNavigationPoint = dictionary3[openEntry.Key];
			if (chaseNavigationPoint.Distance2D(goal) <= limits.GoalConnectionDistance && plannerContext.TrySegment(chaseNavigationPoint, goal))
			{
				ChaseNavigationPoint[] route = Reconstruct(gridKey, openEntry.Key, goal, dictionary2, dictionary3);
				ChaseNavigationPoint[] points = plannerContext.Smooth(start, route);
				return ChaseRoutePlan.Success(points, provider.GeometryVersion, plannerContext.ExpandedNodes, plannerContext.SegmentChecks);
			}
			for (int i = 0; i < NeighborOffsets.Length; i++)
			{
				GridOffset gridOffset = NeighborOffsets[i];
				GridKey gridKey2 = new GridKey(openEntry.Key.X + gridOffset.X, openEntry.Key.Z + gridOffset.Z);
				if (!hashSet.Contains(gridKey2) && plannerContext.IsWithinBounds(gridKey2) && plannerContext.TryProject(gridKey2, chaseNavigationPoint, out var projected) && !(Math.Abs(projected.Y - chaseNavigationPoint.Y) > limits.MaximumVerticalStep) && plannerContext.TrySegment(chaseNavigationPoint, projected))
				{
					double num = value + Distance3D(chaseNavigationPoint, projected);
					if (!dictionary.TryGetValue(gridKey2, out var value2) || !(num >= value2 - 1E-08))
					{
						dictionary[gridKey2] = num;
						dictionary2[gridKey2] = openEntry.Key;
						dictionary3[gridKey2] = projected;
						double num2 = projected.Distance2D(goal);
						openHeap.Push(new OpenEntry(gridKey2, num + num2, num));
					}
				}
			}
		}
		ChaseRoutePlanStatus status = ((plannerContext.ExpandedNodes >= limits.MaximumExpandedNodes || plannerContext.SegmentChecks >= limits.MaximumSegmentChecks) ? ChaseRoutePlanStatus.SearchLimitReached : ChaseRoutePlanStatus.Unreachable);
		return ChaseRoutePlan.Failed(status, provider.GeometryVersion, plannerContext.ExpandedNodes, plannerContext.SegmentChecks);
	}

	private static ChaseNavigationPoint[] Reconstruct(GridKey startKey, GridKey endKey, ChaseNavigationPoint goal, IDictionary<GridKey, GridKey> parents, IDictionary<GridKey, ChaseNavigationPoint> points)
	{
		List<ChaseNavigationPoint> list = new List<ChaseNavigationPoint> { goal };
		GridKey key = endKey;
		while (!key.Equals(startKey))
		{
			list.Add(points[key]);
			if (!parents.TryGetValue(key, out var value))
			{
				break;
			}
			key = value;
		}
		list.Reverse();
		return list.ToArray();
	}

	private static double Distance3D(ChaseNavigationPoint left, ChaseNavigationPoint right)
	{
		double num = left.X - right.X;
		double num2 = left.Y - right.Y;
		double num3 = left.Z - right.Z;
		return Math.Sqrt(num * num + num2 * num2 + num3 * num3);
	}
}
