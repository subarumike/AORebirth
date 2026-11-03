namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using AODB.Common.RDBObjects;
    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Inventory.Dat;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Integration with the selected private runtime content, not copied spell/placement fixtures. These checks
    /// exercise the explicitly classified server compatibility profile; they do not establish retail behavior.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class CryptDoorIntegrationTests
    {
        static readonly Lazy<CryptAssets> Assets = new(CryptAssets.Load);

        [TestMethod]
        [DataRow(0, 17, -25)]
        [DataRow(-1, 0, 125)]
        [DataRow(-2, -1, -400)]
        public void CatalogPreservesRawCountSignedIntervalAndMetadata35(int count, int metadata35, int interval)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ao-object-catalog-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, StaticObjectStateMachineCatalog.FileName);
            string? previousOverride = Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName);
            Directory.CreateDirectory(directory);
            try
            {
                Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, null);
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    SchemaVersion = 1,
                    Machines = new[]
                    {
                        new
                        {
                            Id = 1, InitialState = 1, InitialStateCompatibility = "Test binding",
                            States = new[]
                            {
                                new
                                {
                                    Id = 1,
                                    EntrySpells = new[]
                                    {
                                        new
                                        {
                                            FunctionType = (int)AORebirth.Enums.FunctionType.AreaTrigger,
                                            Target = 1, TickCount = count, TickInterval = interval, Metadata35 = metadata35,
                                            Arguments = new[] { 4, 1 }, Requirements = Array.Empty<object>()
                                        }
                                    },
                                    SignalTimers = Array.Empty<object>(), Transitions = Array.Empty<object>()
                                }
                            }
                        }
                    },
                    SignalPolicies = Array.Empty<object>()
                }));
                StaticObjectStateMachineCatalog catalog = StaticObjectStateMachineCatalog.Load(directory);
                Assert.IsTrue(catalog.TryGet(1, out StaticObjectStateMachine machine));
                ItemSpell spell = machine.States[1].EntrySpells.Single();
                Assert.AreEqual(count, spell.TickCount);
                Assert.AreEqual(interval, unchecked((int)spell.TickInterval));
                Assert.AreEqual(metadata35, spell.Metadata35);
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, previousOverride);
                File.Delete(path);
                Directory.Delete(directory);
            }
        }

        [TestMethod]
        public void CanonicalPlacementsBindTheSkeletonAndKeepUnrelatedControlsUnbound()
        {
            using var crypt = new CryptWorld(Assets.Value);
            Assert.AreEqual(34, crypt.Objects.Count);
            StaticDynel[] doors = crypt.Objects.Values.Where(item => item.Identity.Type == IdentityType.Door).ToArray();
            Assert.AreEqual(23, doors.Length);
            Assert.AreEqual(5, doors.Count(IsLocked));
            Assert.AreEqual(2, doors.Count(IsOpen));
            Assert.AreEqual(1, crypt.Objects.Values.Count(item => item.Stats.GetOrZero(CharacterStat.StateMachine) == 200964));
            Assert.AreEqual(1, crypt.Skeleton.Stats.GetOrZero(CharacterStat.State));
            Assert.AreEqual(0, crypt.Control("C00112C5").Stats.GetOrZero(CharacterStat.StateMachine));
            Assert.AreEqual(0, crypt.Control("C00212C5").Stats.GetOrZero(CharacterStat.StateMachine));
        }

        [TestMethod]
        public void EntrancePairOpensForBothPlayersRelocksAndCanBeUsedAgain()
        {
            using var crypt = new CryptWorld(Assets.Value);
            StaticDynel door = crypt.Door("C00F12C5");
            StaticDynel first = crypt.Control("C00112C5");
            StaticDynel second = crypt.Control("C00212C5");
            int unrelatedFlags = OtherFlags(door);

            crypt.Use(first, crypt.World.First);
            crypt.Step();
            Assert.IsTrue(IsLocked(door));
            crypt.Use(second, crypt.World.Second);
            crypt.Advance(0.2);
            AssertDoor(door, open: true, locked: false, unrelatedFlags);
            crypt.AssertBothSaw(door, open: true, locked: false);
            Assert.IsFalse(first.TryUse(crypt.World.First), "The authored disable signal must gate another use.");
            Assert.IsFalse(second.TryUse(crypt.World.Second));

            // The resource's close action is delayed by 400 centiseconds. It is visited by the odd-pulse pump,
            // so its dispatch must not precede the delay and need not occur at an exact wall-clock instant.
            crypt.Advance(3.5);
            Assert.IsTrue(IsOpen(door), "The delayed close must not run immediately or before four seconds.");
            crypt.AdvanceUntil(() => IsLocked(door), 3);
            AssertDoor(door, open: false, locked: true, unrelatedFlags);
            crypt.AssertBothSaw(door, open: false, locked: true);
            crypt.AdvanceUntil(() => crypt.World.Triggers.IsUseEnabled(first)
                && crypt.World.Triggers.IsUseEnabled(second), 3);

            crypt.ClearMessages();
            crypt.Use(first, crypt.World.First);
            crypt.Step();
            crypt.Use(second, crypt.World.First);
            crypt.Advance(0.2);
            AssertDoor(door, open: true, locked: false, unrelatedFlags);
            crypt.AssertBothSaw(door, open: true, locked: false);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SimultaneousEntranceSignalsAreRetainedWithoutRequiringDifferentPlayers(bool samePlayer)
        {
            using var crypt = new CryptWorld(Assets.Value);
            StaticDynel door = crypt.Door("C00F12C5");
            crypt.Use(crypt.Control("C00112C5"), crypt.World.First);
            crypt.Use(crypt.Control("C00212C5"), samePlayer ? crypt.World.First : crypt.World.Second);

            Assert.IsTrue(IsLocked(door), "Queued uses must not recursively run the machine during submission.");
            crypt.Step();
            Assert.IsTrue(IsLocked(door), "One machine run cannot consume both serialized activations.");
            crypt.Advance(0.2);
            AssertDoor(door, open: true, locked: false);
            crypt.AssertBothSaw(door, open: true, locked: false);
        }

        [TestMethod]
        public void OneEntranceSignalExpiresAfterThreeOddPulsesWithoutOpening()
        {
            using var crypt = new CryptWorld(Assets.Value);
            StaticDynel door = crypt.Door("C00F12C5");
            crypt.Use(crypt.Control("C00112C5"), crypt.World.First);
            crypt.Advance(3.2);
            Assert.AreEqual(2, crypt.Skeleton.Stats.GetOrZero(CharacterStat.State),
                "Three eligible odd pulses must not be replaced with a three-second timeout.");
            Assert.IsTrue(IsLocked(door));
            crypt.AdvanceUntil(() => crypt.Skeleton.Stats.GetOrZero(CharacterStat.State) == 1, 3);
            AssertDoor(door, open: false, locked: true);
            Assert.IsFalse(crypt.World.FirstSession.Sent.OfType<DoorStatusUpdateMessage>()
                .Any(message => message.Identity == door.Identity && message.Unknown3 != 0));
            Assert.IsFalse(crypt.World.SecondSession.Sent.OfType<DoorStatusUpdateMessage>()
                .Any(message => message.Identity == door.Identity && message.Unknown3 != 0));
        }

        [TestMethod]
        [DataRow("C00312C5", "C00712C5", true, 20)]
        [DataRow("C00412C5", "C00C12C5", true, 20)]
        [DataRow("C00512C5", "C00B12C5", true, 200)]
        [DataRow("C00612C5", "C00B12C5", true, 200)]
        [DataRow("C00912C5", "C00E12C5", false, 120)]
        [DataRow("C00A12C5", "C00E12C5", false, 120)]
        public void AuthoredControlChangesItsDoorThenPerformsItsTimedReset(
            string controlId, string doorId, bool opens, int delaySeconds)
        {
            using var crypt = new CryptWorld(Assets.Value);
            StaticDynel control = crypt.Control(controlId);
            StaticDynel door = crypt.Door(doorId);
            int unrelatedFlags = OtherFlags(door);

            crypt.Use(control, crypt.World.First);
            crypt.Step();
            AssertDoor(door, opens, locked: false, unrelatedFlags);
            crypt.AssertBothSaw(door, opens, locked: false);
            Assert.IsFalse(control.TryUse(crypt.World.First), "The control's own disable must prevent repeated activation.");
            Assert.AreEqual(0, crypt.World.SecondSession.Sent.OfType<ChatTextMessage>().Count(),
                "Authored target-2 feedback belongs to the invoking player, not every door viewer.");
            Assert.IsTrue(crypt.World.FirstSession.Sent.OfType<ChatTextMessage>().Any(),
                "The actual authored control list must retain its player feedback.");

            crypt.Advance(delaySeconds - 0.25);
            Assert.IsFalse(IsLocked(door), "The authored reset must not fire before its centisecond delay.");
            crypt.AdvanceUntil(() => IsLocked(door), 2.5);
            AssertDoor(door, open: false, locked: true, unrelatedFlags);
            crypt.AssertBothSaw(door, open: false, locked: true);
            crypt.AdvanceUntil(() => crypt.World.Triggers.IsUseEnabled(control), 2.5);

            crypt.ClearMessages();
            crypt.Use(control, crypt.World.Second);
            crypt.Step();
            AssertDoor(door, opens, locked: false, unrelatedFlags);
            crypt.AssertBothSaw(door, opens, locked: false);
        }

        [TestMethod]
        public void DistantUseIsRejectedBeforeTheAuthoredSwitchProgramRuns()
        {
            using var crypt = new CryptWorld(Assets.Value);
            StaticDynel control = crypt.Control("C00412C5");
            StaticDynel door = crypt.Door("C00C12C5");
            crypt.World.First.Position = new Vector3(control.Position.x + 50, control.Position.y, control.Position.z);
            Assert.IsFalse(control.CanBeginUse(crypt.World.First));
            Assert.IsFalse(control.TryUse(crypt.World.First));
            crypt.Advance(1);
            AssertDoor(door, open: false, locked: true);
            Assert.IsTrue(crypt.World.Triggers.IsUseEnabled(control));
            Assert.AreEqual(0, crypt.World.FirstSession.Sent.OfType<DoorStatusUpdateMessage>().Count());
            Assert.AreEqual(0, crypt.World.SecondSession.Sent.OfType<DoorStatusUpdateMessage>().Count());
        }

        static bool IsOpen(StaticDynel door) => (door.Stats.GetOrZero(CharacterStat.Flags) & Door.OpenFlag) != 0;
        static bool IsLocked(StaticDynel door) => (door.Stats.GetOrZero(CharacterStat.Flags) & Door.LockedFlag) != 0;
        static int OtherFlags(StaticDynel door) => door.Stats.GetOrZero(CharacterStat.Flags) & ~(Door.OpenFlag | Door.LockedFlag);

        static void AssertDoor(StaticDynel door, bool open, bool locked, int? unrelatedFlags = null)
        {
            Assert.AreEqual(open, IsOpen(door), "Open state of " + door.Identity);
            Assert.AreEqual(locked, IsLocked(door), "Lock state of " + door.Identity);
            if (unrelatedFlags.HasValue)
                Assert.AreEqual(unrelatedFlags.Value, OtherFlags(door), "Unrelated placed flags must survive door transitions.");
        }

        sealed class CryptWorld : IDisposable
        {
            public TriggerTestWorld World { get; }
            public Dictionary<Identity, StaticDynel> Objects { get; } = new();
            public StaticDynel Skeleton => Control("C00012C5");

            public CryptWorld(CryptAssets assets)
            {
                World = new TriggerTestWorld(assets.Catalog);
                var blobs = new ItemBase();
                foreach (PlayfieldDynel record in assets.Placements.Dynels)
                {
                    ItemTemplate template = assets.Builder.CreateTemplate(record.TemplateId, record.TemplateId, 1);
                    template = DynelEventSpells.WithOnUseFromDynel(template, record);
                    StaticDynel item = World.Add(record.IdentityInstance, (IdentityType)record.IdentityType, template,
                        new Vector3(record.Position.X, record.Position.Y, record.Position.Z));
                    if (record.Blob is { Length: > 12 })
                    {
                        foreach (KeyValuePair<int, int> stat in blobs.ReadDynelBlob(record.Blob).AppliedStats)
                            item.Stats.Set((CharacterStat)stat.Key, stat.Value, StatDetail.Base);
                    }
                    item.Rotation = new Quaternion(record.Heading.X, record.Heading.Y, record.Heading.Z, record.Heading.W);
                    World.World.BindStaticDoor(item);
                    Objects.Add(item.Identity, item);
                }
                World.Triggers.Initialize();
                ClearMessages();
            }

            public StaticDynel Control(string hex) => Find(IdentityType.Terminal, hex);
            public StaticDynel Door(string hex) => Find(IdentityType.Door, hex);
            StaticDynel Find(IdentityType type, string hex) => Objects[new Identity
            {
                Type = type,
                Instance = unchecked((int)Convert.ToUInt32(hex, 16))
            }];

            public void Use(StaticDynel control, Player player)
            {
                player.Position = new Vector3(control.Position.x, control.Position.y, control.Position.z);
                Assert.IsTrue(control.CanBeginUse(player), "Actual use gate rejected " + control.Identity);
                Assert.IsTrue(control.TryUse(player), "Actual use failed for " + control.Identity);
            }

            public void Step() => World.Tick(0.05);

            public void Advance(double seconds)
            {
                for (int i = 0; i < (int)Math.Ceiling(seconds / 0.05); i++)
                    Step();
            }

            public void AdvanceUntil(Func<bool> condition, double timeoutSeconds)
            {
                for (int i = 0; i < (int)Math.Ceiling(timeoutSeconds / 0.05) && !condition(); i++)
                    Step();
                Assert.IsTrue(condition(), "Authored transition did not complete within the bounded pulse allowance.");
            }

            public void AssertBothSaw(StaticDynel door, bool open, bool locked)
            {
                foreach (RecordingZoneSession session in new[] { World.FirstSession, World.SecondSession })
                {
                    DoorStatusUpdateMessage[] messages = session.Sent.OfType<DoorStatusUpdateMessage>()
                        .Where(message => message.Identity == door.Identity).ToArray();
                    Assert.IsTrue(messages.Length > 0, "A connected viewer did not receive the door state.");
                    DoorStatusUpdateMessage last = messages[^1];
                    Assert.AreEqual((byte)(open ? 1 : 0), last.Unknown3);
                    Assert.AreEqual((byte)(locked ? 1 : 0), last.Unknown2);
                }
            }

            public void ClearMessages()
            {
                World.FirstSession.Sent.Clear();
                World.SecondSession.Sent.Clear();
            }

            public void Dispose() => World.Dispose();
        }

        sealed record CryptAssets(PlayfieldDynels Placements, StubItemBuilder Builder, StaticObjectStateMachineCatalog Catalog)
        {
            public static CryptAssets Load()
            {
                string root = GameDataPaths.ResolveRuntimeRoot();
                string placementPath = Path.Combine(root, GameDataPaths.PlayfieldDynelsRelativePath(4805));
                string itemsPath = Path.Combine(root, GameDataPaths.ItemsFileName);
                string machinesPath = GameDataPaths.ResolveFile(root, StaticObjectStateMachineCatalog.FileName);
                foreach (string path in new[] { placementPath, itemsPath, machinesPath })
                {
                    if (!File.Exists(path))
                        Assert.Inconclusive("Selected private GameData is unavailable: " + path + ". Select it with " + GameDataPaths.EnvironmentVariableName + ".");
                }

                var placements = new PlayfieldDynels();
                using (var stream = File.OpenRead(placementPath))
                using (var reader = new BinaryReader(stream))
                {
                    placements.Deserialize(reader);
                    Assert.AreEqual(stream.Length, stream.Position, "The actual placed-object file must decode completely.");
                }
                var requiredTemplates = placements.Dynels.Select(record => record.TemplateId).ToHashSet();
                var builder = new StubItemBuilder();
                foreach (DatItemTemplate item in ItemsDatReader.Read(itemsPath))
                {
                    if (requiredTemplates.Remove(item.ID))
                        builder.Add(DatItemMapper.ToTemplate(item, item.Name ?? string.Empty));
                }
                Assert.AreEqual(0, requiredTemplates.Count, "Every actual placement template must be present in items.dat.");
                StaticObjectStateMachineCatalog catalog = StaticObjectStateMachineCatalog.Load(root, new StubLogger());
                Assert.IsTrue(catalog.TryGet(200964, out _), "The selected runtime catalog must contain the authored entrance machine.");
                return new CryptAssets(placements, builder, catalog);
            }
        }
    }
}
