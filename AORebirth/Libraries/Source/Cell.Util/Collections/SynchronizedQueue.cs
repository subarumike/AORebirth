using System.Collections.Generic;
using System.Threading;

namespace Cell.Util.Collections
{
    public class SynchronizedQueue<T> : Queue<T>
    {
        private readonly object syncLock = new object();

        public SynchronizedQueue()
        {
        }

        public SynchronizedQueue(int capacity)
            : base(capacity)
        {
        }

        public SynchronizedQueue(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public new int Count
        {
            get
            {
                lock (this.syncLock)
                {
                    return base.Count;
                }
            }
        }

        public new void Clear()
        {
            lock (this.syncLock)
            {
                base.Clear();
            }
        }

        public new bool Contains(T obj)
        {
            lock (this.syncLock)
            {
                return base.Contains(obj);
            }
        }

        public new void CopyTo(T[] array, int arrayIndex)
        {
            lock (this.syncLock)
            {
                base.CopyTo(array, arrayIndex);
            }
        }

        public new T Dequeue()
        {
            lock (this.syncLock)
            {
                return base.Dequeue();
            }
        }

        public new void Enqueue(T value)
        {
            lock (this.syncLock)
            {
                base.Enqueue(value);
            }
        }

        public new IEnumerator<T> GetEnumerator()
        {
            lock (this.syncLock)
            {
                return new List<T>(this).GetEnumerator();
            }
        }

        public new T Peek()
        {
            lock (this.syncLock)
            {
                return base.Peek();
            }
        }

        public new T[] ToArray()
        {
            lock (this.syncLock)
            {
                return base.ToArray();
            }
        }

        public new void TrimExcess()
        {
            lock (this.syncLock)
            {
                base.TrimExcess();
            }
        }

        public void LockCollection()
        {
            Monitor.Enter(this.syncLock);
        }

        public void UnlockCollection()
        {
            Monitor.Exit(this.syncLock);
        }
    }
}
