using System;

namespace LostEden.Vehicles
{
    /// <summary>
    /// Stock <c>Vector3_t</c>. Unity-free so <c>Tests/Vehicle</c> can assert the recovered maths
    /// without a Unity reference; the binding layer converts to <c>UnityEngine.Vector3</c>.
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        /// <summary><c>Vehicle_t::s_cReferenceForward</c> (<c>Vehicle.dll 10019398</c>).</summary>
        public static readonly Vec3 ReferenceForward = new Vec3(0f, 0f, 1f);

        /// <summary><c>Vehicle_t::s_cReferenceUp</c> (<c>Vehicle.dll 100193a4</c>).</summary>
        public static readonly Vec3 ReferenceUp = new Vec3(0f, 1f, 0f);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => a * s;
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public float LengthSquared => X * X + Y * Y + Z * Z;

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        /// <summary>
        /// <c>FUN_1000f12a</c> — the stock "truncate to length" used by the integrator for both the
        /// force clamp and the velocity clamp. Scales <paramref name="v"/> down <b>only</b> when it is
        /// longer than <paramref name="max"/> (it never lengthens a short vector, so it is not a
        /// normalise), and returns the length the vector ends up with: 0 when it was zero,
        /// its own length when already within <paramref name="max"/>, otherwise <paramref name="max"/>.
        /// </summary>
        public static float Truncate(ref Vec3 v, float max)
        {
            float len = v.Length;
            if (len == 0f)
                return 0f;

            if (max < len)
            {
                float k = max / len;
                v.X *= k;
                v.Y *= k;
                v.Z *= k;
                return max;
            }

            return len;
        }

        public bool IsZero => X == 0f && Y == 0f && Z == 0f;

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);

        public override int GetHashCode() => (X, Y, Z).GetHashCode();

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
