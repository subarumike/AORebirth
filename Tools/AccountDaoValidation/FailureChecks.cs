namespace AORebirth.Tools.AccountDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.Accounts;
    using AORebirth.Interfaces.Persistence.Accounts;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void SuccessfulOwnershipChecks()
        {
            NewDao(application).CreateGameAccount(NewAccount("OwnershipTarget"));
            int creates = 0;
            var operations = new Dictionary<string, Action<IAccountDao>>
            {
                {"authentication", dao => dao.LoadForAuthentication("OwnershipTarget")},
                {"username", dao => dao.LoadByUsername("OwnershipTarget")},
                {"character", dao => dao.LoadByCharacterId(101)},
                {"count", dao => dao.CountRegisteredAccounts()},
                {"exists", dao => dao.UsernameExists("OwnershipTarget")},
                {"create", dao => dao.CreateGameAccount(NewAccount("OwnershipCreate" + (++creates)))},
                {"password", dao => dao.ChangePassword("OwnershipTarget", "owned-password")},
                {"expansions", dao => dao.SetExpansions("OwnershipTarget", 71)}
            };
            foreach (var operation in operations)
            {
                var connections = new List<FaultConnection>();
                var dao = new MySqlAccountDao(() =>
                {
                    var connection = new FaultConnection(application);
                    connections.Add(connection);
                    return connection;
                });
                operation.Value(dao);
                operation.Value(dao);
                Require(connections.Count == 2 && !ReferenceEquals(connections[0], connections[1]),
                    operation.Key + "-same-dao-fresh-connection-each-operation");
                Require(connections.All(x => x.Disposed && x.OpenCount == 1 && x.BeginCount == 0
                    && x.CommandCount == (operation.Key == "character" ? 2 : 1)
                    && x.CommandsDisposed == x.CommandCount && x.ReadersCreated == x.ReadersDisposed),
                    operation.Key + "-success-all-owned-resources-disposed");
            }
        }

        private static void FailureChecks()
        {
            var operations = new Dictionary<string, Action<IAccountDao>>
            {
                {"authentication", dao => dao.LoadForAuthentication("Created")},
                {"username", dao => dao.LoadByUsername("Created")},
                {"character", dao => dao.LoadByCharacterId(101)},
                {"count", dao => dao.CountRegisteredAccounts()},
                {"exists", dao => dao.UsernameExists("Created")},
                {"create", dao => dao.CreateGameAccount(NewAccount("FaultCreate"))},
                {"password", dao => dao.ChangePassword("Created", "must-not-be-written")},
                {"expansions", dao => dao.SetExpansions("Created", 999)}
            };
            foreach (var operation in operations)
            {
                var original = new InjectedFailure();
                InjectedFailure caught = Expect<InjectedFailure>(
                    () => operation.Value(new MySqlAccountDao(() => { throw original; })),
                    operation.Key + "-factory-error");
                Require(ReferenceEquals(caught, original), operation.Key + "-original-factory-error-preserved");
                foreach (string point in new[] {"open","create","execute"})
                {
                    var observed = new FaultConnection(application) {FailurePoint=point};
                    caught = Expect<InjectedFailure>(() => operation.Value(new MySqlAccountDao(() => observed)),
                        operation.Key + "-" + point + "-error");
                    Require(ReferenceEquals(caught, observed.Error) && observed.Disposed
                        && observed.BeginCount == 0, operation.Key + "-" + point + "-resources-and-no-transaction");
                    if (observed.CommandCount > 0)
                        Require(observed.CommandsDisposed == observed.CommandCount, operation.Key + "-" + point + "-command-disposed");
                }
            }
            Require(NewDao(application).LoadByUsername("FaultCreate") == null, "pre-execution-failures-no-insert");
            Expect<InvalidOperationException>(() => new MySqlAccountDao(() => null).LoadByUsername("Created"),
                "null-factory-result-is-failure-not-missing");
            foreach (string key in new[] {"authentication","username","character","count","exists"})
            {
                var observed = new FaultConnection(application) {FailurePoint="read"};
                Expect<InjectedFailure>(() => operations[key](new MySqlAccountDao(() => observed)), key + "-reader-error");
                Require(observed.Disposed && observed.ReadersDisposed == observed.ReadersCreated
                    && observed.ReadersCreated > 0 && observed.CommandsDisposed == observed.CommandCount,
                    key + "-reader-command-connection-disposed");
            }
            var secondRead = new FaultConnection(application) {FailCommandNumber=2};
            Expect<InjectedFailure>(() => new MySqlAccountDao(() => secondRead).LoadByCharacterId(101),
                "character-second-read-failure-not-account-missing");
            Require(secondRead.CommandCount == 2 && secondRead.ReadersDisposed == 1 && secondRead.Disposed,
                "character-partial-read-resources-disposed");
            var afterRow = new FaultConnection(application) {FailReadNumber=2};
            Expect<InjectedFailure>(() => new MySqlAccountDao(() => afterRow).LoadByUsername("Created"),
                "buffering-does-not-return-partial-row-on-reader-failure");
            Require(afterRow.ReadersDisposed == 1 && afterRow.Disposed, "partial-reader-resources-disposed");

            var opened = new FaultConnection(application); opened.Open();
            Require(new MySqlAccountDao(() => opened).UsernameExists("Created") && opened.OpenCount == 1
                && opened.Disposed, "already-open-owned-connection-not-reopened");
            var unsupported = new FaultConnection(application);
            Connector.TestConnectionFactory = () => unsupported;
            try
            {
                Expect<NotSupportedException>(() => DatabaseDaoFactory.CreateAccountDao().LoadByUsername("Created"),
                    "configured-unsupported-provider-rejected");
                Require(unsupported.CommandCount == 0 && unsupported.Disposed,
                    "unsupported-provider-disposed-before-account-sql");
            }
            finally { Connector.TestConnectionFactory = null; }

            // Each production mutation is one autocommit statement. An acknowledgement error
            // after execution is an unknown outcome, not evidence of a rollback.
            var afterWrite = new FaultConnection(application) {FailurePoint="after-write"};
            Expect<InjectedFailure>(() => new MySqlAccountDao(() => afterWrite)
                .ChangePassword("Concurrent", "durable-before-ack-error"), "password-lost-autocommit-ack-error");
            Require(NewDao(application).LoadByUsername("Concurrent").PasswordHash == "durable-before-ack-error"
                && afterWrite.BeginCount == 0 && afterWrite.Disposed, "lost-ack-durable-password-requires-reconciliation");
            var afterInsert = new FaultConnection(application) {FailurePoint="after-write"};
            Expect<InjectedFailure>(() => new MySqlAccountDao(() => afterInsert)
                .CreateGameAccount(NewAccount("AckInsert")), "insert-lost-autocommit-ack-error");
            Require(NewDao(application).UsernameExists("AckInsert"), "lost-ack-insert-already-durable");
            Expect<MySqlException>(() => NewDao(application).CreateGameAccount(NewAccount("AckInsert")),
                "lost-ack-insert-retry-not-a-new-row");
            var afterExpansion = new FaultConnection(application) {FailurePoint="after-write"};
            Expect<InjectedFailure>(() => new MySqlAccountDao(() => afterExpansion)
                .SetExpansions("Concurrent", -23), "expansion-lost-autocommit-ack-error");
            Require(NewDao(application).LoadByUsername("Concurrent").Expansions == -23,
                "lost-ack-expansion-already-durable");

            // Preserve characterization of old swallowed failures; do not implement that
            // ambiguity in the new contract. Runtime cutover must explicitly handle errors.
            var legacyFault = new FaultConnection(application) {FailurePoint="execute"};
            Connector.TestConnectionFactory = () => legacyFault;
            int logs = Utility.LogUtil.ErrorCount;
            try
            {
                Require(LoginDataDao.WriteNewPassword(new DBLoginData {Username="Created", Password="x"}) == 0,
                    "legacy-password-error-is-zero");
            }
            finally { Connector.TestConnectionFactory = null; }
            Require(Utility.LogUtil.ErrorCount == logs + 1 && legacyFault.Disposed,
                "legacy-password-error-sanitized-log-and-cleanup");
            var expansionFault = new FaultConnection(application) {FailurePoint="execute"};
            Connector.TestConnectionFactory = () => expansionFault;
            logs = Utility.LogUtil.ErrorCount;
            try { LoginDataDao.SetExpansions("Created", 1); }
            finally { Connector.TestConnectionFactory = null; }
            Require(Utility.LogUtil.ErrorCount == logs + 1 && expansionFault.Disposed,
                "legacy-expansions-error-swallowed-characterized");
            var insertFault = new FaultConnection(application) {FailurePoint="execute"};
            Connector.TestConnectionFactory = () => insertFault;
            try
            {
                Expect<InjectedFailure>(() => LoginDataDao.WriteLoginData(ToLegacy(NewAccount("x"))),
                    "legacy-create-provider-error-rethrows");
            }
            finally { Connector.TestConnectionFactory = null; }
            Require(insertFault.Disposed, "legacy-create-failed-connection-disposed");
            Console.WriteLine("ACCOUNT_DAO_TRANSACTION_TESTS=NO_TRANSACTION_API_SINGLE_STATEMENT_AUTOCOMMIT");
            Console.WriteLine("ACCOUNT_DAO_NULL_SCHEMA_FIELDS=NOT_NULL_CONSTRAINTS_RETAINED");
        }

        private static void SyntheticChecks()
        {
            // These deliberate invalid-schema shapes use only test readers; no production
            // schema constraint is changed or bypassed in the real-MySQL fixtures.
            var nullOwner = new FaultConnection(application) { ReaderOverride = sql => Table("Username", typeof(string), DBNull.Value).CreateDataReader() };
            GameAccountLookupResult result = new MySqlAccountDao(() => nullOwner).LoadByCharacterId(101);
            Require(result.Status == GameAccountLookupStatus.CharacterUsernameMissing
                && result.CharacterUsername == null && result.Account == null, "defensive-null-character-owner");
            var duplicateOwners = new FaultConnection(application)
                {ReaderOverride=sql => Table("Username", typeof(string), "a", "b").CreateDataReader()};
            Expect<InvalidOperationException>(() => new MySqlAccountDao(() => duplicateOwners).LoadByCharacterId(101),
                "duplicate-character-rows-rejected-by-single-or-default");
            var duplicateIds = new FaultConnection(application)
                {ReaderOverride=sql => Table("Id", typeof(int), 1, 2).CreateDataReader()};
            Require(!new MySqlAccountDao(() => duplicateIds).UsernameExists("synthetic"),
                "exactly-one-existence-not-any-match");
            var duplicateAccounts = new FaultConnection(application)
                {ReaderOverride=sql => Table("AccountId", typeof(int), 7, 8).CreateDataReader()};
            Require(new MySqlAccountDao(() => duplicateAccounts).LoadByUsername("synthetic").AccountId == 7,
                "first-matching-row-preserved-no-invented-sort");
            var duplicateAuth = new FaultConnection(application)
                {ReaderOverride=sql => Table("AccountId", typeof(int), 9, 10).CreateDataReader()};
            Require(new MySqlAccountDao(() => duplicateAuth).LoadForAuthentication("synthetic").AccountId == 9,
                "first-auth-row-preserved-no-invented-sort");
            var badNumeric = new FaultConnection(application)
                {ReaderOverride=sql => Table("AccountId", typeof(string), "not-an-integer").CreateDataReader()};
            Expect<DataException>(() => new MySqlAccountDao(() => badNumeric).LoadByUsername("synthetic"),
                "mapper-conversion-failure-not-null-account");
            Require(badNumeric.ReadersDisposed == 1 && badNumeric.Disposed, "mapping-failure-resources-disposed");
        }

        private static DataTable Table(string name, Type type, params object[] values)
        {
            var table = new DataTable();
            table.Columns.Add(name,type);
            foreach (object value in values) table.Rows.Add(value);
            return table;
        }

        // ADO instrumentation only: successful commands use actual MySqlConnector.
        private sealed class FaultConnection : IDbConnection
        {
            private readonly MySqlConnection inner;
            internal readonly InjectedFailure Error = new InjectedFailure();
            internal string FailurePoint;
            internal int FailCommandNumber;
            internal int FailReadNumber;
            internal bool Disposed;
            internal int CommandCount;
            internal int CommandsDisposed;
            internal int ReadersCreated;
            internal int ReadersDisposed;
            internal int BeginCount;
            internal int OpenCount;
            internal int LastAffected;
            internal string LastSql;
            internal string[] LastParameterNames = new string[0];
            internal Func<string,IDataReader> ReaderOverride;
            internal FaultConnection(string connectionString) { inner = new MySqlConnection(connectionString); }
            internal void Fail(string point) { if (FailurePoint == point) throw Error; }
            public string ConnectionString { get {return inner.ConnectionString;} set {inner.ConnectionString=value;} }
            public int ConnectionTimeout {get {return inner.ConnectionTimeout;}}
            public string Database {get {return inner.Database;}}
            public ConnectionState State {get {return inner.State;}}
            public void Open() { OpenCount++; Fail("open"); inner.Open(); }
            public void Close() {inner.Close();}
            public void ChangeDatabase(string name) {inner.ChangeDatabase(name);}
            public IDbTransaction BeginTransaction() {return BeginTransaction(IsolationLevel.Unspecified);}
            public IDbTransaction BeginTransaction(IsolationLevel isolation)
            {
                BeginCount++;
                throw new CheckFailure("unexpected-account-transaction");
            }
            public IDbCommand CreateCommand()
            {
                Fail("create"); CommandCount++;
                return new FaultCommand(this, inner.CreateCommand());
            }
            public void Dispose() {Disposed=true; inner.Dispose();}
        }

        private sealed class FaultCommand : IDbCommand
        {
            private readonly FaultConnection owner;
            private readonly IDbCommand inner;
            private bool disposed;
            internal FaultCommand(FaultConnection owner, IDbCommand inner) {this.owner=owner;this.inner=inner;}
            public string CommandText {get {return inner.CommandText;}set {inner.CommandText=value;}}
            public int CommandTimeout {get {return inner.CommandTimeout;}set {inner.CommandTimeout=value;}}
            public CommandType CommandType {get {return inner.CommandType;}set {inner.CommandType=value;}}
            public IDbConnection Connection {get {return owner;}set {}}
            public IDataParameterCollection Parameters {get {return inner.Parameters;}}
            public IDbTransaction Transaction {get {return inner.Transaction;}set {inner.Transaction=value;}}
            public UpdateRowSource UpdatedRowSource {get {return inner.UpdatedRowSource;}set {inner.UpdatedRowSource=value;}}
            public void Cancel() {inner.Cancel();}
            public IDbDataParameter CreateParameter() {return inner.CreateParameter();}
            public void Prepare() {inner.Prepare();}
            private void Before()
            {
                owner.LastSql=CommandText;
                owner.LastParameterNames=Parameters.Cast<IDataParameter>().Select(x=>x.ParameterName.TrimStart('@','?')).OrderBy(x=>x).ToArray();
                owner.Fail("execute");
                if (owner.FailCommandNumber == owner.CommandCount) throw owner.Error;
            }
            public int ExecuteNonQuery()
            {
                Before(); owner.LastAffected=inner.ExecuteNonQuery();
                owner.Fail("after-write"); return owner.LastAffected;
            }
            public object ExecuteScalar() {Before();return inner.ExecuteScalar();}
            public IDataReader ExecuteReader() {return ExecuteReader(CommandBehavior.Default);}
            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                Before();
                IDataReader reader=owner.ReaderOverride == null ? inner.ExecuteReader(behavior) : owner.ReaderOverride(CommandText);
                owner.ReadersCreated++;
                return new FaultReader(owner,reader);
            }
            public void Dispose()
            {
                if (!disposed) {disposed=true;owner.CommandsDisposed++;}
                inner.Dispose();
            }
        }

        private sealed class FaultReader : IDataReader
        {
            private readonly FaultConnection owner;
            private readonly IDataReader inner;
            private int readCount;
            private bool disposed;
            internal FaultReader(FaultConnection owner,IDataReader inner) {this.owner=owner;this.inner=inner;}
            public bool Read()
            {
                owner.Fail("read");
                if (++readCount == owner.FailReadNumber) throw owner.Error;
                return inner.Read();
            }
            public void Dispose() {if(!disposed){disposed=true;owner.ReadersDisposed++;} inner.Dispose();}
            public bool NextResult(){return inner.NextResult();}
            public void Close(){inner.Close();}
            public DataTable GetSchemaTable(){return inner.GetSchemaTable();}
            public int Depth{get{return inner.Depth;}}
            public bool IsClosed{get{return inner.IsClosed;}}
            public int RecordsAffected{get{return inner.RecordsAffected;}}
            public int FieldCount{get{return inner.FieldCount;}}
            public object this[int i]{get{return inner[i];}}
            public object this[string name]{get{return inner[name];}}
            public bool GetBoolean(int i){return inner.GetBoolean(i);}
            public byte GetByte(int i){return inner.GetByte(i);}
            public long GetBytes(int i,long offset,byte[] buffer,int bufferOffset,int length){return inner.GetBytes(i,offset,buffer,bufferOffset,length);}
            public char GetChar(int i){return inner.GetChar(i);}
            public long GetChars(int i,long offset,char[] buffer,int bufferOffset,int length){return inner.GetChars(i,offset,buffer,bufferOffset,length);}
            public IDataReader GetData(int i){return inner.GetData(i);}
            public string GetDataTypeName(int i){return inner.GetDataTypeName(i);}
            public DateTime GetDateTime(int i){return inner.GetDateTime(i);}
            public decimal GetDecimal(int i){return inner.GetDecimal(i);}
            public double GetDouble(int i){return inner.GetDouble(i);}
            public Type GetFieldType(int i){return inner.GetFieldType(i);}
            public float GetFloat(int i){return inner.GetFloat(i);}
            public Guid GetGuid(int i){return inner.GetGuid(i);}
            public short GetInt16(int i){return inner.GetInt16(i);}
            public int GetInt32(int i){return inner.GetInt32(i);}
            public long GetInt64(int i){return inner.GetInt64(i);}
            public string GetName(int i){return inner.GetName(i);}
            public int GetOrdinal(string name){return inner.GetOrdinal(name);}
            public string GetString(int i){return inner.GetString(i);}
            public object GetValue(int i){return inner.GetValue(i);}
            public int GetValues(object[] values){return inner.GetValues(values);}
            public bool IsDBNull(int i){return inner.IsDBNull(i);}
        }
    }
}
