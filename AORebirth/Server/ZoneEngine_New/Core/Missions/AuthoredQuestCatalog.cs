namespace ZoneEngine_New.Core.Missions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Missions.Content;

public sealed class AuthoredQuestCatalog
{
    public AuthoredQuestCatalog(InteractionContent content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        content.Validate();
        Definitions = content.MissionDefinitions;
    }
    public AuthoredQuestCatalog(IEnumerable<QuestContentPack> packs)
    {
        var registry = new QuestIndex();
        var validation = registry.Load(packs);
        if (!validation.IsValid) throw new InvalidDataException(string.Join("; ", validation.Errors));
        Content = new InteractionContent { MissionDefinitions = registry.GetQuests().Select(q => new MissionDefinition {
            QuestId = q.QuestId, InitialStepId = q.InitialStepId, IsResolved = true,
            StepIds = q.Steps.Select(x => x.StepId).ToList(), PrerequisiteQuestIds = [],
            Objectives = q.Steps.SelectMany(s => s.Objectives.Select(o => new MissionObjectiveDefinition {
                ObjectiveId = o.ObjectiveId, StepId = s.StepId, RequiredCount = o.RequiredCount, IsResolved = o.RequiredCount > 0
            })).ToList()
        }).ToArray() };
        Definitions = Content.MissionDefinitions;
    }
    public InteractionContent Content { get; }
    public IReadOnlyList<MissionDefinition> Definitions { get; }
    public static AuthoredQuestCatalog Load(string contentRoot)
    {
        var content = InteractionContent.Load(contentRoot);
        content.LoadRegistries(contentRoot);
        return new(content);
    }
}
