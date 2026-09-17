using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine_New.Core.Missions.Content
{
    /// <summary>A validated snapshot for cross-content references; native quest services own progression.</summary>
    public sealed class QuestIndex
    {
        Dictionary<string, QuestDefinition> quests = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);
        QuestChainLinkMetadata[] links = new QuestChainLinkMetadata[0];
        public int PackCount { get; private set; }
        public int QuestCount => quests.Count;
        public IEnumerable<QuestDefinition> GetQuests() => quests.Values.ToArray();
        public IEnumerable<QuestChainLinkMetadata> GetLinksFrom(string id)
            => links.Where(link => string.Equals(link.FromQuestId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        public IEnumerable<QuestChainLinkMetadata> GetLinksTo(string id)
            => links.Where(link => string.Equals(link.ToQuestId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        public bool TryGetQuest(string id, out QuestDefinition definition)
        {
            definition = null;
            return id != null && quests.TryGetValue(id, out definition);
        }

        public ContentValidationResult Load(IEnumerable<QuestContentPack> packs)
        {
            var source = (packs ?? Enumerable.Empty<QuestContentPack>()).ToArray();
            var validation = Validate(source);
            if (validation.IsValid)
            {
                var next = source.SelectMany(pack => pack.Quests ?? Enumerable.Empty<QuestDefinition>())
                    .ToDictionary(quest => quest.QuestId, StringComparer.OrdinalIgnoreCase);
                var nextLinks = source.SelectMany(pack => pack.Links ?? Enumerable.Empty<QuestChainLinkMetadata>()).ToArray();
                quests = next;
                links = nextLinks;
                PackCount = source.Length;
            }
            return validation;
        }

        public static ContentReadResult<QuestContentPack> ReadFiles(IEnumerable<string> paths)
        {
            var validation = new ContentValidationResult();
            var packs = new List<QuestContentPack>();
            foreach (var path in paths ?? Enumerable.Empty<string>())
            {
                try { packs.Add(ContentJson.Read<QuestContentPack>(path)); }
                catch (Exception failure) { validation.AddError(path, failure.Message); }
            }
            validation.AddErrors(Validate(packs));
            return new ContentReadResult<QuestContentPack>(packs, validation);
        }

        public static ContentValidationResult Validate(IEnumerable<QuestContentPack> content)
        {
            var check = new ContentValidationResult();
            var packs = Index(content, pack => pack.Identity?.Id, "quest packs", check);
            var definitions = Index(packs.Values.SelectMany(pack => pack.Quests ?? Enumerable.Empty<QuestDefinition>()),
                quest => quest.QuestId, "quests", check);
            var steps = new Dictionary<string, Dictionary<string, QuestStep>>(StringComparer.OrdinalIgnoreCase);
            foreach (var quest in definitions.Values)
            {
                var questSteps = Index(quest.Steps, step => step.StepId, quest.QuestId + " steps", check);
                steps.Add(quest.QuestId, questSteps);
                Index(questSteps.Values.SelectMany(step => step.Objectives ?? Enumerable.Empty<QuestObjective>()),
                    objective => objective.ObjectiveId, quest.QuestId + " objectives", check);
                if (!string.IsNullOrWhiteSpace(quest.InitialStepId) && !questSteps.ContainsKey(quest.InitialStepId))
                    check.AddError(quest.QuestId, "Initial step does not exist: " + quest.InitialStepId);
            }
            foreach (var link in packs.Values.SelectMany(pack => pack.Links ?? Enumerable.Empty<QuestChainLinkMetadata>()))
            {
                if (link == null) { check.AddError("quest links", "Null link."); continue; }
                var endpoints = new[] { new { Quest = link.FromQuestId, Step = link.FromStepId }, new { Quest = link.ToQuestId, Step = link.ToStepId } };
                foreach (var endpoint in endpoints)
                {
                    Dictionary<string, QuestStep> targets;
                    if (string.IsNullOrWhiteSpace(endpoint.Quest) || !steps.TryGetValue(endpoint.Quest, out targets))
                        check.AddError(link.Id, "Unknown quest link target: " + endpoint.Quest);
                    else if (!string.IsNullOrWhiteSpace(endpoint.Step) && !targets.ContainsKey(endpoint.Step))
                        check.AddError(link.Id, "Unknown quest link step: " + endpoint.Step);
                }
            }
            return check;
        }

        static Dictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> key, string source, ContentValidationResult check) where T : class
        {
            var groups = (values ?? Enumerable.Empty<T>()).GroupBy(value => value == null ? null : key(value), StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.Key)) check.AddError(source, "An entry is null or has no identity.");
                else if (group.Skip(1).Any()) check.AddError(source, "Duplicate identity: " + group.Key);
                else result.Add(group.Key, group.First());
            }
            return result;
        }
    }
}
