using System;
using System.Collections.Generic;
using Cell.Util.ObjectPools;

namespace Cell.Core
{
    /// <summary>
    /// Registry for reusable object pools keyed by pooled type.
    /// </summary>
    public static class ObjectPoolMgr
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, IObjectPool> Pools = new Dictionary<Type, IObjectPool>();

        public static bool ContainsType<T>()
        {
            return ContainsType(typeof(T));
        }

        public static bool ContainsType(Type t)
        {
            lock (SyncRoot)
            {
                return Pools.ContainsKey(t);
            }
        }

        public static bool RegisterType<T>(Func<T> func) where T : class
        {
            lock (SyncRoot)
            {
                if (Pools.ContainsKey(typeof(T)))
                {
                    return false;
                }

                Pools.Add(typeof(T), new ObjectPool<T>(func));
                return true;
            }
        }

        public static void SetMinimumSize<T>(int minSize) where T : class
        {
            ObjectPool<T> pool = GetPool<T>();
            if (pool != null)
            {
                pool.MinimumSize = minSize;
            }
        }

        public static void ReleaseObject<T>(T obj) where T : class
        {
            ObjectPool<T> pool = GetPool<T>();
            if (pool != null)
            {
                pool.Recycle(obj);
            }
        }

        public static T ObtainObject<T>() where T : class
        {
            ObjectPool<T> pool = GetPool<T>();
            return pool == null ? default(T) : pool.Obtain();
        }

        public static ObjectPoolInfo GetPoolInfo<T>() where T : class
        {
            ObjectPool<T> pool = GetPool<T>();
            return pool == null ? new ObjectPoolInfo(0, 0) : pool.Info;
        }

        private static ObjectPool<T> GetPool<T>() where T : class
        {
            lock (SyncRoot)
            {
                IObjectPool pool;
                return Pools.TryGetValue(typeof(T), out pool) ? (ObjectPool<T>)pool : null;
            }
        }
    }
}
