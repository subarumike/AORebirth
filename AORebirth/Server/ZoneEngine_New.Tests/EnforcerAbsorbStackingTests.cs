namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class EnforcerAbsorbStackingTests
{
    const int Ward = 117680;
    const int ShrugAbsorb = 266820;
    const int CocoonMarker = 209802;
    const int Cocoon = 215576;
    const int FormOfTroll = 420;
    const int Endurance = 2300;
    static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static readonly Lazy<ItemBuilder> Items = new(() => new ItemBuilder(
        new ItemTemplateCatalog(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: GameDataPaths.ResolveRuntimeRoot()),
            new StubLogger()),
        new StubLogger()));

    [DataTestMethod]
    [DataRow(Ward, ShrugAbsorb)]
    [DataRow(ShrugAbsorb, Ward)]
    public void WardAndShrugStackInBothOrdersAndRefreshIndependently(int first, int second)
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.MaxNCU, 200);
        Assert.IsTrue(Land(player, first, Now));
        Buff original = RequireBuff(player, first);
        Assert.IsTrue(Land(player, second, Now.AddSeconds(1)));
        Assert.AreSame(original, RequireBuff(player, first));
        Assert.AreEqual(1, original.StackingOrder);
        Assert.AreEqual(1, RequireBuff(player, second).StackingOrder);
        Assert.AreEqual(560, Pool(player));

        Assert.AreEqual(0, player.TakeOffShields(player, 380, (int)CharacterStat.MeleeAC));
        Assert.AreEqual(180, RequireBuff(player, second).AbsorbLeft(CharacterStat.AbsorbMeleeAC));
        Assert.AreEqual(0, original.AbsorbLeft(CharacterStat.AbsorbMeleeAC));
        Assert.AreEqual(560, player.Stats.GetOrZero(CharacterStat.AbsorbProjectileAC));

        Assert.IsTrue(Land(player, second, Now.AddSeconds(2)));
        Assert.AreEqual(280, Pool(player));
        Assert.AreSame(original, RequireBuff(player, first));
        Assert.IsTrue(Land(player, first, Now.AddSeconds(3)));
        Assert.AreEqual(560, Pool(player));
        Assert.AreEqual(2, player.Buffs.Count);

        Assert.IsTrue(NanoRuntime.StripBuff(player, first));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(second, out _));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CocoonReplacesWardAndCoexistsWithShrugInBothOrders(bool shrugFirst)
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.MaxNCU, 200);
        Assert.IsTrue(Land(player, Ward, Now));
        if (shrugFirst)
            Assert.IsTrue(Land(player, ShrugAbsorb, Now));

        Assert.IsTrue(Land(player, CocoonMarker, Now.AddSeconds(1)));
        Assert.IsTrue(Land(player, Cocoon, Now.AddSeconds(1)));
        Assert.IsFalse(player.TryGetBuff(Ward, out _));
        Assert.IsFalse(Land(player, Ward, Now.AddSeconds(2)));
        if (!shrugFirst)
            Assert.IsTrue(Land(player, ShrugAbsorb, Now.AddSeconds(2)));

        CollectionAssert.AreEquivalent(new[] { ShrugAbsorb, CocoonMarker, Cocoon },
            player.Buffs.Select(buff => buff.Id).ToArray());
        Assert.AreEqual(10_280, Pool(player));
        Assert.IsTrue(Land(player, ShrugAbsorb, Now.AddSeconds(3)));
        Assert.IsTrue(Land(player, CocoonMarker, Now.AddSeconds(4)));
        Assert.IsTrue(Land(player, Cocoon, Now.AddSeconds(4)));
        Assert.AreEqual(10_280, Pool(player));

        NanoRuntime.Tick(player, Now.AddSeconds(34));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(ShrugAbsorb, out _));
        Assert.IsTrue(Land(player, Ward, Now.AddSeconds(35)));
        Assert.AreEqual(560, Pool(player));

        NanoRuntime.Tick(player, Now.AddSeconds(63));
        Assert.AreEqual(280, Pool(player));
        Assert.IsTrue(player.TryGetBuff(Ward, out _));
        Assert.IsFalse(player.TryGetBuff(ShrugAbsorb, out _));
    }

    [TestMethod]
    public void PausedWardAndShrugRoundTripWithProgressionAfterWriteFailure()
    {
        Player player = Restore(new ProgressionStore());
        DateTime now = DateTime.UtcNow;
        Assert.IsTrue(Land(player, Ward, now));
        Assert.IsTrue(Land(player, ShrugAbsorb, now));
        Assert.IsTrue(player.TryTrainPerk(FormOfTroll));
        Assert.IsTrue(player.TrySelectPersonalResearch(Endurance));
        Assert.AreEqual(0L, player.ApplyResearchXp(1000));
        player.SnapshotActiveNanosForLogout();

        var store = new ProgressionStore { FailNextWrite = true };
        using var flush = CreateFlush(store);
        Assert.ThrowsExactly<IOException>(() => flush.HardFlush(player));
        Assert.AreEqual(0, store.Nanos.Length);
        Assert.AreEqual(0, store.Perks.Length);
        Assert.AreEqual(0, store.Research.Length);
        Assert.IsFalse(player.IsPersistenceQuarantined);

        flush.HardFlush(player);
        Assert.AreEqual(2, store.Nanos.Length);
        Assert.IsTrue(store.Nanos.All(nano => nano.Strain == 219));
        Assert.AreEqual(2, store.Nanos.Select(nano => nano.NanoInstance).Distinct().Count());
        Assert.IsTrue(store.Nanos.All(nano => nano.ExpiresAtUtcTicks > 0
            && nano.ExpiresAtUtcTicks <= TimeSpan.FromHours(100).Ticks));

        Player reconnected = Restore(store);
        CollectionAssert.AreEquivalent(new[] { Ward, ShrugAbsorb }, reconnected.Buffs.Select(buff => buff.Id).ToArray());
        Assert.IsTrue(reconnected.TrainedPerks.Contains(FormOfTroll));
        Assert.IsTrue(reconnected.Research.TryGetRemaining(Endurance, out int remaining));
        Assert.AreEqual(49_000, remaining);

        Assert.IsTrue(Land(reconnected, Ward, now));
        NanoRuntime.Tick(reconnected, RequireBuff(reconnected, ShrugAbsorb).ExpiresAtUtc.AddTicks(1));
        reconnected.SnapshotActiveNanosForTransfer();
        flush.HardFlush(reconnected);
        CollectionAssert.AreEqual(new[] { Ward }, store.Nanos.Select(nano => nano.NanoId).ToArray());

        Assert.IsTrue(Land(reconnected, CocoonMarker, DateTime.UtcNow));
        reconnected.SnapshotActiveNanosForLogout();
        flush.HardFlush(reconnected);
        CollectionAssert.AreEqual(new[] { CocoonMarker }, Restore(store).Buffs.Select(buff => buff.Id).ToArray());
    }

    [TestMethod]
    public void MalformedNanoRowsDoNotPreventProgressionSaveOrRestore()
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.MaxNCU, 200);
        Assert.IsTrue(Land(player, Ward, DateTime.UtcNow));
        var store = new ProgressionStore();
        using var flush = CreateFlush(store);
        flush.HardFlush(player);

        Assert.IsTrue(Land(player, ShrugAbsorb, DateTime.UtcNow));
        List<ActiveNanoRecord> snapshot = player.TakeDirtyActiveNanos()!;
        snapshot.RemoveAll(nano => nano.NanoId == Ward);
        var overflow = new ActiveNanoRecord
        {
            NanoId = Ward, NanoInstance = 30, Strain = 219,
            DurationCentiseconds = 3000, ExpiresAtUtcTicks = long.MaxValue
        };
        snapshot.Add(overflow);
        player.RestoreDirtyActiveNanos(snapshot);
        player.TrainedPerks.Add(100);
        player.Research.Set(101, 55);

        flush.HardFlush(player);
        CollectionAssert.AreEqual(new[] { Ward }, store.Nanos.Select(nano => nano.NanoId).ToArray(),
            "A malformed replacement must preserve the previously saved Ward.");
        CollectionAssert.AreEqual(new[] { 100 }, store.Perks);
        CollectionAssert.AreEqual(new[] { new ResearchProgressRecord(101, 55) }, store.Research);

        player.SnapshotActiveNanosForTransfer();
        flush.HardFlush(player);
        store.Nanos = [overflow, ..store.Nanos];
        Player reconnected = Restore(store);
        CollectionAssert.AreEquivalent(new[] { Ward, ShrugAbsorb }, reconnected.Buffs.Select(buff => buff.Id).ToArray());
        Assert.IsTrue(reconnected.TrainedPerks.Contains(100));
        Assert.IsTrue(reconnected.Research.TryGetRemaining(101, out int remaining));
        Assert.AreEqual(55, remaining);
    }

    static InventoryFlushService CreateFlush(ProgressionStore store)
        => new(new Lazy<PlayfieldManager>(() => throw new InvalidOperationException()), store, new StubLogger(),
            trainedPerks: store, research: store);

    static Player Restore(ProgressionStore store)
    {
        var player = new Player(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 },
            new StubLogger(), Items.Value);
        var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: GameDataPaths.ResolveRuntimeRoot())
        {
            AllowMissingXpLevels = true
        };
        new PlayerHydrator(Items.Value, data).Apply(player, new CharacterHydrationResult
        {
            Character = new CharacterRecord { Id = 1, Name = "AbsorbTest", Playfield = 4582, HeadingW = 1, GmLevel = 1 },
            Stats = CharacterHydrationValidator.RequiredSpawnStats
                .Select(stat => new StatRecord
                {
                    StatId = (int)stat,
                    StatValue = stat switch
                    {
                        CharacterStat.Profession => (int)Profession.Enforcer,
                        CharacterStat.Expansion => 0x02,
                        CharacterStat.Level => 205,
                        _ => 100
                    }
                })
                .Append(new StatRecord { StatId = (int)CharacterStat.MaxNCU, StatValue = 200 }).ToArray(),
            ActiveNanos = store.Nanos,
            TrainedPerkIds = store.Perks,
            Research = store.Research
        });
        return player;
    }

    sealed class ProgressionStore : ICharacterCoalesceCommit, ITrainedPerkRepository, IResearchRepository
    {
        public bool FailNextWrite { get; set; }
        public ActiveNanoRecord[] Nanos { get; set; } = [];
        public int[] Perks { get; private set; } = [];
        public ResearchProgressRecord[] Research { get; private set; } = [];

        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates,
            int characterId, IReadOnlyList<int> uploadedNanoIds, IReadOnlyList<ActiveNanoRecord>? activeNanos,
            IReadOnlyList<SkillLockRecord>? skillLocks)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new IOException("Injected coalesced write failure");
            }
            if (activeNanos != null)
                Nanos = activeNanos.ToArray();
        }

        IReadOnlyList<int> ITrainedPerkRepository.GetForCharacter(int characterId) => Perks;
        IReadOnlyList<ResearchProgressRecord> IResearchRepository.GetForCharacter(int characterId) => Research;
        public void Save(int characterId, IReadOnlyList<int> perkIds) => Perks = perkIds.ToArray();
        public void Save(int characterId, IReadOnlyList<ResearchProgressRecord> research) => Research = research.ToArray();
    }

    static bool Land(Player player, int nanoId, DateTime now)
        => NanoRuntime.TryApplyImmediate(player, player, nanoId, Items.Value, new StubInventoryRepository(), now);

    static Buff RequireBuff(Player player, int id)
    {
        Assert.IsTrue(player.TryGetBuff(id, out Buff? buff), $"Expected nano {id} to be active.");
        return buff!;
    }

    static int Pool(Player player) => player.Stats.GetOrZero(CharacterStat.AbsorbMeleeAC);
}
