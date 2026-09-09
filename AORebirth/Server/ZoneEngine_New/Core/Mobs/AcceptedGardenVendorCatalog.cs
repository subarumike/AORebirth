namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Trade;

/// <summary>The eleven existing ungated garden vendors. No name/proximity lookup or key-gate bypass.</summary>
internal static class AcceptedGardenVendorCatalog
{
    internal sealed record StockRow(int Slot, int LowId, int HighId, int Quality);
    internal sealed record Placement(int PlayfieldId, int SourceNpcInstance, int SourceVendorInstance,
        string Name, string Evidence, int VendorTemplateId, float X, float Y, float Z, float HeadingY, float HeadingW,
        int Mesh, IReadOnlyList<StockRow> Stock);

    // Exact complete geometry/mesh rows from ThrakOmniGardenSpawn and AbanGardenSpawn.
    // Aban's two Protection variants remain separate identities, stock sets and positions.
    internal static IReadOnlyList<Placement> Placements { get; } = Array.AsReadOnly(new[]
    {
        Thrak(0x79758F3F, 491.397156f, 33.0415535f, 323.112885f, 0.655906737f, 0.7548419f, 209541),
        Thrak(0x79758F3E, 490.879669f, 33.06743f, 317.171753f, -0.673385739f, 0.7392913f, 209532),
        Thrak(0x79758F3B, 490.974152f, 33.01f, 311.799866f, -0.836958647f, 0.5472661f, 209541),
        Thrak(0x79758F3C, 491.040436f, 33.01f, 305.692566f, -0.557796538f, 0.829977751f, 209532),
        Thrak(0x79758F3D, 491.143738f, 33.01f, 299.771362f, -0.8378405f, 0.5459151f, 209541),
        Aban(0x7A2013B7, 112.386261f, -0.6433794f, -0.765547752f, 209532),
        Aban(0x7A2013B4, 112.752884f, 0.999763548f, 0.0217469819f, 209541),
        Aban(0x7A2013B5, 110.80838f, -0.9111272f, 0.41212526f, 209532),
        Aban(0x7A2013B8, 110.409264f, -0.838834047f, 0.5443871f, 209541),
        Aban(0x7A2013B6, 108.365479f, -0.519817f, 0.854277849f, 209532),
        Aban(0x7A2013B9, 107.48468f, -0.327861339f, 0.9447259f, 209541)
    });

    internal static IReadOnlyList<AcceptedSocialNpcCatalog.Definition> Definitions { get; } = Array.AsReadOnly(
        Placements.Select(content => new AcceptedSocialNpcCatalog.Definition(new AcceptedNpcBinding(
            "legacy:garden-vendor:" + content.PlayfieldId + ":" + content.SourceNpcInstance.ToString("X8"),
            "AORebirth/Server/ZoneEngine/Core/Playfields/" + (content.PlayfieldId == 4677 ? "ThrakOmniGardenSpawn.cs" : "AbanGardenSpawn.cs")
                + ";" + content.Evidence + ";sourceVendor:" + content.SourceVendorInstance.ToString("X8"),
            "SimpleChar:" + content.SourceNpcInstance.ToString("X8"), content.PlayfieldId, true, true),
            items => Create(content, items))).ToArray());

    static Placement Thrak(int source, float x, float y, float z, float hy, float hw, int mesh)
    {
        var definition = CapturedThrakGardenVendorContentProvider.Definitions.Single(value => value.SourceNpcInstance == source
            && !value.RequiresCompletedGardenKeyQuest);
        return new(4677, source, definition.SourceVendorInstance, definition.DisplayName, definition.Evidence, definition.VendorTemplateId,
            x, y, z, hy, hw, mesh, Array.AsReadOnly(definition.Stock.Select(row => new StockRow(row.Slot, row.LowId, row.HighId, row.Quality)).ToArray()));
    }

    static Placement Aban(int source, float y, float hy, float hw, int mesh)
    {
        var definition = CapturedAbanGardenVendorContentProvider.Definitions.Single(value => value.SourceNpcInstance == source && !value.RequiresGardenKey);
        return new(4676, source, definition.SourceVendorInstance, definition.DisplayName, definition.Evidence, definition.VendorTemplateId,
            definition.ExpectedX, y, definition.ExpectedZ, hy, hw, mesh,
            Array.AsReadOnly(definition.Stock.Select(row => new StockRow(row.Slot, row.LowId, row.HighId, row.Quality)).ToArray()));
    }

