using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class UniformSpatialIndex<TValue> where TValue : class
{
	private sealed class IndexedValue
	{
		internal Identity Identity { get; private set; }

		internal float X { get; set; }

		internal float Z { get; set; }

		internal CellKey Cell { get; set; }

		internal TValue Value { get; private set; }

		internal IndexedValue(Identity identity, float x, float z, CellKey cell, TValue value)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			Identity = identity;
			X = x;
			Z = z;
			Cell = cell;
			Value = value;
		}
	}

	private sealed class QueryMatch
	{
		internal IndexedValue Indexed { get; private set; }

		internal double DistanceSquared { get; private set; }

		internal QueryMatch(IndexedValue indexed, double distanceSquared)
		{
			Indexed = indexed;
			DistanceSquared = distanceSquared;
		}
	}

	private struct CellKey
	{
		private int X { get; set; }

		private int Z { get; set; }

		internal CellKey(int x, int z)
		{
			X = x;
			Z = z;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is CellKey cellKey))
			{
				return false;
			}
			return X == cellKey.X && Z == cellKey.Z;
		}

		public override int GetHashCode()
		{
			int num = 17;
			num = num * 23 + X;
			return num * 23 + Z;
		}
	}

	private readonly object sync = new object();

	private readonly float cellSize;

	private readonly Dictionary<Identity, IndexedValue> values = new Dictionary<Identity, IndexedValue>();

	private readonly Dictionary<CellKey, Dictionary<Identity, IndexedValue>> cells = new Dictionary<CellKey, Dictionary<Identity, IndexedValue>>();

	private int lastCandidateInspectionCount;

	internal int Count
	{
		get
		{
			lock (sync)
			{
				return values.Count;
			}
		}
	}

	internal int LastCandidateInspectionCount
	{
		get
		{
			lock (sync)
			{
				return lastCandidateInspectionCount;
			}
		}
	}

	internal UniformSpatialIndex(float cellSize)
	{
		if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize < 8f || cellSize > 128f)
		{
			throw new ArgumentOutOfRangeException("cellSize");
		}
		this.cellSize = cellSize;
	}

	internal void Upsert(Identity identity, VisibilityPosition position, TValue value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		ValidateIdentity(identity);
		ValidatePosition(position, "position");
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		CellKey cellKey = CellFor(position.X, position.Z);
		lock (sync)
		{
			if (values.TryGetValue(identity, out var value2))
			{
				if (value2.Value != value)
				{
					Identity val = identity;
					throw new InvalidOperationException("Spatial visibility identity is already indexed by another character: " + ((object)(Identity)(ref val)).ToString());
				}
				if (!value2.Cell.Equals(cellKey))
				{
					RemoveFromCell(value2);
					value2.Cell = cellKey;
					AddToCell(value2);
				}
				value2.X = position.X;
				value2.Z = position.Z;
			}
			else
			{
				IndexedValue indexedValue = new IndexedValue(identity, position.X, position.Z, cellKey, value);
				values.Add(identity, indexedValue);
				AddToCell(indexedValue);
			}
		}
	}

	internal bool Remove(Identity identity)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			if (!values.TryGetValue(identity, out var value))
			{
				return false;
			}
			RemoveFromCell(value);
			values.Remove(identity);
			return true;
		}
	}

	internal IReadOnlyList<TValue> Query(VisibilityPosition center, float radius)
	{
		ValidatePosition(center, "center");
		if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f || radius > 384f)
		{
			throw new ArgumentOutOfRangeException("radius");
		}
		int num = CellCoordinate((double)center.X - (double)radius);
		int num2 = CellCoordinate((double)center.X + (double)radius);
		int num3 = CellCoordinate((double)center.Z - (double)radius);
		int num4 = CellCoordinate((double)center.Z + (double)radius);
		double num5 = (double)radius * (double)radius;
		List<QueryMatch> list = new List<QueryMatch>();
		int num6 = 0;
		lock (sync)
		{
			for (long num7 = num; num7 <= num2; num7++)
			{
				for (long num8 = num3; num8 <= num4; num8++)
				{
					if (!cells.TryGetValue(new CellKey((int)num7, (int)num8), out var value2))
					{
						continue;
					}
					num6 += value2.Count;
					foreach (IndexedValue value3 in value2.Values)
					{
						double num9 = DistanceSquared(center, value3);
						if (num9 <= num5)
						{
							list.Add(new QueryMatch(value3, num9));
						}
					}
				}
			}
			lastCandidateInspectionCount = num6;
		}
		return (from value in list.OrderBy((QueryMatch value) => value.DistanceSquared).ThenBy(delegate(QueryMatch value)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Expected I4, but got Unknown
				Identity identity2 = value.Indexed.Identity;
				return (int)((Identity)(ref identity2)).Type;
			}).ThenBy(delegate(QueryMatch value)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				Identity identity = value.Indexed.Identity;
				return ((Identity)(ref identity)).Instance;
			})
			select value.Indexed.Value).ToArray();
	}

	internal void Clear()
	{
		lock (sync)
		{
			values.Clear();
			cells.Clear();
			lastCandidateInspectionCount = 0;
		}
	}

	private static void ValidateIdentity(Identity identity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		if (identity == Identity.None || ((Identity)(ref identity)).Instance <= 0)
		{
			throw new ArgumentException("Spatial visibility identity is required.", "identity");
		}
	}

	private static void ValidatePosition(VisibilityPosition position, string name)
	{
		if (float.IsNaN(position.X) || float.IsInfinity(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.Y) || float.IsNaN(position.Z) || float.IsInfinity(position.Z))
		{
			throw new ArgumentOutOfRangeException(name);
		}
	}

	private static double DistanceSquared(VisibilityPosition center, IndexedValue candidate)
	{
		double num = center.X - candidate.X;
		double num2 = center.Z - candidate.Z;
		return num * num + num2 * num2;
	}

	private CellKey CellFor(float x, float z)
	{
		return new CellKey(CellCoordinate(x), CellCoordinate(z));
	}

	private int CellCoordinate(double value)
	{
		double num = Math.Floor(value / (double)cellSize);
		if (num < -2147483648.0 || num > 2147483647.0)
		{
			throw new ArgumentOutOfRangeException("position");
		}
		return (int)num;
	}

	private void AddToCell(IndexedValue indexed)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (!cells.TryGetValue(indexed.Cell, out var value))
		{
			value = new Dictionary<Identity, IndexedValue>();
			cells.Add(indexed.Cell, value);
		}
		value.Add(indexed.Identity, indexed);
	}

	private void RemoveFromCell(IndexedValue indexed)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (!cells.TryGetValue(indexed.Cell, out var value))
		{
			throw new InvalidOperationException("Spatial visibility cell membership is corrupt.");
		}
		value.Remove(indexed.Identity);
		if (value.Count == 0)
		{
			cells.Remove(indexed.Cell);
		}
	}
}
