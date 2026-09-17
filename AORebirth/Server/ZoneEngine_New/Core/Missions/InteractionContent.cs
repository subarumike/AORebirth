namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ZoneEngine.Core.Arete;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Missions.Content;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

/// <summary>Generic runtime action bindings for the existing validated Content manifest/pack system.</summary>
public sealed class InteractionContent
{
    public int Version { get; set; } = 1;
    public string[] Manifests { get; set; } = [];
    public MissionDefinition[] MissionDefinitions { get; set; } = [];
    public Dictionary<string, InteractionAction> Actions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, DialogueBinding> Dialogues { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, JournalDefinition> Journals { get; set; } = new(StringComparer.Ordinal);
    public TimedTurnInDefinition[] TimedTurnIns { get; set; } = [];
    public QuestPropDefinition[] Props { get; set; } = [];
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, IncludeFields = true };

    public static InteractionContent Load(string contentRoot)
    {
        string path = Path.Combine(contentRoot, "Runtime", "interactions.json");
        var content = JsonSerializer.Deserialize<InteractionContent>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Empty interaction content.");
        content.Validate();
        return content;
    }

    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported interaction content version.");
        AuthoredMissionProgression.Validate(MissionDefinitions);
        var quests = MissionDefinitions.Select(x => x.QuestId).ToHashSet(StringComparer.Ordinal);
        if (quests.Count != MissionDefinitions.Length) throw new InvalidDataException("Duplicate mission definition.");
        if (Actions.Values.Where(x => x.DirectItemUse).SelectMany(x => x.ItemIds).GroupBy(x => x).Any(x => x.Count() > 1))
            throw new InvalidDataException("Ambiguous item-use action binding.");
        foreach (var pair in Actions)
        {
            var a = pair.Value;
            if (string.IsNullOrWhiteSpace(pair.Key) || a.ItemIds.Any(x => x <= 0)
                || a.Grants.Any(x => x.ItemId <= 0 || x.Quality <= 0) || a.Playfields.Any(x => x <= 0))
                throw new InvalidDataException("Invalid action content: " + pair.Key);
            foreach (var q in a.Accept.Concat(a.Complete.Select(x => x.Quest)).Concat(a.RequireMissions.Select(x => x.Quest)))
                if (!quests.Contains(q)) throw new InvalidDataException("Unknown mission in action " + pair.Key + ": " + q);
            foreach (var c in a.RequireMissions.Concat(a.RejectMissions).Concat(a.AnyMissions))
                if (!quests.Contains(c.Quest) || c.States.Length == 0 || c.States.Any(s => !Enum.TryParse<AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState>(s, out _)))
                    throw new InvalidDataException("Invalid mission condition: " + pair.Key);
            foreach (var completion in a.Complete)
                if (!MissionDefinitions.Single(x => x.QuestId == completion.Quest).Objectives.Any(x => x.ObjectiveId == completion.Objective))
                    throw new InvalidDataException("Unknown mission objective: " + completion.Objective);
            foreach (string journal in a.DeleteJournals.Concat(a.SendJournals))
                if (!Journals.ContainsKey(journal)) throw new InvalidDataException("Unknown journal: " + journal);
            if (a.StatReward is { } reward && (!quests.Contains(reward.Quest) || string.IsNullOrWhiteSpace(reward.Key) || reward.Xp < 0 || reward.Cash < 0))
                throw new InvalidDataException("Invalid stat reward identity or amount: " + pair.Key);
            foreach (var follow in a.OptionalActions) if (!Actions.ContainsKey(follow)) throw new InvalidDataException("Unknown optional action: " + follow);
            if (a.OptionalActions.Length != 0 && a.OptionalActions.Any(x => Actions[x].OptionalActions.Length != 0))
                throw new InvalidDataException("Nested optional action graphs are unsupported.");
        }
        foreach (var binding in Dialogues.Values)
        {
            foreach (var route in binding.Routes)
                if (!string.IsNullOrEmpty(route.Action) && !Actions.ContainsKey(route.Action)
                    && !TimedTurnIns.Any(x => x.Key == route.Action)) throw new InvalidDataException("Unknown dialogue action.");
            if (binding.Routes.GroupBy(x => (x.Node, x.Answer)).Any(x => x.Count() > 1)) throw new InvalidDataException("Duplicate dialogue route.");
            if (binding.Routes.Any(x => x.TradeItem < 0 || x.TradeSlots < 1 || (x.TradeItem > 0 && string.IsNullOrWhiteSpace(x.TradePrompt))))
                throw new InvalidDataException("Invalid dialogue trade presentation.");
        }
        foreach (var timed in TimedTurnIns)
            if (timed.Items.Length == 0 || timed.Items.Any(x => x.ItemId <= 0 || x.MinLevel < 1 || x.MaxLevel < x.MinLevel)
                || timed.CooldownSeconds <= 0 || !quests.Contains(timed.Quest) || !quests.Contains(timed.CooldownQuest))
                throw new InvalidDataException("Invalid timed item turn-in definition.");
        foreach (var pair in Journals)
        {
            var journal = pair.Value;
            if (!quests.Contains(pair.Key) || journal.RecipientMarker <= 0 || journal.DeleteRecipientMarker <= 0
                || journal.DeleteQuestMarker <= 0 || journal.DurationSeconds <= 0) throw new InvalidDataException("Invalid journal definition: " + pair.Key);
            if (journal.Packet is { } packet)
            {
                var projection = packet.Deserialize<QuestFullUpdateMessage>(JsonOptions);
                if (projection?.Quests is not { Length: 1 }) throw new InvalidDataException("Invalid typed quest journal.");
            }
            else
            {
                var bytes = Convert.FromHexString(journal.Hex);
                if (bytes.Length < 4 || journal.ExpiryOffset < -1 || journal.ExpiryOffset > bytes.Length - 4)
                    throw new InvalidDataException("Invalid journal packet/expiry projection.");
            }
            foreach (string hex in journal.DeleteHex) if (Convert.FromHexString(hex).Length < 4) throw new InvalidDataException("Invalid journal deletion frame.");
        }
        if (Props.GroupBy(x => (x.Playfield, x.Instance)).Any(x => x.Count() > 1)
            || Props.Any(x => !Actions.ContainsKey(x.Action) || x.Position.Length != 3 || x.Rotation.Length != 4
                || x.Position.Concat(x.Rotation).Any(x => !float.IsFinite(x)))) throw new InvalidDataException("Invalid quest prop content.");
    }

    public InteractionRegistries LoadRegistries(string contentRoot)
    {
        string root = Path.GetFullPath(contentRoot) + Path.DirectorySeparatorChar;
        var paths = Manifests.Select(relative => {
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Content manifest escapes root.");
            return path;
        });
        var dialoguePaths = new List<string>(); var questPaths = new List<string>();
        foreach (var path in paths)
        {
            var manifest = InteractionManifest.Read(path);
            dialoguePaths.AddRange(manifest.DialoguePacks); questPaths.AddRange(manifest.QuestPacks);
        }
        var dialoguePacks = new DialogueContentPackLoader().LoadFiles(dialoguePaths.Distinct(StringComparer.OrdinalIgnoreCase));
        var questPacks = QuestIndex.ReadFiles(questPaths.Distinct(StringComparer.OrdinalIgnoreCase));
        if (!dialoguePacks.IsValid || !questPacks.IsValid) throw new InvalidDataException(string.Join("; ", dialoguePacks.Validation.Errors.Concat(questPacks.Validation.Errors)));
        var dialogues = new DialogueContentRegistry(); var quests = new QuestIndex();
        var validation = dialogues.Load(dialoguePacks.Packs);
        validation.AddErrors(quests.Load(questPacks.Packs));
        validation.AddErrors(DialogueActionReferenceValidator.Validate(dialoguePacks.Packs, quests));
        validation.AddErrors(AreteConditionReferenceValidator.Validate(dialoguePacks.Packs, questPacks.Packs, dialogues, quests));
        if (!validation.IsValid) throw new InvalidDataException(string.Join("; ", validation.Errors));
        var result = new InteractionRegistries(dialogues, quests);
        foreach (var pair in Dialogues)
        {
            if (!result.DialogueRegistry.TryGetNpc(pair.Key, out var npc)) throw new InvalidDataException("Unknown dialogue binding: " + pair.Key);
            var nodes = npc.Nodes.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var route in pair.Value.Routes)
                if (!nodes.TryGetValue(route.Node, out var node) || !node.Options.Any(x => x.Index == route.Answer)) throw new InvalidDataException("Unknown dialogue node/answer.");
        }
        return result;
    }
}
public sealed record InteractionRegistries(DialogueContentRegistry DialogueRegistry, QuestIndex QuestRegistry);

