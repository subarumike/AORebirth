namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>Bounds the resolver's ownership of parsed resources. Callers may keep a resource after eviction.</summary>
internal sealed class RetainedResourceCache<TKey, TValue>(int capacity) where TKey : notnull
{
    readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    readonly Lock _gate = new();
    readonly Dictionary<TKey, Entry> _entries = new();
    readonly LinkedList<TKey> _recent = new();

    sealed record Entry(LinkedListNode<TKey> Position, Lazy<TValue> Resource);

    public TValue Get(TKey key, Func<TKey, TValue> load)
    {
        Entry entry;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out Entry? found))
            {
                entry = found;
                _recent.Remove(entry.Position);
                _recent.AddFirst(entry.Position);
            }
            else
            {
                if (_entries.Count == _capacity)
                {
                    _entries.Remove(_recent.Last!.Value);
                    _recent.RemoveLast();
                }
                entry = new(_recent.AddFirst(key), new Lazy<TValue>(() => load(key)));
                _entries.Add(key, entry);
            }
        }

        try
        {
            return entry.Resource.Value;
        }
        catch
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out Entry? current) && ReferenceEquals(current, entry))
                {
                    _entries.Remove(key);
                    _recent.Remove(entry.Position);
                }
            }
            throw;
        }
    }
}
