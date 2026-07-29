using System;

namespace ZoneEngine.Core.Navigation;

internal struct ChaseNavigationPoint
{
	internal double X { get; private set; }

	internal double Y { get; private set; }

	internal double Z { get; private set; }

	internal bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y) && IsFiniteValue(Z);

	internal ChaseNavigationPoint(double x, double y, double z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	internal double Distance2D(ChaseNavigationPoint other)
	{
		double num = X - other.X;
		double num2 = Z - other.Z;
		return Math.Sqrt(num * num + num2 * num2);
	}

	internal double DistanceSquared2D(ChaseNavigationPoint other)
	{
		double num = X - other.X;
		double num2 = Z - other.Z;
		return num * num + num2 * num2;
	}

	private static bool IsFiniteValue(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
