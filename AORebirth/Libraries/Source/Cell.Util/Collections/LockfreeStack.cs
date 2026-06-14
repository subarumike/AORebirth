using System;
using System.Collections.Concurrent;

namespace Cell.Util.Collections
{
    public class LockFreeStack<T>
    {
        private readonly ConcurrentStack<T> stack = new ConcurrentStack<T>();

        public void Push(T item)
        {
            this.stack.Push(item);
        }

        public bool Pop(out T item)
        {
            return this.stack.TryPop(out item);
        }

        public T Pop()
        {
            T result;
            if (!this.Pop(out result))
            {
                throw new InvalidOperationException("the stack is empty");
            }

            return result;
        }
    }
}
