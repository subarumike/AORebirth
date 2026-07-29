using System.Collections.Generic;

namespace ZoneEngine.Core.Arete;

public sealed class AreteContentManifest
{
	public IList<string> DialoguePacks { get; set; }

	public IList<string> QuestPacks { get; set; }

	public AreteContentManifest()
	{
		DialoguePacks = new List<string>();
		QuestPacks = new List<string>();
	}
}
