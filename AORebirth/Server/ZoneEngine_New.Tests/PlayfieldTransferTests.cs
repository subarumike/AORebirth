namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AORebirth.Database.Dao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Dialogue;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Teams;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;
using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

[TestClass]
public sealed class PlayfieldTransferTests
{
    [TestMethod]
    [DataRow(0.015625)]
    [DataRow(0.03125)]
    public void Snapshot_after_transfer_uses_simulated_position_not_requested_landing(double dt)
    {
        using var f = new Fixture(); var source = f.World(800); var destination = f.World(4582);
        var data = new ZoneEngine_New.Core.GameData.GameDataStore(new StubLogger());
        DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
        using var collision = PlayfieldWorldSimulation.Create(4582, data.GetPlayfieldGeometry(4582),
            data.GetPlayfieldMetaData(4582), DestinationsCatalog.Instance, data, new StubLogger());
        destination.WorldAccess.Instance = collision;
        var player = f.Player(source, 1);
        const double landingX = 923.0542;
        const double landingZ = 859.19006;
        Assert.IsTrue(collision.TryRaycastDown(new Vector3(landingX, 80, landingZ), 100, out var surface));
        // Start just above the surface so the new horizontal grounded sweep still
        // exercises a real correction, rather than requiring the old downward drift.
        var landing = new Vector3(landingX, surface.y + 0.015625, landingZ);
        player.Session!.TransferToPlayfield(destination, landing);
        f.Drain(source); f.Drain(destination);
        Assert.AreEqual(landing.y, player.Position.y, "Transfer publishes the requested coordinate before simulation.");
        f.Owner(destination, () => player.Motor.Tick(dt));
        double authoritativeY = player.Position.y;
        Assert.AreNotEqual(landing.y, authoritativeY, "This route must exercise a real post-arrival adjustment.");
        Assert.AreEqual((double)(float)surface.y,
            authoritativeY, "The grounded sweep snaps to the surface; DAO persistence must preserve that exact result.");
        var snapshot = new CharacterSnapshotService(f.Snapshots, f.Snapshots, new StubLogger());
        snapshot.Commit(player);
        var saved = f.Snapshots.Writes.Single();
        Assert.AreEqual((float)authoritativeY, saved.Y);
        Assert.AreEqual(authoritativeY, player.Position.y, "Snapshot/logout must not move the player.");
        Assert.AreEqual((float)landingX, saved.X); Assert.AreEqual((float)landingZ, saved.Z);
        destination.WorldAccess.Instance = null;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Transfer_heading_reaches_destination_and_teleport_without_rotating_source(bool explicitDoorHeading)
    {
        using var f = new Fixture(); var outside = f.World(800); var inside = f.World(1186);
        var player = f.Player(outside, 1); var session = (ZoneSession)player.Session!;
        player.Rotation = new Quaternion(0, 0.6, 0, 0.8);
        var expected = explicitDoorHeading ? new Quaternion(0, 1, 0, 0) : player.Rotation;
        IZoneSession contract = session;
        if (explicitDoorHeading)
            contract.TransferToPlayfield(inside, new Vector3(175.00107, 5.01, 113.01496), expected);
        else
            contract.TransferToPlayfield(inside, new Vector3(175.00107, 5.01, 113.01496));
        Assert.AreEqual(0.6f, player.Rotation.yf, "Scheduling must not rotate the character on the source.");
        f.Drain(outside);
        Assert.AreEqual(0.6f, player.Rotation.yf);
        f.Drain(inside);
        Assert.AreEqual(expected.y, player.Rotation.y);
        Assert.AreEqual(expected.w, player.Rotation.w);
        var teleport = (N3TeleportMessage)new ZoneMessageCodec().Deserialize(f.Packets(session)[0])!.Body;
        Assert.AreEqual(expected.yf, teleport.Heading.Y);
        Assert.AreEqual(expected.wf, teleport.Heading.W);
    }

    [TestMethod]
    public void Transfer_does_not_replay_held_movement_when_release_arrives_during_loading()
    {
        using var f = new Fixture(); var outside = f.World(800); var inside = f.World(1186);
        var player = f.Player(outside, 1); var session = (ZoneSession)player.Session!;
        player.Motor.ApplyAction(MovementAction.ForwardStart);
        Assert.AreEqual((byte)1, player.Motor.BuildMovementStatus().FwdDir);
        session.TransferToPlayfield(inside, new Vector3(175.00107, 5.01, 113.01496));
        f.Drain(outside);
        new CharDCMoveMessageHandler().Handle(new CharDCMoveMessage { MoveType = (byte)MovementAction.ForwardStop }, session);
        Assert.AreEqual(SessionState.Loading, session.State);
        Assert.IsTrue(player.Motor.IsMoving);
        f.Drain(inside);
        var movement = player.BuildSpawnMessage().MovementStatus;
        Assert.IsFalse(player.Motor.IsMoving);
        Assert.AreEqual((byte)1, movement.FwdState);
        Assert.AreEqual((byte)0, movement.FwdDir);
        Assert.AreEqual((byte)MovementState.Run, movement.ModeId);
        player.Motor.ApplyAction(MovementAction.ForwardStart);
        Assert.IsTrue(player.Motor.IsMoving);
    }

    [TestMethod]
    public void Transfer_clears_old_path_and_directional_input_but_preserves_selected_speed_mode()
    {
        using var f = new Fixture(); var outside = f.World(800); var inside = f.World(1186);
        var player = f.Player(outside, 1);
        player.Motor.ApplyAction(MovementAction.SwitchToWalk);
        player.Motor.SetPath(new[] { new Vector3(10, 0, 20) });
        player.Motor.ApplyAction(MovementAction.StrafeLeftStart);
        player.Motor.ApplyAction(MovementAction.TurnRightStart);
        player.Motor.ApplyAction(MovementAction.JumpStart);
        player.Session!.TransferToPlayfield(inside, new Vector3(175.00107, 5.01, 113.01496));
        f.Drain(outside); f.Drain(inside);
        Assert.IsFalse(player.Motor.HasPath);
        Assert.AreEqual(MovementFlags.None, player.Motor.MovementFlags);
        var movement = player.BuildSpawnMessage().MovementStatus;
        Assert.AreEqual((byte)1, movement.FwdState);
        Assert.AreEqual((byte)1, movement.StrafeState);
        Assert.AreEqual((byte)1, movement.TurnState);
        Assert.AreEqual((byte)1, movement.JumpState);
        Assert.AreEqual((byte)MovementState.Walk, movement.ModeId);
        Assert.AreEqual((byte)MovementState.Walk, movement.LastSpeedMode);
    }

    [TestMethod]
    public void Departure_flushes_on_source_tick_and_arrival_waits_for_destination_tick()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        p.TryAddUploadedNano(999); p.MarkUploadedNanoDirty(999);
        f.Persist.Before = () => { Assert.AreSame(a, p.Playfield); Assert.IsTrue(f.Held(a)); Assert.IsFalse(f.Held(b)); };
        s.TransferToPlayfield(b, new Vector3(8, 9, 10), new Quaternion(0, 1, 0, 0));
        Assert.AreSame(a, p.Playfield); Assert.AreEqual(0, f.Persist.Count);
        f.Drain(a);
        Assert.IsNull(p.Playfield); Assert.AreEqual(1, f.Persist.Count); Assert.AreEqual(SessionState.Loading, s.State);
        Assert.AreEqual(0, f.Packets(s).Length);
        f.Drain(b);
        Assert.AreSame(b, p.Playfield); Assert.IsNull(p.Session); Assert.IsNull(s.Player);
        Assert.AreEqual(8d, p.Position.x); Assert.AreEqual(1d, p.Rotation.y);
        var packets = f.Packets(s); Assert.AreEqual(2, packets.Length);
        var codec = new ZoneMessageCodec(); var teleport = codec.Deserialize(packets[0])!;
        Assert.IsInstanceOfType<N3TeleportMessage>(teleport.Body);
        Assert.AreEqual(501, teleport.Header.Sender); Assert.AreEqual(1, teleport.Header.Receiver);
        Assert.IsInstanceOfType<ZoneRedirectionMessage>(codec.Deserialize(packets[1])!.Body);
        var redirect = (ZoneRedirectionMessage)codec.Deserialize(packets[1])!.Body;
        Assert.IsTrue(f.ClaimRedirect(1, redirect).Accepted);
        Assert.IsFalse(f.ClaimRedirect(1, redirect).Accepted);
        Assert.AreEqual(0x61, ((N3TeleportMessage)teleport.Body).Unknown1);
        f.Drain(a); f.Drain(b);
        Assert.AreEqual(0, f.Packets(s).Length); Assert.AreEqual(1, f.Persist.Count);
    }

