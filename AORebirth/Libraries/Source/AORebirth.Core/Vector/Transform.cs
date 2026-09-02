namespace AORebirth.Core.Vector
{
    /// <summary>
    /// World pose for a dynel: position, rotation, and a per-tick position dirty flag.
    /// </summary>
    public sealed class Transform
    {
        private Vector3 position;

        private Quaternion rotation;

        public Transform()
        {
            this.position = new Vector3(0, 0, 0);
            this.rotation = new Quaternion();
        }

        public Transform(Vector3 position, Quaternion rotation)
        {
            this.position = position ?? new Vector3(0, 0, 0);
            this.rotation = rotation ?? new Quaternion();
        }

        /// <summary>
        /// Stored world position. Setting a different value marks <see cref="PositionChangedSinceLastTick"/>.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                return this.position;
            }

            set
            {
                Vector3 next = value ?? new Vector3(0, 0, 0);
                if (object.ReferenceEquals(this.position, null) || !this.position.Equals(next))
                {
                    this.PositionChangedSinceLastTick = true;
                }

                this.position = next;
            }
        }

        /// <summary>
        /// Stored world rotation (facing).
        /// </summary>
        public Quaternion Rotation
        {
            get
            {
                return this.rotation;
            }

            set
            {
                this.rotation = value ?? new Quaternion();
            }
        }

        /// <summary>
        /// True after <see cref="Position"/> changed since the last <see cref="AcknowledgePositionChange"/>.
        /// </summary>
        public bool PositionChangedSinceLastTick { get; private set; }

        /// <summary>
        /// Clears <see cref="PositionChangedSinceLastTick"/> after locality (or other consumers) process the move.
        /// </summary>
        public void AcknowledgePositionChange()
        {
            this.PositionChangedSinceLastTick = false;
        }
    }
}
