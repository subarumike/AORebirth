namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

/// <summary>
/// This playfield's quest props (GameData PlayfieldContent Props.json): placed, and a use that counts completes their
/// Quests.json steps.
/// </summary>
internal sealed class QuestPropService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemTemplateCatalog catalog,
    ZoneEngine_New.Core.Quests.QuestService? templateQuests = null, ZoneEngine_New.Core.GameData.IGameData? gameData = null,
    QuestPropContent? content = null)
{
    readonly QuestPropContent _content = content ?? QuestPropContent.Current;
    // NPCs a prop's use brought out, with when they leave again. One at a time per prop.
    readonly Dictionary<int, (NpcCharacter Npc, DateTime DespawnAtUtc)> _spawned = new();
    QuestPropDefinition[]? _definitions;
    internal IReadOnlyList<QuestPropDefinition> Definitions
        => _definitions ??= _content.Props.Where(x => x.Playfield == playfield.Identity.Instance).ToArray();
    static Identity PropIdentity(QuestPropDefinition definition) => new() { Type = IdentityType.Terminal, Instance = definition.Instance };
    readonly Dictionary<int, StaticDynel> _bindings = new();
    readonly Dictionary<int, string> _unavailable = new();
    internal IReadOnlyDictionary<int, string> Unavailable => _unavailable;
    // Client-resident statels (Dynels.dat) props stand in for: their identity, to the prop's Instance.
    readonly Dictionary<Identity, int> _residentTargets = new();
    bool _stopped;

    internal void Activate()
    {
        if (_stopped) return;
        foreach (var definition in Definitions)
        {
            if (_bindings.ContainsKey(definition.Instance)) continue;
            // The client already draws a Dynels.dat statel of this template here (the Merchant's Strongbox): spawning
            // the prop as well would show two. The prop takes over the client's own instead.
            if (FindResident(definition) is { } resident)
            {
                _bindings.Add(definition.Instance, resident);
                _residentTargets[resident.Identity] = definition.Instance;
                continue;
            }
            if (registry.TryGet(PropIdentity(definition), out var existing))
            {
                if (existing is StaticDynel prop && Matches(prop, definition)) _bindings.Add(definition.Instance, prop);
                else _unavailable[definition.Instance] = "Existing identity does not match the complete accepted placement.";
                continue;
            }
            if (!catalog.TryGet(definition.TemplateId, out var template) || template.Id != definition.TemplateId)
            { _unavailable[definition.Instance] = "Missing exact accepted template " + definition.TemplateId; continue; }
            var created = new ContentProp(definition, template)
            {
                Playfield = playfield, SpawnSource = SpawnSource.ContentPlacement,
                Position = new AORebirth.Core.Vector.Vector3(definition.Position[0], definition.Position[1], definition.Position[2]),
                Rotation = new AORebirth.Core.Vector.Quaternion(definition.Rotation[0], definition.Rotation[1], definition.Rotation[2], definition.Rotation[3])
            };
            if (!registry.TryRegister(created))
                throw new InvalidOperationException("Accepted quest prop identity collided during owner activation.");
            try
            {
                locality.RegisterDynel(created);
                _bindings.Add(definition.Instance, created);
            }
            catch
            {
                locality.UnregisterDynel(created); registry.UnregisterExact(created); throw;
            }
        }
    }

    /// <summary>
    /// A use of a client-static object (interactions.json StaticUses): completes its Quests.json steps that are active.
    /// The use itself still goes on as usual; true when something completed.
    /// </summary>
    const float StaticUseRange = 2 * LootableDynel.OpenRange;

    internal bool TryCompleteStaticUse(IZoneSession session, Identity target)
    {
        if (_stopped || target.Type != IdentityType.Terminal || templateQuests == null)
            return false;
        var definition = _content.StaticUses.FirstOrDefault(x => x.Playfield == playfield.Identity.Instance && x.Instance == target.Instance);
        Player? player = session?.Player;
        if (definition == null || player == null || session!.State != SessionState.InPlay || !ReferenceEquals(player.Session, session)
            || !ReferenceEquals(player.Playfield, playfield) || player.IsDead || player.IsPersistenceQuarantined)
            return false;
        // The spot is the object's centre, not its edge (the Surgery Clinic is a room-sized machine).
        var at = new AORebirth.Core.Vector.Vector3(definition.Position[0], definition.Position[1], definition.Position[2]);
        if (player.Position.Distance2D(at) > StaticUseRange)
            return false;

        bool completed = false;
        foreach (string hash in definition.CompleteQuests)
            completed |= templateQuests.TryComplete(player, hash, out _);
        return completed;
    }

    StaticDynel? FindResident(QuestPropDefinition definition)
    {
        foreach (var dynel in registry.Dynels())
        {
            if (dynel is not StaticDynel statel || statel is VendingMachine || statel.SpawnSource != SpawnSource.StaticDynel
                || !ReferenceEquals(statel.Playfield, playfield) || statel.Template.Id != definition.TemplateId)
                continue;
            float dx = statel.Position.xf - definition.Position[0], dy = statel.Position.yf - definition.Position[1],
                dz = statel.Position.zf - definition.Position[2];
            if (dx * dx + dz * dz <= ResidentRadius * ResidentRadius && Math.Abs(dy) <= 3f)
                return statel;
        }
        return null;
    }

    const float ResidentRadius = 0.5f;

    /// <summary>The prop <paramref name="target"/> is: its own identity, or the client-resident statel it took over.</summary>
    QuestPropDefinition? Resolve(Identity target)
    {
        if (_residentTargets.TryGetValue(target, out int instance))
            return Definitions.FirstOrDefault(x => x.Instance == instance);
        if (target.Type != IdentityType.Terminal)
            return null;
        var definition = Definitions.FirstOrDefault(x => x.Instance == target.Instance);
        // A prop that took over a resident statel answers to that statel's identity only.
        return definition != null && IsResident(definition) ? null : definition;
    }

    bool IsResident(QuestPropDefinition definition)
        => _bindings.TryGetValue(definition.Instance, out var prop) && prop.SpawnSource == SpawnSource.StaticDynel;

    internal bool ClaimsItemTarget(Identity target) => Resolve(target) is { ItemTarget: true };
    internal bool ClaimsUseTarget(Identity target) => Resolve(target) is { ItemTarget: false };
    internal bool TryUseProp(IZoneSession session, Identity target, Action acknowledgement)
    {
        if (Resolve(target) is not { ItemTarget: false } definition || !Current(session, target, definition, out var player))
            return false;
        return CompleteOnUse(definition, player, acknowledgement);
    }

    /// <summary>
    /// A Quests.json "use this object" step: completing it grants its reward and assigns its NextStep. Only a use
    /// that completes something counts: then the prop's creature comes out and the prop goes away for a while.
    /// </summary>
    bool CompleteOnUse(QuestPropDefinition definition, Player player, Action acknowledgement)
    {
        bool completed = false;
        foreach (string hash in definition.CompleteQuests)
            completed |= templateQuests?.TryComplete(player, hash, out _) == true;
        if (!completed)
            return false;

        acknowledgement();
        if (definition.Feedback.Length != 0)
            player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1, Unknown1 = 0, FormattedMessage = definition.Feedback });
        SpawnOnUse(definition);
        Hide(definition);
        return true;
    }

    // Props gone after a use that counted, with when they come back.
    readonly Dictionary<int, DateTime> _hidden = new();

    void Hide(QuestPropDefinition definition)
    {
        // The client keeps a resident statel whatever the server does, so it cannot be taken away.
        if (definition.RespawnSeconds <= 0 || IsResident(definition) || !_bindings.TryGetValue(definition.Instance, out var prop))
            return;

        locality.UnregisterDynel(prop);
        _hidden[definition.Instance] = DateTime.UtcNow.AddSeconds(definition.RespawnSeconds);
    }

    /// <summary>
    /// Brings out the prop's creature and walks it along its captured route. It runs without a brain, so it does
    /// not wander off or head home mid-route, and leaves after DespawnSeconds.
    /// </summary>
    void SpawnOnUse(QuestPropDefinition definition)
    {
        if (definition.SpawnOnUse is not { } spawn || gameData == null || _spawned.ContainsKey(definition.Instance)
            || !gameData.TryResolveMobTemplate(spawn.Hash, null, out var template))
            return;

        var at = new AORebirth.Core.Vector.Vector3(spawn.Position[0], spawn.Position[1], spawn.Position[2]);
        // Part of the scene, not a target: not attackable, and blue-named so the client shows it that way too.
        // Set before the first spawn packet goes out.
        NpcCharacter npc = playfield.GetRequiredService<SpawnService>().SpawnMob(template, at, spawnSource: SpawnSource.ContentPlacement,
            spawnHash: spawn.Hash, attachDefaultBrain: false, configure: walker =>
            {
                walker.Attackable = false;
                walker.Stats.Set(CharacterStat.Flags, walker.Stats.GetOrZero(CharacterStat.Flags) | (int)CharacterFlags.HasBlueName);
            });
        var path = spawn.Path.Select(p => new AORebirth.Core.Vector.Vector3(p[0], p[1], p[2])).ToList();
        if (path.Count > 0)
        {
            npc.Motor.SetPath(path);
            ZoneEngine_New.Core.Ai.NpcFollowTarget.AnnounceCoordinatePath(npc, at, path);
        }
        _spawned[definition.Instance] = (npc, DateTime.UtcNow.AddSeconds(spawn.DespawnSeconds));
    }

    /// <summary>Removes prop-spawned creatures whose time is up (or that already died).</summary>
    internal void Tick()
    {
        DateTime now = DateTime.UtcNow;
        foreach (var pair in _hidden.ToArray())
        {
            if (now < pair.Value) continue;
            _hidden.Remove(pair.Key);
            if (!_stopped && _bindings.TryGetValue(pair.Key, out var prop))
                locality.RegisterDynel(prop);
        }

        if (_spawned.Count == 0) return;
        foreach (var pair in _spawned.ToArray())
        {
            NpcCharacter npc = pair.Value.Npc;
            bool present = registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc);
            if (present && !npc.IsDead && now < pair.Value.DespawnAtUtc) continue;
            if (present && !npc.IsDead) playfield.GetRequiredService<SpawnService>().DespawnNpc(npc);
            _spawned.Remove(pair.Key);
        }
    }
    internal bool TryUseItemOnProp(IZoneSession session, Identity slot, Identity target, Action acknowledgement)
    {
        if (Resolve(target) is not { ItemTarget: true } definition || !Current(session, target, definition, out var player)
            || slot.Type != IdentityType.Inventory || !player.Inventory.TryGetItem(slot.Type, slot.Instance, out var item))
            return false;

        AlertGuards(definition, player);

        // An item used on the prop (the extinguisher on a Gas Fire): the right one counts, and it is kept, unless it
        // is left on the prop (the bug planted in the Prized Houseplant).
        if (!definition.UseItemIds.Contains(item.LowId) && !definition.UseItemIds.Contains(item.HighId))
            return false;
        if (!CompleteOnUse(definition, player, acknowledgement))
            return false;
        if (definition.ConsumeUsedItem)
            item.DestroyOne(player, slot);
        return true;
    }

    bool Current(IZoneSession session, Identity target, QuestPropDefinition definition, out Player player)
    {
        player = session?.Player!;
        if (_stopped || player == null || _hidden.ContainsKey(definition.Instance) || session!.State != SessionState.InPlay || !ReferenceEquals(player.Session, session)
            || !ReferenceEquals(player.Playfield, playfield) || player.IsDead || player.IsPersistenceQuarantined
            || !player.Inventory.IsHydrated || !_bindings.TryGetValue(definition.Instance, out var prop)
            || !ReferenceEquals(prop.Playfield, playfield) || !registry.TryGet(target, out var current) || !ReferenceEquals(current, prop)
            || !registry.TryGet(player.Identity, out var currentPlayer) || !ReferenceEquals(currentPlayer, player)
            || player.GetEdgeDistanceTo(prop) > LootableDynel.OpenRange) return false;
        // A resident statel is the client's own placement, not the prop's: it was matched on template and spot.
        return IsResident(definition) || Matches(prop, definition);
    }

    /// <summary>
    /// An item used on the prop (a pick tried on the Merchant's Strongbox), whether it works or not: its GuardNpcs that
    /// would notice the player by their usual aggro rules go for them (the Robotic Guard Dog, which otherwise lets
    /// people walk past).
    /// </summary>
    void AlertGuards(QuestPropDefinition definition, Player player)
    {
        if (definition.GuardNpcs.Length == 0)
            return;
        foreach (var dynel in registry.Dynels())
            if (dynel is NpcCharacter npc && ReferenceEquals(npc.Playfield, playfield) && !npc.IsDead
                && Array.IndexOf(definition.GuardNpcs, npc.SpawnHash) >= 0)
                npc.Brain?.TryAlertAggro(player);
    }

    /// <summary>A guard of one of this playfield's props attacks only when a prop is tampered with, never on sight.</summary>
    internal bool IsGuard(NpcCharacter npc)
        => !string.IsNullOrEmpty(npc.SpawnHash) && Definitions.Any(x => Array.IndexOf(x.GuardNpcs, npc.SpawnHash) >= 0);

    bool Matches(StaticDynel prop, QuestPropDefinition definition) => ReferenceEquals(prop.Playfield, playfield)
        && prop.Identity == PropIdentity(definition) && prop.Template.Id == definition.TemplateId
        && prop.Position.xf == definition.Position[0] && prop.Position.yf == definition.Position[1] && prop.Position.zf == definition.Position[2]
        && prop.Rotation.xf == definition.Rotation[0] && prop.Rotation.yf == definition.Rotation[1] && prop.Rotation.zf == definition.Rotation[2] && prop.Rotation.wf == definition.Rotation[3];

    internal void Shutdown() { _stopped = true; _bindings.Clear(); _residentTargets.Clear(); _spawned.Clear(); _hidden.Clear(); }

    sealed class ContentProp(QuestPropDefinition definition, ItemTemplate template) : StaticDynel(PropIdentity(definition), template)
    {
        protected override bool OnUse(Player player) => false; // Only the exact trusted owner route may apply a quest mutation.
        public override MessageBody BuildSpawnMessage()
        {
            var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
            // Identitytype/Instance are the owner: a set owner means "in a container", which drops the position,
            // heading and playfield from the packet and the client draws nothing. A placed prop has no owner
            // (retail Gas Fire SimpleItemFullUpdate, 2026-09-29), as WorldItem does.
            message.Identitytype = 0;
            message.Instance = 0;
            message.Stats = definition.Stats.Select(x => Stat((CharacterStat)x.Key, x.Value)).ToArray();
            return message;
        }
        static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
    }
}
