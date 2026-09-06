namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.Characters;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces.Persistence.Characters;
    using Dapper;
    using MySqlConnector;

    internal static partial class Program
    {
        private const string DatabaseName = "aorebirth_character_dao_validation";
        private static int checks;
        private static string category;
        private static string application;
        private static string rootConnection;

        private static int Main(string[] args)
        {
            if (args.Length != 1 || args[0] != "--run-disposable"
                || Environment.GetEnvironmentVariable("AO_REBIRTH_ALLOW_DISPOSABLE_CHARACTER_DAO_VALIDATION") != "1")
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
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "AORebirth", "Libraries",
                        "Source", "AORebirth.Database", "SqlTables", "characters.sql");
                    connection.Execute(File.ReadAllText(path));
                    Require(connection.Query<string>("SELECT DATABASE()").Single() == DatabaseName, "fixture-database-identity");
                    string collation = connection.Query<string>("SELECT COLLATION_NAME FROM information_schema.COLUMNS "
                        + "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND COLUMN_NAME='Name'").Single();
                    Console.WriteLine("CHARACTER_DAO_FIXTURE_NAME_COLLATION=" + collation);
                    Require(collation == "latin1_swedish_ci", "fixture-current-name-collation");
                    Require(connection.Query<int>("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE "
                        + "TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND INDEX_NAME='PRIMARY' AND COLUMN_NAME='Id'").Single()==1,
                        "fixture-primary-key-retained");
                }
                category = "contract"; ContractChecks();
                category = "directory"; DirectoryChecks();
                foreach (bool affected in new[] { false, true })
                {
                    category = affected ? "changed-rows" : "matched-rows";
                    OnlineChecks(Mode(affected), affected);
                }
                category = "stale"; StaleChecks();
                category = "ownership"; OwnershipChecks();
                category = "faults"; FailureChecks();
                category = "uncertain"; UncertainChecks();
                category = "concurrency"; ConcurrencyChecks();
                category = "synthetic-defensive"; SyntheticChecks();
                category = "legacy-offline"; OfflineChecks();
                Console.WriteLine("CHARACTER_DAO_TEST_MODE=ISOLATED_PRODUCTION_AND_LEGACY_SOURCES");
                Console.WriteLine("CHARACTER_DAO_CHECKS=" + checks.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("CHARACTER_DAO_MYSQL_INTEGRATION=PASS");
                result = 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("CHARACTER_DAO_MYSQL_INTEGRATION=FAIL");
                Console.Error.WriteLine("ERROR_TYPE=" + exception.GetType().Name);
                if (exception is CheckFailure) Console.Error.WriteLine("FAILED_CASE=" + exception.Message);
                if (exception is MySqlException) Console.Error.WriteLine("MYSQL_ERROR_NUMBER=" + ((MySqlException)exception).Number);
            }
            finally
            {
                Connector.TestConnectionFactory = null;
                if (fixture != null)
                {
                    try { fixture.Dispose(); }
                    catch { Console.Error.WriteLine("CHARACTER_DAO_DISPOSABLE_CLEANUP=FAIL"); result=1; }
                }
            }
            return result;
        }

        private static MySqlCharacterDao Dao(string connection = null)
        {
            return new MySqlCharacterDao(() => new MySqlConnection(connection ?? application));
        }

        private static string Mode(bool affected)
        {
            return new MySqlConnectionStringBuilder(application) { UseAffectedRows=affected }.ConnectionString;
        }

        private static void ContractChecks()
        {
            Require(typeof(ICharacterDao).GetMethods().Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(new[]
                { "IsOwnedByAccount", "ListForAccount", "ListLoggedIn", "LoadById", "LoadByName", "MarkOffline", "MarkOnline", "RecoverStaleOnline" }),
                "exact-eight-operation-contract");
            Require(typeof(CharacterDirectoryData).IsSealed && typeof(StaleOnlineCharacterData).IsSealed
                && typeof(StaleOnlineRecoveryData).IsSealed, "sealed-neutral-data");
            Require(typeof(CharacterDirectoryData).GetProperties().Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(
                new[] { "AccountUsername", "CharacterId", "FirstName", "LastName", "Name", "Online", "Playfield" }),
                "exact-seven-directory-fields");
            Require(typeof(CharacterDirectoryData).GetProperty("Online").PropertyType == typeof(int?), "nullable-online-factual-distinction");
            Require(typeof(StaleOnlineRecoveryData).GetProperties().All(x=>x.SetMethod == null || !x.SetMethod.IsPublic)
                && typeof(StaleOnlineCharacterData).GetProperties().All(x=>x.SetMethod==null || !x.SetMethod.IsPublic),
                "immutable-stale-public-properties");
            int acquisitions=0;
            Connector.TestConnectionFactory=()=>{ acquisitions++; throw new InjectedFailure(); };
            try
            {
                Require(DatabaseDaoFactory.CreateCharacterDao() is ICharacterDao, "factory-returns-character-interface");
                Require(DatabaseDaoFactory.CreateAccountDao()!=null && DatabaseDaoFactory.CreateMissionDao()!=null,
                    "existing-factories-preserved");
                Require(acquisitions==0, "all-factories-lazy-no-sql");
            }
            finally { Connector.TestConnectionFactory=null; }
            Expect<ArgumentNullException>(()=>new MySqlCharacterDao(null), "null-injected-factory-rejected");
            var configured = new UnsupportedConnection();
            Connector.TestConnectionFactory=()=>configured;
            try { Expect<NotSupportedException>(()=>DatabaseDaoFactory.CreateCharacterDao().LoadById(1), "unsupported-default-provider-rejected"); }
            finally { Connector.TestConnectionFactory=null; }
            Require(configured.Disposed && configured.CommandCount==0, "unsupported-provider-disposed-before-sql");
        }

        private static void DirectoryChecks()
        {
            Reset();
            MySqlCharacterDao dao=Dao();
            WithLegacy(()=> {
                Require(dao.LoadById(100)==null && CharacterDao.Instance.Get(100)==null, "empty-id-parity");
                Require(dao.LoadByName("missing")==null && CharacterDao.Instance.GetByCharName("missing")==null, "empty-name-parity");
                Require(dao.ListForAccount("none").Count==0 && !CharacterDao.Instance.GetAllForUser("none").Any(), "empty-account-parity");
                Require(dao.ListLoggedIn().Count==0 && !CharacterDao.Instance.GetLoggedInCharacters().Any(), "empty-online-parity");
                Require(CharacterDao.Instance.GetCharacterNameById(100)==string.Empty && CharacterDao.Instance.IsOnline(100)==0,
                    "legacy-missing-name-empty-online-zero");
            });
            Seed(101,"OwnerA","DisplayOne",0);
            Seed(102,"OwnerA","DisplayTwo",1);
            Seed(103,"OwnerB","Other",7);
            Seed(104,"OwnerB","Nullable",null);
            Seed(105,"","","",0);
            Seed(106,"OwnerA","Duplicate",1);
            Seed(107,"OwnerB","Duplicate",0);
            // Canonical schema has no UNIQUE Name index: physical duplicate names are valid.
            Require(Sql(c=>c.Query<int>("SELECT COUNT(*) FROM characters WHERE Name=@Name",new{Name="Duplicate"}).Single())==2,
                "real-schema-duplicate-names-permitted");
            CharacterDirectoryData first=dao.LoadById(101);
            Require(first.CharacterId==101 && first.AccountUsername=="OwnerA" && first.Name=="DisplayOne"
                && first.FirstName=="First101" && first.LastName=="Last101" && first.Playfield==127 && first.Online==0,
                "all-seven-directory-fields-map");
            Require(dao.LoadById(102).Online==1 && dao.LoadById(103).Online==7 && dao.LoadById(104).Online==null,
                "online-zero-one-other-null-distinguished");
            Require(dao.LoadById(-1)==null && dao.LoadById(int.MaxValue)==null && dao.LoadById(0)==null,
                "missing-boundary-ids-not-offline-record");
            Require(dao.LoadByName(null)==null && dao.LoadByName("absent")==null, "null-missing-name-results");
            Require(dao.LoadByName("").CharacterId==105 && dao.LoadById(105).FirstName=="" && dao.LoadById(105).LastName=="",
                "empty-display-and-profile-names-permitted");
            Require(dao.LoadByName("displayone").CharacterId==101 && dao.LoadByName("DisplayOne   ").CharacterId==101,
                "actual-name-case-and-trailing-space-collation");
            Require(dao.LoadByName(" DisplayOne")==null, "leading-name-space-not-trimmed");
            Require(new[]{106,107}.Contains(dao.LoadByName("Duplicate").CharacterId),
                "duplicate-name-first-row-belongs-to-match-set-no-universal-order");
            Require(dao.ListForAccount("ownera").Count==3 && dao.ListForAccount("OwnerA ").Count==3,
                "account-case-and-trailing-space-collation");
            Require(dao.ListForAccount(" OwnerA").Count==0 && dao.ListForAccount(null).Count==0,
                "account-leading-space-and-null-unchanged");
            Require(dao.ListForAccount("").Single().CharacterId==105, "empty-account-name-is-data");
            Require(dao.ListForAccount("OwnerA").All(x=>x.AccountUsername=="OwnerA"), "multiple-list-no-cross-account");
            Require(dao.ListForAccount("OwnerB").Count==3, "exact-other-account-filter");
            Require(dao.ListLoggedIn().Select(x=>x.CharacterId).OrderBy(x=>x).SequenceEqual(new[]{102,106}),
                "logged-in-filter-exact-one-not-any-nonzero");
            Require(dao.IsOwnedByAccount("OwnerA",101) && !dao.IsOwnedByAccount("OwnerB",101)
                && !dao.IsOwnedByAccount("OwnerA",999) && !dao.IsOwnedByAccount(null,101), "ownership-correct-wrong-missing-null");
            Require(dao.IsOwnedByAccount("ownera ",101) && dao.IsOwnedByAccount("",105), "ownership-column-collation-and-empty");
            Require(!dao.IsOwnedByAccount("OwnerA",uint.MaxValue) && !dao.IsOwnedByAccount("OwnerA",0),
                "ownership-unsigned-id-boundaries");
            foreach (string input in new[]{"DisplayOne","Duplicate","displayone","DisplayOne ","","missing",null})
                WithLegacy(()=> {
                    CharacterDirectoryData row=dao.LoadByName(input);
                    DBCharacter legacy=CharacterDao.Instance.GetByCharName(input);
                    Require((row==null)==(legacy==null), "legacy-name-presence-parity-"+InputTag(input));
                    Require((row!=null)==CharacterDao.Instance.ExistsByName(input), "name-existence-derived-parity-"+InputTag(input));
                    if(input!="Duplicate") Require(Snapshot(row)==Snapshot(legacy), "legacy-name-projection-parity-"+InputTag(input));
                });
            foreach(string owner in new[]{"OwnerA","OwnerB","",null,"missing"})
                WithLegacy(()=>Require(Normalize(dao.ListForAccount(owner))==Normalize(CharacterDao.Instance.GetAllForUser(owner).Select(Map)),
                    "legacy-account-normalized-parity-"+InputTag(owner)));
            WithLegacy(()=> {
                Require(Snapshot(first)==Snapshot(CharacterDao.Instance.Get(101)), "legacy-id-full-projection-parity");
                Require(CharacterDao.Instance.IsOnline(104)==0 && dao.LoadById(104).Online==null, "legacy-null-online-collapses-to-zero");
                Require(Normalize(dao.ListLoggedIn())==Normalize(CharacterDao.Instance.GetLoggedInCharacters().Select(Map)),
                    "legacy-online-directory-parity");
                Require(CharacterDao.Instance.IsCharacterOnAccount("OwnerA",101)==dao.IsOwnedByAccount("OwnerA",101),
                    "legacy-exact-one-ownership-parity");
            });
            int inputId=120;
            foreach(string name in new[]{"q'\\;%_","@Name","?name","%","_"})
            {
                int id=inputId++;
                Seed(id,name,name,0);
                Require(dao.LoadByName(name).CharacterId==id && dao.ListForAccount(name).Single().CharacterId==id,
                    "literal-punctuation-name-account-"+id);
                Require(dao.IsOwnedByAccount(name,unchecked((uint)id)), "literal-punctuation-ownership-"+id);
                Require(dao.MarkOnline(id)==1 && dao.LoadByName(name).Online==1, "literal-roundtrip-online-"+id);
            }
            Require(dao.LoadByName("' OR 1=1;--")==null && dao.ListForAccount("' OR 1=1;--").Count==0, "injection-not-executed");
            IList<CharacterDirectoryData> detached=dao.ListForAccount("OwnerA");
            Sql(c=>c.Execute("UPDATE characters SET Name='Changed' WHERE Id=101"));
            Require(detached.Single(x=>x.CharacterId==101).Name=="DisplayOne", "buffered-list-detached-after-owned-connection-disposal");
            var observed=new ObservedConnection(application);
            new MySqlCharacterDao(()=>observed).ListForAccount("OwnerA");
            Require(observed.Commands.All(x=>!x.Sql.Contains("ORDER BY",StringComparison.OrdinalIgnoreCase)), "ordinary-list-order-unspecified");
            Expect<MySqlException>(()=>Sql(c=>c.Execute("INSERT INTO characters SELECT * FROM characters WHERE Id=101")),
                "canonical-id-duplicates-rejected");
            Expect<MySqlException>(()=>Sql(c=>c.Execute("UPDATE characters SET Name=NULL WHERE Id=101")),
                "canonical-null-display-name-rejected");
            Expect<MySqlException>(()=>Sql(c=>c.Execute("UPDATE characters SET Username=NULL WHERE Id=101")),
                "canonical-null-owner-rejected");
            Console.WriteLine("CHARACTER_DAO_DIRECTORY_ORDER=UNSPECIFIED_NORMALIZED_PARITY");
        }

        private static void OnlineChecks(string connection,bool affected)
        {
            Reset(); Seed(101,"a","One",0); Seed(102,"b","Other",7); Seed(103,"a","Null",null);
            MySqlCharacterDao dao=Dao(connection);
            string before=UnrelatedSnapshot();
            Require(dao.MarkOnline(101)==1 && dao.LoadById(101).Online==1, "mark-existing-online");
            Require(dao.MarkOnline(101)==(affected?0:1), "same-online-provider-count");
            Require(dao.MarkOffline(101)==1 && dao.LoadById(101).Online==0, "mark-existing-offline");
            Require(dao.MarkOffline(101)==(affected?0:1), "same-offline-provider-count");
            Require(dao.MarkOnline(999)==0 && dao.MarkOffline(999)==0, "missing-online-writes-zero");
            Require(dao.MarkOnline(0)==0 && dao.MarkOffline(-1)==0, "boundary-online-writes-missing");
            Require(dao.MarkOnline(103)==1 && dao.LoadById(103).Online==1, "null-to-online-write");
            Sql(c=>c.Execute("UPDATE characters SET Online=NULL WHERE Id=103"));
            Require(dao.MarkOffline(103)==1 && dao.LoadById(103).Online==0, "null-to-offline-write");
            Require(dao.LoadById(102).Online==7 && before==UnrelatedSnapshot(), "online-writes-only-target-online-column");
            foreach(bool online in new[]{true,false})
            {
                var observed=new ObservedConnection(connection);
                var target=new MySqlCharacterDao(()=>observed);
                if(online) target.MarkOnline(101); else target.MarkOffline(101);
                Require(observed.BeginCount==1 && observed.Isolation==IsolationLevel.Unspecified && observed.CommitCount==1
                    && observed.RollbackCount==0, "owned-default-transaction-"+online);
                Require(observed.Commands.Count==1 && observed.Commands[0].BoundToTransaction
                    && observed.Commands[0].Parameters.Values.Contains(101), "online-statement-parameterized-id-"+online);
                Require(observed.Disposed && observed.TransactionDisposed && observed.CommandsDisposed==1,
                    "online-success-resources-disposed-"+online);
            }
            WithLegacy(()=> {
                CharacterDao.Instance.SetOnline(101);
                Require(dao.LoadById(101).Online==1, "actual-legacy-set-online-parity");
                CharacterDao.Instance.SetOffline(101);
                Require(dao.LoadById(101).Online==0, "actual-legacy-set-offline-parity");
            },connection);
            foreach(bool online in new[]{true,false})
            {
                Sql(c=>c.Execute("UPDATE characters SET Online=@Online WHERE Id=101",new{Online=online?1:0}));
                foreach(int id in new[]{101,999})
                {
                    var legacy=new ObservedConnection(connection);
                    Connector.TestConnectionFactory=()=>{legacy.Open();return legacy;};
                    try {if(online)CharacterDao.Instance.SetOnline(id);else CharacterDao.Instance.SetOffline(id);}
                    finally {Connector.TestConnectionFactory=null;}
                    Require(legacy.LastAffected==(id==999?0:affected?0:1),
                        "actual-legacy-raw-provider-count-"+online+"-"+id);
                    Require(legacy.BeginCount==1 && legacy.Isolation==IsolationLevel.Unspecified && legacy.CommitCount==1
                        && legacy.RollbackCount==0 && legacy.TransactionDisposed && legacy.Disposed,
                        "actual-legacy-online-owned-transaction-"+online+"-"+id);
                    Console.WriteLine("CHARACTER_DAO_LEGACY_ONLINE_OBSERVATION MODE="+(affected?"Changed":"Matched")
                        +" OP="+(online?"Online":"Offline")+" TARGET="+(id==999?"Missing":"SameValue")
                        +" AFFECTED="+legacy.LastAffected+" TRANSACTION=Owned COMMIT="+legacy.CommitCount);
                }
            }
            Console.WriteLine("CHARACTER_DAO_AFFECTED_MODE="+(affected?"Changed":"Matched")+" SAME_VALUE="+(affected?0:1)+" MISSING=0");
        }

        private static void StaleChecks()
        {
            Reset(); Seed(101,"a","Offline",0); Seed(102,"b","Null",null);
            var empty=new ObservedConnection(application);
            StaleOnlineRecoveryData none=new MySqlCharacterDao(()=>empty).RecoverStaleOnline(DatabaseName);
            Require(none.DatabaseName==DatabaseName && none.Rows.Count==0 && none.RowsUpdated==0
                && none.PostUpdateNonzeroCount==null && !none.CleanupRequired, "no-stale-neutral-result");
            Require(empty.BeginCount==1 && empty.Isolation==IsolationLevel.Serializable && empty.CommitCount==0
                && empty.RollbackCount==1 && empty.Commands.Count==2, "no-stale-read-only-rollback-no-count-or-commit");
            Require(empty.Disposed && empty.TransactionDisposed, "no-stale-resource-disposal");
            Seed(103,"a","One",1);
            StaleOnlineRecoveryData one=Dao().RecoverStaleOnline(DatabaseName);
            Require(one.Rows.Count==1 && one.Rows[0].CharacterId==103 && one.Rows[0].PreviousOnline==1
                && one.RowsUpdated==1 && one.PostUpdateNonzeroCount==0 && one.CleanupRequired, "one-stale-exact-result");
            Reset(); Seed(109,"a","Nine",-7); Seed(101,"b","One",1); Seed(105,"c","Five",32767);
            Seed(102,"d","Offline",0); Seed(103,"e","Null",null);
            string fields=UnrelatedSnapshot();
            var observed=new ObservedConnection(application);
            StaleOnlineRecoveryData rows=new MySqlCharacterDao(()=>observed).RecoverStaleOnline(DatabaseName);
            Require(rows.Rows.Select(x=>x.CharacterId).SequenceEqual(new[]{101,105,109}), "captured-stale-ascending-id-order");
            Require(rows.Rows.Select(x=>x.PreviousOnline).SequenceEqual(new[]{1,32767,-7}), "captured-previous-online-exact-values");
            Require(rows.RowsUpdated==3 && rows.PostUpdateNonzeroCount==0 && rows.CleanupRequired, "multiple-stale-exact-row-verification");
            Require(Dao().LoadById(103).Online==null && UnrelatedSnapshot()==fields, "stale-leaves-null-and-other-columns-unchanged");
            Require(observed.Isolation==IsolationLevel.Serializable && observed.CommitCount==1 && observed.RollbackCount==0,
                "stale-serializable-owned-commit");
            Require(observed.Commands.Count==4 && observed.Commands.All(x=>x.BoundToTransaction),
                "stale-all-four-statements-same-transaction");
            Require(observed.Commands[0].Sql.Contains("DATABASE()",StringComparison.OrdinalIgnoreCase)
                && observed.Commands[1].Sql.Contains("FOR UPDATE",StringComparison.OrdinalIgnoreCase)
                && observed.Commands[1].Sql.Contains("ORDER BY",StringComparison.OrdinalIgnoreCase),
                "stale-database-before-lock-read-order");
            CommandObservation update=observed.Commands.Single(x=>x.Sql.StartsWith("UPDATE",StringComparison.OrdinalIgnoreCase));
            Require(update.Sql.Contains(" IN ",StringComparison.OrdinalIgnoreCase)
                && update.Parameters.Values.Select(Convert.ToInt32).OrderBy(x=>x).SequenceEqual(new[]{101,105,109}),
                "bounded-update-uses-exact-captured-ids");
            Require(update.Sql.Contains("IS NOT NULL",StringComparison.OrdinalIgnoreCase)
                && update.Sql.Replace(" ", "").Contains("<>0",StringComparison.Ordinal), "bounded-update-rechecks-nonzero");
            Require(observed.Disposed && observed.TransactionDisposed && observed.ReadersCreated==observed.ReadersDisposed
                && observed.Commands.Count==observed.CommandsDisposed, "stale-success-all-resources-disposed");
            IList<StaleOnlineCharacterData> mutable=rows.Rows as IList<StaleOnlineCharacterData>;
            Require(mutable==null || mutable.IsReadOnly, "stale-captured-collection-read-only");
            Sql(c=>c.Execute("UPDATE characters SET Online=1 WHERE Id=101"));
            foreach(string expected in new[]{DatabaseName.ToUpperInvariant(),DatabaseName+" ","wrong",null,""})
            {
                var mismatch=new ObservedConnection(application);
                Expect<Exception>(()=>new MySqlCharacterDao(()=>mismatch).RecoverStaleOnline(expected),
                    "expected-database-refusal-"+InputTag(expected));
                Require(Dao().LoadById(101).Online==1 && mismatch.CommitCount==0
                    && !mismatch.Commands.Any(x=>x.Sql.StartsWith("UPDATE",StringComparison.OrdinalIgnoreCase)),
                    "database-refusal-before-mutation-"+InputTag(expected));
            }
            // Actual unchanged legacy store: equivalent captured rows/update/count on the same schema.
            using(var connection=new MySqlConnection(application))
            using(var legacy=new ZoneEngine.AdoNetStaleOnlineRecoveryStore(connection))
            {
                Require(legacy.DatabaseName==DatabaseName, "actual-legacy-stale-database");
                var old=legacy.ReadNonzeroRows();
                Require(old.Count==1 && old[0].CharacterId==101 && old[0].Online==1, "actual-legacy-stale-capture");
                Require(legacy.ClearRows(old.Select(x=>x.CharacterId).ToArray())==1 && legacy.CountNonzeroRows()==0,
                    "actual-legacy-stale-bounded-clear-count");
                legacy.Commit();
            }
            Require(Dao().LoadById(101).Online==0, "actual-legacy-stale-commit-durable");
        }

        private static void Reset() { Sql(c=>c.Execute("DELETE FROM characters")); }

        private static void Seed(int id,string owner,string name,int? online) { Seed(id,owner,name,null,online); }
        private static void Seed(int id,string owner,string name,string emptyProfile,int? online)
        {
            Sql(c=>c.Execute("INSERT INTO characters (Id,Username,Name,FirstName,LastName,Textures0,Textures1,Textures2,Textures3,Textures4,"
                +"Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online,BuddyList) "
                +"VALUES (@Id,@Username,@Name,@FirstName,@LastName,11,12,13,14,15,127,1.25,2.5,3.75,1,0.1,0.2,0.3,@Online,'9,8')",
                new{Id=id,Username=owner,Name=name,FirstName=emptyProfile??("First"+id),LastName=emptyProfile??("Last"+id),Online=online}));
        }
        private static T Sql<T>(Func<MySqlConnection,T> action)
        {
            using(var connection=new MySqlConnection(rootConnection)) { connection.Open(); return action(connection); }
        }
        private static string UnrelatedSnapshot()
        {
            return Sql(c=>string.Join("\n",c.Query("SELECT Id,Username,Name,FirstName,LastName,Textures0,Textures1,Textures2,Textures3,Textures4,"
                +"Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,BuddyList FROM characters ORDER BY Id")
                .Select(x=>string.Join("|",((IDictionary<string,object>)x).Values.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture))))));
        }
        private static void WithLegacy(Action action,string connection=null)
        {
            Connector.TestConnectionFactory=()=>{var c=new MySqlConnection(connection??application);c.Open();return c;};
            try {action();} finally {Connector.TestConnectionFactory=null;}
        }
        private static CharacterDirectoryData Map(DBCharacter row)
        {
            return row==null?null:new CharacterDirectoryData{CharacterId=row.Id,AccountUsername=row.Username,Name=row.Name,
                FirstName=row.FirstName,LastName=row.LastName,Playfield=row.Playfield,Online=row.Online};
        }
        private static string Snapshot(DBCharacter row) { return Snapshot(Map(row)); }
        private static string Snapshot(CharacterDirectoryData row)
        {
            // Compatibility projection only: actual null preservation has a separate assertion above.
            return row==null?"<missing>":string.Join("|",row.CharacterId,row.AccountUsername,row.Name,row.FirstName,row.LastName,row.Playfield,row.Online??0);
        }
        private static string Normalize(IEnumerable<CharacterDirectoryData> rows) {return string.Join("\n",rows.Select(Snapshot).OrderBy(x=>x,StringComparer.Ordinal));}
        private static string InputTag(string input) {return input==null?"null":input.Length==0?"empty":Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input));}
        private static void Require(bool condition,string name)
        {
            if(!condition) throw new CheckFailure(category+":"+name);
            checks++; Console.WriteLine("PASS ["+(category??"fixture")+"] "+name);
        }
        private static T Expect<T>(Action action,string name) where T:Exception
        {
            try {action();} catch(T error) {if(error is CheckFailure) throw;Require(true,name);return error;}
            throw new CheckFailure(category+":"+name+"-did-not-throw");
        }
        private sealed class CheckFailure:Exception {internal CheckFailure(string name):base(name){}}
        private sealed class InjectedFailure:Exception {}
    }
}
