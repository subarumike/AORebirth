namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Trade;

/// <summary>Projects validated editable actor content through ordinary runtime mechanics.</summary>
public static class WorldNpcFactory
{
    public static NpcCharacter Create(WorldNpcDefinition definition, IItemBuilder items, Identity? optionalIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var npc = new ContentNpcCharacter(definition, optionalIdentity ?? new Identity
            { Type = IdentityType.CanbeAffected, Instance = definition.InstanceId }, items)
        {
            Name = definition.Name, Attackable = definition.Attackable, SpawnSource = SpawnSource.ContentPlacement,
            Position = new(definition.Position[0], definition.Position[1], definition.Position[2]),
            Rotation = new(definition.Rotation[0], definition.Rotation[1], definition.Rotation[2], definition.Rotation[3])
        };
        foreach (var stat in definition.Stats) npc.Stats.Set((CharacterStat)stat.Key, stat.Value);
        foreach (var texture in definition.Textures) npc.Textures.Add(new AOTextures(texture.Place, texture.Id));
        foreach (var mesh in definition.Meshes) npc.Meshes.Add(Clone(mesh));
        npc.Motor.RefreshFromStats();
        return npc;
    }
    public static bool TryCreateShop(WorldVendorDefinition definition, IItemTemplateCatalog catalog,
        out VendingMachine shop, out string failure, Identity? optionalIdentity = null)
    {
        shop = null!; failure = string.Empty;
        WorldContentCatalog.RequireVendor(definition);
        if (!catalog.TryGet(definition.TemplateId, out var template))
        { failure = "Missing vendor template " + definition.TemplateId; return false; }
        foreach (var row in definition.Stock)
            if (!catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _))
            { failure = "Missing vendor item template at slot " + row.Slot; return false; }
        shop = new VendingMachine(optionalIdentity ?? new Identity
            { Type = IdentityType.VendingMachine, Instance = definition.InstanceId }, template);
        shop.Stock.SetConfiguredSnapshot(definition.Stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray());
        return true;
    }
    public static bool TryAttachShop(NpcCharacter npc, WorldVendorDefinition definition, IItemTemplateCatalog catalog,
        out string failure, Identity? optionalIdentity = null)
    {
        failure = string.Empty;
        if (npc is not ContentNpcCharacter content || !ReferenceEquals(content.Definition.Vendor, definition)
            || npc.IsDead || npc.Shop != null) { failure = "NPC shop definition is unbound, already attached, or actor is dead."; return false; }
        if (!TryCreateShop(definition, catalog, out var shop, out failure, optionalIdentity)) return false;
        shop.Playfield = npc.Playfield; shop.Position = npc.Position; shop.Rotation = npc.Rotation;
        int flags = npc.Stats.GetOrZero(CharacterStat.Flags);
        npc.AttachShop(shop); npc.Stats.Set(CharacterStat.Flags, flags);
        return true;
    }
    static Mesh Clone(Mesh mesh) => new() { Position = mesh.Position, Id = mesh.Id, Layer = mesh.Layer, OverrideTextureId = mesh.OverrideTextureId };

    internal static WorldNpcDefinition? DefinitionFor(NpcCharacter npc) => (npc as ContentNpcCharacter)?.Definition;

    sealed class ContentNpcCharacter(WorldNpcDefinition definition, Identity identity, IItemBuilder items) : NpcCharacter(identity, items)
    {
        internal WorldNpcDefinition Definition => definition;
        protected override bool UsesPassiveRegen => definition.PassiveRegen;
        public override void Rebase() { if (!definition.Passive) base.Rebase(); }
        public override void RebaseWeapons() { if (!definition.Passive) base.RebaseWeapons(); }
        public override void StartFighting(Identity target, byte action) { if (!definition.Passive) base.StartFighting(target, action); }
        protected override void TickCombat(double deltaTime) { if (!definition.Passive) base.TickCombat(deltaTime); }
        public override void Tick(double deltaTime) { if (definition.TickLiving || IsDead) base.Tick(deltaTime); }
        public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
        {
            if (definition.Vendor?.CompanionPacket is not { } packet)
            {
                foreach (var message in base.BuildSpawnCompanionMessages()) yield return message;
                yield break;
            }
            if (Shop is not { } shop || IsDead || Playfield == null) yield break;
            var projection = packet.Deserialize<VendingMachineFullUpdateMessage>(new JsonSerializerOptions
                { PropertyNameCaseInsensitive = true, IncludeFields = true })
                ?? throw new InvalidOperationException("Invalid vendor companion packet.");
            projection.Identity = shop.Identity; projection.NpcIdentity = Identity; projection.PlayfieldId = Playfield.Identity.Instance;
            yield return projection;
        }
        public override SimpleCharFullUpdateMessage BuildSpawnMessage()
        {
            var result = base.BuildSpawnMessage(); var view = definition.Presentation;
            if (view.AppearanceValue is { } appearance) result.Appearance.Value = appearance;
            if (view.Family is { } family) result.CharacterInfo = new SimpleNpcInfo { Family = family, LosHeight = view.LosHeight ?? 0 };
            if (view.Flags is { } flags) { result.AdditionalFlags = (SimpleCharFullUpdateFlags)flags; result.SuppressedFlags = ~(SimpleCharFullUpdateFlags)flags; }
            if (view.Flags2 is { } flags2) result.Flags2 = flags2;
            if (view.Unknown1 != null) result.Unknown1 = view.Unknown1.ToArray();
            if (view.Unknown2 is { } unknown2) result.Unknown2 = unknown2;
            if (view.VisibleTitle is { } title) result.VisibleTitle = title;
            if (view.Textures != null) result.Textures = view.Textures.Select(t => new Texture { Place = t.Place, Id = t.Id, Unknown = t.Unknown }).ToArray();
            if (view.Meshes != null) result.Meshes = view.Meshes.Select(Clone).ToArray();
            if (view.Waypoints != null) result.Waypoints = view.Waypoints.Select(p => new Vector3 { X = p.X, Y = p.Y, Z = p.Z }).ToArray();
            return result;
        }
    }
}
