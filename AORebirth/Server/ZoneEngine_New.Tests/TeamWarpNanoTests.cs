namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

[TestClass]
public sealed class TeamWarpNanoTests
{
    [TestMethod]
    [DataRow(154914, 881, 2000)]
    [DataRow(154913, 2382, 3000)]
    public void Real_catalog_warp_uses_declared_timing_one_caster_commit_and_no_ncu_row(int id, int cost, int attack)
    {
        using var f = new Fixture(); var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
        NanoDefinition nano = f.Nano(id); Assert.AreEqual(cost, nano.NanoCost); Assert.AreEqual(attack, nano.AttackCentiseconds);
        Assert.AreEqual(500, nano.RechargeCentiseconds); Assert.IsTrue(nano.NcuCost > 0); Assert.AreEqual(0, nano.DurationCentiseconds);
        f.World.Owner(a, () => Assert.IsTrue(f.Service.TryCast(caster, id, member.Identity)));
        Assert.AreEqual(5000, caster.Stats.GetOrZero(CharacterStat.CurrentNano)); Assert.AreEqual(0, f.Store.Commits);
        f.Advance(caster, attack * 10 - 1); Assert.AreEqual(0, f.Store.Commits);
        f.Advance(caster, 1);
        Assert.AreEqual(1, f.Store.Commits); Assert.AreEqual(5000 - cost, caster.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.AreEqual(1, f.Store.Last.Count); Assert.AreEqual(caster.Identity.Instance, f.Store.Last.Single().CharacterId);
        Assert.AreEqual(0, f.Service.GetActive(caster).Count); Assert.AreEqual(0, f.Service.GetActive(member).Count);
        Assert.AreEqual(0, caster.Stats.GetOrZero(CharacterStat.CurrentNCU));
        Assert.AreEqual(caster.Position.x, member.Position.x); Assert.AreEqual(caster.Position.z + 2, member.Position.z);
        Assert.IsFalse(f.Bodies(caster).OfType<CharacterActionMessage>().Any(m => m.Action == CharacterActionType.SetNanoDuration));
        Assert.IsFalse(f.Service.TryCast(caster, id, member.Identity));
    }

    [TestMethod]
    public void Same_playfield_observers_keep_despawn_setpos_spawn_appearance_order_and_self_full_refresh()
    {
        using var f = new Fixture(); var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
        var unrelated = f.Player(a, 3);
        // Still in the same authoritative PF registry, but not an active visibility recipient.
        a.GetRequiredService<PlayfieldLocality>().UnregisterDynel(unrelated); f.Clear(caster, member, unrelated);
        f.Cast(caster, 154914, member.Identity);
        byte[][] observedPackets = f.World.Packets((ZoneSession)caster.Session!);
        var codec = new ZoneMessageCodec();
        MessageBody[] observed = observedPackets.Select(bytes => codec.Deserialize(bytes)!.Body).ToArray(), self = f.Bodies(member);
        // Despawn is the outbound alias. The existing ToClientQuit inbound serializer reads
        // only its opcode, so assert the actual outbound N3 identity and flag from wire bytes.
        int despawn = Array.FindIndex(observedPackets, bytes => bytes.Length >= 20
            && BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) == (int)N3MessageType.Despawn);
        int setPos = Array.FindIndex(observed, m => m is SetPosMessage p && p.Identity == member.Identity);
        int spawn = Array.FindIndex(observed, m => m is SimpleCharFullUpdateMessage s && s.Identity == member.Identity);
        int appearance = Array.FindIndex(observed, m => m is AppearanceUpdateMessage u && u.Identity == member.Identity);
        Assert.IsTrue(despawn >= 0 && setPos > despawn && spawn > setPos && appearance > spawn,
            $"despawn={despawn};setPos={setPos};spawn={spawn};appearance={appearance};bodies={string.Join(',', observed.Select(m => m.GetType().Name))}");
        byte[] quit = observedPackets[despawn];
        Assert.AreEqual(29, quit.Length);
        Assert.AreEqual(29, (int)BinaryPrimitives.ReadInt16BigEndian(quit.AsSpan(6, 2)));
        Assert.AreEqual(a.Identity.Instance, BinaryPrimitives.ReadInt32BigEndian(quit.AsSpan(8, 4)));
        Assert.AreEqual(caster.Identity.Instance, BinaryPrimitives.ReadInt32BigEndian(quit.AsSpan(12, 4)));
        Assert.AreEqual((int)member.Identity.Type, BinaryPrimitives.ReadInt32BigEndian(quit.AsSpan(20, 4)));
        Assert.AreEqual(member.Identity.Instance, BinaryPrimitives.ReadInt32BigEndian(quit.AsSpan(24, 4)));
        Assert.AreEqual((byte)1, quit[28]);
        var position = (SetPosMessage)observed[setPos]; Assert.AreEqual(1, position.Unknown1);
        Assert.AreEqual(member.Position.xf, position.Coordinates.X); Assert.AreEqual(member.Position.zf, position.Coordinates.Z);
        int selfPosition = Array.FindIndex(self, m => m is SetPosMessage);
        int selfAppearance = Array.FindIndex(self, m => m is AppearanceUpdateMessage u && u.Identity == member.Identity);
        int selfFull = Array.FindLastIndex(self, m => m is SimpleCharFullUpdateMessage s && s.Identity == member.Identity);
        Assert.IsTrue(selfPosition >= 0 && selfAppearance > selfPosition && selfFull > selfAppearance);
        Assert.IsFalse(f.Bodies(unrelated).OfType<SetPosMessage>().Any(), "Existing visibility filter must not become a PF-wide broadcast.");
    }

