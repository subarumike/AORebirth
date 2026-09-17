namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete;
using System.IO;
using ZoneEngine_New.Core.Missions;
using ZoneEngine.Core.Arete.Dialogue;

/// <summary>The existing checked-in manifest validator owns content. This class never authorizes an NPC spawn.</summary>
public sealed class DialogueCatalog
{
    public DialogueCatalog(DialogueContentRegistry registry, InteractionContent? content = null)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Content = content;
        Sessions = new DialogueGraph(registry);
    }

    public DialogueContentRegistry Registry { get; }
    public InteractionContent? Content { get; }
    public DialogueGraph Sessions { get; }

    public static DialogueCatalog Load(string runtimeBaseDirectory)
    {
        var root = Path.Combine(runtimeBaseDirectory, "Content");
        var content = InteractionContent.Load(root);
        return new(content.LoadRegistries(root).DialogueRegistry, content);
    }
    public bool TryGet(string contentIdentity, out DialogueNpcEntry npc) => Registry.TryGetNpc(contentIdentity, out npc);
    public bool IsEnabled(string contentIdentity) => Content == null || (Content.Dialogues.TryGetValue(contentIdentity, out var binding) && binding.Enabled);

    public static DialogueOption[] VisibleOptions(DialogueStep result) => result.AvailableOptions
        .Where(option => option != null && !string.IsNullOrWhiteSpace(option.Text)
            && !option.Hidden)
        .OrderBy(option => option.Index).ToArray();
}
