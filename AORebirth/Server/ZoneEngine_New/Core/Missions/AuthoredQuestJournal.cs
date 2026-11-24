namespace ZoneEngine_New.Core.Missions;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;

/// <summary>Projects editable journal templates using the existing packet contract. No content routing.</summary>
internal static class AuthoredQuestJournal
{
    internal static bool Send(Player player, string questId, DateTime now, InteractionContent content, int? remainingSeconds = null)
    {
        if (!content.Journals.TryGetValue(questId, out var definition) || player.Session is not { } session)
            return false;
        if (definition.SynchronizeClock)
        {
            // The shared world clock, as at world entry; the journal's expiry below is given in it.
            GameClock.Reading reading = GameClock.Now(
                player.Playfield?.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.WorldClock, now);
            if (session is IGameTimeSession clock)
                clock.RecordGameTimeSynchronization(now, reading.ServerSeconds);
            session.Send(GameClock.BuildMessage(player.Identity, reading));
            // The clock just moved: the perk reset timer follows it.
            player.SyncLastPerkResetTime();
            player.FlushDirtyStats();
        }
        var json = JsonNode.Parse(definition.Packet.GetRawText())!;
        ReplaceRecipient(json, definition.RecipientMarker, player.Identity.Instance);
        var packet = json.Deserialize<QuestFullUpdateMessage>(InteractionContent.JsonOptions)!;
        if (definition.ClientClockExpiryActionIndex is int index)
        {
            long clientNow = session is IGameTimeSession { GameTimeSynchronizedAtUtc: { } anchor } clock
                ? clock.GameTimeServerSeconds + Math.Max(0, (long)(now - anchor).TotalSeconds)
                : GameClock.UnixSeconds(now);
            int duration = Math.Min(remainingSeconds ?? definition.DurationSeconds, definition.DurationSeconds);
            Span<byte> expiry = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(expiry, unchecked((int)(clientNow + duration)));
            packet.Quests[0].QuestActions[index].UnknownHash1 = Encoding.Latin1.GetString(expiry);
        }
        session.Send(packet);
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
        if (!content.Journals.TryGetValue(questId, out var definition) || player.Session is not { } session)
            return;

        int instance = int.Parse(questId[(questId.LastIndexOf(':') + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var mission = new Identity { Type = IdentityType.Mission, Instance = instance };
        foreach (JournalDeletePacket kind in definition.DeletePackets)
        {
            MessageBody packet = kind switch
            {
                JournalDeletePacket.MissionAccomplished => new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.MissionAccomplished,
                    Target = mission,
                    Parameter1 = (int)mission.Type,
                    Parameter2 = mission.Instance
                },
                JournalDeletePacket.QuestDelete => new QuestMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = QuestAction.Delete,
                    Mission = mission
                },
                _ => throw new InvalidOperationException("Unknown journal deletion packet: " + kind)
            };
            session.Send(packet);
        }
    }
}