    [TestMethod]
    public void Cross_rubi_ka_warp_ignores_catalog_one_meter_range_and_uses_existing_transfer_queue()
    {
        using var f = new Fixture(); var destination = f.World.World(500); var source = f.World.World(501);
        var caster = f.Player(destination, 1); var member = f.Player(source, 2); f.Join(caster, member);
        member.Position = new Vector3(5000, 20, 5000); caster.Rotation = new Quaternion(0, 1, 0, 0);
        var transport = (ZoneSession)member.Session!;
        f.Cast(caster, 154914, member.Identity);
        Assert.AreSame(source, member.Playfield); Assert.AreEqual(1, f.Store.Commits);
        f.World.Drain(source); Assert.IsNull(member.Playfield); Assert.AreEqual(SessionState.Loading, transport.State);
        f.World.Drain(destination);
        Assert.AreSame(destination, member.Playfield); Assert.IsNull(member.Session);
        Assert.AreEqual(caster.Position.z - 2, member.Position.z); Assert.AreEqual(caster.Rotation.y, member.Rotation.y);
        Assert.AreEqual(1, f.World.Packets(transport).Select(p => new ZoneMessageCodec().Deserialize(p)!.Body).OfType<N3TeleportMessage>().Count());
        Assert.AreEqual(1, f.Store.Commits);
    }

    [TestMethod]
    public void Selected_cast_uses_nonzero_wire_target_or_current_selection_at_completion()
    {
        foreach (bool explicitTarget in new[] { false, true })
        {
            using var f = new Fixture(); var a = f.World.World(500); var caster = f.Player(a, 1);
            var first = f.Player(a, 2); var second = f.Player(a, 3); f.Join(caster, first); f.Join(caster, second);
            caster.Target = first.Identity;
            f.World.Owner(a, () => Assert.IsTrue(f.Service.TryCast(caster, 154914, explicitTarget ? first.Identity : Identity.None)));
            caster.Target = second.Identity; f.Advance(caster, 20_000);
            Player moved = explicitTarget ? first : second, unchanged = explicitTarget ? second : first;
            Assert.AreEqual(2d, moved.Position.z); Assert.AreEqual(0d, unchanged.Position.z);
            Assert.AreEqual(1, f.Store.Commits);
        }
    }

    [TestMethod]
    public void Team_roster_slots_skip_missing_owners_but_count_resolved_restricted_members()
    {
        using var f = new Fixture(); var a = f.World.World(500); var restricted = f.World.World(127);
        var caster = f.Player(a, 1); var blocked = f.Player(restricted, 2); var missing = f.Player(a, 3); var last = f.Player(a, 4);
        f.Join(caster, blocked); f.Join(caster, missing); f.Join(caster, last);
        f.World.Manager.UnregisterPlayer(missing);
        f.Cast(caster, 154913, Identity.None);
        Vector3 expected = TeamWarpNanoSpecialization.ComputeLanding(caster.Position, caster.Rotation, 1);
        Assert.AreEqual(expected.x, last.Position.x); Assert.AreEqual(expected.z, last.Position.z);
        Assert.AreEqual(0d, blocked.Position.z); Assert.AreEqual(0d, missing.Position.z);
        f.World.Manager.RegisterPlayer(missing);
    }

