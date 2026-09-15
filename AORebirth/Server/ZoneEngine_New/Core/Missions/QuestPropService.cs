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
/// Editable prop placements and action bindings using the existing owner and DAO boundaries.
/// </summary>
internal sealed class QuestPropService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemTemplateCatalog catalog, AuthoredQuestService quests)
{
    internal IReadOnlyList<QuestPropDefinition> Definitions => quests?.Content.Props.Where(x => x.Playfield == playfield.Identity.Instance).ToArray() ?? [];
    static Identity PropIdentity(QuestPropDefinition definition) => new() { Type = IdentityType.Terminal, Instance = definition.Instance };
    readonly Dictionary<int, StaticDynel> _bindings = new();
    readonly Dictionary<int, string> _unavailable = new();
    internal IReadOnlyDictionary<int, string> Unavailable => _unavailable;
    bool _stopped;

    internal void Activate()
    {
        if (_stopped) return;
        foreach (var definition in Definitions)
        {
            if (_bindings.ContainsKey(definition.Instance)) continue;
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

    internal bool ClaimsItemTarget(Identity target) => target.Type == IdentityType.Terminal && Definitions.Any(x => x.Instance == target.Instance && x.ItemTarget);
    internal bool ClaimsUseTarget(Identity target) => target.Type == IdentityType.Terminal && Definitions.Any(x => x.Instance == target.Instance && !x.ItemTarget);
    internal bool TryUseProp(IZoneSession session, Identity target, Action acknowledgement)
        => ClaimsUseTarget(target) && Current(session, target, out var player)
            && quests.TryExecuteAction(player, Definitions.Single(x => x.Instance == target.Instance).Action, acknowledge: acknowledgement);
    internal bool TryUseItemOnProp(IZoneSession session, Identity slot, Identity target, Action acknowledgement)
        => ClaimsItemTarget(target) && Current(session, target, out var player)
            && slot.Type == IdentityType.Inventory && player.Inventory.TryGetItem(slot.Type, slot.Instance, out var item)
            && quests.TryExecuteAction(player, Definitions.Single(x => x.Instance == target.Instance).Action, slot, item, acknowledgement);

    bool Current(IZoneSession session, Identity target, out Player player)
    {
        player = session?.Player!;
        if (_stopped || player == null || session!.State != SessionState.InPlay || !ReferenceEquals(player.Session, session)
            || !ReferenceEquals(player.Playfield, playfield) || player.IsDead || player.IsPersistenceQuarantined
            || !player.Inventory.IsHydrated || !_bindings.TryGetValue(target.Instance, out var prop)
            || !ReferenceEquals(prop.Playfield, playfield) || !registry.TryGet(target, out var current) || !ReferenceEquals(current, prop)
            || !registry.TryGet(player.Identity, out var currentPlayer) || !ReferenceEquals(currentPlayer, player)
            || player.Distance3D(prop) > LootableDynel.OpenRange) return false;
        return Matches(prop, Definitions.Single(definition => definition.Instance == target.Instance));
    }

    bool Matches(StaticDynel prop, QuestPropDefinition definition) => ReferenceEquals(prop.Playfield, playfield)
        && prop.Identity == PropIdentity(definition) && prop.Template.Id == definition.TemplateId
        && prop.Position.xf == definition.Position[0] && prop.Position.yf == definition.Position[1] && prop.Position.zf == definition.Position[2]
        && prop.Rotation.xf == definition.Rotation[0] && prop.Rotation.yf == definition.Rotation[1] && prop.Rotation.zf == definition.Rotation[2] && prop.Rotation.wf == definition.Rotation[3];

    internal void Shutdown() { _stopped = true; _bindings.Clear(); }

    sealed class ContentProp(QuestPropDefinition definition, ItemTemplate template) : StaticDynel(PropIdentity(definition), template)
    {
        protected override bool OnUse(Player player) => false; // Only the exact trusted owner route may apply a quest mutation.
        public override MessageBody BuildSpawnMessage()
        {
            var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
            message.Stats = definition.Stats.Select(x => Stat((CharacterStat)x.Key, x.Value)).ToArray();
            return message;
        }
        static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
    }
}
