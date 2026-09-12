namespace AORebirth.Database.Domain.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Missions;
    using Dapper;

    public sealed partial class MySqlMissionDao
    {
        private sealed partial class MySqlMissionDaoTransaction : IMissionInventoryMutationTransaction
        {
            public void ApplyInventoryMutation(
                IReadOnlyList<MissionItemInstanceData> grants,
                IReadOnlyList<MissionItemInstanceData> consumed)
            {
                this.EnsureActive();
                try
                {
                    this.ApplyInventoryMutationCore(grants, consumed);
                }
                catch
                {
                    this.failed = true;
                    throw;
                }
            }

            private void ApplyInventoryMutationCore(
                IReadOnlyList<MissionItemInstanceData> grants,
                IReadOnlyList<MissionItemInstanceData> consumed)
            {
                if (grants == null || consumed == null) throw new ArgumentNullException("inventoryPlan");
                if (grants.Concat(consumed).Any(item => item == null || item.InstanceId <= 0
                    || item.ContainerType != 104 || item.ContainerInstance != this.CharacterId
                    || item.ContainerPlacement < 0 || item.StackCount <= 0)
                    || grants.Concat(consumed).Select(item => item.InstanceId).Distinct().Count() != grants.Count + consumed.Count)
                    throw new ArgumentException("Authored inventory mutation requires distinct exact owned main-inventory rows.");

                // Runtime rejects bag use, and the durable boundary also rejects any surviving
                // child row (including unhydrated content). 0xC749 is IdentityType.Container.
                // The owner transaction's repeatable-read range lock prevents orphaning races.
                foreach (var item in consumed)
                {
                    if (this.connection.Query<int>(
                        "SELECT InstanceId FROM item_instances WHERE ContainerType=0xC749 AND ContainerInstance=@InstanceId LIMIT 1 FOR UPDATE",
                        new { item.InstanceId }, this.transaction).Any())
                        throw new InvalidOperationException("Authored item retirement would orphan container contents.");
                    RetireGeneratedItem(this.connection, this.transaction, item, this.CharacterId);
                }
                foreach (var item in grants) InsertGeneratedItem(this.connection, this.transaction, item, this.CharacterId);
            }
        }
    }
}
