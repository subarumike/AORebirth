using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete.Dialogue
{
    public static class DialogueContentPackValidator
    {
        public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> packs)
        {
            var result = new AreteValidationResult();
            var packNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var npcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pack in packs ?? Enumerable.Empty<DialogueContentPack>())
            {
                if (pack == null) { result.AddError("dialogue", "content pack is null"); continue; }
                string id = pack.Identity?.Id;
                if (string.IsNullOrWhiteSpace(id)) result.AddError("dialogue", "missing dialogue content pack id");
                else if (!packNames.Add(id)) result.AddError(id, "duplicate dialogue content pack id");
                foreach (var npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    if (npc == null) { result.AddError(id, "npc entry is null"); continue; }
                    if (string.IsNullOrWhiteSpace(npc.NpcIdentity)) result.AddError(id, "missing NPC identity");
                    else if (!npcNames.Add(npc.NpcIdentity)) result.AddError(npc.NpcIdentity, "duplicate NPC identity");
                    ValidateGraph(npc, result);
                }
            }
            return result;
        }

        static void ValidateGraph(DialogueNpcEntry npc, AreteValidationResult errors)
        {
            var nodes = (npc.Nodes ?? Enumerable.Empty<DialogueNode>()).ToArray();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes)
            {
                if (node == null) { errors.AddError(npc.NpcIdentity, "dialogue node is null"); continue; }
                if (string.IsNullOrWhiteSpace(node.Id)) errors.AddError(npc.NpcIdentity, "missing dialogue node id");
                else if (!names.Add(node.Id)) errors.AddError(npc.NpcIdentity, "duplicate dialogue node id '" + node.Id + "'");
            }
            if (!string.IsNullOrWhiteSpace(npc.RootNodeId) && !names.Contains(npc.RootNodeId))
                errors.AddError(npc.NpcIdentity, "root dialogue node target was not found");
            foreach (var option in nodes.Where(node => node != null)
                .SelectMany(node => node.Options ?? Enumerable.Empty<DialogueOption>()))
            {
                if (option == null) { errors.AddError(npc.NpcIdentity, "dialogue option is null"); continue; }
                string target = option.NextNodeId;
                if (string.IsNullOrWhiteSpace(target))
                {
                    if (!(option.Actions ?? Enumerable.Empty<DialogueAction>()).Any(action => action != null && IsClose(action.Type)))
                        errors.AddError(npc.NpcIdentity, "missing dialogue node target");
                }
                else if (!IsControlTarget(target) && !names.Contains(target))
                    errors.AddError(npc.NpcIdentity, "dialogue node target '" + target + "' was not found");
            }
        }

        static bool IsClose(string action) => string.Equals(action, "closeDialogue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "endDialogue", StringComparison.OrdinalIgnoreCase);

        static bool IsControlTarget(string target)
        {
            switch (target.ToLowerInvariant())
            {
                case "close": case "end": case "parent": case "root": case "self": return true;
                default: return false;
            }
        }
    }

    public static class DialogueActionReferenceValidator
    {
        public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> packs, QuestContentRegistry quests)
        {
            var errors = new AreteValidationResult();
            foreach (var pack in packs ?? Enumerable.Empty<DialogueContentPack>())
            {
                if (pack == null) { errors.AddError("dialogue", "content pack is null"); continue; }
                foreach (var npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    if (npc == null) { errors.AddError(pack.Identity?.Id, "npc entry is null"); continue; }
                    var actions = new List<DialogueAction>(npc.Actions ?? Enumerable.Empty<DialogueAction>());
                    foreach (var node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
                    {
                        if (node == null) { errors.AddError(npc.NpcIdentity, "dialogue node is null"); continue; }
                        actions.AddRange(node.EnterActions ?? Enumerable.Empty<DialogueAction>());
                        foreach (var option in node.Options ?? Enumerable.Empty<DialogueOption>())
                        {
                            if (option == null) errors.AddError(npc.NpcIdentity, "dialogue option is null");
                            else actions.AddRange(option.Actions ?? Enumerable.Empty<DialogueAction>());
                        }
                    }
                    foreach (var action in actions)
                    {
                        if (action == null) { errors.AddError(npc.NpcIdentity, "dialogue action is null"); continue; }
                        switch (action.Type?.ToLowerInvariant())
                        {
                            case "enddialogue": break;
                            case "offermission": case "acceptmission": case "completemission":
                            case "failmission": case "abandonmission":
                                ReferenceChecks.Quest(action.QuestId, quests, npc.NpcIdentity, "dialogue action", errors);
                                break;
                            default:
                                errors.AddError(npc.NpcIdentity, string.IsNullOrWhiteSpace(action.Type)
                                    ? "missing dialogue action type" : "unsupported dialogue action type '" + action.Type + "'");
                                break;
                        }
                    }
                }
            }
            return errors;
        }
    }

    internal static class ReferenceChecks
    {
        internal static void Quest(string id, QuestContentRegistry quests, string location, string context, AreteValidationResult errors)
        {
            if (string.IsNullOrWhiteSpace(id)) errors.AddError(location, "missing mission id for " + context);
            else if (quests == null) errors.AddError(location, "quest registry is missing");
            else if (!quests.TryGetQuest(id, out _)) errors.AddError(location, "mission id '" + id + "' was not found");
        }

        internal static void Condition(string type, string questId, QuestContentRegistry quests, string location, AreteValidationResult errors)
        {
            switch (type?.ToLowerInvariant())
            {
                case "alwaystrue": case "alwaysfalse": return;
                case "missionoffered": case "missionactive": case "missioncompleted": case "missionnotstarted":
                    Quest(questId, quests, location, "condition", errors); return;
                default:
                    errors.AddError(location, string.IsNullOrWhiteSpace(type) ? "missing condition type" : "unsupported condition type '" + type + "'");
                    return;
            }
        }
    }
}