    [TestMethod]
    public void FairTrade_repeated_zone_returns_replay_all_63_vending_machines()
    {
        using var f = new Fixture();
        Playfield fairTrade = f.World(1186);
        Playfield outside = f.World(655);
        Player player = f.Player(fairTrade, 1);
        DynelRegistry registry = fairTrade.GetRequiredService<DynelRegistry>();
        PlayfieldLocality locality = fairTrade.GetRequiredService<PlayfieldLocality>();
        var shopInstances = new HashSet<int>();

        for (int index = 0; index < 63; index++)
        {
            int instance = unchecked((int)(0xC00004A2u | ((uint)index << 16)));
            var shop = new VendingMachine(
                new Identity
                {
                    Type = IdentityType.VendingMachine,
                    Instance = instance
                },
                new ItemTemplate { Id = 300000 + index, Name = "Fair Trade shop " + index });
            shop.Playfield = fairTrade;
            shop.SpawnSource = SpawnSource.StaticDynel;
            Assert.IsTrue(registry.TryRegister(shop));
            locality.RegisterDynel(shop);
            shopInstances.Add(instance);
        }

        ZoneSession current = (ZoneSession)player.Session!;
        locality.ActivatePlayerVisibility(player);
        Assert.AreEqual(63, CountPackets<VendingMachineFullUpdateMessage>(f.Packets(current)));

        for (int cycle = 0; cycle < 2; cycle++)
        {
            current.TransferToPlayfield(outside, new Vector3(8, 0, 0));
            f.Drain(fairTrade);
            byte[][] departurePackets = f.Packets(current);
            Assert.AreEqual(63, departurePackets.Length,
                "Transfer departure must send one cleanup packet per visible Fair Trade shop.");
            Assert.IsTrue(ExpectedVendingDespawns(shopInstances, 1186, player.Identity.Instance)
                    .SetEquals(departurePackets.Select(Convert.ToHexString)),
                "Every visible Fair Trade shop must be withdrawn before transfer cycle " + cycle + ".");

            f.Drain(outside);
            f.Packets(current);
            ZoneSession outsideSession = f.Reconnect(outside, player);
            f.Packets(outsideSession);

            outsideSession.TransferToPlayfield(fairTrade, new Vector3(175.00107, 5.01, 113.01496));
            f.Drain(outside);
            f.Drain(fairTrade);
            f.Packets(outsideSession);

            current = f.Reconnect(fairTrade, player);
            byte[][] returnPackets = f.Packets(current);
            Assert.AreEqual(63, CountPackets<VendingMachineFullUpdateMessage>(returnPackets),
                "All Fair Trade shops must be visible after return cycle " + cycle + ".");
            AssertVendingUpdatesPrecedeFullCharacter(returnPackets, cycle);
        }
    }

