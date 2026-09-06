namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using AORebirth.Database;
    using AORebirth.Database.Domain.Characters;
    using AORebirth.Interfaces.Persistence.Characters;
    using Dapper;

    internal static partial class Program
    {
        private sealed class Operation
        {
            internal string Name; internal Action<ICharacterDao> Run; internal bool Transaction; internal bool Reader;
        }
        private static Operation[] Operations()
        {
            return new[]{
                new Operation{Name="LoadById",Run=d=>d.LoadById(101),Reader=true},
                new Operation{Name="LoadByName",Run=d=>d.LoadByName("One"),Reader=true},
                new Operation{Name="ListForAccount",Run=d=>d.ListForAccount("a"),Reader=true},
                new Operation{Name="IsOwnedByAccount",Run=d=>d.IsOwnedByAccount("a",101),Reader=true},
                new Operation{Name="MarkOnline",Run=d=>d.MarkOnline(101),Transaction=true},
                new Operation{Name="MarkOffline",Run=d=>d.MarkOffline(101),Transaction=true},
                new Operation{Name="ListLoggedIn",Run=d=>d.ListLoggedIn(),Reader=true},
                new Operation{Name="RecoverStaleOnline",Run=d=>d.RecoverStaleOnline(DatabaseName),Transaction=true,Reader=true}
            };
        }
        private static void FaultFixture()
        {
            Reset();Seed(101,"a","One",1);Seed(102,"a","Two",7);Seed(103,"b","Other",0);Seed(104,"b","Null",null);
        }
        private static string OnlineSnapshot()
        {
            return Sql(c=>string.Join("|",c.Query("SELECT Id,Online FROM characters ORDER BY Id")
                .Select(x=>Convert.ToString(x.Id)+":"+(x.Online==null?"null":Convert.ToString(x.Online)))));
        }
        private static void OwnershipChecks()
        {
            foreach(Operation operation in Operations())
            {
                var owned=new List<ObservedConnection>();
                var dao=new MySqlCharacterDao(()=>{var c=new ObservedConnection(application);owned.Add(c);return c;});
                for(int index=0;index<2;index++)
                {
                    FaultFixture();operation.Run(dao);
                    ObservedConnection connection=owned.Last();
                    Require(connection.Disposed && connection.CommandsDisposed==connection.Commands.Count
                        && connection.ReadersDisposed==connection.ReadersCreated, operation.Name+"-success-resources-"+index);
                    Require(connection.OpenCount==1 && connection.BeginCount==(operation.Transaction?1:0)
                        && (!operation.Transaction || connection.TransactionDisposed), operation.Name+"-success-owned-lifetime-"+index);
                }
                Require(owned.Count==2 && !object.ReferenceEquals(owned[0],owned[1]), operation.Name+"-fresh-connections-on-same-dao");
            }
            var open=new ObservedConnection(application);open.Open();
            new MySqlCharacterDao(()=>open).LoadById(101);
            Require(open.OpenCount==1 && open.Disposed, "already-open-returned-connection-not-reopened");
            int configuredCalls=0;
            Connector.TestConnectionFactory=()=>{configuredCalls++;var c=new MySqlConnector.MySqlConnection(application);c.Open();return c;};
            try{Require(DatabaseDaoFactory.CreateCharacterDao().LoadById(101)!=null && configuredCalls==1,
                "default-factory-actual-mysql-operation");}
            finally{Connector.TestConnectionFactory=null;}
        }
        private static void FailureChecks()
        {
            foreach(Operation operation in Operations())
            {
                FaultFixture();string original=OnlineSnapshot();
                var factoryError=new InjectedFailure();
                Require(object.ReferenceEquals(Expect<InjectedFailure>(()=>operation.Run(new MySqlCharacterDao(()=>{throw factoryError;})),
                    operation.Name+"-factory-failure-propagates"),factoryError), operation.Name+"-factory-primary-identity");
                Expect<InvalidOperationException>(()=>operation.Run(new MySqlCharacterDao(()=>null)),operation.Name+"-null-returned-connection-rejected");
                if(operation.Transaction)
                {
                    var nullTransaction=new ObservedConnection(application){ReturnNullTransaction=true};
                    Expect<InvalidOperationException>(()=>operation.Run(new MySqlCharacterDao(()=>nullTransaction)),
                        operation.Name+"-null-returned-transaction-rejected");
                    Require(nullTransaction.Disposed && nullTransaction.Commands.Count==0 && nullTransaction.CommitCount==0
                        && nullTransaction.RollbackCount==0 && OnlineSnapshot()==original,
                        operation.Name+"-null-transaction-no-sql-disposed");
                }
                var points=new List<string>{"open","create","execute"};
                if(operation.Transaction)points.Add("begin");
                if(operation.Reader)points.Add("read");
                foreach(string point in points)
                {
                    var connection=new ObservedConnection(application){FailurePoint=point};
                    Exception failure=Expect<InjectedFailure>(()=>operation.Run(new MySqlCharacterDao(()=>connection)),
                        operation.Name+"-"+point+"-failure");
                    Require(object.ReferenceEquals(failure,connection.Error),operation.Name+"-"+point+"-primary-identity");
                    Require(connection.Disposed && connection.CommandsDisposed==connection.Commands.Count
                        && connection.ReadersCreated==connection.ReadersDisposed, operation.Name+"-"+point+"-resources-disposed");
                    Require(!operation.Transaction || point=="open" || point=="begin" || connection.TransactionDisposed,
                        operation.Name+"-"+point+"-transaction-disposal");
                    Require(OnlineSnapshot()==original,operation.Name+"-"+point+"-no-persisted-change");
                }
            }
            foreach(string operation in new[]{"ListForAccount","ListLoggedIn"})
            {
                FaultFixture();
                var partial=new ObservedConnection(application){FailReadNumber=2};
                Expect<InjectedFailure>(()=> {
                    var dao=new MySqlCharacterDao(()=>partial);
                    if(operation=="ListForAccount")dao.ListForAccount("a");else dao.ListLoggedIn();
                },operation+"-partial-reader-never-returns-partial-success");
                Require(partial.ReadersCreated==partial.ReadersDisposed && partial.Disposed,operation+"-partial-reader-disposal");
            }
            foreach(int command in new[]{1,2,3,4})
            {
                FaultFixture();string original=OnlineSnapshot();
                var connection=new ObservedConnection(application){FailCommandNumber=command};
                Exception failure=Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>connection).RecoverStaleOnline(DatabaseName),
                    "stale-command-"+command+"-failure");
                Require(object.ReferenceEquals(failure,connection.Error) && connection.RollbackCount==1 && connection.CommitCount==0,
                    "stale-command-"+command+"-primary-rollback");
                Require(OnlineSnapshot()==original && connection.TransactionDisposed && connection.Disposed,
                    "stale-command-"+command+"-atomic-restoration-disposal");
            }
            foreach(string kind in new[]{"after-write","row-count","post-count"})
            {
                FaultFixture();string original=OnlineSnapshot();
                var connection=new ObservedConnection(application);
                if(kind=="after-write")connection.FailurePoint="after-write";
                if(kind=="row-count")connection.OverrideAffected=1;
                if(kind=="post-count")connection.OverrideCount=1;
                Expect<Exception>(()=>new MySqlCharacterDao(()=>connection).RecoverStaleOnline(DatabaseName),"stale-"+kind+"-rejected");
                Require(OnlineSnapshot()==original && connection.RollbackCount==1 && connection.CommitCount==0,
                    "stale-"+kind+"-no-partial-cleanup");
                Require(connection.TransactionDisposed && connection.Disposed,"stale-"+kind+"-resources-disposed");
            }
            FaultFixture();string old=OnlineSnapshot();
            var readFault=new ObservedConnection(application){FailReadNumber=2,FailReadSqlContains="FOR UPDATE"};
            Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>readFault).RecoverStaleOnline(DatabaseName),"stale-partial-capture-failure");
            Require(OnlineSnapshot()==old && readFault.RollbackCount==1, "stale-partial-capture-no-update");

            foreach(bool online in new[]{true,false})
            {
                FaultFixture();
                if(online)Sql(c=>c.Execute("UPDATE characters SET Online=0 WHERE Id=101"));
                string original=OnlineSnapshot();
                var writeFault=new ObservedConnection(application){FailurePoint="after-write"};
                Expect<InjectedFailure>(()=>{var dao=new MySqlCharacterDao(()=>writeFault);
                    if(online)dao.MarkOnline(101);else dao.MarkOffline(101);},"ordinary-write-executed-before-failure-"+online);
                Require(OnlineSnapshot()==original && writeFault.RollbackCount==1 && writeFault.CommitCount==0,
                    "ordinary-write-precommit-failure-rolls-back-"+online);
            }

            foreach(bool before in new[]{false,true})
            {
                FaultFixture();string original=OnlineSnapshot();
                var connection=new ObservedConnection(application){FailurePoint="after-write",RollbackFails=!before,RollbackFailsBefore=before};
                Exception error=Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>connection).RecoverStaleOnline(DatabaseName),
                    "rollback-"+(before?"before":"after")+"-primary-thrown");
                Require(object.ReferenceEquals(error,connection.Error)
                    && object.ReferenceEquals(error.Data["CharacterDao.RollbackFailure"],connection.RollbackError),
                    "rollback-"+(before?"before":"after")+"-secondary-retained");
                Require(connection.TransactionDisposed && connection.Disposed && OnlineSnapshot()==original,
                    "rollback-"+(before?"before":"after")+"-owned-disposal-rolls-back");
            }
            Reset();Seed(101,"a","One",0);
            var emptyFailure=new ObservedConnection(application){RollbackFails=true};
            Exception emptyError=Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>emptyFailure).RecoverStaleOnline(DatabaseName),
                "empty-stale-rollback-failure-visible");
            Require(object.ReferenceEquals(emptyError,emptyFailure.RollbackError) && emptyFailure.RollbackCount==1
                && emptyFailure.CommitCount==0 && emptyFailure.TransactionDisposed && emptyFailure.Disposed,
                "empty-stale-rollback-original-not-retried");
            DisposalChecks();
        }

        private static void DisposalChecks()
        {
            foreach(string target in new[]{"connection","transaction"})
            {
                foreach(bool primary in new[]{false,true})
                {
                    FaultFixture();
                    var connection=new ObservedConnection(application){FailurePoint=primary?"execute":null,
                        ConnectionDisposeFails=target=="connection",TransactionDisposeFails=target=="transaction"};
                    Exception error=Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>connection).MarkOffline(101),
                        target+"-dispose-failure-"+primary);
                    if(primary)
                        Require(object.ReferenceEquals(error,connection.Error)
                            && object.ReferenceEquals(error.Data[target=="connection"?"CharacterDao.ConnectionDisposeFailure":"CharacterDao.TransactionDisposeFailure"],
                                target=="connection"?connection.ConnectionDisposeError:connection.TransactionDisposeError),
                            target+"-dispose-keeps-primary-secondary");
                    else
                        Require(object.ReferenceEquals(error,target=="connection"?connection.ConnectionDisposeError:connection.TransactionDisposeError)
                            && Dao().LoadById(101).Online==0,target+"-dispose-success-error-may-follow-commit");
                    Require(connection.Disposed && connection.TransactionDisposed,target+"-dispose-both-owners-attempted-"+primary);
                }
            }
            FaultFixture();
            var both=new ObservedConnection(application){ConnectionDisposeFails=true,TransactionDisposeFails=true};
            Exception bothError=Expect<InjectedFailure>(()=>new MySqlCharacterDao(()=>both).MarkOffline(101),
                "both-disposal-failures-visible-after-commit");
            Require(object.ReferenceEquals(bothError,both.TransactionDisposeError)
                && object.ReferenceEquals(bothError.Data["CharacterDao.ConnectionDisposeFailure"],both.ConnectionDisposeError)
                && both.Disposed && both.TransactionDisposed && Dao().LoadById(101).Online==0,
                "first-disposal-error-primary-second-attached-commit-durable");
        }

        private static void UncertainChecks()
        {
            foreach(string operation in new[]{"MarkOnline","MarkOffline","RecoverStaleOnline"})
            {
                foreach(bool durable in new[]{false,true})
                {
                    FaultFixture();
                    if(operation=="MarkOnline")Sql(c=>c.Execute("UPDATE characters SET Online=0 WHERE Id=101"));
                    string original=OnlineSnapshot();
                    var connection=new ObservedConnection(application){FailurePoint=durable?"commit-after":"commit-before"};
                    Exception error=Expect<InjectedFailure>(()=>{
                        var dao=new MySqlCharacterDao(()=>connection);
                        if(operation=="MarkOnline")dao.MarkOnline(101);else if(operation=="MarkOffline")dao.MarkOffline(101);
                        else dao.RecoverStaleOnline(DatabaseName);
                    },operation+"-commit-"+(durable?"lost-ack":"before-durable")+"-throws");
                    Require(object.ReferenceEquals(error,connection.Error),operation+"-commit-"+durable+"-primary-preserved");
                    Require(connection.CommitCount==1 && connection.RollbackCount==1 && connection.TransactionDisposed && connection.Disposed,
                        operation+"-commit-"+durable+"-resource-termination");
                    Require(durable?OnlineSnapshot()!=original:OnlineSnapshot()==original,
                        operation+"-commit-"+durable+"-actual-durable-reconciliation");
                    if(durable)
                    {
                        Require(error.Data["CharacterDao.RollbackFailure"] is Exception,
                            operation+"-lost-ack-rollback-failure-secondary");
                        if(operation=="RecoverStaleOnline")
                            Require(!Dao().RecoverStaleOnline(DatabaseName).CleanupRequired,"stale-lost-ack-safe-empty-reconciliation");
                        else
                        {
                            int expected=operation=="MarkOnline"?1:0;
                            Require(Dao().LoadById(101).Online==expected,operation+"-lost-ack-fresh-read-is-stable");
                            Require((operation=="MarkOnline"?Dao(Mode(true)).MarkOnline(101):Dao(Mode(true)).MarkOffline(101))==0,
                                operation+"-lost-ack-repeated-state-write-no-physical-change");
                        }
                    }
                }
            }
            Console.WriteLine("CHARACTER_DAO_ONLINE_ATOMICITY=OWNED_TRANSACTION_NOT_AUTOCOMMIT");
            Console.WriteLine("CHARACTER_DAO_COMMIT_EXCEPTION=OUTCOME_REQUIRES_RECONCILIATION");
        }

        private static void SyntheticChecks()
        {
            var duplicate=new ObservedConnection(application){ReaderOverride=sql=>Table("CharacterId",typeof(int),1,2).CreateDataReader()};
            Expect<InvalidOperationException>(()=>new MySqlCharacterDao(()=>duplicate).LoadById(1),"invalid-schema-duplicate-id-rejected");
            var ownership=new ObservedConnection(application){ReaderOverride=sql=>Table("Id",typeof(int),1,1).CreateDataReader()};
            Require(!new MySqlCharacterDao(()=>ownership).IsOwnedByAccount("a",1),"invalid-schema-duplicate-ownership-exact-one-not-any");
            var malformed=new ObservedConnection(application){ReaderOverride=sql=>Table("Online",typeof(string),"bad-int").CreateDataReader()};
            Expect<DataException>(()=>new MySqlCharacterDao(()=>malformed).LoadById(1),"invalid-reader-online-mapping-failure-not-offline");
            Require(malformed.ReadersCreated==malformed.ReadersDisposed && malformed.Disposed,"invalid-mapping-reader-disposed");
            var nullable=new ObservedConnection(application){ReaderOverride=sql=>{
                var t=new DataTable();t.Columns.Add("CharacterId",typeof(int));t.Columns.Add("Name",typeof(string));
                t.Columns.Add("AccountUsername",typeof(string));t.Columns.Add("Online",typeof(int));t.Rows.Add(1,DBNull.Value,DBNull.Value,DBNull.Value);
                return t.CreateDataReader();
            }};
            CharacterDirectoryData nulls=new MySqlCharacterDao(()=>nullable).LoadById(1);
            Require(nulls.Name==null && nulls.AccountUsername==null && nulls.Online==null,"invalid-schema-null-name-owner-preserved-defensively");
            Console.WriteLine("CHARACTER_DAO_SYNTHETIC_CASES=INVALID_PRIMARY_KEY_AND_NON_NULL_NAME_OWNER_ONLY");
        }
        private static DataTable Table(string name,Type type,params object[] values)
        {
            var table=new DataTable();table.Columns.Add(name,type);
            foreach(object value in values)table.Rows.Add(value);
            return table;
        }
    }
}
