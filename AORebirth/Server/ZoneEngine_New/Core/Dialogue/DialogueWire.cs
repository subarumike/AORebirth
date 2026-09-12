namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;

/// <summary>Exact existing KnuBot handler fields and content-router formatting; no reconstructed raw packets.</summary>
public static class DialogueWire
{
    public static KnuBotOpenChatWindowMessage Open(Identity player, Identity npc, string contentIdentity) => new()
    {
        Identity = player, Target = npc, Unknown1 = 2,
        // ContentDrivenNpcDialogueRouter.SendOpenChatWindow: captured Rex and Tailor use zero.
        Unknown2 = contentIdentity is "SimpleChar:782DE568" or "SimpleChar:79135F51" ? 0 : 1
    };

    public static KnuBotCloseChatWindowMessage Close(Identity player, Identity npc) => new()
    { Identity = player, Target = npc, Unknown = 0, Unknown1 = 2, Seconds = 3, Unknown3 = 0 };

    public static IEnumerable<MessageBody> Node(Identity player, Identity npc, string? playerName, DialogueSessionResult result)
    {
        var node = result.CurrentNode;
        bool segments = false;
        if (node?.PromptSegments != null)
            foreach (var segment in node.PromptSegments)
            {
                if (segment?.Text == null) continue;
                segments = true;
                yield return Append(player, npc, Format(segment.Text.Replace("\\n", "\n"), playerName), segment.Unknown2);
            }
        if (!segments && !string.IsNullOrWhiteSpace(node?.PromptText))
            yield return Append(player, npc, Format(node.PromptText.Replace("\\n", "\n"), playerName), 0);
        var choices = DialogueCatalog.VisibleOptions(result);
        if (choices.Length > 0)
            yield return new KnuBotAnswerListMessage
            {
                Identity = player, Target = npc, Unknown1 = 2,
                DialogOptions = choices.Select(option => new KnuBotDialogOption { Text = Format(option.Text, playerName) }).ToArray()
            };
    }

    static KnuBotAppendTextMessage Append(Identity player, Identity npc, string text, int segmentKind) => new()
    { Identity = player, Target = npc, Unknown1 = 2, Unknown2 = segmentKind, Text = text };

    static string Format(string text, string? name) => text.Replace("{player}", string.IsNullOrWhiteSpace(name) ? "stranger" : name);

    public static KnuBotStartTradeMessage StartTrade(Identity player, Identity npc, DialogueActionOutcome trade) => new()
    {
        Identity = player, Target = npc, Unknown1 = 2, NumberOfItemSlotsInTradeWindow = trade == DialogueActionOutcome.StanTrade ? 4 : 1,
        Message = trade switch
        {
            DialogueActionOutcome.DojaTrade => "Drag and drop the item(s) you want to give to Scarlett Dalquist into one of the slots available and press \"accept\"",
            DialogueActionOutcome.StanTrade => "Drag and drop the item(s) you want to give to Stanley Goodman into one of the slots available and press \"accept\"",
            DialogueActionOutcome.SarahTrade => "Drag and drop the item(s) you want to give to Sarah Greene into one of the slots available and press \"accept\"",
            _ => throw new InvalidOperationException("Unsupported NPC trade prompt.")
        }
    };

    public static KnuBotRejectedItemsMessage AcceptedTrade(Identity player, Identity npc) => new()
    { Identity = player, Target = npc, Unknown1 = 2, Unknown2 = 0, Items = Array.Empty<KnuBotRejectedItem>() };
}
