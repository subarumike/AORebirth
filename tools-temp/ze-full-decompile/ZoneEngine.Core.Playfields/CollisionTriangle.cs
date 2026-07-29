using System;

namespace ZoneEngine.Core.Playfields;

internal struct CollisionTriangle
{
	private const double MinimumAreaSquared = 1E-20;

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

	internal CollisionTriangle(int id, CollisionPoint3 a, CollisionPoint3 b, CollisionPoint3 c)
	{
		if (id < 0)
		{
			throw new ArgumentOutOfRangeException("id");
		}
		if (!a.IsFinite || !b.IsFinite || !c.IsFinite)
		{
			throw new ArgumentOutOfRangeException("triangle", "Triangle coordinates must be finite.");
		}
		double num = b.X - a.X;
		double num2 = b.Y - a.Y;
		double num3 = b.Z - a.Z;
		double num4 = c.X - a.X;
		double num5 = c.Y - a.Y;
		double num6 = c.Z - a.Z;
		double num7 = num2 * num6 - num3 * num5;
		double num8 = num3 * num4 - num * num6;
		double num9 = num * num5 - num2 * num4;
		double num10 = num7 * num7 + num8 * num8 + num9 * num9;
		if (num10 <= 1E-20)
		{
			throw new ArgumentException("Triangle must not be degenerate.", "triangle");
		}
		Id = id;
		A = a;
		B = b;
		C = c;
		MinimumX = Math.Min(a.X, Math.Min(b.X, c.X));
		MinimumY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
		MinimumZ = Math.Min(a.Z, Math.Min(b.Z, c.Z));
		MaximumX = Math.Max(a.X, Math.Max(b.X, c.X));
		MaximumY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
		MaximumZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
	}
}
