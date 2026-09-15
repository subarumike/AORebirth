namespace ZoneEngine_New.Core.Missions;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;

/// <summary>Projects editable journal templates using the existing packet contract. No content routing.</summary>
internal static class AuthoredQuestJournal
{
    internal static bool Send(Player player, string questId, DateTime now, InteractionContent content, int? remainingSeconds = null)
    {
        if (!content.Journals.TryGetValue(questId, out var definition) || player.Session == null) return false;
        if (definition.SynchronizeClock)
        {
            player.Session.Send(new GameTimeMessage { Identity = player.Identity, Unknown1 = definition.ClockTime,
                Unknown3 = definition.ClockUnknown3, Unknown4 = definition.ClockUnknown4 });
            if (player.Session is IGameTimeSession clock) clock.RecordGameTimeSynchronization(now);
        }
        if (definition.Packet is { } typed)
        {
            var json = JsonNode.Parse(typed.GetRawText())!;
            ReplaceRecipient(json, definition.RecipientMarker, player.Identity.Instance);
            player.Session.Send(json.Deserialize<QuestFullUpdateMessage>(InteractionContent.JsonOptions)!);
        }
        else
        {
            byte[] bytes = Convert.FromHexString(definition.Hex);
            Replace(bytes, definition.RecipientMarker, player.Identity.Instance);
            if (definition.ExpiryOffset >= 0)
            {
                long elapsed = player.Session is IGameTimeSession { GameTimeSynchronizedAtUtc: { } anchor } ? Math.Max(0, (long)(now-anchor).TotalSeconds) : 0;
                int duration = Math.Min(remainingSeconds ?? definition.DurationSeconds, definition.DurationSeconds);
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(definition.ExpiryOffset, 4), unchecked((int)(definition.ClockBaseSeconds + elapsed + duration)));
            }
            player.Session.Send(bytes);
        }
        return true;
    }
    static void ReplaceRecipient(JsonNode node, int from, int to)
    {
        if (node is JsonObject obj)
        {
            if (obj["Type"] is JsonValue type && type.TryGetValue<int>(out int kind) && kind == (int)IdentityType.CanbeAffected
                && obj["Instance"] is JsonValue id && id.TryGetValue<int>(out int instance) && instance == from) obj["Instance"] = to;
            foreach (var item in obj) if (item.Value != null) ReplaceRecipient(item.Value, from, to);
        }
        else if (node is JsonArray array) foreach (var item in array) if (item != null) ReplaceRecipient(item, from, to);
    }
    internal static void Delete(Player player, string questId, InteractionContent content)
    {
        if (!content.Journals.TryGetValue(questId, out var definition)) return;
        int instance = int.Parse(questId[(questId.LastIndexOf(':') + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        foreach (string hex in definition.DeleteHex)
        {
            byte[] packet = Convert.FromHexString(hex);
            Replace(packet, definition.DeleteRecipientMarker, player.Identity.Instance);
            Replace(packet, definition.DeleteQuestMarker, instance);
            player.Session?.Send(packet);
        }
        if (definition.TypedDelete) player.Session?.Send(new QuestMessage { Identity = player.Identity, Unknown = 0,
            Action = QuestAction.Delete, Unknown1 = 0, Mission = new() { Type = IdentityType.Mission, Instance = instance }, Unknown2 = 0, Unknown3 = 0 });
    }
    static void Replace(byte[] packet, int from, int to)
    {
        for (int i = 0; i + 4 <= packet.Length; i++)
            if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(i, 4)) == from)
            { BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(i, 4), to); i += 3; }
    }
}
