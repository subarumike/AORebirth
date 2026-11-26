namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.Modules.World;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Modules;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Quests;

using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>Module API 1.2: the world view, tick-marshalled actions, capabilities and the Knubot reload refresh.</summary>
[TestClass]
public sealed class ModuleApiTests
{
    const int PlayfieldId = 4321;

    [TestMethod]
    public void WorldSnapshotsAreDetachedFromLiveEntities()
    {
        using var rig = new Rig();
        Player player = rig.AddPlayer(901, new Vector3(10, 0, 10));
        NpcCharacter npc = rig.AddNpc(1_200_001, "TSTA", new Vector3(12, 0, 10));
        npc.Stats.Set(CharacterStat.Level, 20);
        var world = new ZoneModuleWorld(() => rig.Manager);

        PlayerInfo? seenPlayer = null;
        NpcInfo? seenNpc = null;
        int? level = null;
        rig.Owner(() =>
        {
            seenPlayer = world.Player(901);
            seenNpc = world.Npc(PlayfieldId, npc.Identity.Instance);
            level = world.Stat(EntityRef.Character(npc.Identity.Instance), (int)CharacterStat.Level);
        });

        Assert.IsNotNull(seenPlayer);
        Assert.IsNotNull(seenNpc);
        Assert.AreEqual(20, level);
        npc.Name = "Renamed";
        npc.Position = new Vector3(500, 0, 500);
        npc.Stats.Set(CharacterStat.Level, 99);
        player.Position = new Vector3(700, 0, 700);

        Assert.AreEqual("Test NPC", seenNpc!.Name);
        Assert.AreEqual(20, seenNpc.Level);
        Assert.AreEqual(12f, seenNpc.Position.X);
        Assert.AreEqual(10f, seenPlayer!.Position.X);
        Assert.AreEqual(PlayfieldId, seenPlayer.PlayfieldId);
        CollectionAssert.AreEqual(new[] { PlayfieldId }, world.Playfields().ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WorldPlayersKeepsNewestSnapshotDuringTransferOverlap(bool destinationFirst)
    {
        using var rig = new Rig();
        Playfield second = rig.AddPlayfield(PlayfieldId + 1);
        Playfield source = destinationFirst ? second : rig.World;
        Playfield destination = destinationFirst ? rig.World : second;
        Player moving = rig.AddPlayer(906, new Vector3(10, 0, 10), source);
        Player resident = rig.AddPlayer(907, new Vector3(20, 0, 20), destination);
        var snapshots = new ZoneModuleWorld(() => rig.Manager);
        IZoneWorld world = new GatedWorld(snapshots,
            new CapabilityGate("WorldReader", [ModuleCapabilities.ReadWorld], new StubLogger()));

        rig.ObserveFromOtherTick(world.Players);
        rig.Publish(snapshots, source);
        PlayerInfo before = rig.ObserveFromOtherTick(world.Players).Single();
        Assert.AreEqual(source.Identity.Instance, before.PlayfieldId);
        Assert.IsTrue(before.IsConnected);

        rig.Depart(moving);
        rig.Arrive(moving, destination, new Vector3(30, 0, 40));
        rig.Owner(destination, () =>
        {
            moving.Stats.Set(CharacterStat.Health, 73);
            moving.EnterLinkDead(TimeSpan.FromMinutes(1));
        });
        rig.Publish(snapshots, destination);

        IReadOnlyList<PlayerInfo> overlapping = rig.ObserveFromOtherTick(world.Players);
        CollectionAssert.AreEquivalent(new[] { moving.Identity.Instance, resident.Identity.Instance },
            overlapping.Select(info => info.CharacterId).ToArray());
        PlayerInfo transferred = overlapping.Single(info => info.CharacterId == moving.Identity.Instance);
        Assert.AreEqual(overlapping.Single(info => info.CharacterId == resident.Identity.Instance).Name, transferred.Name,
            "Display names do not identify characters.");
        Assert.AreEqual(destination.Identity.Instance, transferred.PlayfieldId);
        Assert.AreEqual(new WorldPosition(30, 0, 40), transferred.Position);
        Assert.AreEqual(73, transferred.Health);
        Assert.IsFalse(transferred.IsConnected);
        Assert.AreEqual(transferred, rig.ObserveFromOtherTick(() => world.Player(moving.Identity.Instance)));

        rig.Owner(destination, () => moving.EnterOnline(new CaptureSession()));
        rig.Publish(snapshots, destination);

        PlayerInfo reconnected = rig.ObserveFromOtherTick(world.Players).Single(info => info.CharacterId == moving.Identity.Instance);
        Assert.AreEqual(destination.Identity.Instance, reconnected.PlayfieldId);
        Assert.IsTrue(reconnected.IsConnected);
        Assert.IsFalse(transferred.IsConnected, "Previously returned snapshots remain detached.");
        Assert.AreEqual(source.Identity.Instance, before.PlayfieldId);
    }

    [TestMethod]
    public void WorldPlayersMergesInterleavedAndRepeatedTransfersIndependently()
    {
        using var rig = new Rig();
        Playfield first = rig.World;
        Playfield second = rig.AddPlayfield(PlayfieldId + 1);
        Playfield third = rig.AddPlayfield(PlayfieldId + 2);
        Player a = rig.AddPlayer(908, new Vector3(10, 0, 10), first);
        Player b = rig.AddPlayer(909, new Vector3(20, 0, 20), second);
        var snapshots = new ZoneModuleWorld(() => rig.Manager);
        IZoneWorld world = snapshots;

        rig.ObserveFromOtherTick(world.Players);
        rig.Publish(snapshots, first);
        rig.Publish(snapshots, second);

        rig.Depart(a);
        rig.Depart(b);
        rig.Arrive(a, third, new Vector3(30, 0, 30));
        rig.Arrive(b, first, new Vector3(40, 0, 40));
        rig.Publish(snapshots, third);

        Dictionary<int, PlayerInfo> partial = rig.ObserveFromOtherTick(world.Players).ToDictionary(info => info.CharacterId);
        Assert.AreEqual(2, partial.Count);
        Assert.AreEqual(third.Identity.Instance, partial[a.Identity.Instance].PlayfieldId);
        Assert.AreEqual(second.Identity.Instance, partial[b.Identity.Instance].PlayfieldId,
            "The destination has yet to publish the moved character.");

        rig.Publish(snapshots, first);
        rig.Depart(a);
        rig.Arrive(a, second, new Vector3(50, 0, 50));
        rig.Publish(snapshots, second);
        rig.Depart(a);
        rig.Arrive(a, first, new Vector3(60, 0, 60));
        rig.Publish(snapshots, first);

        Dictionary<int, PlayerInfo> latest = rig.ObserveFromOtherTick(world.Players).ToDictionary(info => info.CharacterId);
        Assert.AreEqual(2, latest.Count);
        Assert.AreEqual(first.Identity.Instance, latest[a.Identity.Instance].PlayfieldId);
        Assert.AreEqual(new WorldPosition(60, 0, 60), latest[a.Identity.Instance].Position);
        Assert.AreEqual(first.Identity.Instance, latest[b.Identity.Instance].PlayfieldId);
        Assert.AreEqual(new WorldPosition(40, 0, 40), latest[b.Identity.Instance].Position);
    }

    [TestMethod]
    public void ModuleThreadReadWaitsForTheTickPublish()
    {
        using var rig = new Rig();
        rig.AddNpc(1_200_002, "TSTB", new Vector3(12, 0, 10));
        var world = new ZoneModuleWorld(() => rig.Manager);
        ZoneModuleWorld.WaitLimit = TimeSpan.FromSeconds(5);
        try
        {
            Task<IReadOnlyList<NpcInfo>> read = Task.Run(() => world.Npcs(PlayfieldId));
            // Stand in for the playfield's ticks until the reader has its snapshot.
            for (int i = 0; i < 400 && !read.IsCompleted; i++)
            {
                rig.Owner(() => world.OnTick(rig.World));
                Thread.Sleep(5);
            }

            Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, read.Result.Count);
            Assert.AreEqual(1_200_002, read.Result[0].Instance);
        }
        finally
        {
            ZoneModuleWorld.WaitLimit = TimeSpan.FromMilliseconds(500);
        }
    }

    [TestMethod]
    public void ActionsRunOnTheOwningTick()
    {
        using var rig = new Rig();
        rig.AddPlayer(902, new Vector3(10, 0, 10));
        var actions = new ZoneModuleActions(() => rig.Manager);
        bool? held = null;

        Assert.IsTrue(rig.World.Dispatch(() => held = rig.World.IsTickHeldByCurrentThread));
        Task<bool> message = actions.SendSystemMessage(902, "hello");
        Assert.IsNull(held, "Dispatch from a module thread must queue, not run inline.");
        Assert.IsFalse(message.IsCompleted);
        Assert.AreEqual(0, rig.Session(902).Bodies.OfType<ChatTextMessage>().Count());

        rig.Drain();

        Assert.IsTrue(held);
        Assert.IsTrue(message.Wait(TimeSpan.FromSeconds(5)) && message.Result);
        Assert.AreEqual("hello", rig.Session(902).Bodies.OfType<ChatTextMessage>().Single().Text);
        Assert.IsFalse(actions.SendSystemMessage(903, "nobody").Result);
    }

    [TestMethod]
    public void RefreshNpcOnADespawnedNpcReturnsFalse()
    {
        using var rig = new Rig();
        NpcCharacter npc = rig.AddNpc(1_200_003, "TSTC", new Vector3(12, 0, 10));
        var actions = new ZoneModuleActions(() => rig.Manager);
        rig.Owner(() =>
        {
            rig.Locality.UnregisterDynel(npc);
            rig.Registry.Unregister(npc.Identity);
            npc.Playfield = null;
        });

        Task<bool> refresh = actions.RefreshNpc(PlayfieldId, npc.Identity.Instance);
        rig.Drain();

        Assert.IsTrue(refresh.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsFalse(refresh.Result);
        Assert.IsFalse(actions.RefreshNpc(PlayfieldId + 1, npc.Identity.Instance).Result, "Unknown playfield.");
    }

    [TestMethod]
    public void UngrantedCapabilityThrows()
    {
        using var rig = new Rig();
        var gate = new CapabilityGate("Example", [ModuleCapabilities.ReadWorld], new StubLogger());
        var actions = new GatedActions(new ZoneModuleActions(() => rig.Manager), gate);
        var world = new GatedWorld(new ZoneModuleWorld(() => rig.Manager), gate);

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => actions.SendSystemMessage(1, "x"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => actions.RefreshNpc(PlayfieldId, 1));
        CollectionAssert.AreEqual(new[] { PlayfieldId }, world.Playfields().ToArray());

        // No Capabilities entry: 1.1 behaviour, everything allowed.
        var open = new GatedActions(new ZoneModuleActions(() => rig.Manager), new CapabilityGate("Old", null, new StubLogger()));
        Assert.IsFalse(open.SendSystemMessage(1, "x").Result);
    }

    [TestMethod]
    public void KnubotReloadTellsVisiblePlayersWithoutRezoning()
    {
        using var rig = new Rig();
        Player player = rig.AddPlayer(904, new Vector3(10, 0, 10));
        NpcCharacter npc = rig.AddNpc(1_200_004, "TSTD", new Vector3(12, 0, 10));
        rig.Owner(() => rig.Locality.ActivatePlayerVisibility(player));
        Assert.IsTrue(rig.Locality.IsVisibleTo(npc, player));
        Assert.IsFalse(KnubotFlags(rig.Session(904), npc).Any(), "No script: no HasKnubotData on spawn.");
        rig.Session(904).Bodies.Clear();

        string script = Path.Combine(rig.Root, KnubotCatalog.DirectoryName, "test.json");
        File.WriteAllText(script, """
            { "Npcs": ["TSTD"], "Openers": [ { "Say": "hi" } ], "Lines": { "hi": { "Text": "Hello." } } }
            """);
        KnubotLoadResult loaded = rig.Knubot.Reload();
        Assert.AreEqual(1, loaded.Loaded, string.Join("; ", loaded.Problems));
        Assert.IsFalse(KnubotFlags(rig.Session(904), npc).Any(), "The refresh runs on the tick, not the reloading thread.");
        rig.Drain();
        CollectionAssert.AreEqual(new uint[] { 1 }, KnubotFlags(rig.Session(904), npc).ToArray());

        rig.Session(904).Bodies.Clear();
        File.Delete(script);
        rig.Knubot.Reload();
        rig.Drain();
        CollectionAssert.AreEqual(new uint[] { 0 }, KnubotFlags(rig.Session(904), npc).ToArray());

        // A reload that changes nothing sends nothing.
        rig.Session(904).Bodies.Clear();
        rig.Knubot.Reload();
        rig.Drain();
        Assert.IsFalse(KnubotFlags(rig.Session(904), npc).Any());
    }

    [TestMethod]
    public void ModuleGmCommandRunsThroughTheDispatcherWithItsGmLevel()
    {
        using var rig = new Rig();
        Player player = rig.AddPlayer(905, new Vector3(10, 0, 10));
        ZoneModuleHost host = rig.ModuleHost();
        IReadOnlyList<string>? seen = null;
        Register(host, "Example", new AORebirth.Modules.Commands.ModuleGmCommand
        {
            Name = "hello", RequiredGmLevel = 2, Summary = "Says hello.",
            Execute = run => { seen = run.Args; run.Reply("hi " + run.CharacterId); }
        });
        var dispatcher = new ZoneEngine_New.Core.Commands.GmCommandDispatcher([new BuiltIn("spawn")], host, new StubLogger());

        player.Stats.Set(CharacterStat.GmLevel, 1);
        dispatcher.TryExecute(rig.Session(905), player, "HELLO", ["a"]);
        Assert.IsNull(seen, "Below the command's GM level.");

        player.Stats.Set(CharacterStat.GmLevel, 2);
        dispatcher.TryExecute(rig.Session(905), player, "hello", ["a", "b"]);
        CollectionAssert.AreEqual(new[] { "a", "b" }, seen!.ToArray());
        Assert.IsTrue(rig.Session(905).Bodies.OfType<ChatTextMessage>().Any(m => m.Text == "hi 905"));
    }

    [TestMethod]
    public void ModuleGmCommandClashingWithABuiltInFailsThatModule()
    {
        using var rig = new Rig();
        ZoneModuleHost host = rig.ModuleHost();
        bool ran = false;
        Register(host, "Clash", new AORebirth.Modules.Commands.ModuleGmCommand { Name = "Spawn", Execute = _ => ran = true });
        Register(host, "Clash", new AORebirth.Modules.Commands.ModuleGmCommand { Name = "other", Execute = _ => ran = true });
        Register(host, "Fine", new AORebirth.Modules.Commands.ModuleGmCommand { Name = "fine", Execute = _ => { } });
        Assert.ThrowsExactly<TargetInvocationException>(() =>
            Register(host, "Fine", new AORebirth.Modules.Commands.ModuleGmCommand { Name = "FINE", Execute = _ => { } }),
            "Two modules cannot share a name.");

        var builtIn = new BuiltIn("spawn");
        IReadOnlyList<(string ModuleId, AORebirth.Modules.Commands.ModuleGmCommand Command)> bound = host.BindGmCommands(["spawn"]);

        CollectionAssert.AreEqual(new[] { "fine" }, bound.Select(x => x.Command.Name).ToArray(), "The clashing module loses all its commands.");
        Assert.IsFalse(ran);

        ZoneModuleHost required = rig.ModuleHost();
        ((HashSet<string>)Field(required, "_required")).Add("Clash");
        Register(required, "Clash", new AORebirth.Modules.Commands.ModuleGmCommand { Name = "spawn", Execute = _ => { } });
        Assert.ThrowsExactly<InvalidOperationException>(() => required.BindGmCommands([builtIn.Name]));
    }

    static void Register(ZoneModuleHost host, string moduleId, AORebirth.Modules.Commands.ModuleGmCommand command)
    {
        // What a module's Initialize does through its context.
        typeof(ZoneModuleHost).GetField("_initializing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, moduleId);
        try
        {
            typeof(ZoneModuleHost).GetMethod("RegisterGmCommand", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(host, [moduleId, command]);
        }
        finally
        {
            typeof(ZoneModuleHost).GetField("_initializing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, null);
        }
    }

    static object Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    sealed class BuiltIn(string name) : ZoneEngine_New.Core.Commands.IGmCommand
    {
        public string Name => name;
        public int RequiredGmLevel => 1;
        public string Usage => "." + name;
        public ZoneEngine_New.Core.Commands.GmCommandHelp Help { get; } = new("Built-in.", [], []);
        public void Execute(ZoneEngine_New.Core.Commands.GmCommandContext context) { }
    }

    static IEnumerable<uint> KnubotFlags(CaptureSession session, NpcCharacter npc)
        => session.Bodies.OfType<StatMessage>().Where(message => message.Identity == npc.Identity)
            .SelectMany(message => message.Stats).Where(stat => stat.Value1 == CharacterStat.HasKnubotData)
            .Select(stat => stat.Value2);

    sealed class Rig : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "aorebirth-modules-" + Guid.NewGuid().ToString("N"));
        internal readonly PlayfieldManager Manager = Blank<PlayfieldManager>();
        internal readonly Playfield World = Blank<Playfield>();
        internal readonly DynelRegistry Registry = new();
        internal readonly PlayfieldLocality Locality = new(PlayfieldId, null);
        internal readonly KnubotCatalog Knubot;
        readonly Dictionary<int, CaptureSession> _sessions = [];
        readonly ServiceProvider _services;
        readonly List<ServiceProvider> _additionalServices = [];

        internal Rig()
        {
            Directory.CreateDirectory(Path.Combine(Root, KnubotCatalog.DirectoryName));
            var logger = new StubLogger();
            var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: Root);
            string? overrideRoot = Environment.GetEnvironmentVariable(KnubotCatalog.OverrideVariable);
            Environment.SetEnvironmentVariable(KnubotCatalog.OverrideVariable, null);
            try
            {
                Knubot = new KnubotCatalog(data, new QuestCatalog(data, logger), new StubCatalog(), logger);
            }
            finally
            {
                Environment.SetEnvironmentVariable(KnubotCatalog.OverrideVariable, overrideRoot);
            }

            Set(Manager, "_sync", new Lock());
            Set(Manager, "_playersByCharacterId", new Dictionary<int, Player>());
            Set(Manager, "_playfields", new Dictionary<int, Playfield> { [PlayfieldId] = World });
            Set(World, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = PlayfieldId });
            Set(World, "_tickSync", new Lock());
            Set(World, "_lifecycleSync", new Lock());
            Set(World, "_inbound", new PlayfieldInboundQueue());
            Set(World, "_dynelRegistry", Registry);
            Set(World, "_logger", logger);
            Set(World, "_playfieldManager", Manager);
            var activation = new NpcContentActivationService(World, Registry, Locality, new StubItemBuilder(), new StubCatalog());
            _services = new ServiceCollection().AddSingleton(Registry).AddSingleton(Locality).AddSingleton(activation)
                .AddSingleton(Knubot).AddSingleton<IGameData>(data).BuildServiceProvider();
            Set(World, "_serviceProvider", _services);
            // What PlayfieldManager's constructor subscribes.
            Knubot.Reloaded += change => KnubotNpcRefresh.Queue(Manager, change);
        }

        internal Playfield AddPlayfield(int id)
        {
            var playfield = Blank<Playfield>();
            var registry = new DynelRegistry();
            var locality = new PlayfieldLocality(id, null);
            ServiceProvider services = new ServiceCollection().AddSingleton(registry).AddSingleton(locality).BuildServiceProvider();
            _additionalServices.Add(services);
            Set(playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = id });
            Set(playfield, "_tickSync", new Lock());
            Set(playfield, "_lifecycleSync", new Lock());
            Set(playfield, "_dynelRegistry", registry);
            Set(playfield, "_serviceProvider", services);
            Set(playfield, "_playfieldManager", Manager);
            ((Dictionary<int, Playfield>)Get(Manager, "_playfields")).Add(id, playfield);
            return playfield;
        }

        internal Player AddPlayer(int id, Vector3 at, Playfield? playfield = null)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Stats.Set(CharacterStat.Health, 100);
            var session = new CaptureSession();
            _sessions[id] = session;
            player.EnterOnline(session);
            Arrive(player, playfield ?? World, at);
            Manager.RegisterPlayer(player);
            return player;
        }

