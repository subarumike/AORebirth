using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Cell.Util.Collections
{
    [Serializable]
    public struct HeapEntry<T>
    {
        private T item;
        private IComparable priority;

        public HeapEntry(T item, IComparable priority)
        {
            this.item = item;
            this.priority = priority;
        }

        public T Item
        {
            get { return this.item; }
        }

        public IComparable Priority
        {
            get { return this.priority; }
        }

        public void Clear()
        {
            this.item = default(T);
            this.priority = null;
        }
    }

    [Serializable]
    public class PriorityQueue<T> : IEnumerable<T>, ISerializable
    {
        private const string EntriesName = "entries";

        private readonly List<HeapEntry<T>> entries;
        private int version;

        public PriorityQueue()
        {
            this.entries = new List<HeapEntry<T>>();
        }

        protected PriorityQueue(SerializationInfo info, StreamingContext context)
        {
            var saved = (HeapEntry<T>[])info.GetValue(EntriesName, typeof(HeapEntry<T>[]));
            this.entries = new List<HeapEntry<T>>(saved);
        }

        public int Count
        {
            get { return this.entries.Count; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public void Enqueue(T item, IComparable priority)
        {
            if (priority == null)
            {
                throw new ArgumentNullException("priority");
            }

            this.entries.Add(new HeapEntry<T>(item, priority));
            this.MoveUp(this.entries.Count - 1);
            this.version++;
        }

        public T Dequeue()
        {
            if (this.entries.Count == 0)
            {
                throw new InvalidOperationException();
            }

            T result = this.entries[0].Item;
            int lastIndex = this.entries.Count - 1;
            this.entries[0] = this.entries[lastIndex];
            this.entries.RemoveAt(lastIndex);
            if (this.entries.Count > 0)
            {
                this.MoveDown(0);
            }

            this.version++;
            return result;
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection)this.entries.ToArray()).CopyTo(array, index);
        }

        public IEnumerator<T> GetEnumerator()
        {
            int startVersion = this.version;
            for (int index = 0; index < this.entries.Count; index++)
            {
                if (startVersion != this.version)
                {
                    throw new InvalidOperationException();
                }

                yield return this.entries[index].Item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(EntriesName, this.entries.ToArray(), typeof(HeapEntry<T>[]));
        }

        private void MoveUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (this.entries[parent].Priority.CompareTo(this.entries[index].Priority) >= 0)
                {
                    break;
                }

                this.Swap(parent, index);
                index = parent;
            }
        }

        private void MoveDown(int index)
        {
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= this.entries.Count)
                {
                    return;
                }

                int right = left + 1;
                int largest = right < this.entries.Count
                    && this.entries[left].Priority.CompareTo(this.entries[right].Priority) < 0
                        ? right
                        : left;

                if (this.entries[index].Priority.CompareTo(this.entries[largest].Priority) >= 0)
                {
                    return;
                }

                this.Swap(index, largest);
                index = largest;
            }
        }

        private void Swap(int left, int right)
        {
            HeapEntry<T> temp = this.entries[left];
            this.entries[left] = this.entries[right];
            this.entries[right] = temp;
        }
    }
}
