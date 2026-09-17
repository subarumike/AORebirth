namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Dialogue;

/// <summary>Immutable graph position; transport and durable actions belong to DialogueService.</summary>
public sealed record DialogueCursor(string NpcIdentity, string CurrentNodeId, bool IsActive = true);

public sealed record DialogueStep(DialogueCursor? Session, DialogueNode? CurrentNode,
    IReadOnlyList<DialogueOption> AvailableOptions, bool IsValid)
{
    internal static readonly DialogueStep Rejected = new(null, null, Array.Empty<DialogueOption>(), false);
}

/// <summary>Plans dialogue transitions without applying actions or mutating the input cursor.</summary>
public sealed class DialogueGraph(DialogueContentRegistry registry)
{
    public DialogueStep StartSessionAtNode(string npcIdentity, string? requestedNode)
    {
        if (!registry.TryGetNpc(npcIdentity, out var npc) || !SupportedNpc(npc)) return DialogueStep.Rejected;
        string nodeId = string.IsNullOrWhiteSpace(requestedNode) ? npc.RootNodeId : requestedNode;
        return Enter(npc, new DialogueCursor(npc.NpcIdentity, nodeId));
    }

    public DialogueStep SelectOption(DialogueCursor cursor, int answer)
    {
        if (!cursor.IsActive || !registry.TryGetNpc(cursor.NpcIdentity, out var npc) || !SupportedNpc(npc))
            return DialogueStep.Rejected;
        var current = Find(npc, cursor.CurrentNodeId);
        if (current == null) return DialogueStep.Rejected;
        var matches = Choices(current).Where(option => option.Index == answer).Take(2).ToArray();
        if (matches.Length != 1 || !SupportedActions(matches[0].Actions)) return DialogueStep.Rejected;
        var option = matches[0];
        if (EqualsToken(option.NextNodeId, "close") || EqualsToken(option.NextNodeId, "end")
            || (option.Actions?.Any(action => EqualsToken(action.Type, "EndDialogue")) ?? false))
            return new(cursor with { IsActive = false }, current, Array.Empty<DialogueOption>(), true);
        string destination = EqualsToken(option.NextNodeId, "root") ? npc.RootNodeId
            : EqualsToken(option.NextNodeId, "self") ? current.Id : option.NextNodeId;
        return Enter(npc, cursor with { CurrentNodeId = destination });
    }

    static DialogueStep Enter(DialogueNpcEntry npc, DialogueCursor cursor)
    {
        var node = Find(npc, cursor.CurrentNodeId);
        if (node == null || !SupportedActions(node.EnterActions)) return DialogueStep.Rejected;
        return new(cursor with { CurrentNodeId = node.Id }, node, Choices(node).ToArray(), true);
    }

    static DialogueNode? Find(DialogueNpcEntry npc, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;
        var matches = (npc.Nodes ?? Array.Empty<DialogueNode>())
            .Where(node => node != null && EqualsToken(node.Id, nodeId)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    static IEnumerable<DialogueOption> Choices(DialogueNode node)
        => (node.Options ?? Array.Empty<DialogueOption>()).Where(option => option != null
            && (option.Conditions ?? Array.Empty<DialogueCondition>()).All(condition => condition != null
                && (EqualsToken(condition.Type, "alwaysTrueTest") || EqualsToken(condition.Type, "testAlwaysTrue"))));

    // Graph actions are limited to ending a conversation. Real effects remain in the validated action router.
    static bool SupportedActions(IEnumerable<DialogueAction>? actions)
        => actions == null || actions.All(action => action != null && EqualsToken(action.Type, "EndDialogue"));

    // NPC-level executable metadata has no supported handler. Current packs leave these collections empty.
    static bool SupportedNpc(DialogueNpcEntry npc)
        => (npc.Conditions?.Count ?? 0) == 0 && (npc.Actions?.Count ?? 0) == 0;

    static bool EqualsToken(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