        internal void Depart(Player player)
        {
            Playfield playfield = player.Playfield!;
            Owner(playfield, () =>
            {
                playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(player);
                playfield.GetRequiredService<DynelRegistry>().Unregister(player.Identity);
                player.Playfield = null;
            });
        }

        internal void Arrive(Player player, Playfield playfield, Vector3 at)
        {
            Owner(playfield, () =>
            {
                player.Playfield = playfield;
                player.Position = at;
                playfield.GetRequiredService<DynelRegistry>().Register(player);
                playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);
            });
        }

        internal NpcCharacter AddNpc(int instance, string hash, Vector3 at)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new StubItemBuilder())
            {
                Name = "Test NPC", Playfield = World, Position = at, SpawnHash = hash
            };
            Registry.Register(npc);
            Locality.RegisterDynel(npc);
            return npc;
        }

        internal CaptureSession Session(int id) => _sessions[id];

        /// <summary>A host with no Modules.json (no module loaded), to register into directly.</summary>
        internal ZoneModuleHost ModuleHost() => ZoneModuleHost.Load(Root, new StubLogger(), () => Knubot, () => Manager);

        internal void Owner(Action work) => Owner(World, work);

        internal void Owner(Playfield playfield, Action work)
        {
            lock ((Lock)Get(playfield, "_tickSync"))
                work();
        }

        internal void Publish(ZoneModuleWorld world, Playfield playfield) => Owner(playfield, () => world.OnTick(playfield));

        internal T ObserveFromOtherTick<T>(Func<T> read)
        {
            // A module event on another tick asks these worlds to publish without refreshing or waiting for either here.
            FieldInfo ticking = typeof(Playfield).GetField("t_ticking", BindingFlags.Static | BindingFlags.NonPublic)!;
            int previous = (int)ticking.GetValue(null)!;
            ticking.SetValue(null, previous + 1);
            try { return read(); }
            finally { ticking.SetValue(null, previous); }
        }

        internal void Drain() => Owner(() => ((PlayfieldInboundQueue)Get(World, "_inbound")).Drain(null!, null!, World));

        public void Dispose()
        {
            foreach (ServiceProvider services in _additionalServices)
                services.Dispose();
            _services.Dispose();
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }

        static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        static object Get(object target, string name)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

        static void Set<T>(object target, string name, T value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    internal sealed class CaptureSession : IZoneSession
    {
        internal readonly List<MessageBody> Bodies = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public bool TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) { }
        public void Send(Message message) => Bodies.Add(message.Body);
        public void Send(MessageBody body) => Bodies.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
