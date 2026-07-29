using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueContentPack
{
	public DialogueContentPackIdentity Identity { get; set; }

	public IList<string> SourceCaptures { get; set; }

	public IList<DialogueNpcEntry> Npcs { get; set; }

	public DialogueContentPack()
	{
		Identity = new DialogueContentPackIdentity();
		SourceCaptures = new List<string>();
		Npcs = new List<DialogueNpcEntry>();
	}
}
