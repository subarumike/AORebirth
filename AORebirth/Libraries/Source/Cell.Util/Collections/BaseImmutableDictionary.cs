using System;
using System.Collections;
using System.Collections.Generic;

namespace Cell.Util.Collections
{
    public sealed class BaseImmutableDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> dictionary;

        public BaseImmutableDictionary()
        {
            this.dictionary = new Dictionary<TKey, TValue>();
        }

        public BaseImmutableDictionary(int capacity)
        {
            this.dictionary = new Dictionary<TKey, TValue>(capacity);
        }

        private BaseImmutableDictionary(Dictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary;
        }

        public TValue this[TKey key]
        {
            get { return this.dictionary[key]; }
        }

        public int Count
        {
            get { return this.dictionary.Count; }
        }

        public ICollection<TKey> Keys
        {
            get { return this.dictionary.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return this.dictionary.Values; }
        }

        public BaseImmutableDictionary<TKey, TValue> Add(TKey key, TValue value)
        {
            return this.CopyAndMutate(copy => copy.Add(key, value));
        }

        public BaseImmutableDictionary<TKey, TValue> Remove(TKey key)
        {
            return this.CopyAndMutate(copy => copy.Remove(key));
        }

        public BaseImmutableDictionary<TKey, TValue> Clear()
        {
            return new BaseImmutableDictionary<TKey, TValue>();
        }

        public BaseImmutableDictionary<TKey, TValue> SetValue(TKey key, TValue value)
        {
            return this.CopyAndMutate(copy => copy[key] = value);
        }

        public bool ContainsKey(TKey key)
        {
            return this.dictionary.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.dictionary.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private BaseImmutableDictionary<TKey, TValue> CopyAndMutate(Action<Dictionary<TKey, TValue>> mutator)
        {
            var copy = new Dictionary<TKey, TValue>(this.dictionary);
            mutator(copy);
            return new BaseImmutableDictionary<TKey, TValue>(copy);
        }
    }
}
