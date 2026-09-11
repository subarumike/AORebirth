using System.Reflection;
using System.Runtime.CompilerServices;
using AORebirth.Database.Domain.Characters;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Characters;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Vector3 = AORebirth.Core.Vector.Vector3;

// Application-service fixture: only world/session infrastructure is synthetic.
// Real catalog templates, runtime loot/move/equip handlers, hydration, snapshots and DAO/MySQL are used.
// ConnectedAcceptanceSmoke subsequently reloads these same identities in two real engine processes.
static class CharacterPersistenceGameplaySmoke
{
    public static int FirstItem, SecondItem, WearSlot, TemplateId, TemplateQuality, ItemType;
    public static CharacterStat BonusStat;
    public static int Bonus;

    public static void Prepare(string binary, DisposableSchemaDatabase fixture, int owner)
    {
        var logger = new SilentLogger();
        var dao = new MySqlCharacterPersistenceDao(() => fixture.Open());
        var inventory = new MySqlInventoryRepository(logger, dao);
        var nanos = new MySqlUploadedNanoRepository(logger, dao);
        var characters = new MySqlCharacterRepository(logger, persistence: dao);
        var stats = new MySqlStatRepository(logger, dao);
        IGameData data = DispatchProxy.Create<IGameData, CatalogRoot>();
        ((CatalogRoot)(object)data).Root = Path.Combine(Path.GetDirectoryName(binary)!, "GameData");
        var catalog = new ItemTemplateCatalog(new MySqlItemNameRepository(logger, dao), data, logger);
        var builder = new ItemBuilder(catalog, logger);
        var loader = new CharacterHydrationService(characters, stats, inventory, nanos, logger);
        var snapshot = new CharacterSnapshotService(characters, stats, logger);
        Player player = Load();
        var all = (Dictionary<int, ItemTemplate>)typeof(ItemTemplateCatalog).GetField("_templates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(catalog)!;
        // Select actual armor that the existing runtime accepts for this character.
        foreach (var template in all.Values.OrderBy(t => t.Id))
        {
            if (template.Stats.GetValueOrDefault(CharacterStat.ItemClass) != (int)ItemClass.Armor
                || !template.MeetsActionRequirements(s => player.Stats.Get(s), ActionType.ToWear)) continue;
            int slot = Enumerable.Range(player.Inventory.Armor.Offset, player.Inventory.Armor.Capacity)
                .FirstOrDefault(s => !player.Inventory.Armor.Content.ContainsKey(s)
                    && (template.Stats.GetValueOrDefault(CharacterStat.Slot) & (1 << (s - player.Inventory.Armor.Offset + 1))) != 0, -1);
            if (slot < 0) continue;
            TemplateId = template.Id; TemplateQuality = template.Quality; WearSlot = slot;
            BonusStat = CharacterStat.ProjectileAC; Bonus = 11; break;
        }
        Require(TemplateId > 0, "real-catalog-wear-fixture-missing");
        // Nonzero modifier proof is explicitly synthetic and confined to this process.
        // Never write this definition to GameData or claim it exists in the real catalog.
        var selectedTemplate = all[TemplateId];
        selectedTemplate.SpellList.TryGetValue(EventType.OnWear, out var originalWear);
        selectedTemplate.SpellList[EventType.OnWear] = new List<ItemSpell>(originalWear ?? [])
        { new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)BonusStat, Bonus] } };
        dao.SaveStats(owner, new[] { new CharacterStatData { StatId = (int)BonusStat, StatValue = 100 } });
        player.Stats.Set(BonusStat, 100, StatDetail.Base);
        var manager = Blank<PlayfieldManager>();
        Set(manager, "_sync", new Lock()); Set(manager, "_playersByCharacterId", new Dictionary<int, Player>());
        var world = Blank<Playfield>();
        var registry = new DynelRegistry();
        using var services = new ServiceCollection().AddSingleton(registry).AddSingleton(new PlayfieldLocality(4582, null)).BuildServiceProvider();
        Set(world, "_serviceProvider", services); Set(world, "_dynelRegistry", registry);
        Set(world, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = 4582 });
        Set(world, "_playfieldManager", manager);
        player.Playfield = world;
        var session = new GameplaySession(); session.BindPlayer(player); player.Session = session;
        manager.RegisterPlayer(player); registry.Register(player);
        using var flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager), new MySqlCharacterCoalesceCommit(inventory, nanos, logger), logger);
        var allocator = new ItemInstanceIdAllocator(inventory, logger);
        var actions = new InventoryActionService(new MySqlInventoryMutationPersistence(inventory, nanos), flush, allocator, logger, catalog, builder);
        var moves = new InventoryMoveService(logger, flush, actions);
        player.Rebase();
        int baseline = player.Stats.Get(BonusStat);
        FirstItem = allocator.Allocate(); SecondItem = allocator.Allocate();
        Item first = builder.Create(TemplateId, TemplateId, TemplateQuality, ItemSource.Loot, instanceId: FirstItem);
        Item second = builder.Create(TemplateId, TemplateId, TemplateQuality, ItemSource.Loot, instanceId: SecondItem);
        ItemType = (int)first.Identity.Type;
        var loot = new FixtureLoot { Playfield = world };
        registry.Register(loot);
        loot.Loot.Add(0, first); loot.Loot.Add(1, second);
        Require(loot.TryUse(player), "loot-open");
        VerifyLootCommitFailure(); Claim(1, 69);
        Require(loot.Loot.Content.Count == 1 && player.Inventory.Inventory.Content[68].IsPersisted && second.IsPersisted, "actual-loot-not-committed");
        Move(IdentityType.Inventory, 68, WearSlot);
        Require(player.Inventory.Armor.Content[WearSlot].InstanceId == FirstItem && player.Stats.Get(BonusStat) == baseline + Bonus, "actual-equip-modifier");
        Move(IdentityType.Inventory, 69, WearSlot);
        Require(player.Inventory.Armor.Content[WearSlot].InstanceId == SecondItem && player.Inventory.Inventory.Content[69].InstanceId == FirstItem
            && player.Stats.Get(BonusStat) == baseline + Bonus, "actual-replacement-swap");
        Move(IdentityType.ArmorPage, WearSlot, 68);
        Require(player.Stats.Get(BonusStat) == baseline, "actual-unequip-clears-modifier");
        Move(IdentityType.Inventory, 68, WearSlot);
        player.Rebase(); player.Rebase();
        snapshot.Commit(player);
        Require(dao.LoadStats(owner).Single(s => s.StatId == (int)BonusStat).StatValue == 100, "effective-equipment-bonus-persisted-as-base");
        Player reloaded = Load(); reloaded.Rebase(); reloaded.Rebase();
        Require(reloaded.Stats.Get(BonusStat) == baseline + Bonus && reloaded.Inventory.Armor.Content[WearSlot].InstanceId == SecondItem,
            "real-dao-reload-compounded-equipment");
        VerifyRuntimeFailures();
        VerifySnapshotSerialization();
        if (originalWear == null) selectedTemplate.SpellList.Remove(EventType.OnWear);
        else selectedTemplate.SpellList[EventType.OnWear] = originalWear;
        Console.WriteLine($"CHARACTER_DAO_GAMEPLAY=PASS LOOT_CLAIMS=2 EQUIP=PASS REPLACE_SWAP=PASS UNEQUIP=PASS REEQUIP=PASS SNAPSHOT_BASE_ONLY=PASS TEMPLATE={TemplateId} SLOT={WearSlot} SYNTHETIC_IN_MEMORY_MODIFIER={Bonus} IDENTITIES={FirstItem},{SecondItem} CATALOG_WEAR_EFFECTS=NOT_PROVEN");

        Player Load()
        {
            var hydrated = loader.LoadForLogin(owner) ?? throw new FixtureFailure("full-dao-hydration-missing");
            var p = new Player(new Identity { Type = IdentityType.CanbeAffected, Instance = owner }, logger, builder)
            { Name = hydrated.Character.Name, Position = new Vector3(hydrated.Character.X, hydrated.Character.Y, hydrated.Character.Z),
                Rotation = new AORebirth.Core.Vector.Quaternion(hydrated.Character.HeadingX, hydrated.Character.HeadingY, hydrated.Character.HeadingZ, hydrated.Character.HeadingW) };
            foreach (var stat in hydrated.Stats) p.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base);
            p.Inventory.Apply(hydrated, owner, builder);
            foreach (int nano in hydrated.UploadedNanoIds) p.TryAddUploadedNano(nano);
            return p;
        }
        void Claim(int source, int destination)
        {
            int acks = session.Messages.OfType<ContainerAddItemMessage>().Count();
            moves.Handle(player, new ClientMoveItemToInventoryMessage { Identity = player.Identity,
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = (loot.InventoryHandle << 16) | source }, TargetPlacement = destination });
            Require(session.Messages.OfType<ContainerAddItemMessage>().Count() == acks + 1, "loot-success-ack");
        }
        void Move(IdentityType sourceType, int source, int destination)
        {
            int acks = session.Messages.OfType<ContainerAddItemMessage>().Count();
            moves.Handle(player, new ClientMoveItemToInventoryMessage { Identity = player.Identity,
                SourceContainer = new Identity { Type = sourceType, Instance = source }, TargetPlacement = destination });
            moves.Tick(world, 3600);
            Require(session.Messages.OfType<ContainerAddItemMessage>().Count() == acks + 1, "equipment-success-ack");
        }

        void VerifyRuntimeFailures()
        {
            var fault = new PersistenceFault { FailAfterWrite = 3 };
            var failureDao = new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), fault));
            var faultInventory = new MySqlInventoryRepository(logger, failureDao);
            var faultActions = new InventoryActionService(new MySqlInventoryMutationPersistence(faultInventory, nanos), flush, allocator, logger, catalog, builder);
            var faultMoves = new InventoryMoveService(logger, flush, faultActions);
            string equipped = Fingerprint();
            int equipAcks = session.Messages.OfType<ContainerAddItemMessage>().Count();
            faultMoves.Handle(player, new ClientMoveItemToInventoryMessage { Identity = player.Identity,
                SourceContainer = new Identity { Type = IdentityType.Inventory, Instance = 69 }, TargetPlacement = WearSlot });
            faultMoves.Tick(world, 3600);
            Require(Fingerprint() == equipped && player.Inventory.Armor.Content[WearSlot].InstanceId == SecondItem
                && player.Inventory.Inventory.Content[69].InstanceId == FirstItem
                && session.Messages.OfType<ContainerAddItemMessage>().Count() == equipAcks, "partial-equipment-swap-not-rolled-back");
            fault.FailAfterWrite = 2; fault.Writes = 0; fault.RollbackCalls = 0;
            var request = new ClientMoveItemToInventoryMessage { Identity = player.Identity,
                SourceContainer = new Identity { Type = IdentityType.Inventory, Instance = 69 }, TargetPlacement = 70 };
            string before = Fingerprint();
            int acks = session.Messages.OfType<ContainerAddItemMessage>().Count();
            faultMoves.Handle(player, request);
            Require(fault.RollbackCalls == 1 && !player.IsPersistenceQuarantined && Fingerprint() == before
                && player.Inventory.Inventory.Content[69].InstanceId == FirstItem
                && session.Messages.OfType<ContainerAddItemMessage>().Count() == acks, "runtime-known-failure-published-or-quarantined");
            fault.FailAfterWrite = -1; fault.CommitFault = CommitFault.After;
            faultMoves.Handle(player, request);
            Require(player.IsPersistenceQuarantined && session.State == SessionState.Closed
                && player.Inventory.Inventory.Content[69].InstanceId == FirstItem
                && dao.LoadCarriedItems(owner).Single(i => i.InstanceId == FirstItem).ContainerPlacement == 70
                && session.Messages.OfType<ContainerAddItemMessage>().Count() == acks, "runtime-unknown-outcome-not-quarantined-or-acknowledged");
            int writes = fault.Writes;
            faultMoves.Handle(player, request);
            bool refused = false;
            try { snapshot.Commit(player); } catch (InvalidOperationException) { refused = true; }
            Require(refused && fault.Writes == writes, "quarantined-old-owner-replayed-or-saved");
            manager.UnregisterPlayer(player);
            player = Load(); player.Playfield = world;
            session = new GameplaySession(); session.BindPlayer(player); player.Session = session;
            manager.RegisterPlayer(player);
            Move(IdentityType.Inventory, 70, 69);
            player.Rebase(); snapshot.Commit(player);
            Require(dao.LoadCarriedItems(owner).Count == 6 && player.Inventory.Inventory.Content[69].InstanceId == FirstItem,
                "authoritative-reload-did-not-recover-unknown-outcome");
            Console.WriteLine("CHARACTER_DAO_RUNTIME_FAILURES=PASS KNOWN_FAILURE_NO_ACK=PASS UNKNOWN_NO_ACK=PASS QUARANTINE=PASS STALE_SNAPSHOT_REFUSED=PASS RELOAD_RECOVERY=PASS NO_BLIND_REPLAY=PASS");
        }

        void VerifyLootCommitFailure()
        {
            var fault = new PersistenceFault { FailAfterWrite = 1 };
            var failureDao = new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), fault));
            var faultInventory = new MySqlInventoryRepository(logger, failureDao);
            var faultActions = new InventoryActionService(new MySqlInventoryMutationPersistence(faultInventory, nanos), flush, allocator, logger, catalog, builder);
            var faultMoves = new InventoryMoveService(logger, flush, faultActions);
            var claim = new ClientMoveItemToInventoryMessage { Identity = player.Identity,
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = loot.InventoryHandle << 16 }, TargetPlacement = 68 };
            string before = Fingerprint();
            int acks = session.Messages.OfType<ContainerAddItemMessage>().Count();
            faultMoves.Handle(player, claim);
            Require(Fingerprint() == before && loot.Loot.Content.ContainsKey(0) && !player.IsPersistenceQuarantined
                && !player.Inventory.Inventory.Content.ContainsKey(68) && session.Messages.OfType<ContainerAddItemMessage>().Count() == acks,
                "loot-known-failure-granted-or-acknowledged");
            fault.FailAfterWrite = -1; fault.CommitFault = CommitFault.After;
            faultMoves.Handle(player, claim);
            Require(player.IsPersistenceQuarantined && session.State == SessionState.Closed && loot.Loot.Content.ContainsKey(0)
                && !player.Inventory.Inventory.Content.ContainsKey(68) && dao.LoadCarriedItems(owner).Count(i => i.InstanceId == FirstItem) == 1
                && session.Messages.OfType<ContainerAddItemMessage>().Count() == acks, "loot-unknown-failure-not-durable-and-quarantined");
            manager.UnregisterPlayer(player); registry.Unregister(player.Identity);
            player = Load(); player.Playfield = world;
            session = new GameplaySession(); session.BindPlayer(player); player.Session = session;
            manager.RegisterPlayer(player); registry.Register(player);
            string committed = Fingerprint();
            // The stale corpse projection still has the same leased ID. A fresh session must
            // not turn it into a second reward at a different empty inventory slot.
            claim.TargetPlacement = 70;
            moves.Handle(player, claim);
            Require(Fingerprint() == committed && player.Inventory.Inventory.Content[68].InstanceId == FirstItem
                && !player.Inventory.Inventory.Content.ContainsKey(70) && session.Messages.OfType<ContainerAddItemMessage>().Count() == 0,
                "loot-claim-duplicated-after-authoritative-reload");
            Console.WriteLine("CHARACTER_DAO_LOOT_FAILURES=PASS PARTIAL_INSERT_ROLLBACK=PASS LOST_COMMIT_ACK=QUARANTINED RELOADED_ID_PRESERVED=PASS STALE_CORPSE_RECLAIM_REJECTED=PASS");
        }
        string Fingerprint() { using var reopened = fixture.Open(); return FixtureSql.Fingerprint(reopened, includeAutoIncrementCounters: false); }

        void VerifySnapshotSerialization()
        {
            var credits = new MySqlTradePersistence(inventory, nanos);
            int oldCash = player.Stats.Get(CharacterStat.Cash);
            using var started = new ManualResetEventSlim();
            Task waitingSnapshot;
            lock (player.PersistenceGate)
            {
                waitingSnapshot = Task.Run(() => { started.Set(); snapshot.Commit(player); });
                Require(started.Wait(TimeSpan.FromSeconds(10)), "snapshot-worker-not-started");
                // Existing item/credit transaction and in-memory publication share this gate.
                credits.Persist(new TradePersistenceBatch([], [], [new TradeCharacterWrite(owner, oldCash + 7, [])]));
                player.Stats.Set(CharacterStat.Cash, oldCash + 7, StatDetail.Base, dirty: true);
                Require(!waitingSnapshot.IsCompleted, "snapshot-bypassed-persistence-gate");
            }
            Require(waitingSnapshot.Wait(TimeSpan.FromSeconds(10))
                && dao.LoadStats(owner).Single(s => s.StatId == (int)CharacterStat.Cash).StatValue == oldCash + 7,
                "older-snapshot-overwrote-new-credit-transaction");
            lock (player.PersistenceGate)
            {
                credits.Persist(new TradePersistenceBatch([], [], [new TradeCharacterWrite(owner, oldCash, [])]));
                player.Stats.Set(CharacterStat.Cash, oldCash, StatDetail.Base, dirty: true);
                snapshot.Commit(player);
            }
            Console.WriteLine("CHARACTER_DAO_SNAPSHOT_SERIALIZATION=PASS REAL_CREDIT_TRANSACTION=PASS BLOCKED_SNAPSHOT_BUILDS_CURRENT_BASE=PASS");
        }
    }

    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    static void Require(bool condition, string detail) { if (!condition) throw new FixtureFailure(detail); }
    sealed class FixtureLoot() : LootableDynel(new Identity { Type = IdentityType.Corpse, Instance = 9822 }, IdentityType.Corpse, 9);
}

public class CatalogRoot : DispatchProxy
{
    public string Root = "";
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method?.Name == "get_RootPath" ? Root : throw new NotSupportedException("Catalog fixture only supplies a filesystem root.");
}

sealed class GameplaySession : IZoneSession
{
    public readonly List<MessageBody> Messages = new();
    public SessionState State { get; set; } = SessionState.InPlay;
    public Player? Player { get; private set; }
    public void BindPlayer(Player player) => Player = player;
    public void UnbindPlayer() => Player = null;
    public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
    public void Send(byte[] packet) => throw new NotSupportedException();
    public void Send(Message message) => throw new NotSupportedException();
    public void Send(MessageBody message) => Messages.Add(message);
    public void Send(MessageBody message, int sender, int receiver) => Messages.Add(message);
    public void SendInitiateCompression() => throw new NotSupportedException();
    public void Close() => State = SessionState.Closed;
}
