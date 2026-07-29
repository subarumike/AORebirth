using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public static class DialogueContentPackValidator
{
	private static readonly HashSet<string> TerminalTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "close", "end", "parent", "root", "self" };

	public static AreteValidationResult Validate(IEnumerable<DialogueContentPack> packs)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> npcIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (DialogueContentPack item in packs ?? Enumerable.Empty<DialogueContentPack>())
		{
			string text = "dialoguePack[" + num + "]";
			string packId = GetPackId(item);
			if (string.IsNullOrWhiteSpace(packId))
			{
				areteValidationResult.AddError(text, "missing dialogue content pack id");
			}
			else if (!hashSet.Add(packId))
			{
				areteValidationResult.AddError(text, "duplicate dialogue content pack id '" + packId + "'");
			}
			ValidateNpcs(areteValidationResult, item, text, npcIdentities);
			num++;
		}
		return areteValidationResult;
	}

	private static void ValidateNpcs(AreteValidationResult result, DialogueContentPack pack, string packLocation, HashSet<string> npcIdentities)
	{
		if (pack == null)
		{
			result.AddError(packLocation, "content pack is null");
			return;
		}
		int num = 0;
		IEnumerable<DialogueNpcEntry> npcs = pack.Npcs;
		foreach (DialogueNpcEntry item in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
		{
			string text = packLocation + ".npc[" + num + "]";
			if (item == null)
			{
				result.AddError(text, "npc entry is null");
				num++;
				continue;
			}
			if (string.IsNullOrWhiteSpace(item.NpcIdentity))
			{
				result.AddError(text, "missing NPC identity");
			}
			else if (!npcIdentities.Add(item.NpcIdentity))
			{
				result.AddError(text, "duplicate NPC identity '" + item.NpcIdentity + "'");
			}
			ValidateNodes(result, item, text);
			num++;
		}
	}

	private static void ValidateNodes(AreteValidationResult result, DialogueNpcEntry npc, string npcLocation)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		IEnumerable<DialogueNode> nodes = npc.Nodes;
		foreach (DialogueNode item in nodes ?? Enumerable.Empty<DialogueNode>())
		{
			string location = npcLocation + ".node[" + num + "]";
			if (item == null)
			{
				result.AddError(location, "dialogue node is null");
				num++;
				continue;
			}
			if (string.IsNullOrWhiteSpace(item.Id))
			{
				result.AddError(location, "missing dialogue node id");
			}
			else if (!hashSet.Add(item.Id))
			{
				result.AddError(location, "duplicate dialogue node id '" + item.Id + "'");
			}
			num++;
		}
		if (!string.IsNullOrWhiteSpace(npc.RootNodeId) && !hashSet.Contains(npc.RootNodeId))
		{
			result.AddError(npcLocation, "root dialogue node target '" + npc.RootNodeId + "' was not found");
		}
		ValidateOptions(result, npc, npcLocation, hashSet);
	}

	private static void ValidateOptions(AreteValidationResult result, DialogueNpcEntry npc, string npcLocation, HashSet<string> nodeIds)
	{
		int num = 0;
		IEnumerable<DialogueNode> nodes = npc.Nodes;
		foreach (DialogueNode item in nodes ?? Enumerable.Empty<DialogueNode>())
		{
			if (item == null)
			{
				num++;
				continue;
			}
			int num2 = 0;
			IEnumerable<DialogueOption> options = item.Options;
			foreach (DialogueOption item2 in options ?? Enumerable.Empty<DialogueOption>())
			{
				string location = npcLocation + ".node[" + num + "].option[" + num2 + "]";
				if (item2 == null)
				{
					result.AddError(location, "dialogue option is null");
					num2++;
					continue;
				}
				if (string.IsNullOrWhiteSpace(item2.NextNodeId))
				{
					if (!OptionHasTerminalAction(item2))
					{
						result.AddError(location, "missing dialogue node target");
					}
				}
				else if (!TerminalTargets.Contains(item2.NextNodeId) && !nodeIds.Contains(item2.NextNodeId))
				{
					result.AddError(location, "dialogue node target '" + item2.NextNodeId + "' was not found");
				}
				num2++;
			}
			num++;
		}
	}

	private static bool OptionHasTerminalAction(DialogueOption option)
	{
		return option.Actions != null && option.Actions.Any((DialogueAction action) => action != null && (string.Equals(action.Type, "closeDialogue", StringComparison.OrdinalIgnoreCase) || string.Equals(action.Type, "endDialogue", StringComparison.OrdinalIgnoreCase)));
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
