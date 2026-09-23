namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class DynelRegistry
    {
        private const int FirstNpcInstance = 1_000_000;
        private const int FirstCorpseInstance = 2_000_000;
        private const int FirstVendingMachineInstance = 3_000_000;
        private const int FirstTempBagInstance = 4_000_000;
        private const int FirstStaticDynelInstance = 5_000_000;

        private readonly Lock _sync = new();
        private readonly Dictionary<ulong, Dynel> _dynels = new();
        private int _nextNpcInstance = FirstNpcInstance - 1;
        private int _nextCorpseInstance = FirstCorpseInstance - 1;
        private int _nextVendingMachineInstance = FirstVendingMachineInstance - 1;
        private int _nextTempBagInstance = FirstTempBagInstance - 1;
        private int _nextStaticDynelInstance = FirstStaticDynelInstance - 1;

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

        /// <summary>Identity for a shop attached to an NPC vendor rather than placed by Dynels.dat.</summary>
        public Identity AllocateVendingMachineIdentity()
        {
            int instance = Interlocked.Increment(ref _nextVendingMachineInstance);
            return new Identity
            {
                Type = IdentityType.VendingMachine,
                Instance = instance
            };
        }

        /// <summary>Identity for a static item dynel spawned from a hash rather than placed by Dynels.dat.</summary>
        public Identity AllocateStaticDynelIdentity()
        {
            int instance = Interlocked.Increment(ref _nextStaticDynelInstance);
            return new Identity
            {
                Type = IdentityType.Terminal,
                Instance = instance
            };
        }

        /// <summary>Identity for a trade window's temporary bag. Not a registered dynel.</summary>
        public Identity AllocateTempBagIdentity()
        {
            int instance = Interlocked.Increment(ref _nextTempBagInstance);
            return new Identity
            {
                Type = IdentityType.TempBag,
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

        /// <summary>Accepted fixed identities fail on collision instead of replacing another actor.</summary>
        public bool TryRegister(Dynel dynel)
        {
            lock (_sync) return _dynels.TryAdd(dynel.Identity.Long(), dynel);
        }

        public bool UnregisterExact(Dynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);
            ulong key = dynel.Identity.Long();
            Character? leaving = CombatantIfRegistered(key, dynel);
            leaving?.LeaveCombat();
            lock (_sync)
                return _dynels.TryGetValue(key, out Dynel? current)
                    && ReferenceEquals(current, dynel)
                    && _dynels.Remove(key);
        }

        public void Unregister(Identity identity)
        {
            ulong key = identity.Long();
            Character? leaving = CombatantIfRegistered(key, expected: null);
            leaving?.LeaveCombat();
            lock (_sync)
                _dynels.Remove(key);
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
            Character[] leaving;
            lock (_sync)
                leaving = _dynels.Values.OfType<Character>().ToArray();

            foreach (Character character in leaving)
                character.LeaveCombat();

            lock (_sync)
                _dynels.Clear();
        }

        /// <summary>
        /// The registered character for <paramref name="key"/>, when it is a
        /// <see cref="Character"/> and, if <paramref name="expected"/> is set, that same instance.
        /// Read outside the lock that removes it so <see cref="Character.LeaveCombat"/> can run first.
        /// </summary>
        Character? CombatantIfRegistered(ulong key, Dynel? expected)
        {
            lock (_sync)
            {
                if (!_dynels.TryGetValue(key, out Dynel? current))
                    return null;
                if (expected != null && !ReferenceEquals(current, expected))
                    return null;
                return current as Character;
            }
        }
    }
}
