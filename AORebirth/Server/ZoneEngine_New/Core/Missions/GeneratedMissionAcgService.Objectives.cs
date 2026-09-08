namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Vector3 = AORebirth.Core.Vector.Vector3;

public sealed partial class GeneratedMissionAcgService
{
    public bool ClaimsExteriorMarker(Player player)
        => player.Playfield != null && player.Playfield is not MissionPlayfield
            && _dao.ReadAccepted(player.Identity.Instance).Any(binding => binding.CleanupCheckpoints != (1L << 17) - 1 && WithinExteriorMarker(player, binding));
    // Existing Legacy entry resolver resolves the unique owned exterior marker before any
    // target fallback: horizontal 10 / vertical 14; the client cannot supply another lease.
    public bool TryEnterExterior(Player player, Identity entranceTarget)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.IsDead || player.Playfield == null || player.Playfield is MissionPlayfield) return false;
            var candidates = _dao.ReadAccepted(player.Identity.Instance).Where(binding => binding.State == GeneratedMissionState.Active
                && binding.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks && WithinExteriorMarker(player, binding)).ToArray();
            if (candidates.Length != 1) return false;
            var b = candidates[0];
            return TryEnter(player, new() { Type = (IdentityType)b.Offer.EntranceType, Instance = b.Offer.EntranceInstance }, b.Offer.EntranceLow, b.Offer.EntranceHigh);
        }
    }

    static bool WithinExteriorMarker(Player player, GeneratedMissionBinding binding)
    {
        if (player.Playfield?.Identity.Instance != binding.Offer.DestinationPlayfield) return false;
        double dx = player.Position.x - binding.Offer.DestinationX, dz = player.Position.z - binding.Offer.DestinationZ;
        return dx * dx + dz * dz <= 100 && Math.Abs(player.Position.y - binding.Offer.DestinationY) <= 14;
    }

    Item CreateRepairComponent()
    {
        int[] available = new[] { 100292, 100299, 100344, 100348, 100349, 100361 }.Where(id => _templates.TryGet(id, out _)).ToArray();
        int low, high;
        if (available.Length != 0) low = high = available[Random.Shared.Next(available.Length)];
        else if (_templates.TryGet(87810, out _) && _templates.TryGet(87814, out _)) { low = 87810; high = 87814; }
        else if (_templates.TryGet(87810, out _)) low = high = 87810;
        else if (_templates.TryGet(95576, out _)) low = high = 95576;
        else throw new InvalidOperationException("No accepted repair component template exists in the item catalog.");
        int id = _ids.Allocate();
        return _items.Create(low, high, 1, ItemSource.Other, 1, id, new() { Type = (IdentityType)0xC73D, Instance = id });
    }

    bool TryPickup(Player player, GeneratedMissionBinding binding, GeneratedMissionObject state)
    {
        if (binding.Offer.MissionType is not (2 or 4) || binding.ObjectiveType != state.RuntimeType
            || binding.ObjectiveInstance != state.RuntimeInstance || state.ObjectiveConsumed || binding.MissionItem != null) return false;
        int id = _ids.Allocate();
        var item = _items.Create(binding.ObjectiveTemplateId, binding.ObjectiveTemplateId, binding.Offer.Quality, ItemSource.Other,
            1, id, new() { Type = (IdentityType)0xC76D, Instance = id });
        state.ObjectiveConsumed = true;
        var observation = Observation(binding, 3); observation.AdvanceProgress = binding.Offer.MissionType == 2; observation.Objects = [state];
        var result = _missions.ObserveArtifact(player, observation, [item], null);
        if (result.Status != GeneratedMissionResultStatus.Applied) return false;
        // Visibility follows the committed consumption; the same normalized state
        // suppresses this object on reconnect. Never remove it before the item grant commits.
        var registry = player.Playfield!.GetRequiredService<DynelRegistry>();
        if (registry.TryGet(new() { Type = (IdentityType)state.RuntimeType, Instance = state.RuntimeInstance }, out var consumed) && consumed != null)
        {
            player.Playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(consumed);
            registry.Unregister(consumed.Identity);
            consumed.Playfield = null;
        }
        if (observation.AdvanceProgress) CompleteVerified(player, result.Binding);
        return true;
    }

    public bool TryInfoRequest(Player player, Identity target)
    {
        lock (player.PersistenceGate)
        {
            if (!TryOwnedTarget(player, target, out var binding, out var state, out var dynel)
                || binding.Offer.MissionType != 1 || state.IsDead || dynel.Distance3D(player) > 8.0) return false;
            var result = _missions.Observe(player, Observation(binding, 2));
            if (result.Status is GeneratedMissionResultStatus.Applied or GeneratedMissionResultStatus.AlreadyApplied)
                CompleteVerified(player, result.Binding);
            return result.Status != GeneratedMissionResultStatus.Rejected;
        }
    }

    public bool TryUseItemOnTarget(Player player, Identity sourceSlot, Identity target)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.IsDead || player.Playfield == null || sourceSlot.Type != IdentityType.Inventory
                || !player.Inventory.Inventory.Content.TryGetValue(sourceSlot.Instance, out var item) || !item.IsPersisted || item.Locked) return false;
            var matches = _dao.ReadAccepted(player.Identity.Instance).Where(b => b.State == GeneratedMissionState.Active && b.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks
                && b.MissionItem?.InstanceId == item.InstanceId && b.Offer.MissionType is 3 or 4).ToArray();
            if (matches.Length != 1) return false;
            var binding = matches[0];
            var observation = Observation(binding, binding.ObjectiveInteraction);
            if (binding.Offer.MissionType == 3)
            {
                if (!TryOwnedTarget(player, target, out var exact, out var state, out var dynel)
                    || exact.QuestInstance != binding.QuestInstance || state.ObjectiveConsumed || dynel.Distance3D(player) > 8.0) return false;
                state.ObjectiveConsumed = true; observation.Objects = [state];
            }
            else
            {
                if ((int)target.Type != binding.Offer.IssuingTerminalType || target.Instance != binding.Offer.IssuingTerminalInstance
                    || player.Playfield.Identity.Instance != binding.Offer.IssuingTerminalPlayfield) return false;
                observation.TerminalType = (int)target.Type; observation.TerminalInstance = target.Instance;
                observation.ActualPlayfield = player.Playfield.Identity.Instance;
            }
            var result = _missions.ObserveArtifact(player, observation, [], sourceSlot.Instance);
            if (result.Status is GeneratedMissionResultStatus.Applied or GeneratedMissionResultStatus.AlreadyApplied)
                CompleteVerified(player, result.Binding);
            return result.Status != GeneratedMissionResultStatus.Rejected;
        }
    }

    // The NPC adapter calls this BEFORE health/death publication. A failed/unknown commit
    // must stop the damage pipeline; neither attack feedback nor a death may claim success.
    public bool TryPersistNpc(NpcCharacter npc, Player? attacker, int finalHealth)
    {
        if (npc.Playfield is not MissionPlayfield world || finalHealth < 0) return false;
        var owner = world.GetRequiredService<DynelRegistry>().Players().OfType<Player>().SingleOrDefault(player => player.Identity.Instance == world.World.OwnerId);
        if (owner == null) return false;
        lock (owner.PersistenceGate)
        {
            if (owner.IsPersistenceQuarantined) return false;
            try
            {
                var binding = _dao.ReadAccepted(owner.Identity.Instance, world.World.QuestType, world.World.QuestInstance);
                if (binding == null || (binding.State != GeneratedMissionState.Active && binding.State != GeneratedMissionState.Completed)
                    || binding.CleanupCheckpoints != 0 || binding.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks) return false;
                var state = _dao.ReadObjects(owner.Identity.Instance, binding.QuestType, binding.QuestInstance)
                    .Single(value => value.RuntimeType == (int)npc.Identity.Type && value.RuntimeInstance == npc.Identity.Instance);
                if (state.IsDead) return finalHealth == 0;
                if (finalHealth > state.MaxHealth) return false;
                state.X = npc.Position.xf; state.Y = npc.Position.yf; state.Z = npc.Position.zf;
                state.HeadingX = npc.Rotation.xf; state.HeadingY = npc.Rotation.yf; state.HeadingZ = npc.Rotation.zf; state.HeadingW = npc.Rotation.wf;
                state.CurrentHealth = finalHealth; state.IsDead = finalHealth == 0;
                if (state.IsDead)
                {
                    if (!MissionAcgCorpseCreditPolicy.TryResolve(state.RuntimeInstance, binding.LivePlayfield, out int credits)) return false;
                    state.DeathActorId = attacker?.Identity.Instance ?? 0;
                    state.DiedAtUtcTicks = DateTime.UtcNow.Ticks;
                    state.CorpseExpiresAtUtcTicks = state.DiedAtUtcTicks + TimeSpan.TicksPerMillisecond
                        * (MissionAcgCorpseCreditPolicy.SpawnDelayMilliseconds + MissionAcgCorpseCreditPolicy.LifetimeMilliseconds);
                    state.CorpseCredits = credits;
                }
                GeneratedMissionResult result;
                if (binding.State == GeneratedMissionState.Active && state.IsDead && binding.Offer.MissionType == 0 && binding.ObjectiveType == state.RuntimeType
                    && binding.ObjectiveInstance == state.RuntimeInstance && attacker?.Identity.Instance == binding.OwnerId)
                {
                    var observation = Observation(binding, 1); observation.Objects = [state];
                    result = _missions.Observe(owner, observation);
                }
                else result = _dao.UpdateObjects(owner.Identity.Instance, binding.QuestType, binding.QuestInstance, [state], DateTime.UtcNow.Ticks);
                return result.Status is GeneratedMissionResultStatus.Applied or GeneratedMissionResultStatus.AlreadyApplied;
            }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(owner, exception); return false; }
            catch (Exception exception) { _logger.Error(exception, "Mission NPC state failed before health publication."); return false; }
        }
    }

    public void OnNpcDeathPublished(NpcCharacter npc)
    {
        if (npc.Playfield is not MissionPlayfield world) return;
        var owner = world.GetRequiredService<DynelRegistry>().Players().OfType<Player>().SingleOrDefault(player => player.Identity.Instance == world.World.OwnerId);
        if (owner != null)
        {
            var binding = _dao.ReadAccepted(owner.Identity.Instance, world.World.QuestType, world.World.QuestInstance);
            if (binding != null && binding.State == GeneratedMissionState.Active && binding.CompletionFrozenAtUtcTicks == 0)
            {
                var states = _dao.ReadObjects(owner.Identity.Instance, binding.QuestType, binding.QuestInstance);
                if (states.Any(state => state.RuntimeInstance == npc.Identity.Instance && state.Kind == (int)MissionAcgRuntimeObjectKind.AmbientNpc
                    && state.IsDead && state.DeathActorId == owner.Identity.Instance))
                {
                    var ambient = states.Where(state => state.Kind == (int)MissionAcgRuntimeObjectKind.AmbientNpc).ToArray();
                    int percent = MissionAcgTokenRewardPolicy.CalculatePercent(ambient.Count(state => state.IsDead && state.DeathActorId == owner.Identity.Instance), ambient.Length);
                    owner.Session?.Send(GeneratedMissionTokenProjection.Build(owner.Identity, percent));
                }
            }
            if (binding != null && binding.Progress == binding.RequiredCount) CompleteVerified(owner, binding);
        }
    }

    bool TryOwnedTarget(Player player, Identity target, out GeneratedMissionBinding binding, out GeneratedMissionObject state, out Dynel dynel)
    {
        binding = null!; state = null!; dynel = null!;
        if (player.IsPersistenceQuarantined || player.IsDead || player.Playfield is not MissionPlayfield world || world.World.OwnerId != player.Identity.Instance
            || !world.GetRequiredService<DynelRegistry>().TryGet(target, out var found) || found == null || found.Playfield != player.Playfield) return false;
        var b = _dao.ReadAccepted(player.Identity.Instance, world.World.QuestType, world.World.QuestInstance);
        if (b == null || b.State != GeneratedMissionState.Active || b.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks
            || b.ObjectiveType != (int)target.Type || b.ObjectiveInstance != target.Instance) return false;
        var s = _dao.ReadObjects(player.Identity.Instance, b.QuestType, b.QuestInstance).SingleOrDefault(value => value.RuntimeType == (int)target.Type && value.RuntimeInstance == target.Instance);
        if (s == null) return false;
        binding = b; state = s; dynel = found; return true;
    }

    static GeneratedMissionObservation Observation(GeneratedMissionBinding binding, int interaction) => new()
    {
        OwnerId = binding.OwnerId, QuestType = binding.QuestType, QuestInstance = binding.QuestInstance, LivePlayfield = binding.LivePlayfield,
        ObjectiveType = binding.ObjectiveType, ObjectiveInstance = binding.ObjectiveInstance, ObjectiveTemplateId = binding.ObjectiveTemplateId,
        Interaction = interaction, ObservationIdentity = "objective:" + interaction + ":" + binding.ObjectiveType + ":" + binding.ObjectiveInstance
    };

    void CompleteVerified(Player player, GeneratedMissionBinding binding)
    {
        var result = _missions.Complete(player, new() { Type = (IdentityType)binding.QuestType, Instance = binding.QuestInstance });
        if (result.Status != GeneratedMissionResultStatus.Applied) return;
        try
        {
            var quest = new Identity { Type = (IdentityType)binding.QuestType, Instance = binding.QuestInstance };
            player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, Unknown1 = 0, CategoryId = 110, MessageId = 108871108 });
            player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Unknown = 0, Action = (CharacterActionType)59,
                Target = quest, Parameter1 = 0xDAC3, Parameter2 = quest.Instance, Unknown1 = 0, Unknown2 = 0 });
            player.Session?.Send(new QuestMessage { Identity = player.Identity, Unknown = 0, Action = QuestAction.Delete, Mission = quest, Unknown1 = 0, Unknown2 = 0, Unknown3 = 0 });
        }
        catch (Exception exception) { Quarantine(player, exception); }
    }
}
