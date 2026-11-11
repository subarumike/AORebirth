namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;

using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

/// <summary>Authorizes only plain NPC armor and visual weapons. Monster weapon equippers remain authored rows.</summary>
internal static class NpcEditorEquipmentPolicy
{
    internal readonly record struct Role(ItemClass Class, int Position)
    {
        internal bool Editable => Class is ItemClass.Armor or ItemClass.Weapon;
        internal string Description => Class switch
        {
            ItemClass.Armor => "Armor",
            ItemClass.Weapon => "Visual weapon",
            _ => "Read-only equipment"
        };
    }

    internal static (int LowId, int HighId) ReadIds(IReadOnlyList<int>? pair)
    {
        if (pair == null || pair.Count == 0)
            return (0, 0);
        return (pair[0], pair.Count > 1 && pair[1] > 0 ? pair[1] : pair[0]);
    }

    internal static (int LowId, int HighId) ReadIds(NpcLevelBand band, NpcEditorEquipmentEntry entry)
        => entry.ItemId is int item ? (item, item) : ReadIds(band.Equipment![entry.SourceIndex!.Value]);

    internal static Role Classify(IItemTemplateCatalog catalog, int lowId, int highId)
    {
        if (!catalog.TryGet(lowId, out ItemTemplate low) || !catalog.TryGet(highId, out ItemTemplate high))
            return default;
        Role lowRole = Classify(low);
        return lowRole == Classify(high) ? lowRole : default;
    }

    static Role Classify(ItemTemplate template)
    {
        if (template.Quality <= 0)
            return default;
        var item = new Item { Definition = template };
        if (NpcCharacter.IsProtectedNpcEquipment(item))
            return default;
        int position = NpcCharacter.EditableArmorPosition(item);
        if (position > 0)
            return new Role(ItemClass.Armor, position);
        if ((ItemClass)item.GetStat(CharacterStat.ItemClass) == ItemClass.Weapon && NpcCharacter.IsVisualWeapon(item)
            && StatCollection.Normalize(item.GetStat(CharacterStat.WeaponMesh)) > 0)
            return new Role(ItemClass.Weapon, 0);
        return default;
    }

    internal static bool TryResolve(
        NpcLevelBand band, NpcEditorEquipmentEntry[] entries, IItemTemplateCatalog catalog,
        out List<List<int>> pairs, out string? error)
    {
        pairs = [];
        error = null;
        if (!NpcEditorEquipmentJournalFile.ValidEntries(band, entries))
        {
            error = "The equipment entries do not match the authored band.";
            return false;
        }

        IReadOnlyList<List<int>> authoredPairs = band.Equipment ?? [];
        var sourceKept = new bool[authoredPairs.Count];
        var armorCount = new Dictionary<int, int>();
        var editedArmor = new HashSet<int>();
        int expandedCount = 0;
        foreach (NpcEditorEquipmentEntry entry in entries)
        {
            (int lowId, int highId) = ReadIds(band, entry);
            if (entry.SourceIndex is int source)
            {
                sourceKept[source] = true;
                if (entry.ItemId == null)
                {
                    List<int>? original = authoredPairs[source];
                    pairs.Add(original == null ? [] : new List<int>(original));
                    expandedCount += CountEquipmentSlots(catalog, lowId, highId);
                    CountArmor(catalog, lowId, highId, armorCount);
                    continue;
                }
            }

            Role next = Classify(catalog, lowId, highId);
            Role originalRole = entry.SourceIndex is int index
                ? RoleForAuthored(catalog, band, index)
                : new Role(ItemClass.Armor, next.Position);
            if (!next.Editable || next != originalRole)
            {
                error = "Use a plain visible weapon or armor for the same supported NPC position.";
                return false;
            }

            pairs.Add([lowId, highId]);
            expandedCount++;
            if (next.Class == ItemClass.Armor)
            {
                editedArmor.Add(next.Position);
                armorCount[next.Position] = armorCount.GetValueOrDefault(next.Position) + 1;
            }
        }

        for (int index = 0; index < sourceKept.Length; index++)
        {
            if (!sourceKept[index] && !RoleForAuthored(catalog, band, index).Editable)
            {
                error = "A read-only equipment entry, monster weapon or spawn effect cannot be removed.";
                return false;
            }
        }

        foreach (int position in editedArmor)
        {
            if (armorCount[position] > 1)
            {
                error = "An armor entry already occupies that NPC position.";
                return false;
            }
        }
        if (expandedCount > NpcCharacter.EquipmentCapacity)
        {
            error = "This configuration would exceed NPC equipment capacity after monster weapons are expanded.";
            return false;
        }
        return true;
    }

    static Role RoleForAuthored(IItemTemplateCatalog catalog, NpcLevelBand band, int index)
    {
        (int lowId, int highId) = ReadIds(band.Equipment![index]);
        return Classify(catalog, lowId, highId);
    }

    static int CountEquipmentSlots(IItemTemplateCatalog catalog, int lowId, int highId)
    {
        if (lowId <= 0)
            return 0;
        return HasEquipper(catalog, lowId) || HasEquipper(catalog, highId) ? 2 : 1;
    }

    static bool HasEquipper(IItemTemplateCatalog catalog, int id)
        => catalog.TryGet(id, out ItemTemplate item)
            && NpcCharacter.TryFindEquipMonsterWeaponHash(new Item { Definition = item }, out _);

    static void CountArmor(IItemTemplateCatalog catalog, int lowId, int highId, Dictionary<int, int> counts)
    {
        int first = Position(catalog, lowId);
        int second = Position(catalog, highId);
        if (first > 0)
            counts[first] = counts.GetValueOrDefault(first) + 1;
        if (second > 0 && second != first)
            counts[second] = counts.GetValueOrDefault(second) + 1;
    }

    static int Position(IItemTemplateCatalog catalog, int id)
        => catalog.TryGet(id, out ItemTemplate item) ? NpcCharacter.EditableArmorPosition(new Item { Definition = item }) : 0;
}
