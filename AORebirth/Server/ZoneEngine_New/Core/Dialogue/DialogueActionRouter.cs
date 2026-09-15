namespace ZoneEngine_New.Core.Dialogue;
using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;

public enum DialogueActionOutcome { Rejected, Continue, Trade, Vendor }

/// <summary>Executes validated editable dialogue bindings through the durable authored action service.</summary>
public sealed class DialogueActionRouter(AuthoredQuestService quests)
{
    public bool TryOpen(Player player, string contentIdentity, bool hasPriorOpen, out string? startNode)
    {
        startNode = null;
        return quests.Content.Dialogues.TryGetValue(contentIdentity, out var binding)
            && quests.TryResolveDialogueStart(player, binding, hasPriorOpen, out startNode);
    }
    public DialogueBinding? Binding(string identity) => quests.Content.Dialogues.GetValueOrDefault(identity);
    public DialogueRoute? Route(string identity, string node, int answer)
        => Binding(identity)?.Routes.SingleOrDefault(x => x.Node == node && x.Answer == answer);
    public DialogueActionOutcome ApplyAnswer(Player player, string contentIdentity, string previousNode, int answerIndex)
    {
        var route = Route(contentIdentity, previousNode, answerIndex);
        if (route == null) return DialogueActionOutcome.Continue;
        if (route.Vendor) return DialogueActionOutcome.Vendor;
        if (route.TradeItem > 0) return quests.CanExecuteAction(player, route.Action) ? DialogueActionOutcome.Trade : DialogueActionOutcome.Rejected;
        return quests.TryExecuteAction(player, route.Action) ? DialogueActionOutcome.Continue : DialogueActionOutcome.Rejected;
    }
    public bool CompleteTrade(Player player, DialogueRoute route, SmokeLounge.AOtomation.Messaging.GameData.Identity slot,
        ZoneEngine_New.Core.Inventory.Item item, Action publishAccepted)
        => quests.TryExecuteAction(player, route.Action, slot, item, publishAccepted);
}
