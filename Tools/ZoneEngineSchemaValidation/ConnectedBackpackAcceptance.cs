using System.Security.Cryptography;
using System.Xml.Linq;
using AORebirth.Core.Encryption;
using AORebirth.Core.GameData;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

static partial class ConnectedAcceptanceSmoke
{
    const int BackpackMainSlot = 64;
    const int BackpackOtherSlot = 65;
    const int BackpackContentSlot = 3;
    const int BackpackInstanceFlag = 0x0002;

    public static void VerifyBackpackEnginePaths(string zone, string login, string world)
    {
        string build = Path.Combine(RepositoryRoot(), "AORebirth", "Built", "Debug");
        Verify(zone, Path.Combine(build, "ZoneEngine_New", "ZoneEngine_New.dll"));
        Verify(login, Path.Combine(build, "LoginEngine.exe"), Path.Combine(build, "LoginEngine.dll"));
        Verify(world, Path.Combine(build, "WorldEngine", "WorldEngine.dll"));

        static void Verify(string actual, params string[] expected)
        {
            string fullPath = Path.GetFullPath(actual);
            Require(Path.IsPathFullyQualified(actual) && expected.Contains(fullPath, StringComparer.OrdinalIgnoreCase)
                && new FileInfo(fullPath).LinkTarget == null, "backpack-binary-must-belong-to-owning-checkout");
        }
    }

    public static void ValidateBackpacks(string zoneBinary, string loginBinary, string worldBinary,
        DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previousGameData = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        string gameDataRoot = string.IsNullOrWhiteSpace(previousGameData)
            ? GameDataPaths.Resolve(Path.GetDirectoryName(zoneBinary)!)
            : Path.GetFullPath(previousGameData.Trim());
        Require(File.Exists(Path.Combine(gameDataRoot, GameDataPaths.ItemsFileName)), "backpack-gamedata-items-missing");
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        string? previousConnection = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, gameDataRoot);
            var config = XDocument.Load(fixture.ConfigPath);
            config.Root!.SetElementValue("LoginPort", fixture.LoginPort);
            config.Save(fixture.ConfigPath);
            fixture.ConfigureOwnedWorld();

            EnsureChestFixtureTable(connection, "login", wholeFile: true);
            EnsureChestFixtureTable(connection, "charactersperks", wholeFile: true);
            foreach (string table in new[] { "vendors", "vendortemplate", "shopinventorytemplates" })
                EnsureChestFixtureTable(connection, table);
            SeedBackpackPlayers(connection, password);

