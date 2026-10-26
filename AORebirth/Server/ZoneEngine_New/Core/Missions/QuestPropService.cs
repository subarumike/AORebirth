namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
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
    // NPCs a prop's use brought out, until they are gone. One at a time per prop.
    readonly Dictionary<int, NpcCharacter> _spawned = new();
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
        var definition = _content.StaticUses.FirstOrDefault(x => x.Playfield == playfield.Identity.Instance && x.Instance == target.Instance)
            ?? StaticUseOfStatel(target);
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

    /// <summary>
    /// The static use a server-registered statel stands for: StaticUses carry the live client's instance, but this
    /// server gives the Dynels.dat statel its own (the Surgery Clinic: captured 0x5809907C, used here as 0xC00E1999).
    /// Matched on its template at the captured spot instead.
    /// </summary>
    StaticUseDefinition? StaticUseOfStatel(Identity target)
    {
        if (!registry.TryGet(target, out var dynel) || dynel is not StaticDynel statel || !ReferenceEquals(statel.Playfield, playfield))
            return null;
        return _content.StaticUses.FirstOrDefault(x => x.Playfield == playfield.Identity.Instance && x.TemplateId == statel.Template.Id
            && statel.Position.Distance2D(new AORebirth.Core.Vector.Vector3(x.Position[0], x.Position[1], x.Position[2])) <= StaticUseRange);
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
        if (definition.CompleteQuests.Length == 0)
            return UseTemplate(definition, player, acknowledgement);
        return CompleteOnUse(definition, player, acknowledgement);
    }

    /// <summary>
    /// A prop no Quests.json step is done on does what its template's OnUse says (the Bank of Rubi-Ka Credit Card
    /// 297315: SpawnQuest KY6D and QHL3, "You pick up the credit card.", SpawnItem BBOK), gated by its ToUse
    /// requirements (297315: neither quest active yet).
    /// </summary>
    bool UseTemplate(QuestPropDefinition definition, Player player, Action acknowledgement)
    {
        if (!_bindings.TryGetValue(definition.Instance, out var prop) || !prop.Template.SpellList.ContainsKey(EventType.OnUse)
            || !prop.Template.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.ToUse, player.ResolveRequirement)
            || !prop.Template.ExecuteOnUseSpells(player, playfield.GetRequiredService<IInventoryRepository>(), playfield.GetRequiredService<IItemBuilder>()))
            return false;

        acknowledgement();
        Hide(definition);
        return true;
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
    /// Brings out the prop's creature. Its NpcScripts program (bound to the spawn's NpcKey) walks it and despawns it;
    /// it spawns without a combat brain, and the script attach gives it one for movement only.
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
            spawnHash: spawn.Hash, attachDefaultBrain: false, npcKey: spawn.NpcKey, configure: walker =>
            {
                walker.Attackable = false;
                walker.Stats.Set(CharacterStat.Flags, walker.Stats.GetOrZero(CharacterStat.Flags) | (int)CharacterFlags.HasBlueName);
            });
        _spawned[definition.Instance] = npc;
    }

    /// <summary>Forgets prop-spawned creatures that are gone (their script despawned them, or they died).</summary>
    internal void Tick()
    {
        DateTime now = DateTime.UtcNow;
        TickEnter(now);
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
            NpcCharacter npc = pair.Value;
            bool present = registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc);
            if (present && !npc.IsDead) continue;
            _spawned.Remove(pair.Key);
        }
    }
    // OnEnter props (Henri Zure 297257): who is in range of each now, so a character triggers it once per arrival.
    readonly Dictionary<int, HashSet<Identity>> _inside = new();
    readonly Dictionary<int, float> _enterRange = new();
    DateTime _nextEnterCheck;
    static readonly TimeSpan EnterCheckInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A placed prop with OnEnter functions runs them on each character that comes into its collision sphere (from
    /// its mesh, GameData DynelCollision.json), once per arrival. Henri Zure 297257 (mesh 200990, 3.96 m): the
    /// captured escort stopped 2.9 m from him; there he recasts Call to Arms on the Omni-AF Private who brought the
    /// player. The functions' own requirements pick who counts (Henri: an "OIUR" Private).
    /// </summary>
    void TickEnter(DateTime now)
    {
        if (_stopped || now < _nextEnterCheck)
            return;
        _nextEnterCheck = now + EnterCheckInterval;

        IInventoryRepository? inventory = null;
        IItemBuilder? items = null;
        foreach (var (instance, prop) in _bindings)
        {
            if (_hidden.ContainsKey(instance) || !prop.Template.SpellList.ContainsKey(EventType.OnEnter))
                continue;
            float range = EnterRange(prop.Template);
            if (range <= 0)
                continue;

            var inside = _inside.TryGetValue(instance, out var set) ? set : _inside[instance] = new HashSet<Identity>();
            var nowInside = new HashSet<Identity>();
            foreach (var dynel in registry.Dynels())
            {
                if (dynel is not Character character || character.IsDead || !ReferenceEquals(character.Playfield, playfield)
                    || character.Distance3D(prop) - character.GetCollisionRadius() > range)
                    continue;
                nowInside.Add(character.Identity);
                if (inside.Contains(character.Identity))
                    continue;

                inventory ??= playfield.GetRequiredService<IInventoryRepository>();
                items ??= playfield.GetRequiredService<IItemBuilder>();
                prop.Template.ExecuteSpells(EventType.OnEnter, character, inventory, items);
            }

            inside.Clear();
            inside.UnionWith(nowInside);
        }
    }

    float EnterRange(ItemTemplate template)
    {
        if (_enterRange.TryGetValue(template.Id, out float known))
            return known;

        int mesh = template.Stats.TryGetValue(CharacterStat.Mesh, out int m) ? m : 0;
        float range = gameData != null && gameData.TryGetDynelCollisionSphere(template.Id, mesh, 0, out float radius, out _)
            ? radius : 0f;
        return _enterRange[template.Id] = range;
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
