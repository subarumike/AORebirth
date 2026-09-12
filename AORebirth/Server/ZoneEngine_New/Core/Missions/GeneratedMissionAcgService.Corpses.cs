namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine.Core.Missions;

public sealed partial class GeneratedMissionAcgService
{
    readonly ConcurrentDictionary<int, (NpcCharacter Npc, long Deadline)> _deadNpcVisuals = new();
    readonly ConditionalWeakTable<Player, HashSet<GeneratedMissionCorpseDynel>> _corpseAcknowledgements = new();

    void PollCorpseAcknowledgements(Player player)
    {
        lock (player.PersistenceGate)
        {
            if (!_corpseAcknowledgements.TryGetValue(player, out var corpses)) return;
            foreach (var corpse in corpses.ToArray())
            {
                corpse.FlushPendingAcknowledgements();
                if (!corpse.HasPendingAcknowledgements) corpses.Remove(corpse);
            }
            if (corpses.Count == 0) _corpseAcknowledgements.Remove(player);
        }
    }
    public void SpawnDeathCorpse(NpcCharacter npc)
    {
        if (npc.Playfield is not MissionPlayfield world) throw new InvalidOperationException("Generated corpse requires its exact owned mission world.");
        var state = _dao.ReadObjects(world.World.OwnerId, world.World.QuestType, world.World.QuestInstance)
            .Single(value => value.RuntimeType == (int)npc.Identity.Type && value.RuntimeInstance == npc.Identity.Instance);
        if (!state.IsDead || state.CurrentHealth != 0) throw new InvalidOperationException("Mission corpse cannot precede durable death.");
        var corpse = world.World.CreateCorpse(state, world);
        var registry = world.GetRequiredService<DynelRegistry>();
        var locality = world.GetRequiredService<PlayfieldLocality>();
        _deadNpcVisuals[npc.Identity.Instance] = (npc, state.DiedAtUtcTicks
            + MissionAcgCorpseCreditPolicy.DeadNpcDespawnMilliseconds * TimeSpan.TicksPerMillisecond);
        if (corpse != null && !registry.TryGet(corpse.Identity, out _))
        { registry.Register(corpse); locality.RegisterDynel(corpse); }
    }

    public void PollNpcCorpseLifetime(NpcCharacter npc)
    {
        if (!_deadNpcVisuals.TryGetValue(npc.Identity.Instance, out var pending) || !ReferenceEquals(pending.Npc, npc)
            || DateTime.UtcNow.Ticks < pending.Deadline) return;
        _deadNpcVisuals.TryRemove(npc.Identity.Instance, out _);
        if (npc.Playfield is not MissionPlayfield world) return;
        world.GetRequiredService<PlayfieldLocality>().UnregisterDynel(npc);
        world.GetRequiredService<DynelRegistry>().Unregister(npc.Identity);
        npc.Playfield = null;
    }

    void RestorePendingCorpses(Player player, GeneratedMissionBinding binding)
    {
        if (player.Playfield is not MissionPlayfield world || !world.World.Matches(binding)) return;
        var registry = world.GetRequiredService<DynelRegistry>();
        foreach (var state in _dao.ReadObjects(binding.OwnerId, binding.QuestType, binding.QuestInstance).Where(value => value.IsDead))
        {
            var identity = new Identity { Type = IdentityType.Corpse, Instance = state.RuntimeInstance };
            if (registry.TryGet(identity, out _)) continue;
            var corpse = world.World.CreateCorpse(state, world);
            if (corpse == null) continue;
            registry.Register(corpse); world.GetRequiredService<PlayfieldLocality>().RegisterDynel(corpse);
        }
    }

    public bool TryHandleCorpseUse(Player player, Identity target, Action<Identity> acknowledge, Action deny)
    {
        if (player.Playfield is not MissionPlayfield world || target.Type is not (IdentityType.Corpse or IdentityType.CanbeAffected)) return false;
        var corpseId = new Identity { Type = IdentityType.Corpse, Instance = target.Instance };
        var registry = world.GetRequiredService<DynelRegistry>();
        if (!registry.TryGet(corpseId, out var found) || found is not GeneratedMissionCorpseDynel corpse)
        {
            if (target.Type != IdentityType.Corpse) return false;
            deny(); return true;
        }
        lock (player.PersistenceGate)
        {
            if (!corpse.Open(player, opener => ClaimCorpse(opener, corpse), acknowledge, deny)) deny();
            else _corpseAcknowledgements.GetValue(player, _ => new()).Add(corpse);
        }
        return true;
    }

    bool ClaimCorpse(Player player, GeneratedMissionCorpseDynel corpse)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.Playfield != corpse.Playfield || player.Identity.Instance != corpse.OwnerId) return false;
            try
            {
                var result = _dao.ClaimCorpseCredits(player.Identity.Instance, corpse.QuestType, corpse.QuestInstance,
                    corpse.Identity.Instance, Math.Max(0, player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base)), DateTime.UtcNow.Ticks);
                if (result.Status == GeneratedMissionResultStatus.Applied)
                {
                    try { player.Stats.Set(CharacterStat.Cash, result.Cash, StatDetail.Base, dirty: true); player.FlushDirtyStats(); }
                    catch (Exception exception) { Quarantine(player, exception); return false; }
                }
                return result.Status is GeneratedMissionResultStatus.Applied or GeneratedMissionResultStatus.AlreadyApplied;
            }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(player, exception); return false; }
            catch (Exception exception) { _logger.Error(exception, "Mission corpse claim rolled back before cash publication."); return false; }
        }
    }
}
