using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestContentPack
{
	public QuestContentPackIdentity Identity { get; set; }

	public IList<string> SourceCaptures { get; set; }

	public IList<QuestDefinition> Quests { get; set; }

	public IList<QuestChainLinkMetadata> Links { get; set; }

	public QuestContentPack()
	{
		Identity = new QuestContentPackIdentity();
		SourceCaptures = new List<string>();
		Quests = new List<QuestDefinition>();
		Links = new List<QuestChainLinkMetadata>();
	}
}
