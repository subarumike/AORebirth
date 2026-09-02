namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldDynelCellRegistry
    {
        private const int NonLocalCellId = -1;

        private readonly object sync = new object();
        private readonly IPlayfieldCellLayout layout;
        private readonly Dictionary<ulong, int> cellByIdentity = new Dictionary<ulong, int>();
        private readonly Dictionary<int, HashSet<ulong>> identitiesByCell = new Dictionary<int, HashSet<ulong>>();
        private readonly Dictionary<ulong, ICharacter> charactersByIdentity = new Dictionary<ulong, ICharacter>();

        internal PlayfieldDynelCellRegistry(IPlayfieldCellLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException("layout");
        }

        internal bool UsesOutdoorGrid
        {
            get { return !this.layout.IsIndoor; }
        }

        internal void Register(IDynel dynel)
        {
            if (dynel == null)
            {
                return;
            }

            lock (this.sync)
            {
                ICharacter character = dynel as ICharacter;
                if (character != null)
                {
                    this.charactersByIdentity[character.Identity.Long()] = character;
                }

                this.AssignCellUnlocked(dynel.Identity, new AORebirth.Core.Vector.Coordinate(dynel.Position));
            }
        }

        internal void Register(StaticDynel staticDynel)
        {
            if (staticDynel == null)
            {
                return;
            }

            lock (this.sync)
            {
                this.AssignCellUnlocked(staticDynel.Identity, staticDynel.Coordinate);
            }
        }

        internal bool Move(IDynel dynel)
        {
            int oldCellId;
            int newCellId;
            return this.Move(dynel, out oldCellId, out newCellId);
        }

        internal bool Move(IDynel dynel, out int oldCellId, out int newCellId)
        {
            oldCellId = NonLocalCellId;
            newCellId = NonLocalCellId;
            if (dynel == null)
            {
                return false;
            }

            lock (this.sync)
            {
                ICharacter character = dynel as ICharacter;
                if (character != null)
                {
                    this.charactersByIdentity[character.Identity.Long()] = character;
                }

                return this.AssignCellUnlocked(
                    dynel.Identity,
                    new AORebirth.Core.Vector.Coordinate(dynel.Position),
                    out oldCellId,
                    out newCellId);
            }
        }

        internal void Unregister(Identity identity)
        {
            ulong key = identity.Long();
            lock (this.sync)
            {
                this.charactersByIdentity.Remove(key);
                int cellId;
                if (!this.cellByIdentity.TryGetValue(key, out cellId))
                {
                    return;
                }

                this.RemoveFromCellUnlocked(key, cellId);
                this.cellByIdentity.Remove(key);
            }
        }

        internal void Clear()
        {
            lock (this.sync)
            {
                this.cellByIdentity.Clear();
                this.identitiesByCell.Clear();
                this.charactersByIdentity.Clear();
            }
        }

        internal void Synchronize(IEnumerable<ICharacter> characters)
        {
            if (characters == null)
            {
                return;
            }

            lock (this.sync)
            {
                var current = new HashSet<ulong>(
                    characters.Where(c => c != null).Select(c => c.Identity.Long()));
                List<ulong> stale = this.charactersByIdentity.Keys.Where(k => !current.Contains(k)).ToList();
                foreach (ulong key in stale)
                {
                    int cellId;
                    if (this.cellByIdentity.TryGetValue(key, out cellId))
                    {
                        this.RemoveFromCellUnlocked(key, cellId);
                    }

                    this.cellByIdentity.Remove(key);
                    this.charactersByIdentity.Remove(key);
                }

                foreach (ICharacter character in characters)
                {
                    if (character == null)
                    {
                        continue;
                    }

                    this.charactersByIdentity[character.Identity.Long()] = character;
                    this.AssignCellUnlocked(character.Identity, character.CalculatePredictedPosition());
                }
            }
        }

        internal IEnumerable<ICharacter> GetCharactersInCells(IEnumerable<int> cellIds)
        {
            var results = new List<ICharacter>();
            if (cellIds == null)
            {
                return results;
            }

            lock (this.sync)
            {
                foreach (int cellId in cellIds)
                {
                    HashSet<ulong> identities;
                    if (!this.identitiesByCell.TryGetValue(cellId, out identities))
                    {
                        continue;
                    }

                    foreach (ulong identity in identities.ToList())
                    {
                        ICharacter character;
                        if (this.charactersByIdentity.TryGetValue(identity, out character) && character != null)
                        {
                            results.Add(character);
                        }
                    }
                }
            }

            return results;
        }

        internal IEnumerable<ICharacter> AllRegisteredCharacters()
        {
            lock (this.sync)
            {
                return this.charactersByIdentity.Values.ToList();
            }
        }

        internal bool TryGetCellId(IDynel dynel, out int cellId)
        {
            cellId = NonLocalCellId;
            if (dynel == null)
            {
                return false;
            }

            return this.TryGetCellId(dynel.Identity, out cellId);
        }

        internal bool TryGetCellId(Identity identity, out int cellId)
        {
            cellId = NonLocalCellId;
            lock (this.sync)
            {
                return this.cellByIdentity.TryGetValue(identity.Long(), out cellId);
            }
        }

        internal void CollectNeighborCells(int cellId, int radius, List<int> results)
        {
            if (this.layout.IsIndoor || cellId < 0)
            {
                results.Clear();
                return;
            }

            this.layout.CollectNeighbors(cellId, radius, results);
        }

        internal IEnumerable<int> EnumeratePopulatedCells()
        {
            lock (this.sync)
            {
                return this.identitiesByCell.Keys.ToList();
            }
        }

        internal IEnumerable<ICharacter> GetCharactersInCell(int cellId)
        {
            return this.GetCharactersInCells(new[] { cellId });
        }

        private bool AssignCellUnlocked(Identity identity, Coordinate coordinate)
        {
            int oldCellId;
            int newCellId;
            return this.AssignCellUnlocked(identity, coordinate, out oldCellId, out newCellId);
        }

        private bool AssignCellUnlocked(
            Identity identity,
            Coordinate coordinate,
            out int oldCellId,
            out int newCellId)
        {
            oldCellId = NonLocalCellId;
            newCellId = NonLocalCellId;
            if (!this.layout.IsIndoor && coordinate != null)
            {
                if (!this.layout.TryGetCellId(coordinate, out newCellId))
                {
                    newCellId = NonLocalCellId;
                }
            }

            ulong key = identity.Long();
            if (this.cellByIdentity.TryGetValue(key, out oldCellId) && oldCellId == newCellId)
            {
                return false;
            }

            if (this.cellByIdentity.ContainsKey(key))
            {
                this.RemoveFromCellUnlocked(key, oldCellId);
            }

            this.cellByIdentity[key] = newCellId;
            if (newCellId >= 0)
            {
                HashSet<ulong> bucket;
                if (!this.identitiesByCell.TryGetValue(newCellId, out bucket))
                {
                    bucket = new HashSet<ulong>();
                    this.identitiesByCell.Add(newCellId, bucket);
                }

                bucket.Add(key);
            }

            return true;
        }

        private void RemoveFromCellUnlocked(ulong identity, int cellId)
        {
            if (cellId < 0)
            {
                return;
            }

            HashSet<ulong> bucket;
            if (!this.identitiesByCell.TryGetValue(cellId, out bucket))
            {
                return;
            }

            bucket.Remove(identity);
            if (bucket.Count == 0)
            {
                this.identitiesByCell.Remove(cellId);
            }
        }
    }
}
