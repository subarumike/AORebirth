namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.World.Contracts;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class DynelRegistry
    {
        private const int FirstCorpseInstance = 2_000_000;
        private const int FirstVendingMachineInstance = 3_000_000;
        private const int FirstTempBagInstance = 4_000_000;
        private const int FirstStaticDynelInstance = 5_000_000;

        private readonly Lock _sync = new();
        private readonly Dictionary<ulong, Dynel> _dynels = new();
        /// <summary>
        /// Engine-wide, unlike the other counters: an NPC can change playfield alive (a pet zoning with its owner)
        /// and keeps its identity, so no two NPCs on any playfield may ever share one.
        /// </summary>
        private static int _nextNpcInstance = NpcInstanceRange.FirstStandaloneInstance - 1;
        private int _nextCorpseInstance = FirstCorpseInstance - 1;
        private int _nextVendingMachineInstance = FirstVendingMachineInstance - 1;
        private int _nextTempBagInstance = FirstTempBagInstance - 1;
        private int _nextStaticDynelInstance = FirstStaticDynelInstance - 1;

        /// <summary>Last NPC instance id this engine may hand out; another zone instance owns the next block.</summary>
        private static int _lastNpcInstance = int.MaxValue;
        private static int _npcRangeConfigured;
        private static int _firstNpcInstance = NpcInstanceRange.FirstStandaloneInstance;

        /// <summary>
        /// The block of NPC instance ids World gave this zone instance (its slot), so NPCs and pets on different
        /// instances never share an identity. Set once, before the first playfield builds.
        /// </summary>
        public static bool ConfigureNpcRange(int first, int count)
        {
            if (first <= 0 || count <= 0) throw new ArgumentOutOfRangeException(nameof(first));
            // Already set: true only when World gave the same block again.
            if (Interlocked.CompareExchange(ref _npcRangeConfigured, 1, 0) != 0)
                return first == Volatile.Read(ref _firstNpcInstance);
            Volatile.Write(ref _firstNpcInstance, first);
            Volatile.Write(ref _lastNpcInstance, (int)Math.Min(int.MaxValue, first + (long)count - 1));
            Volatile.Write(ref _nextNpcInstance, first - 1);
            return true;
        }

        public Identity AllocateNpcIdentity()
        {
            int instance = Interlocked.Increment(ref _nextNpcInstance);
            if (instance > Volatile.Read(ref _lastNpcInstance))
                throw new InvalidOperationException("This zone instance has used up its NPC identity range.");
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

        /// <summary>A snapshot of the players into the caller's buffer (cleared first), for per-tick passes.</summary>
        public void CopyPlayers(List<Player> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            buffer.Clear();
            lock (_sync)
            {
                foreach (Dynel dynel in _dynels.Values)
                {
                    if (dynel is Player player)
                        buffer.Add(player);
                }
            }
        }

        /// <summary>Whether any player is registered, without copying the list.</summary>
        public bool HasPlayers()
        {
            lock (_sync)
            {
                foreach (Dynel dynel in _dynels.Values)
                {
                    if (dynel is Player)
                        return true;
                }
            }

            return false;
        }

        /// <summary>A snapshot of every dynel into the caller's buffer (cleared first), for per-tick passes.</summary>
        public void CopyDynels(List<Dynel> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            buffer.Clear();
            lock (_sync)
            {
                buffer.AddRange(_dynels.Values);
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
