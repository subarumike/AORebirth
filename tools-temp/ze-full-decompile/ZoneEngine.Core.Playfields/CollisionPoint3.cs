namespace ZoneEngine.Core.Playfields;

internal struct CollisionPoint3
{
	internal double X { get; private set; }

	internal double Y { get; private set; }

	internal double Z { get; private set; }

	internal bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y) && IsFiniteValue(Z);

	internal CollisionPoint3(double x, double y, double z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	internal double DistanceSquared(CollisionPoint3 other)
	{
		double num = X - other.X;
		double num2 = Y - other.Y;
		double num3 = Z - other.Z;
		return num * num + num2 * num2 + num3 * num3;
	}

	private static bool IsFiniteValue(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
