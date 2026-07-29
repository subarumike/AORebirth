using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestContentRegistry
{
	private readonly Dictionary<string, QuestContentPack> packsById = new Dictionary<string, QuestContentPack>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, QuestDefinition> questsById = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);

	private readonly List<QuestChainLinkMetadata> links = new List<QuestChainLinkMetadata>();

	public int PackCount => packsById.Count;

	public int QuestCount => questsById.Count;

	public AreteValidationResult Load(IEnumerable<QuestContentPack> packs)
	{
		AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().Load(packs);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromFiles(IEnumerable<string> filePaths)
	{
		AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadFiles(filePaths);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromDirectory(string directoryPath)
	{
		AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadDirectory(directoryPath);
		return ApplyLoadResult(loadResult);
	}

	public AreteValidationResult LoadFromManifest(string manifestPath)
	{
		AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadManifest(manifestPath);
		return ApplyLoadResult(loadResult);
	}

	private AreteValidationResult ApplyLoadResult(AreteContentLoadResult<QuestContentPack> loadResult)
	{
		if (!loadResult.IsValid)
		{
			return loadResult.Validation;
		}
		packsById.Clear();
		questsById.Clear();
		links.Clear();
		foreach (QuestContentPack pack in loadResult.Packs)
		{
			packsById.Add(pack.Identity.Id, pack);
			IEnumerable<QuestDefinition> quests = pack.Quests;
			foreach (QuestDefinition item in quests ?? Enumerable.Empty<QuestDefinition>())
			{
				questsById.Add(item.QuestId, item);
			}
			IEnumerable<QuestChainLinkMetadata> enumerable = pack.Links;
			foreach (QuestChainLinkMetadata item2 in enumerable ?? Enumerable.Empty<QuestChainLinkMetadata>())
			{
				links.Add(item2);
			}
		}
		return loadResult.Validation;
	}

	public IEnumerable<QuestChainLinkMetadata> GetLinksFrom(string questId)
	{
		if (string.IsNullOrWhiteSpace(questId))
		{
			return Enumerable.Empty<QuestChainLinkMetadata>();
		}
		return links.Where((QuestChainLinkMetadata link) => link != null && string.Equals(link.FromQuestId, questId, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	public IEnumerable<QuestChainLinkMetadata> GetLinksTo(string questId)
	{
		if (string.IsNullOrWhiteSpace(questId))
		{
			return Enumerable.Empty<QuestChainLinkMetadata>();
		}
		return links.Where((QuestChainLinkMetadata link) => link != null && string.Equals(link.ToQuestId, questId, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	public IEnumerable<QuestDefinition> GetQuests()
	{
		return questsById.Values.ToList();
	}

	public bool TryGetQuest(string questId, out QuestDefinition quest)
	{
		if (string.IsNullOrWhiteSpace(questId))
		{
			quest = null;
			return false;
		}
		return questsById.TryGetValue(questId, out quest);
	}
}
