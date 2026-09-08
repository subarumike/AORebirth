namespace AORebirth.Database.Dao
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    public enum LoginOwnedOnlineCleanupResult
    {
        Cleared,
        ZoneOwned
    }

    /// <summary>
    /// Cross-process ownership gate for the character Online flag. The ZoneEngine
    /// holds the byte-range lock for the lifetime of an accepted zone session.
    /// </summary>
    public static class CharacterOnlineOwnershipGuard
    {
        private const int ZoneAcquireTimeoutMilliseconds = 5000;
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, HeldZoneLease> ZoneLeases =
            new Dictionary<int, HeldZoneLease>();

#if !AOREBIRTH_WIN_NET10
        public static IDisposable AcquireZoneOwnership(int characterId)
        {
            return AcquireZoneOwnership(characterId, CharacterDao.Instance.SetOnline);
        }
#endif

        public static IDisposable AcquireZoneOwnership(int characterId, Action<int> setOnline)
        {
            ValidateCharacterId(characterId);
            if (setOnline == null) throw new ArgumentNullException("setOnline");
            lock (Sync)
            {
                HeldZoneLease existing;
                if (ZoneLeases.TryGetValue(characterId, out existing))
                {
                    setOnline(characterId);
                    existing.ReferenceCount++;
                    return new ZoneLeaseReference(characterId);
                }

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(ZoneAcquireTimeoutMilliseconds);
                FileStream ownershipStream;
                do
                {
                    ownershipStream = TryAcquire(characterId);
                    if (ownershipStream != null)
                    {
                        break;
                    }

                    Thread.Sleep(25);
                }
                while (DateTime.UtcNow < deadline);

                if (ownershipStream == null)
                {
                    throw new InvalidOperationException(
                        "Timed out acquiring ZoneEngine online ownership for character " + characterId + ".");
                }

                try
                {
                    setOnline(characterId);
                    ZoneLeases.Add(characterId, new HeldZoneLease(ownershipStream));
                    return new ZoneLeaseReference(characterId);
                }
                catch
                {
                    ReleaseStream(ownershipStream);
                    throw;
                }
            }
        }

#if !AOREBIRTH_WIN_NET10
        public static LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId)
        {
            return TryClearLoginOwnership(characterId, CharacterDao.Instance.SetOffline);
        }
#endif

        public static LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId, Action<int> setOffline)
        {
            ValidateCharacterId(characterId);
            if (setOffline == null) throw new ArgumentNullException("setOffline");
            lock (Sync)
            {
                // Unix byte-range locks are process-scoped. Opening and closing another
                // descriptor for an already-held file could release the zone's lock.
                if (ZoneLeases.ContainsKey(characterId)) return LoginOwnedOnlineCleanupResult.ZoneOwned;
                FileStream ownershipStream = TryAcquire(characterId);
                if (ownershipStream == null)
                {
                    return LoginOwnedOnlineCleanupResult.ZoneOwned;
                }

                try
                {
                    setOffline(characterId);
                    return LoginOwnedOnlineCleanupResult.Cleared;
                }
                finally
                {
                    ReleaseStream(ownershipStream);
                }
            }
        }

        private static FileStream TryAcquire(int characterId)
        {
            string directory = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Path.GetTempPath(), "ao-rebirth-session-ownership");
            }

            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "character-" + characterId + ".lock");
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            try
            {
                stream.Lock(0, 1);
                return stream;
            }
            catch (IOException)
            {
                stream.Dispose();
                return null;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static void ReleaseZoneOwnership(int characterId)
        {
            lock (Sync)
            {
                HeldZoneLease lease;
                if (!ZoneLeases.TryGetValue(characterId, out lease))
                {
                    return;
                }

                lease.ReferenceCount--;
                if (lease.ReferenceCount > 0)
                {
                    return;
                }

                ZoneLeases.Remove(characterId);
                ReleaseStream(lease.Stream);
            }
        }

        private static void ReleaseStream(FileStream stream)
        {
            try
            {
                stream.Unlock(0, 1);
            }
            catch (IOException)
            {
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static void ValidateCharacterId(int characterId)
        {
            if (characterId <= 0)
            {
                throw new ArgumentOutOfRangeException("characterId");
            }
        }

        private sealed class HeldZoneLease
        {
            public HeldZoneLease(FileStream stream)
            {
                this.Stream = stream;
                this.ReferenceCount = 1;
            }

            public FileStream Stream { get; private set; }

            public int ReferenceCount { get; set; }
        }

        private sealed class ZoneLeaseReference : IDisposable
        {
            private readonly int characterId;
            private int disposed;

            public ZoneLeaseReference(int characterId)
            {
                this.characterId = characterId;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.disposed, 1) != 0)
                {
                    return;
                }

                ReleaseZoneOwnership(this.characterId);
            }
        }
    }
}
