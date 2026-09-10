namespace AORebirth.Database.Dao
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    /// <summary>Server-only, cross-process authority for the two retail opaque cookies.
    /// Shares the governed ownership directory, never a database schema or client account field.</summary>
    public sealed class ZoneHandoffStore
    {
        private static readonly object ProcessLock = new object();
        private readonly string directory;
        private readonly Func<DateTime> utcNow;
        public ZoneHandoffStore(string directory, Func<DateTime> utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Private handoff directory required.");
            this.directory = Path.GetFullPath(directory);
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }
        public static ZoneHandoffStore Configured()
        {
            string root = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
            if (string.IsNullOrWhiteSpace(root))
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    throw new InvalidOperationException("AO_REBIRTH_SESSION_OWNERSHIP_DIR must identify a private shared directory.");
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AORebirth", "session-ownership");
            }
            return new ZoneHandoffStore(Path.Combine(root, "zone-handoffs-v1"));
        }
        public static int LifetimeSeconds
        {
            get
            {
                int seconds;
                return int.TryParse(Environment.GetEnvironmentVariable("AO_REBIRTH_LOGIN_HANDOFF_TIMEOUT_SECONDS"), out seconds)
                    && seconds >= 5 && seconds <= 120 ? seconds : 30;
            }
        }
        // Call only after credentials are verified, never for an untrusted login challenge.
        public string BeginLogin(string authenticatedAccount)
        {
            ValidateAccount(authenticatedAccount);
            return Locked(() =>
            {
                string generation = Guid.NewGuid().ToString("N");
                Write(AccountPath(authenticatedAccount), writer => writer.Write(generation));
                return generation;
            });
        }
        public ZoneHandoffTicket Issue(string account, string generation, int selectedCharacter)
        {
            ValidateAccount(account);
            if (selectedCharacter <= 0) throw new ArgumentOutOfRangeException("selectedCharacter");
            return Locked(() =>
            {
                if (!Current(account, generation)) throw new InvalidOperationException("Stale authenticated login.");
                var bytes = new byte[8];
                using (var random = RandomNumberGenerator.Create())
                    do { random.GetBytes(bytes); } while (BitConverter.ToUInt64(bytes, 0) == 0);
                var ticket = new ZoneHandoffTicket(BitConverter.ToUInt32(bytes, 0), BitConverter.ToUInt32(bytes, 4));
                long issued = utcNow().Ticks;
                var record = new Record { Account = account, Generation = generation, Character = selectedCharacter,
                    Hash = Hash(ticket.Cookie1, ticket.Cookie2), Issued = issued,
                    Expires = checked(issued + TimeSpan.FromSeconds(LifetimeSeconds).Ticks) };
                Save(record);
                return ticket;
            });
        }
        public ZoneHandoffClaim Claim(int character, uint cookie1, uint cookie2, Func<int, string> resolveCurrentAccount)
        {
            if (character <= 0 || (cookie1 == 0 && cookie2 == 0)) return new ZoneHandoffClaim(false, "missing_handoff");
            if (resolveCurrentAccount == null) throw new ArgumentNullException("resolveCurrentAccount");
            return Locked(() =>
            {
                string path = CharacterPath(character);
                if (!File.Exists(path)) return new ZoneHandoffClaim(false, "unknown_handoff");
                Record record;
                using (var reader = Read(path))
                {
                    if (reader.ReadInt32() != 1) throw new InvalidDataException("Invalid handoff version.");
                    record = new Record { Character = reader.ReadInt32(), Account = reader.ReadString(), Generation = reader.ReadString(),
                        Hash = reader.ReadBytes(32), Issued = reader.ReadInt64(), Expires = reader.ReadInt64(), Consumed = reader.ReadBoolean() };
                    if (record.Hash.Length != 32 || reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Invalid handoff record.");
                }
                if (record.Character != character || !Equal(record.Hash, Hash(cookie1, cookie2))) return new ZoneHandoffClaim(false, "unknown_handoff");
                long now = utcNow().Ticks;
                if (now < record.Issued || now >= record.Expires || record.Expires - record.Issued > TimeSpan.FromSeconds(120).Ticks)
                    return new ZoneHandoffClaim(false, "expired_handoff");
                if (record.Consumed) return new ZoneHandoffClaim(false, "already_claimed");
                if (!Current(record.Account, record.Generation)) return new ZoneHandoffClaim(false, "stale_login");
                if (!string.Equals(record.Account, resolveCurrentAccount(character), StringComparison.OrdinalIgnoreCase))
                    return new ZoneHandoffClaim(false, "account_mismatch");
                record.Consumed = true;
                Save(record); // Durable claim before any hydration/ownership. Crash here requires a fresh login.
                return new ZoneHandoffClaim(true, "claimed");
            });
        }
        private bool Current(string account, string generation)
        {
            string path = AccountPath(account);
            if (string.IsNullOrEmpty(generation) || !File.Exists(path)) return false;
            using (var reader = Read(path)) return reader.ReadString() == generation;
        }
        private void Save(Record record) => Write(CharacterPath(record.Character), writer =>
        {
            writer.Write(1); writer.Write(record.Character); writer.Write(record.Account); writer.Write(record.Generation);
            writer.Write(record.Hash); writer.Write(record.Issued); writer.Write(record.Expires); writer.Write(record.Consumed);
        });
        private string CharacterPath(int id) => Path.Combine(directory, "character-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
        private string AccountPath(string account)
        {
            using (var hash = SHA256.Create()) return Path.Combine(directory,
                "account-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(account.ToUpperInvariant()))).Replace("-", "") + ".bin");
        }
        private static byte[] Hash(uint first, uint second)
        {
            byte[] bytes = new byte[8];
            for (int i = 0; i < 4; i++) { bytes[i] = (byte)(first >> (24 - i * 8)); bytes[i + 4] = (byte)(second >> (24 - i * 8)); }
            using (var hash = SHA256.Create()) return hash.ComputeHash(bytes);
        }
        private static bool Equal(byte[] a, byte[] b)
        {
            int difference = a.Length ^ b.Length;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++) difference |= a[i] ^ b[i];
            return difference == 0;
        }
        private static void ValidateAccount(string account)
        { if (string.IsNullOrWhiteSpace(account) || account.Length > 32) throw new ArgumentException("Authenticated account required."); }
        private static BinaryReader Read(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 2048) { stream.Dispose(); throw new InvalidDataException("Oversized handoff record."); }
            return new BinaryReader(stream, Encoding.UTF8);
        }
        private static void Write(string path, Action<BinaryWriter> write)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) { write(writer); writer.Flush(); }
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private T Locked<T>(Func<T> action)
        {
            lock (ProcessLock)
            {
#if NET8_0_OR_GREATER
                if (!OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    if ((File.GetUnixFileMode(directory) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                        throw new InvalidOperationException("Handoff directory must be private to the engine service account.");
                }
                else Directory.CreateDirectory(directory);
#else
                Directory.CreateDirectory(directory);
#endif
                using (var stream = new FileStream(Path.Combine(directory, "authority.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    while (true)
                    {
                        try { stream.Lock(0, 1); break; }
                        catch (IOException) { if (elapsed.ElapsedMilliseconds >= 5000) throw; Thread.Sleep(10); }
                    }
                    try { return action(); } finally { stream.Unlock(0, 1); }
                }
            }
        }
        private sealed class Record
        {
            public int Character; public string Account; public string Generation; public byte[] Hash;
            public long Issued; public long Expires; public bool Consumed;
        }
    }
    public sealed class ZoneHandoffTicket
    {
        public ZoneHandoffTicket(uint first, uint second) { Cookie1 = first; Cookie2 = second; }
        public uint Cookie1 { get; private set; }
        public uint Cookie2 { get; private set; }
    }
    public sealed class ZoneHandoffClaim
    {
        public ZoneHandoffClaim(bool accepted, string reason) { Accepted = accepted; Reason = reason; }
        public bool Accepted { get; private set; }
        public string Reason { get; private set; }
    }
}