public sealed class InteractionAction
{
    public bool DirectItemUse { get; set; }
    public int[] ItemIds { get; set; } = [];
    public int[] Playfields { get; set; } = [];
    public MissionCondition[] RequireMissions { get; set; } = [];
    public MissionCondition[] RejectMissions { get; set; } = [];
    public MissionCondition[] AnyMissions { get; set; } = [];
    public int[] AnyCarried { get; set; } = [];
    public int[] RejectCarried { get; set; } = [];
    public bool Consume { get; set; }
    public bool UseProjection { get; set; }
    public ItemGrant[] Grants { get; set; } = [];
    public MissionCompletion[] Complete { get; set; } = [];
    public string[] Accept { get; set; } = [];
    public string[] OptionalActions { get; set; } = [];
    public StatReward? StatReward { get; set; }
    public string[] DeleteJournals { get; set; } = [];
    public string[] SendJournals { get; set; } = [];
    public string Feedback { get; set; } = "";
    public int FeedbackCategory { get; set; }
    public int FeedbackMessage { get; set; }
    public bool AcknowledgementBeforeConsumption { get; set; }
}
public sealed class MissionCondition { public string Quest { get; set; } = ""; public string[] States { get; set; } = []; }
public sealed class MissionCompletion
{
    public string Quest { get; set; } = ""; public string Objective { get; set; } = "";
    public string Observation { get; set; } = ""; public string Event { get; set; } = "";
}
public sealed class ItemGrant
{
    public int ItemId { get; set; } public int Quality { get; set; } = 1;
    public bool SkipIfCarried { get; set; } public bool NotifyIfCarried { get; set; }
    public bool HonorUnique { get; set; }
    public bool PublishBeforeConsumption { get; set; }
}
public sealed class StatReward
{
    public string Quest { get; set; } = ""; public string Key { get; set; } = "";
    public int Xp { get; set; } public int Cash { get; set; } public string Evidence { get; set; } = "";
    public string EffectReference { get; set; } = "";
}
public sealed class DialogueBinding
{
    public bool Enabled { get; set; } = true; public int OpenMode { get; set; } = 1;
    public int[] Playfields { get; set; } = [];
    public string ReopenNode { get; set; } = "";
    public DialogueStartRule[] Starts { get; set; } = [];
    public DialogueRoute[] Routes { get; set; } = [];
}
public sealed class DialogueStartRule
{
    public string Node { get; set; } = ""; public int[] Carried { get; set; } = [];
    public MissionCondition[] Missions { get; set; } = [];
}
public sealed class DialogueRoute
{
    public string Node { get; set; } = ""; public int Answer { get; set; }
    public string Action { get; set; } = ""; public bool Vendor { get; set; }
    public int TradeItem { get; set; } public int TradeSlots { get; set; } = 1;
    public string TradePrompt { get; set; } = "";
}
public sealed class JournalDefinition
{
    public JsonElement? Packet { get; set; }
    public string Hex { get; set; } = "";
    public int RecipientMarker { get; set; }
    public string[] DeleteHex { get; set; } = [];
    public int DeleteQuestMarker { get; set; }
    public int DeleteRecipientMarker { get; set; }
    public bool TypedDelete { get; set; }
    public bool SynchronizeClock { get; set; }
    public float ClockTime { get; set; } public int ClockUnknown3 { get; set; } public float ClockUnknown4 { get; set; }
    public long ClockBaseSeconds { get; set; }
    public int DurationSeconds { get; set; }
    public int ExpiryOffset { get; set; } = -1;
}
public sealed class TimedTurnInDefinition
{
    public string Key { get; set; } = ""; public string Quest { get; set; } = "";
    public string Objective { get; set; } = ""; public string CooldownQuest { get; set; } = "";
    public string CooldownObjective { get; set; } = ""; public string CooldownFlag { get; set; } = "";
    public string GrantedFlag { get; set; } = ""; public int CooldownSeconds { get; set; }
    public int[] TurnInPlayfields { get; set; } = [];
    public TimedItem[] Items { get; set; } = [];
    public string XpRewardKey { get; set; } = ""; public string TokenRewardKey { get; set; } = "";
    public string ObservationPrefix { get; set; } = ""; public string EventType { get; set; } = "";
    public string IneligibleText { get; set; } = ""; public string CooldownText { get; set; } = "";
    public string UnavailableText { get; set; } = ""; public string TokenText { get; set; } = "";
}
public sealed class TimedItem { public int ItemId { get; set; } public int MinLevel { get; set; } public int MaxLevel { get; set; } public bool Enabled { get; set; } public string Name { get; set; } = ""; }
public sealed class QuestPropDefinition
{
    public int Instance { get; set; } public int Playfield { get; set; } public int TemplateId { get; set; }
    public float[] Position { get; set; } = []; public float[] Rotation { get; set; } = [];
    public string Action { get; set; } = ""; public bool ItemTarget { get; set; }
    public Dictionary<int,uint> Stats { get; set; } = new();
}
