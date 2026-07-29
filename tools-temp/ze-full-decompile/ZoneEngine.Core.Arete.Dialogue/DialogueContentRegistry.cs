using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueContentRegistry
{
	private readonly Dictionary<string, DialogueContentPack> packsById = new Dictionary<string, DialogueContentPack>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DialogueNpcEntry> npcsByIdentity = new Dictionary<string, DialogueNpcEntry>(StringComparer.OrdinalIgnoreCase);

	public int PackCount => packsById.Count;

	public int NpcCount => npcsByIdentity.Count;

	public AreteValidationResult Load(IEnumerable<DialogueContentPack> packs)
	{
		AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().Load(packs);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromFiles(IEnumerable<string> filePaths)
	{
		AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().LoadFiles(filePaths);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromDirectory(string directoryPath)
	{
		AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().LoadDirectory(directoryPath);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromManifest(string manifestPath)
	{
		AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().LoadManifest(manifestPath);
		return ApplyLoadResult(loadResult);
	}

	private AreteValidationResult ApplyLoadResult(AreteContentLoadResult<DialogueContentPack> loadResult)
	{
		if (!loadResult.IsValid)
		{
			return loadResult.Validation;
		}
		packsById.Clear();
		npcsByIdentity.Clear();
		foreach (DialogueContentPack pack in loadResult.Packs)
		{
			packsById.Add(pack.Identity.Id, pack);
			IEnumerable<DialogueNpcEntry> npcs = pack.Npcs;
			foreach (DialogueNpcEntry item in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
			{
				npcsByIdentity.Add(item.NpcIdentity, item);
			}
		}
		return loadResult.Validation;
	}

	public bool TryGetNpc(string npcIdentity, out DialogueNpcEntry npc)
	{
		if (string.IsNullOrWhiteSpace(npcIdentity))
		{
			npc = null;
			return false;
		}
		return npcsByIdentity.TryGetValue(npcIdentity, out npc);
	}
}