    static int CountPackets<TBody>(byte[][] packets)
    {
        var codec = new ZoneMessageCodec();
        return packets.Count(packet => codec.Deserialize(packet)?.Body is TBody);
    }

    static void AssertVendingUpdatesPrecedeFullCharacter(byte[][] packets, int cycle)
    {
        var codec = new ZoneMessageCodec();
        var bodies = packets.Select(packet => codec.Deserialize(packet)?.Body).ToArray();
        int fullCharacterIndex = Array.FindIndex(bodies, body => body is FullCharacterMessage);
        Assert.IsTrue(fullCharacterIndex >= 0, "Reconnect must include FullCharacter on cycle " + cycle + ".");
        Assert.AreEqual(63, bodies.Take(fullCharacterIndex).Count(body => body is VendingMachineFullUpdateMessage),
            "Every Fair Trade shop must be established before FullCharacter on cycle " + cycle + ".");
        Assert.AreEqual(0, bodies.Skip(fullCharacterIndex + 1).Count(body => body is VendingMachineFullUpdateMessage),
            "Complete visibility activation must not duplicate shops after FullCharacter on cycle " + cycle + ".");
    }

    static HashSet<string> ExpectedVendingDespawns(IEnumerable<int> instances, int sender, int receiver)
    {
        var codec = new ZoneMessageCodec();
        return instances.Select(instance => Convert.ToHexString(codec.Serialize(
                new DespawnMessage
                {
                    Identity = new Identity { Type = IdentityType.VendingMachine, Instance = instance },
                    Unknown = 1
                },
                sender,
                receiver)))
            .ToHashSet();
    }

