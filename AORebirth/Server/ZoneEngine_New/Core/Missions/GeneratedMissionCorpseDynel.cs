namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

// Generated corpses have the exact dead NPC instance, no invented item pool, and
// a durable owner-only currency claim. They never enter generic loot persistence.
internal sealed class GeneratedMissionCorpseDynel : Dynel
{
    readonly GeneratedMissionNpcEvidence _evidence;
    readonly GeneratedMissionObject _state;
    readonly int _livePlayfield;
    readonly Func<long> _now;
    readonly Queue<(long DueAt, Action<Identity> Send)> _acknowledgements = new();
    Player? _opener;
    Func<Player, bool>? _claim;
    Action? _deny;
    long _claimAt, _ackAt;
    bool _claimed, _opened;
    int _inventoryHandle;
    MissionCorpseInteractionLease? _lease;

    internal GeneratedMissionCorpseDynel(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject state, int livePlayfield)
        : this(evidence, state, livePlayfield, () => DateTime.UtcNow.Ticks) { }

    internal GeneratedMissionCorpseDynel(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject state, int livePlayfield, Func<long> now)
        : base(new() { Type = IdentityType.Corpse, Instance = state.RuntimeInstance })
    { _evidence = evidence; _state = state; _livePlayfield = livePlayfield; _now = now; }

    internal int OwnerId => _state.OwnerId;
    internal int QuestType => _state.QuestType;
    internal int QuestInstance => _state.QuestInstance;
    public override byte[] BuildSpawnPacket(Identity receiver) => GeneratedMissionCorpseProjection.BuildPacket(_evidence, _state, _livePlayfield, receiver);

    internal bool Open(Player player, Func<Player, bool> claim, Action<Identity> acknowledge, Action deny)
    {
        long now = _now();
        if (Playfield == null || player.Session?.State != SessionState.InPlay || player.IsPersistenceQuarantined || player.IsDead || player.Identity.Instance != OwnerId || player.Playfield != Playfield
            || !double.IsFinite(Distance3D(player)) || Distance3D(player) > 8.0 || now >= _state.CorpseExpiresAtUtcTicks) return false;
        if (_opener != null)
        {
            if (_opener != player || !Authorized(now)) return false;
            _opened = !_opened;
            if (!_opened)
            {
                // Exact accepted Legacy close: rotate the handle, Action 0x66,
                // then UseActionFinished; this request's generic ack remains delayed.
                _inventoryHandle = Playfield.AllocateContainerInventoryHandle();
                player.Session.Send(new ActionMessage
                { Identity = Identity, Unknown = 1, ActionCode = 1, ActionIdentity = 0x66, Target = player.Identity });
                player.Session.Send(new CharacterActionMessage
                { Identity = player.Identity, Unknown = 0, Action = CharacterActionType.UseActionFinished, Target = Identity.None });
            }
            else SendInventory(player);
            _acknowledgements.Enqueue((now + TimeSpan.TicksPerMillisecond * 550, acknowledge));
            return true;
        }
        if (_inventoryHandle == 0) _inventoryHandle = Playfield!.AllocateContainerInventoryHandle();
        _opener = player; _claim = claim; _deny = deny; _opened = true;
        _lease = new(player.Session, Playfield, OwnerId, _state.CorpseExpiresAtUtcTicks);
        _claimAt = now + TimeSpan.TicksPerMillisecond * 500;
        _ackAt = now + TimeSpan.TicksPerMillisecond * 550;
        _acknowledgements.Enqueue((_ackAt, acknowledge));
        SendInventory(player);
        return true;
    }

    void SendInventory(Player player)
    {
        player.Session?.Send(new InventoryUpdateMessage
        {
            Identity = player.Identity, Unknown = 1, NumberOfSlots = 21, Unknown1 = 2, Entries = [],
            BagIdentity = Identity, SlotnumberInMainInventory = _inventoryHandle, Unknown2 = 1
        });
    }

    public override void Tick(double deltaTime)
    {
        long now = _now();
        if (_opener != null && !_claimed && now >= _claimAt)
        {
            bool authorized = Authorized(now);
            if (!authorized || _claim?.Invoke(_opener) != true)
            {
                if (authorized) _deny?.Invoke();
                _opener = null; _claim = null; _deny = null; _lease = null; _opened = false;
                _acknowledgements.Clear();
            }
            else { _claimed = true; _state.CorpseClaimed = true; _state.LootResolved = true; }
        }
        FlushPendingAcknowledgements();
        if (_claimed && now >= _ackAt) Remove();
        else if (now >= _state.CorpseExpiresAtUtcTicks) Remove();
    }

    internal bool HasPendingAcknowledgements => _acknowledgements.Count != 0;

    // Also polled by the owning player's mission dispatcher after visual removal.
    // Closing/reopening does not extend the corpse's first-use removal deadline.
    internal void FlushPendingAcknowledgements()
    {
        long now = _now();
        if (!Authorized(now)) { _acknowledgements.Clear(); return; }
        if (!_claimed) return;
        while (_acknowledgements.TryPeek(out var pending) && now >= pending.DueAt)
        {
            _acknowledgements.Dequeue();
            try { if (Authorized(now)) pending.Send(Identity); }
            catch { _opener?.QuarantinePersistence(); _opener?.Session?.Close(); _acknowledgements.Clear(); return; }
        }
    }

    bool Authorized(long now) => _opener != null && _opener.Session?.State == SessionState.InPlay && _lease?.Matches(_opener.Session, _opener.Playfield,
        _opener.Identity.Instance, _opener.IsDead, _opener.IsPersistenceQuarantined, Distance3D(_opener), now) == true;

    internal void Remove()
    {
        if (Playfield == null) return;
        Playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(this);
        Playfield.GetRequiredService<DynelRegistry>().Unregister(Identity);
        Playfield = null;
    }
}
