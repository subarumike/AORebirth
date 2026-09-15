namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Linq;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core;
using ZoneEngine_New.Core.Inventory;

/// <summary>
/// Generic procedural mission damage and packet mechanics. Weapon and attack
/// definitions are selected exclusively from the loaded mission content.
/// </summary>
internal static class MissionNpcCombatPolicy
{
    internal static CapturedEnemyCombatContract Create(int runtimeIdentity, int level, bool hasGunMesh,
        IItemBuilder items, IItemTemplateCatalog catalog, out Item? weapon, MissionNpcContent? content = null)
    {
        content ??= MissionNpcContent.Load();
        if (runtimeIdentity < 1_000_000 || level < 1 || level > 220)
            throw new ArgumentOutOfRangeException(nameof(level));
        weapon = null;
        int minimum = Math.Max(2, level), maximum = Math.Max(4, level + level / 2 + 2);
        if (hasGunMesh)
        {
            for (int offset = 0; offset < content.Weapons.Length; offset++)
            {
                var selected = content.Weapons[(int)(Math.Abs((long)runtimeIdentity + offset) % content.Weapons.Length)];
                if (!catalog.TryGet(selected.LowId, out _) || !catalog.TryGet(selected.HighId, out _)) continue;
                var attack = content.Ranged;
                var item = items.Create(selected.LowId, selected.HighId, Math.Min(level, selected.MaximumQuality), ItemSource.Other,
                    identity: new Identity { Type = IdentityType.WeaponPage, Instance = attack.Slot });
                if (item.Quality <= 0 || item.LowId != selected.LowId || item.HighId != selected.HighId)
                    throw new InvalidOperationException("The mission weapon builder changed its configured template/quality.");
                var definition = CreateWeapon(selected, item.Quality, attack.Slot);
                var contract = CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                    attack.Id, 0,
                    selected.LowId, selected.HighId, item.Quality, attack.Slot, false, minimum, maximum, 0, attack.Range,
                    attack.StartDelay, 0, attack.FirstHitDelay, attack.Interval, true, true, attack.Ammo, 0,
                    attack.SpecialWeaponUnknown, attack.Initiative, attack.Initiative, attack.Initiative, 0, attack.HitType, 0, 0, 0, 0)
                    .WithCapturedWeapon(definition);
                if (!contract.IsRuntimeReady) throw new InvalidOperationException("Mission combat packet data is incomplete.");
                weapon = item;
                return contract;
            }
        }

        // This procedural mission profile is explicit content, never a fallback
        // for an unresolved world template or placeholder hash.
        var melee = content.Melee;
        return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
            melee.Id, 0, NpcAiProfile.Aggressive,
            minimum, maximum, melee.Interval,
            [new CapturedEnemySpecialAttackDefinition(melee.SpecialLowId, melee.SpecialHighId, melee.WeaponInstance, melee.SpecialName)],
            0, melee.SpecialWeaponUnknown, melee.Initiative, melee.Initiative, melee.Initiative, 0, 0, 0,
            melee.Ammo, melee.Slot, 0, melee.HitType, melee.WeaponInstance, 0, false,
            [minimum, maximum], [melee.StartDelay], [melee.FirstHitDelay], [melee.Interval], 0, false, melee.Range, true);
    }

    internal static CapturedEnemyWeaponDefinition CreateWeapon(MissionWeaponContent weapon, int quality, int slot)
    {
        var stats = weapon.Stats.ToDictionary(pair => pair.Key, pair => pair.Value);
        stats[CharacterStat.StaticInstance] = (uint)weapon.LowId;
        stats[CharacterStat.ACGItemLevel] = (uint)quality;
        stats[CharacterStat.ACGItemTemplateID] = (uint)weapon.LowId;
        stats[CharacterStat.ACGItemTemplateID2] = (uint)weapon.HighId;
        return new(string.Empty, 0, 0, 0x0b, slot, 1000015, 0, checked((short)(0x0100 | (slot & 0xff))),
            new[] { CharacterStat.Flags, CharacterStat.StaticInstance, CharacterStat.ACGItemLevel,
                CharacterStat.ACGItemTemplateID, CharacterStat.ACGItemTemplateID2, CharacterStat.MultipleCount,
                CharacterStat.Energy, CharacterStat.AttackDelay, CharacterStat.RechargeDelay }
                .Select(stat => new CapturedEnemyWeaponStatDefinition(stat, stats[stat])).ToArray(), 0);
    }
}
