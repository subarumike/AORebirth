using System.Collections.Generic;
using System.Threading;

namespace Cell.Util.Collections
{
    public class SynchronizedList<T> : List<T>
    {
        private readonly object syncLock = new object();

        public SynchronizedList()
        {
        }

        public SynchronizedList(int capacity)
            : base(capacity)
        {
        }

        public SynchronizedList(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public new T this[int index]
        {
            get
            {
                lock (this.syncLock)
                {
                    return base[index];
                }
            }

            set
            {
                lock (this.syncLock)
                {
                    base[index] = value;
                }
            }
        }

        public new void Add(T value)
        {
            lock (this.syncLock)
            {
                base.Add(value);
            }
        }

        public new bool Remove(T value)
        {
            lock (this.syncLock)
            {
                return base.Remove(value);
            }
        }

        public new void RemoveAt(int index)
        {
            lock (this.syncLock)
            {
                base.RemoveAt(index);
            }
        }

        protected void RemoveUnlocked(int index)
        {
            base.RemoveAt(index);
        }

        public new void Clear()
        {
            lock (this.syncLock)
            {
                base.Clear();
            }
        }

        public new bool Contains(T item)
        {
            lock (this.syncLock)
            {
                return base.Contains(item);
            }
        }

        public void EnterLock()
        {
            Monitor.Enter(this.syncLock);
        }

        public void ExitLock()
        {
            Monitor.Exit(this.syncLock);
        }
    }
}
