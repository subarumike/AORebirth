namespace AORebirth.Tools.AccountDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using AORebirth.Core.Encryption;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.Accounts;
    using AORebirth.Interfaces.Persistence.Accounts;
    using Dapper;
    using MySqlConnector;

    internal static partial class Program
    {
        private static int checks;
        private static string category;
        private static string application;
        private static string rootConnection;

        private static int Main(string[] args)
        {
            if (args.Length != 1 || args[0] != "--run-disposable"
                || Environment.GetEnvironmentVariable("AO_REBIRTH_ALLOW_DISPOSABLE_ACCOUNT_DAO_VALIDATION") != "1")
            {
                Console.Error.WriteLine("REFUSED: exact disposable argument and wrapper acknowledgement required.");
                return 2;
            }
            DisposableMySql fixture = null;
            int result = 1;
            try
            {
                fixture = DisposableMySql.Create();
                application = fixture.ApplicationConnectionString;
                rootConnection = fixture.RootConnectionString;
                using (MySqlConnection connection = fixture.WaitForReady())
                {
                    string directory = Path.Combine(Directory.GetCurrentDirectory(), "AORebirth", "Libraries",
                        "Source", "AORebirth.Database", "SqlTables");
                    foreach (string name in new[] { "login.sql", "characters.sql" })
                        connection.Execute(File.ReadAllText(Path.Combine(directory, name)));
                    string collation = connection.Query<string>("SELECT COLLATION_NAME FROM information_schema.COLUMNS "
                        + "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='login' AND COLUMN_NAME='Username'").Single();
                    Console.WriteLine("ACCOUNT_DAO_FIXTURE_USERNAME_COLLATION=" + collation);
                }
                category = "contract";
                ContractChecks();
                var dao = NewDao(application);
                category = "reads";
                ReadChecks(dao);
                category = "create";
                CreateChecks(dao);
                category = "resolution";
                ResolutionChecks(dao);
                foreach (bool affected in new[] { false, true })
                {
                    string connectionString = new MySqlConnectionStringBuilder(application)
                        { UseAffectedRows = affected }.ConnectionString;
                    category = affected ? "changed-rows" : "matched-rows";
                    MutationChecks(connectionString, affected);
                    LegacyGmChecks(connectionString, affected);
                }
                category = "concurrency";
                ConcurrencyChecks(dao);
                category = "faults";
                SuccessfulOwnershipChecks();
                FailureChecks();
                category = "mock-defensive";
                SyntheticChecks();
                Console.WriteLine("ACCOUNT_DAO_TEST_MODE=ISOLATED_PRODUCTION_AND_LEGACY_SOURCES");
                Console.WriteLine("ACCOUNT_DAO_CHECKS=" + checks.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("ACCOUNT_DAO_MYSQL_INTEGRATION=PASS");
                result = 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("ACCOUNT_DAO_MYSQL_INTEGRATION=FAIL");
                // Provider messages can contain account/hash data; never print them.
                Console.Error.WriteLine("ERROR_TYPE=" + exception.GetType().Name);
                if (exception is CheckFailure) Console.Error.WriteLine("FAILED_CASE=" + exception.Message);
                if (exception is MySqlException)
                    Console.Error.WriteLine("MYSQL_ERROR_NUMBER=" + ((MySqlException)exception).Number);
            }
            finally
            {
                Connector.TestConnectionFactory = null;
                if (fixture != null)
                {
                    try { fixture.Dispose(); }
                    catch { Console.Error.WriteLine("ACCOUNT_DAO_DISPOSABLE_CLEANUP=FAIL"); result = 1; }
                }
            }
            return result;
        }

        private static MySqlAccountDao NewDao(string connectionString)
        {
            return new MySqlAccountDao(() => new MySqlConnection(connectionString));
        }

        private static void ContractChecks()
        {
            string[] methods = typeof(IAccountDao).GetMethods().Select(x => x.Name).OrderBy(x => x).ToArray();
            Require(methods.SequenceEqual(new[] { "ChangePassword", "CountRegisteredAccounts", "CreateGameAccount",
                "LoadByCharacterId", "LoadByUsername", "LoadForAuthentication", "SetExpansions", "UsernameExists" }),
                "exact-account-surface-no-gm-logoff-or-identity-workflow");
            Require(typeof(GameAccountData).IsSealed && typeof(GameAccountData).GetProperties().Length == 12,
                "detached-full-data-fields");
            Require(typeof(GameAccountAuthenticationData).IsSealed
                && typeof(GameAccountAuthenticationData).GetProperties().Length == 8, "minimal-auth-fields");
            Require(typeof(NewGameAccountData).GetProperties().Length == 10, "creation-fields-no-id-or-clock-input");
            Expect<ArgumentNullException>(() => new MySqlAccountDao(null), "null-factory-rejected");
            int opened = 0;
            Connector.TestConnectionFactory = () => { opened++; return new MySqlConnection(application); };
            var factory = DatabaseDaoFactory.CreateAccountDao();
            Require(factory is MySqlAccountDao && opened == 0, "factory-is-lazy-mysql");
            Require(factory.CountRegisteredAccounts() == 0 && opened == 1, "factory-real-configured-test-host");
            Connector.TestConnectionFactory = null;
            var fail = new MySqlAccountDao(() => { throw new InjectedFailure(); });
            Expect<ArgumentNullException>(() => fail.CreateGameAccount(null), "null-command-before-resource-acquisition");
            Require(new NewGameAccountData().AllowedCharacters == 0 && new NewGameAccountData().Expansions == 0,
                "dto-does-not-invent-runtime-defaults");
        }

        private static void ReadChecks(IAccountDao dao)
        {
            Require(dao.CountRegisteredAccounts() == 0, "zero-accounts-count");
            foreach (string name in new string[] { null, "", "absent", "' OR 1=1 --" })
            {
                Require(dao.LoadByUsername(name) == null && dao.LoadForAuthentication(name) == null
                    && !dao.UsernameExists(name), "missing-null-empty-quoted-lookup-" + checks);
            }
            using (MySqlConnection connection = Open(rootConnection))
            {
                connection.Execute("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,"
                    + "AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES "
                    + "(41,@date,@email,@first,@last,@name,@hash,@allowed,@flags,@accountFlags,@expansions,@gm)",
                    new { date = new DateTime(2001, 2, 3, 4, 5, 6), email = "profile@example.test",
                        first = "First", last = "Last", name = "MixedUser", hash = "legacy:opaque/hash+value",
                        allowed = int.MinValue, flags = int.MinValue, accountFlags = int.MaxValue,
                        expansions = -1, gm = int.MaxValue });
            }
            Require(dao.CountRegisteredAccounts() == 1, "one-account-count");
            WithLegacy(application, legacy =>
            {
                foreach (string name in new string[] { "MixedUser", "mixeduser", "MIXEDUSER", "MixedUser ", " MixedUser", null, "" })
                {
                    DBLoginData old = legacy.GetByUsername(name);
                    GameAccountData current = dao.LoadByUsername(name);
                    Require(Equal(old, current), "lookup-parity-case-space-null-" + checks);
                    Require(legacy.Exists(name) == dao.UsernameExists(name), "exists-parity-" + checks);
                    Require(AuthEqual(old, dao.LoadForAuthentication(name)), "auth-projection-parity-" + checks);
                }
                Require(legacy.GetRegisteredCount() == dao.CountRegisteredAccounts(), "count-parity");
            });
            GameAccountData row = dao.LoadByUsername("MixedUser");
            Require(row.CreationDate == new DateTime(2001, 2, 3, 4, 5, 6)
                && row.CreationDate.Kind == DateTimeKind.Unspecified, "creation-date-no-utc-conversion");
            Require(row.Flags == int.MinValue && row.AccountFlags == int.MaxValue && row.AllowedCharacters == int.MinValue
                && row.Expansions == -1 && row.GmLevel == int.MaxValue, "signed-bitfields-preserved");
            row.Username = "local-copy"; row.PasswordHash = "local-copy";
            Require(dao.LoadByUsername("MixedUser").Username == "MixedUser"
                && dao.LoadForAuthentication("MixedUser").PasswordHash != "local-copy", "read-snapshots-detached");
            GameAccountAuthenticationData auth = dao.LoadForAuthentication("MixedUser");
            auth.PasswordHash = "local-auth";
            Require(dao.LoadForAuthentication("MixedUser").PasswordHash != "local-auth", "authentication-copy-detached");
        }

        private static void CreateChecks(IAccountDao dao)
        {
            DateTime before = DateTime.Now.AddSeconds(-1);
            NewGameAccountData command = NewAccount("Created");
            command.Email = "quoted'@example.test";
            command.FirstName = "F'\\irst";
            command.LastName = "L;--ast";
            command.AllowedCharacters = 6; command.Expansions = 127;
            command.Flags = 7; command.AccountFlags = 9; command.GmLevel = 3;
            string hash = PasswordHash.CreateHash("fixture-password-only");
            command.PasswordHash = hash;
            Require(dao.CreateGameAccount(command) == 1, "insert-returns-row-count-not-identity");
            DateTime after = DateTime.Now.AddSeconds(1);
            GameAccountData created = dao.LoadByUsername("Created");
            Require(created.AccountId > 41 && created.Email == command.Email && created.FirstName == command.FirstName
                && created.LastName == command.LastName && created.AllowedCharacters == 6 && created.Flags == 7
                && created.AccountFlags == 9 && created.Expansions == 127 && created.GmLevel == 3,
                "all-creation-fields-roundtrip");
            Require(created.CreationDate >= before && created.CreationDate <= after
                && created.CreationDate.Kind == DateTimeKind.Unspecified, "creation-uses-local-clock");
            Require(created.PasswordHash == hash && PasswordHash.ValidatePassword("fixture-password-only", created.PasswordHash)
                && !PasswordHash.ValidatePassword("wrong-fixture-password", created.PasswordHash),
                "existing-password-hash-unchanged-and-compatible");
            Require(dao.CreateGameAccount(NewAccount("")) == 1 && dao.UsernameExists(""), "empty-name-is-schema-data");
            Require(dao.CreateGameAccount(NewAccount(" space ")) == 1
                && dao.LoadByUsername("space") == null, "no-leading-space-normalization");
            const string literal = "q'\\;--";
            Require(dao.CreateGameAccount(NewAccount(literal)) == 1 && dao.LoadByUsername(literal).Username == literal,
                "sql-punctuation-stays-parameter-data");
            Require(dao.CountRegisteredAccounts() == 5, "multiple-account-count");
            GameAccountData zeroValues = dao.LoadByUsername("");
            Require(zeroValues.AllowedCharacters == 0 && zeroValues.Flags == 0 && zeroValues.AccountFlags == 0
                && zeroValues.Expansions == 0 && zeroValues.GmLevel == 0 && zeroValues.Email == ""
                && zeroValues.FirstName == "" && zeroValues.LastName == "", "persisted-explicit-zero-and-empty-values-no-defaults");
            Require(dao.CreateGameAccount(NewAccount("@Username")) == 1
                && dao.LoadByUsername("@Username").Username == "@Username", "parameter-like-name-roundtrip");
            Require(dao.ChangePassword("@Username", "literal-parameter-name-hash") == 1
                && dao.SetExpansions("@Username", 5) == 1
                && dao.LoadByUsername("@Username").PasswordHash == "literal-parameter-name-hash"
                && dao.LoadByUsername("@Username").Expansions == 5, "parameter-like-name-targeted-updates");
            long count = dao.CountRegisteredAccounts();
            Expect<MySqlException>(() => dao.CreateGameAccount(NewAccount("created")), "case-equivalent-duplicate-rejected");
            Require(dao.CountRegisteredAccounts() == count, "duplicate-insert-no-partial-row");
            NewGameAccountData nullName = NewAccount(null);
            Expect<MySqlException>(() => dao.CreateGameAccount(nullName), "not-null-name-constraint");
            NewGameAccountData nullHash = NewAccount("NullHash"); nullHash.PasswordHash = null;
            Expect<MySqlException>(() => dao.CreateGameAccount(nullHash), "not-null-hash-constraint");
            NewGameAccountData oversized = NewAccount(new string('x', 33));
            Expect<MySqlException>(() => dao.CreateGameAccount(oversized), "username-length-constraint");
            Require(dao.CountRegisteredAccounts() == count && dao.LoadByUsername("NullHash") == null,
                "failed-inserts-not-empty-success");
            var legacyCreate = NewAccount("LegacyCreate");
            WithLegacy(application, legacy =>
            {
                DBLoginData old = ToLegacy(legacyCreate);
                old.CreationDate = new DateTime(1000, 1, 1);
                LoginDataDao.WriteLoginData(old);
                DBLoginData saved = legacy.GetByUsername(old.Username);
                Require(saved.Id > 0 && old.Id == 0 && saved.CreationDate.Year != 1000,
                    "legacy-insert-does-not-copy-id-or-use-input-clock");
                Require(Equal(saved, dao.LoadByUsername(old.Username)), "actual-legacy-write-new-read-parity");
            });
            using (MySqlConnection connection = Open(rootConnection))
            {
                foreach (DateTime date in new[] { new DateTime(1000,1,1), new DateTime(9999,12,31,23,59,59) })
                {
                    connection.Execute("UPDATE login SET CreationDate=@date WHERE Username=@name",
                        new { date, name = "LegacyCreate" });
                    Require(dao.LoadByUsername("LegacyCreate").CreationDate == date, "datetime-schema-boundary-" + date.Year);
                }
            }
        }

        private static void ResolutionChecks(IAccountDao dao)
        {
            using (MySqlConnection connection = Open(rootConnection))
            {
                foreach (var pair in new[] { new { Id=101, Name="MixedUser" }, new { Id=102, Name="" },
                    new { Id=103, Name="orphan" }, new { Id=104, Name=" Created" }, new { Id=105, Name="created" } })
                    connection.Execute("INSERT INTO characters (Id,Username,Name,FirstName,LastName,playfield,X,Y,Z,"
                        + "HeadingX,HeadingY,HeadingZ,HeadingW) VALUES (@Id,@Name,@character,'','',1,0,0,0,0,0,0,1)",
                        new { pair.Id, pair.Name, character = "AccountFixture" + pair.Id });
                Expect<MySqlException>(() => connection.Execute("UPDATE characters SET Username=NULL WHERE Id=101"),
                    "real-character-null-username-constraint-retained");
                Require(connection.Query<string>("SELECT Username FROM characters WHERE Id=101").Single() == "MixedUser",
                    "failed-null-character-owner-write-preserves-row");
            }
            foreach (int id in new[] { -1, 0, 999 })
            {
                GameAccountLookupResult missing = dao.LoadByCharacterId(id);
                Require(missing.Status == GameAccountLookupStatus.CharacterNotFound && missing.CharacterUsername == null
                    && missing.Account == null, "missing-character-distinct-" + id);
            }
            GameAccountLookupResult empty = dao.LoadByCharacterId(102);
            Require(empty.Status == GameAccountLookupStatus.CharacterUsernameMissing
                && empty.CharacterUsername == "" && empty.Account == null, "empty-character-owner-not-empty-named-account");
            foreach (int id in new[] {103,104})
            {
                GameAccountLookupResult missingAccount = dao.LoadByCharacterId(id);
                Require(missingAccount.Status == GameAccountLookupStatus.AccountNotFound && missingAccount.Account == null
                    && !string.IsNullOrEmpty(missingAccount.CharacterUsername), "missing-account-distinct-" + id);
            }
            WithLegacy(application, legacy =>
            {
                foreach (int id in new[] {101,102,103,104,105,999})
                    Require(Equal(legacy.GetByCharacterId(id), dao.LoadByCharacterId(id).Account), "character-resolution-parity-" + id);
            });
            GameAccountLookupResult found = dao.LoadByCharacterId(105);
            Require(found.Status == GameAccountLookupStatus.Found && found.CharacterUsername == "created"
                && found.Account.Username == "Created", "owner-spelling-distinct-from-stored-account-spelling");
            var observed = new FaultConnection(application);
            GameAccountLookupResult observedResult = new MySqlAccountDao(() => observed).LoadByCharacterId(101);
            Require(observedResult.Status == GameAccountLookupStatus.Found && observed.CommandCount == 2
                && observed.BeginCount == 0 && observed.Disposed, "two-reads-one-owned-connection-no-transaction");
        }

        private static void MutationChecks(string connectionString, bool affected)
        {
            IAccountDao dao = NewDao(connectionString);
            string name = affected ? "ChangedMode" : "MatchedMode";
            Require(dao.CreateGameAccount(NewAccount(name)) == 1, "mode-fixture-created");
            GameAccountData untouched = dao.LoadByUsername("Created");
            Require(dao.ChangePassword(name, "new'\\hash;--") == 1, "password-changed-row");
            var passwordSql = new FaultConnection(connectionString);
            new MySqlAccountDao(() => passwordSql).ChangePassword(name, "new'\\hash;--");
            Require(passwordSql.LastSql.EndsWith("WHERE Username=@Username LIMIT 1", StringComparison.Ordinal)
                && passwordSql.LastParameterNames.SequenceEqual(new[] {"PasswordHash", "Username"}),
                "actual-password-limit-one-and-parameter-binding");
            var expansionSql = new FaultConnection(connectionString);
            new MySqlAccountDao(() => expansionSql).SetExpansions(name, 0);
            Require(expansionSql.LastSql == "UPDATE login SET Expansions=@Expansions WHERE Username=@Username"
                && expansionSql.LastParameterNames.SequenceEqual(new[] {"Expansions", "Username"}),
                "actual-expansion-scope-and-parameter-binding");
            Require(dao.LoadByUsername(name).PasswordHash == "new'\\hash;--", "password-exact-bytes");
            Require(dao.ChangePassword(name, "new'\\hash;--") == (affected ? 0 : 1), "same-password-provider-row-count");
            Require(dao.ChangePassword("not-present", "x") == 0 && dao.ChangePassword(null, "x") == 0,
                "missing-password-target-zero");
            Require(dao.SetExpansions(name, int.MinValue) == 1
                && dao.LoadByUsername(name).Expansions == int.MinValue, "expansion-signed-minimum");
            Require(dao.SetExpansions(name, int.MinValue) == (affected ? 0 : 1), "same-expansions-provider-row-count");
            Require(dao.SetExpansions(name, int.MaxValue) == 1, "expansion-signed-maximum");
            Require(dao.SetExpansions("not-present", 0) == 0 && dao.SetExpansions(null, 0) == 0,
                "missing-expansions-target-zero");
            Require(dao.ChangePassword("", "") >= 0 && dao.LoadByUsername("").PasswordHash == "", "empty-name-hash-mutation");
            Expect<MySqlException>(() => dao.ChangePassword(name, null), "null-password-failure-propagates");
            Require(dao.LoadByUsername(name).PasswordHash == "new'\\hash;--", "constraint-failed-update-atomic");
            WithLegacy(connectionString, legacy =>
            {
                DBLoginData old = legacy.GetByUsername(name);
                old.Password = "legacy-update";
                Require(LoginDataDao.WriteNewPassword(old) == 1, "legacy-password-changed-row");
                Require(dao.LoadByUsername(name).PasswordHash == "legacy-update", "legacy-password-update-parity");
                Require(LoginDataDao.WriteNewPassword(old) == (affected ? 0 : 1), "legacy-password-same-count-parity");
                LoginDataDao.SetExpansions(name, -17);
                Require(dao.LoadByUsername(name).Expansions == -17, "legacy-expansion-write-parity");
            });
            GameAccountData still = dao.LoadByUsername("Created");
            Require(still.PasswordHash == untouched.PasswordHash && still.Expansions == untouched.Expansions
                && still.GmLevel == untouched.GmLevel && still.Email == untouched.Email, "unrelated-account-preserved");
        }

        private static void LegacyGmChecks(string connectionString, bool affected)
        {
            using (MySqlConnection connection = Open(rootConnection))
                connection.Execute("UPDATE login SET GM=0");
            long total = NewDao(application).CountRegisteredAccounts();
            Require(total >= 2, "legacy-gm-characterization-at-least-two-rows");
            foreach (string unusedUsername in new string[] { "Created", "does-not-exist", null })
            {
                var observed = new FaultConnection(connectionString);
                Connector.TestConnectionFactory = () => observed;
                int level = unusedUsername == "Created" ? 17 : 18;
                try { LoginDataDao.SetGM(unusedUsername, level); }
                finally { Connector.TestConnectionFactory = null; }
                Require(observed.LastSql == "UPDATE login SET GM=@gm"
                    && observed.LastParameterNames.SequenceEqual(new[] {"gm"}), "actual-legacy-gm-username-unused-" + checks);
                long expected = affected && unusedUsername == null ? 0 : total;
                Require(observed.LastAffected == expected, "actual-legacy-gm-affected-rows-" + checks);
                long rowsAtLevel;
                using (MySqlConnection connection = Open(rootConnection))
                    rowsAtLevel = connection.Query<long>("SELECT COUNT(*) FROM login WHERE GM=@level", new {level}).Single();
                Require(rowsAtLevel == total, "actual-legacy-gm-all-rows-observed-" + checks);
                Console.WriteLine("LEGACY_SETGM_OBSERVATION mode=" + (affected ? "changed" : "matched")
                    + " suppliedNameCase=" + (unusedUsername == null ? "null" : unusedUsername == "Created" ? "existing" : "missing")
                    + " totalRows=" + total + " affectedRows=" + observed.LastAffected + " rowsAtLevel=" + rowsAtLevel);
                Require(observed.BeginCount == 0 && observed.Disposed, "legacy-gm-autocommit-owned-connection-" + checks);
            }
            var failed = new FaultConnection(connectionString) { FailurePoint = "execute" };
            Connector.TestConnectionFactory = () => failed;
            int logs = Utility.LogUtil.ErrorCount;
            try { LoginDataDao.SetGM("ignored", 19); }
            finally { Connector.TestConnectionFactory = null; }
            Require(Utility.LogUtil.ErrorCount == logs + 1 && failed.Disposed,
                "legacy-gm-swallows-provider-failure-no-new-gm-api");
        }

        private static void ConcurrencyChecks(IAccountDao dao)
        {
            long before = dao.CountRegisteredAccounts();
            Func<bool> create = () =>
            {
                try { dao.CreateGameAccount(NewAccount("Concurrent")); return true; }
                catch (MySqlException exception) { if (exception.Number == 1062) return false; throw; }
            };
            Task<bool> one = Task.Run(create);
            Task<bool> two = Task.Run(create);
            Task.WaitAll(one, two);
            Require((one.Result ? 1 : 0) + (two.Result ? 1 : 0) == 1
                && dao.CountRegisteredAccounts() == before + 1, "unique-name-concurrent-create-one-winner");
            Task<int> password = Task.Run(() => dao.ChangePassword("Concurrent", "concurrent-password"));
            Task<int> expansion = Task.Run(() => dao.SetExpansions("Concurrent", 31));
            Task.WaitAll(password, expansion);
            Require(password.Result == 1 && expansion.Result == 1
                && dao.LoadByUsername("Concurrent").PasswordHash == "concurrent-password"
                && dao.LoadByUsername("Concurrent").Expansions == 31, "independent-updates-no-full-row-overwrite");
            Task<int> a = Task.Run(() => dao.ChangePassword("Concurrent", "last-a"));
            Task<int> b = Task.Run(() => dao.ChangePassword("Concurrent", "last-b"));
            Task.WaitAll(a,b);
            Require(new[] {"last-a","last-b"}.Contains(dao.LoadByUsername("Concurrent").PasswordHash),
                "concurrent-password-last-writer-complete-value");
        }

        private static NewGameAccountData NewAccount(string name)
        {
            return new NewGameAccountData { Username = name, PasswordHash = "opaque-fixture", Email = "",
                FirstName = "", LastName = "", AllowedCharacters = 0, Flags = 0, AccountFlags = 0,
                Expansions = 0, GmLevel = 0 };
        }

        private static DBLoginData ToLegacy(NewGameAccountData data)
        {
            return new DBLoginData { Username=data.Username, Password=data.PasswordHash, Email=data.Email,
                FirstName=data.FirstName, LastName=data.LastName, AllowedCharacters=data.AllowedCharacters,
                Flags=data.Flags, AccountFlags=data.AccountFlags, Expansions=data.Expansions, GM=data.GmLevel };
        }

        private static bool Equal(DBLoginData old, GameAccountData current)
        {
            if (old == null || current == null) return old == null && current == null;
            return old.Id == current.AccountId && old.CreationDate == current.CreationDate && old.Email == current.Email
                && old.FirstName == current.FirstName && old.LastName == current.LastName && old.Username == current.Username
                && old.Password == current.PasswordHash && old.AllowedCharacters == current.AllowedCharacters
                && old.Flags == current.Flags && old.AccountFlags == current.AccountFlags && old.Expansions == current.Expansions
                && old.GM == current.GmLevel;
        }

        private static bool AuthEqual(DBLoginData old, GameAccountAuthenticationData current)
        {
            if (old == null || current == null) return old == null && current == null;
            return old.Id == current.AccountId && old.Username == current.Username && old.Password == current.PasswordHash
                && old.AllowedCharacters == current.AllowedCharacters && old.Flags == current.Flags
                && old.AccountFlags == current.AccountFlags && old.Expansions == current.Expansions && old.GM == current.GmLevel;
        }

        private static void WithLegacy(string connectionString, Action<LoginDataDao> operation)
        {
            Connector.TestConnectionFactory = () => new MySqlConnection(connectionString);
            try { operation(LoginDataDao.Instance); }
            finally { Connector.TestConnectionFactory = null; }
        }

        private static MySqlConnection Open(string connectionString)
        {
            var connection = new MySqlConnection(connectionString);
            try { connection.Open(); return connection; }
            catch { connection.Dispose(); throw; }
        }

        private static void Require(bool value, string name)
        {
            if (!value) throw new CheckFailure(category + "/" + name);
            checks++;
            Console.WriteLine("PASS [" + category + "] " + name);
        }

        private static T Expect<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T exception) { Require(true, name); return exception; }
            throw new CheckFailure(category + "/" + name + "-expected-" + typeof(T).Name);
        }

        private sealed class CheckFailure : Exception { internal CheckFailure(string message) : base(message) {} }
        private sealed class InjectedFailure : Exception {}
    }
}
