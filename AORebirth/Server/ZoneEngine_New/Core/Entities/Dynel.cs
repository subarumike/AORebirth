namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

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
        Corpse = 4,
        StaticDynel = 5,
        ContentPlacement = 6,
        Summoned = 7
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

        /// <summary>
        /// Body radius of a character without CharRadius (no MonsterData record, players): the client's initial
        /// radius, 1.0, not scaled.
        /// </summary>
        public const double DefaultCollisionRadius = 1.0;

        public double Distance3D(Dynel other)
        {
            ArgumentNullException.ThrowIfNull(other);
            // Component math: this runs thousands of times a tick, and Vector3 is a class.
            Vector3 a = Position;
            Vector3 b = other.Position;
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        public virtual double GetCollisionRadius() => 0.0;

        public double GetEdgeDistanceTo(Dynel other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return Distance3D(other) - GetCollisionRadius() - other.GetCollisionRadius();
        }

        /// <summary>
        /// True when hard world geometry does not occlude the eye-height ray to <paramref name="other"/>.
        /// Soft zone triggers never block LOS. Missing WorldSimulation → true.
        /// </summary>
        public bool HasLineOfSightTo(Dynel other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(other, this))
                return true;
            if (Playfield == null || other.Playfield == null
                || Playfield.Identity.Instance != other.Playfield.Identity.Instance)
                return false;

            WorldSimulation.PlayfieldWorldSimulation? world = Playfield.WorldAccess.Instance;
            if (world == null)
                return true;

            using Metrics.TickStallWatch.StageScope scope = Metrics.TickStallWatch.Enter("los", Identity.Instance);
            const float eye = Movement.MovementConfig.LineOfSightEyeHeight;
            Vector3 a = Position;
            Vector3 b = other.Position;
            float fx = (float)a.x, fy = (float)a.y + eye, fz = (float)a.z;
            float tx = (float)b.x, ty = (float)b.y + eye, tz = (float)b.z;
            float dx = tx - fx, dy = ty - fy, dz = tz - fz;
            if ((dx * dx) + (dy * dy) + (dz * dz) > 200f * 200f)
                return false;

            return world.HasLineOfSight(fx, fy, fz, tx, ty, tz);
        }

        public virtual MessageBody BuildSpawnMessage()
        {
            throw new NotSupportedException(
                GetType().Name + " does not implement BuildSpawnMessage.");
        }

        /// <summary>Exact accepted wire for a spawn not representable by the shared typed codec.
        /// Ordinary dynels return null and retain the existing typed path.</summary>
        public virtual byte[]? BuildSpawnPacket(Identity receiver) => null;

        /// <summary>
        /// Additional packets sent right after <see cref="BuildSpawnMessage"/> when this dynel enters
        /// a client's visibility. Used for objects that are rendered as part of another dynel, such as
        /// the shop pane attached to a vendor NPC.
        /// </summary>
        public virtual IEnumerable<MessageBody> BuildSpawnCompanionMessages() => [];

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
