namespace AORebirth.World.Collision
{
    /// <summary>Indexed triangle into a <see cref="CollisionTriangleMesh.Vertices"/> array.</summary>
    public readonly struct CollisionTriangle
    {
        public CollisionTriangle(int a, int b, int c)
        {
            A = a;
            B = b;
            C = c;
        }

        public int A { get; }

        public int B { get; }

        public int C { get; }
    }
}
