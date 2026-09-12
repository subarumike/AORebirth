namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Linq;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core;
using ZoneEngine_New.Core.Inventory;

/// <summary>
/// Adapter of existing MissionInstanceMobCombat policy, not a generated-profile
/// similarity match. Mission quality owns damage; an exact attached weapon mesh
/// selects the existing five-pistol presentation policy, otherwise the accepted SIW1.
/// </summary>
internal static class MissionNpcCombatPolicy
{
    internal const double AggroRadius = 2.0;
    static readonly int[] Pistols = [121564, 121567, 121568, 121570, 121571];

    internal static CapturedEnemyCombatContract Create(int runtimeIdentity, int level, bool hasGunMesh,
        IItemBuilder items, IItemTemplateCatalog catalog, out Item? weapon)
    {
        if (runtimeIdentity < 1_000_000 || level < 1 || level > 220)
            throw new ArgumentOutOfRangeException(nameof(level));
        weapon = null;
        int minimum = Math.Max(2, level), maximum = Math.Max(4, level + level / 2 + 2);
        if (hasGunMesh)
        {
            for (int offset = 0; offset < Pistols.Length; offset++)
            {
                int templateId = Pistols[(int)(Math.Abs((long)runtimeIdentity + offset) % Pistols.Length)];
                if (!catalog.TryGet(templateId, out _)) continue;
                var item = items.Create(templateId, templateId, Math.Min(level, 23), ItemSource.Other,
                    identity: new Identity { Type = IdentityType.WeaponPage, Instance = 6 });
                if (item.Quality <= 0 || item.LowId != templateId || item.HighId != templateId)
                    throw new InvalidOperationException("The mission pistol builder changed its accepted template/quality.");
                var definition = CreateWeapon(runtimeIdentity, templateId, item.Quality);
                var contract = CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                    "mission-instance-gun-equipped-20260725-185432", runtimeIdentity,
                    templateId, templateId, item.Quality, 6, false, minimum, maximum, 0, 8.0,
                    0.25, 0, 0.5, 2.0, true, true, -1, 0, 30, 30, 30, 30, 0, 3, 0, 0, 0, 0)
                    .WithCapturedWeapon(definition);
                if (!contract.IsCombatReady) throw new InvalidOperationException(contract.QuarantineReason);
                weapon = item;
                return contract;
            }
        }

        // The Legacy policy explicitly selects this context when no approved gun
        // can be equipped. It is not a generic fallback for unresolved world mobs.
        return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
            "mission-instance-mob-siw1-20260725-185432", runtimeIdentity, NpcAiProfile.Aggressive,
            minimum, maximum, 2.0,
            [new CapturedEnemySpecialAttackDefinition(0x023566, 0x023567, 0x53495731, "SIW1")],
            0, 20, 20, 20, 20, 0, 0, 0, -1, 0, 0, 3, 0x53495731, 0, false,
            [minimum, maximum], [0.0], [0.25], [2.0], 0, false, 8.0, true);
    }

    internal static CapturedEnemyWeaponDefinition CreateWeapon(int source, int templateId, int quality)
        => new("mission-trash-pistol-" + templateId, source, 0, 0x0b, 6, 1000015, 0, 0x0106,
        [
            new(CharacterStat.Flags, templateId == 121570 ? 67110401u : 67109889u),
            new(CharacterStat.StaticInstance, (uint)templateId), new(CharacterStat.ACGItemLevel, (uint)quality),
            new(CharacterStat.ACGItemTemplateID, (uint)templateId), new(CharacterStat.ACGItemTemplateID2, (uint)templateId),
            new(CharacterStat.MultipleCount, 1), new(CharacterStat.Energy, unchecked((uint)-1)),
            new(CharacterStat.AttackDelay, 235), new(CharacterStat.RechargeDelay, 235)
        ], 0);
}
