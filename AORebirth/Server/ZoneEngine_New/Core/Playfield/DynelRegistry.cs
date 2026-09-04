namespace ZoneEngine_New.Core.Playfield
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class DynelRegistry
    {
        private const int FirstNpcInstance = 1_000_000;
        private const int FirstCorpseInstance = 2_000_000;

        private readonly Lock _sync = new();
        private readonly Dictionary<ulong, Dynel> _dynels = new();
        private int _nextNpcInstance = FirstNpcInstance - 1;
        private int _nextCorpseInstance = FirstCorpseInstance - 1;

        public Identity AllocateNpcIdentity()
        {
            int instance = Interlocked.Increment(ref _nextNpcInstance);
            return new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = instance
            };
        }

        public Identity AllocateCorpseIdentity()
        {
            int instance = Interlocked.Increment(ref _nextCorpseInstance);
            return new Identity
            {
                Type = IdentityType.Corpse,
                Instance = instance
            };
        }

        public void Register(Dynel? dynel)
        {
            if (dynel == null)
            {
                return;
            }

            lock (_sync)
            {
                _dynels[dynel.Identity.Long()] = dynel;
            }
        }

        public void Unregister(Identity identity)
        {
            lock (_sync)
            {
                _dynels.Remove(identity.Long());
            }
        }

        public bool TryGet(Identity identity, out Dynel? dynel)
        {
            lock (_sync)
            {
                return _dynels.TryGetValue(identity.Long(), out dynel);
            }
        }

        public IEnumerable<Dynel> Dynels()
        {
            lock (_sync)
            {
                return _dynels.Values.ToList();
            }
        }

        public IEnumerable<Dynel> Players()
        {
            lock (_sync)
            {
                return _dynels.Values.Where(d => d.IsPlayer).ToList();
            }
        }

        public IEnumerable<Player> PlayerEntities()
        {
            lock (_sync)
            {
                return _dynels.Values.OfType<Player>().ToList();
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _dynels.Clear();
            }
        }
    }
}
