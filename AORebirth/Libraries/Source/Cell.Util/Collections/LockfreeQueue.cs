using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Cell.Util.Collections
{
    public class LockfreeQueue<T> : IEnumerable<T>
    {
        private ConcurrentQueue<T> queue;

        public LockfreeQueue()
        {
            this.queue = new ConcurrentQueue<T>();
        }

        public LockfreeQueue(IEnumerable<T> items)
        {
            this.queue = new ConcurrentQueue<T>(items);
        }

        public int Count
        {
            get { return this.queue.Count; }
        }

        public void Enqueue(T item)
        {
            this.queue.Enqueue(item);
        }

        public T TryDequeue()
        {
            T item;
            this.TryDequeue(out item);
            return item;
        }

        public bool TryDequeue(out T item)
        {
            return this.queue.TryDequeue(out item);
        }

        public T Dequeue()
        {
            T result;
            if (!this.TryDequeue(out result))
            {
                throw new InvalidOperationException("the queue is empty");
            }

            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.queue.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Clear()
        {
            this.queue = new ConcurrentQueue<T>();
        }
    }
}
