using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Movement;

// Separate from the broad nano/inventory scenario. All setup writes precede
// engine startup; thereafter only authenticated protocol messages mutate state.
static class ConnectedMissionMatrix
{
    const int Owner = 9910;
    const string Account = "missionconnected";
    static readonly Identity Character = new() { Type = IdentityType.CanbeAffected, Instance = Owner };
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static ZoneLoginMessage handoff = null!;

    public static void Validate(string zoneBinary, string loginBinary, DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
        try
        {
            var data = new GameDataStore(new SilentLogger());
            var terminal = data.GetPlayfieldGeometry(710).Dynels!.Dynels
                .First(value => value.IdentityType == MissionTerminal.LiveIdentityType);
            FixtureSql.Execute(connection, File.ReadAllText(Path.Combine(ConnectedAcceptanceSmoke.RepositoryRoot(),
                "AORebirth/Libraries/Source/AORebirth.Database/SqlTables/login.sql")));
            using (var command = new MySqlCommand("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (9910,'2026-09-26','fixture@invalid','','',@account,@hash,6,0,0,2047,0)", connection))
            {
                command.Parameters.AddWithValue("@account", Account);
                command.Parameters.AddWithValue("@hash", AORebirth.Core.Encryption.PasswordHash.CreateHash(password));
                command.ExecuteNonQuery();
            }
            using (var command = new MySqlCommand("INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES (9910,@account,'MissionFixture','','',710,@x,@y,@z,1,0,0,0,0)", connection))
            {
                command.Parameters.AddWithValue("@account", Account);
                command.Parameters.AddWithValue("@x", terminal.Position.X + 0.5f);
                command.Parameters.AddWithValue("@y", terminal.Position.Y);
                command.Parameters.AddWithValue("@z", terminal.Position.Z);
                command.ExecuteNonQuery();
            }
            var stats = LifecycleSpawnFixture.Stats();
            stats[CharacterStat.Level] = 60;
            stats[CharacterStat.GmLevel] = 1;
            stats[CharacterStat.Side] = 2;
            stats[CharacterStat.Cash] = 500000;
            stats[CharacterStat.BodyDevelopment] = 5000;
            stats[CharacterStat.Stamina] = 1000;
            stats[CharacterStat.Health] = stats[CharacterStat.MaxHealth] = 50000;
            stats[CharacterStat.MartialArts] = 1000;
            stats[CharacterStat.MeleeDamageModifier] = 100000;
            foreach (var stat in stats)
                FixtureSql.Execute(connection, $"INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (50000,{Owner},{(int)stat.Key},{stat.Value})");
            var config = System.Xml.Linq.XDocument.Load(fixture.ConfigPath);
            config.Root!.SetElementValue("LoginPort", fixture.LoginPort);
            config.Save(fixture.ConfigPath);
            var dao = new MySqlMissionDao(() => fixture.Open());
            Console.WriteLine("CONNECTED_MISSION_SETUP=PRESTART_ONLY TERMINAL=" + terminal.IdentityInstance);
            using var login = new ConnectedEngineProcess(loginBinary, true, fixture);
            using var zone = new ConnectedEngineProcess(zoneBinary, false, fixture);
            Console.WriteLine("MISSION_FULL_STARTUP=PASS ZONE_PID=" + zone.Id + " LOGIN_PID=" + login.Id + " ZONE_SHA256=" + zone.BinarySha256 + " LOGIN_SHA256=" + login.BinarySha256);
            GeneratedMissionBinding binding;
            GeneratedMissionObject dead;
            Func<byte[], bool>? verifyCorpse = null;
            int verifiedCorpses = 0;
            var client = Enter(fixture, password);
            try
            {
                Console.WriteLine("MISSION_LOGIN=PASS");
                client.Send(new QuestAlternativeMessage { Identity = Character, LevelSlider = 1,
                    MissionTerminalIdentity = new Identity { Type = (IdentityType)terminal.IdentityType, Instance = terminal.IdentityInstance }, QuestInfos = [] }, Owner);
                var roll = client.Wait<QuestAlternativeMessage>(value => value.Identity == Character && value.QuestInfos.Length == 5);
                Require(roll.QuestInfos.Select(value => value.QuestIdentity).Distinct().Count() == 5, "offer-identities");
                var stored = dao.ReadOffers(Owner);
                Require(stored.Count == 5, "offer-durable-count");
                foreach (var offer in stored)
                    Require(GeneratedMissionWire.Read(offer.FrozenWireBody).QuestInfos.Any(q => q.QuestIdentity.Instance == offer.OfferInstance), "offer-frozen-serialization");
                Console.WriteLine("MISSION_ROLL=PASS OFFER_SERIALIZATION=PASS OFFERS=5");
                // A random roll does not guarantee any particular mission type.
                // Prefer a repair object when offered, otherwise exercise an actual offered mission.
                var choice = roll.QuestInfos.FirstOrDefault(value => MissionRollPolicy.Current.TypeFromIcon(value.MissionIconId) == MissionRollType.RepairMachine)
                    ?? roll.QuestInfos.First();
                Console.WriteLine("MISSION_SELECTED_TYPE=" + MissionRollPolicy.Current.TypeFromIcon(choice.MissionIconId));
                client.Send(new CreateQuestMessage { Identity = Character, QuestIdentity = choice.QuestIdentity }, Owner);
                client.Wait<QuestFullUpdateMessage>(value => value.Quests.Length > 0);
                binding = dao.ReadAccepted(Owner).Single();
                Require(binding.State == GeneratedMissionState.Active && binding.OfferInstance == choice.QuestIdentity.Instance, "acceptance-durable-binding");
                Console.WriteLine("MISSION_ACCEPTANCE=PASS BUNDLE=" + binding.BundleId + " HASH=" + binding.BundleSha256);
                SendCommand(client, FormattableString.Invariant($".tp {binding.Offer.DestinationX} {binding.Offer.DestinationY} {binding.Offer.DestinationZ} {binding.Offer.DestinationPlayfield}"));
                if (binding.Offer.DestinationPlayfield != 710) client = Transfer(client, fixture);
                else client.Wait<CharDCMoveMessage>(value => value.Identity == Character);
                Use(client, new Identity { Type = (IdentityType)binding.Offer.EntranceType, Instance = binding.Offer.EntranceInstance });
                client = Transfer(client, fixture);
                Require(client.Received.OfType<PlayfieldAnarchyFMessage>().Any(value => value.PlayfieldId2.Instance == binding.LivePlayfield), "entry-live-playfield");
                Console.WriteLine("MISSION_ENTRY=PASS LIVE_PLAYFIELD=" + binding.LivePlayfield);
                var objects = dao.ReadObjects(Owner, binding.QuestType, binding.QuestInstance);
                var bundle = MissionAcgCapturedLayoutCatalog.CreateBundles().Single(value => value.LayoutId == binding.BundleId);
                Require(MissionAcgRuntimeMaterializer.TryMaterialize(binding, bundle, null, DateTime.UtcNow, out var materialized, out string failure), "materialization-" + failure);
                Require(objects.Count == materialized.Objects.Count, "npc-static-durable-count");
                var npc = objects.Where(value => value.Level.HasValue).OrderBy(value => Distance(value, bundle.EntryPoint)).First();
                DrainUntil(client, () => client.Received.OfType<SimpleCharFullUpdateMessage>().Any(value => value.Identity.Instance == npc.RuntimeInstance));
                var spawn = client.Received.OfType<SimpleCharFullUpdateMessage>().Last(value => value.Identity.Instance == npc.RuntimeInstance);
                var slot = bundle.NpcSlots.Single(value => value.CapturedIdentity.Instance == npc.CapturedInstance);
                Require(spawn.MonsterScale == slot.Appearance.Scale && spawn.HeadMesh == slot.Appearance.HeadMesh, "npc-appearance-scale-head");
                Require(JsonSerializer.Serialize(spawn.Textures ?? [], Json) == JsonSerializer.Serialize(slot.Appearance.Textures, Json), "npc-appearance-textures");
                foreach (var mesh in slot.Appearance.Meshes)
                    Require((spawn.Meshes ?? []).Any(value => value.Id == mesh.Id && value.Position == mesh.Position && value.Layer == mesh.Layer && value.OverrideTextureId == mesh.OverrideTextureId), "npc-appearance-mesh");
                Require(client.Received.OfType<DoorFullUpdateMessage>().Any() && client.Received.OfType<ChestItemFullUpdateMessage>().Any(), "static-door-chest-wire");
                Console.WriteLine("MISSION_NPC_CREATION=PASS MISSION_NPC_APPEARANCE=PASS MISSION_STATIC_OBJECTS=PASS");
                var runtime = materialized.Objects.Single(value => value.Identity.RuntimeIdentity.Instance == npc.RuntimeInstance);
                var evidence = new GeneratedMissionNpcEvidence(runtime, bundle, binding.Offer.Quality, binding.Offer.MissionType);
                int corpseKind = BinaryPrimitives.ReadInt32BigEndian(ZoneEngine.Core.Packets.GeneratedMissionCorpseWire.CopyTemplate().AsSpan(16, 4));
                verifyCorpse = packet =>
                {
                    if (packet.Length < 28 || BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(16, 4)) != corpseKind
                        || BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(24, 4)) != npc.RuntimeInstance) return false;
                    var death = dao.ReadObjects(Owner, binding.QuestType, binding.QuestInstance).Single(value => value.RuntimeInstance == npc.RuntimeInstance);
                    Require(death.IsDead && death.CurrentHealth == 0, "corpse-requires-durable-death");
                    byte[] expected = GeneratedMissionCorpseProjection.BuildPacket(evidence, death, binding.LivePlayfield, Character);
                    // ConnectedWireClient independently verifies the per-session sequence.
                    BinaryPrimitives.WriteUInt16BigEndian(expected, BinaryPrimitives.ReadUInt16BigEndian(packet));
                    if (!packet.SequenceEqual(expected))
                    {
                        string directory = Path.Combine(ConnectedAcceptanceSmoke.RepositoryRoot(), "build-verify", "connected-mission-followup", "corpse-mismatch-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                        Directory.CreateDirectory(directory);
                        File.WriteAllBytes(Path.Combine(directory, "actual.bin"), packet);
                        File.WriteAllBytes(Path.Combine(directory, "expected.bin"), expected);
                        File.WriteAllText(Path.Combine(directory, "state.json"), JsonSerializer.Serialize(new { death, evidence.Name, evidence.MonsterData, evidence.Appearance, binding }, Json));
                        Console.WriteLine("CORPSE_MISMATCH_EVIDENCE=" + directory + " ACTUAL_BYTES=" + packet.Length + " EXPECTED_BYTES=" + expected.Length);
                    }
                    Require(packet.SequenceEqual(expected), "corpse-exact-typed-durable-projection");
                    verifiedCorpses++;
                    Console.WriteLine("MISSION_CORPSE_EXACT_WIRE=PASS SHA256=" + Convert.ToHexString(SHA256.HashData(packet)));
                    return true;
                };
                client.IsVerifiedOpaquePacket = verifyCorpse;
                client.Send(new CharDCMoveMessage { Identity = Character, MoveType = (byte)MovementAction.FullStop,
                    Coordinates = new Vector3 { X = npc.X + 0.5f, Y = npc.Y, Z = npc.Z }, Heading = new Quaternion { W = 1 } }, Owner);
                var npcIdentity = new Identity { Type = (IdentityType)npc.RuntimeType, Instance = npc.RuntimeInstance };
                client.Wait<AttackInfoMessage>(value => value.Identity == npcIdentity && value.Target == Character);
                Require(client.Received.OfType<SpecialAttackWeaponMessage>().Any(value => value.Identity == npcIdentity)
                    || client.Received.OfType<WeaponItemFullUpdateMessage>().Any(value => value.Owner == npcIdentity), "npc-weapon-wire");
                Console.WriteLine("MISSION_NPC_WEAPON_BEHAVIOR=PASS NPC_ATTACKED_PLAYER=PASS");
                client.Send(new AttackMessage { Identity = Character, Target = npcIdentity, Action = 0 }, Owner);
                client.Wait<AttackInfoMessage>(value => value.Identity == Character && value.Target == npcIdentity);
                DrainUntil(client, () => dao.ReadObjects(Owner, binding.QuestType, binding.QuestInstance).Single(value => value.RuntimeInstance == npc.RuntimeInstance).IsDead);
                dead = dao.ReadObjects(Owner, binding.QuestType, binding.QuestInstance).Single(value => value.RuntimeInstance == npc.RuntimeInstance);
                byte[] corpse = GeneratedMissionCorpseProjection.BuildPacket(evidence, dead, binding.LivePlayfield, Character);
                DrainUntil(client, () => verifiedCorpses > 0);
                Console.WriteLine("MISSION_NPC_DEATH=PASS MISSION_CORPSE_PROJECTION=PASS EXACT_BYTES=" + corpse.Length);
                Logout(client, connection);
            }
            finally { client.Dispose(); }
            using (var reconnect = Enter(fixture, password, verifyCorpse))
            {
                DrainUntil(reconnect, () => reconnect.Received.OfType<QuestFullUpdateMessage>().Any(value => value.Quests.Any(q => q.QuestId.Instance == binding.QuestInstance)));
                var restored = dao.ReadAccepted(Owner).Single();
                Require(restored.BundleId == binding.BundleId && restored.BundleSha256 == binding.BundleSha256 && restored.LivePlayfield == binding.LivePlayfield, "accepted-restoration-identity");
                Require(dao.ReadObjects(Owner, binding.QuestType, binding.QuestInstance).Single(value => value.RuntimeInstance == dead.RuntimeInstance).IsDead, "accepted-restoration-death");
                Console.WriteLine("MISSION_RECONNECT=PASS ACCEPTED_MISSION_RESTORATION=PASS");
                Logout(reconnect, connection);
            }
            zone.Stop(); login.Stop();
            Console.WriteLine("CONNECTED_MISSION_MATRIX=PASS");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }

