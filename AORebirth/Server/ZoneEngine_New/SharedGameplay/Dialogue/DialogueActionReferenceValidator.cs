namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public static class DialogueActionReferenceValidator
    {
        private static readonly HashSet<string> SupportedActionTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "OfferMission",
                "AcceptMission",
                "CompleteMission",
                "FailMission",
                "AbandonMission",
                "EndDialogue"
            };

        private static readonly HashSet<string> MissionActionTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "OfferMission",
                "AcceptMission",
                "CompleteMission",
                "FailMission",
                "AbandonMission"
            };

        public static AreteValidationResult Validate(
            IEnumerable<DialogueContentPack> dialoguePacks,
            QuestContentRegistry questRegistry)
        {
            var result = new AreteValidationResult();
            int packIndex = 0;

            foreach (DialogueContentPack pack in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
            {
                string packLocation = "dialoguePack[" + packIndex + "]";
                ValidatePack(result, pack, packLocation, questRegistry);
                packIndex++;
            }

            return result;
        }

        private static void ValidatePack(
            AreteValidationResult result,
            DialogueContentPack pack,
            string packLocation,
            QuestContentRegistry questRegistry)
        {
            if (pack == null)
            {
                result.AddError(packLocation, "content pack is null");
                return;
            }

            int npcIndex = 0;
            foreach (DialogueNpcEntry npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
            {
                string npcLocation = packLocation + ".npc[" + npcIndex + "]";
                ValidateNpc(result, npc, npcLocation, questRegistry);
                npcIndex++;
            }
        }

        private static void ValidateNpc(
            AreteValidationResult result,
            DialogueNpcEntry npc,
            string npcLocation,
            QuestContentRegistry questRegistry)
        {
            if (npc == null)
            {
                result.AddError(npcLocation, "npc entry is null");
                return;
            }

            ValidateActions(result, npc.Actions, npcLocation + ".action", questRegistry);

            int nodeIndex = 0;
            foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
            {
                string nodeLocation = npcLocation + ".node[" + nodeIndex + "]";
                ValidateNode(result, node, nodeLocation, questRegistry);
                nodeIndex++;
            }
        }

        private static void ValidateNode(
            AreteValidationResult result,
            DialogueNode node,
            string nodeLocation,
            QuestContentRegistry questRegistry)
        {
            if (node == null)
            {
                result.AddError(nodeLocation, "dialogue node is null");
                return;
            }

            ValidateActions(result, node.EnterActions, nodeLocation + ".enterAction", questRegistry);

            int optionIndex = 0;
            foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
            {
                string optionLocation = nodeLocation + ".option[" + optionIndex + "]";
                if (option == null)
                {
                    result.AddError(optionLocation, "dialogue option is null");
                    optionIndex++;
                    continue;
                }

                ValidateActions(result, option.Actions, optionLocation + ".action", questRegistry);
                optionIndex++;
            }
        }

        private static void ValidateActions(
            AreteValidationResult result,
            IEnumerable<DialogueAction> actions,
            string actionLocationPrefix,
            QuestContentRegistry questRegistry)
        {
            int actionIndex = 0;
            foreach (DialogueAction action in actions ?? Enumerable.Empty<DialogueAction>())
            {
                string actionLocation = actionLocationPrefix + "[" + actionIndex + "]";
                ValidateAction(result, action, actionLocation, questRegistry);
                actionIndex++;
            }
        }

        private static void ValidateAction(
            AreteValidationResult result,
            DialogueAction action,
            string actionLocation,
            QuestContentRegistry questRegistry)
        {
            if (action == null)
            {
                result.AddError(actionLocation, "dialogue action is null");
                return;
            }

            if (string.IsNullOrWhiteSpace(action.Type))
            {
                result.AddError(actionLocation, "missing dialogue action type");
                return;
            }

            if (!SupportedActionTypes.Contains(action.Type))
            {
                result.AddError(actionLocation, "unsupported dialogue action type '" + action.Type + "'");
                return;
            }

            if (!MissionActionTypes.Contains(action.Type))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(action.QuestId))
            {
                result.AddError(actionLocation, "missing mission id for dialogue action '" + action.Type + "'");
                return;
            }

            if (questRegistry == null)
            {
                result.AddError(actionLocation, "quest registry is missing");
                return;
            }

            QuestDefinition quest;
            if (!questRegistry.TryGetQuest(action.QuestId, out quest))
            {
                result.AddError(actionLocation, "mission id '" + action.QuestId + "' was not found");
            }
        }
    }
}
