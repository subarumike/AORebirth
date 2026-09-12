namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Trade;

/// <summary>
/// Exactly the six compiled Legacy Subway merchant placements. Combat readiness is
/// independent of their proven social appearance and NPC-to-shop identity binding.
/// </summary>
internal static class AcceptedSubwayMerchantCatalog
{
    internal const int TailorSourceIdentity = 0x79135F51;
    internal static IReadOnlyList<AcceptedSocialNpcCatalog.Definition> Definitions { get; }
        = Array.AsReadOnly(CapturedSubwayVendorContentProvider.Definitions.Select(content =>
            new AcceptedSocialNpcCatalog.Definition(new AcceptedNpcBinding(
                "legacy:CapturedSubwayVendorContentProvider:127:" + content.SourceNpcInstance.ToString("X8"),
                "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayVendorContentProvider.cs;"
                    + content.Evidence + ";sourceVendor:" + content.SourceVendorInstance.ToString("X8")
                    + ";vendorTemplate:" + content.VendorTemplateId,
                "SimpleChar:" + content.SourceNpcInstance.ToString("X8"),
                CapturedSubwayVendorContentProvider.SubwayPlayfieldResource,
                content.SourceNpcInstance == TailorSourceIdentity, content.HasCapturedStock),
                items => Create(content, items))).ToArray());

    static NpcCharacter Create(CapturedSubwayVendorDefinition content, IItemBuilder items)
    {
        var npc = new AcceptedSubwayMerchantCharacter(content, items)
        {
            Name = content.DisplayName, SpawnSource = SpawnSource.AcceptedPlacement,
            Position = new AORebirth.Core.Vector.Vector3(content.X, content.Y, content.Z),
            Rotation = new AORebirth.Core.Vector.Quaternion(content.HeadingX, content.HeadingY, content.HeadingZ, content.HeadingW)
        };
        // Exact CreateCharacter overrides from CapturedSubwayVendorRuntimeService.
        // These are captured operational fields, not a guessed BART/level fallback.
        foreach (var stat in new (CharacterStat Id, int Value)[]
        {
            (CharacterStat.Side, content.Side), (CharacterStat.Fatness, content.Fatness),
            (CharacterStat.Breed, content.Breed), (CharacterStat.Sex, content.Sex), (CharacterStat.Race, content.Race),
            (CharacterStat.Flags, content.CharacterFlags), (CharacterStat.AccountFlags, 0), (CharacterStat.Expansion, 0),
            (CharacterStat.NPCFamily, 0), ((CharacterStat)466, 0), (CharacterStat.MonsterData, content.MonsterData),
            (CharacterStat.Scale, content.MonsterScale), (CharacterStat.HeadMesh, content.HeadMesh),
            (CharacterStat.VisualFlags, content.VisualFlags), (CharacterStat.CurrentMovementMode, 3),
            (CharacterStat.PrevMovementMode, 3), (CharacterStat.RunSpeed, content.RunSpeed),
            (CharacterStat.Level, content.Level), (CharacterStat.MaxHealth, content.Health), (CharacterStat.Health, content.Health)
        }) npc.Stats.Set(stat.Id, stat.Value);
        foreach (var texture in content.Textures) npc.Textures.Add(new AOTextures(texture.Place, texture.Id));
        foreach (var mesh in content.Meshes) npc.Meshes.Add(new Mesh
        { Position = (byte)mesh.Position, Id = mesh.Id, OverrideTextureId = mesh.OverrideTextureId, Layer = (byte)mesh.Layer });
        npc.Motor.RefreshFromStats();
        return npc;
    }

    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure)
    {
        failure = string.Empty;
        if (npc is not AcceptedSubwayMerchantCharacter merchant)
        { failure = "Not an exact accepted Subway merchant."; return false; }
        var content = merchant.Content;
        if (npc.Identity.Type != IdentityType.CanbeAffected || npc.Identity.Instance != content.SourceNpcInstance || npc.IsDead
            || !content.HasCapturedStock || npc.Shop != null)
        { failure = "Merchant source/lifetime/shop binding is invalid or already attached."; return false; }
        if (!catalog.TryGet(content.VendorTemplateId, out var vendorTemplate))
        { failure = "Missing exact captured vendor template " + content.VendorTemplateId; return false; }
        var stock = content.Stock.OrderBy(row => row.Slot).ToArray();
        for (int index = 0; index < stock.Length; index++)
        {
            var row = stock[index];
            if (row.Slot != index || row.Quality <= 0 || !catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _))
            { failure = "Missing or noncontiguous exact captured stock at slot " + row.Slot; return false; }
        }
        // Validate all endpoints before exposing any shop; IItemBuilder's unknown-template
        // and missing-high fallbacks are deliberately not used as content authority.
        var shop = new VendingMachine(new() { Type = IdentityType.VendingMachine, Instance = content.SourceVendorInstance }, vendorTemplate)
        { Playfield = npc.Playfield, Position = npc.Position, Rotation = npc.Rotation };
        shop.Stock.SetAcceptedSnapshot(stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray());
        npc.AttachShop(shop);
        npc.Stats.Set(CharacterStat.Flags, content.CharacterFlags);
        return true;
    }

    internal sealed class AcceptedSubwayMerchantCharacter(CapturedSubwayVendorDefinition content, IItemBuilder items)
        : NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = content.SourceNpcInstance }, items)
    {
        internal CapturedSubwayVendorDefinition Content { get; } = content;
        public override void Rebase() { }
        public override void RebaseWeapons() { }
        public override void StartFighting(Identity target, byte action) { }
        protected override void TickCombat(double deltaTime) { }
        // Legacy DoNotDoTimers=true: no invented patrol, regen or retaliation. Preserve
        // the existing shared death progression instead of creating a merchant policy.
        public override void Tick(double deltaTime) { if (IsDead) base.Tick(deltaTime); }

        public override SimpleCharFullUpdateMessage BuildSpawnMessage()
        {
            var result = base.BuildSpawnMessage();
            var flags = (SimpleCharFullUpdateFlags)Content.CapturedScfuFlags;
            result.Appearance.Value = (uint)Content.AppearanceValue;
            result.CharacterInfo = new SimpleNpcInfo { Family = 0, LosHeight = 0 };
            result.AdditionalFlags = flags; result.SuppressedFlags = ~flags;
            result.Flags2 = 0; result.Unknown1 = Content.CapturedScfuUnknown1.ToArray();
            result.Unknown2 = 0; result.VisibleTitle = 0;
            result.Textures = Content.Textures.Select(texture => new Texture
            { Place = texture.Place, Id = texture.Id, Unknown = texture.Unknown }).ToArray();
            result.Meshes = Content.Meshes.Select(mesh => new Mesh
            { Position = (byte)mesh.Position, Id = mesh.Id, OverrideTextureId = mesh.OverrideTextureId, Layer = (byte)mesh.Layer }).ToArray();
            result.Waypoints = Content.Waypoints.Select(point => new Vector3 { X = point.X, Y = point.Y, Z = point.Z }).ToArray();
            return result;
        }
    }
}