    static ConnectedWireClient Enter(DisposableSchemaDatabase fixture, string password, Func<byte[], bool>? verifyOpaque = null)
    {
        handoff = ConnectedAcceptanceSmoke.Authorize(fixture, password, Account, Owner);
        return EnterWithHandoff(fixture, verifyOpaque);
    }
    static ConnectedWireClient Transfer(ConnectedWireClient source, DisposableSchemaDatabase fixture)
    {
        source.Wait<N3TeleportMessage>();
        source.Wait<ZoneRedirectionMessage>();
        source.Dispose();
        return EnterWithHandoff(fixture);
    }
    static ConnectedWireClient EnterWithHandoff(DisposableSchemaDatabase fixture, Func<byte[], bool>? verifyOpaque = null)
    {
        var client = new ConnectedWireClient(fixture.ZonePort) { IsVerifiedOpaquePacket = verifyOpaque };
        try { client.Send(handoff); FinishEntry(client); return client; }
        catch { client.Dispose(); throw; }
    }
    static void FinishEntry(ConnectedWireClient client)
    {
        client.Wait<FullCharacterMessage>(value => value.Identity == Character);
        client.Wait<SpecialAttackWeaponMessage>(value => value.Identity == Character);
        client.Send(new CharInPlayMessage { Identity = Character }, Owner);
        client.Wait<CharInPlayMessage>(value => value.Identity == Character);
    }
    static void SendCommand(ConnectedWireClient client, string text)
        => client.Send(new TextMessage { Message = new ChatMessage { Text = text } }, Owner);
    static void Use(ConnectedWireClient client, Identity identity)
        => client.Send(new GenericCmdMessage { Identity = Character, User = Character, Action = GenericCmdAction.Use, Count = 1, Target = [identity] }, Owner);
    static void DrainUntil(ConnectedWireClient client, Func<bool> condition)
    { if (!condition()) client.Wait<MessageBody>(_ => condition()); }
    static double Distance(GeneratedMissionObject value, MissionAcgPointRecord p)
        => Math.Pow(value.X - p.X, 2) + Math.Pow(value.Y - p.Y, 2) + Math.Pow(value.Z - p.Z, 2);
    static void Logout(ConnectedWireClient client, MySqlConnection connection)
    {
        client.Send(new CharacterActionMessage { Identity = Character, Action = CharacterActionType.Logout }, Owner);
        client.Wait<StartLogoutMessage>();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30) && FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={Owner}") != 0) Thread.Sleep(50);
        Require(FixtureSql.Scalar(connection, $"SELECT Online FROM characters WHERE Id={Owner}") == 0, "logout-persistence");
        Console.WriteLine("MISSION_LOGOUT=PASS");
    }
    static void Require(bool condition, string code) { if (!condition) throw new FixtureFailure("mission-matrix-" + code); }
}
