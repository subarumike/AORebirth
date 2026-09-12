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
/// Exact accepted Arete props from AreteLandingQuestPropDefinitions. The placement
/// authorizes a concrete registered object; an item name or template alone does not.
/// </summary>
internal sealed class AcceptedQuestPropService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemTemplateCatalog catalog, AuthoredQuestService quests)
{
    internal const int StrongboxInstance = 0x574187CE, RemainsInstance = 0x574187CF;
    internal sealed record Definition(int Instance, int TemplateId, float X, float Y, float Z, string Evidence)
    {
        internal Identity Identity => new() { Type = IdentityType.Terminal, Instance = Instance };
    }
    internal static IReadOnlyList<Definition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new Definition(StrongboxInstance, 295604, 3409.956f, 9.01f, 893.5452f, "20260720-goldman Merchant's Strongbox"),
        new Definition(RemainsInstance, 295620, 3424.016f, 0.01011355f, 887.8564f, "20260721-sara Remains of Shop Thief")
    });
    readonly Dictionary<int, StaticDynel> _bindings = new();
    readonly Dictionary<int, string> _unavailable = new();
    internal IReadOnlyDictionary<int, string> Unavailable => _unavailable;
    bool _stopped;

    internal void Activate()
    {
        if (_stopped || playfield.Identity.Instance != 6553) return;
        foreach (var definition in Definitions)
        {
            if (_bindings.ContainsKey(definition.Instance)) continue;
            if (registry.TryGet(definition.Identity, out var existing))
            {
                if (existing is StaticDynel prop && Matches(prop, definition)) _bindings.Add(definition.Instance, prop);
                else _unavailable[definition.Instance] = "Existing identity does not match the complete accepted placement.";
                continue;
            }
            if (!catalog.TryGet(definition.TemplateId, out var template) || template.Id != definition.TemplateId)
            { _unavailable[definition.Instance] = "Missing exact accepted template " + definition.TemplateId; continue; }
            var created = new AcceptedProp(definition, template)
            {
                Playfield = playfield, SpawnSource = SpawnSource.AcceptedPlacement,
                Position = new AORebirth.Core.Vector.Vector3(definition.X, definition.Y, definition.Z),
                Rotation = new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1)
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

    internal bool ClaimsStrongbox(Identity target) => playfield.Identity.Instance == 6553
        && target.Type == IdentityType.Terminal && target.Instance == StrongboxInstance;
    internal bool ClaimsRemains(Identity target) => playfield.Identity.Instance == 6553
        && target.Type == IdentityType.Terminal && target.Instance == RemainsInstance;

    internal bool TryUseRemains(IZoneSession session, Identity target, Action acknowledgement)
        => ClaimsRemains(target) && Current(session, target, out var player)
            && quests.TryUseShopThiefRemains(player, acknowledgement);

    internal bool TryUseStrongbox(IZoneSession session, Identity slot, Identity target, Action acknowledgement)
        => ClaimsStrongbox(target) && Current(session, target, out var player)
            && slot.Type == IdentityType.Inventory && player.Inventory.TryGetItem(slot.Type, slot.Instance, out var item)
            && quests.TryUseLockpickOnStrongbox(player, slot, item, acknowledgement);

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

    bool Matches(StaticDynel prop, Definition definition) => ReferenceEquals(prop.Playfield, playfield)
        && prop.Identity == definition.Identity && prop.Template.Id == definition.TemplateId
        && prop.Position.xf == definition.X && prop.Position.yf == definition.Y && prop.Position.zf == definition.Z
        && prop.Rotation.xf == 0 && prop.Rotation.yf == 0 && prop.Rotation.zf == 0 && prop.Rotation.wf == 1;

    internal void Shutdown() { _stopped = true; _bindings.Clear(); }

    sealed class AcceptedProp(Definition definition, ItemTemplate template) : StaticDynel(definition.Identity, template)
    {
        protected override bool OnUse(Player player) => false; // Only the exact trusted owner route may apply a quest mutation.
        public override MessageBody BuildSpawnMessage()
        {
            var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
            message.Stats = new[]
            {
                Stat(CharacterStat.Flags, 0x80003201u), Stat(CharacterStat.StaticInstance, (uint)definition.TemplateId),
                Stat(CharacterStat.ACGItemLevel, 1), Stat(CharacterStat.ACGItemTemplateID, (uint)definition.TemplateId),
                Stat(CharacterStat.ACGItemTemplateID2, (uint)definition.TemplateId), Stat(CharacterStat.MultipleCount, 1),
                Stat(CharacterStat.AnimPlay, 0), Stat(CharacterStat.AnimPos, 0)
            };
            return message;
        }
        static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
    }
}
