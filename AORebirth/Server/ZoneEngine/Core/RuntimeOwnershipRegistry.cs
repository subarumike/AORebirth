namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class RuntimeOwnershipRegistry<TKey, TRuntime> : IDisposable
        where TRuntime : class, IDisposable
    {
        private readonly object sync = new object();

        private readonly Dictionary<TKey, TRuntime> runtimes = new Dictionary<TKey, TRuntime>();

        private readonly Func<TKey, TRuntime> runtimeFactory;

        private bool disposed;

        internal RuntimeOwnershipRegistry(Func<TKey, TRuntime> runtimeFactory)
        {
            if (runtimeFactory == null)
            {
                throw new ArgumentNullException("runtimeFactory");
            }

            this.runtimeFactory = runtimeFactory;
        }

        internal TRuntime GetOrCreate(TKey key)
        {
            lock (this.sync)
            {
                this.ThrowIfDisposed();

                TRuntime runtime;
                if (this.runtimes.TryGetValue(key, out runtime))
                {
                    return runtime;
                }

                runtime = this.runtimeFactory(key);
                this.runtimes.Add(key, runtime);
                return runtime;
            }
        }

        internal TRuntime Replace(TKey key)
        {
            lock (this.sync)
            {
                this.ThrowIfDisposed();

                TRuntime previous;
                if (this.runtimes.TryGetValue(key, out previous))
                {
                    this.runtimes.Remove(key);
                    previous.Dispose();
                }

                TRuntime replacement = this.runtimeFactory(key);
                this.runtimes.Add(key, replacement);
                return replacement;
            }
        }

        internal IList<TRuntime> Snapshot()
        {
            lock (this.sync)
            {
                return this.runtimes.Values.ToList();
            }
        }

        public void Dispose()
        {
            IList<TRuntime> ownedRuntimes;
            lock (this.sync)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                ownedRuntimes = this.runtimes.Values.ToList();
                this.runtimes.Clear();
            }

            foreach (TRuntime runtime in ownedRuntimes)
            {
                runtime.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}
