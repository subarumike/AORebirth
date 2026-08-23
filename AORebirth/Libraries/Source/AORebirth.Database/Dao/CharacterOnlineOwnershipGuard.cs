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

        public static IDisposable AcquireZoneOwnership(int characterId)
        {
            ValidateCharacterId(characterId);
            lock (Sync)
            {
                HeldZoneLease existing;
                if (ZoneLeases.TryGetValue(characterId, out existing))
                {
                    existing.ReferenceCount++;
                    CharacterDao.Instance.SetOnline(characterId);
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
                    CharacterDao.Instance.SetOnline(characterId);
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

        public static LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId)
        {
            ValidateCharacterId(characterId);
            FileStream ownershipStream = TryAcquire(characterId);
            if (ownershipStream == null)
            {
                return LoginOwnedOnlineCleanupResult.ZoneOwned;
            }

            try
            {
                CharacterDao.Instance.SetOffline(characterId);
                return LoginOwnedOnlineCleanupResult.Cleared;
            }
            finally
            {
                ReleaseStream(ownershipStream);
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
            private bool disposed;

            public ZoneLeaseReference(int characterId)
            {
                this.characterId = characterId;
            }

            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                ReleaseZoneOwnership(this.characterId);
            }
        }
    }
}
