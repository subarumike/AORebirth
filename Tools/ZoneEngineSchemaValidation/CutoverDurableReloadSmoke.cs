using MySqlConnector;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>
/// Real repository round trips across fresh service instances and real engine
/// process restarts. This is deliberately not labelled a credential/login-wire test.
/// Test identities and values are disposable fixture data, never runtime content.
/// </summary>
static class CutoverDurableReloadSmoke
{
    const int CharacterId = 9801;
    public static void Validate(string engine, DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES ({CharacterId},'CutoverFixture','','',4582,100,20,100,1,0,0,0,0)");
            var log = new SilentLogger();
            var inventory = new MySqlInventoryRepository(log);
            int identity = inventory.LeaseInstanceIdBlock(2);
            var first = new ItemInstanceRecord { InstanceId = identity, ContainerType = (int)IdentityType.Inventory,
                ContainerInstance = CharacterId, ContainerPlacement = 64, LowId = 20, HighId = 21, Quality = 17, StackCount = 9, Source = (AORebirth.Enums.ItemSource)1 };
            var second = new ItemInstanceRecord { InstanceId = identity + 1, ContainerType = (int)IdentityType.Inventory,
                ContainerInstance = CharacterId, ContainerPlacement = 65, LowId = 22, HighId = 23, Quality = 18, StackCount = 1, Source = 0 };
            inventory.Insert(first); inventory.Insert(second);
            var stats = new[] { new StatRecord { StatId = (int)CharacterStat.Level, StatValue = 1 },
                new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = 1234 } };
            var character = new MySqlCharacterRepository(log).GetById(CharacterId)!;
            new MySqlCharacterRepository(log).SaveSnapshot(character, 0, stats);
            string legacyBefore = FixtureSql.TableFingerprint(connection, "items") + FixtureSql.TableFingerprint(connection, "instanceditems");

            // Swap ownership slots through the actual batch writer, then move one item
            // to wear storage. This verifies persistence representation, not equip legality.
            inventory.UpdateLocations([new(identity, (int)IdentityType.Inventory, CharacterId, 65),
                new(identity + 1, (int)IdentityType.Inventory, CharacterId, 64)]);
            var mutation = new MySqlInventoryMutationPersistence(inventory, new MySqlUploadedNanoRepository(log));
            mutation.Persist(new InventoryMutationBatch(CharacterId, [],
                [new(identity + 1, (int)IdentityType.WeaponPage, CharacterId, 6)], [new(identity, 9, 7)], [])
                { FinalStats = [new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = 1200 }] });
            character = new CharacterRecord { Id = CharacterId, Name = "CutoverFixture", FirstName = "", LastName = "",
                Playfield = 4582, X = 101, Y = 21, Z = 102, HeadingW = 1 };
            new MySqlCharacterRepository(log).SaveSnapshot(character, 0,
                [stats[0], new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = 1200 }]);
            VerifyFreshReload(fixture, connection, identity);
            string durable = FixtureSql.Fingerprint(connection);
            EngineSmoke.Lifecycle(engine, fixture);
            VerifyFreshReload(fixture, connection, identity);
            Require(durable == FixtureSql.Fingerprint(connection), "first-restart-exact-database-fingerprint");
            EngineSmoke.Lifecycle(engine, fixture);
            VerifyFreshReload(fixture, connection, identity);
            Require(durable == FixtureSql.Fingerprint(connection), "second-restart-exact-database-fingerprint");
            Require(legacyBefore == FixtureSql.TableFingerprint(connection, "items") + FixtureSql.TableFingerprint(connection, "instanceditems"), "legacy-tables-remain-stale-after-new-write");
            Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM items WHERE ContainerType={CharacterId}") == 0
                && FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM instanceditems WHERE ContainerType={CharacterId}") == 0, "new-owned-items-not-readable-from-legacy-tables");
            Console.WriteLine("CUTOVER_DAO_FRESH_RELOAD=PASS CUTOVER_EXACT_ITEM_STATE=PASS CUTOVER_PERSISTED_STATE_PROCESS_RESTART=PASS POST_NEWENGINE_WRITE_LEGACY_ROLLBACK_SAFE=NO LOGIN_WIRE_ACCEPTANCE=NOT_EXERCISED");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }

    static void VerifyFreshReload(DisposableSchemaDatabase fixture, MySqlConnection connection, int identity)
    {
        var log = new SilentLogger();
        var characters = new MySqlCharacterRepository(log);
        var stats = new MySqlStatRepository(log);
        var inventory = new MySqlInventoryRepository(log);
        var nanos = new MySqlUploadedNanoRepository(log);
        var loaded = new CharacterHydrationService(characters, stats, inventory, nanos, log).LoadForLogin(CharacterId);
        Require(loaded != null && loaded.Character.Id == CharacterId && loaded.Character.X == 101
            && loaded.Character.Y == 21 && loaded.Character.Z == 102 && loaded.Character.Playfield == 4582, "fresh-character-reload");
        Require(loaded!.Stats.Single(s => s.StatId == (int)CharacterStat.Cash).StatValue == 1200, "fresh-credit-reload");
        Require(loaded.Items.Count == 2 && loaded.Items.Select(i => i.InstanceId).Distinct().Count() == 2, "no-duplicate-or-missing-items");
        var first = loaded.Items.Single(i => i.InstanceId == identity);
        var second = loaded.Items.Single(i => i.InstanceId == identity + 1);
        Require(first.ContainerType == (int)IdentityType.Inventory && first.ContainerInstance == CharacterId && first.ContainerPlacement == 65
            && first.LowId == 20 && first.HighId == 21 && first.Quality == 17 && first.StackCount == 7 && (int)first.Source == 1, "exact-first-item-state");
        Require(second.ContainerType == (int)IdentityType.WeaponPage && second.ContainerInstance == CharacterId && second.ContainerPlacement == 6
            && second.LowId == 22 && second.HighId == 23 && second.Quality == 18 && second.StackCount == 1 && second.Source == 0, "exact-equipped-item-state");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE ContainerInstance={CharacterId}") == 2, "no-phantom-owned-rows");
    }
    static void Require(bool condition, string name) { if (!condition) throw new FixtureFailure("cutover-" + name); }
}
