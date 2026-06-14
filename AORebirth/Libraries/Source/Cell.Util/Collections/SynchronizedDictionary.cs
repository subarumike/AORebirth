using System.Collections.Generic;
using System.Threading;

namespace Cell.Util.Collections
{
    public class SynchronizedDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private readonly object syncLock = new object();

        public SynchronizedDictionary()
        {
        }

        public SynchronizedDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
        }

        public SynchronizedDictionary(int capacity)
            : base(capacity)
        {
        }

        public SynchronizedDictionary(IDictionary<TKey, TValue> dictionary)
            : base(dictionary)
        {
        }

        public object SyncLock
        {
            get { return this.syncLock; }
        }

        public virtual new TValue this[TKey key]
        {
            get
            {
                lock (this.syncLock)
                {
                    return base[key];
                }
            }

            set
            {
                lock (this.syncLock)
                {
                    base[key] = value;
                }
            }
        }

        public virtual new void Add(TKey key, TValue value)
        {
            lock (this.syncLock)
            {
                base.Add(key, value);
            }
        }

        public virtual new void Clear()
        {
            lock (this.syncLock)
            {
                base.Clear();
            }
        }

        public new bool ContainsKey(TKey key)
        {
            lock (this.syncLock)
            {
                return base.ContainsKey(key);
            }
        }

        public virtual new bool Remove(TKey key)
        {
            lock (this.syncLock)
            {
                return base.Remove(key);
            }
        }

        public void Lock()
        {
            Monitor.Enter(this.syncLock);
        }

        public void Unlock()
        {
            Monitor.Exit(this.syncLock);
        }
    }
}
