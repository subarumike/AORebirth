#region License

// Copyright (c) 2005-2014, CellAO Team
//
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    public static class WorldEntrySummary
    {
        private static readonly TimeSpan ActiveLifetime = TimeSpan.FromSeconds(30);

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<int, Summary> ActiveSummaries = new Dictionary<int, Summary>();

        // Replacement-client comparison labels: SimpleCharFullUpdateIIR_t,
        // PlayfieldAnarchyFIIR_t, DoorStatusUpdateIIR_t, WeaponItemFullUpdateIIR_t.
        public static void Begin(ZoneClient client, string phase)
        {
            try
            {
                int characterId;
                string characterIdentity;
                string playfieldIdentity;
                if (!TryGetCharacterContext(client, out characterId, out characterIdentity, out playfieldIdentity))
                {
                    return;
                }

                lock (SyncRoot)
                {
                    CleanupExpiredSummaries();
                    ActiveSummaries[characterId] = new Summary(phase, characterIdentity, playfieldIdentity);
                }
            }
            catch (Exception exception)
            {
                LogDiagnosticError("begin", exception);
            }
        }

        public static void RecordOutboundMessage(ZoneClient client, MessageBody messageBody)
        {
            try
            {
                int characterId;
                string characterIdentity;
                string playfieldIdentity;
                if (!TryGetCharacterContext(client, out characterId, out characterIdentity, out playfieldIdentity))
                {
                    return;
                }

                var n3Message = messageBody as N3Message;
                if (n3Message == null)
                {
                    return;
                }

                lock (SyncRoot)
                {
                    CleanupExpiredSummaries();

                    Summary summary;
                    if (!ActiveSummaries.TryGetValue(characterId, out summary))
                    {
                        return;
                    }

                    summary.Record(n3Message);
                }
            }
            catch (Exception exception)
            {
                LogDiagnosticError("record", exception);
            }
        }

        public static void Complete(ZoneClient client)
        {
            try
            {
                int characterId;
                string characterIdentity;
                string playfieldIdentity;
                if (!TryGetCharacterContext(client, out characterId, out characterIdentity, out playfieldIdentity))
                {
                    return;
                }

                Summary summary = null;
                lock (SyncRoot)
                {
                    CleanupExpiredSummaries();
                    if (ActiveSummaries.TryGetValue(characterId, out summary))
                    {
                        ActiveSummaries.Remove(characterId);
                    }
                }

                if (summary != null)
                {
                    LogUtil.Debug(DebugInfoDetail.Engine, summary.Format());
                }
            }
            catch (Exception exception)
            {
                LogDiagnosticError("complete", exception);
            }
        }

        private static bool TryGetCharacterContext(
            ZoneClient client,
            out int characterId,
            out string characterIdentity,
            out string playfieldIdentity)
        {
            characterId = 0;
            characterIdentity = "none";
            playfieldIdentity = "none";

            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return false;
            }

            characterId = client.Controller.Character.Identity.Instance;
            characterIdentity = FormatIdentity(client.Controller.Character.Identity);
            if (client.Controller.Character.Playfield != null)
            {
                playfieldIdentity = FormatIdentity(client.Controller.Character.Playfield.Identity);
            }

            return characterId != 0;
        }

        private static void CleanupExpiredSummaries()
        {
            DateTime now = DateTime.UtcNow;
            var expired = new List<int>();
            foreach (KeyValuePair<int, Summary> entry in ActiveSummaries)
            {
                if (entry.Value.ExpiresAt <= now)
                {
                    expired.Add(entry.Key);
                }
            }

            foreach (int characterId in expired)
            {
                ActiveSummaries.Remove(characterId);
            }
        }

        private static void LogDiagnosticError(string action, Exception exception)
        {
            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "world_entry_summary_error action={0} error={1}",
                    action,
                    exception.Message));
        }

        private static bool IsStableIdentity(Identity identity)
        {
            return identity.Type != IdentityType.None || identity.Instance != 0;
        }

        private static string FormatIdentity(Identity identity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:X8}",
                (int)identity.Type,
                identity.Instance);
        }

        private static void Increment(IDictionary<string, int> counts, string key)
        {
            int count;
            if (!counts.TryGetValue(key, out count))
            {
                count = 0;
            }

            counts[key] = count + 1;
        }

        private sealed class Summary
        {
            private readonly Dictionary<string, int> categoryCounts = new Dictionary<string, int>();

            private readonly Dictionary<string, int> messageTypeCounts = new Dictionary<string, int>();

            private readonly HashSet<string> stableObjectKeys = new HashSet<string>();

            public Summary(string phase, string characterIdentity, string playfieldIdentity)
            {
                this.Phase = string.IsNullOrEmpty(phase) ? "world_entry" : phase;
                this.CharacterIdentity = characterIdentity;
                this.PlayfieldIdentity = playfieldIdentity;
                this.ExpiresAt = DateTime.UtcNow + ActiveLifetime;
            }

            public DateTime ExpiresAt { get; private set; }

            private string CharacterIdentity { get; set; }

            private int DoorStatusLike { get; set; }

            private int InvalidObjects { get; set; }

            private int NoDecodedPosition { get; set; }

            private int ObjectsTotal { get; set; }

            private string Phase { get; set; }

            private string PlayfieldIdentity { get; set; }

            private int PlayfieldLike { get; set; }

            private int PositionBacked { get; set; }

            private int SimpleCharLike { get; set; }

            private int WeaponItemLike { get; set; }

            public void Record(N3Message message)
            {
                Classification classification = Classify(message);
                if (!classification.Record)
                {
                    return;
                }

                this.ExpiresAt = DateTime.UtcNow + ActiveLifetime;
                this.ObjectsTotal++;
                Increment(this.messageTypeCounts, message.GetType().Name);
                Increment(this.categoryCounts, classification.Category);

                if (classification.HasPosition)
                {
                    this.PositionBacked++;
                }
                else
                {
                    this.NoDecodedPosition++;
                }

                if (classification.Category == "simple_char_like")
                {
                    this.SimpleCharLike++;
                }
                else if (classification.Category == "playfield_like")
                {
                    this.PlayfieldLike++;
                }
                else if (classification.Category == "door_status_like")
                {
                    this.DoorStatusLike++;
                }
                else if (classification.Category == "weapon_item_like")
                {
                    this.WeaponItemLike++;
                }

                Identity stableIdentity;
                if (TryGetStableIdentity(message, out stableIdentity))
                {
                    this.stableObjectKeys.Add(FormatIdentity(stableIdentity));
                }
                else
                {
                    this.InvalidObjects++;
                }

                if (classification.ExpectsPosition && !classification.HasPosition)
                {
                    this.InvalidObjects++;
                }
            }

            public string Format()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "world_entry_summary phase={0} char={1} playfield={2} objects_total={3} message_types={4} categories={5} simple_char_like={6} playfield_like={7} door_status_like={8} weapon_item_like={9} position_backed={10} no_decoded_position={11} stable_id_count={12} invalid_objects={13}",
                    this.Phase,
                    this.CharacterIdentity,
                    this.PlayfieldIdentity,
                    this.ObjectsTotal,
                    FormatCounts(this.messageTypeCounts),
                    FormatCounts(this.categoryCounts),
                    this.SimpleCharLike,
                    this.PlayfieldLike,
                    this.DoorStatusLike,
                    this.WeaponItemLike,
                    this.PositionBacked,
                    this.NoDecodedPosition,
                    this.stableObjectKeys.Count,
                    this.InvalidObjects);
            }

            private static Classification Classify(N3Message message)
            {
                if (message is SimpleCharFullUpdateMessage)
                {
                    var simpleChar = (SimpleCharFullUpdateMessage)message;
                    return Classification.RecordObject("simple_char_like", true, simpleChar.Coordinates != null);
                }

                if (message is PlayfieldAnarchyFMessage)
                {
                    var playfield = (PlayfieldAnarchyFMessage)message;
                    return Classification.RecordObject("playfield_like", true, playfield.CharacterCoordinates != null);
                }

                if (message is DoorStatusUpdateMessage)
                {
                    return Classification.RecordObject("door_status_like", false, false);
                }

                if (message is WeaponItemFullUpdateMessage)
                {
                    return Classification.RecordObject("weapon_item_like", false, false);
                }

                if (message is VendingMachineFullUpdateMessage)
                {
                    var vendingMachine = (VendingMachineFullUpdateMessage)message;
                    return Classification.RecordObject(
                        "vending_machine_full_update",
                        true,
                        vendingMachine.Coordinates != null);
                }

                if (message is SimpleItemFullUpdateMessage)
                {
                    var simpleItem = (SimpleItemFullUpdateMessage)message;
                    return Classification.RecordObject("simple_item_full_update", true, simpleItem.Coordinate != null);
                }

                if (message is FullCharacterMessage)
                {
                    return Classification.RecordObject("full_character", false, false);
                }

                if (message is CharInPlayMessage)
                {
                    return Classification.RecordObject("char_in_play", false, false);
                }

                if (message is PlayfieldAllTowersMessage || message is PlayfieldAllCitiesMessage)
                {
                    return Classification.RecordObject("playfield_ready", false, false);
                }

                return Classification.Skip();
            }

            private static string FormatCounts(Dictionary<string, int> counts)
            {
                if (counts.Count == 0)
                {
                    return "none";
                }

                var keys = new List<string>(counts.Keys);
                keys.Sort(StringComparer.Ordinal);

                var builder = new StringBuilder();
                for (int index = 0; index < keys.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append('|');
                    }

                    string key = keys[index];
                    builder.Append(key);
                    builder.Append(':');
                    builder.Append(counts[key].ToString(CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }

            private static bool TryGetStableIdentity(N3Message message, out Identity identity)
            {
                identity = message.Identity;
                if (IsStableIdentity(identity))
                {
                    return true;
                }

                var vendingMachine = message as VendingMachineFullUpdateMessage;
                if (vendingMachine != null && IsStableIdentity(vendingMachine.NpcIdentity))
                {
                    identity = vendingMachine.NpcIdentity;
                    return true;
                }

                var simpleItem = message as SimpleItemFullUpdateMessage;
                if (simpleItem != null && IsStableIdentity(simpleItem.Owner))
                {
                    identity = simpleItem.Owner;
                    return true;
                }

                var weaponItem = message as WeaponItemFullUpdateMessage;
                if (weaponItem != null && IsStableIdentity(weaponItem.Owner))
                {
                    identity = weaponItem.Owner;
                    return true;
                }

                return false;
            }
        }

        private sealed class Classification
        {
            private Classification()
            {
            }

            public string Category { get; private set; }

            public bool ExpectsPosition { get; private set; }

            public bool HasPosition { get; private set; }

            public bool Record { get; private set; }

            public static Classification RecordObject(string category, bool expectsPosition, bool hasPosition)
            {
                return new Classification
                       {
                           Category = category,
                           ExpectsPosition = expectsPosition,
                           HasPosition = hasPosition,
                           Record = true
                       };
            }

            public static Classification Skip()
            {
                return new Classification { Record = false };
            }
        }
    }
}
