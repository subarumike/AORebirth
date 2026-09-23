namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Playfield;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class CharacterCheckpointTests
{
    const int PlayfieldId = 4310;

    [TestMethod]
    public void Dirty_player_checkpoints_after_the_coalesce_window_once()
    {
        var state = new CharacterSaveState();
        long now = Environment.TickCount64;
        state.MarkDirty();

        Assert.IsFalse(state.TakeDue(now + 1000, characterId: 1));
        Assert.IsTrue(state.TakeDue(now + CharacterSnapshotService.CoalesceMilliseconds + 1000, characterId: 1));
        Assert.IsFalse(state.TakeDue(now + CharacterSnapshotService.CoalesceMilliseconds + 2000, characterId: 1));
    }

    [TestMethod]
    public void Clean_player_still_checkpoints_within_one_interval_of_login()
    {
        foreach (int characterId in new[] { 0, 29_999, 59_999, int.MaxValue })
        {
            var state = new CharacterSaveState();
            long now = Environment.TickCount64;

            Assert.IsFalse(state.TakeDue(now, characterId), characterId.ToString());
            Assert.IsTrue(state.TakeDue(now + CharacterSnapshotService.CheckpointIntervalMilliseconds, characterId), characterId.ToString());
        }
    }

    [TestMethod]
    public void Checkpoint_older_than_a_committed_snapshot_is_obsolete()
    {
        var state = new CharacterSaveState();
        long checkpoint = state.NextSequence();
        long logout = state.NextSequence();

        state.MarkCommitted(logout, [], null);

        Assert.IsFalse(state.IsNewerThanCommitted(checkpoint));
    }

    [TestMethod]
    public void Progress_stats_mark_dirty_but_combat_churn_waits_for_the_interval()
    {
        Player player = HydratedPlayer(503);
        long now = Environment.TickCount64;
        long afterCoalesce = now + CharacterSnapshotService.MaxDirtyDelayMilliseconds;

        player.Stats.Set(CharacterStat.Health, 5, StatDetail.Base, dirty: true);
        player.Stats.Set(CharacterStat.CurrentNano, 5, StatDetail.Base, dirty: true);
        player.Stats.AddBonus(CharacterStat.MartialArts, 10);
        Assert.IsFalse(player.SaveState.TakeDue(afterCoalesce, player.Identity.Instance), "health, nano and bonuses are checkpoint-only");

        player.Stats.Set(CharacterStat.XP, 1234, StatDetail.Base, dirty: true);
        Assert.IsTrue(player.SaveState.TakeDue(afterCoalesce, player.Identity.Instance), "XP is progress");
    }

    [TestMethod]
    public void Checkpoint_writes_changed_stats_and_moved_location_in_one_transaction()
    {
        var characters = new RecordingCharacterRepository();
        using var snapshots = new CharacterSnapshotService(characters, new UnusedStatRepository(), new StubLogger());
        Player player = HydratedPlayer(501);
        long due = Environment.TickCount64 + CharacterSnapshotService.MaxDirtyDelayMilliseconds;

        player.Stats.Set(CharacterStat.IP, 1478, StatDetail.Base);
        player.Stats.Set(CharacterStat.MartialArts, 6, StatDetail.Base);
        player.Position = new Vector3(120, 5, 80);
        snapshots.CheckpointIfDue(player, due);

        Checkpoint first = characters.WaitForWrite(1);
        CollectionAssert.AreEquivalent(
            new[] { ((int)CharacterStat.IP, 1478), ((int)CharacterStat.MartialArts, 6) },
            first.Stats.Select(s => (s.StatId, s.StatValue)).ToArray());
        Assert.IsNotNull(first.Location);
        Assert.AreEqual(PlayfieldId, first.Location.Playfield);
        Assert.AreEqual(120f, first.Location.X);

        player.Position = new Vector3(120.4f, 5, 80);
        player.Stats.Set(CharacterStat.Strength, 7, StatDetail.Base);
        snapshots.CheckpointIfDue(player, due + CharacterSnapshotService.MaxDirtyDelayMilliseconds);

        Checkpoint second = characters.WaitForWrite(2);
        CollectionAssert.AreEquivalent(
            new[] { ((int)CharacterStat.Strength, 7) },
            second.Stats.Select(s => (s.StatId, s.StatValue)).ToArray());
        Assert.IsNull(second.Location, "sub-meter drift is not worth a write");
    }

    [TestMethod]
    public void Dead_player_keeps_the_stored_location()
    {
        var characters = new RecordingCharacterRepository();
        using var snapshots = new CharacterSnapshotService(characters, new UnusedStatRepository(), new StubLogger());
        Player player = HydratedPlayer(504);
        player.Position = new Vector3(500, 0, 500);
        player.Stats.Set(CharacterStat.XP, 99, StatDetail.Base);
        typeof(Player).GetField("_respawnPending", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, true);

        snapshots.CheckpointIfDue(player, Environment.TickCount64 + CharacterSnapshotService.CheckpointIntervalMilliseconds);

        Checkpoint write = characters.WaitForWrite(1);
        Assert.IsNull(write.Location);
        Assert.IsTrue(write.Stats.Any(s => s.StatId == (int)CharacterStat.XP && s.StatValue == 99));
    }

    [TestMethod]
    public void Offline_row_skips_the_checkpoint_and_keeps_changes_for_the_next_one()
    {
        var characters = new RecordingCharacterRepository { Online = false };
        using var snapshots = new CharacterSnapshotService(characters, new UnusedStatRepository(), new StubLogger());
        Player player = HydratedPlayer(505);
        long due = Environment.TickCount64 + CharacterSnapshotService.CheckpointIntervalMilliseconds;

        player.Stats.Set(CharacterStat.XP, 77, StatDetail.Base);
        snapshots.CheckpointIfDue(player, due);
        characters.WaitForWrite(1);

        characters.Online = true;
        snapshots.CheckpointIfDue(player, due + CharacterSnapshotService.CheckpointIntervalMilliseconds);

        Checkpoint retry = characters.WaitForWrite(2);
        Assert.IsTrue(retry.Stats.Any(s => s.StatId == (int)CharacterStat.XP && s.StatValue == 77));
    }

    [TestMethod]
    public void Quarantined_player_never_checkpoints()
    {
        var characters = new RecordingCharacterRepository();
        using var snapshots = new CharacterSnapshotService(characters, new UnusedStatRepository(), new StubLogger());
        Player player = HydratedPlayer(502);
        player.Stats.Set(CharacterStat.IP, 10, StatDetail.Base);
        player.QuarantinePersistence();

        snapshots.CheckpointIfDue(player, Environment.TickCount64 + CharacterSnapshotService.CheckpointIntervalMilliseconds * 3);

        Assert.AreEqual(0, characters.Count);
    }

    static Player HydratedPlayer(int id)
    {
        Player player = TestWorld.CreatePlayer(id);
        player.Playfield = BarePlayfield(PlayfieldId);
        player.Position = new Vector3(100, 5, 100);
        player.Stats.Set(CharacterStat.IP, 1500, StatDetail.Base);
        player.Stats.Set(CharacterStat.MartialArts, 5, StatDetail.Base);
        player.Stats.Set(CharacterStat.Strength, 6, StatDetail.Base);
        player.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);
        player.Stats.Set(CharacterStat.MaxHealth, 100, StatDetail.Base);
        player.SaveState.SeedPersisted(
            new CharacterRecord { Id = id, Playfield = PlayfieldId, X = 100, Y = 5, Z = 100 },
            player.Stats.GetEntries().Select(e => new StatRecord { StatId = (int)e.Stat, StatValue = e.Base }).ToArray());
        return player;
    }

    static Playfield BarePlayfield(int id)
    {
        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(playfield, new Identity { Type = IdentityType.Playfield, Instance = id });
        return playfield;
    }

    sealed record Checkpoint(int CharacterId, CharacterRecord? Location, IReadOnlyList<StatRecord> Stats);

    sealed class RecordingCharacterRepository : ICharacterRepository
    {
        readonly object _gate = new();
        readonly List<Checkpoint> _writes = [];

        public volatile bool Online = true;

        public int Count { get { lock (_gate) return _writes.Count; } }

        public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats)
        {
            lock (_gate)
            {
                _writes.Add(new Checkpoint(characterId, location, stats.ToArray()));
                Monitor.PulseAll(_gate);
            }
            return Online;
        }

        public Checkpoint WaitForWrite(int count)
        {
            lock (_gate)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (_writes.Count < count)
                {
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                        Assert.Fail("Checkpoint write " + count + " did not arrive.");
                }

                return _writes[count - 1];
            }
        }

        public CharacterRecord? GetById(int characterId) => throw new InvalidOperationException("Unexpected character read.");
        public void SetOnline(int characterId) => throw new InvalidOperationException("Unexpected online write.");
        public void SetOffline(int characterId) => throw new InvalidOperationException("Unexpected offline write.");
        public void SaveLocation(CharacterRecord character, int online) => throw new InvalidOperationException("Checkpoints must not touch the online flag.");
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) => throw new InvalidOperationException("Checkpoints must not touch the online flag.");
    }

    sealed class UnusedStatRepository : IStatRepository
    {
        public IReadOnlyList<StatRecord> GetForCharacter(int characterId) => throw new InvalidOperationException("Unexpected stats read.");
        public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats) => throw new InvalidOperationException("Checkpoints must be one transaction with location.");
    }
}
