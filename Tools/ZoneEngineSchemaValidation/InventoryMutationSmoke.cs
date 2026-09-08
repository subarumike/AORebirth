using MySqlConnector;
using ZoneEngine_New.Core.Data;

static class InventoryMutationSmoke
{
    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            var inventory = new MySqlInventoryRepository(new SilentLogger());
            var persistence = new MySqlInventoryMutationPersistence(inventory, new MySqlUploadedNanoRepository(new SilentLogger()));
            int id = inventory.LeaseInstanceIdBlock(2);
            ItemInstanceRecord Row(int instance, int slot, int count) => new()
            {
                InstanceId = instance, ContainerType = 104, ContainerInstance = 9501, ContainerPlacement = slot,
                LowId = 20, HighId = 20, Quality = 1, StackCount = count
            };
            inventory.Insert(Row(id, 64, 10));
            var batch = new InventoryMutationBatch(9501, [Row(id + 1, 65, 3)],
                [new ItemLocationUpdate(id, 104, 9501, 66)], [new ItemStackUpdate(id, 999, 7)], [37]);
            string before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
            bool failed = false;
            try { persistence.Persist(batch); }
            catch (InvalidOperationException) { failed = true; }
            if (!failed || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("split-stale-count-did-not-roll-back-insert-location-and-nano-plan");
            batch = batch with { Stacks = [new ItemStackUpdate(id, 10, 7)] };
            FixtureSql.Execute(connection, "ALTER TABLE charactersuploadednanos ADD CONSTRAINT fixture_split_nano_failure CHECK (CharacterId <> 9501)");
            before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
            failed = false;
            try { persistence.Persist(batch); }
            catch (MySqlException exception) when (exception.Number == 3819) { failed = true; }
            if (!failed || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("split-late-nano-failure-did-not-roll-back-insert-location-stack");
            FixtureSql.Execute(connection, "ALTER TABLE charactersuploadednanos DROP CHECK fixture_split_nano_failure");
            batch = batch with { FinalStats = [new StatRecord { StatId = 16, StatValue = 45 }] };
            FixtureSql.Execute(connection, "ALTER TABLE stats ADD CONSTRAINT fixture_split_stat_failure CHECK (Instance <> 9501)");
            before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
            failed = false;
            try { persistence.Persist(batch); }
            catch (MySqlException exception) when (exception.Number == 3819) { failed = true; }
            if (!failed || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("split-late-stat-failure-did-not-roll-back-insert-location-stack-and-nanos");
            FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_split_stat_failure");
            persistence.Persist(batch);
            if (FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE (InstanceId={id} AND ContainerPlacement=66 AND StackCount=7) OR (InstanceId={id + 1} AND ContainerPlacement=65 AND StackCount=3)") != 2
                || FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM charactersuploadednanos WHERE CharacterId=9501 AND NanoId=37") != 1)
                throw new FixtureFailure("split-success-not-durable");
            int bag = inventory.LeaseInstanceIdBlock(2);
            inventory.Insert(Row(bag, 67, 1));
            var child = new ItemInstanceRecord
            {
                InstanceId = bag + 1, ContainerType = (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.Container,
                ContainerInstance = bag, ContainerPlacement = 1, LowId = 20, HighId = 20, Quality = 1, StackCount = 1
            };
            inventory.Insert(child);
            var retire = new InventoryMutationBatch(9501, [], [new ItemLocationUpdate(bag, 0, 9501, bag)], [], []) { EmptyContainersBeforeRetire = [bag] };
            before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false); failed = false;
            try { persistence.Persist(retire); } catch (InvalidOperationException) { failed = true; }
            if (!failed || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("nonempty-container-retirement-orphaned-child");
            persistence.Persist(retire with { Locations = [new ItemLocationUpdate(bag + 1, 104, 9501, 68), new ItemLocationUpdate(bag, 0, 9501, bag)] });
            if (FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={bag} AND ContainerType=0") != 1)
                throw new FixtureFailure("empty-container-retirement-not-durable");
            Console.WriteLine("INVENTORY_SPLIT_ATOMIC_COMMIT=PASS INVENTORY_SPLIT_STALE_COUNT_ROLLBACK=PASS INVENTORY_SPLIT_LATE_NANO_ROLLBACK=PASS INVENTORY_LATE_STAT_ROLLBACK=PASS INVENTORY_NONEMPTY_BAG_ROLLBACK=PASS INVENTORY_EMPTY_BAG_RETIREMENT=PASS");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }
}
