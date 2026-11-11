namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield.Locality;

internal sealed partial class NpcEditorRuntime
{
    const int MaxHistory = 50;

    sealed class SpawnState
    {
        internal Guid Generation { get; } = Guid.NewGuid();
        internal List<LiveChange> History { get; } = new();
    }

    sealed record LiveChange(string Description, string? Actor, string Before, string After, StatChange? Stat, EquipmentChange? Equipment)
    {
        internal Guid Id { get; } = Guid.NewGuid();
        internal DateTime AtUtc { get; } = DateTime.UtcNow;
        internal bool Reverted { get; set; }
        internal string? RevertedBy { get; set; }
    }

    sealed record StatChange(CharacterStat Stat, int Before, int After, int? DependentBefore, int? DependentAfter);
    internal sealed record EquipmentChange(int Slot, Item? Before, Item? After);

    public Task<NpcEditorNpcDetails> ApplyStatAsync(
        NpcEditorIdentity identity, int statId, int expectedBase, int value, CancellationToken cancellationToken, string? actor = null)
        => WithNpcAsync(identity, npc =>
        {
            var stat = (CharacterStat)statId;
            ApplyStatWithHistory(npc, stat, expectedBase, value, actor);
            return Snapshot(npc);
        }, cancellationToken);

    Guid ApplyStatWithHistory(NpcCharacter npc, CharacterStat stat, int expectedBase, int value, string? actor)
    {
        CharacterStat? dependent = NpcEditorStatPolicy.DependentVital(stat);
        int? before = dependent is { } current ? ReadStat(npc, current, StatDetail.Base) : null;
        ApplyStat(npc, stat, expectedBase, value);
        int? after = dependent is { } changed ? ReadStat(npc, changed, StatDetail.Base) : null;
        var change = new LiveChange($"{stat}: {expectedBase} to {value}", actor,
            DescribeStat(expectedBase, dependent, before), DescribeStat(value, dependent, after),
            new StatChange(stat, expectedBase, value, before, after), null);
        Record(npc, change);
        return change.Id;
    }

    static string DescribeStat(int value, CharacterStat? dependent, int? dependentValue)
        => dependent is { } current
            ? string.Create(CultureInfo.InvariantCulture, $"{value} ({current} {dependentValue})")
            : value.ToString(CultureInfo.InvariantCulture);

    public Task<NpcEditorNpcDetails> ApplyEquipmentAsync(
        NpcEditorIdentity identity, NpcEditorEquipmentEdit edit, CancellationToken cancellationToken, string? actor = null)
        => WithNpcAsync(identity, npc =>
        {
            ApplyEquipmentWithHistory(npc, edit, actor);
            return Snapshot(npc);
        }, cancellationToken);

    Guid ApplyEquipmentWithHistory(NpcCharacter npc, NpcEditorEquipmentEdit edit, string? actor, int? expectedFreeSlot = null)
    {
        EquipmentChange change = EditEquipment(npc, _catalog, npc.Playfield!.GetRequiredService<IItemBuilder>(), edit,
            expectedFreeSlot: expectedFreeSlot);
        string before = DescribeEquipment(change.Before);
        string after = DescribeEquipment(change.After);
        var history = new LiveChange($"Equipment slot {change.Slot}: {before} to {after}", actor, before, after, null, change);
        Record(npc, history);
        return history.Id;
    }

    public Task<NpcEditorNpcDetails> RevertAsync(
        NpcEditorIdentity identity, Guid changeId, CancellationToken cancellationToken, string? actor = null)
        => WithNpcAsync(identity, npc =>
        {
            LiveChange? change = State(npc).History.Find(entry => entry.Id == changeId);
            if (change == null)
                throw new NpcEditorRuntimeException(404, "That change is unavailable for this spawn.");
            if (!CanRevert(npc, change))
                throw new NpcEditorRuntimeException(409, "That change can no longer be reverted. Refresh or revert later changes first.");

            if (change.Stat is { } stat)
                SetStat(npc, stat.Stat, stat.Before, restoring: true, restoreDependent: stat.DependentBefore);
            else if (change.Equipment is { } equipment)
                UndoEquipment(npc, equipment);
            change.Reverted = true;
            change.RevertedBy = actor;
            return Snapshot(npc);
        }, cancellationToken);

    void Record(NpcCharacter npc, LiveChange change)
    {
        List<LiveChange> history = State(npc).History;
        if (history.Count == MaxHistory)
            history.RemoveAt(0);
        history.Add(change);
    }