namespace ZoneEngine.Core.Arete
{
    using ZoneEngine.Core.Arete.Dialogue;

    public static class AreteConditionReferenceValidator
    {
        // Quest definitions are inspected through their existing public contracts; no quest state is changed.
        public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> dialoguePacks,
            IEnumerable<QuestContentPack> questPacks, DialogueContentRegistry dialogueRegistry, QuestContentRegistry questRegistry)
        {
            var errors = new AreteValidationResult();
            var dialogueNpcs = (dialoguePacks ?? Enumerable.Empty<DialogueContentPack>()).Where(pack => pack != null)
                .SelectMany(pack => pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>()).Where(npc => npc != null);
            foreach (var npc in dialogueNpcs)
            {
                var conditions = (npc.Conditions ?? Enumerable.Empty<DialogueCondition>()).Concat(
                    (npc.Nodes ?? Enumerable.Empty<DialogueNode>()).Where(node => node != null)
                    .SelectMany(node => node.Options ?? Enumerable.Empty<DialogueOption>()).Where(option => option != null)
                    .SelectMany(option => option.Conditions ?? Enumerable.Empty<DialogueCondition>()));
                foreach (var condition in conditions)
                {
                    if (condition == null) errors.AddError(npc.NpcIdentity, "dialogue condition is null");
                    else ReferenceChecks.Condition(condition.Type, condition.QuestId, questRegistry, npc.NpcIdentity, errors);
                }
            }
            var quests = (questPacks ?? Enumerable.Empty<QuestContentPack>()).Where(pack => pack != null)
                .SelectMany(pack => pack.Quests ?? Enumerable.Empty<QuestDefinition>()).Where(quest => quest != null);
            foreach (var quest in quests)
            {
                var steps = (quest.Steps ?? Enumerable.Empty<QuestStep>()).Where(step => step != null).ToArray();
                var conditions = (quest.Conditions ?? Enumerable.Empty<QuestCondition>())
                    .Concat(steps.SelectMany(step => step.Conditions ?? Enumerable.Empty<QuestCondition>()))
                    .Concat(steps.SelectMany(step => step.Objectives ?? Enumerable.Empty<QuestObjective>())
                        .Where(objective => objective != null)
                        .SelectMany(objective => objective.Conditions ?? Enumerable.Empty<QuestCondition>()));
                foreach (var condition in conditions)
                {
                    if (condition == null) errors.AddError(quest.QuestId, "quest condition is null");
                    else ReferenceChecks.Condition(condition.Type, condition.QuestId, questRegistry, quest.QuestId, errors);
                }
            }
            return errors;
        }
    }
}
