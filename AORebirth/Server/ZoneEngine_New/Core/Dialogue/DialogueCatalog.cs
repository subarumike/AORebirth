namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete;
using ZoneEngine.Core.Arete.Dialogue;

/// <summary>The existing checked-in manifest validator owns content. This class never authorizes an NPC spawn.</summary>
public sealed class DialogueCatalog
{
    public DialogueCatalog(DialogueContentRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Sessions = new DialogueSessionService(registry);
    }

    public DialogueContentRegistry Registry { get; }
    public DialogueSessionService Sessions { get; }

    public static DialogueCatalog Load(string runtimeBaseDirectory)
        => new(AreteFrameworkBootstrap.InitializeCheckedInContent(runtimeBaseDirectory).DialogueRegistry);

    public bool TryGet(string contentIdentity, out DialogueNpcEntry npc)
        => Registry.TryGetNpc(contentIdentity, out npc);

    public static bool IsEnabled(string contentIdentity)
    {
        if (contentIdentity == "SimpleChar:79135F51")
            return AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_SUBWAY_TAILOR_DIALOGUE_ROUTING");
        return !AreteIdentities.Contains(contentIdentity)
            || AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING");
    }

    // Existing ContentDrivenNpcDialogueRouter registrations; never match a runtime actor by this list.
    static readonly HashSet<string> AreteIdentities = new(StringComparer.Ordinal)
    {
        "SimpleChar:782DE568", "SimpleChar:782DE567", "SimpleChar:78E0FC64", "SimpleChar:78E0FC61",
        "SimpleChar:78E0FC66", "SimpleChar:78E0FC65", "SimpleChar:78E0FC69", "SimpleChar:78E0FC68",
        "SimpleChar:78E0FC6C", "SimpleChar:78E0FC6B", "SimpleChar:7985CAEC", "SimpleChar:78E0FC6A",
        "SimpleChar:78E0FC81", "SimpleChar:78E0FC73"
    };

    public static DialogueSession Copy(DialogueSession session) => new()
    {
        SessionId = session.SessionId, NpcIdentity = session.NpcIdentity,
        CurrentNodeId = session.CurrentNodeId, IsActive = session.IsActive
    };

    public static DialogueOption[] VisibleOptions(DialogueSessionResult result) => result.AvailableOptions
        .Where(option => option != null && !string.IsNullOrWhiteSpace(option.Text)
            && option.Text.IndexOf("(Continue after trade)", StringComparison.OrdinalIgnoreCase) < 0)
        .OrderBy(option => option.Index).ToArray();
}
