namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public static class DialogueContentPackValidator
    {
        private static readonly HashSet<string> TerminalTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "close",
                "end",
                "parent",
                "root",
                "self"
            };

        public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> packs)
        {
            var result = new AreteValidationResult();
            var packIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var npcIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int packIndex = 0;

            foreach (DialogueContentPack pack in packs ?? Enumerable.Empty<DialogueContentPack>())
            {
                string packLocation = "dialoguePack[" + packIndex + "]";
                string packId = GetPackId(pack);

                if (string.IsNullOrWhiteSpace(packId))
                {
                    result.AddError(packLocation, "missing dialogue content pack id");
                }
                else if (!packIds.Add(packId))
                {
                    result.AddError(packLocation, "duplicate dialogue content pack id '" + packId + "'");
                }

                ValidateNpcs(result, pack, packLocation, npcIdentities);
                packIndex++;
            }

            return result;
        }

        private static void ValidateNpcs(
            AreteValidationResult result,
            DialogueContentPack pack,
            string packLocation,
            HashSet<string> npcIdentities)
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
                if (npc == null)
                {
                    result.AddError(npcLocation, "npc entry is null");
                    npcIndex++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(npc.NpcIdentity))
                {
                    result.AddError(npcLocation, "missing NPC identity");
                }
                else if (!npcIdentities.Add(npc.NpcIdentity))
                {
                    result.AddError(npcLocation, "duplicate NPC identity '" + npc.NpcIdentity + "'");
                }

                ValidateNodes(result, npc, npcLocation);
                npcIndex++;
            }
        }

        private static void ValidateNodes(AreteValidationResult result, DialogueNpcEntry npc, string npcLocation)
        {
            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int nodeIndex = 0;

            foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
            {
                string nodeLocation = npcLocation + ".node[" + nodeIndex + "]";
                if (node == null)
                {
                    result.AddError(nodeLocation, "dialogue node is null");
                    nodeIndex++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    result.AddError(nodeLocation, "missing dialogue node id");
                }
                else if (!nodeIds.Add(node.Id))
                {
                    result.AddError(nodeLocation, "duplicate dialogue node id '" + node.Id + "'");
                }

                nodeIndex++;
            }

            if (!string.IsNullOrWhiteSpace(npc.RootNodeId) && !nodeIds.Contains(npc.RootNodeId))
            {
                result.AddError(npcLocation, "root dialogue node target '" + npc.RootNodeId + "' was not found");
            }

            ValidateOptions(result, npc, npcLocation, nodeIds);
        }

        private static void ValidateOptions(
            AreteValidationResult result,
            DialogueNpcEntry npc,
            string npcLocation,
            HashSet<string> nodeIds)
        {
            int nodeIndex = 0;
            foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
            {
                if (node == null)
                {
                    nodeIndex++;
                    continue;
                }

                int optionIndex = 0;
                foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
                {
                    string optionLocation = npcLocation + ".node[" + nodeIndex + "].option[" + optionIndex + "]";
                    if (option == null)
                    {
                        result.AddError(optionLocation, "dialogue option is null");
                        optionIndex++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(option.NextNodeId))
                    {
                        if (!OptionHasTerminalAction(option))
                        {
                            result.AddError(optionLocation, "missing dialogue node target");
                        }
                    }
                    else if (!TerminalTargets.Contains(option.NextNodeId) && !nodeIds.Contains(option.NextNodeId))
                    {
                        result.AddError(
                            optionLocation,
                            "dialogue node target '" + option.NextNodeId + "' was not found");
                    }

                    optionIndex++;
                }

                nodeIndex++;
            }
        }

        private static bool OptionHasTerminalAction(DialogueOption option)
        {
            return option.Actions != null
                   && option.Actions.Any(
                       action => action != null
                                 && (string.Equals(action.Type, "closeDialogue", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(action.Type, "endDialogue", StringComparison.OrdinalIgnoreCase)));
        }

        private static string GetPackId(DialogueContentPack pack)
        {
            if (pack == null || pack.Identity == null)
            {
                return string.Empty;
            }

            return pack.Identity.Id;
        }
    }
}
