namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AORebirth.Database.Domain.Characters;
    using AORebirth.Interfaces.Persistence.Characters;
    using Dapper;

    internal static partial class Program
    {
        private static void ConcurrencyChecks()
        {
            // This canonical schema has no Online index. Do not assume only matching rows
            // are locked: InnoDB can lock other scanned rows during the serializable scan.
            foreach(int writerId in new[]{101,103}) LockedWriterChecks(writerId,false);
            LockedWriterChecks(101,true);

            FaultFixture();
            using(var start=new ManualResetEventSlim(false))
            {
                Task<StaleOnlineRecoveryData> first=Task.Run(()=>{start.Wait();return Dao().RecoverStaleOnline(DatabaseName);});
                Task<StaleOnlineRecoveryData> second=Task.Run(()=>{start.Wait();return Dao().RecoverStaleOnline(DatabaseName);});
                start.Set();
                CompleteBoth(first,second,"parallel-recovery");
                StaleOnlineRecoveryData a=Complete(first,"parallel-recovery-first");
                StaleOnlineRecoveryData b=Complete(second,"parallel-recovery-second");
                Require(a.Rows.Count+b.Rows.Count==2 && a.RowsUpdated+b.RowsUpdated==2,
                    "simultaneous-recovery-no-double-clear");
                Require(new[]{a,b}.Count(x=>x.CleanupRequired)==1 && new[]{a,b}.Count(x=>x.PostUpdateNonzeroCount==null)==1,
                    "simultaneous-recovery-one-committed-one-read-only");
                Require(Dao().ListLoggedIn().Count==0 && Dao().LoadById(102).Online==0,
                    "simultaneous-recovery-durable-cleared-state");
            }
            FaultFixture();string before=UnrelatedSnapshot();
            using(var start=new ManualResetEventSlim(false))
            {
                Task<int> online=Task.Run(()=>{start.Wait();return Dao().MarkOnline(101);});
                Task<int> offline=Task.Run(()=>{start.Wait();return Dao().MarkOffline(101);});
                start.Set();
                CompleteBoth(online,offline,"parallel-state-writes");
                Require(Complete(online,"concurrent-online")==1 && Complete(offline,"concurrent-offline")==1,
                    "concurrent-state-writes-return-matched-row-counts");
                int? final=Dao().LoadById(101).Online;
                Require((final==0 || final==1) && Dao().LoadById(102).Online==7 && before==UnrelatedSnapshot(),
                    "concurrent-state-writes-serialized-valid-last-state-only");
            }
        }

        private static void LockedWriterChecks(int writerId,bool rollback)
        {
            FaultFixture();
            using(var locked=new ManualResetEventSlim(false))
            using(var release=new ManualResetEventSlim(false))
            {
                var observed=new ObservedConnection(application){FailurePoint=rollback?"after-write":null};
                observed.AfterCommand=command=>{
                    if(command.Sql.Contains("FOR UPDATE",StringComparison.OrdinalIgnoreCase))
                    {
                        locked.Set();
                        if(!release.Wait(TimeSpan.FromSeconds(15)))throw new CheckFailure("lock-release-deadline");
                    }
                };
                StaleOnlineRecoveryData result=null;
                Task<Exception> recovery=Task.Run(()=>{
                    try{result=new MySqlCharacterDao(()=>observed).RecoverStaleOnline(DatabaseName);return null;}
                    catch(Exception caught){return caught;}
                });
                Task<int> writer=null;
                string tag=writerId+"-"+(rollback?"rollback":"commit");
                Exception synchronizationError=null;
                try
                {
                    Require(locked.Wait(TimeSpan.FromSeconds(10)),"real-lock-read-reached-"+tag);
                    writer=Task.Run(()=>Dao().MarkOnline(writerId));
                    Require(WaitForFixtureLockWait(),"real-mysql-writer-lock-wait-observed-"+tag);
                    Require(!writer.IsCompleted,"writer-blocked-before-recovery-boundary-"+tag);
                }
                catch(Exception caught){synchronizationError=caught;}
                finally
                {
                    release.Set();
                    // Drain every started task even when a lock assertion fails, before
                    // disposing its signals or letting the wrapper remove the fixture.
                    Drain(recovery,"held-recovery-cleanup-"+tag,ref synchronizationError);
                    if(writer!=null)Drain(writer,"held-writer-cleanup-"+tag,ref synchronizationError);
                }
                if(synchronizationError!=null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(synchronizationError).Throw();
                Exception error=Complete(recovery,"held-recovery-"+tag);
                Require(writer!=null && Complete(writer,"held-writer-"+tag)==1,"writer-proceeds-after-owned-boundary-"+tag);
                Require(observed.Disposed && observed.TransactionDisposed,"concurrent-owned-resources-disposed-"+tag);
                if(rollback)
                {
                    Require(object.ReferenceEquals(error,observed.Error) && observed.RollbackCount==1 && observed.CommitCount==0,
                        "concurrent-recovery-failure-original-and-rollback");
                    Require(Dao().LoadById(101).Online==1 && Dao().LoadById(102).Online==7,
                        "concurrent-rollback-no-partial-cleanup");
                }
                else
                {
                    Require(error==null && result.Rows.Select(x=>x.CharacterId).SequenceEqual(new[]{101,102})
                        && result.Rows.Select(x=>x.PreviousOnline).SequenceEqual(new[]{1,7}) && result.RowsUpdated==2
                        && result.PostUpdateNonzeroCount==0,"concurrent-capture-stable-before-later-writer-"+tag);
                    Require(Dao().LoadById(writerId).Online==1 && Dao().LoadById(102).Online==0,
                        "post-commit-writer-does-not-rewrite-historical-result-"+tag);
                    StaleOnlineRecoveryData later=Dao().RecoverStaleOnline(DatabaseName);
                    Require(later.Rows.Count==1 && later.Rows[0].CharacterId==writerId && later.RowsUpdated==1,
                        "fresh-reconciliation-captures-later-state-"+tag);
                }
            }
        }

        private static bool WaitForFixtureLockWait()
        {
            var elapsed=Stopwatch.StartNew();
            while(elapsed.Elapsed < TimeSpan.FromSeconds(8))
            {
                long waits=Sql(c=>c.Query<long>("SELECT COUNT(*) FROM performance_schema.data_lock_waits w "
                    +"INNER JOIN performance_schema.data_locks l ON l.ENGINE_LOCK_ID=w.REQUESTING_ENGINE_LOCK_ID "
                    +"AND l.ENGINE=w.ENGINE WHERE l.OBJECT_SCHEMA=@Database AND l.OBJECT_NAME='characters'",
                    new{Database=DatabaseName}).Single());
                if(waits>0)return true;
                Thread.Sleep(50);
            }
            return false;
        }

        private static T Complete<T>(Task<T> task,string name)
        {
            if(!((IAsyncResult)task).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(15)))
                throw new CheckFailure(name+"-deadline");
            return task.GetAwaiter().GetResult();
        }

        private static void CompleteBoth(Task first,Task second,string name)
        {
            Exception error=null;
            Drain(first,name+"-first",ref error);
            Drain(second,name+"-second",ref error);
            if(error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        private static void Drain(Task task,string name,ref Exception original)
        {
            try
            {
                if(!((IAsyncResult)task).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(20)))
                    throw new CheckFailure(name+"-deadline");
                task.GetAwaiter().GetResult();
            }
            catch(Exception error)
            {
                if(original==null)original=error;
                else original.Data["CharacterDaoValidation.TaskCleanupFailure."+name]=error;
            }
        }
    }
}
