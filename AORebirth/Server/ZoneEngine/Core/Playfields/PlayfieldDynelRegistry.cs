namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;

    #endregion

    internal sealed class PlayfieldDynelRegistry
    {
        private readonly object sync = new object();

        private readonly Identity playfieldIdentity;

        private readonly Dictionary<ulong, IEntity> entities = new Dictionary<ulong, IEntity>();

        private readonly Dictionary<ulong, IInstancedEntity> instancedEntities =
            new Dictionary<ulong, IInstancedEntity>();

        private readonly Dictionary<ulong, IDynel> dynels = new Dictionary<ulong, IDynel>();

        private readonly Dictionary<ulong, ICharacter> characters = new Dictionary<ulong, ICharacter>();

        private readonly Dictionary<ulong, ICharacter> players = new Dictionary<ulong, ICharacter>();

        private readonly Dictionary<ulong, ICharacter> npcs = new Dictionary<ulong, ICharacter>();

        private readonly Dictionary<ulong, Vendor> vendors = new Dictionary<ulong, Vendor>();

        private readonly Dictionary<ulong, StaticDynel> staticDynels = new Dictionary<ulong, StaticDynel>();

        private readonly Dictionary<ulong, StatelData> statels = new Dictionary<ulong, StatelData>();

        private readonly Dictionary<ulong, StatelData> terminals = new Dictionary<ulong, StatelData>();

        private readonly Dictionary<ulong, StatelData> doors = new Dictionary<ulong, StatelData>();

        private readonly Dictionary<ulong, Identity> corpseIdentities = new Dictionary<ulong, Identity>();

        internal PlayfieldDynelRegistry(Identity playfieldIdentity)
        {
            this.playfieldIdentity = playfieldIdentity;
        }

        internal void RefreshFromPool()
        {
            lock (this.sync)
            {
                this.ClearPooledViews();
                foreach (IEntity entity in Pool.Instance.GetAll<IEntity>(this.playfieldIdentity))
                {
                    this.RegisterUnlocked(entity);
                }
            }
        }

        internal void Register(IEntity entity)
        {
            lock (this.sync)
            {
                this.RegisterUnlocked(entity);
            }
        }

        internal void Unregister(Identity identity)
        {
            ulong key = identity.Long();
            lock (this.sync)
            {
                this.entities.Remove(key);
                this.instancedEntities.Remove(key);
                this.dynels.Remove(key);
                this.characters.Remove(key);
                this.players.Remove(key);
                this.npcs.Remove(key);
                this.vendors.Remove(key);
                this.staticDynels.Remove(key);
                this.corpseIdentities.Remove(key);
            }
        }

        internal void RegisterStatels(IEnumerable<StatelData> playfieldStatels)
        {
            lock (this.sync)
            {
                this.statels.Clear();
                this.terminals.Clear();
                this.doors.Clear();

                if (playfieldStatels == null)
                {
                    return;
                }

                foreach (StatelData statel in playfieldStatels)
                {
                    if (statel == null)
                    {
                        continue;
                    }

                    ulong key = statel.Identity.Long();
                    this.statels[key] = statel;

                    if (IsTerminal(statel.Identity.Type))
                    {
                        this.terminals[key] = statel;
                    }

                    if (statel.Identity.Type == IdentityType.Door)
                    {
                        this.doors[key] = statel;
                    }
                }
            }
        }

        internal void RegisterCorpse(Identity corpseIdentity)
        {
            lock (this.sync)
            {
                this.corpseIdentities[corpseIdentity.Long()] = corpseIdentity;
            }
        }

        internal IInstancedEntity FindByIdentity(Identity identity)
        {
            IInstancedEntity entity;
            if (this.TryGetRegistered(identity, out entity))
            {
                return entity;
            }

            entity = Pool.Instance.GetObject<IInstancedEntity>(identity);
            if (entity != null)
            {
                this.Register(entity);
            }

            return entity;
        }

        internal T FindByIdentity<T>(Identity identity) where T : class, IEntity
        {
            T entity;
            if (this.TryGetRegistered(identity, out entity))
            {
                return entity;
            }

            entity = Pool.Instance.GetObject<T>(identity);
            if (entity != null)
            {
                this.Register(entity);
            }

            return entity;
        }

        internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)
        {
            this.RefreshFromPool();

            var result = new List<IDynel>();
            if (dynel == null)
            {
                return result.AsReadOnly();
            }

            Coordinate coord = dynel.Coordinates();
            foreach (IDynel entity in this.DynelsSnapshot())
            {
                if (entity == dynel || entity.Identity.Type != IdentityType.CanbeAffected)
                {
                    continue;
                }

                if (entity.Coordinates().Distance2D(coord) <= range)
                {
                    result.Add(entity);
                }
            }

            return result.AsReadOnly();
        }

        internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)
        {
            this.RefreshFromPool();

            var result = new List<ICharacter>();
            if (dynel == null)
            {
                return result.AsReadOnly();
            }

            Coordinate coord = dynel.Coordinates();
            foreach (ICharacter entity in this.Characters())
            {
                if (entity == dynel || entity.Identity.Type != IdentityType.CanbeAffected)
                {
                    continue;
                }

                if (entity.Coordinates().Distance2D(coord) <= range)
                {
                    result.Add(entity);
                }
            }

            return result.AsReadOnly();
        }

        internal ReadOnlyCollection<ICharacter> Characters()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.characters.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<Character> CharacterEntities()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.characters.Values.OfType<Character>().ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<ICharacter> Players()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.players.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<ICharacter> Npcs()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.npcs.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<Vendor> Vendors()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.vendors.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<StaticDynel> StaticDynels()
        {
            this.RefreshFromPool();
            lock (this.sync)
            {
                return this.staticDynels.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<StatelData> Statels()
        {
            lock (this.sync)
            {
                return this.statels.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<StatelData> Terminals()
        {
            lock (this.sync)
            {
                return this.terminals.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<StatelData> Doors()
        {
            lock (this.sync)
            {
                return this.doors.Values.ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<Identity> CorpseIdentities()
        {
            lock (this.sync)
            {
                return this.corpseIdentities.Values.ToList().AsReadOnly();
            }
        }

        private void RegisterUnlocked(IEntity entity)
        {
            if (entity == null || entity.Parent != this.playfieldIdentity)
            {
                return;
            }

            ulong key = entity.Identity.Long();
            this.entities[key] = entity;

            var instancedEntity = entity as IInstancedEntity;
            if (instancedEntity != null)
            {
                this.instancedEntities[key] = instancedEntity;
            }

            var dynel = entity as IDynel;
            if (dynel != null)
            {
                this.dynels[key] = dynel;
            }

            var character = entity as ICharacter;
            if (character != null)
            {
                this.characters[key] = character;
                if (character.Controller is NPCController)
                {
                    this.npcs[key] = character;
                }
                else
                {
                    this.players[key] = character;
                }
            }

            var vendor = entity as Vendor;
            if (vendor != null)
            {
                this.vendors[key] = vendor;
            }

            var staticDynel = entity as StaticDynel;
            if (staticDynel != null)
            {
                this.staticDynels[key] = staticDynel;
            }
        }

        private bool TryGetRegistered<T>(Identity identity, out T entity) where T : class, IEntity
        {
            lock (this.sync)
            {
                IEntity registered;
                if (this.entities.TryGetValue(identity.Long(), out registered))
                {
                    entity = registered as T;
                    return entity != null;
                }
            }

            entity = null;
            return false;
        }

        private IEnumerable<IDynel> DynelsSnapshot()
        {
            lock (this.sync)
            {
                return this.dynels.Values.ToList();
            }
        }

        private void ClearPooledViews()
        {
            this.entities.Clear();
            this.instancedEntities.Clear();
            this.dynels.Clear();
            this.characters.Clear();
            this.players.Clear();
            this.npcs.Clear();
            this.vendors.Clear();
            this.staticDynels.Clear();
        }

        private static bool IsTerminal(IdentityType identityType)
        {
            return identityType == IdentityType.Terminal
                   || identityType == IdentityType.MailTerminal
                   || identityType == IdentityType.MissionTerminal
                   || identityType == IdentityType.VendingMachine;
        }

    }
}
