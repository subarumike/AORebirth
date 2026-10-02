namespace ZoneEngine_New.Core.Entities
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The perk ids a player has trained: the persistent perk state (charactersperks). Mutated on the
    /// playfield tick; the flush writer only ever sees a copied snapshot.
    /// </summary>
    public sealed class TrainedPerks
    {
        readonly object _gate = new();
        readonly HashSet<int> _perkIds = new();
        bool _dirty;

        public bool IsDirty
        {
            get
            {
                lock (_gate)
                    return _dirty;
            }
        }

        public bool Contains(int perkId)
        {
            lock (_gate)
                return _perkIds.Contains(perkId);
        }

        /// <summary>Sorted copy of the trained perk ids.</summary>
        public int[] Snapshot()
        {
            lock (_gate)
                return _perkIds.Order().ToArray();
        }

        /// <summary>Hydration: replaces the set with the stored perks without marking it dirty.</summary>
        public void Restore(IEnumerable<int> perkIds)
        {
            lock (_gate)
            {
                _perkIds.Clear();
                foreach (int perkId in perkIds)
                {
                    if (perkId > 0)
                        _perkIds.Add(perkId);
                }
            }
        }

        public bool Add(int perkId)
        {
            lock (_gate)
            {
                if (!_perkIds.Add(perkId))
                    return false;
                _dirty = true;
                return true;
            }
        }

        public bool Remove(int perkId)
        {
            lock (_gate)
            {
                if (!_perkIds.Remove(perkId))
                    return false;
                _dirty = true;
                return true;
            }
        }

        /// <summary>The set to persist, or null when nothing changed since the last take.</summary>
        public int[]? TakeDirty()
        {
            lock (_gate)
            {
                if (!_dirty)
                    return null;

                _dirty = false;
                return _perkIds.Order().ToArray();
            }
        }

        /// <summary>A failed write leaves the set dirty; the next take re-reads the current perks.</summary>
        public void RestoreDirty(int[]? snapshot)
        {
            if (snapshot == null)
                return;

            lock (_gate)
                _dirty = true;
        }
    }
}
