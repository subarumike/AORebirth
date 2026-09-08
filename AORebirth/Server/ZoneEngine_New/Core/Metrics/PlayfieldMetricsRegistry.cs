namespace ZoneEngine_New.Core.Metrics
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public interface IPlayfieldMetricsRegistry
    {
        PlayfieldMetrics GetOrCreate(int playfieldId);

        bool TryGetSnapshot(int playfieldId, int windowSeconds, out PlayfieldMetricsSnapshot? snapshot);

        IReadOnlyList<PlayfieldMetricsSnapshot> GetAllSnapshots(int windowSeconds);

        void Remove(int playfieldId);

        void Clear();
    }

    public sealed class PlayfieldMetricsRegistry : IPlayfieldMetricsRegistry
    {
        private readonly ConcurrentDictionary<int, PlayfieldMetrics> _byPlayfieldId = new();

        public PlayfieldMetrics GetOrCreate(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);
            return _byPlayfieldId.GetOrAdd(playfieldId, static id => new PlayfieldMetrics(id));
        }

        public bool TryGetSnapshot(int playfieldId, int windowSeconds, out PlayfieldMetricsSnapshot? snapshot)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

            if (!_byPlayfieldId.TryGetValue(playfieldId, out PlayfieldMetrics? metrics))
            {
                snapshot = null;
                return false;
            }

            snapshot = metrics.Snapshot(windowSeconds);
            return true;
        }

        public IReadOnlyList<PlayfieldMetricsSnapshot> GetAllSnapshots(int windowSeconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

            List<PlayfieldMetricsSnapshot> snapshots = new(_byPlayfieldId.Count);
            foreach (KeyValuePair<int, PlayfieldMetrics> pair in _byPlayfieldId)
                snapshots.Add(pair.Value.Snapshot(windowSeconds));

            snapshots.Sort(static (a, b) => a.PlayfieldId.CompareTo(b.PlayfieldId));
            return snapshots;
        }

        public void Remove(int playfieldId)
        {
            _byPlayfieldId.TryRemove(playfieldId, out _);
        }

        public void Clear()
        {
            _byPlayfieldId.Clear();
        }
    }
}
