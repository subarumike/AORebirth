namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;

public sealed record NpcEditorLootLiveEdit([property: JsonRequired] int ExpectedRevision, NpcEditorLootRowKey? Row, NpcEditorLootEntry? Entry);

public sealed record NpcEditorLootLiveRevert([property: JsonRequired] int ExpectedRevision, Guid ChangeId);

public sealed record NpcEditorLootLiveChange(Guid ChangeId, string Actor, DateTimeOffset AtUtc, string Action, int RowIndex,
    NpcEditorLootEntry? Before, NpcEditorLootEntry? After, bool Revertible = false);

public sealed record NpcEditorLootLiveView(bool CanApply, int Revision, NpcEditorLootLiveChange[] History);

public sealed partial class NpcEditorLoot
{
    const int MaxLiveChanges = 64;
    static readonly ConditionalWeakTable<NpcCharacter, LiveState> LiveEdits = new();

    public NpcEditorLootView ApplyLive(NpcCharacter npc, NpcEditorLootLiveEdit edit, string actor)
    {
        LiveState state = RequireLiveState(npc, edit.ExpectedRevision, actor);
        (int index, NpcEditorLootEntry? before, IReadOnlyList<MobItemTableEntry> current) = ValidateLiveEdit(npc, edit);
        MobItemTableEntry[]? previousTable = npc.LiveEditorLootTable;
        MobItemTableEntry[] changed = EditRow(current, index, edit.Entry?.ToNative());
        var change = new NpcEditorLootLiveChange(Guid.NewGuid(), actor, DateTimeOffset.UtcNow, "apply", index, before, edit.Entry);
        state.Undo.Add(new(change, previousTable));
        if (state.Undo.Count > MaxLiveChanges)
            state.Undo.RemoveAt(0);
        state.Record(change);
        npc.LiveEditorLootTable = changed;
        return Describe(npc);
    }

    internal NpcEditorLootLiveEdit PrepareLiveAppend(NpcCharacter npc, NpcEditorLootEntry entry)
    {
        NpcEditorLootLiveView current = LiveView(npc);
        if (!current.CanApply)
            throw new NpcEditorRuntimeException(409, "Live loot editing requires a living, non-pet NPC with a resolved template.");
        var edit = new NpcEditorLootLiveEdit(current.Revision, null, entry);
        ValidateLiveEdit(npc, edit);
        return edit;
    }

    (int Index, NpcEditorLootEntry? Before, IReadOnlyList<MobItemTableEntry> Current)
        ValidateLiveEdit(NpcCharacter npc, NpcEditorLootLiveEdit edit)
    {
        IReadOnlyList<MobItemTableEntry> current = npc.ActiveLootTable ?? [];
        int index = edit.Row?.Index ?? current.Count;
        if (edit.Row != null && (index < 0 || index >= current.Count || Key(index, current[index]) != edit.Row))
            throw new NpcEditorLootConflictException("The live loot row changed. Refresh and retry.");

        if (edit.Entry != null && (!NpcEditorLootJournalFile.ValidEntry(edit.Entry) || !HasRdbTemplates(edit.Entry.ItemHash)))
            throw new ArgumentException("Loot rows require a current item hash and valid chance, repeats and QL settings.");
        int originalCount = npc.MobTemplate?.ItemTable.Count ?? 0;
        if (index == current.Count && current.Count >= originalCount + NpcEditorLootJournalFile.MaxRowsPerTemplate)
            throw new InvalidOperationException("The live loot table has reached its added row limit.");
        NpcEditorLootEntry? before = index < current.Count ? NpcEditorLootEntry.FromNative(current[index]) : null;
        if (edit.Entry != null && before == edit.Entry)
            throw new ArgumentException("The selected live loot row already has these values.");

        return (index, before, current);
    }

    public NpcEditorLootView RevertLive(NpcCharacter npc, NpcEditorLootLiveRevert edit, string actor)
    {
        LiveState state = RequireLiveState(npc, edit.ExpectedRevision, actor);
        if (state.Undo.Count == 0 || state.Undo[^1].Change.ChangeId != edit.ChangeId)
            throw new NpcEditorLootConflictException("Undo the latest live loot change first.");

        LiveUndo undo = state.Undo[^1];
        state.Undo.RemoveAt(state.Undo.Count - 1);
        npc.LiveEditorLootTable = undo.PreviousTable;
        state.Record(new(Guid.NewGuid(), actor, DateTimeOffset.UtcNow, "revert", undo.Change.RowIndex,
            undo.Change.After, undo.Change.Before));
        return Describe(npc);
    }

    static LiveState RequireLiveState(NpcCharacter npc, int expectedRevision, string actor)
    {
        if (!CanApplyLive(npc))
            throw new NpcEditorRuntimeException(409, "Live loot editing requires a living, non-pet NPC with a resolved template.");
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new ArgumentException("A valid GM actor is required.", nameof(actor));
        LiveState state = LiveEdits.GetValue(npc, static _ => new());
        if (state.Revision != expectedRevision)
            throw new NpcEditorLootConflictException("The live loot revision changed. Refresh and retry.");
        return state;
    }

    static bool CanApplyLive(NpcCharacter npc) => !npc.IsDead && npc.Pet == null && npc.MobTemplate?.UnresolvedPlaceholder == false;

    static NpcEditorLootLiveView LiveView(NpcCharacter npc)
    {
        bool canApply = CanApplyLive(npc);
        if (!LiveEdits.TryGetValue(npc, out LiveState? state))
            return new(canApply, 0, []);
        Guid latest = state.Undo.Count == 0 ? Guid.Empty : state.Undo[^1].Change.ChangeId;
        // A long chain of undos can make the latest undoable change older than the visible history.
        var history = state.History.Select(row => row with { Revertible = canApply && row.ChangeId == latest }).ToList();
        if (latest != Guid.Empty && history.All(row => row.ChangeId != latest))
            history.Insert(0, state.Undo[^1].Change with { Revertible = canApply });
        return new(canApply, state.Revision, [.. history]);
    }

    sealed class LiveState
    {
        internal int Revision { get; private set; }
        internal List<NpcEditorLootLiveChange> History { get; } = [];
        internal List<LiveUndo> Undo { get; } = [];

        internal void Record(NpcEditorLootLiveChange change)
        {
            History.Add(change);
            if (History.Count > MaxLiveChanges)
                History.RemoveAt(0);
            Revision++;
        }
    }

    sealed record LiveUndo(NpcEditorLootLiveChange Change, MobItemTableEntry[]? PreviousTable);
}