    bool CanRevert(NpcCharacter npc, LiveChange change)
    {
        if (change.Reverted)
            return false;
        List<LiveChange> history = State(npc).History;
        if (change.Stat is { } stat)
        {
            return ReferenceEquals(history.FindLast(entry => !entry.Reverted && entry.Stat is { } previous
                    && NpcEditorStatPolicy.AreRelated(previous.Stat, stat.Stat)), change)
                && ReadStat(npc, stat.Stat, StatDetail.Base) == stat.After
                && (NpcEditorStatPolicy.DependentVital(stat.Stat) is not { } dependent
                    || ReadStat(npc, dependent, StatDetail.Base) == stat.DependentAfter)
                && PlanLiveStat(npc, stat.Stat, stat.Before, restoring: true, stat.DependentBefore, out _) == null;
        }

        return change.Equipment is { } equipment && CanEditEquipment(npc)
            && ReferenceEquals(history.FindLast(entry => !entry.Reverted && entry.Equipment != null), change)
            && ReferenceEquals(npc.Equipment.Content.GetValueOrDefault(equipment.Slot), equipment.After)
            && equipment.After?.Locked != true
            && (equipment.Before == null || !ArmorPositionTaken(npc, equipment.Slot, equipment.Before));
    }

    static bool CanEditStat(NpcCharacter npc, CharacterStat stat)
        => LiveStatAvailabilityError(npc, stat) == null;

    static string? LiveStatAvailabilityError(NpcCharacter npc, CharacterStat stat)
    {
        CharacterStat? current = NpcEditorStatPolicy.DependentVital(stat);
        if (npc.IsDead || !npc.Attackable || npc.Pet != null || !NpcEditorStatPolicy.Supports(stat)
            || (NpcEditorStatPolicy.IsBehaviorStat(stat) && npc.IsInCombat)
            || (current is { } currentStat && ReadStat(npc, currentStat, StatDetail.Base) == null))
            return "This stat does not support live editing on the selected NPC.";
        if (npc.HasBaseDependentPercentageBonus(stat)
            || (current is { } dependent && npc.HasBaseDependentPercentageBonus(dependent)))
            return "An active percentage modifier depends on this base value. Use a saved edit for future spawns.";
        return null;
    }

    static string? StatValidationError(NpcCharacter npc, CharacterStat stat, int value, bool restoring)
        => PlanLiveStat(npc, stat, value, restoring, null, out _);

    static string? PlanLiveStat(
        NpcCharacter npc, CharacterStat stat, int value, bool restoring, int? restoreDependent, out int? dependentBase)
    {
        dependentBase = null;
        if (LiveStatAvailabilityError(npc, stat) is string unavailable)
            return unavailable;
        if (ReadStat(npc, stat, StatDetail.Base) == null)
            return "This stat does not support live editing on the selected NPC.";

        long effective = (long)value + npc.Stats.GetOrZero(stat, StatDetail.Bonus);
        if (effective < int.MinValue || effective > int.MaxValue || StatCollection.IsUnset(value)
            || StatCollection.IsUnset((int)effective))
            return "The result is outside the stat's supported range.";

        if (NpcEditorStatPolicy.DependentVital(stat) is { } dependent)
        {
            int minimum = dependent == CharacterStat.Health ? 1 : 0;
            int oldMaximum = npc.Stats.GetOrZero(stat);
            int oldCurrent = npc.Stats.GetOrZero(dependent);
            if ((!restoring && !NpcEditorStatPolicy.IsAllowedValue(stat, value))
                || value < minimum || effective < minimum
                || !NpcEditorStatPolicy.ValidVitalPair(oldCurrent, oldMaximum, minimum))
                return "The live maximum and current values must be within the supported vital range.";

            int currentBonus = npc.Stats.GetOrZero(dependent, StatDetail.Bonus);
            long baseValue = restoreDependent ?? (long)NpcEditorStatPolicy.ResizeVital(
                oldCurrent, oldMaximum, (int)effective, minimum) - currentBonus;
            long fullValue = baseValue + currentBonus;
            if (baseValue < 0 || baseValue > int.MaxValue || fullValue < minimum || fullValue > effective
                || StatCollection.IsUnset((int)baseValue) || StatCollection.IsUnset((int)fullValue))
                return "The active current-vital modifiers prevent a safe edit to that maximum.";
            dependentBase = (int)baseValue;
        }
        else if (stat is CharacterStat.Health or CharacterStat.CurrentNano)
        {
            bool health = stat == CharacterStat.Health;
            int max = npc.Stats.GetOrZero(health ? CharacterStat.MaxHealth : CharacterStat.MaxNanoEnergy);
            if (value < 0 || !NpcEditorStatPolicy.ValidVitalPair((int)effective, max, health ? 1 : 0))
                return "The resulting vital must remain within the live maximum and keep the NPC alive.";
        }
        else if (!restoring && !NpcEditorStatPolicy.IsAllowedValue(stat, value))
        {
            return "The stat value is outside the editor's supported range.";
        }
        return null;
    }

