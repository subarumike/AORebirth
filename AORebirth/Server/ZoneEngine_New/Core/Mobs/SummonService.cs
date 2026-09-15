namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.GameData;

/// <summary>
/// Content-defined summon, owned and expired by the source playfield tick. The nano
/// transaction is the sole cost authority; this service never writes player data.
/// </summary>
internal sealed class SummonService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, NpcContentActivationService accepted, IItemBuilder items,
    IItemTemplateCatalog catalog, IGameData data, Func<long>? milliseconds = null)
{
    sealed record Summon(Player Owner, IZoneSession Session, NpcCharacter Npc, long ExpiresAt);
    readonly Dictionary<int, Summon> _summons = new();
    readonly Func<long> _milliseconds = milliseconds ?? (() => Environment.TickCount64);
    bool _stopped;
    internal int Count => _summons.Count;
    internal NpcCharacter? ForOwner(Player owner) => _summons.TryGetValue(owner.Identity.Instance, out var summon)
        && ReferenceEquals(summon.Owner, owner) ? summon.Npc : null;

    internal bool TryPrepare(Player owner, WorldSummonDefinition requested, Func<bool> stillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        var rule = data.WorldContent.Summons.SingleOrDefault(x => x.NanoId == requested.NanoId);
        if (rule == null) return false;
        var definition = data.WorldContent.Npcs.SingleOrDefault(x => x.Key == rule.NpcDefinitionKey);
        if (_stopped || !Current(owner, owner.Session) || !Finite(owner, rule) || definition == null) return false;
        if (definition.Vendor != null && !WorldNpcFactory.TryCreateShop(definition.Vendor, catalog, out _, out _)) return false;
        var session = owner.Session!;
        // Deferred allocation/registration runs once, only after a known successful
        // cost commit. Failed casts neither replace the old summon nor burn its lifetime.
        int published = 0;
        afterCommit = () =>
        {
            if (Interlocked.Exchange(ref published, 1) != 0 || _stopped
                || !stillCurrent() || !Current(owner, session) || !Finite(owner, rule)) return;
            var npc = WorldNpcFactory.Create(definition, items, registry.AllocateNpcIdentity());
            npc.Position = PositionFor(owner, rule);
            npc.Rotation = new(owner.Rotation.xf, owner.Rotation.yf, owner.Rotation.zf, owner.Rotation.wf);
            npc.Playfield = playfield;
            if (definition.Vendor != null && !WorldNpcFactory.TryAttachShop(npc, definition.Vendor, catalog, out string failure, registry.AllocateVendingMachineIdentity()))
                throw new InvalidOperationException(failure);
            if (!registry.TryRegister(npc)) throw new InvalidOperationException("Summon identity allocation collided.");
            try
            {
                accepted.Bind(npc, new NpcContentBinding("summon:" + rule.NpcDefinitionKey + ":" + owner.Identity.Instance + ":" + npc.Identity.Instance,
                    definition.Provenance, definition.ContentNpcIdentity,
                    playfield.Identity.Instance, definition.HasDialogue, definition.Vendor != null));
                if (_summons.Remove(owner.Identity.Instance, out var old)) Remove(old);
                _summons.Add(owner.Identity.Instance, new(owner, session, npc, checked(_milliseconds() + (long)rule.LifetimeSeconds * 1000)));
                locality.RegisterDynel(npc); // Existing visibility sends SCFU then the exact companion VMFU.
            }
            catch
            {
                if (_summons.TryGetValue(owner.Identity.Instance, out var added) && ReferenceEquals(added.Npc, npc))
                    _summons.Remove(owner.Identity.Instance);
                accepted.Detached(npc); locality.UnregisterDynel(npc); registry.UnregisterExact(npc);
                npc.Playfield = null;
                throw;
            }
        };
        return true;
    }

    internal void Tick()
    {
        foreach (var entry in _summons.ToArray())
        {
            var summon = entry.Value;
            if (_milliseconds() < summon.ExpiresAt && Current(summon.Owner, summon.Session)
                && !summon.Npc.IsDead && ReferenceEquals(summon.Npc.Playfield, playfield)
                && registry.TryGet(summon.Npc.Identity, out var current) && ReferenceEquals(current, summon.Npc)) continue;
            _summons.Remove(entry.Key); Remove(summon);
        }
    }

    internal void Shutdown()
    {
        _stopped = true;
        foreach (var summon in _summons.Values) Remove(summon);
        _summons.Clear();
    }

    void Remove(Summon summon)
    {
        // Exact-reference cleanup must never withdraw a replacement's shop/visibility.
        accepted.Detached(summon.Npc);
        locality.UnregisterDynel(summon.Npc); registry.UnregisterExact(summon.Npc);
        if (ReferenceEquals(summon.Npc.Playfield, playfield)) summon.Npc.Playfield = null;
    }

    bool Current(Player owner, IZoneSession? session) => !playfield.IsDisposed && !owner.IsDead
        && !owner.IsPersistenceQuarantined && session is { State: SessionState.InPlay }
        && ReferenceEquals(owner.Session, session) && ReferenceEquals(session.Player, owner)
        && ReferenceEquals(owner.Playfield, playfield)
        && registry.TryGet(owner.Identity, out var current) && ReferenceEquals(current, owner);

    static AORebirth.Core.Vector.Vector3 PositionFor(Player owner, WorldSummonDefinition rule)
    {
        float y = owner.Rotation.yf, w = owner.Rotation.wf;
        float dx = rule.RelativeOffset[0], dy = rule.RelativeOffset[1], dz = rule.RelativeOffset[2];
        return new(owner.Position.x + dx * (1f - 2f*y*y) - dz * 2f*y*w, owner.Position.y + dy,
            owner.Position.z + dx * 2f*y*w + dz * (1f - 2f*y*y));
    }
    static bool Finite(Player owner, WorldSummonDefinition rule)
    {
        float x = owner.Rotation.xf, y = owner.Rotation.yf, z = owner.Rotation.zf, w = owner.Rotation.wf;
        double norm = (double)x * x + (double)y * y + (double)z * z + (double)w * w;
        // Check the actual float wire projection as well as the input doubles.
        // Do not normalize or invent a replacement heading for invalid input.
        var position = PositionFor(owner, rule);
        return double.IsFinite(norm) && norm > 0
            && float.IsFinite(position.xf) && float.IsFinite(position.yf) && float.IsFinite(position.zf);
    }

}