    static NpcCharacter Create(Placement content, IItemBuilder items)
    {
        bool aban = content.PlayfieldId == 4676;
        var npc = new AcceptedGardenVendorCharacter(content, items)
        {
            Name = content.Name, SpawnSource = SpawnSource.AcceptedPlacement,
            Position = new(content.X, content.Y, content.Z), Rotation = new(0f, content.HeadingY, 0f, content.HeadingW)
        };
        // Existing factories explicitly seed BART via NonPlayerCharacterHandler.CreateMob,
        // then apply the garden row. Preserve those retained fields; no nearest-level lookup.
        foreach (var stat in new (CharacterStat Id, int Value)[]
        {
            (CharacterStat.Level, 30), (CharacterStat.MaxHealth, 32800), (CharacterStat.Health, 32800),
            (CharacterStat.MonsterData, aban ? 236640 : 208640), (CharacterStat.Scale, aban ? 100 : 200),
            (CharacterStat.VisualFlags, 31), (CharacterStat.Flags, 271061505), (CharacterStat.HeadMesh, 40694),
            (CharacterStat.NPCFamily, 137), ((CharacterStat)466, 15), (CharacterStat.Fatness, 1),
            (CharacterStat.Side, aban ? 1 : 0), (CharacterStat.Breed, aban ? (int)Breed.Monster : 1),
            (CharacterStat.Sex, 2), (CharacterStat.Race, 1), (CharacterStat.Profession, 15), (CharacterStat.VisualProfession, 15),
            (CharacterStat.RunSpeed, aban ? 103 : 513), (CharacterStat.AccountFlags, 0), (CharacterStat.Expansion, 0)
        }) npc.Stats.Set(stat.Id, stat.Value);
        // CreateMob does not copy BART SQL textures. Garden clears both mesh layers to this exact mesh.
        npc.Meshes.Add(new Mesh { Position = 1, Id = (uint)content.Mesh, OverrideTextureId = 0, Layer = 2 });
        npc.Motor.RefreshFromStats(); return npc;
    }

    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure)
    {
        failure = string.Empty;
        if (npc is not AcceptedGardenVendorCharacter merchant || npc.IsDead || npc.Shop != null
            || !Placements.Any(value => ReferenceEquals(value, merchant.Content))
            || npc.Identity.Type != IdentityType.CanbeAffected || npc.Identity.Instance != merchant.Content.SourceNpcInstance)
        { failure = "Not an exact current ungated garden merchant."; return false; }
        var content = merchant.Content;
        if (!catalog.TryGet(content.VendorTemplateId, out var template))
        { failure = "Missing the existing garden vendor template " + content.VendorTemplateId; return false; }
        if (content.Stock.Count == 0) { failure = "Captured garden stock is empty."; return false; }
        for (int index = 0; index < content.Stock.Count; index++)
        {
            var row = content.Stock[index];
            if (row.Slot != index || row.Quality <= 0 || !catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _))
            { failure = "Missing or noncontiguous captured garden stock at slot " + row.Slot; return false; }
        }
        var shop = new VendingMachine(new() { Type = IdentityType.VendingMachine, Instance = content.SourceVendorInstance }, template)
        { Playfield = npc.Playfield, Position = npc.Position, Rotation = npc.Rotation };
        shop.Stock.SetAcceptedSnapshot(content.Stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray());
        npc.AttachShop(shop); npc.Stats.Set(CharacterStat.Flags, 271061505); return true;
    }

    internal sealed class AcceptedGardenVendorCharacter(Placement content, IItemBuilder items)
        : NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = content.SourceNpcInstance }, items)
    {
        internal Placement Content { get; } = content;
        public override void Rebase() { }
        public override void RebaseWeapons() { }
        public override void StartFighting(Identity target, byte action) { }
        protected override void TickCombat(double deltaTime) { }
        // Legacy Character.Read leaves DoNotDoTimers=true; both factories explicitly quarantine combat.
        public override void Tick(double deltaTime) { if (IsDead) base.Tick(deltaTime); }
        public override SimpleCharFullUpdateMessage BuildSpawnMessage()
        {
            var result = base.BuildSpawnMessage();
            result.Meshes = Meshes.ToArray(); // Do not append New's automatic head mesh to the exact cleared Legacy layer.
            return result;
        }
    }
}
