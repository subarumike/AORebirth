namespace ZoneEngine_New.Tests;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Dialogue;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine.Core.Arete.Dialogue;

[TestClass]
public sealed class InteractionEditabilityTests
{
    static InteractionContent Content() => InteractionContent.Load(Path.Combine(AppContext.BaseDirectory,"Content"));
    static InteractionContent Reload(InteractionContent content)
    {
        string root = Path.Combine(Path.GetTempPath(), "aor-interaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Runtime"));
        try
        {
            File.WriteAllText(Path.Combine(root,"Runtime","interactions.json"), JsonSerializer.Serialize(content, InteractionContent.JsonOptions));
            return InteractionContent.Load(root);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void Same_binary_uses_changed_quest_item_reward_after_data_reload()
    {
        var content = Content(); var action = content.Actions["open-sealed-lockpick"];
        action.Grants[0].ItemId = 43379; action.Grants[0].Quality = 8;
        using var world = new AuthoredQuestTests.World(content: Reload(content));
        world.Activate(AuthoredQuestFixture.BuyLockpick); var source = world.Add(295999);
        Assert.IsTrue(world.Service.TryUseItem(world.Player,new(){Type=IdentityType.Inventory,Instance=64},source), world.Logger.LastError);
        Assert.AreEqual(0,world.Dao.Items[10].ContainerType); // Consumption preserves the historical row.
        Assert.AreEqual(43379,world.Dao.Items.Values.Single(x => x.ContainerType != 0).LowId);
        Assert.AreEqual(8,world.Dao.Items.Values.Single(x => x.ContainerType != 0).Quality);
    }
    [TestMethod]
    public void Same_binary_uses_changed_dialogue_action_binding_after_data_reload()
    {
        var content = Content();
        content.Dialogues[DialogueFixture.Stan].Routes.Single(x=>x.Node=="stan_goldman_003").Action="accept-sarah-job";
        using var world = new AuthoredQuestTests.World(content: Reload(content));
        world.Activate(AuthoredQuestFixture.TalkSarah);
        var router = new DialogueActionRouter(world.Service);
        Assert.AreEqual(DialogueActionOutcome.Continue,router.ApplyAnswer(world.Player,DialogueFixture.Stan,"stan_goldman_003",0));
        Assert.AreEqual(AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState.Active,world.Dao.GetMission(new(111,AuthoredQuestFixture.FindThief)).State);
        Assert.IsNull(world.Dao.GetMission(new(111,AuthoredQuestFixture.BuyLockpick)));
    }
    [TestMethod]
    public void Same_binary_projects_edited_dialogue_text_from_existing_pack_model()
    {
        var catalog=DialogueCatalog.Load(AppContext.BaseDirectory);
        Assert.IsTrue(catalog.TryGet(DialogueFixture.Stan,out var npc));
        var root=npc.Nodes.Single(x=>x.Id==npc.RootNodeId);
        root.PromptSegments.Clear(); root.PromptText="An operator edited this conversation.";
        var pack = new DialogueContentPack { Identity = new() { Id="editable-fixture", Version="1", Source="" }, Npcs=[npc] };
        string file = Path.Combine(Path.GetTempPath(), "aor-dialogue-" + Guid.NewGuid().ToString("N") + ".json");
        DialogueContentRegistry registry = new();
        try
        {
            File.WriteAllText(file,JsonSerializer.Serialize(pack));
            var loaded = new DialogueContentPackLoader().LoadFile(file);
            Assert.IsTrue(loaded.IsValid,string.Join("; ",loaded.Validation.Errors));
            Assert.IsTrue(registry.Load(loaded.Packs).IsValid);
        }
        finally { File.Delete(file); }
        var node=new DialogueSessionService(registry).StartSession(DialogueFixture.Stan);
        var messages=DialogueWire.Node(new(){Type=IdentityType.CanbeAffected,Instance=111},new(){Type=IdentityType.CanbeAffected,Instance=222},"Tester",node).ToArray();
        Assert.AreEqual(root.PromptText,messages.OfType<KnuBotAppendTextMessage>().Single().Text);
    }
    [TestMethod]
    public void Invalid_content_action_reference_is_rejected_before_runtime_mutation()
    {
        var content=Content(); content.Dialogues[DialogueFixture.Stan].Routes[0].Action="absent-action";
        Assert.ThrowsExactly<InvalidDataException>(()=>content.Validate());
    }
    [TestMethod]
    public void Blank_provenance_does_not_block_valid_operator_authored_quest_reward()
    {
        var content=Content(); var reward=content.Actions["finish-buy-nano"].StatReward!;
        reward.Evidence=""; reward.EffectReference="";
        using var world=new AuthoredQuestTests.World(content:Reload(content));
        world.Activate(AuthoredQuestFixture.BuyNano); var source=world.Add(248258);
        Assert.IsTrue(world.Service.TryUseItem(world.Player,new(){Type=IdentityType.Inventory,Instance=64},source),world.Logger.LastError);
        Assert.AreEqual(AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState.Completed,world.Dao.GetMission(new(111,AuthoredQuestFixture.BuyNano)).State);
    }
}