    internal static void SetStat(
        NpcCharacter npc, CharacterStat stat, int value, bool restoring = false, int? restoreDependent = null)
    {
        string? error = PlanLiveStat(npc, stat, value, restoring, restoreDependent, out int? dependentBase);
        if (error != null)
            throw new NpcEditorRuntimeException(400, error);

        npc.Stats.Set(stat, value, StatDetail.Base, dirty: true);
        if (dependentBase is { } baseValue && NpcEditorStatPolicy.DependentVital(stat) is { } current)
        {
            npc.Stats.Set(current, baseValue, StatDetail.Base, dirty: true);
            CharacterStat percent = current == CharacterStat.Health
                ? CharacterStat.PercentRemainingHealth : CharacterStat.PercentRemainingNano;
            if (npc.Stats.GetOrZero(stat) == 0)
                npc.Stats.Set(percent, 0, StatDetail.Base, dirty: true);
            else
                npc.SyncVitalPercent(npc.Stats.GetOrZero(current), stat, percent);
        }
        npc.FlushDirtyStats();
    }

    internal static void ApplyStat(NpcCharacter npc, CharacterStat stat, int expectedBase, int value)
    {
        if (ReadStat(npc, stat, StatDetail.Base) != expectedBase)
            throw new NpcEditorRuntimeException(409, "The runtime base value changed. Refresh the selected NPC.");
        if (value == expectedBase)
            throw new NpcEditorRuntimeException(400, "Enter a different base value.");
        SetStat(npc, stat, value);
    }

    static bool CanEditEquipment(NpcCharacter npc)
        => !npc.IsDead && !npc.IsInCombat && npc.Attackable && npc.Pet == null && npc.Shop == null
            && npc.MobTemplate is { UnresolvedPlaceholder: false };

    static ItemClass GetClass(Item item) => (ItemClass)item.GetStat(CharacterStat.ItemClass);
    static int DefaultPosition(Item item) => StatCollection.Normalize(item.GetStat(CharacterStat.DefaultPos));
    static bool IsEditableArmor(Item item) => NpcCharacter.EditableArmorPosition(item) > 0;
    static string DescribeEquipment(Item? item) => item == null
        ? "Empty" : $"{item.Name} ({item.LowId}/{item.HighId}, QL {item.Quality})";

    static bool CanReplaceItem(NpcCharacter npc, int slot, Item item)
        => CanEditEquipment(npc) && !item.Locked && !npc.IsMonsterCombatSlot(slot)
            && (IsEditableArmor(item) || (GetClass(item) == ItemClass.Weapon && ReferenceEquals(npc.VisualRightHand, item)))
            && !NpcCharacter.IsProtectedNpcEquipment(item);

    static bool ArmorPositionTaken(NpcCharacter npc, int slot, Item item)
        => IsEditableArmor(item) && npc.Equipment.EnumerateSlots().Any(pair =>
            pair.Key != slot && GetClass(pair.Value) == ItemClass.Armor && DefaultPosition(pair.Value) == DefaultPosition(item));

    static NpcEditorEquippedItem EquipmentRow(NpcCharacter npc, int slot, Item item)
    {
        string role = npc.IsMonsterCombatSlot(slot) ? "Monster combat weapon"
            : ReferenceEquals(npc.VisualRightHand, item) ? "Visible right hand"
            : NpcCharacter.TryFindEquipMonsterWeaponHash(item, out _) ? "Monster weapon equipper"
            : IsEditableArmor(item) ? "Armor" : "Other";
        bool replaceable = CanReplaceItem(npc, slot, item);
        return new NpcEditorEquippedItem(slot, item.InstanceId, item.LowId, item.HighId, item.Quality, item.Name,
            GetClass(item).ToString(), role, DefaultPosition(item), replaceable, replaceable);
    }

