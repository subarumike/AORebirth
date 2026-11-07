using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using AORebirth.Core.Encryption;
using AORebirth.Core.GameData;
using AORebirth.Database.Domain.Characters;
using AORebirth.Database.Domain.Missions;
using AORebirth.Database.Domain.Quests;
using AORebirth.Interfaces.Persistence.Characters;
using AORebirth.Interfaces.Persistence.Missions;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.Quests.Dungeons;

static partial class ConnectedAcceptanceSmoke
{
    // A current terminal origin already used by the connected mission fixture.
    const int ChestOriginPlayfield = 710;
    const int ChestPeerOwner = 9902;
    const string ChestPeerAccount = "connectedchestpeer";
    const int ChestNonGmOwner = 9903;
    const string ChestNonGmAccount = "connectedchestnongm";
    const int MaximumCaveAcceptances = 12;

    public static void ValidateChests(string zoneBinary, string loginBinary, string worldBinary, DisposableSchemaDatabase fixture, MySqlConnection connection, bool lifecycleOnly = false)
    {
        string? configuredGameData = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        string gameDataRoot = string.IsNullOrWhiteSpace(configuredGameData)
            ? GameDataPaths.Resolve(Path.GetDirectoryName(Path.GetFullPath(zoneBinary))!)
            : Path.GetFullPath(configuredGameData.Trim());
        Require(Directory.Exists(gameDataRoot), "chest-gamedata-root-missing");
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        bool allowProvenShutdownBaseline = !lifecycleOnly && string.Equals(
            Environment.GetEnvironmentVariable("AO_REBIRTH_CHEST_ALLOW_PROVEN_SHUTDOWN_BASELINE"), "1", StringComparison.Ordinal);
        if (allowProvenShutdownBaseline)
            ConnectedEngineProcess.ValidateProvenChestShutdownBaseline();
        bool usedProvenShutdownBaseline = false;
        string? previousConnection = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        string? previousGameData = Environment.GetEnvironmentVariable("AO_REBIRTH_GAMEDATA_PATH");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            Environment.SetEnvironmentVariable("AO_REBIRTH_GAMEDATA_PATH", gameDataRoot);
            var xml = XDocument.Load(fixture.ConfigPath);
            xml.Root!.SetElementValue("LoginPort", fixture.LoginPort);
            xml.Save(fixture.ConfigPath);
            fixture.ConfigureOwnedWorld();
            if (lifecycleOnly)
            {
                using var lifecycleWorld = new ConnectedEngineProcess(worldBinary, false, fixture, gameDataRoot, world: true);
                using var lifecycleLogin = new ConnectedEngineProcess(loginBinary, true, fixture, gameDataRoot);
                using var lifecycleZone = new ConnectedEngineProcess(zoneBinary, false, fixture, gameDataRoot);
                Console.WriteLine("CONNECTED_LIFECYCLE_READY=PASS GAMEPLAY=NOT_EXERCISED");
                lifecycleZone.Stop();
                lifecycleLogin.Stop();
                lifecycleWorld.Stop();
                Console.WriteLine("CONNECTED_LIFECYCLE=PASS PRODUCTION_CONTACT=NO MIGRATIONS_EXECUTED=0");
                return;
            }
            var data = new GameDataStore(new SilentLogger());
            var keyPolicy = MissionKeyPolicy.Load(gameDataRoot, new ItemTemplateCatalog(data, new SilentLogger()));
            MissionChestRKAllowlist rkPolicy = MissionChestRKAllowlist.Load(gameDataRoot, data.ItemFamilies);
            Require(rkPolicy.Count > 0, "connected-chest-approved-rk-policy-required");
            var terminal = data.GetPlayfieldGeometry(ChestOriginPlayfield).Dynels?.Dynels
                .FirstOrDefault(dynel => dynel.IdentityType == MissionTerminal.LiveIdentityType)
                ?? throw new FixtureFailure("connected-chest-real-terminal-missing");
            EnsureChestFixtureTable(connection, "login", wholeFile: true);
            EnsureChestFixtureTable(connection, "charactersperks", wholeFile: true);
            // Activation reads these current catalog tables even when the fixture has no database vendors.
            // Keep their canonical definitions without importing the unrelated vendor/stock seed corpus.
            foreach (string table in new[] { "vendors", "vendortemplate", "shopinventorytemplates" })
            {
                EnsureChestFixtureTable(connection, table);
            }
            foreach ((int owner, string account, int gm) in new[] { (Owner, Account, 1), (ChestPeerOwner, ChestPeerAccount, 1), (ChestNonGmOwner, ChestNonGmAccount, 0) })
            {
                using (var command = new MySqlCommand("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (@owner,'2026-10-24','fixture@invalid','','',@account,@hash,6,0,0,2047,@gm)", connection))
                {
                    command.Parameters.AddWithValue("@owner", owner);
                    command.Parameters.AddWithValue("@account", account);
                    command.Parameters.AddWithValue("@hash", PasswordHash.CreateHash(password));
                    command.Parameters.AddWithValue("@gm", gm);
                    command.ExecuteNonQuery();
                }
                using (var command = new MySqlCommand("INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES (@owner,@account,@name,'','',@pf,@x,@y,@z,1,0,0,0,0)", connection))
                {
                    command.Parameters.AddWithValue("@owner", owner);
                    command.Parameters.AddWithValue("@account", account);
                    command.Parameters.AddWithValue("@name", "ChestConnectedFixture" + owner);
                    command.Parameters.AddWithValue("@pf", ChestOriginPlayfield);
                    command.Parameters.AddWithValue("@x", terminal.Position.X);
                    command.Parameters.AddWithValue("@y", terminal.Position.Y);
                    command.Parameters.AddWithValue("@z", terminal.Position.Z);
                    command.ExecuteNonQuery();
                }
            }
            var stats = LifecycleSpawnFixture.Stats();
            stats[CharacterStat.Level] = 60;
            stats[CharacterStat.Side] = (int)Side.Omni;
            stats[CharacterStat.GmLevel] = 1;
            stats[CharacterStat.Cash] = 1000000;
            stats[CharacterStat.Health] = stats[CharacterStat.MaxHealth] = 1000000;
            stats[CharacterStat.ExternalPlayfieldInstance] = stats[CharacterStat.ExternalDoorInstance] = 0;
            foreach (var stat in stats)
                foreach (int owner in new[] { Owner, ChestPeerOwner, ChestNonGmOwner })
                    FixtureSql.Execute(connection, $"INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (50000,{owner},{(int)stat.Key},{(owner == ChestNonGmOwner && stat.Key == CharacterStat.GmLevel ? 0 : stat.Value)})");
            var repository = new MySqlInventoryRepository(new SilentLogger());
            int retainedItem = repository.LeaseInstanceIdBlock(1);
            repository.Insert(new ItemInstanceRecord { InstanceId = retainedItem, ContainerType = 104, ContainerInstance = Owner,
                ContainerPlacement = 90, ItemType = 0xC76D, LowId = 43384, HighId = 43384,
                Quality = 1, StackCount = 3, Source = AORebirth.Enums.ItemSource.Loot });
            int duplicator = repository.LeaseInstanceIdBlock(1);
            repository.Insert(new ItemInstanceRecord { InstanceId = duplicator, ContainerType = 104, ContainerInstance = Owner,
                ContainerPlacement = 91, ItemType = (int)IdentityType.MissionKeyDuplicator, LowId = keyPolicy.DuplicatorTemplateId,
                HighId = keyPolicy.DuplicatorTemplateId, Quality = 1, StackCount = 1, Source = AORebirth.Enums.ItemSource.Loot });
            var quests = new MySqlCharacterQuestDao(() => fixture.Open());
            var missions = new MySqlMissionDao(() => fixture.Open());
            var inventory = new MySqlCharacterPersistenceDao(() => fixture.Open());
            string retainedBefore = JsonSerializer.Serialize(inventory.LoadCarriedItems(Owner).Single(row => row.InstanceId == retainedItem));
            Console.WriteLine("SOURCE_SHA=" + Git("rev-parse", "HEAD"));
            Console.WriteLine("CONNECTED_CHEST_FIXTURE=LABELLED_DISPOSABLE_ONLY ONLINE_WRITES=NETWORK_ONLY STOPPED_ZONE_FIXTURE_NEGATIVE_SEED=1");
            using var world = new ConnectedEngineProcess(worldBinary, false, fixture, gameDataRoot, world: true);
            Console.WriteLine("CONNECTED_CHEST_WORLD=OWNED_LOOPBACK_ONLY BINARY_SHA256=" + world.BinarySha256);
            using var login = new ConnectedEngineProcess(loginBinary, true, fixture, gameDataRoot);
            string questId;
            QuestDungeonParameters parameters;
            string[] frozenFlags;
            MissionChestSnapshot chest;
            MissionChestSnapshot[] allChests;
            string planMetrics;
            PersistedItemData taken;
            PersistedItemData[] remainder;
            int firstProcess;
            using (var zone = new ConnectedEngineProcess(zoneBinary, false, fixture, gameDataRoot))
            {
                firstProcess = zone.Id;
                Console.WriteLine("CONNECTED_CHEST_ENGINE_PID_BEFORE=" + firstProcess + " BINARY_SHA256=" + zone.BinarySha256);
                using (var nonGm = EnterChest(fixture, password, out _, ChestNonGmAccount, ChestNonGmOwner))
                {
                    string before = ChestDiagnosticInventory(inventory, []);
                    nonGm.Send(new TextMessage { Message = new ChatMessage { Text = ".chest roll 1" } }, ChestNonGmOwner);
                    nonGm.Wait<ChatTextMessage>(message => message.Text.Contains("Insufficient GM level", StringComparison.Ordinal));
                    Require(before == ChestDiagnosticInventory(inventory, []), "chest-nongm-diagnostic-no-item-writes");
                    ChestLogout(nonGm, connection, ChestNonGmOwner);
                    Console.WriteLine("CONNECTED_CHEST_GM_PERMISSION=PASS NON_GM_REJECTED=YES VIA=TCP");
                }
                ZoneLoginMessage ticket;
                ZoneLoginMessage peerTicket;
                using (var terminalClient = EnterChest(fixture, password, out ticket))
                {
                    VerifyChestRollCommand(terminalClient, inventory, rkPolicy, 60, "player-level fallback", 20, []);
                    (questId, parameters) = AcceptCaveChestMission(terminalClient, missions, quests,
                        new Identity { Type = (IdentityType)terminal.IdentityType, Instance = terminal.IdentityInstance });
                    Require(quests.LoadKeyQuests(inventory.LoadCarriedItems(Owner).Where(row => row.LowId == keyPolicy.KeyTemplateId)
                        .Select(row => row.InstanceId).ToArray()).Values.Contains(questId), "chest-acceptance-key-link");
                    Console.WriteLine("CONNECTED_CHEST_GENERATE_ACCEPT=PASS VIA=TCP_CURRENT_QUEST_DUNGEON");
                    using (var peer = EnterChest(fixture, password, out peerTicket, ChestPeerAccount, ChestPeerOwner))
                    {
                        ShareChestKey(terminalClient, peer, inventory, quests, questId, duplicator, keyPolicy.KeyTemplateId);
                        RouteToChestEntrance(peer, parameters, world, ChestPeerOwner);
                    }
                    RouteToChestEntrance(terminalClient, parameters, world);
                }
                if (parameters.EntrancePlayfield != ChestOriginPlayfield)
                {
                    using var entranceClient = EnterChestTicket(fixture, ticket);
                    EnterChestEntrance(entranceClient, parameters, world);
                }
                if (parameters.EntrancePlayfield != ChestOriginPlayfield)
                {
                    using var peerEntrance = EnterChestTicket(fixture, peerTicket);
                    EnterChestEntrance(peerEntrance, parameters, world, ChestPeerOwner);
                }
                using (var dungeonClient = EnterChestTicket(fixture, ticket))
                using (var peer = EnterChestTicket(fixture, peerTicket))
                {
                    frozenFlags = ChestFlagSnapshot(missions, questId);
                    Require(frozenFlags.Length > 1, "chest-entry-materializes-nonempty-population");
                    allChests = ReadChestPopulation(missions, questId);
                    VerifyChestRollCommand(dungeonClient, inventory, rkPolicy, parameters.Quality, "accepted generated mission QL", 10000, allChests);
                    Require(ChestFlagSnapshot(missions, questId).SequenceEqual(frozenFlags), "chest-gm-diagnostic-preserves-mission-flags");
                    planMetrics = VisitChestPopulation(dungeonClient, zone, questId, allChests, "entry");
                    var nonempty = allChests
                        .Select(snapshot => (Snapshot: snapshot, Contents: inventory.LoadContainerItems(snapshot.ContainerInstance)
                            .OrderBy(row => row.ContainerPlacement).ToArray()))
                        .Where(value => value.Contents.Length > 0)
                        .OrderByDescending(value => value.Contents.Length).ToArray();
                    Require(nonempty.Length > 0, "fixture-limitation-no-naturally-nonempty-chest");
                    var selected = nonempty[0];
                    chest = selected.Snapshot;
                    PersistedItemData[] contents = selected.Contents;
                    Require(contents.Length is >= 1 and <= 3, "chest-natural-contents-count-within-policy");
                    MoveNearChest(dungeonClient, chest);
                    CheckChestSpawn(dungeonClient, chest);
                    InventoryUpdateMessage opened = OpenChest(dungeonClient, chest, contents);
                    MoveNearChest(peer, chest, ChestPeerOwner);
                    CheckChestSpawn(peer, chest);
                    VerifyNonOpenerCannotTake(peer, opened, contents[0], inventory, chest);
                    taken = contents[0];
                    TakeChestItem(dungeonClient, opened, taken, 80);
                    Until(() => inventory.LoadCarriedItems(Owner).Any(row => row.InstanceId == taken.InstanceId), "chest-first-loot-durable");
                    remainder = contents.Skip(1).ToArray();
                    int duplicateMark = dungeonClient.Received.Count;
                    SendChestTake(dungeonClient, opened, taken, 80);
                    CloseChest(dungeonClient, chest);
                    Require(!dungeonClient.Received.Skip(duplicateMark).OfType<ContainerAddItemMessage>().Any(), "chest-stale-duplicate-take-has-no-ack");
                    if (remainder.Length > 0)
                    {
                        OpenChest(dungeonClient, chest, remainder);
                    }
                    VerifyChestRows(inventory, chest, taken, remainder);
                    ChestLogout(peer, connection, ChestPeerOwner);
                    ChestLogout(dungeonClient, connection);
                    Console.WriteLine("CONNECTED_CHEST_ENTRY_OPEN_TAKE=PASS REMAINING=" + remainder.Length
                        + " PARTIAL_REOPEN=" + (remainder.Length > 0 ? "PASS" : "UNAVAILABLE_NATURAL_SINGLE_ITEM"));
                }
                if (remainder.Length == 0)
                {
                    zone.WaitForChestRetirement(chest.ContainerInstance);
                }
                using (var reconnected = EnterChest(fixture, password, out _))
                {
                    VerifyCarriedWire(reconnected, taken);
                    VisitChestPopulation(reconnected, zone, questId, allChests, "reconnect", planMetrics,
                        remainder.Length == 0 ? chest.ContainerInstance : null);
                    if (remainder.Length > 0)
                    {
                        MoveNearChest(reconnected, chest);
                        CheckChestSpawn(reconnected, chest);
                        OpenChest(reconnected, chest, remainder);
                    }
                    VerifyChestRows(inventory, chest, taken, remainder);
                    Require(ChestFlagSnapshot(missions, questId).SequenceEqual(frozenFlags), "chest-reconnect-frozen-identities-and-positions");
                    ChestLogout(reconnected, connection);
                    Console.WriteLine("CONNECTED_CHEST_AUTH_RECONNECT=PASS");
                }
                zone.Stop(allowProvenShutdownBaseline);
                usedProvenShutdownBaseline |= zone.UsedProvenShutdownBaseline;
            }
            // A labelled disposable fixture setup phase, with its owned Zone writer fully stopped.
            // Never inject rows into an online server or alter a previously generated/player-owned row.
            (PersistedItemData Row, string ExactJson) restricted = SeedRestrictedChestAuditRow(fixture, connection,
                firstProcess, repository, inventory, chest, allChests, data, rkPolicy, parameters.Quality);
            using (var zone = new ConnectedEngineProcess(zoneBinary, false, fixture, gameDataRoot))
            {
                Require(zone.Id != firstProcess, "chest-distinct-process-restart");
                Console.WriteLine("CONNECTED_CHEST_ENGINE_PID_AFTER=" + zone.Id + " BINARY_SHA256=" + zone.BinarySha256);
                using (var restarted = EnterChest(fixture, password, out _))
                {
                    VerifyCarriedWire(restarted, taken);
                    VisitChestPopulation(restarted, zone, questId, allChests, "restart", planMetrics);
                    Require(ChestFlagSnapshot(missions, questId).SequenceEqual(frozenFlags), "chest-restart-frozen-identities-and-positions");
                    var expectedOwned = remainder.Prepend(taken).ToList();
                    string peerLootCoverage;
                    using (var peer = EnterChest(fixture, password, out _, ChestPeerAccount, ChestPeerOwner))
                    {
                        var available = allChests.Select(snapshot => (Snapshot: snapshot,
                                Contents: inventory.LoadContainerItems(snapshot.ContainerInstance).Where(row => row.InstanceId != restricted.Row.InstanceId)
                                    .OrderBy(row => row.ContainerPlacement).ToArray()))
                            .Where(value => value.Contents.Length > 0)
                            .OrderBy(value => value.Snapshot.ContainerInstance == chest.ContainerInstance ? 0 : 1)
                            .ThenByDescending(value => value.Contents.Length).ToArray();
                        // Log in both players before opening an empty reconstructed chest starts its existing 1s retirement.
                        MoveNearChest(restarted, chest);
                        CheckChestSpawn(restarted, chest);
                        MoveNearChest(peer, chest, ChestPeerOwner);
                        CheckChestSpawn(peer, chest);
                        InventoryUpdateMessage restrictedOpened = OpenChest(restarted, chest, remainder);
                        int restrictedMark = restarted.Received.Count;
                        Identity restrictedSource = SendChestTake(restarted, restrictedOpened, restricted.Row, 87);
                        restarted.Send(new CharacterActionMessage { Identity = Character, Action = CharacterActionType.DeleteItem,
                            Target = restrictedSource, Parameter1 = restricted.Row.LowId, Parameter2 = restricted.Row.HighId }, Owner);
                        CloseChest(restarted, chest);
                        Require(!restarted.Received.Skip(restrictedMark).OfType<ContainerAddItemMessage>()
                                .Any(message => message.SourceContainer == restrictedSource)
                            && !restarted.Received.Skip(restrictedMark).OfType<CharacterActionMessage>()
                                .Any(message => message.Action == CharacterActionType.DeleteItem && message.Target == restrictedSource),
                            "chest-restricted-stale-move-and-delete-have-no-ack");
                        VerifyRestrictedChestAuditRow(inventory, restricted, chest);
                        Console.WriteLine("CONNECTED_CHEST_RESTRICTED_PERSISTED=PASS HIDDEN_ON_RESTART=YES STALE_MOVE=REJECTED STALE_DELETE=REJECTED ORIGINAL_MYSQL_ROW=RETAINED");
                        if (available.Length > 0)
                        {
                            MissionChestSnapshot peerChest = available[0].Snapshot;
                            PersistedItemData[] peerContents = available[0].Contents;
                            Require(peerContents.Length is >= 1 and <= 3, "chest-peer-natural-contents-count-within-policy");
                            if (peerChest.ContainerInstance != chest.ContainerInstance)
                            {
                                expectedOwned.AddRange(peerContents);
                                MoveNearChest(restarted, peerChest);
                                CheckChestSpawn(restarted, peerChest);
                                OpenChest(restarted, peerChest, peerContents);
                                CloseChest(restarted, peerChest);
                            }
                            MoveNearChest(peer, peerChest, ChestPeerOwner);
                            CheckChestSpawn(peer, peerChest);
                            InventoryUpdateMessage peerOpened = OpenChest(peer, peerChest, peerContents, ChestPeerOwner);
                            TakeChestItem(peer, peerOpened, peerContents[0], 80, ChestPeerOwner);
                            int duplicateMark = peer.Received.Count;
                            SendChestTake(peer, peerOpened, peerContents[0], 81, ChestPeerOwner);
                            CloseChest(peer, peerChest, ChestPeerOwner);
                            Require(!peer.Received.Skip(duplicateMark).OfType<ContainerAddItemMessage>().Any(), "chest-peer-stale-duplicate-take-has-no-ack");
                            Require(inventory.LoadCarriedItems(ChestPeerOwner).Count(item => item.InstanceId == peerContents[0].InstanceId) == 1,
                                "chest-second-player-keeps-one-original-instance");
                            MoveNearChest(restarted, peerChest);
                            InventoryUpdateMessage opened = OpenChest(restarted, peerChest, peerContents.Skip(1).ToArray());
                            for (int index = 1; index < peerContents.Length; index++)
                                TakeChestItem(restarted, opened, peerContents[index], 80 + index);
                            Require(inventory.LoadContainerItems(peerChest.ContainerInstance).Count(row => row.InstanceId != restricted.Row.InstanceId) == 0,
                                "chest-peer-target-approved-loot-empty-after-restart");
                            CloseChest(restarted, peerChest);
                            OpenChest(restarted, peerChest, []);
                            peerLootCoverage = "PASS CHEST=" + peerChest.ContainerInstance + " ORIGINAL_SELECTED_CHEST=" + chest.ContainerInstance;
                        }
                        else
                        {
                            InventoryUpdateMessage empty = OpenChest(peer, chest, [], ChestPeerOwner);
                            int duplicateMark = peer.Received.Count;
                            SendChestTake(peer, empty, taken, 80, ChestPeerOwner);
                            CloseChest(peer, chest, ChestPeerOwner);
                            Require(!peer.Received.Skip(duplicateMark).OfType<ContainerAddItemMessage>().Any()
                                && !inventory.LoadCarriedItems(ChestPeerOwner).Any(item => item.InstanceId == taken.InstanceId)
                                && inventory.LoadCarriedItems(Owner).Count(item => item.InstanceId == taken.InstanceId) == 1,
                                "chest-peer-empty-reconstructed-stale-take-refused");
                            peerLootCoverage = "UNAVAILABLE_EXHAUSTED_POPULATION EMPTY_OPEN_AND_STALE_TAKE=PASS";
                        }
                        Require(inventory.LoadContainerItems(chest.ContainerInstance).Count(row => row.InstanceId != restricted.Row.InstanceId) == 0,
                            "chest-approved-loot-empty-after-restart");
                        VerifyRestrictedChestAuditRow(inventory, restricted, chest);
                        ChestLogout(peer, connection, ChestPeerOwner);
                    }
                    Require(JsonSerializer.Serialize(inventory.LoadCarriedItems(Owner).Single(row => row.InstanceId == retainedItem)) == retainedBefore,
                        "chest-unrelated-player-item-preserved");
                    foreach (PersistedItemData row in expectedOwned)
                    {
                        PersistedItemData[] owned = inventory.LoadCarriedItems(Owner).Concat(inventory.LoadCarriedItems(ChestPeerOwner))
                            .Where(item => item.InstanceId == row.InstanceId).ToArray();
                        Require(owned.Length == 1 && owned[0].LowId == row.LowId && owned[0].HighId == row.HighId
                            && owned[0].Quality == row.Quality && owned[0].StackCount == row.StackCount && owned[0].Source == row.Source,
                            "chest-original-instance-owned-once-with-exact-definition");
                    }
                    ChestLogout(restarted, connection);
                    Console.WriteLine("CONNECTED_CHEST_DISTINCT_PROCESS_RESTART=PASS FROZEN_POPULATION=PASS EMPTY_REOPEN=PASS PLAYER_RETENTION=PASS");
                    Console.WriteLine("CONNECTED_CHEST_TWO_TCP_PLAYERS=PASS NONOPENER_REJECTED=PASS OPENER_HANDOFF=PASS DUPLICATE_TAKES=REJECTED");
                    Console.WriteLine("CONNECTED_CHEST_PEER_LOOT=" + peerLootCoverage);
                }
                zone.Stop(allowProvenShutdownBaseline);
                usedProvenShutdownBaseline |= zone.UsedProvenShutdownBaseline;
            }
            login.Stop();
            world.Stop();
            Console.WriteLine("CONNECTED_MISSION_CHEST_ACCEPTANCE="
                + (usedProvenShutdownBaseline ? "PASS_WITH_PROVEN_BASELINE_EXCEPTIONS" : "PASS")
                + " PRODUCTION_CONTACT=NO ORIGINAL_CLIENT_ACCEPTANCE=NOT_EXERCISED");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previousConnection);
            Environment.SetEnvironmentVariable("AO_REBIRTH_GAMEDATA_PATH", previousGameData);
        }
    }

    static void EnsureChestFixtureTable(MySqlConnection connection, string table, bool wholeFile = false)
    {
        using var check = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name=@table", connection);
        check.Parameters.AddWithValue("@table", table);
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            return;
        string sql = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "AORebirth/Libraries/Source/AORebirth.Database/SqlTables/" + table + ".sql"));
        FixtureSql.Execute(connection, wholeFile ? sql : sql.Substring(0, sql.IndexOf(';') + 1));
    }

    static (string Quest, QuestDungeonParameters Parameters) AcceptCaveChestMission(ConnectedWireClient client,
        MySqlMissionDao missions, MySqlCharacterQuestDao quests, Identity terminal)
    {
        var seen = quests.LoadGeneratedForCharacter(Owner).Select(row => row.QuestId).ToHashSet(StringComparer.Ordinal);
        for (int attempt = 1; attempt <= MaximumCaveAcceptances; attempt++)
        {
            client.Send(new QuestAlternativeMessage { Identity = Character, VersionId = 4, MissionTerminalIdentity = terminal,
                LevelSlider = 6, GoodBadSlider = 0, OrderChaosSlider = 0, OpenHiddenSlider = 0,
                PhysicalMysticalSlider = 0, HeadOnStealthSlider = 0, MoneyExperienceSlider = 0, QuestInfos = [] }, Owner);
            client.Wait<QuestAlternativeMessage>(message => message.QuestInfos.Length == 5);
            GeneratedMissionOffer offer = missions.ReadOffers(Owner).Where(value => value.State == GeneratedMissionState.Offered)
                .OrderBy(value => value.MissionType == 2 ? 0 : 1).First(value => value.MissionType != 4);
            client.Send(new CreateQuestMessage { Identity = Character,
                QuestIdentity = new Identity { Type = (IdentityType)offer.OfferType, Instance = offer.OfferInstance } }, Owner);
            QuestFullUpdateMessage assigned = client.Wait<QuestFullUpdateMessage>(message => message.Quests.Length > 0);
            GeneratedQuestRow row = quests.LoadGeneratedForCharacter(Owner).Single(value => !seen.Contains(value.QuestId));
            seen.Add(row.QuestId);
            Require(QuestDungeonParameters.TryParse(row.AcgBuildingGeneratorJson, out QuestDungeonParameters parameters)
                && parameters.LayoutPlan != null, "chest-accepted-current-frozen-layout");
            Console.WriteLine("CONNECTED_CHEST_STYLE_ATTEMPT=" + attempt + " QUEST=" + row.QuestId
                + " STYLE=" + parameters.LayoutPlan!.StyleId + " FLOORS=" + parameters.LayoutPlan.FloorCount + " SEED=" + parameters.Seed);
            if (parameters.LayoutPlan.StyleId == 341)
                return (row.QuestId, parameters);
            Identity journal = assigned.Quests.Single().QuestId;
            client.Send(new QuestMessage { Identity = Character, Action = QuestAction.Delete, Mission = journal }, Owner);
            client.Wait<QuestMessage>(message => message.Action == QuestAction.Delete && message.Mission == journal);
        }
        throw new FixtureFailure("fixture-limitation-no-cave-accepted-within-" + MaximumCaveAcceptances);
    }

    static void RouteToChestEntrance(ConnectedWireClient client, QuestDungeonParameters parameters, ConnectedEngineProcess world, int owner = Owner)
    {
        client.Send(new TextMessage { Message = new ChatMessage { Text = FormattableString.Invariant(
            $".tp {parameters.EntranceX:R} {parameters.EntranceY:R} {parameters.EntranceZ:R} {parameters.EntrancePlayfield}") } }, owner);
        client.Wait<N3TeleportMessage>();
        if (parameters.EntrancePlayfield == ChestOriginPlayfield)
        {
            EnterChestEntrance(client, parameters, world, owner);
            return;
        }
        client.Wait<ZoneRedirectionMessage>();
    }

    static void EnterChestEntrance(ConnectedWireClient client, QuestDungeonParameters parameters, ConnectedEngineProcess world, int owner = Owner)
    {
        var entrance = new Identity { Type = IdentityType.MissionEntrance, Instance = parameters.EntranceInstance };
        bool needsAssignment = !world.HasPlayfieldAssignment(parameters.DungeonPlayfield);
        int mark = client.Received.Count;
        SendChestUse(client, entrance, owner);
        bool retried = false;
        if (needsAssignment)
        {
            GenericCmdMessage response = client.Wait<GenericCmdMessage>(message => message.Action == GenericCmdAction.Use
                && message.Target.Contains(entrance));
            Require(response.Temp1 == 1, "chest-entrance-world-resolution-use-accepted");
            if (!client.Received.Skip(mark).OfType<N3TeleportMessage>().Any())
            {
                world.WaitForPlayfieldAssignment(parameters.DungeonPlayfield);
                // Current TryEnter consumes its 750ms cooldown before asynchronous World resolution.
                // One subsequent player Use follows the real assignment; it never manufactures a route.
                Thread.Sleep(800);
                SendChestUse(client, entrance, owner);
                retried = true;
            }
        }
        if (!client.Received.Skip(mark).OfType<N3TeleportMessage>().Any())
        {
            client.Wait<N3TeleportMessage>();
        }
        if (!client.Received.Skip(mark).OfType<ZoneRedirectionMessage>().Any())
        {
            client.Wait<ZoneRedirectionMessage>();
        }
        Console.WriteLine("CONNECTED_CHEST_ENTRANCE=PASS OWNER=" + owner + " WORLD_RESOLVE_RETRY=" + (retried ? 1 : 0));
    }

    static void ShareChestKey(ConnectedWireClient owner, ConnectedWireClient peer, MySqlCharacterPersistenceDao inventory,
        MySqlCharacterQuestDao quests, string questId, int duplicator, int keyTemplateId)
    {
        PersistedItemData original = inventory.LoadCarriedItems(Owner).Single(item => item.LowId == keyTemplateId
            && quests.LoadKeyQuests(new[] { item.InstanceId }).TryGetValue(item.InstanceId, out string? linked) && linked == questId);
        PersistedItemData tool = inventory.LoadCarriedItems(Owner).Single(item => item.InstanceId == duplicator);
        owner.Send(new GenericCmdMessage { Identity = Character, User = Character, Temp1 = 0, Count = 21, Temp4 = 0,
            Action = GenericCmdAction.UseItemOnItem, Target = [new Identity { Type = IdentityType.Inventory, Instance = tool.ContainerPlacement },
                new Identity { Type = IdentityType.Inventory, Instance = original.ContainerPlacement }] }, Owner);
        owner.Wait<GenericCmdMessage>(message => message.Action == GenericCmdAction.UseItemOnItem && message.Temp1 == 1);
        PersistedItemData copy = inventory.LoadCarriedItems(Owner).Single(item => item.LowId == keyTemplateId
            && item.InstanceId != original.InstanceId && quests.LoadKeyQuests(new[] { item.InstanceId })
                .TryGetValue(item.InstanceId, out string? linked) && linked == questId);
        var peerIdentity = ChestCharacter(ChestPeerOwner);
        owner.Send(new TradeMessage { Identity = Character, Action = TradeAction.Open, Target = peerIdentity }, Owner);
        owner.Wait<TradeMessage>(message => message.Action == TradeAction.Open);
        peer.Wait<TradeMessage>(message => message.Action == TradeAction.Open);
        owner.Send(new TradeMessage { Identity = Character, Action = TradeAction.AddItem, Target = Character,
            Container = new Identity { Type = IdentityType.Inventory, Instance = copy.ContainerPlacement } }, Owner);
        owner.Wait<TradeMessage>(message => message.Action == TradeAction.AddItem);
        owner.Send(new TradeMessage { Identity = Character, Action = TradeAction.Confirm }, Owner);
        peer.Send(new TradeMessage { Identity = peerIdentity, Action = TradeAction.Confirm }, ChestPeerOwner);
        owner.Wait<TradeMessage>(message => message.Action == TradeAction.Confirm);
        peer.Wait<TradeMessage>(message => message.Action == TradeAction.Confirm);
        owner.Send(new TradeMessage { Identity = Character, Action = TradeAction.Accept }, Owner);
        peer.Send(new TradeMessage { Identity = peerIdentity, Action = TradeAction.Accept }, ChestPeerOwner);
        owner.Wait<TradeMessage>(message => message.Action == TradeAction.Complete);
        peer.Wait<TradeMessage>(message => message.Action == TradeAction.Complete);
        Require(inventory.LoadCarriedItems(ChestPeerOwner).Count(item => item.InstanceId == copy.InstanceId) == 1
            && !inventory.LoadCarriedItems(Owner).Any(item => item.InstanceId == copy.InstanceId)
            && quests.LoadKeyQuests(new[] { original.InstanceId, copy.InstanceId }).Values.All(value => value == questId),
            "chest-second-player-receives-one-linked-key-through-tcp-trade");
        // Key copy and entrance use share the existing 750ms request cooldown.
        Thread.Sleep(800);
        Console.WriteLine("CONNECTED_CHEST_KEY_COPY_AND_TRADE=PASS TWO_ACCOUNTS=PASS");
    }

    static MissionChestSnapshot[] ReadChestPopulation(MySqlMissionDao missions, string quest)
    {
        MissionFlagData[] flags = missions.ReadCharacter(Owner).Flags.Where(flag => flag.QuestId == quest
            && flag.FlagKey.StartsWith("mission-chest:1:", StringComparison.Ordinal)).ToArray();
        MissionFlagData[] rows = flags.Where(flag => flag.FlagKey.Contains(":chest:", StringComparison.Ordinal)).ToArray();
        Require(flags.Length == rows.Length + 1 && int.TryParse(flags.Single(flag => !flag.FlagKey.Contains(":chest:", StringComparison.Ordinal)).Value,
            out int count) && count == rows.Length && count > 0 && count <= 128, "chest-runtime-seal-matches-bounded-persisted-population");
        return rows.Select(row => JsonSerializer.Deserialize<MissionChestSnapshot>(row.Value)!).ToArray();
    }

    static string VisitChestPopulation(ConnectedWireClient client, ConnectedEngineProcess zone, string quest,
        MissionChestSnapshot[] snapshots, string phase, string? originalPlanMetrics = null, int? exhaustedContainer = null)
    {
        zone.WaitForChestPopulation(quest, snapshots.Length);
        string metrics = originalPlanMetrics ?? zone.ChestPlanMetrics(quest, snapshots.Length);
        MissionChestSnapshot[] active = snapshots.Where(snapshot => snapshot.ContainerInstance != exhaustedContainer).ToArray();
        foreach (MissionChestSnapshot snapshot in active)
        {
            MoveNearChest(client, snapshot);
            CheckChestSpawn(client, snapshot);
        }
        var expected = active.Select(snapshot => snapshot.ContainerInstance).ToHashSet();
        int published = client.Received.OfType<ChestItemFullUpdateMessage>().Where(message => expected.Contains(message.Identity.Instance))
            .Select(message => message.Identity.Instance).Distinct().Count();
        Require(published == active.Length, "chest-runtime-every-required-world-object-published-" + phase);
        Console.WriteLine("CONNECTED_CHEST_RUNTIME_COUNTS PHASE=" + phase + " QUEST=" + quest
            + " " + metrics + " PERSISTED=" + snapshots.Length + " INSTANTIATED_ON_LOAD=" + snapshots.Length
            + " EXPECTED_ACTIVE=" + active.Length + " PUBLISHED_UNIQUE=" + published
            + " EXHAUSTED_RETIRED=" + (snapshots.Length - active.Length));
        return metrics;
    }

    static void VerifyNonOpenerCannotTake(ConnectedWireClient peer, InventoryUpdateMessage opened, PersistedItemData item,
        MySqlCharacterPersistenceDao inventory, MissionChestSnapshot chest)
    {
        int mark = peer.Received.Count;
        SendChestTake(peer, opened, item, 80, ChestPeerOwner);
        SendChestUse(peer, new Identity { Type = IdentityType.Container, Instance = chest.ContainerInstance }, ChestPeerOwner);
        GenericCmdMessage refused = peer.Wait<GenericCmdMessage>(message => message.Action == GenericCmdAction.Use);
        Require(refused.Temp1 != 1 && !peer.Received.Skip(mark).OfType<ContainerAddItemMessage>().Any()
            && !peer.Received.Skip(mark).OfType<InventoryUpdateMessage>().Any(message => message.BagIdentity.Instance == chest.ContainerInstance)
            && inventory.LoadContainerItems(chest.ContainerInstance).Any(row => row.InstanceId == item.InstanceId)
            && !inventory.LoadCarriedItems(ChestPeerOwner).Any(row => row.InstanceId == item.InstanceId),
            "chest-nonopener-cannot-open-or-take-another-players-chest");
    }

    static (PersistedItemData Row, string ExactJson) SeedRestrictedChestAuditRow(DisposableSchemaDatabase fixture,
        MySqlConnection connection, int stoppedZonePid, MySqlInventoryRepository repository,
        MySqlCharacterPersistenceDao inventory, MissionChestSnapshot chest, MissionChestSnapshot[] allChests,
        GameDataStore data, MissionChestRKAllowlist policy, int quality)
    {
        bool writerAlive;
        try { using var writer = System.Diagnostics.Process.GetProcessById(stoppedZonePid); writerAlive = !writer.HasExited; }
        catch (ArgumentException) { writerAlive = false; }
        var bound = new MySqlConnectionStringBuilder(fixture.ConnectionString);
        var actual = new MySqlConnectionStringBuilder(connection.ConnectionString);
        Require(!writerAlive && connection.Database == DisposableSchemaDatabase.DatabaseName
            && bound.Server == "127.0.0.1" && actual.Server == bound.Server && actual.Port == bound.Port
            && actual.UserID == bound.UserID
            && Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION") == fixture.ConnectionString,
            "restricted-fixture-seed-requires-owned-disposable-database-and-stopped-zone");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM characters WHERE Id IN ({Owner},{ChestPeerOwner},{ChestNonGmOwner}) AND Online<>0") == 0,
            "restricted-fixture-seed-requires-all-fixture-players-logged-out");
        var catalog = new ItemTemplateCatalog(data, new SilentLogger());
        ItemTemplate excluded = catalog.Templates.OrderBy(item => item.Id).FirstOrDefault(item =>
            item.Quality >= Math.Max(1, quality - 5) && item.Quality <= Math.Min(250, quality + 5)
            && item.Stats.TryGetValue(CharacterStat.Can, out int can) && (can & (int)AORebirth.Enums.CanFlags.Carry) != 0
            && policy.ExclusionReasons(item.Id, item.Id).Length > 0)
            ?? throw new FixtureFailure("restricted-fixture-no-reviewed-negative-template-at-accepted-quality");
        PersistedItemData[] original = inventory.LoadContainerItems(chest.ContainerInstance).OrderBy(row => row.ContainerPlacement).ToArray();
        Require(original.Length <= 2, "restricted-fixture-seed-must-use-newly-freed-original-slot");
        int slot = Enumerable.Range(0, 3).Except(original.Select(row => row.ContainerPlacement)).First();
        string before = ChestDiagnosticInventory(inventory, allChests);
        int id = repository.LeaseInstanceIdBlock(1);
        repository.Insert(new ItemInstanceRecord { InstanceId = id, ContainerType = (int)IdentityType.Container,
            ContainerInstance = chest.ContainerInstance, ContainerPlacement = slot, ItemType = 0xC76D,
            LowId = excluded.Id, HighId = excluded.Id, Quality = excluded.Quality, StackCount = 1,
            Source = AORebirth.Enums.ItemSource.Loot });
        Require(before == ChestDiagnosticInventory(inventory, allChests, id), "restricted-fixture-seed-preserves-every-original-player-and-chest-row");
        PersistedItemData row = inventory.LoadContainerItems(chest.ContainerInstance).Single(item => item.InstanceId == id);
        Require(!policy.IsAllowed(row.LowId, row.HighId, row.Quality), "restricted-fixture-uses-actual-reviewed-negative");
        Console.WriteLine("CONNECTED_CHEST_STOPPED_FIXTURE_NEGATIVE_SEED=PASS REASONS="
            + string.Join(",", policy.ExclusionReasons(row.LowId, row.HighId)) + " ORIGINAL_ROWS_CHANGED=0");
        return (row, JsonSerializer.Serialize(row));
    }

    static void VerifyRestrictedChestAuditRow(MySqlCharacterPersistenceDao inventory,
        (PersistedItemData Row, string ExactJson) restricted, MissionChestSnapshot chest)
    {
        Require(inventory.LoadContainerItems(chest.ContainerInstance).Where(row => row.InstanceId == restricted.Row.InstanceId)
                .Select(row => JsonSerializer.Serialize(row)).Single() == restricted.ExactJson,
            "chest-restricted-audit-row-retains-exact-original-definition-slot-and-owner");
        Require(new[] { Owner, ChestPeerOwner, ChestNonGmOwner }.All(owner =>
                inventory.LoadCarriedItems(owner).All(row => row.InstanceId != restricted.Row.InstanceId)),
            "chest-restricted-audit-item-never-enters-player-inventory");
    }

    static string ChestDiagnosticInventory(MySqlCharacterPersistenceDao inventory, MissionChestSnapshot[] chests, int excludedInstance = 0)
        => JsonSerializer.Serialize(new
        {
            Players = new[] { Owner, ChestPeerOwner, ChestNonGmOwner }.Select(owner => new
            {
                Owner = owner, Items = inventory.LoadCarriedItems(owner).Where(row => row.InstanceId != excludedInstance).OrderBy(row => row.InstanceId).ToArray()
            }).ToArray(),
            Chests = chests.OrderBy(chest => chest.ContainerInstance).Select(chest => new
            {
                chest.ContainerInstance,
                Items = inventory.LoadContainerItems(chest.ContainerInstance).Where(row => row.InstanceId != excludedInstance).OrderBy(row => row.ContainerPlacement).ToArray()
            }).ToArray()
        });

    static void VerifyChestRollCommand(ConnectedWireClient client, MySqlCharacterPersistenceDao inventory,
        MissionChestRKAllowlist policy, int quality, string qualitySource, int populations, MissionChestSnapshot[] chests)
    {
        string before = ChestDiagnosticInventory(inventory, chests);
        foreach (string invalid in new[] { ".chest roll 0", ".chest roll 10001" })
        {
            client.Send(new TextMessage { Message = new ChatMessage { Text = invalid } }, Owner);
            client.Wait<ChatTextMessage>(message => message.Text.Contains("Usage:", StringComparison.Ordinal)
                && !message.Text.Contains("text://", StringComparison.Ordinal));
            // SendUsage sends the error and then its help popup. Drain this call's
            // second response so it cannot satisfy the next call or pollute roll-page accounting.
            client.Wait<ChatTextMessage>(message => message.Text.Contains(">.chest help</a>", StringComparison.Ordinal));
        }
        int mark = client.Received.Count;
        client.Send(new TextMessage { Message = new ChatMessage { Text = ".chest roll " + populations } }, Owner);
        ChatTextMessage first = client.Wait<ChatTextMessage>(message => message.Text.Contains("Chest loot roll results", StringComparison.Ordinal));
        var page = System.Text.RegularExpressions.Regex.Match(first.Text, @"Chest loot roll results \(1/(\d+)\)");
        int pages = page.Success ? int.Parse(page.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
        Require(pages is >= 1 and <= 32, "chest-gm-bounded-output-pages");
        for (int index = 2; index <= pages; index++)
        {
            string label = $"Chest loot roll results ({index}/{pages})";
            client.Wait<ChatTextMessage>(message => message.Text.Contains(label, StringComparison.Ordinal));
        }
        ChatTextMessage[] messages = client.Received.Skip(mark).OfType<ChatTextMessage>().ToArray();
        Console.WriteLine($"CONNECTED_CHEST_GM_OUTPUT_FRAMING PAGES={pages} RECEIVED_CHAT={messages.Length} MAX_LENGTH={messages.Max(message => message.Text.Length)}");
        Require(messages.Length == pages && messages.All(message => message.Text.Length <= 4224), "chest-gm-bounded-output");
        string text = string.Join("\n", messages.Select(message => message.Text));
        Require(text.Contains($"Chest populations: {populations}; total generated items:", StringComparison.Ordinal)
            && text.Contains($"QL {quality}: {qualitySource}", StringComparison.Ordinal)
            && text.Contains("classification OrdinaryRubiKa", StringComparison.Ordinal)
            && text.Contains("rarity", StringComparison.Ordinal), "chest-gm-production-result-details");
        var items = System.Text.RegularExpressions.Regex.Matches(text, @"ID (\d+)/(\d+); QL (\d+); rarity ");
        Require(items.Count is >= 1 and <= 100, "chest-gm-bounded-item-details");
        foreach (System.Text.RegularExpressions.Match item in items)
        {
            int low = int.Parse(item.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            int high = int.Parse(item.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            int itemQuality = int.Parse(item.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            Require(policy.IsAllowed(low, high, itemQuality) && itemQuality >= Math.Max(1, quality - 5)
                && itemQuality <= Math.Min(250, quality + 5), "chest-gm-only-approved-rk-details");
        }
        Require(before == ChestDiagnosticInventory(inventory, chests), "chest-gm-readonly-player-and-chest-inventory");
        Console.WriteLine($"CONNECTED_CHEST_GM_ROLL=PASS POPULATIONS={populations} MISSION_QL={quality} PAGES={pages} DISPLAYED={items.Count} INVENTORY_WRITES=0 VIA=TCP");
    }

    static Identity ChestCharacter(int owner) => new() { Type = IdentityType.CanbeAffected, Instance = owner };

    static ConnectedWireClient EnterChest(DisposableSchemaDatabase fixture, string password, out ZoneLoginMessage ticket,
        string account = Account, int owner = Owner)
    {
        ticket = Authorize(fixture, password, account, owner);
        return EnterChestTicket(fixture, ticket);
    }

    static ConnectedWireClient EnterChestTicket(DisposableSchemaDatabase fixture, ZoneLoginMessage ticket)
    {
        var client = new ConnectedWireClient(fixture.ZonePort);
        try
        {
            Identity character = ChestCharacter(ticket.CharacterId);
            client.Send(ticket);
            client.Wait<FullCharacterMessage>(message => message.Identity == character);
            client.Wait<SpecialAttackWeaponMessage>(message => message.Identity == character);
            client.Send(new CharInPlayMessage { Identity = character }, ticket.CharacterId);
            client.Wait<CharInPlayMessage>(message => message.Identity == character);
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    static void ChestLogout(ConnectedWireClient client, MySqlConnection connection, int owner = Owner)
    {
        client.Send(new CharacterActionMessage { Identity = ChestCharacter(owner), Action = CharacterActionType.Logout }, owner);
        client.Wait<StartLogoutMessage>();
        Until(() => FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={owner}") == 0, "chest-logout");
    }

    static string[] ChestFlagSnapshot(MySqlMissionDao missions, string quest)
        => missions.ReadCharacter(Owner).Flags.Where(flag => flag.QuestId == quest && flag.FlagKey.StartsWith("mission-chest:1:", StringComparison.Ordinal))
            .OrderBy(flag => flag.FlagKey, StringComparer.Ordinal).Select(flag => flag.FlagKey + "=" + flag.Value).ToArray();

    static void CheckChestSpawn(ConnectedWireClient client, MissionChestSnapshot chest)
    {
        var identity = new Identity { Type = IdentityType.Container, Instance = chest.ContainerInstance };
        ChestItemFullUpdateMessage spawn = client.Received.OfType<ChestItemFullUpdateMessage>().FirstOrDefault(message => message.Identity == identity)
            ?? client.Wait<ChestItemFullUpdateMessage>(message => message.Identity == identity);
        Require(spawn.Coordinate.X == chest.X && spawn.Coordinate.Y == chest.Y && spawn.Coordinate.Z == chest.Z,
            "chest-spawn-frozen-transform");
        Require(spawn.StateMachine.Type == (IdentityType)chest.SpawnDefinition!.StateMachineType
            && spawn.StateMachine.Instance == chest.SpawnDefinition.StateMachineInstance, "chest-spawn-frozen-appearance");
    }

    static void MoveNearChest(ConnectedWireClient client, MissionChestSnapshot chest, int owner = Owner)
    {
        double yaw = chest.YawDegrees * Math.PI / 180;
        // Direct-control Update uses the production movement handler. This is not a walking/navigation claim.
        client.Send(new CharDCMoveMessage { Identity = ChestCharacter(owner), MoveType = (byte)MovementAction.Update,
            Heading = new Quaternion { X = 0, Y = 0, Z = 0, W = 1 },
            Coordinates = new Vector3(chest.X + (float)Math.Sin(yaw) * 1.5f, chest.Y, chest.Z + (float)Math.Cos(yaw) * 1.5f) }, owner);
    }

    static void SendChestUse(ConnectedWireClient client, Identity target, int owner = Owner)
        => client.Send(new GenericCmdMessage { Identity = ChestCharacter(owner), Temp1 = 0, Count = 21,
            Action = GenericCmdAction.Use, Temp4 = 1, User = ChestCharacter(owner), Target = [target] }, owner);

    static InventoryUpdateMessage OpenChest(ConnectedWireClient client, MissionChestSnapshot chest, PersistedItemData[] expected, int owner = Owner)
    {
        var identity = new Identity { Type = IdentityType.Container, Instance = chest.ContainerInstance };
        SendChestUse(client, identity, owner);
        InventoryUpdateMessage opened = client.Wait<InventoryUpdateMessage>(message => message.BagIdentity == identity);
        Require(opened.NumberOfSlots == Chest.LootCapacity && opened.SlotnumberInMainInventory > 0
            && opened.Entries.Length == expected.Length, "chest-open-original-contents-contract");
        foreach (PersistedItemData row in expected)
            Require(opened.Entries.Any(entry => entry.Identity.Instance == row.InstanceId && entry.Slotnumber == row.ContainerPlacement
                && entry.LowId == row.LowId && entry.HighId == row.HighId && entry.Quality == row.Quality), "chest-open-exact-remaining-instance");
        client.Wait<GenericCmdMessage>(message => message.Action == GenericCmdAction.Use && message.Temp1 == 1 && message.Target.Contains(identity));
        return opened;
    }

    static void CloseChest(ConnectedWireClient client, MissionChestSnapshot chest, int owner = Owner)
    {
        var identity = new Identity { Type = IdentityType.Container, Instance = chest.ContainerInstance };
        SendChestUse(client, identity, owner);
        client.Wait<ActionMessage>(message => message.Identity == identity && message.ActionIdentity == 0x66);
        client.Wait<GenericCmdMessage>(message => message.Action == GenericCmdAction.Use && message.Temp1 == 1 && message.Target.Contains(identity));
    }

    static void TakeChestItem(ConnectedWireClient client, InventoryUpdateMessage opened, PersistedItemData row, int targetSlot, int owner = Owner)
    {
        Identity source = SendChestTake(client, opened, row, targetSlot, owner);
        client.Wait<ContainerAddItemMessage>(message => message.Identity == ChestCharacter(owner) && message.SourceContainer == source && message.TargetPlacement == targetSlot);
    }

    static Identity SendChestTake(ConnectedWireClient client, InventoryUpdateMessage opened, PersistedItemData row, int targetSlot, int owner = Owner)
    {
        var source = new Identity { Type = IdentityType.Backpack, Instance = opened.SlotnumberInMainInventory << 16 | row.ContainerPlacement };
        client.Send(new ClientMoveItemToInventoryMessage { Identity = ChestCharacter(owner), SourceContainer = source, TargetPlacement = targetSlot }, owner);
        return source;
    }

    static void VerifyChestRows(MySqlCharacterPersistenceDao inventory, MissionChestSnapshot chest, PersistedItemData taken, PersistedItemData[] remainder)
    {
        PersistedItemData carried = inventory.LoadCarriedItems(Owner).Single(row => row.InstanceId == taken.InstanceId);
        Require(carried.ContainerType == 104 && carried.ContainerInstance == Owner && carried.LowId == taken.LowId
            && carried.HighId == taken.HighId && carried.Quality == taken.Quality && carried.StackCount == taken.StackCount && carried.Source == taken.Source,
            "chest-looted-item-exact-durable-owner-and-definition");
        Require(inventory.LoadContainerItems(chest.ContainerInstance).OrderBy(row => row.ContainerPlacement).Select(row => JsonSerializer.Serialize(row))
            .SequenceEqual(remainder.Select(row => JsonSerializer.Serialize(row))), "chest-remaining-durable-rows-unchanged");
    }

    static void VerifyCarriedWire(ConnectedWireClient client, PersistedItemData row)
        => Require(client.Received.OfType<FullCharacterMessage>().Single(message => message.Identity == Character).InventorySlots
            .Count(slot => slot.Identity.Instance == row.InstanceId && slot.Placement == 80 && slot.ItemLowId == row.LowId
                && slot.ItemHighId == row.HighId && slot.Quality == row.Quality) == 1, "chest-reconnect-carried-item-wire");
}
