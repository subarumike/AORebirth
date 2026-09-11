namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed partial class MySqlCharacterPersistenceDao
    {
        private const string ItemColumns = "InstanceId,ContainerType,ContainerInstance,ContainerPlacement,ItemType,LowId,HighId,Quality,StackCount,Source";

        public IList<PersistedItemData> LoadCarriedItems(int characterId)
        {
            if (characterId <= 0) return new List<PersistedItemData>();
            return Query("SELECT " + ItemColumns + " FROM item_instances WHERE ContainerInstance=@Id AND ContainerType IN ("
                + (int)IdentityType.Inventory + "," + (int)IdentityType.WeaponPage + "," + (int)IdentityType.ArmorPage + ","
                + (int)IdentityType.ImplantPage + "," + (int)IdentityType.SocialPage + ")", ReadItem, "@Id", characterId);
        }

        public IList<PersistedItemData> LoadBankItems(int characterId) { return LoadItems((int)IdentityType.BankByRef, characterId); }
        public IList<PersistedItemData> LoadContainerItems(int containerInstanceId) { return LoadItems((int)IdentityType.Container, containerInstanceId); }

        private IList<PersistedItemData> LoadItems(int type, int instance)
        {
            if (instance <= 0) return new List<PersistedItemData>();
            return Query("SELECT " + ItemColumns + " FROM item_instances WHERE ContainerType=@Type AND ContainerInstance=@Id",
                ReadItem, "@Type", type, "@Id", instance);
        }

        private static PersistedItemData ReadItem(IDataRecord r)
        {
            return new PersistedItemData { InstanceId = r.GetInt32(0), ContainerType = r.GetInt32(1),
                ContainerInstance = r.GetInt32(2), ContainerPlacement = r.GetInt32(3), ItemType = r.GetInt32(4),
                LowId = r.GetInt32(5), HighId = r.GetInt32(6), Quality = r.GetInt32(7), StackCount = r.GetInt32(8), Source = r.GetInt32(9) };
        }

        public int LeaseItemInstanceIds(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            return Transaction((c, t) =>
            {
                if (Execute(c, t, "UPDATE item_instance_id_sequence SET NextInstanceId=LAST_INSERT_ID(NextInstanceId)+@Count WHERE Id=1",
                    "@Count", count) == 0) throw new InvalidOperationException("item_instance_id_sequence row Id=1 is missing; apply SqlTables/migrations.");
                using (var command = Command(c, t, "SELECT LAST_INSERT_ID()"))
                {
                    object result = command.ExecuteScalar();
                    if (result == null || result is DBNull) throw new InvalidOperationException("LeaseInstanceIdBlock could not read LAST_INSERT_ID().");
                    int start = Convert.ToInt32(result, CultureInfo.InvariantCulture);
                    if (start <= 0) throw new InvalidOperationException("LeaseInstanceIdBlock returned a non-positive start.");
                    return start;
                }
            });
        }

        public void InsertItem(PersistedItemData item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            Transaction((c, t) => { WriteItem(c, t, item); return 0; });
        }

        public void UpdateItemLocation(ItemLocationData location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            Transaction((c, t) => { WriteItemLocation(c, t, location, location.ContainerPlacement); return 0; });
        }

        public void SaveItemLocations(IList<PersistedItemData> inserts, IList<ItemLocationData> locations)
        {
            if (inserts == null) throw new ArgumentNullException(nameof(inserts));
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            if (inserts.Count == 0 && locations.Count == 0) return;
            Transaction((c, t) => { WriteItems(c, t, inserts, locations); return 0; });
        }

        private static void WriteItem(IDbConnection c, IDbTransaction t, PersistedItemData v)
        {
            if (v.InstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(v), "Insert requires a pre-allocated InstanceId.");
            Execute(c, t, "INSERT INTO item_instances (" + ItemColumns + ") VALUES (@Id,@Type,@Owner,@Slot,@ItemType,@Low,@High,@Quality,@Count,@Source)",
                "@Id", v.InstanceId, "@Type", v.ContainerType, "@Owner", v.ContainerInstance, "@Slot", v.ContainerPlacement,
                "@ItemType", v.ItemType, "@Low", v.LowId, "@High", v.HighId, "@Quality", v.Quality, "@Count", v.StackCount, "@Source", (byte)v.Source);
        }

        private static void WriteItemLocation(IDbConnection c, IDbTransaction t, ItemLocationData v, int placement)
        {
            if (v.InstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(v));
            if (Execute(c, t, "UPDATE item_instances SET ContainerType=@Type,ContainerInstance=@Owner,ContainerPlacement=@Slot WHERE InstanceId=@Id",
                "@Type", v.ContainerType, "@Owner", v.ContainerInstance, "@Slot", placement, "@Id", v.InstanceId) == 0)
                throw new InvalidOperationException("UpdateLocations found no row for InstanceId=" + v.InstanceId);
        }

        private static void WriteItems(IDbConnection c, IDbTransaction t, IList<PersistedItemData> inserts, IList<ItemLocationData> locations)
        {
            // Preserve the existing unique-slot-safe ordering, including insert into a vacated slot.
            for (int i = 0; i < locations.Count; i++) WriteItemLocation(c, t, locations[i], -(i + 1));
            foreach (var item in inserts) WriteItem(c, t, item);
            foreach (var location in locations) WriteItemLocation(c, t, location, location.ContainerPlacement);
        }

        private static void WriteStacks(IDbConnection c, IDbTransaction t, IList<ItemStackData> stacks)
        {
            foreach (var stack in stacks)
            {
                if (stack.InstanceId <= 0 || stack.ExpectedCount <= 0 || stack.FinalCount <= 0) throw new ArgumentOutOfRangeException(nameof(stacks));
                if (Execute(c, t, "UPDATE item_instances SET StackCount=@Final WHERE InstanceId=@Id AND StackCount=@Expected",
                    "@Final", stack.FinalCount, "@Id", stack.InstanceId, "@Expected", stack.ExpectedCount) != 1)
                    throw new InvalidOperationException("Inventory stack changed before the operation could commit.");
            }
        }

        private static void AssertContainersEmpty(IDbConnection c, IDbTransaction t, IList<int> containers)
        {
            foreach (int id in containers)
            {
                if (id <= 0) throw new InvalidOperationException("Invalid container retirement identity.");
                using (var command = Command(c, t, "SELECT InstanceId FROM item_instances WHERE ContainerType=@Type AND ContainerInstance=@Id LIMIT 1 FOR UPDATE",
                    "@Type", (int)IdentityType.Container, "@Id", id))
                    if (command.ExecuteScalar() != null) throw new InvalidOperationException("Cannot retire a nonempty item container.");
            }
        }
    }
}
