namespace ZoneEngine_New.Core.Missions;

using System;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

// Generated corpses have the exact dead NPC instance, no invented item pool, and
// a durable owner-only currency claim. They never enter generic loot persistence.
internal sealed class GeneratedMissionCorpseDynel : Dynel
{
    readonly GeneratedMissionNpcEvidence _evidence;
    readonly GeneratedMissionObject _state;
    readonly int _livePlayfield;
    Player? _opener;
    Func<Player, bool>? _claim;
    Action<Identity>? _acknowledge;
    Action? _deny;
    long _claimAt, _ackAt;
    bool _claimed;
    int _inventoryHandle;
    MissionCorpseInteractionLease? _lease;

    internal GeneratedMissionCorpseDynel(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject state, int livePlayfield)
        : base(new() { Type = IdentityType.Corpse, Instance = state.RuntimeInstance })
    { _evidence = evidence; _state = state; _livePlayfield = livePlayfield; }

    internal int OwnerId => _state.OwnerId;
    internal int QuestType => _state.QuestType;
    internal int QuestInstance => _state.QuestInstance;
    public override byte[] BuildSpawnPacket(Identity receiver) => GeneratedMissionCorpseProjection.BuildPacket(_evidence, _state, _livePlayfield, receiver);

    internal bool Open(Player player, Func<Player, bool> claim, Action<Identity> acknowledge, Action deny)
    {
        if (Playfield == null || player.Session == null || player.IsPersistenceQuarantined || player.IsDead || player.Identity.Instance != OwnerId || player.Playfield != Playfield
            || Distance3D(player) > 8.0 || DateTime.UtcNow.Ticks >= _state.CorpseExpiresAtUtcTicks) return false;
        if (_opener != null) return _opener == player;
        if (_inventoryHandle == 0) _inventoryHandle = Playfield!.AllocateContainerInventoryHandle();
        _opener = player; _claim = claim; _acknowledge = acknowledge; _deny = deny;
        _lease = new(player.Session, Playfield, OwnerId, _state.CorpseExpiresAtUtcTicks);
        long now = DateTime.UtcNow.Ticks;
        _claimAt = now + TimeSpan.TicksPerMillisecond * 500;
        _ackAt = now + TimeSpan.TicksPerMillisecond * 550;
        player.Session?.Send(new InventoryUpdateMessage
        {
            Identity = player.Identity, Unknown = 1, NumberOfSlots = 21, Unknown1 = 2, Entries = [],
            BagIdentity = Identity, SlotnumberInMainInventory = _inventoryHandle, Unknown2 = 1
        });
        return true;
    }

    public override void Tick(double deltaTime)
    {
        long now = DateTime.UtcNow.Ticks;
        if (_opener != null && !_claimed && now >= _claimAt)
        {
            bool authorized = Authorized(now);
            if (!authorized || _claim?.Invoke(_opener) != true)
            {
                if (authorized) _deny?.Invoke();
                _opener = null; _claim = null; _acknowledge = null; _deny = null; _lease = null;
            }
            else { _claimed = true; _state.CorpseClaimed = true; _state.LootResolved = true; }
        }
        if (_claimed && now >= _ackAt)
        {
            try { if (Authorized(now)) _acknowledge?.Invoke(Identity); }
            catch { _opener?.QuarantinePersistence(); _opener?.Session?.Close(); }
            finally { _acknowledge = null; Remove(); }
        }
        else if (now >= _state.CorpseExpiresAtUtcTicks) Remove();
    }

    bool Authorized(long now) => _opener != null && _lease?.Matches(_opener.Session, _opener.Playfield,
        _opener.Identity.Instance, _opener.IsDead, _opener.IsPersistenceQuarantined, Distance3D(_opener), now) == true;

    internal void Remove()
    {
        if (Playfield == null) return;
        Playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(this);
        Playfield.GetRequiredService<DynelRegistry>().Unregister(Identity);
        Playfield = null;
    }
}