    [TestMethod]
    public void Abandoned_redirect_expires_on_destination_tick_and_releases_ownership_once()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var lease = new Lease(); p.AttachOnlineOwnership(lease);
        p.Session!.TransferToPlayfield(b, new Vector3(8, 9, 10)); f.Drain(a); f.Drain(b);
        Assert.AreEqual(PlayerConnectionPhase.LinkDead, p.ConnectionPhase);
        Assert.IsNotNull(p.LinkDeadUntilUtc); Assert.IsTrue(p.LinkDeadUntilUtc > DateTime.UtcNow);
        Assert.IsTrue(f.Manager.FindPlayer(1, out var owner)); Assert.AreSame(p, owner);
        Assert.AreEqual(0, lease.Released); Assert.AreEqual(0, f.Snapshots.Writes.Count);
        p.LinkDeadUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        f.Owner(b, () => b.GetRequiredService<SpawnService>().Tick());
        f.Owner(b, () => b.GetRequiredService<SpawnService>().Tick());
        Assert.IsNull(p.Playfield); Assert.IsFalse(f.Manager.FindPlayer(1, out _));
        Assert.IsFalse(b.GetRequiredService<DynelRegistry>().TryGet(p.Identity, out _));
        Assert.AreEqual(1, lease.Released); Assert.AreEqual(1, f.Snapshots.Writes.Count);
        Assert.AreEqual(501, f.Snapshots.Writes[0].Playfield); Assert.AreEqual(8f, f.Snapshots.Writes[0].X);
    }

    [TestMethod]
    public void Redirect_reconnect_clears_deadline_and_old_transport_cannot_release_new_session()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var old = (ZoneSession)p.Session!; var lease = new Lease(); p.AttachOnlineOwnership(lease);
        old.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a); f.Drain(b);
        p.LinkDeadUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        var current = f.Session(); p.EnterOnline(current); old.Close();
        f.Owner(b, () => b.GetRequiredService<SpawnService>().Tick());
        Assert.AreEqual(PlayerConnectionPhase.Online, p.ConnectionPhase); Assert.IsNull(p.LinkDeadUntilUtc);
        Assert.AreSame(current, p.Session); Assert.AreSame(p, current.Player); Assert.AreSame(b, p.Playfield);
        Assert.IsTrue(f.Manager.FindPlayer(1, out var owner)); Assert.AreSame(p, owner);
        Assert.AreEqual(0, lease.Released); Assert.AreEqual(0, f.Snapshots.Writes.Count);
    }

    [TestMethod]
    public void Reciprocal_owner_tick_transfers_never_enter_the_other_tick_lock()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var q = f.Player(b, 2);
        using var ready = new Barrier(2);
        Task first = Task.Run(() => f.Owner(a, () => { Assert.IsTrue(ready.SignalAndWait(TimeSpan.FromSeconds(5))); p.Session!.TransferToPlayfield(b, new Vector3(2, 0, 0)); }));
        Task second = Task.Run(() => f.Owner(b, () => { Assert.IsTrue(ready.SignalAndWait(TimeSpan.FromSeconds(5))); q.Session!.TransferToPlayfield(a, new Vector3(3, 0, 0)); }));
        Assert.IsTrue(Task.WaitAll([first, second], TimeSpan.FromSeconds(5)), "Reciprocal departure must not wait on the foreign tick.");
        p.TryAddUploadedNano(999); q.TryAddUploadedNano(999); p.MarkUploadedNanoDirty(999); q.MarkUploadedNanoDirty(999);
        f.Persist.Before = () => Assert.IsTrue(ready.SignalAndWait(TimeSpan.FromSeconds(5)));
        Task departA = Task.Run(() => f.Drain(a)), departB = Task.Run(() => f.Drain(b));
        Assert.IsTrue(Task.WaitAll([departA, departB], TimeSpan.FromSeconds(5)), "Opposite flushed departures must not enter the foreign tick lock.");
        f.Persist.Before = null;
        Parallel.Invoke(() => f.Drain(a), () => f.Drain(b));
        Assert.AreSame(b, p.Playfield); Assert.AreSame(a, q.Playfield);
        Assert.IsTrue(f.Manager.FindPlayer(1, out var owner)); Assert.AreSame(p, owner);
    }

    [TestMethod]
    public void Background_writer_close_cannot_deadlock_a_departure_waiting_for_persistence()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        using var writerHeld = new ManualResetEventSlim(); using var closeNow = new ManualResetEventSlim();
        Task writer = Task.Run(() => { lock (p.PersistenceGate) { writerHeld.Set(); Assert.IsTrue(closeNow.Wait(TimeSpan.FromSeconds(5))); s.Close(); } });
        Assert.IsTrue(writerHeld.Wait(TimeSpan.FromSeconds(5)));
        s.TransferToPlayfield(b, new Vector3(8, 0, 0));
        Task departure = Task.Run(() => f.Owner(a, () => { closeNow.Set(); f.Drain(a); }));
        Assert.IsTrue(Task.WaitAll([writer, departure], TimeSpan.FromSeconds(5)), "Departure cannot hold Session while awaiting the writer's PersistenceGate.");
        f.Drain(b); Assert.AreSame(a, p.Playfield); Assert.IsNull(p.Session);
    }

    [TestMethod]
    public void Late_socket_close_after_disposed_playfield_does_not_resolve_its_child_provider()
    {
        using var f = new Fixture(); var a = f.World(500); var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        // An ordinary shutdown snapshot failure can leave the transport attached until its
        // receive loop closes; the playfield has already disposed its child services.
        f.Snapshots.Fail = true; a.Dispose(); Assert.IsTrue(a.IsDisposed);
        s.Close(); s.Close();
        Assert.AreEqual(SessionState.Closed, s.State); Assert.IsNull(s.Player); Assert.IsNull(p.Session);
        Assert.AreEqual(PlayerConnectionPhase.LinkDead, p.ConnectionPhase);
        f.Manager.UnregisterPlayer(p);
    }

    [TestMethod]
    public void Failed_flush_retains_source_actor_session_and_dirty_work_without_arrival()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!; p.TryAddUploadedNano(999); p.MarkUploadedNanoDirty(999);
        f.Persist.Fail = true;
        s.TransferToPlayfield(b, new Vector3(8, 0, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => f.Drain(a));
        Assert.AreSame(a, p.Playfield); Assert.AreSame(s, p.Session); Assert.AreEqual(SessionState.InPlay, s.State);
        Assert.IsTrue(p.HasDirtyUploadedNanos); f.Drain(b); Assert.AreSame(a, p.Playfield);
        f.Persist.Fail = false;
    }

    [TestMethod]
    public void Disposed_destination_returns_detached_actor_on_source_tick_only()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        s.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a);
        b.Dispose(); Assert.IsNull(p.Playfield);
        f.Drain(a); Assert.AreSame(a, p.Playfield); Assert.AreSame(s, p.Session);
        Assert.AreEqual(SessionState.InPlay, s.State); Assert.AreEqual(1d, p.Position.x);
        Assert.AreEqual(0, f.Snapshots.Writes.Count);
    }

    [TestMethod]
    public void Destination_collision_preserves_foreign_actor_and_returns_original()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        var collision = TestWorld.CreatePlayer(1); collision.Playfield = b;
        b.GetRequiredService<DynelRegistry>().Register(collision);
        s.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a); f.Drain(b); f.Drain(a);
        Assert.AreSame(a, p.Playfield);
        Assert.IsTrue(b.GetRequiredService<DynelRegistry>().TryGet(p.Identity, out var still)); Assert.AreSame(collision, still);
        Assert.AreEqual(0, f.Packets(s).Length);
        b.GetRequiredService<DynelRegistry>().UnregisterExact(collision);
    }

    [TestMethod]
    public void Transport_close_returns_same_actor_linkdead_without_binding_a_replacement_session()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var s = (ZoneSession)p.Session!;
        s.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a); s.Close();
        f.Drain(b); f.Drain(a);
        Assert.AreSame(a, p.Playfield); Assert.IsNull(p.Session); Assert.IsNull(s.Player);
        Assert.AreEqual(PlayerConnectionPhase.LinkDead, p.ConnectionPhase); Assert.IsNotNull(p.LinkDeadUntilUtc);
        var replacement = f.Session(); p.EnterOnline(replacement); f.Drain(b); f.Drain(a);
        Assert.AreSame(replacement, p.Session); Assert.AreSame(p, replacement.Player);
    }

    [TestMethod]
    public void Source_shutdown_recovers_pending_departure_then_snapshots_original_location_once()
    {
        using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
        var p = f.Player(a, 1); var lease = new Lease(); p.AttachOnlineOwnership(lease);
        p.Session!.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a);
        a.Dispose(); f.Drain(b);
        Assert.IsNull(p.Playfield); Assert.IsFalse(f.Manager.FindPlayer(1, out _));
        Assert.AreEqual(1, lease.Released); Assert.AreEqual(1, f.Snapshots.Writes.Count);
        Assert.AreEqual(500, f.Snapshots.Writes[0].Playfield); Assert.AreEqual(1f, f.Snapshots.Writes[0].X);
    }

    [TestMethod]
    public void Both_playfield_shutdown_orders_dispose_pending_owner_once()
    {
        foreach (bool sourceFirst in new[] { false, true })
        {
            using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
            var p = f.Player(a, 1); var lease = new Lease(); p.AttachOnlineOwnership(lease);
            p.Session!.TransferToPlayfield(b, new Vector3(8, 0, 0)); f.Drain(a);
            if (sourceFirst) { a.Dispose(); b.Dispose(); } else { b.Dispose(); a.Dispose(); }
            Assert.IsNull(p.Playfield); Assert.AreEqual(1, f.Snapshots.Writes.Count); Assert.AreEqual(1, lease.Released);
        }
    }

    [TestMethod]
    public void Late_departure_or_arrival_cannot_steal_replacement_character_authority()
    {
        foreach (bool departed in new[] { false, true })
        {
            using var f = new Fixture(); var a = f.World(500); var b = f.World(501);
            var old = f.Player(a, 1); var s = (ZoneSession)old.Session!;
            s.TransferToPlayfield(b, new Vector3(8, 0, 0)); if (departed) f.Drain(a);
            f.Manager.UnregisterPlayer(old);
            var current = TestWorld.CreatePlayer(1); f.Manager.RegisterPlayer(current);
            f.Drain(a); f.Drain(b); f.Drain(a);
            Assert.IsTrue(f.Manager.FindPlayer(1, out var owner)); Assert.AreSame(current, owner);
            Assert.IsNull(current.Playfield); Assert.AreEqual(0, f.Snapshots.Writes.Count);
            Assert.IsTrue(old.IsPersistenceQuarantined); Assert.IsNull(old.Playfield);
            Assert.IsFalse(a.GetRequiredService<DynelRegistry>().TryGet(old.Identity, out _));
        }
    }

    sealed class Lease : IDisposable { public int Released; public void Dispose() => Released++; }

    internal sealed class Fixture : IDisposable
    {
        internal readonly PlayfieldManager Manager = Blank<PlayfieldManager>();
        internal readonly PersistStore Persist = new();
        internal readonly SnapshotStore Snapshots = new();
        readonly InventoryFlushService _flush;
        readonly TradeService _trades = Blank<TradeService>();
        readonly List<Playfield> _worlds = [];
        readonly List<ZoneSession> _sessions = [];
        readonly string _handoffDirectory = Path.Combine(Path.GetTempPath(), "aorebirth-transfer-handoff-" + Guid.NewGuid().ToString("N"));
        readonly Dictionary<int, ZoneHandoffTicket> _tickets = [];
        readonly ZoneHandoffStore _handoffs;
        internal Fixture()
        {
            _handoffs = new ZoneHandoffStore(_handoffDirectory);
            Set(Manager, "_sync", new Lock()); Set(Manager, "_playersByCharacterId", new Dictionary<int, Player>());
            Set(Manager, "<Teams>k__BackingField", new TeamService(dispatchOnOwner: (player, action) =>
                player.Playfield?.DispatchPlayerProjection(player, action)));
            Set(Manager, "<Dialogues>k__BackingField", new DialogueService(null!, null!));
            _flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => Manager), Persist, new StubLogger());
            Set(_trades, "_gate", new object()); Set(_trades, "_byPlayer", new Dictionary<int, TradeSession>());
        }
        internal Playfield World(int id, IItemTemplateCatalog? itemCatalog = null, IItemBuilder? itemBuilder = null,
            Func<long>? milliseconds = null)
        {
            var world = Blank<Playfield>(); var registry = new DynelRegistry(); var logger = new StubLogger();
            var locality = new PlayfieldLocality(id, null); var spawn = Blank<SpawnService>();
            Set(world, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = id });
            Set(world, "_tickSync", new Lock()); Set(world, "_inbound", new PlayfieldInboundQueue());
            Set(world, "_outgoingTransfers", new ConcurrentDictionary<PlayfieldTransfer, byte>());
            Set(world, "_incomingTransfers", new ConcurrentDictionary<PlayfieldTransfer, byte>());
            Set(world, "_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            Set(world, "_dynelRegistry", registry); Set(world, "_logger", logger); Set(world, "_playfieldManager", Manager);
            itemCatalog ??= new StubCatalog(); itemBuilder ??= new StubItemBuilder();
            var accepted = new NpcContentActivationService(world, registry, locality, itemBuilder, itemCatalog);
            var contentData = new StubGameData(HashItemCatalog.Parse("{}")) { RootPath = Path.Combine(AppContext.BaseDirectory, "GameData") };
            var services = new ServiceCollection().AddSingleton(spawn).AddSingleton(registry).AddSingleton(locality)
                .AddSingleton<IGameData>(contentData)
                .AddSingleton(Manager.Teams).AddSingleton(new WorldSimulationAccess())
                .AddSingleton(new InventoryMoveService(logger, _flush, Blank<InventoryActionService>()))
                .AddSingleton(_trades)
                .AddSingleton(new QuestPropService(world, registry, locality, new StubCatalog(), null!))
                .AddSingleton(accepted)
                .BuildServiceProvider();
            Set(world, "_serviceProvider", services);
            Set(spawn, "_registry", registry); Set(spawn, "_logger", logger); Set(spawn, "_playfield", world);
            Set(spawn, "_playfieldManager", Manager); Set(spawn, "_trades", _trades); Set(spawn, "_flush", _flush);
            Set(spawn, "_snapshot", new CharacterSnapshotService(Snapshots, Snapshots, logger));
            _worlds.Add(world); return world;
        }
        internal Player Player(Playfield world, int id)
        {
            var player = TestWorld.CreatePlayer(id); player.Position = new Vector3(id, 0, 0); player.Playfield = world;
            player.Stats.Set(CharacterStat.Health, 100); player.Stats.Set(CharacterStat.MaxHealth, 100);
            ZoneSession session = Session();
            string account = "fixture-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ZoneHandoffTicket ticket = _handoffs.Issue(account, _handoffs.BeginLogin(account), id);
            Assert.IsTrue(_handoffs.Claim(id, ticket.Cookie1, ticket.Cookie2, _ => account).Accepted);
            session.BindZoneHandoff(id, ticket.Cookie1, ticket.Cookie2, _handoffs);
            _tickets[id] = ticket;
            player.EnterOnline(session); Manager.RegisterPlayer(player);
            world.GetRequiredService<DynelRegistry>().Register(player); world.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);
            return player;
        }
        internal ZoneSession Session()
        {
            var s = new ZoneSession(Guid.NewGuid(), new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                new ZoneMessageCodec(), Blank<ZoneMessageDispatcher>(), new StubLogger()) { State = SessionState.InPlay };
            _sessions.Add(s); return s;
        }
        internal ZoneSession Reconnect(Playfield world, Player player)
        {
            ZoneSession session = Session();
            int characterId = player.Identity.Instance;
            string account = "fixture-" + characterId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ZoneHandoffTicket ticket = _handoffs.Issue(account, _handoffs.BeginLogin(account), characterId);
            Assert.IsTrue(_handoffs.Claim(characterId, ticket.Cookie1, ticket.Cookie2, _ => account).Accepted);
            session.BindZoneHandoff(characterId, ticket.Cookie1, ticket.Cookie2, _handoffs);
            Owner(world, () =>
            {
                player.EnterOnline(session);
                session.State = SessionState.SpawnReady;
                PlayfieldLocality locality = world.GetRequiredService<PlayfieldLocality>();
                locality.PrimeVendingMachineVisibility(player);
                session.Send(player.BuildFullCharacterMessage());
                session.State = SessionState.InPlay;
                locality.ActivatePlayerVisibility(player);
            });
            return session;
        }
        internal bool Held(Playfield world) => ((Lock)Get(world, "_tickSync")).IsHeldByCurrentThread;
        internal void Owner(Playfield world, Action work) { lock ((Lock)Get(world, "_tickSync")) work(); }
        internal void Drain(Playfield world) => Owner(world, () => ((PlayfieldInboundQueue)Get(world, "_inbound"))
            .Drain(null!, world.GetRequiredService<SpawnService>(), world));
        internal byte[][] Packets(ZoneSession session)
        {
            var result = new List<byte[]>(); var queue = (Channel<byte[]>)Get(session, "_sendQueue");
            while (queue.Reader.TryRead(out var packet)) result.Add(packet);
            return result.ToArray();
        }
        internal ZoneHandoffClaim ClaimRedirect(int character, ZoneRedirectionMessage redirect)
        {
            ZoneHandoffTicket ticket = _tickets[character];
            string account = "fixture-" + character.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return _handoffs.Claim(character, ticket.Cookie1, ticket.Cookie2, _ => account,
                redirect.ServerIpAddress.ToString(), redirect.ServerPort);
        }
        public void Dispose()
        {
            foreach (var world in _worlds) world.Dispose();
            foreach (var session in _sessions) session.Close();
            _flush.Dispose();
            if (Directory.Exists(_handoffDirectory)) Directory.Delete(_handoffDirectory, true);
        }
        static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
    internal sealed class PersistStore : ICharacterCoalesceCommit
    {
        public int Count; public bool Fail; public Action? Before;
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> uploadedNanoIds, IReadOnlyList<ActiveNanoRecord>? activeNanos)
        { Before?.Invoke(); if (Fail) throw new InvalidOperationException("fixture flush failure"); Interlocked.Increment(ref Count); }
    }
    internal sealed class SnapshotStore : ICharacterRepository, IStatRepository
    {
        public readonly List<CharacterRecord> Writes = [];
        public bool Fail;
        public CharacterRecord? GetById(int id) => throw new NotSupportedException();
        public void SetOnline(int id) { }
        public void SetOffline(int id) { }
        public void SaveLocation(CharacterRecord character, int online) => throw new NotSupportedException();
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats)
        { if (Fail) throw new InvalidOperationException("fixture snapshot failure"); Writes.Add(character); }
        public IReadOnlyList<StatRecord> GetForCharacter(int id) => [];
        public void UpsertForCharacter(int id, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();
    }
}
