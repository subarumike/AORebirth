namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Doja;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

/// <summary>Explicit adapters for accepted Legacy social placements; not a native hash fallback.</summary>
internal static class AcceptedSocialNpcCatalog
{
    internal sealed record Definition(AcceptedNpcBinding Binding, Func<IItemBuilder, NpcCharacter> Create);

    internal static IReadOnlyList<Definition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new Definition(new AcceptedNpcBinding("legacy:ScarlettDalquistSpawn:7010:7A18B924",
            "AORebirth/Server/ZoneEngine/Core/Playfields/ScarlettDalquistSpawn.cs;capture:20260821-222107;template:BART",
            "SimpleChar:7A18B924", 7010, true, false), CreateScarlett)
    }.Concat(AcceptedSubwayMerchantCatalog.Definitions).Concat(AcceptedAreteQuestNpcCatalog.Definitions)
        .Concat(AcceptedAreteVendorCatalog.Definitions).Concat(AcceptedGardenVendorCatalog.Definitions).ToArray());

    static NpcCharacter CreateScarlett(IItemBuilder items)
    {
        var npc = new AcceptedSocialNpcCharacter(new Identity { Type = IdentityType.CanbeAffected,
            Instance = DojaChipInteractionRules.ScarlettInstance }, items)
        {
            Name = DojaChipInteractionRules.ScarlettName,
            SpawnSource = SpawnSource.AcceptedPlacement,
            Position = new AORebirth.Core.Vector.Vector3(104.180695f, 2.185f, 76.13117f),
            Rotation = new AORebirth.Core.Vector.Quaternion(0f, -0.342263281f, 0f, 0.9396041f)
        };
        // BART's explicit appearance seed (SqlTables/mobtemplate.sql), then Scarlett's
        // exact accepted overrides. This is not interpolation of native template levels.
        npc.Stats.Set(CharacterStat.Fatness, 1); npc.Stats.Set(CharacterStat.Breed, 1);
        npc.Stats.Set(CharacterStat.Sex, 2); npc.Stats.Set(CharacterStat.Race, 1);
        npc.Stats.Set(CharacterStat.MonsterData, 26090);
        npc.Stats.Set(CharacterStat.MaxHealth, 16042); npc.Stats.Set(CharacterStat.Health, 16042);
        npc.Stats.Set(CharacterStat.Level, 150); npc.Stats.Set(CharacterStat.VisualFlags, 31);
        npc.Stats.Set(CharacterStat.Flags, 277352961); npc.Stats.Set(CharacterStat.Side, 0);
        npc.Stats.Set(CharacterStat.NPCFamily, 137); npc.Stats.Set(CharacterStat.RunSpeed, 432);
        npc.Stats.Set(CharacterStat.Scale, 117); npc.Stats.Set(CharacterStat.HeadMesh, 223846);
        npc.Stats.Set(CharacterStat.AccountFlags, 0); npc.Stats.Set(CharacterStat.Expansion, 0);
        npc.Stats.Set(CharacterStat.Profession, 0); npc.Stats.Set(CharacterStat.VisualProfession, 0);
        npc.Stats.Set(CharacterStat.CurrentMovementMode, 3); npc.Stats.Set(CharacterStat.PrevMovementMode, 3);
        foreach (var texture in new[] { new AOTextures(0, 213851), new AOTextures(1, 213751),
            new AOTextures(2, 213807), new AOTextures(3, 213708), new AOTextures(4, 213925) }) npc.Textures.Add(texture);
        npc.Meshes.Add(new Mesh { Position = 0, Id = 223846, Layer = 4, OverrideTextureId = 0 });
        npc.Meshes.Add(new Mesh { Position = 1, Id = 258990, Layer = 2, OverrideTextureId = 0 });
        npc.Motor.RefreshFromStats();
        return npc;
    }
}

// Scarlett's compiled Legacy spawn explicitly registers an unresolved passive combat
// contract. Do not give a dialogue placement New's generic unarmed fallback/retaliation.
internal sealed class AcceptedSocialNpcCharacter(Identity identity, IItemBuilder items) : NpcCharacter(identity, items)
{
    public override void Rebase() { }
    public override void RebaseWeapons() { }
    public override void StartFighting(Identity target, byte action) { }
    protected override void TickCombat(double deltaTime) { }
}
