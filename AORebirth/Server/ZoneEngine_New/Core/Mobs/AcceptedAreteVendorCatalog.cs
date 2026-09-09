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

/// <summary>Accepted Arete actors and complete frozen stock. Standalone machines are never fabricated NPCs.</summary>
internal static class AcceptedAreteVendorCatalog
{
    internal sealed record StandaloneDefinition(string PlacementIdentity, string SourceIdentity,
        int PlayfieldId, CapturedAreteAlexAreaVendorDefinition Content)
    {
        internal int SourceVendorInstance => Content.SourceVendorInstance;
    }

    internal static IReadOnlyList<StandaloneDefinition> StandaloneDefinitions { get; } = Array.AsReadOnly(
        CapturedAreteAlexAreaVendorContentProvider.Vendors.Select(content => new StandaloneDefinition(
            "legacy:CapturedAreteAlexAreaVendorContentProvider:6553:" + content.SourceVendorInstance.ToString("X8"),
            "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteAlexAreaVendorContentProvider.cs;"
                + "capture:20260720-074847+20260721-lockpick;sourceVendor:" + content.SourceVendorInstance.ToString("X8"),
            CapturedAreteAlexAreaVendorContentProvider.AreteLandingPlayfieldId, content)).ToArray());

    internal static IReadOnlyList<AcceptedSocialNpcCatalog.Definition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        NpcDefinition(CapturedAreteMarcoSpidaVendorContentProvider.SourceNpcInstance,
            CapturedAreteMarcoSpidaVendorContentProvider.Evidence, CreateMarco),
        NpcDefinition(CapturedAreteLoreleiVendorContentProvider.SourceNpcInstance,
            CapturedAreteLoreleiVendorContentProvider.Evidence, CreateLorelei)
    });

    static AcceptedSocialNpcCatalog.Definition NpcDefinition(int source, string evidence, Func<IItemBuilder, NpcCharacter> create)
        => new(new AcceptedNpcBinding("legacy:AreteLandingSpawn:6553:" + source.ToString("X8"),
            "AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs;" + evidence + ";source:" + source.ToString("X8"),
            "SimpleChar:" + source.ToString("X8"), 6553, true, true), create);

    static NpcCharacter CreateMarco(IItemBuilder items)
    {
        var npc = Create(items, CapturedAreteMarcoSpidaVendorContentProvider.SourceNpcInstance,
            CapturedAreteMarcoSpidaVendorContentProvider.DisplayName, 1576, 26092, 95, 40694, 34, 1, 2,
            new(3407.67676f, 9.01f, 831.262451f), new(0f, -0.02306845f, 0f, 0.9997331f));
        AddAppearance(npc, [(0, 0), (1, 247966), (2, 9619), (3, 247920), (4, 9626)], [(0, 40694, 4)]);
        return npc;
    }

    static NpcCharacter CreateLorelei(IItemBuilder items)
    {
        var npc = Create(items, CapturedAreteLoreleiVendorContentProvider.SourceNpcInstance,
            CapturedAreteLoreleiVendorContentProvider.DisplayName, 1864, 26137, 100, 40209, 35, 2, 3,
            new(3369.1416f, 17.315f, 794.4232f), new(0f, 0f, 0f, 1f));
        AddAppearance(npc, [(0, 0), (1, 30862), (2, 40903), (3, 30839), (4, 30886)], [(0, 40209, 4), (1, 7777, 2)]);
        return npc;
    }

    static AcceptedAreteVendorCharacter Create(IItemBuilder items, int source, string name, int appearance, int monster,
        int scale, int head, int speed, int breed, int gender, AORebirth.Core.Vector.Vector3 position, AORebirth.Core.Vector.Quaternion rotation)
    {
        var npc = new AcceptedAreteVendorCharacter(source, appearance, items)
        { Name = name, Position = position, Rotation = rotation, SpawnSource = SpawnSource.AcceptedPlacement };
        // These are complete explicit AreteLandingSpawn rows, not BART/level interpolation.
        foreach (var stat in new (CharacterStat Id, int Value)[]
        {
            (CharacterStat.Level, 10), (CharacterStat.MaxHealth, 227), (CharacterStat.Health, 227),
            (CharacterStat.MonsterData, monster), (CharacterStat.Scale, scale), (CharacterStat.HeadMesh, head),
            (CharacterStat.RunSpeed, speed), (CharacterStat.Flags, 279450113), (CharacterStat.VisualFlags, 31),
            (CharacterStat.NPCFamily, 0), ((CharacterStat)466, 0), (CharacterStat.Side, 0),
            (CharacterStat.Breed, breed), (CharacterStat.Sex, gender), (CharacterStat.Race, 1), (CharacterStat.Fatness, 1),
            (CharacterStat.AccountFlags, 0), (CharacterStat.Expansion, 0), (CharacterStat.Profession, 0),
            (CharacterStat.VisualProfession, 0), (CharacterStat.CurrentMovementMode, 3), (CharacterStat.PrevMovementMode, 3)
        }) npc.Stats.Set(stat.Id, stat.Value);
        npc.Motor.RefreshFromStats(); return npc;
    }

    static void AddAppearance(NpcCharacter npc, (int Place, int Id)[] textures, (int Position, int Id, int Layer)[] meshes)
    {
        foreach (var texture in textures) npc.Textures.Add(new AOTextures(texture.Place, texture.Id));
        foreach (var mesh in meshes) npc.Meshes.Add(new Mesh
        { Position = (byte)mesh.Position, Id = (uint)mesh.Id, Layer = (byte)mesh.Layer, OverrideTextureId = 0 });
    }

    internal static bool TryAttachShop(NpcCharacter npc, IItemBuilder items, IItemTemplateCatalog catalog, out string failure)
    {
        failure = string.Empty;
        if (npc is not AcceptedAreteVendorCharacter || npc.IsDead || npc.Shop != null)
        { failure = "Not a current unbound accepted Arete merchant."; return false; }
        int source = npc.Identity.Instance;
        int vendor, template; IReadOnlyList<CapturedAreteAlexAreaVendorStockDefinition> stock;
        if (source == CapturedAreteMarcoSpidaVendorContentProvider.SourceNpcInstance)
        {
            vendor = CapturedAreteMarcoSpidaVendorContentProvider.SourceVendorInstance;
            template = CapturedAreteMarcoSpidaVendorContentProvider.CaptureVendorTemplateId;
            stock = CapturedAreteMarcoSpidaVendorContentProvider.Stock;
        }
        else if (source == CapturedAreteLoreleiVendorContentProvider.SourceNpcInstance)
        {
            vendor = CapturedAreteLoreleiVendorContentProvider.SourceVendorInstance;
            template = CapturedAreteLoreleiVendorContentProvider.CaptureVendorTemplateId;
            stock = CapturedAreteLoreleiVendorContentProvider.Stock;
        }
        else { failure = "Unsupported accepted merchant source."; return false; }
        if (!TryCreateShop(vendor, template, stock, catalog, out var machine, out failure)) return false;
        machine.Playfield = npc.Playfield; machine.Position = npc.Position; machine.Rotation = npc.Rotation;
        npc.AttachShop(machine); npc.Stats.Set(CharacterStat.Flags, 279450113);
        return true;
    }

    internal static bool TryCreateStandaloneShop(StandaloneDefinition definition, ZoneEngine_New.Core.Playfield.Playfield playfield,
        IItemTemplateCatalog catalog, out VendingMachine machine, out string failure)
    {
        machine = null!; failure = string.Empty;
        if (!StandaloneDefinitions.Any(value => ReferenceEquals(value, definition)) || playfield.Identity.Instance != definition.PlayfieldId)
        { failure = "Standalone shop requires its exact accepted definition and playfield."; return false; }
        var content = definition.Content;
        if (!TryCreateShop(content.SourceVendorInstance, content.TemplateId, content.Stock, catalog, out machine, out failure)) return false;
        machine.Playfield = playfield;
        machine.Position = new(content.X, content.Y, content.Z);
        machine.Rotation = new(content.HeadingX, content.HeadingY, content.HeadingZ, content.HeadingW);
        return true;
    }

    static bool TryCreateShop(int vendor, int template, IReadOnlyList<CapturedAreteAlexAreaVendorStockDefinition> stock,
        IItemTemplateCatalog catalog, out VendingMachine machine, out string failure)
    {
        machine = null!; failure = string.Empty;
        if (!catalog.TryGet(template, out var vendorTemplate))
        { failure = "Missing exact captured vendor template " + template; return false; }
        if (stock.Count == 0) { failure = "Accepted shop stock is empty."; return false; }
        for (int index = 0; index < stock.Count; index++)
        {
            var row = stock[index];
            if (row.Slot != index || row.Quality <= 0 || !catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _))
            { failure = "Missing or noncontiguous captured stock at slot " + row.Slot; return false; }
        }
        machine = new(new() { Type = IdentityType.VendingMachine, Instance = vendor }, vendorTemplate);
        machine.Stock.SetAcceptedSnapshot(stock.Select(row => new ShopStockSlot(row.LowId, row.HighId, row.Quality)).ToArray());
        return true;
    }

    internal sealed class AcceptedAreteVendorCharacter(int source, int appearance, IItemBuilder items)
        : NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = source }, items)
    {
        public override void Rebase() { }
        public override void RebaseWeapons() { }
        public override void StartFighting(Identity target, byte action) { }
        protected override void TickCombat(double deltaTime) { }
        public override SimpleCharFullUpdateMessage BuildSpawnMessage()
        {
            var result = base.BuildSpawnMessage(); result.Appearance.Value = (uint)appearance;
            result.CharacterInfo = new SimpleNpcInfo { Family = 0, LosHeight = 0 };
            return result;
        }
    }
}
