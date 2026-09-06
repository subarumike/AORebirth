namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using MySqlConnector;

    internal static partial class Program
    {
        private sealed class CommandObservation
        {
            internal string Sql;
            internal Dictionary<string,object> Parameters;
            internal bool BoundToTransaction;
        }

        private sealed class ObservedConnection : IDbConnection
        {
            private readonly MySqlConnection inner;
            internal readonly InjectedFailure Error=new InjectedFailure();
            internal readonly InjectedFailure RollbackError=new InjectedFailure();
            internal readonly InjectedFailure ConnectionDisposeError=new InjectedFailure();
            internal readonly InjectedFailure TransactionDisposeError=new InjectedFailure();
            internal bool ConnectionDisposeFails;
            internal bool TransactionDisposeFails;
            internal readonly List<CommandObservation> Commands=new List<CommandObservation>();
            internal string FailurePoint;
            internal int FailCommandNumber;
            internal int FailReadNumber;
            internal string FailReadSqlContains;
            internal bool ReturnNullTransaction;
            internal bool RollbackFails;
            internal bool RollbackFailsBefore;
            internal bool Disposed;
            internal bool TransactionDisposed;
            internal int CommandsDisposed;
            internal int ReadersCreated;
            internal int ReadersDisposed;
            internal int BeginCount;
            internal int CommitCount;
            internal int RollbackCount;
            internal int OpenCount;
            internal IsolationLevel Isolation;
            internal int? OverrideAffected;
            internal long? OverrideCount;
            internal int? LastAffected;
            internal Func<string,IDataReader> ReaderOverride;
            internal Action<CommandObservation> BeforeCommand;
            internal Action<CommandObservation> AfterCommand;
            internal ObservedConnection(string value) {inner=new MySqlConnection(value);}
            internal void Fail(string point) {if(FailurePoint==point) throw Error;}
            public string ConnectionString {get{return inner.ConnectionString;}set{inner.ConnectionString=value;}}
            public int ConnectionTimeout {get{return inner.ConnectionTimeout;}}
            public string Database {get{return inner.Database;}}
            public ConnectionState State {get{return inner.State;}}
            public void Open(){OpenCount++;Fail("open");inner.Open();}
            public void Close(){inner.Close();}
            public void ChangeDatabase(string name){inner.ChangeDatabase(name);}
            public IDbTransaction BeginTransaction(){return BeginTransaction(IsolationLevel.Unspecified);}
            public IDbTransaction BeginTransaction(IsolationLevel isolation)
            {
                BeginCount++;Isolation=isolation;Fail("begin");
                if(ReturnNullTransaction)return null;
                return new ObservedTransaction(this,inner.BeginTransaction(isolation));
            }
            public IDbCommand CreateCommand(){Fail("create");return new ObservedCommand(this,inner.CreateCommand());}
            public void Dispose(){Disposed=true;inner.Dispose();if(ConnectionDisposeFails)throw ConnectionDisposeError;}
        }

        private sealed class ObservedTransaction:IDbTransaction
        {
            internal readonly IDbTransaction Inner;
            private readonly ObservedConnection owner;
            internal ObservedTransaction(ObservedConnection owner,IDbTransaction inner){this.owner=owner;Inner=inner;}
            public IDbConnection Connection {get{return owner;}}
            public IsolationLevel IsolationLevel {get{return Inner.IsolationLevel;}}
            public void Commit(){owner.CommitCount++;owner.Fail("commit-before");Inner.Commit();owner.Fail("commit-after");}
            public void Rollback()
            {
                owner.RollbackCount++;
                if(owner.RollbackFailsBefore) throw owner.RollbackError;
                Inner.Rollback();
                if(owner.RollbackFails) throw owner.RollbackError;
            }
            public void Dispose(){owner.TransactionDisposed=true;Inner.Dispose();if(owner.TransactionDisposeFails)throw owner.TransactionDisposeError;}
        }

        private sealed class ObservedCommand:IDbCommand
        {
            private readonly ObservedConnection owner;
            private readonly IDbCommand inner;
            private IDbTransaction transaction;
            private bool disposed;
            private CommandObservation observation;
            internal ObservedCommand(ObservedConnection owner,IDbCommand inner){this.owner=owner;this.inner=inner;}
            public string CommandText {get{return inner.CommandText;}set{inner.CommandText=value;}}
            public int CommandTimeout {get{return inner.CommandTimeout;}set{inner.CommandTimeout=value;}}
            public CommandType CommandType {get{return inner.CommandType;}set{inner.CommandType=value;}}
            public IDbConnection Connection {get{return owner;}set{}}
            public IDataParameterCollection Parameters {get{return inner.Parameters;}}
            public IDbTransaction Transaction
            {
                get{return transaction;}
                set{transaction=value;inner.Transaction=value is ObservedTransaction?((ObservedTransaction)value).Inner:value;}
            }
            public UpdateRowSource UpdatedRowSource {get{return inner.UpdatedRowSource;}set{inner.UpdatedRowSource=value;}}
            public void Cancel(){inner.Cancel();}
            public IDbDataParameter CreateParameter(){return inner.CreateParameter();}
            public void Prepare(){inner.Prepare();}
            private void Before()
            {
                observation=new CommandObservation{Sql=CommandText,BoundToTransaction=transaction!=null,
                    Parameters=Parameters.Cast<IDataParameter>().ToDictionary(x=>x.ParameterName.TrimStart('@','?'),x=>x.Value)};
                owner.Commands.Add(observation);
                if(owner.BeforeCommand!=null) owner.BeforeCommand(observation);
                owner.Fail("execute");
                if(owner.FailCommandNumber==owner.Commands.Count) throw owner.Error;
            }
            private void After(){if(owner.AfterCommand!=null) owner.AfterCommand(observation);}
            public int ExecuteNonQuery()
            {
                Before();int result=inner.ExecuteNonQuery();owner.LastAffected=result;After();owner.Fail("after-write");return owner.OverrideAffected??result;
            }
            public object ExecuteScalar()
            {
                Before();object result=inner.ExecuteScalar();After();
                if(owner.OverrideCount.HasValue && CommandText.Contains("COUNT(",StringComparison.OrdinalIgnoreCase))
                    return owner.OverrideCount.Value;
                return result;
            }
            public IDataReader ExecuteReader(){return ExecuteReader(CommandBehavior.Default);}
            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                Before();
                IDataReader reader=owner.ReaderOverride==null?inner.ExecuteReader(behavior):owner.ReaderOverride(CommandText);
                if(owner.OverrideCount.HasValue && CommandText.Contains("COUNT(",StringComparison.OrdinalIgnoreCase))
                {
                    reader.Dispose();reader=Table("Count",typeof(long),owner.OverrideCount.Value).CreateDataReader();
                }
                owner.ReadersCreated++;
                var observed=new ObservedReader(owner,reader,CommandText);
                try {After();return observed;} catch {observed.Dispose();throw;}
            }
            public void Dispose(){if(!disposed){disposed=true;owner.CommandsDisposed++;}inner.Dispose();}
        }

        private sealed class UnsupportedConnection:IDbConnection
        {
            internal bool Disposed;
            internal int CommandCount;
            public string ConnectionString{get;set;}
            public int ConnectionTimeout{get{return 0;}}
            public string Database{get{return "unsupported-test";}}
            public ConnectionState State{get{return ConnectionState.Closed;}}
            public void Open(){throw new CheckFailure("unsupported-open");}
            public void Close(){}
            public void ChangeDatabase(string databaseName){throw new NotSupportedException();}
            public IDbTransaction BeginTransaction(){throw new NotSupportedException();}
            public IDbTransaction BeginTransaction(IsolationLevel isolation){throw new NotSupportedException();}
            public IDbCommand CreateCommand(){CommandCount++;throw new CheckFailure("unsupported-sql");}
            public void Dispose(){Disposed=true;}
        }

        private sealed class ObservedReader : IDataReader
        {
            private readonly ObservedConnection owner;
            private readonly IDataReader inner;
            private int readCount;
            private bool disposed;
            private readonly string sql;
            internal ObservedReader(ObservedConnection owner,IDataReader inner,string sql) {this.owner=owner;this.inner=inner;this.sql=sql;}
            public bool Read()
            {
                owner.Fail("read");
                if (++readCount == owner.FailReadNumber && (owner.FailReadSqlContains==null
                    || sql.Contains(owner.FailReadSqlContains,StringComparison.OrdinalIgnoreCase))) throw owner.Error;
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
