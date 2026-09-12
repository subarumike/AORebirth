using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Linq;
using MySqlConnector;
using AORebirth.Core.Encryption;
using AORebirth.LinuxBuild.Stage7MySqlSecurityIntegrationTests;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Missions;
using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;

/// <summary>Administrative setup ends before startup. Every subsequent mutation is sent over TCP.</summary>
static partial class ConnectedAcceptanceSmoke
{
    const int Owner = 9901;
    const string Account = "cutoverconnected";
    static readonly Identity Character = new() { Type = IdentityType.CanbeAffected, Instance = Owner };
    static GeneratedMissionBinding generated = null!;
    static ActiveNanoRecord seededNano = null!;
    static string missionSnapshot = "";
    static DisposableSchemaDatabase ownedFixture = null!;
    static bool morphActive = true;
    static int previousDuration = int.MaxValue;
    static LifecyclePersistenceEvidence persistenceEvidence = null!;
    public static bool HandoffRejected { get; private set; }
    public static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
        throw new FixtureFailure("connected-repository-root-not-found");
    }
    public static void Validate(string zoneBinary, string loginBinary, DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            ownedFixture = fixture;
            Console.WriteLine("SOURCE_SHA=" + Git("rev-parse", "HEAD"));
            Console.WriteLine("SOURCE_WORKTREE_TRACKED_CLEAN=" + (Git("status", "--porcelain", "--untracked-files=no").Length == 0 ? "YES" : "NO"));
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            var xml = XDocument.Load(fixture.ConfigPath);
            xml.Root!.SetElementValue("LoginPort", fixture.LoginPort);
            xml.Save(fixture.ConfigPath);
            FixtureSql.Execute(connection, File.ReadAllText(Path.Combine(RepositoryRoot(),
                "AORebirth/Libraries/Source/AORebirth.Database/SqlTables/login.sql")));
            using (var command = new MySqlCommand("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (9901,'2026-09-09','fixture@invalid','','',@account,@hash,6,0,0,2047,0)", connection))
            {
                command.Parameters.AddWithValue("@account", Account);
                command.Parameters.AddWithValue("@hash", PasswordHash.CreateHash(password));
                command.ExecuteNonQuery();
            }
            FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES ({Owner},'{Account}','ConnectedFixture','','',4582,100,0,100,1,0,0,0,0)");
            // Real schema stat IDs from CharacterList and CharacterStat, fixture values only.
            var stats = LifecycleSpawnFixture.Stats();
            // Disposable fixture permissions; never uses the unsafe legacy SetGM API.
            stats[CharacterStat.GmLevel] = 1;
            stats[CharacterStat.ExternalPlayfieldInstance] = 0;
            stats[CharacterStat.ExternalDoorInstance] = 0;
            foreach (var stat in stats)
                FixtureSql.Execute(connection, $"INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (50000,{Owner},{(int)stat.Key},{stat.Value})");
            var inventory = new MySqlInventoryRepository(new SilentLogger());
            int identity = inventory.LeaseInstanceIdBlock(3);
            inventory.Insert(new ItemInstanceRecord { InstanceId = identity, ContainerType = 104, ContainerInstance = Owner,
                ContainerPlacement = 64, LowId = 43384, HighId = 43384, Quality = 1, StackCount = 3, Source = (AORebirth.Enums.ItemSource)1 });
            inventory.Insert(new ItemInstanceRecord { InstanceId = identity + 1, ContainerType = 104, ContainerInstance = Owner,
                ContainerPlacement = 65, LowId = 42423, HighId = 42423, Quality = 4, StackCount = 1, Source = (AORebirth.Enums.ItemSource)1 });
            // Seed a wear-page row to prove equipment storage/hydration survives the lifecycle.
            // This does not exercise or claim player-driven equip legality.
            inventory.Insert(new ItemInstanceRecord { InstanceId = identity + 2, ContainerType = (int)IdentityType.ArmorPage, ContainerInstance = Owner,
                ContainerPlacement = 17, LowId = 43384, HighId = 43384, Quality = 1, StackCount = 1, Source = (AORebirth.Enums.ItemSource)1 });
            var catalog = NanoCatalog.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(zoneBinary))!, "GameData", "nanos.dat"));
            Require(catalog.TryGet(270542, out var morph), "seed-supported-morph-catalog");
            long expiry = DateTime.UtcNow.AddMilliseconds((long)morph.DurationCentiseconds * 10).Ticks;
            var active = new ActiveNanoRecord(morph.Id, morph.Strain, 99, morph.DurationCentiseconds, expiry);
            seededNano = active;
            new MySqlActiveNanoRepository().Commit([new NanoCharacterWrite(Owner, [active], [])]);
            FixtureSql.Execute(connection, $"INSERT INTO charactersuploadednanos (CharacterId,NanoId) VALUES ({Owner},{morph.Id})");
            IMissionDao missions = new MySqlMissionDao(() => fixture.Open());
            long now = DateTime.UtcNow.Ticks;
            missions.Execute(Owner, Account, tx => { tx.SaveMission(new MissionKeyData(Owner, AuthoredQuestService.DeliverArmor),
                new MissionStateData { CharacterId = Owner, QuestId = AuthoredQuestService.DeliverArmor,
                    State = MissionLifecycleState.Active, CurrentStepId = "active", OfferedAtUtcTicks = now,
                    AcceptedAtUtcTicks = now, CreatedAtUtcTicks = now, UpdatedAtUtcTicks = now }); return true; });
            generated = ConnectedMissionSeed.Create(fixture, Owner);
            missionSnapshot = MissionSnapshot();
            SeedHandoffCases(connection, fixture, password);
            CharacterPersistenceGameplaySmoke.Prepare(zoneBinary, fixture, Owner);
            persistenceEvidence = LifecyclePersistenceEvidence.Capture(connection, Owner);
            persistenceEvidence.Verify(connection, "SEEDED", 0);
            Console.WriteLine("DATABASE_FIXTURE_ID=" + fixture.FixtureId + " ACCEPTANCE_TIMESTAMP=" + DateTime.UtcNow.ToString("O"));
            Console.WriteLine("CONNECTED_FIXTURE_SEEDED=PASS ACCOUNT_ID=9901 CHARACTER_ID=9901");
            using var login = new ConnectedEngineProcess(loginBinary, true, fixture);
            Console.WriteLine("LOGINENGINE_STARTED=YES LOGINENGINE_BINARY_SHA256=" + login.BinarySha256);
            using (var rejected = new ConnectedWireClient(fixture.LoginPort, paddedLoginFrames: true))
            {
                rejected.Send(new UserLoginMessage { UserName = Account, ClientVersion = "18.8.53_EP1" });
                var challenge = rejected.Wait<ServerSaltMessage>();
                rejected.Send(new UserCredentialsMessage { UserName = Account,
                    Credentials = DeterministicLoginKeyEncoder.Create(Account, password + "incorrect", challenge.ServerSalt) });
                Require(rejected.Wait<LoginErrorMessage>().Error == LoginError.InvalidUserNamePassword, "wrong-password-rejected");
                Require(FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={Owner}") == 0, "wrong-password-no-session");
                Console.WriteLine("CONNECTED_WRONG_PASSWORD_FAIL_CLOSED=PASS");
            }
            int before;
            ZoneLoginMessage outstanding;
            using (var zone = new ConnectedEngineProcess(zoneBinary, false, fixture))
            {
                before = zone.Id;
                Console.WriteLine("ZONEENGINE_NEW_STARTED=YES ZONEENGINE_NEW_BINARY_SHA256=" + zone.BinarySha256);
                using (var client = Enter(fixture, password))
                {
                    VerifyConnected(client, identity, 64, "INITIAL");
                    VerifyDatabase(connection, identity, 64);
                    persistenceEvidence.Verify(connection, "LOGIN", 1);
                    client.Send(new ClientMoveItemToInventoryMessage { Identity = Character,
                        SourceContainer = new Identity { Type = IdentityType.Inventory, Instance = 9999 }, TargetPlacement = 65 }, Owner);
                    client.Send(new ClientMoveItemToInventoryMessage { Identity = Character,
                        SourceContainer = new Identity { Type = IdentityType.Inventory, Instance = 64 }, TargetPlacement = 66 }, Owner);
                    client.Wait<ContainerAddItemMessage>(m => m.Identity == Character && m.TargetPlacement == 66);
                    Until(() => FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={identity} AND ContainerPlacement=66") == 1, "connected-inventory-move");
                    persistenceEvidence.ExpectInventoryMove(identity, 64, 66);
                    VerifyDatabase(connection, identity, 66);
                    persistenceEvidence.Verify(connection, "INVENTORY_MOVE", 1);
                    Console.WriteLine("CONNECTED_INVALID_INVENTORY_SOURCE_FAIL_CLOSED=PASS");
                    Console.WriteLine("CONNECTED_DURABLE_MUTATION=PASS OPERATION=inventory-move SOURCE=64 TARGET=66");
                    Logout(client, connection);
                    persistenceEvidence.Verify(connection, "FIRST_LOGOUT", 0);
                }
                ZonePersistenceRoundTrip(fixture, password, connection);
                using (var client = Enter(fixture, password))
                {
                    VerifyConnected(client, identity, 66, "RECONNECT");
                    VerifyDatabase(connection, identity, 66);
                    persistenceEvidence.Verify(connection, "FRESH_AUTH", 1);
                    Console.WriteLine("AUTH_RECONNECT=PASS STATE_AFTER_RECONNECT=PASS");
                    Logout(client, connection);
                    persistenceEvidence.Verify(connection, "FRESH_AUTH_LOGOUT", 0);
                }
                ValidateHandoffCases(fixture, password, connection, identity);
                outstanding = Authorize(fixture, password, "cutoverother", 9903);
                zone.Stop();
                persistenceEvidence.Verify(connection, "CLEAN_STOP", 0);
                Console.WriteLine("ENGINE_PID_BEFORE=" + before + " CLEAN_DISCONNECT=PASS");
            }
            using (var zone = new ConnectedEngineProcess(zoneBinary, false, fixture))
            {
                Require(zone.Id != before, "restart-process-identity");
                RejectUnchanged(fixture, connection, consumedHandoff, "replay-after-process-restart");
                RejectUnchanged(fixture, connection, expiredHandoff, "expired-after-process-restart");
                using (var pending = new ConnectedWireClient(fixture.ZonePort))
                {
                    pending.Send(outstanding);
                    pending.Wait<FullCharacterMessage>(m => m.Identity.Instance == 9903);
                    var otherCharacter = new Identity { Type = IdentityType.CanbeAffected, Instance = 9903 };
                    ValidatePlayerPayload(pending, otherCharacter);
                    pending.Send(new CharInPlayMessage { Identity = otherCharacter }, 9903);
                    pending.Wait<CharInPlayMessage>(m => m.Identity == otherCharacter);
                    pending.Send(new CharacterActionMessage { Identity = otherCharacter, Action = CharacterActionType.Logout }, 9903);
                    pending.Wait<StartLogoutMessage>();
                    Until(() => FixtureSql.Scalar(connection, "SELECT Online FROM characters WHERE Id=9903") == 0, "outstanding-restart-logout");
                    Console.WriteLine("UNCONSUMED_HANDOFF_AFTER_PROCESS_RESTART=PASS");
                }
                using (var client = Enter(fixture, password))
                {
                    VerifyConnected(client, identity, 66, "RESTART");
                    VerifyDatabase(connection, identity, 66);
                    persistenceEvidence.Verify(connection, "RESTART_AUTH", 1);
                    Console.WriteLine("AUTHENTICATED_RECONNECT_AFTER_RESTART=PASS STATE_AFTER_RESTART=PASS");
                    client.Send(new CharacterActionMessage { Identity = Character, Action = CharacterActionType.RemoveFriendlyNano,
                        Target = new Identity { Type = IdentityType.NanoProgram, Instance = 270542 } }, Owner);
                    client.Wait<BuffMessage>(m => m.Identity == Character && m.NanoProgram.Instance == 270542 && m.Action == 0);
                    client.Wait<StatMessage>(m => m.Identity == Character && m.Stats.Any(s => s.Value1 == CharacterStat.MonsterData && s.Value2 == 0));
                    morphActive = false;
                    VerifyDatabase(connection, identity, 66);
                    Console.WriteLine("CONNECTED_MORPH_CANCEL=PASS BASELINE_RESTORATION=PASS");
                    Logout(client, connection);
                    persistenceEvidence.Verify(connection, "MORPH_CANCEL_LOGOUT", 0);
                }
                using (var client = Enter(fixture, password))
                {
                    VerifyConnected(client, identity, 66, "AFTER_CANCEL");
                    VerifyDatabase(connection, identity, 66);
                    Logout(client, connection);
                    persistenceEvidence.Verify(connection, "FINAL_LOGOUT", 0);
                    Console.WriteLine("MORPH_CANCEL_AUTHENTICATED_RECONNECT=PASS");
                }
                zone.Stop();
                persistenceEvidence.Verify(connection, "FINAL_CLEAN_STOP", 0);
                Console.WriteLine("ENGINE_PID_AFTER=" + zone.Id + " ENGINE_RESTART_PROVEN=YES");
            }
            login.Stop();
            Console.WriteLine("LOGIN_WIRE_ACCEPTANCE=PASS CONNECTED_POSITIVE_LIFECYCLE=PASS");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }
    static ConnectedWireClient Enter(DisposableSchemaDatabase fixture, string password)
        => EnterWithHandoff(fixture, Authorize(fixture, password));

    static ZoneLoginMessage Authorize(DisposableSchemaDatabase fixture, string password, string account = Account, int owner = Owner)
    {
        using var login = new ConnectedWireClient(fixture.LoginPort, paddedLoginFrames: true);
        login.Send(new UserLoginMessage { UserName = account, ClientVersion = "18.8.53_EP1" });
        var challenge = login.Wait<ServerSaltMessage>();
        login.Send(new UserCredentialsMessage { UserName = account,
            Credentials = DeterministicLoginKeyEncoder.Create(account, password, challenge.ServerSalt) });
        var characters = login.Wait<CharacterListMessage>();
        Require(characters.Characters.Any(c => c.Id == owner), "authenticated-character-list");
        login.Send(new SelectCharacterMessage { CharacterId = owner });
        var zone = login.Wait<ZoneInfoMessage>();
        Require(zone.CharacterId == owner && zone.ServerIpAddress.Equals(System.Net.IPAddress.Loopback)
            && zone.ServerPort == fixture.ZonePort, "authenticated-character-selection-endpoint");
        Require(zone.EventServerType == 1 && zone.PlayerId == 0
            && login.Packets.Any(packet => packet.Length == 46), "retail-zoneinfo-26-byte-body");
        Console.WriteLine("AUTHENTICATED_LOGIN=PASS CHARACTER_LIST=PASS CHARACTER_SELECTION=PASS");
        Require(zone.Cookie1 != 0 || zone.Cookie2 != 0, "issued-handoff");
        login.Dispose();
        // Wait for the legitimate LoginEngine disconnect cleanup before measuring
        // a rejected zone attempt; otherwise its Online write races the snapshot.
        using (var connection = fixture.Open())
            Until(() => FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={owner}") == 0, "login-disconnect-cleanup");
        return new ZoneLoginMessage { CharacterId = owner, Cookie1 = zone.Cookie1, Cookie2 = zone.Cookie2 };
    }
    static ConnectedWireClient EnterWithHandoff(DisposableSchemaDatabase fixture, ZoneLoginMessage handoff)
    {
        var client = new ConnectedWireClient(fixture.ZonePort);
        try
        {
            client.Send(handoff);
            client.Wait<FullCharacterMessage>(m => m.Identity.Instance == Owner);
            client.Wait<SpecialAttackWeaponMessage>(m => m.Identity.Instance == Owner);
            VerifyRetailWorldEntryReadyBlock(client);
            ValidatePlayerPayload(client, Character);
            Console.WriteLine("ZONE_HANDOFF_VALIDATION=PASS");
            EnterWorld(client);
            return client;
        }
        catch { client.Dispose(); throw; }
    }
    static void VerifyRetailWorldEntryReadyBlock(ConnectedWireClient client)
    {
        var received = client.Received.ToList();
        int spawn = received.FindIndex(m => m is SimpleCharFullUpdateMessage s && s.Identity == Character);
        int gameTime = received.FindIndex(m => m is GameTimeMessage g && g.Identity == Character);
        int social = received.FindIndex(m => m is StatMessage s && s.Identity == Character && s.Unknown == 1
            && s.Stats.Any(stat => stat.Value1 == CharacterStat.SocialStatus && stat.Value2 == 4));
        int full = received.FindIndex(m => m is FullCharacterMessage f && f.Identity == Character);
        int towers = received.FindIndex(m => m is PlayfieldAllTowersMessage);
        int cities = received.FindIndex(m => m is PlayfieldAllCitiesMessage);
        int specials = received.FindIndex(m => m is SpecialAttackWeaponMessage s && s.Identity == Character);
        Require(spawn >= 0 && spawn < gameTime && gameTime < social && social < full
            && full < towers && towers < cities && cities < specials, "retail-world-entry-ready-order");
        Console.WriteLine("RETAIL_WORLD_ENTRY_READY_BLOCK=PASS");
    }
    static void EnterWorld(ConnectedWireClient client)
    {
        client.Send(new CharInPlayMessage { Identity = Character }, Owner);
        client.Wait<QuestFullUpdateMessage>(m => m.Quests.Any(q => q.QuestId.Instance == generated.QuestInstance));
        client.Wait<QuestFullUpdateMessage>(m => m.Quests.Any(q => q.QuestId.Instance == unchecked((int)0x555BE9F6)));
    }
    static void ValidatePlayerPayload(ConnectedWireClient client, Identity identity)
    {
        var spawn = client.Received.OfType<SimpleCharFullUpdateMessage>().First(m => m.Identity == identity);
        var full = client.Received.OfType<FullCharacterMessage>().Single(m => m.Identity == identity);
        ZoneEngine_New.Core.Characters.PlayerSpawnPayloadValidator.RequireValidMessages(spawn, full);
        Require((int)spawn.CharacterFlags != (int)CharacterStat.Unset, "player-flags-not-unset");
        Require(!spawn.CharacterFlags.HasFlag(CharacterFlags.Tower), "ordinary-player-not-tower");
        Require(spawn.Health > 0 && spawn.HealthDamage >= 0 && spawn.HealthDamage <= spawn.Health,
            "player-health-relationship");
        Require(spawn.HeadMesh.HasValue && spawn.HeadMesh.Value > 0 && spawn.VisualFlags >= 0,
            "player-appearance-required");
        Require(spawn.CharacterInfo is SimplePcInfo pc && pc.StrengthBase > 0 && pc.AgilityBase > 0
            && pc.StaminaBase > 0 && pc.IntelligenceBase > 0 && pc.SenseBase > 0 && pc.PsychicBase > 0,
            "player-primary-abilities");

        var values = new Dictionary<int, long>();
        foreach (var row in full.Stats1.Concat(full.Stats2)) values[row.Value1] = row.Value2;
        foreach (var row in full.Stats3) values[row.Value1] = row.Value2;
        foreach (var row in full.Stats4) values[row.Value1] = row.Value2;
        foreach (CharacterStat required in ZoneEngine_New.Core.Characters.CharacterHydrationValidator.RequiredSpawnStats)
        {
            if (required == CharacterStat.HeadMesh) continue;
            Require(values.TryGetValue((int)required, out long value)
                && value != (int)CharacterStat.Unset, "fullcharacter-required-stat-" + (int)required);
        }

        Require(values[(int)CharacterStat.MaxHealth] == spawn.Health
            && values[(int)CharacterStat.Health] == spawn.Health - spawn.HealthDamage,
            "fullcharacter-scfu-health-consistency");
        Console.WriteLine("SYNTHETIC_ACCEPTANCE_PLAYER_PAYLOAD_VALIDATION=PASS");
    }
    static void VerifyConnected(ConnectedWireClient client, int identity, int slot, string phase)
    {
        var full = client.Received.OfType<FullCharacterMessage>().Single(m => m.Identity.Instance == Owner);
        var spawn = client.Received.OfType<SimpleCharFullUpdateMessage>().First(m => m.Identity.Instance == Owner);
        Require(spawn.Coordinates.X == 100 && spawn.Coordinates.Y == 0 && spawn.Coordinates.Z == 100,
            $"wire-position-{phase}-actual-{spawn.Coordinates.X}-{spawn.Coordinates.Y}-{spawn.Coordinates.Z}");
        Require(MorphVisualPackets.TryBuild(270542, false, Character, 4582, false, out byte[] expectedMorph)
            && client.Packets.Any(p => p.AsSpan(16).SequenceEqual(expectedMorph.AsSpan(16))) == morphActive, "wire-captured-morph-projection-" + phase);
        var durations = client.Received.OfType<CharacterActionMessage>().Where(m => m.Identity == Character && m.Action == CharacterActionType.SetNanoDuration && m.Target.Instance == 270542).ToArray();
        Require(durations.Length == (morphActive ? 1 : 0), "wire-active-nano-count-" + phase);
        if (morphActive)
        {
            int duration = durations[0].Parameter2;
            Require(duration > 0 && duration <= seededNano.DurationCentiseconds && duration <= previousDuration,
                "wire-active-nano-expiry-not-reset-" + phase);
            previousDuration = duration;
            Console.WriteLine("WIRE_ACTIVE_NANO_REMAINING_CENTISECONDS=" + duration + " PERSISTED_INSTANCE=99 PERSISTED_EXPIRY=" + seededNano.ExpiresAtUtcTicks);
        }
        Require(full.UploadedNanoIds.Contains(270542), "wire-uploaded-morph-" + phase);
        Require(full.InventorySlots.Length == 6, "wire-item-count-" + phase);
        Require(full.InventorySlots.Single(i => i.Identity.Instance == CharacterPersistenceGameplaySmoke.SecondItem).Placement == CharacterPersistenceGameplaySmoke.WearSlot,
            "wire-actually-equipped-item-" + phase);
        Require(full.InventorySlots.Single(i => i.Identity.Instance == CharacterPersistenceGameplaySmoke.FirstItem).Placement == 69,
            "wire-actually-swapped-item-" + phase);
        foreach (int itemId in new[] { CharacterPersistenceGameplaySmoke.FirstItem, CharacterPersistenceGameplaySmoke.SecondItem })
        {
            var acquired = full.InventorySlots.Single(i => i.Identity.Instance == itemId);
            Require(acquired.ItemLowId == CharacterPersistenceGameplaySmoke.TemplateId && acquired.ItemHighId == CharacterPersistenceGameplaySmoke.TemplateId
                && acquired.Quality == CharacterPersistenceGameplaySmoke.TemplateQuality && acquired.Count == 1
                && (int)acquired.Identity.Type == CharacterPersistenceGameplaySmoke.ItemType, "wire-actual-loot-metadata-" + phase);
        }
        var key = full.InventorySlots.Single(i => i.Identity.Instance == generated.KeyInstance);
        Require(key.Placement == 67 && key.Identity.Type == (IdentityType)0xC76D && key.ItemLowId == 28577 && key.ItemHighId == 28577 && key.Quality == 1 && key.Count == 1, "wire-mission-key-" + phase);
        var first = full.InventorySlots.Single(i => i.Identity.Instance == identity);
        var second = full.InventorySlots.Single(i => i.Identity.Instance == identity + 1);
        var equipped = full.InventorySlots.Single(i => i.Identity.Instance == identity + 2);
        Require(equipped.Placement == 17 && equipped.ItemLowId == 43384 && equipped.ItemHighId == 43384
            && equipped.Quality == 1 && equipped.Count == 1 && (int)equipped.Identity.Type == 0xC73D,
            "wire-seeded-equipment-" + phase);
        Require(first.Placement == slot && first.ItemLowId == 43384 && first.ItemHighId == 43384 && first.Quality == 1 && first.Count == 3, "wire-first-item-" + phase);
        Require(second.Placement == 65 && second.ItemLowId == 42423 && second.ItemHighId == 42423 && second.Quality == 4 && second.Count == 1, "wire-second-item-" + phase);
        Require((int)first.Identity.Type == 0xC73D && (int)second.Identity.Type == 0xC73D
            && full.InventorySlots.All(i => i.Flags == 161 && i.Unknown == 0), "wire-item-types-flags-metadata-" + phase);
        Require(full.Stats1.Concat(full.Stats2).Any(s => s.Value1 == (int)CharacterStat.Cash && s.Value2 == expectedCash), "wire-cash-" + phase);
        Console.WriteLine("CONNECTED_STATE_" + phase + "=PASS CHARACTER_IDENTITY=PASS INVENTORY_IDENTITY=PASS CREDITS=PASS POSITION=PASS");
        Console.WriteLine("ACTIVE_NANOS_RELOAD=PASS MORPH_RELOAD=PASS AUTHORED_MISSION_RELOAD=PASS GENERATED_MISSION_RELOAD=PASS PROOF=SEEDED_STATE_CONNECTED_RELOAD");
        Console.WriteLine("WIRE_INVENTORY_" + phase + "=" + System.Text.Json.JsonSerializer.Serialize(full.InventorySlots));
    }
    static void VerifyDatabase(MySqlConnection connection, int identity, int slot)
    {
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE ContainerInstance={Owner}") == 6, "persisted-no-phantom-rows");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={identity + 2} AND ContainerType={(int)IdentityType.ArmorPage} AND ContainerInstance={Owner} AND ContainerPlacement=17 AND ItemType=0 AND LowId=43384 AND HighId=43384 AND Quality=1 AND StackCount=1 AND Source=1") == 1, "persisted-seeded-equipment-exact");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={generated.KeyInstance} AND ContainerType=104 AND ContainerInstance={Owner} AND ContainerPlacement=67 AND ItemType={0xC76D} AND LowId=28577 AND HighId=28577 AND Quality=1 AND StackCount=1 AND Source=0") == 1, "persisted-mission-key-exact");
        var nanos = new MySqlActiveNanoRepository().Load(Owner);
        Require(morphActive ? nanos.Count == 1 && nanos[0] == seededNano : nanos.Count == 0, "persisted-active-nano-exact-expiry");
        Require(MissionSnapshot() == missionSnapshot, "persisted-generated-binding-objects-exact");
        var authored = ((IMissionDao)new MySqlMissionDao(() => ownedFixture.Open())).GetMission(new MissionKeyData(Owner, AuthoredQuestService.DeliverArmor));
        Require(authored != null && authored.State == MissionLifecycleState.Active && authored.CurrentStepId == "active", "persisted-authored-mission");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={identity} AND ContainerType=104 AND ContainerInstance={Owner} AND ContainerPlacement={slot} AND ItemType=0 AND LowId=43384 AND HighId=43384 AND Quality=1 AND StackCount=3 AND Source=1") == 1, "persisted-first-item-exact");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={identity + 1} AND ContainerType=104 AND ContainerInstance={Owner} AND ContainerPlacement=65 AND ItemType=0 AND LowId=42423 AND HighId=42423 AND Quality=4 AND StackCount=1 AND Source=1") == 1, "persisted-second-item-exact");
        Require(FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type=50000 AND Instance={Owner} AND StatId={(int)CharacterStat.Cash}") == expectedCash, "persisted-cash-exact");
        Require(FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type=50000 AND Instance={Owner} AND StatId={(int)CharacterStat.MonsterData}") == 0, "persisted-morph-baseline");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM stats WHERE Type=50000 AND Instance={Owner} AND ((StatId={(int)CharacterStat.CATMesh} AND StatValue=111) OR (StatId={(int)CharacterStat.DisplayCATMesh} AND StatValue=222))") == 2, "persisted-morph-mesh-baselines");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM characters WHERE Id={Owner} AND Username='{Account}' AND Name='ConnectedFixture' AND Playfield=4582 AND X=100 AND Y=0 AND Z=100 AND HeadingW=1 AND HeadingX=0 AND HeadingY=0 AND HeadingZ=0") == 1, "persisted-character-exact");
    }
    static void Logout(ConnectedWireClient client, MySqlConnection connection)
    {
        client.Send(new CharacterActionMessage { Identity = Character, Action = CharacterActionType.Logout }, Owner);
        client.Wait<StartLogoutMessage>();
        Until(() => FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={Owner}") == 0, "logout-persistence");
    }
    static void Until(Func<bool> test, string code)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15)) { if (test()) return; Thread.Sleep(50); }
        throw new FixtureFailure("connected-" + code + "-timeout");
    }
    static void Require(bool condition, string code) { if (!condition) throw new FixtureFailure("connected-" + code); }
    static string MissionSnapshot()
    {
        var dao = new MySqlMissionDao(() => ownedFixture.Open());
        return System.Text.Json.JsonSerializer.Serialize(new { Binding = dao.ReadAccepted(Owner, generated.QuestType, generated.QuestInstance),
            Objects = dao.ReadObjects(Owner, generated.QuestType, generated.QuestInstance).OrderBy(o => o.RuntimeType).ThenBy(o => o.RuntimeInstance) });
    }
    static string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = RepositoryRoot(), UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new FixtureFailure("connected-source-provenance-start");
        string value = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        Require(process.ExitCode == 0, "source-provenance"); return value;
    }
}