    [TestMethod]
    public void Exact_restrictions_requirements_upload_and_catalog_graph_fail_without_cost()
    {
        foreach (int restricted in new[] { 127, 647, 1931, 4000, 4999 })
        {
            using var f = new Fixture(); var a = f.World.World(500); var b = f.World.World(restricted);
            var caster = f.Player(a, 1); var member = f.Player(b, 2); f.Join(caster, member);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
            Assert.IsFalse(f.Service.TryCast(member, 154914, caster.Identity));
            Assert.AreEqual(0, f.Store.Commits);
        }
        using (var f = new Fixture())
        {
            var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
            caster.Stats.Set((CharacterStat)130, 421); Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
            caster.Stats.Set((CharacterStat)130, 1000); caster.Stats.Set((CharacterStat)531, 1);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); caster.Stats.Set((CharacterStat)531, 0);
            f.Nano(154914).Template.SpellList[EventType.OnUse][0].Arguments.Add(1);
            Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); Assert.AreEqual(0, f.Store.Commits);
        }
    }

    [TestMethod]
    public void Invalid_caster_geometry_is_rejected_without_guessing_a_heading_or_publishing_nan()
    {
        using var f = new Fixture(); var a = f.World.World(500); var caster = f.Player(a, 1); var member = f.Player(a, 2); f.Join(caster, member);
        foreach (var heading in new[] { new Quaternion(0, 0, 0, 0), new Quaternion(double.NaN, 0, 0, 1), new Quaternion(0, double.PositiveInfinity, 0, 1) })
        { caster.Rotation = heading; Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity)); }
        caster.Rotation = new Quaternion(0, 0, 0, 1); caster.Position = new Vector3(double.NaN, 0, 0);
        Assert.IsFalse(f.Service.TryCast(caster, 154914, member.Identity));
        Assert.AreEqual(0, f.Store.Commits); Assert.AreEqual(5000, caster.Stats.GetOrZero(CharacterStat.CurrentNano));
        caster.Position = new Vector3(1, 0, 0);
    }

    [TestMethod]
    public void Commit_failure_has_no_warp_and_indeterminate_cost_quarantines_only_caster()
    {
        foreach (bool unknown in new[] { false, true })
        {
            using var f = new Fixture(); var a = f.World.World(500); var b = f.World.World(501);
            var caster = f.Player(a, 1); var member = f.Player(b, 2); f.Join(caster, member);
            f.Store.Fail = true; f.Store.Unknown = unknown; f.Cast(caster, 154914, member.Identity);
            f.World.Drain(b); f.World.Drain(a);
            Assert.AreSame(b, member.Playfield); Assert.AreEqual(0d, member.Position.z);
            Assert.AreEqual(5000, caster.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(unknown, caster.IsPersistenceQuarantined); Assert.IsFalse(member.IsPersistenceQuarantined);
            Assert.IsFalse(f.Bodies(member).OfType<SetPosMessage>().Any());
        }
    }

    [TestMethod]
    public void Pending_cast_cancellation_and_postcommit_recipient_fences_prevent_stale_warps()
    {
        foreach (string mode in new[] { "cast_disconnect", "caster_interrupt", "caster_replaced", "member_replaced", "leave_team", "recipient_zone", "arrival_cancel" })
        {
            using var f = new Fixture(); var a = f.World.World(500); var b = f.World.World(501); var c = f.World.World(502);
            var caster = f.Player(a, 1); var member = f.Player(b, 2); f.Join(caster, member);
            f.World.Owner(a, () => Assert.IsTrue(f.Service.TryCast(caster, 154914, member.Identity)));
            if (mode == "cast_disconnect") caster.Session!.Close();
            f.Advance(caster, 20_000);
            if (mode is "caster_interrupt" or "arrival_cancel") f.World.Owner(a, () => caster.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield));
            if (mode == "caster_replaced") { caster.Session!.UnbindPlayer(); caster.EnterOnline(f.World.Session()); }
            if (mode == "member_replaced") { member.Session!.UnbindPlayer(); member.EnterOnline(f.World.Session()); }
            if (mode == "leave_team") f.World.Manager.Teams.TryHandle(member, new CharacterActionMessage { Identity = member.Identity, Action = CharacterActionType.LeaveTeam });
            if (mode == "recipient_zone") member.Playfield = c;
            f.World.Drain(b); f.World.Drain(c); f.World.Drain(a); f.World.Drain(b);
            Assert.AreEqual(0d, member.Position.z, mode);
            Assert.AreEqual(mode == "cast_disconnect" ? 0 : 1, f.Store.Commits, mode);
            if (mode == "recipient_zone") member.Playfield = b;
        }
    }

    [TestMethod]
    public void Accepted_transfer_rechecks_caster_authority_after_member_departure()
    {
        using var f = new Fixture(); var a = f.World.World(500); var b = f.World.World(501);
        var caster = f.Player(a, 1); var member = f.Player(b, 2); f.Join(caster, member);
        var session = member.Session;
        f.Cast(caster, 154914, member.Identity); f.World.Drain(b); Assert.IsNull(member.Playfield);
        f.World.Owner(a, () => caster.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield));
        f.World.Drain(a); f.World.Drain(b);
        Assert.AreSame(b, member.Playfield); Assert.AreSame(session, member.Session); Assert.AreEqual(0d, member.Position.z);
        Assert.AreEqual(1, f.Store.Commits);
    }

    sealed class Fixture : IDisposable
    {
        internal readonly PlayfieldTransferTests.Fixture World = new();
        internal readonly Store Store = new();
        internal readonly NanoCatalog Catalog = NanoCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat"));
        internal readonly NanoService Service;
        long _time;
        internal Fixture()
        {
            Service = new(Catalog, Store, [new TeamWarpNanoSpecialization(World.Manager.Teams, new Lazy<PlayfieldManager>(() => World.Manager))],
                utcNow: () => new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(_time), monotonicMilliseconds: () => _time);
            typeof(PlayfieldManager).GetField("<Nanos>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(World.Manager, Service);
        }
        internal NanoDefinition Nano(int id) { Assert.IsTrue(Catalog.TryGet(id, out var definition)); return definition; }
        internal Player Player(Playfield world, int id)
        {
            var p = World.Player(world, id); p.Name = "Member" + id; p.Rotation = new Quaternion(0, 0, 0, 1);
            foreach (int stat in new[] { 130, 131, 127 }) p.Stats.Set((CharacterStat)stat, 1000);
            p.Stats.Set((CharacterStat)531, 0); p.Stats.Set(CharacterStat.Profession, 3); p.Stats.Set(CharacterStat.Level, 60);
            p.Stats.Set(CharacterStat.CurrentNano, 5000); p.Stats.Set(CharacterStat.MaxNanoEnergy, 5000);
            p.Stats.Set(CharacterStat.MaxNCU, 0); p.Stats.Set(CharacterStat.AggDef, 25); p.Stats.Set(CharacterStat.NanoCInit, 0);
            p.TryAddUploadedNano(154914); p.TryAddUploadedNano(154913);
            World.Owner(world, () =>
            {
                Assert.IsTrue(Service.AttachPlayer(p)); p.NanoRuntime = Service; World.Manager.Teams.AttachPlayer(p);
                world.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(p);
            });
            return p;
        }
        internal void Join(Player a, Player b)
        {
            World.Owner(a.Playfield!, () => World.Manager.Teams.TryHandle(a, new CharacterActionMessage { Identity = a.Identity, Target = b.Identity, Action = CharacterActionType.TeamRequestInvite }));
            World.Owner(b.Playfield!, () => World.Manager.Teams.TryHandle(b, new CharacterActionMessage { Identity = b.Identity, Target = a.Identity, Action = CharacterActionType.TeamRequestReply, Parameter2 = 1 }));
            World.Drain(a.Playfield!); if (!ReferenceEquals(a.Playfield, b.Playfield)) World.Drain(b.Playfield!);
            Assert.IsTrue(World.Manager.Teams.AreTeammates(a, b)); Clear(a, b);
        }
        internal void Advance(Player caster, int milliseconds) { _time += milliseconds; World.Owner(caster.Playfield!, () => Service.Tick(caster)); }
        internal void Cast(Player caster, int id, Identity target)
        { World.Owner(caster.Playfield!, () => Assert.IsTrue(Service.TryCast(caster, id, target))); Advance(caster, Nano(id).AttackCentiseconds * 10); }
        internal MessageBody[] Bodies(Player p) => p.Session is ZoneSession s ? World.Packets(s).Select(bytes => new ZoneMessageCodec().Deserialize(bytes)!.Body).ToArray() : [];
        internal void Clear(params Player[] players) { foreach (var p in players) Bodies(p); }
        public void Dispose() => World.Dispose();
    }
    sealed class Store : IActiveNanoRepository
    {
        internal int Commits; internal bool Fail, Unknown; internal IReadOnlyList<NanoCharacterWrite> Last = [];
        public IReadOnlyList<ActiveNanoRecord> Load(int id) => [];
        public void Commit(IReadOnlyList<NanoCharacterWrite> writes)
        {
            if (Unknown) throw new DatabaseCommitOutcomeUnknownException(new InvalidOperationException("fixture unknown"));
            if (Fail) throw new InvalidOperationException("fixture rollback");
            Commits++; Last = writes;
        }
    }
}
