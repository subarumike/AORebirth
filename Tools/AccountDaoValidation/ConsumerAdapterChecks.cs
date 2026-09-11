namespace AORebirth.Tools.AccountDaoValidation
{
    using System;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using Dapper;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void ConsumerAdapterChecks()
        {
            var expected = new DBLoginData
            {
                Username = "AdapterOwner", Password = "opaque:adapter/hash+value", Email = "adapter@example.test",
                FirstName = "AdapterFirst", LastName = "AdapterLast", AllowedCharacters = 7,
                Flags = int.MinValue, AccountFlags = int.MaxValue, Expansions = -17, GM = 511
            };
            WithLegacy(application, adapter =>
            {
                long before = adapter.GetRegisteredCount();
                DateTime createdAfter = DateTime.Now.AddSeconds(-1);
                LoginDataDao.WriteLoginData(expected);
                DBLoginData actual = adapter.GetByUsername(expected.Username);
                Require(adapter.GetRegisteredCount() == before + 1, "create-count-through-configured-dao");
                Require(actual.Id > 0 && actual.Username == expected.Username && actual.Password == expected.Password,
                    "id-username-and-opaque-password-preserved");
                Require(actual.Email == expected.Email && actual.FirstName == expected.FirstName
                    && actual.LastName == expected.LastName, "complete-profile-preserved");
                Require(actual.AllowedCharacters == expected.AllowedCharacters && actual.Flags == expected.Flags
                    && actual.AccountFlags == expected.AccountFlags && actual.Expansions == expected.Expansions
                    && actual.GM == expected.GM, "all-authority-and-slot-fields-preserved");
                Require(actual.CreationDate >= createdAfter && actual.CreationDate <= DateTime.Now.AddSeconds(1)
                    && actual.CreationDate.Kind == DateTimeKind.Unspecified, "creation-local-clock-preserved");
                actual.Password = "detached";
                Require(adapter.GetByUsername(expected.Username).Password == expected.Password, "read-result-detached");
                Require(adapter.Exists(expected.Username) && !adapter.Exists("AdapterMissing"), "username-availability");
                Require(adapter.GetByUsername(null) == null && adapter.GetByUsername("AdapterMissing") == null,
                    "missing-account-null-results");

                using (MySqlConnection connection = Open(rootConnection))
                {
                    foreach (var row in new[] { new { Id = 9801, Owner = expected.Username },
                        new { Id = 9802, Owner = "AdapterMissing" }, new { Id = 9803, Owner = "" } })
                        connection.Execute("INSERT INTO characters (Id,Username,Name,FirstName,LastName,playfield,X,Y,Z,"
                            + "HeadingX,HeadingY,HeadingZ,HeadingW) VALUES (@Id,@Owner,@name,'','',1,0,0,0,0,0,0,1)",
                            new { row.Id, row.Owner, name = "AdapterCharacter" + row.Id });
                }
                Require(adapter.GetByCharacterId(9801).Id == actual.Id, "character-owner-resolves-account");
                Require(adapter.GetByCharacterId(9802) == null && adapter.GetByCharacterId(9803) == null
                    && adapter.GetByCharacterId(9899) == null, "missing-character-owner-null-results");

                Require(LoginDataDao.WriteNewPassword(new DBLoginData
                    { Username = expected.Username, Password = "opaque:changed/hash" }) == 1,
                    "password-affected-count-forwarded");
                Require(adapter.GetByUsername(expected.Username).Password == "opaque:changed/hash",
                    "password-not-rehashed-by-persistence");
                Require(LoginDataDao.WriteNewPassword(new DBLoginData
                    { Username = "AdapterMissing", Password = "unused" }) == 0, "missing-password-target-zero");
                LoginDataDao.SetExpansions(expected.Username, 127);
                actual = adapter.GetByUsername(expected.Username);
                Require(actual.Expansions == 127 && actual.Password == "opaque:changed/hash" && actual.GM == 511
                    && actual.Email == expected.Email, "expansion-write-does-not-overwrite-other-fields");

                int errors = Utility.LogUtil.ErrorCount;
                Expect<MySqlException>(() => LoginDataDao.WriteLoginData(expected), "duplicate-create-rethrows");
                Require(Utility.LogUtil.ErrorCount == errors + 1 && adapter.GetRegisteredCount() == before + 1,
                    "duplicate-create-logged-no-extra-row");
                errors = Utility.LogUtil.ErrorCount;
                Require(LoginDataDao.WriteNewPassword(new DBLoginData
                    { Username = expected.Username, Password = null }) == 0, "constraint-error-password-zero");
                Require(Utility.LogUtil.ErrorCount == errors + 1
                    && adapter.GetByUsername(expected.Username).Password == "opaque:changed/hash",
                    "constraint-error-logged-no-password-change");
            });

            var failure = new InjectedFailure();
            int attempts = 0;
            Connector.TestConnectionFactory = () => { attempts++; throw failure; };
            try
            {
                int errors = Utility.LogUtil.ErrorCount;
                Exception observed = Expect<InjectedFailure>(() => LoginDataDao.WriteLoginData(expected),
                    "create-provider-failure-propagates");
                Require(ReferenceEquals(observed, failure) && attempts == 1 && Utility.LogUtil.ErrorCount == errors + 1,
                    "create-preserves-error-and-never-retries");
                errors = Utility.LogUtil.ErrorCount;
                Require(LoginDataDao.WriteNewPassword(expected) == 0 && attempts == 2
                    && Utility.LogUtil.ErrorCount == errors + 1, "password-error-logged-zero-no-fallback");
                errors = Utility.LogUtil.ErrorCount;
                LoginDataDao.SetExpansions(expected.Username, 1);
                Require(attempts == 3 && Utility.LogUtil.ErrorCount == errors + 1,
                    "expansion-error-logged-and-swallowed-no-fallback");
                errors = Utility.LogUtil.ErrorCount;
                observed = Expect<InjectedFailure>(() => LoginDataDao.Instance.GetByUsername(expected.Username),
                    "lookup-provider-failure-propagates");
                Require(ReferenceEquals(observed, failure) && attempts == 4 && Utility.LogUtil.ErrorCount == errors,
                    "lookup-does-not-mask-failure-as-missing-account");
            }
            finally { Connector.TestConnectionFactory = null; }
        }
    }
}
