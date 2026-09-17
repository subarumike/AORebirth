namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine_New.Core.Missions;

/// <summary>Exact existing KnuBot handler fields and content-router formatting; no reconstructed raw packets.</summary>
public static class DialogueWire
{
    public static KnuBotOpenChatWindowMessage Open(Identity player, Identity npc, int openMode) => new()
    {
        Identity = player, Target = npc, Unknown1 = 2,
        // Wire presentation mode is supplied by the editable dialogue binding.
        Unknown2 = openMode
    };

    public static KnuBotCloseChatWindowMessage Close(Identity player, Identity npc) => new()
    { Identity = player, Target = npc, Unknown = 0, Unknown1 = 2, Seconds = 3, Unknown3 = 0 };

    public static IEnumerable<MessageBody> Node(Identity player, Identity npc, string? playerName, DialogueStep result)
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

    public static KnuBotStartTradeMessage StartTrade(Identity player, Identity npc, DialogueRoute route) => new()
    { Identity = player, Target = npc, Unknown1 = 2, NumberOfItemSlotsInTradeWindow = route.TradeSlots, Message = route.TradePrompt };

    public static KnuBotRejectedItemsMessage AcceptedTrade(Identity player, Identity npc) => new()
    { Identity = player, Target = npc, Unknown1 = 2, Unknown2 = 0, Items = Array.Empty<KnuBotRejectedItem>() };
}
