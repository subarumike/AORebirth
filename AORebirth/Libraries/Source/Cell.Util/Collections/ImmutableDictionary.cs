using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Cell.Util.Collections
{
    public class ImmutableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private BaseImmutableDictionary<TKey, TValue> headDictionary;

        public ImmutableDictionary()
        {
            this.headDictionary = new BaseImmutableDictionary<TKey, TValue>();
        }

        public ImmutableDictionary(int capacity)
        {
            this.headDictionary = new BaseImmutableDictionary<TKey, TValue>(capacity);
        }

        public int Count
        {
            get { return this.headDictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ICollection<TKey> Keys
        {
            get { return this.headDictionary.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return this.headDictionary.Values; }
        }

        public virtual TValue this[TKey key]
        {
            get { return this.headDictionary[key]; }
            set { this.PromoteChanges(dictionary => dictionary.SetValue(key, value)); }
        }

        public virtual void Add(TKey key, TValue value)
        {
            this.PromoteChanges(dictionary => dictionary.Add(key, value));
        }

        public virtual bool Remove(TKey key)
        {
            while (true)
            {
                var current = this.headDictionary;
                if (!current.ContainsKey(key))
                {
                    return false;
                }

                var candidate = current.Remove(key);
                if (Interlocked.CompareExchange(ref this.headDictionary, candidate, current) == current)
                {
                    return true;
                }
            }
        }

        public virtual void Clear()
        {
            this.PromoteChanges(dictionary => dictionary.Clear());
        }

        public bool ContainsKey(TKey key)
        {
            return this.headDictionary.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.headDictionary.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.headDictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            this.Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            return this.TryGetValue(item.Key, out value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!((ICollection<KeyValuePair<TKey, TValue>>)this).Contains(item))
            {
                return false;
            }

            return this.Remove(item.Key);
        }

        private void PromoteChanges(Func<BaseImmutableDictionary<TKey, TValue>, BaseImmutableDictionary<TKey, TValue>> candidateBuilder)
        {
            BaseImmutableDictionary<TKey, TValue> current;
            BaseImmutableDictionary<TKey, TValue> candidate;

            do
            {
                current = this.headDictionary;
                candidate = candidateBuilder(current);
            }
            while (Interlocked.CompareExchange(ref this.headDictionary, candidate, current) != current);
        }
    }
}