            var inventory = new MySqlInventoryRepository(new SilentLogger());
            var catalog = new ItemTemplateCatalog(new GameDataStore(new SilentLogger()), new SilentLogger());
            ItemTemplate backpack = catalog.Templates.Where(item => item.DynelType == (int)IdentityType.Container
                && string.Equals(item.Name, "Small Backpack", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Id).FirstOrDefault() ?? throw new FixtureFailure("backpack-small-template-not-in-items-dat");
            ItemTemplate content = catalog.Require(21605); // Ammo: Bullets, ordinary tradeable contents.
            int bag = inventory.LeaseInstanceIdBlock(4);
            int otherBag = bag + 1;
            int nestedItem = bag + 2;
            int peerItem = bag + 3;
            SeedItem(bag, backpack, IdentityType.Inventory, Owner, BackpackMainSlot, 1);
            SeedItem(otherBag, backpack, IdentityType.Inventory, Owner, BackpackOtherSlot, 1);
            SeedItem(nestedItem, content, IdentityType.Container, bag, BackpackContentSlot, 3);
            SeedItem(peerItem, content, IdentityType.Inventory, ChestPeerOwner, BackpackMainSlot, 1);

            Console.WriteLine("SOURCE_SHA=" + Git("rev-parse", "HEAD") + " FIXTURE=" + fixture.FixtureId);
            using var world = new ConnectedEngineProcess(worldBinary, false, fixture, gameDataRoot, world: true);
            using var login = new ConnectedEngineProcess(loginBinary, true, fixture, gameDataRoot);
            int firstProcess;
            using (var zone = new ConnectedEngineProcess(zoneBinary, false, fixture, gameDataRoot))
            {
                firstProcess = zone.Id;
                using var first = EnterChest(fixture, password, out _);
                using var second = EnterChest(fixture, password, out _, ChestPeerAccount, ChestPeerOwner);
                VerifyBackpackAdmission(first, bag, BackpackMainSlot);
                VerifyBackpackAdmission(first, otherBag, BackpackOtherSlot);
                VerifyOpenBackpack(first, Owner, bag, BackpackMainSlot, nestedItem);

                TradeBackpack(first, Owner, second, ChestPeerOwner, bag, BackpackMainSlot, BackpackOtherSlot, peerItem);
                VerifyOwnership(ChestPeerOwner, Owner, BackpackOtherSlot);
                VerifyOpenBackpack(second, ChestPeerOwner, bag, BackpackOtherSlot, nestedItem);

                TradeBackpack(second, ChestPeerOwner, first, Owner, bag, BackpackOtherSlot, BackpackMainSlot, otherBag);
                VerifyOwnership(Owner, ChestPeerOwner, BackpackMainSlot);
                VerifyOpenBackpack(first, Owner, bag, BackpackMainSlot, nestedItem);
                ChestLogout(second, connection, ChestPeerOwner);
                ChestLogout(first, connection);
                zone.Stop();
            }
            using (var restarted = new ConnectedEngineProcess(zoneBinary, false, fixture, gameDataRoot))
            {
                Require(restarted.Id != firstProcess, "backpack-zone-restart-must-use-distinct-process");
                using var first = EnterChest(fixture, password, out _);
                VerifyBackpackAdmission(first, bag, BackpackMainSlot);
                VerifyBackpackAdmission(first, otherBag, BackpackOtherSlot);
                VerifyOwnership(Owner, ChestPeerOwner, BackpackMainSlot);
                VerifyOpenBackpack(first, Owner, bag, BackpackMainSlot, nestedItem);
                ChestLogout(first, connection);
                restarted.Stop();
            }
            login.Stop();
            world.Stop();
            Console.WriteLine("CONNECTED_BACKPACK_ACCEPTANCE=PASS TWO_TCP_SESSIONS=PASS ORIGINAL_CLIENT_UI=NOT_EXERCISED PRODUCTION_CONTACT=NO");

            void SeedItem(int instance, ItemTemplate template, IdentityType page, int owner, int slot, int count)
                => inventory.Insert(new ItemInstanceRecord
                {
                    InstanceId = instance, ContainerType = (int)page, ContainerInstance = owner,
                    ContainerPlacement = slot, ItemType = template.ItemType, LowId = template.Id,
                    HighId = template.Id, Quality = Math.Max(1, template.Quality), StackCount = count
                });

            void VerifyOwnership(int holder, int previous, int slot)
            {
                Require(inventory.GetCarriedItems(holder).Count(row => row.InstanceId == bag
                    && row.ContainerType == (int)IdentityType.Inventory && row.ContainerPlacement == slot) == 1
                    && inventory.GetCarriedItems(previous).All(row => row.InstanceId != bag), "backpack-durable-owner-and-slot");
                Require(inventory.GetCarriedItems(Owner).Count(row => row.InstanceId == otherBag
                    && row.ContainerPlacement == BackpackOtherSlot) == 1, "backpack-identical-template-sibling-unchanged");
                ItemInstanceRecord child = inventory.GetContainerItems(bag).Single();
                Require(child.InstanceId == nestedItem && child.ContainerPlacement == BackpackContentSlot
                    && child.LowId == content.Id && child.StackCount == 3, "backpack-contents-durable-and-unchanged");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previousConnection);
            Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previousGameData);
        }
    }

    static void SeedBackpackPlayers(MySqlConnection connection, string password)
    {
        foreach ((int owner, string account) in new[] { (Owner, Account), (ChestPeerOwner, ChestPeerAccount) })
        {
            using var login = new MySqlCommand("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (@owner,'2026-10-24','fixture@invalid','','',@account,@password,6,0,0,2047,0)", connection);
            login.Parameters.AddWithValue("@owner", owner);
            login.Parameters.AddWithValue("@account", account);
            login.Parameters.AddWithValue("@password", PasswordHash.CreateHash(password));
            login.ExecuteNonQuery();

            using var character = new MySqlCommand("INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES (@owner,@account,@name,'','',4582,100,0,100,1,0,0,0,0)", connection);
            character.Parameters.AddWithValue("@owner", owner);
            character.Parameters.AddWithValue("@account", account);
            character.Parameters.AddWithValue("@name", "BackpackFixture" + owner);
            character.ExecuteNonQuery();

            var stats = LifecycleSpawnFixture.Stats();
            stats[CharacterStat.ExternalPlayfieldInstance] = 0;
            stats[CharacterStat.ExternalDoorInstance] = 0;
            foreach (var stat in stats)
                FixtureSql.Execute(connection, $"INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (50000,{owner},{(int)stat.Key},{stat.Value})");
        }
    }

    static void TradeBackpack(ConnectedWireClient from, int sender, ConnectedWireClient to, int receiver, int bag,
        int sourceSlot, int destinationSlot, int retainedItem)
    {
        Identity source = ChestCharacter(sender);
        Identity recipient = ChestCharacter(receiver);
        from.Send(new TradeMessage { Identity = source, Action = TradeAction.Open, Target = recipient }, sender);
        from.Wait<TradeMessage>(message => message.Action == TradeAction.Open);
        to.Wait<TradeMessage>(message => message.Action == TradeAction.Open);
        from.Send(new TradeMessage { Identity = source, Action = TradeAction.AddItem, Target = source,
            Container = new Identity { Type = IdentityType.Inventory, Instance = sourceSlot } }, sender);
        from.Wait<TradeMessage>(message => message.Action == TradeAction.AddItem);
        TemplateActionMessage render = to.Wait<TemplateActionMessage>(message => message.Action == TemplateActionType.TradeRender);
        Require(render.Placement.Type == IdentityType.Inventory && render.Placement.Instance == sourceSlot,
            "backpack-trade-render-original-placement");

        from.Send(new TradeMessage { Identity = source, Action = TradeAction.Confirm }, sender);
        to.Send(new TradeMessage { Identity = recipient, Action = TradeAction.Confirm }, receiver);
        from.Wait<TradeMessage>(message => message.Action == TradeAction.Confirm);
        to.Wait<TradeMessage>(message => message.Action == TradeAction.Confirm);
        int mark = to.Received.Count;
        from.Send(new TradeMessage { Identity = source, Action = TradeAction.Accept }, sender);
        to.Send(new TradeMessage { Identity = recipient, Action = TradeAction.Accept }, receiver);
        from.Wait<TradeMessage>(message => message.Action == TradeAction.Complete);
        to.Wait<StatMessage>(message => message.Identity == recipient
            && message.Stats.Any(stat => stat.Value1 == CharacterStat.SocialStatus && stat.Value2 == 0));
        var packets = to.Received.Skip(mark).ToArray();
        var complete = packets.OfType<TradeMessage>().Where(message => message.Action == TradeAction.Complete).ToArray();
        InventoryUpdateMessage refreshed = packets.OfType<InventoryUpdateMessage>().Single();
        Require(complete.Length == 2 && Array.IndexOf(packets, refreshed) > Array.IndexOf(packets, complete[1])
            && !packets.OfType<AddTemplateMessage>().Any(), "backpack-trade-two-complete-before-one-snapshot-without-grant");
        Require(refreshed.Identity == recipient && refreshed.BagIdentity == recipient
            && refreshed.NumberOfSlots == 94 && refreshed.SlotnumberInMainInventory == 0x68,
            "backpack-trade-authoritative-combined-inventory-snapshot");
        InventoryEntry entry = refreshed.Entries.Single(value => value.Identity == BackpackIdentity(bag));
        Require(entry.Slotnumber == destinationSlot && (entry.UnknownFlags & 3) == BackpackInstanceFlag
            && refreshed.Entries.Length == 2 && refreshed.Entries.Any(value => value.Identity.Instance == retainedItem),
            "backpack-trade-retains-item-and-binds-stable-identity");
        Console.WriteLine($"CONNECTED_BACKPACK_TRADE=PASS SENDER={sender} RECEIVER={receiver} BAG={bag} SLOT={destinationSlot}");
    }

    static void VerifyBackpackAdmission(ConnectedWireClient client, int bag, int slot)
    {
        InventorySlot entry = client.Received.OfType<FullCharacterMessage>().Single().InventorySlots
            .Single(value => value.Identity == BackpackIdentity(bag));
        Require(entry.Placement == slot && (entry.Flags & 3) == BackpackInstanceFlag, "backpack-full-character-instance-binding");
    }

    static void VerifyOpenBackpack(ConnectedWireClient client, int owner, int bag, int slot, int content)
    {
        SendChestUse(client, new Identity { Type = IdentityType.Inventory, Instance = slot }, owner);
        ChestItemFullUpdateMessage chest = client.Wait<ChestItemFullUpdateMessage>(message => message.Identity == BackpackIdentity(bag));
        InventoryUpdateMessage opened = client.Wait<InventoryUpdateMessage>(message => message.BagIdentity == BackpackIdentity(bag));
        Require(chest.Owner == ChestCharacter(owner) && chest.Unknown5 == (0x0100 | slot)
            && opened.Entries.Single().Identity.Instance == content && opened.Entries[0].Slotnumber == BackpackContentSlot
            && opened.Entries[0].Unknown1 == 3, "backpack-opened-same-container-with-original-contents");
    }

    static Identity BackpackIdentity(int instance) => new() { Type = IdentityType.Container, Instance = instance };
}
