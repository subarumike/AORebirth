namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;

// Baseline test accessors read the same editable files as production. They do not
// contain actor definitions or participate in runtime construction/authorization.
internal static class WorldContentFixtures
{
    internal static readonly WorldContentCatalog Content = WorldContentCatalog.Load(Path.Combine(AppContext.BaseDirectory, "GameData"));
    internal static bool Attach(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure)
    {
        var content = WorldNpcFactory.DefinitionFor(npc);
        failure = "No data-bound vendor definition.";
        return content?.Vendor != null && WorldNpcFactory.TryAttachShop(npc, content.Vendor, catalog, out failure);
    }
}
internal static class SocialNpcFixture
{
    internal sealed record Definition(WorldNpcDefinition Content)
    {
        internal NpcContentBinding Binding => new(Content.Key, Content.Provenance, Content.ContentNpcIdentity,
            Content.PlayfieldId, Content.HasDialogue, Content.Vendor != null);
        internal NpcCharacter Create(IItemBuilder items) => WorldNpcFactory.Create(Content, items);
    }
    internal static IReadOnlyList<Definition> Definitions { get; } = WorldContentFixtures.Content.Npcs
        .Where(n => n.PlayfieldId > 0).Select(n => new Definition(n)).ToArray();
}
internal static class SubwayMerchantFixture
{
    internal static IReadOnlyList<SocialNpcFixture.Definition> Definitions { get; } = SocialNpcFixture.Definitions.Where(n => n.Binding.PlayfieldId == 127).ToArray();
    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure) => WorldContentFixtures.Attach(npc, items, catalog, out failure);
}
internal static class AreteQuestNpcFixture
{
    internal static IReadOnlyList<SocialNpcFixture.Definition> Definitions { get; } = SocialNpcFixture.Definitions.Where(n => n.Binding.PlayfieldId == 6553 && !n.Binding.HasVendor).ToArray();
}
internal static class AreteVendorFixture
{
    internal sealed record StandaloneDefinition(WorldShopDefinition Data)
    {
        internal string PlacementIdentity => Data.Key;
        internal string SourceIdentity => Data.Provenance;
        internal int PlayfieldId => Data.PlayfieldId;
        internal int SourceVendorInstance => Data.Vendor.InstanceId;
        internal StandaloneContent Content => new(Data);
    }
    internal sealed record StandaloneContent(WorldShopDefinition Data)
    {
        internal int SourceVendorInstance => Data.Vendor.InstanceId;
        internal int TemplateId => Data.Vendor.TemplateId;
        internal float X => Data.Position[0]; internal float Y => Data.Position[1]; internal float Z => Data.Position[2];
        internal float HeadingX => Data.Rotation[0]; internal float HeadingY => Data.Rotation[1]; internal float HeadingZ => Data.Rotation[2]; internal float HeadingW => Data.Rotation[3];
        internal IReadOnlyList<WorldVendorStock> Stock => Data.Vendor.Stock;
    }
    internal static IReadOnlyList<SocialNpcFixture.Definition> Definitions { get; } = SocialNpcFixture.Definitions.Where(n => n.Binding.PlayfieldId == 6553 && n.Binding.HasVendor).ToArray();
    internal static IReadOnlyList<StandaloneDefinition> StandaloneDefinitions { get; } = WorldContentFixtures.Content.Shops.Select(n => new StandaloneDefinition(n)).ToArray();
    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure) => WorldContentFixtures.Attach(npc, items, catalog, out failure);
    internal static bool TryCreateStandaloneShop(StandaloneDefinition definition, Playfield playfield, IItemTemplateCatalog catalog, out VendingMachine shop, out string failure)
    {
        shop = null!; failure = "Incorrect placement playfield.";
        if (definition.PlayfieldId != playfield.Identity.Instance || !WorldNpcFactory.TryCreateShop(definition.Data.Vendor, catalog, out shop, out failure)) return false;
        shop.Playfield = playfield; shop.Position = new(definition.Content.X, definition.Content.Y, definition.Content.Z);
        shop.Rotation = new(definition.Content.HeadingX, definition.Content.HeadingY, definition.Content.HeadingZ, definition.Content.HeadingW);
        return true;
    }
}
internal static class GardenVendorFixture
{
    internal sealed record Placement(WorldNpcDefinition Data)
    {
        internal int PlayfieldId => Data.PlayfieldId;
        internal int SourceNpcInstance => Data.InstanceId;
        internal int SourceVendorInstance => Data.Vendor!.InstanceId;
        internal string Name => Data.Name; internal string Evidence => Data.Provenance;
        internal int VendorTemplateId => Data.Vendor!.TemplateId;
        internal float X => Data.Position[0]; internal float Y => Data.Position[1]; internal float Z => Data.Position[2];
        internal float HeadingY => Data.Rotation[1]; internal float HeadingW => Data.Rotation[3];
        internal int Mesh => (int)Data.Meshes[0].Id;
        internal IReadOnlyList<WorldVendorStock> Stock => Data.Vendor!.Stock;
    }
    internal static IReadOnlyList<SocialNpcFixture.Definition> Definitions { get; } = SocialNpcFixture.Definitions.Where(n => n.Binding.PlayfieldId is 4676 or 4677).ToArray();
    internal static IReadOnlyList<Placement> Placements { get; } = Definitions.Select(n => new Placement(n.Content)).ToArray();
    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure) => WorldContentFixtures.Attach(npc, items, catalog, out failure);
}