    internal static EquipmentChange EditEquipment(
        NpcCharacter npc, IItemTemplateCatalog catalog, IItemBuilder builder, NpcEditorEquipmentEdit edit,
        bool preview = false, int? expectedFreeSlot = null)
    {
        if (!CanEditEquipment(npc))
            throw new NpcEditorRuntimeException(409, "Live equipment editing requires a living template NPC out of combat, without a pet owner or shop.");

        bool addingArmor = edit.Slot == -1;
        int slot = addingArmor ? npc.Equipment.FindFreeSlot() : edit.Slot;
        if (expectedFreeSlot is int expected && slot != expected)
            throw new NpcEditorRuntimeException(409, "The available armor slot changed. Preview the batch again.");
        if (slot < npc.Equipment.Offset || slot >= npc.Equipment.Offset + npc.Equipment.Capacity)
            throw new NpcEditorRuntimeException(400, "No supported equipment slot is available.");

        Item? previous = npc.Equipment.Content.GetValueOrDefault(slot);
        if (addingArmor ? edit.ExpectedInstanceId != 0 || previous != null
                        : previous == null || previous.InstanceId != edit.ExpectedInstanceId)
            throw new NpcEditorRuntimeException(409, "The equipment slot changed. Refresh the selected NPC.");
        if (previous != null && !CanReplaceItem(npc, slot, previous))
            throw new NpcEditorRuntimeException(400, "This equipment role is read-only.");

        Item? replacement = null;
        if (edit.ItemId is { } id)
        {
            if (!catalog.TryGet(id, out ItemTemplate template) || template.Quality <= 0
                || (edit.Quality.HasValue && edit.Quality != template.Quality))
                throw new NpcEditorRuntimeException(400, "Select an existing item at its native catalog quality.");
            ItemClass requiredClass = previous == null ? ItemClass.Armor : GetClass(previous);
            if ((ItemClass)template.Stats.GetValueOrDefault(CharacterStat.ItemClass) != requiredClass)
                throw new NpcEditorRuntimeException(400, "The item class does not match the equipment role.");
            if (previous?.LowId == id && previous.HighId == id && previous.Quality == template.Quality)
                throw new NpcEditorRuntimeException(400, "Select a different item.");

            Item candidate = builder.Create(id, id, template.Quality, ItemSource.Other);
            if (NpcCharacter.IsProtectedNpcEquipment(candidate))
                throw new NpcEditorRuntimeException(400, "That item changes loot, uploaded nanos, spawn weapons or boss behavior.");
            if (requiredClass == ItemClass.Armor)
            {
                if (!IsEditableArmor(candidate) || (previous != null && DefaultPosition(candidate) != DefaultPosition(previous))
                    || ArmorPositionTaken(npc, slot, candidate))
                    throw new NpcEditorRuntimeException(400, "The item requires an available matching NPC armor position.");
            }
            else if (!NpcCharacter.IsVisualWeapon(candidate)
                || StatCollection.Normalize(candidate.GetStat(CharacterStat.WeaponMesh)) <= 0)
            {
                throw new NpcEditorRuntimeException(400, "Select a visible weapon compatible with the NPC hand.");
            }
            replacement = preview ? candidate : builder.CreateWithNewInstance(id, id, template.Quality, ItemSource.Other);
        }
        else if (previous == null || edit.Quality.HasValue)
        {
            throw new NpcEditorRuntimeException(400, "Select an equipped NPC armor or visual weapon to remove.");
        }

        if (!preview)
            SetEquipmentSlot(npc, slot, previous, replacement);
        return new EquipmentChange(slot, previous, replacement);
    }

    internal static void UndoEquipment(NpcCharacter npc, EquipmentChange equipment)
        => SetEquipmentSlot(npc, equipment.Slot, equipment.After, equipment.Before);

    static void SetEquipmentSlot(NpcCharacter npc, int slot, Item? before, Item? after)
    {
        if (!ReferenceEquals(npc.Equipment.Content.GetValueOrDefault(slot), before)
            || (before != null && !ReferenceEquals(npc.Equipment.Remove(slot), before)))
            throw new NpcEditorRuntimeException(409, "The equipment slot changed before the edit could be applied.");
        if (after != null && !npc.Equipment.Add(slot, after))
        {
            if (before != null)
                npc.Equipment.Add(slot, before);
            throw new NpcEditorRuntimeException(409, "The equipment slot no longer accepts this item.");
        }

        bool weaponChanged = GetClass(before ?? after!) == ItemClass.Weapon;
        npc.RebaseEditedEquipment(weaponChanged);
        if (npc.Playfield?.GetService<PlayfieldLocality>() is not { } locality)
            return;

        if (npc.ConsumeAppearanceDirty())
            locality.Announce(npc, npc.BuildAppearanceUpdateMessage());
        if (weaponChanged)
        {
            foreach (var message in npc.BuildWeaponInstanceMessages())
                locality.Announce(npc, message);
        }
    }
}
