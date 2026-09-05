namespace ZoneEngine_New.Core.Entities
{
    using System;

    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>How a dynel was introduced into the world.</summary>
    public enum SpawnSource
    {
        None = 0,
        HashSpawn = 1,
        Command = 2,
        Player = 3,
        Corpse = 4
    }

    /// <summary>
    /// Skeleton dynel: identity and world transform until full entities land.
    /// </summary>
    public class Dynel : ISpawnable
    {
        public Dynel(Identity identity)
        {
            Identity = identity;
            Transform = new Transform();
            Stats = new StatCollection();
        }

        public Identity Identity { get; }

        public Transform Transform { get; }

        public StatCollection Stats { get; }

        public Playfield? Playfield { get; set; }

        public Cell? Cell { get; internal set; }

        /// <summary>Origin of this dynel in the world (hash spawn, GM command, login, etc.).</summary>
        public SpawnSource SpawnSource { get; set; }

        public virtual bool IsPlayer => false;

        public virtual Vector3 Position
        {
            get => Transform.Position;
            set => Transform.Position = value;
        }

        public virtual Quaternion Rotation
        {
            get => Transform.Rotation;
            set => Transform.Rotation = value == null
                ? new Quaternion()
                : new Quaternion(value.xf, value.yf, value.zf, value.wf);
        }

        public double Distance3D(Dynel other)
        {
            ArgumentNullException.ThrowIfNull(other);
            Vector3 delta = Position - other.Position;
            return Vector3.Abs(delta);
        }

        public virtual MessageBody BuildSpawnMessage()
        {
            throw new NotSupportedException(
                GetType().Name + " does not implement BuildSpawnMessage.");
        }

        public virtual void Tick(double deltaTime)
        {
            FlushDirtyStats();
        }

        public void FlushDirtyStats()
        {
            GameTuple<CharacterStat, uint>[] dirtyStats = Stats.DrainDirty();
            if (dirtyStats.Length == 0)
                return;

            var message = new StatMessage
            {
                Identity = Identity,
                Stats = dirtyStats
            };

            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, message, includeSelf: true);
        }
    }
}
