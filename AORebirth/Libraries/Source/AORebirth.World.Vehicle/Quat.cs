using System;

namespace LostEden.Vehicles
{
    /// <summary>
    /// Stock <c>Quaternion_t</c>, stored x, y, z, w to match the <c>Vehicle_t</c> layout at
    /// <c>+0x80..+0x8c</c> (the constructor at <c>Vehicle.dll 1000ce2f</c> writes 0, 0, 0, 1).
    /// Unity-free; the binding layer converts to <c>UnityEngine.Quaternion</c>, which uses the same
    /// component order and the same left-handed convention, so the conversion is a field copy.
    /// </summary>
    public struct Quat : IEquatable<Quat>
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        /// <summary>Rotation of <paramref name="radians"/> about a <b>unit</b> <paramref name="axis"/>.</summary>
        public static Quat FromAxisAngle(Vec3 axis, float radians)
        {
            float half = radians * 0.5f;
            float s = (float)Math.Sin(half);
            return new Quat(axis.X * s, axis.Y * s, axis.Z * s, (float)Math.Cos(half));
        }

        /// <summary>Hamilton product: <paramref name="a"/> applied after <paramref name="b"/>.</summary>
        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        /// <summary>Rotate a vector by this quaternion.</summary>
        public static Vec3 operator *(Quat q, Vec3 v)
        {
            var u = new Vec3(q.X, q.Y, q.Z);
            float s = q.W;
            return u * (2f * Vec3.Dot(u, v))
                 + v * (s * s - Vec3.Dot(u, u))
                 + Vec3.Cross(u, v) * (2f * s);
        }

        /// <summary>
        /// <c>FUN_10005c95</c> — the inverse rotation for a unit quaternion, used by
        /// <c>UpdateHeadingToPos</c> to take a world direction into the target's local frame.
        /// </summary>
        /// <summary>
        /// <c>FUN_1000f1f2</c> — the body orientation stock builds from a forward and an up vector.
        /// Used by the orientation update (<c>FUN_1000c616</c>) in modes 1, 2 and 3.
        ///
        /// <para>
        /// Two details are stock's and both matter:
        /// </para>
        /// <list type="bullet">
        ///   <item><b>An up vector with <c>Y &lt;= 0</c> is replaced by world up</b>
        ///     (<c>1000f1fe</c>), so a body is never turned upside down by a bad normal.</item>
        ///   <item>Forward is projected onto the plane by adjusting <b>only its Y</b>:
        ///     <c>forward.y -= dot(forward, up) / up.y</c> (<c>1000f241</c>). That is algebraically a
        ///     valid projection — the result is perpendicular to <c>up</c> — and it is what tilts a
        ///     walker's forward along a slope, which is how the swept solver comes to see an upward
        ///     move at all.</item>
        /// </list>
        ///
        /// <para>
        /// The quaternion construction from the resulting basis is the standard one; stock's
        /// remaining arithmetic (<c>1000f045</c> onward) was not read instruction by instruction.
        /// </para>
        /// </summary>
        public static Quat LookRotation(Vec3 forward, Vec3 up)
        {
            if (up.Y <= 0f)
                up = Vec3.ReferenceUp;

            float dot = Vec3.Dot(forward, up);
            forward.Y -= dot / up.Y;

            float forwardLength = forward.Length;
            if (forwardLength <= 1e-6f)
                return Identity;
            forward = forward * (1f / forwardLength);

            float upLength = up.Length;
            if (upLength <= 1e-6f)
                return Identity;
            up = up * (1f / upLength);

            Vec3 right = Vec3.Cross(up, forward);
            float rightLength = right.Length;
            if (rightLength <= 1e-6f)
                return Identity;
            right = right * (1f / rightLength);

            // re-derive up so the basis is exactly orthonormal
            up = Vec3.Cross(forward, right);

            // standard orthonormal basis -> quaternion
            float trace = right.X + up.Y + forward.Z;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                return new Quat(
                    (up.Z - forward.Y) / s,
                    (forward.X - right.Z) / s,
                    (right.Y - up.X) / s,
                    s * 0.25f);
            }

            if (right.X > up.Y && right.X > forward.Z)
            {
                float s = (float)Math.Sqrt(1f + right.X - up.Y - forward.Z) * 2f;
                return new Quat(
                    s * 0.25f,
                    (up.X + right.Y) / s,
                    (forward.X + right.Z) / s,
                    (up.Z - forward.Y) / s);
            }

            if (up.Y > forward.Z)
            {
                float s = (float)Math.Sqrt(1f + up.Y - right.X - forward.Z) * 2f;
                return new Quat(
                    (up.X + right.Y) / s,
                    s * 0.25f,
                    (forward.Y + up.Z) / s,
                    (forward.X - right.Z) / s);
            }

            {
                float s = (float)Math.Sqrt(1f + forward.Z - right.X - up.Y) * 2f;
                return new Quat(
                    (forward.X + right.Z) / s,
                    (forward.Y + up.Z) / s,
                    s * 0.25f,
                    (right.Y - up.X) / s);
            }
        }

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

        public Quat Normalized
        {
            get
            {
                float len = Length;
                if (len == 0f)
                    return Identity;
                return new Quat(X / len, Y / len, Z / len, W / len);
            }
        }

        public bool Equals(Quat other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;

        public override bool Equals(object obj) => obj is Quat q && Equals(q);

        public override int GetHashCode() => (X, Y, Z, W).GetHashCode();

        public override string ToString() => $"({X}, {Y}, {Z}, {W})";
    }
}
