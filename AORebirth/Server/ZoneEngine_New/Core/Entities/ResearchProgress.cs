namespace ZoneEngine_New.Core.Entities
{
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine_New.Core.Data;

    /// <summary>
    /// A player's personal research tiers in progress: research id to the research XP still needed (characterresearch).
    /// A finished tier leaves here and becomes a trained perk. Mutated on the playfield tick; the flush writer only ever
    /// sees a copied snapshot.
    /// </summary>
    public sealed class ResearchProgress
    {
        readonly object _gate = new();
        readonly Dictionary<int, int> _remaining = new();
        bool _dirty;

        public bool IsDirty
        {
            get
            {
                lock (_gate)
                    return _dirty;
            }
        }

        /// <summary>The XP <paramref name="researchId"/> still needs, when it has been started.</summary>
        public bool TryGetRemaining(int researchId, out int remaining)
        {
            lock (_gate)
                return _remaining.TryGetValue(researchId, out remaining);
        }

        /// <summary>Copy of every tier in progress.</summary>
        public KeyValuePair<int, int>[] Snapshot()
        {
            lock (_gate)
                return _remaining.OrderBy(pair => pair.Key).ToArray();
        }

        /// <summary>Hydration: replaces the progress with the stored rows without marking it dirty.</summary>
        public void Restore(IEnumerable<ResearchProgressRecord> rows)
        {
            lock (_gate)
            {
                _remaining.Clear();
                foreach (ResearchProgressRecord row in rows)
                {
                    if (row.ResearchId > 0 && row.RemainingXp > 0)
                        _remaining[row.ResearchId] = row.RemainingXp;
                }
            }
        }

        /// <summary>Sets the XP <paramref name="researchId"/> still needs; zero or less removes it (finished).</summary>
        public void Set(int researchId, int remaining)
            => Set(researchId, remaining, dirty: true);

        internal void SetCommitted(int researchId, int remaining)
            => Set(researchId, remaining, dirty: false);

        void Set(int researchId, int remaining, bool dirty)
        {
            lock (_gate)
            {
                bool changed = remaining > 0
                    ? !_remaining.TryGetValue(researchId, out int previous) || previous != remaining
                    : _remaining.ContainsKey(researchId);
                if (!changed)
                    return;

                if (remaining > 0)
                    _remaining[researchId] = remaining;
                else
                    _remaining.Remove(researchId);
                _dirty |= dirty;
            }
        }

        /// <summary>GM setup: forgets every tier in progress.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                if (_remaining.Count == 0)
                    return;

                _remaining.Clear();
                _dirty = true;
            }
        }

        /// <summary>Drops progress rows for tiers that are already trained (a crash between the two writes).</summary>
        public void DropFinished(TrainedPerks trained)
        {
            lock (_gate)
            {
                foreach (int researchId in _remaining.Keys.Where(trained.Contains).ToArray())
                {
                    _remaining.Remove(researchId);
                    _dirty = true;
                }
            }
        }

        /// <summary>The rows to persist, or null when nothing changed since the last take.</summary>
        public ResearchProgressRecord[]? TakeDirty()
        {
            lock (_gate)
            {
                if (!_dirty)
                    return null;

                _dirty = false;
                return _remaining.OrderBy(pair => pair.Key)
                    .Select(pair => new ResearchProgressRecord(pair.Key, pair.Value)).ToArray();
            }
        }

        /// <summary>A failed write leaves the progress dirty; the next take re-reads the current rows.</summary>
        public void RestoreDirty(ResearchProgressRecord[]? snapshot)
        {
            if (snapshot == null)
                return;

            lock (_gate)
                _dirty = true;
        }
    }
}
