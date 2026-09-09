namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

/// <summary>Exact accepted Stan/Sarah actor adapters, not a name-to-template resolver.</summary>
internal static class AcceptedAreteQuestNpcCatalog
{
    internal static IReadOnlyList<AcceptedSocialNpcCatalog.Definition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        Definition(0x78E0FC65, CreateStan), Definition(0x78E0FC69, CreateSarah)
    });

    static AcceptedSocialNpcCatalog.Definition Definition(int source, Func<IItemBuilder, NpcCharacter> create)
        => new(new AcceptedNpcBinding("legacy:AreteLandingSpawn:6553:" + source.ToString("X8"),
            "AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs;capture:20260720-goldman;source:"
                + source.ToString("X8") + ";template:BART",
            "SimpleChar:" + source.ToString("X8"), 6553, true, false), create);

    static NpcCharacter CreateStan(IItemBuilder items)
    {
        var npc = Create(items, 0x78E0FC65, "Stan Goodman", 26084, 110, 40689, 69, 277352961, 2,
            new(3463.06055f, 9.01f, 880.1275f), new(0f, -0.00337376748f, 0f, 0.999994338f));
        AddAppearance(npc, new[] { (0, 0), (1, 22586), (2, 9615), (3, 22557), (4, 22645) },
            new[] { (0, 45777, 0), (0, 40689, 4), (1, 258990, 2) });
        return npc;
    }

    static NpcCharacter CreateSarah(IItemBuilder items)
    {
        var npc = Create(items, 0x78E0FC69, "Sarah Greene", 295889, 99, 40618, 72, 279450113, 3,
            new(3471.26025f, 9.01f, 840.8831f), new(0f, -0.7743728f, 0f, 0.6327316f));
        AddAppearance(npc, new[] { (0, 164946), (1, 164943), (2, 164945), (3, 164944), (4, 164948) },
            new[] { (0, 204942, 0), (0, 40618, 4), (1, 99152, 2), (5, 291500, 0) });
        return npc;
    }

    static NpcCharacter Create(IItemBuilder items, int source, string name, int monsterData, int scale, int head,
        int speed, int flags, int gender, AORebirth.Core.Vector.Vector3 position, AORebirth.Core.Vector.Quaternion rotation)
    {
        var npc = new AcceptedSocialNpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = source }, items)
        { Name = name, SpawnSource = SpawnSource.AcceptedPlacement, Position = position, Rotation = rotation };
        // Every field below is an explicit AreteLandingSpawn override for these two
        // level-20 variants. Their Social policy cannot retaliate or proximity-aggro.
        foreach (var stat in new (CharacterStat Id, int Value)[]
        {
            (CharacterStat.Level, 20), (CharacterStat.MaxHealth, 559), (CharacterStat.Health, 559),
            (CharacterStat.MonsterData, monsterData), (CharacterStat.Scale, scale), (CharacterStat.HeadMesh, head),
            (CharacterStat.RunSpeed, speed), (CharacterStat.Flags, flags), (CharacterStat.VisualFlags, 31),
            (CharacterStat.NPCFamily, 137), ((CharacterStat)466, 0), (CharacterStat.Side, 0),
            (CharacterStat.Breed, 1), (CharacterStat.Sex, gender), (CharacterStat.Race, 1), (CharacterStat.Fatness, 1),
            (CharacterStat.AccountFlags, 0), (CharacterStat.Expansion, 0), (CharacterStat.Profession, 0),
            (CharacterStat.VisualProfession, 0), (CharacterStat.CurrentMovementMode, 3), (CharacterStat.PrevMovementMode, 3)
        }) npc.Stats.Set(stat.Id, stat.Value);
        npc.Motor.RefreshFromStats();
        return npc;
    }

    static void AddAppearance(NpcCharacter npc, (int Place, int Id)[] textures, (int Position, int Id, int Layer)[] meshes)
    {
        foreach (var texture in textures) npc.Textures.Add(new AOTextures(texture.Place, texture.Id));
        foreach (var mesh in meshes) npc.Meshes.Add(new Mesh
        { Position = (byte)mesh.Position, Id = (uint)mesh.Id, OverrideTextureId = 0, Layer = (byte)mesh.Layer });
    }
}
