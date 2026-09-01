using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace AORebirth.MissionEvidence
{
    public sealed class MissionOfferHarvester : AOPluginEntry
    {
        private const double DefaultIntervalSeconds = 2.0;
        private const double MinimumIntervalSeconds = 1.5;
        private const double RequestTimeoutSeconds = 30.0;

        private MissionTerminal _terminal;
        private JsonLineJournal _journal;
        private string _sessionId;
        private string _pendingRequestId;
        private IDictionary<string, object> _pendingRollOrigin;
        private DateTime _pendingSinceUtc;
        private DateTime _nextRequestUtc;
        private int _characterLevel;
        private int _difficultySlot;
        private int _targetMissionQl;
        private int _requestedRequestCount;
        private int _issuedRequestCount;
        private int _completedCohortCount;
        private int _harvestedOfferCount;
        private double _intervalSeconds;
        private bool _active;
        private string _sessionDirectory;
        private string _sessionOutputPath;
        private string _lastStopReason;
        private string _lastCohortFingerprint;
        private string _lastCohortRequestId;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        [Obsolete]
        public override void Run(string pluginDir)
        {
            Network.N3MessageReceived += OnN3MessageReceived;
            Game.OnUpdate += OnUpdate;
            Chat.RegisterCommand("missionharvest", OnCommand);
            Chat.WriteLine("Mission evidence harvester 1.2.1 loaded (roll origin + mission destination + canonical type + rewards). Select a mission terminal, then use /missionharvest start <targetQL> <requests> [intervalSeconds].", ChatColor.Gold);
        }

        public override void Teardown()
        {
            Stop("plugin_teardown", false);
            Network.N3MessageReceived -= OnN3MessageReceived;
            Game.OnUpdate -= OnUpdate;
        }

        private void OnCommand(string command, string[] parameters, ChatWindow chatWindow)
        {
            if (parameters.Length == 1 && string.Equals(parameters[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                Stop("user_stop", true);
                return;
            }
            if (parameters.Length == 1 && string.Equals(parameters[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                WriteStatus();
                return;
            }
            if ((parameters.Length < 3 || parameters.Length > 4)
                || !string.Equals(parameters[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                WriteUsage();
                return;
            }
            int targetQl;
            int requestCount;
            double interval = DefaultIntervalSeconds;
            if (!int.TryParse(parameters[1], out targetQl)
                || targetQl < 1
                || targetQl > 250
                || !int.TryParse(parameters[2], out requestCount)
                || requestCount < 1
                || requestCount > 100000
                || (parameters.Length == 4
                    && !double.TryParse(
                        parameters[3],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out interval)))
            {
                Chat.WriteLine("Invalid start arguments.");
                WriteUsage();
                return;
            }
            if (interval < MinimumIntervalSeconds)
            {
                Chat.WriteLine("Interval must be at least 1.5 seconds.");
                return;
            }
            if (_active)
            {
                Chat.WriteLine(
                    "A mission evidence session is already active. Stop it before starting another target.",
                    ChatColor.Red);
                return;
            }
            if (_terminal == null || !_terminal.IsValid)
            {
                Chat.WriteLine("Select/use an ordinary mission terminal before starting.");
                return;
            }
            int characterLevel = DynelManager.LocalPlayer.GetStat(Stat.Level);
            int slot;
            if (!MissionQlResolver.TryResolveFirstSlot(
                    characterLevel,
                    targetQl,
                    out slot))
            {
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Target QL {0} is not exactly rollable by character level {1}; no request was sent.",
                        targetQl,
                        characterLevel),
                    ChatColor.Red);
                return;
            }
            Start(characterLevel, slot, targetQl, requestCount, interval);
        }

        private static void WriteUsage()
        {
            Chat.WriteLine(
                "Usage: /missionharvest start <targetQL 1-250> <requests 1-100000> [intervalSeconds] | stop | status");
        }

        private void WriteStatus()
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                Chat.WriteLine("Mission harvester has not started a session.");
                return;
            }

            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Harvester active={0}; captureContract=2; session={1}; level={2}; targetQL={3}; slot={4}; requests={5}/{6}; completeCohorts={7}; harvestedOffers={8}; pending={9}; stopReason={10}; output={11}",
                    _active,
                    _sessionId,
                    _characterLevel,
                    _targetMissionQl,
                    _difficultySlot,
                    _issuedRequestCount,
                    _requestedRequestCount,
                    _completedCohortCount,
                    _harvestedOfferCount,
                    _pendingRequestId ?? "none",
                    _lastStopReason ?? "none",
                    _sessionOutputPath ?? "unknown"));
        }

        private void Start(
            int characterLevel,
            int slot,
            int targetQl,
            int requestCount,
            double interval)
        {
            if (_active || _journal != null)
                Stop("replaced_by_new_session", true);
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            _sessionId = string.Format("mission-{0}-{1}-{2}", stamp, DynelManager.LocalPlayer.Identity.Instance, Guid.NewGuid().ToString("N").Substring(0, 8));
            _sessionDirectory = System.IO.Path.Combine(PluginDataDirectory.FullName, "sessions", _sessionId);
            _sessionOutputPath = System.IO.Path.Combine(_sessionDirectory, "events.jsonl");
            _journal = new JsonLineJournal(_sessionOutputPath);
            _characterLevel = characterLevel;
            _difficultySlot = slot;
            _targetMissionQl = targetQl;
            _requestedRequestCount = requestCount;
            _intervalSeconds = interval;
            _issuedRequestCount = 0;
            _completedCohortCount = 0;
            _harvestedOfferCount = 0;
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _lastCohortFingerprint = null;
            _lastCohortRequestId = null;
            _lastStopReason = null;
            _active = true;
            _nextRequestUtc = DateTime.UtcNow;
            _journal.Append("session_started", _sessionId, null, BuildSessionPayload());
            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mission evidence session started: {0}; captureContract=2; level={1}; targetQL={2}; slot={3}; requests={4}; output={5}",
                    _sessionId,
                    _characterLevel,
                    _targetMissionQl,
                    _difficultySlot,
                    _requestedRequestCount,
                    _sessionOutputPath),
                ChatColor.Gold);
        }

        private void Stop(string reason, bool announce)
        {
            bool hadActiveSession = _active || _journal != null;
            if (_journal != null)
            {
                _journal.Append("session_stopped", _sessionId, null, new Dictionary<string, object>
                {
                    ["reason"] = reason,
                    ["issued_request_count"] = _issuedRequestCount,
                    ["completed_cohort_count"] = _completedCohortCount,
                    ["harvested_offer_count"] = _harvestedOfferCount,
                    ["pending_request_id"] = _pendingRequestId
                });
                _journal.Dispose();
                _journal = null;
            }
            _active = false;
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _lastStopReason = reason;
            if (announce && hadActiveSession)
            {
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mission evidence session stopped: {0}; reason={1}; requests={2}/{3}; completeCohorts={4}; harvestedOffers={5}; output={6}",
                        _sessionId,
                        reason,
                        _issuedRequestCount,
                        _requestedRequestCount,
                        _completedCohortCount,
                        _harvestedOfferCount,
                        _sessionOutputPath ?? "unknown"),
                    ChatColor.Gold);
            }
            else if (announce)
            {
                Chat.WriteLine("No mission evidence session is active.");
            }
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            if (!_active)
                return;
            DateTime now = DateTime.UtcNow;
            if (_pendingRequestId != null)
            {
                if ((now - _pendingSinceUtc).TotalSeconds >= RequestTimeoutSeconds)
                {
                    _journal.Append("request_timeout", _sessionId, _pendingRequestId, new Dictionary<string, object>
                    {
                        ["timeout_seconds"] = RequestTimeoutSeconds,
                        ["possible_causes"] = new[] { "terminal_rejection", "insufficient_credits", "network_interruption", "disconnect", "unknown" }
                    });
                    _pendingRequestId = null;
                    _nextRequestUtc = now.AddSeconds(_intervalSeconds);
                }
                return;
            }
            if (_issuedRequestCount >= _requestedRequestCount)
            {
                Stop("requested_count_completed", true);
                return;
            }
            if (now < _nextRequestUtc)
                return;
            if (_terminal == null || !_terminal.IsValid)
            {
                _journal.Append("error", _sessionId, null, new Dictionary<string, object> { ["code"] = "TERMINAL_NO_LONGER_VALID" });
                Stop("terminal_invalid", true);
                return;
            }
            IssueRequest(now);
        }

        private void IssueRequest(DateTime now)
        {
            _issuedRequestCount++;
            _pendingRequestId = string.Format("{0}/request/{1:D8}", _sessionId, _issuedRequestCount);
            _pendingSinceUtc = now;
            _pendingRollOrigin = BuildRollOriginPayload("REQUEST_STARTED");
            _journal.Append("request_started", _sessionId, _pendingRequestId, new Dictionary<string, object>
            {
                ["request_sequence"] = _issuedRequestCount,
                ["character_level"] = _characterLevel,
                ["difficulty_slot"] = _difficultySlot,
                ["target_mission_ql"] = _targetMissionQl,
                ["target_mission_ql_semantics"] = "exact_character_level_table_lookup_resolved_to_first_matching_difficulty_slot",
                ["mission_ql_table_source"] = MissionQlResolver.SourceRepositoryPath,
                ["mission_ql_table_sha256"] = MissionQlResolver.SourceSha256,
                ["roll_origin"] = _pendingRollOrigin,
                ["sliders"] = SliderPayload((byte)_difficultySlot, 255, 255, 255, 255, 255, 255)
            });
            _terminal.RequestMissions((byte)_difficultySlot, 255, 255, 255, 255, 255, 255);
        }

        private void OnN3MessageReceived(object sender, N3Message message)
        {
            GenericCmdMessage generic = message as GenericCmdMessage;
            if (generic != null && generic.Identity == DynelManager.LocalPlayer.Identity &&
                generic.Action == GenericCmdAction.Use && generic.Target.Type == IdentityType.MissionTerminal)
            {
                Dynel dynel = DynelManager.GetDynel(generic.Target);
                if (dynel != null)
                    _terminal = new MissionTerminal(dynel);
                return;
            }
            QuestAlternativeMessage response = message as QuestAlternativeMessage;
            if (response == null || !_active)
                return;
            if (_terminal == null || response.Terminal != _terminal.Identity)
            {
                _journal.Append("error", _sessionId, _pendingRequestId, new Dictionary<string, object>
                {
                    ["code"] = "RESPONSE_TERMINAL_MISMATCH",
                    ["response_terminal"] = IdentityPayload(response.Terminal),
                    ["selected_terminal"] = _terminal == null ? null : IdentityPayload(_terminal.Identity)
                });
                return;
            }
            if (_pendingRequestId == null)
            {
                string unmatchedFingerprint;
                IDictionary<string, object> unmatchedPayload = BuildCohortPayload(response, out unmatchedFingerprint);
                if (unmatchedFingerprint == _lastCohortFingerprint)
                {
                    _journal.Append("duplicate_callback", _sessionId, _lastCohortRequestId, new Dictionary<string, object>
                    {
                        ["callback_fingerprint"] = unmatchedFingerprint,
                        ["original_event_type"] = "cohort_received"
                    });
                }
                else
                {
                    _journal.Append("error", _sessionId, null, new Dictionary<string, object>
                    {
                        ["code"] = "UNMATCHED_COHORT_RAW_PRESERVED",
                        ["cohort"] = unmatchedPayload
                    });
                }
                return;
            }
            HandleCohort(response);
        }

        private void HandleCohort(QuestAlternativeMessage response)
        {
            string fingerprint;
            IDictionary<string, object> payload = BuildCohortPayload(response, out fingerprint);
            string completedRequestId = _pendingRequestId;
            _journal.Append("cohort_received", _sessionId, completedRequestId, payload);
            _lastCohortFingerprint = fingerprint;
            _lastCohortRequestId = completedRequestId;
            _completedCohortCount++;
            _harvestedOfferCount +=
                response.MissionDetails == null ? 0 : response.MissionDetails.Length;
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _nextRequestUtc = DateTime.UtcNow.AddSeconds(_intervalSeconds);
        }

        private IDictionary<string, object> BuildCohortPayload(QuestAlternativeMessage response, out string fingerprint)
        {
            MissionInfo[] details = response.MissionDetails ?? new MissionInfo[0];
            IDictionary<string, object> rollOrigin =
                _pendingRollOrigin ?? BuildRollOriginPayload("UNMATCHED_RESPONSE_CURRENT_SNAPSHOT");
            var offers = new List<object>();
            for (int index = 0; index < details.Length; index++)
                offers.Add(OfferPayload(details[index], index + 1, rollOrigin));
            var envelope = new Dictionary<string, object>
            {
                ["identity"] = IdentityPayload(response.Identity),
                ["packet_type"] = (int)response.PacketType,
                ["n3_message_type"] = (int)response.N3MessageType,
                ["unknown"] = response.Unknown,
                ["unknown1"] = response.Unknown1,
                ["unknown2"] = response.Unknown2,
                ["scope"] = (int)response.Scope,
                ["terminal"] = IdentityPayload(response.Terminal)
            };
            var payload = new Dictionary<string, object>
            {
                ["message_envelope"] = envelope,
                ["roll_origin"] = rollOrigin,
                ["sliders"] = SliderPayload(response.MissionSliders.Difficulty, response.MissionSliders.GoodBad, response.MissionSliders.OrderChaos, response.MissionSliders.OpenHidden, response.MissionSliders.PhysicalMystical, response.MissionSliders.HeadonStealth, response.MissionSliders.CreditsXp),
                ["offers"] = offers
            };
            fingerprint = Sha256(_serializer.Serialize(payload));
            payload["callback_fingerprint"] = fingerprint;
            return payload;
        }

        private IDictionary<string, object> BuildSessionPayload()
        {
            LocalPlayer player = DynelManager.LocalPlayer;
            Vector3 terminalPosition = _terminal.Position;
            return new Dictionary<string, object>
            {
                ["character_surrogate"] = Sha256(player.Name + ":" + player.Identity.Instance.ToString(CultureInfo.InvariantCulture)),
                ["character_identity_raw"] = IdentityPayload(player.Identity),
                ["character_level"] = StatValue(player, "Level"),
                ["profession_raw"] = StatValue(player, "Profession"),
                ["breed_raw"] = StatValue(player, "Breed"),
                ["faction_side_raw"] = StatValue(player, "Side"),
                ["organization_id_raw"] = StatValue(player, "Clan"),
                ["organization_rank_raw"] = StatValue(player, "ClanLevel"),
                ["organization_side"] = null,
                ["organization_side_availability"] = "NOT_DIRECTLY_EXPOSED_BY_INSPECTED_AOSHARP_PATH",
                ["terminal_identity"] = IdentityPayload(_terminal.Identity),
                ["terminal_playfield"] = IdentityPayload(Playfield.ModelIdentity),
                ["terminal_coordinates"] = VectorPayload(terminalPosition),
                ["roll_origin"] = BuildRollOriginPayload("SESSION_STARTED"),
                ["difficulty_slot"] = _difficultySlot,
                ["target_mission_ql"] = _targetMissionQl,
                ["target_resolution"] = "EXACT_FIRST_MATCHING_SLOT",
                ["mission_ql_table_source"] = MissionQlResolver.SourceRepositoryPath,
                ["mission_ql_table_sha256"] = MissionQlResolver.SourceSha256,
                ["requested_request_count"] = _requestedRequestCount,
                ["minimum_request_interval_seconds"] = MinimumIntervalSeconds,
                ["configured_request_interval_seconds"] = _intervalSeconds,
                ["one_outstanding_request_only"] = true,
                ["session_output_directory"] = _sessionDirectory,
                ["session_output_path"] = _sessionOutputPath,
                ["client_version_label"] = null,
                ["client_version_availability"] = "NOT_EXPOSED_BY_INSPECTED_AOSHARP_API",
                ["aosharp_expected_package_version"] = "1.0.106",
                ["aosharp_observed_assembly_version"] = typeof(AOPluginEntry).Assembly.GetName().Version.ToString(),
                ["harvester_version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                ["capture_contract_version"] = 2,
                ["mission_type_catalog_source"] = "docs/generated/missions/malis/mission-type-catalog.json",
                ["aosharp_mission_info_public_fields_captured"] = new[]
                {
                    "Credits", "Description", "Location", "MissionIcon",
                    "MissionIdentity", "MissionItemData", "Playfield",
                    "RewardDescriptorVersion", "TerminalIdentity", "Title",
                    "Unk1", "UnkChunk1", "UnkChunk2", "UnkChunk3",
                    "UnkChunk4", "UnkChunk5", "UnkChunk6", "XpReward"
                },
                ["raw_event_format"] = "incremental_jsonl_flush_true"
            };
        }

        private static IDictionary<string, object> OfferPayload(
            MissionInfo offer,
            int offerIndex,
            IDictionary<string, object> rollOrigin)
        {
            var rewards = new List<object>();
            foreach (MissionItemReward reward in offer.MissionItemData ?? new MissionItemReward[0])
            {
                rewards.Add(new Dictionary<string, object>
                {
                    ["low_id"] = reward.LowId,
                    ["high_id"] = reward.HighId,
                    ["ql"] = reward.Ql,
                    ["unknown"] = reward.Unk,
                    ["identity_semantics"] = reward.LowId == reward.HighId
                        ? "EXACT_TEMPLATE_ID_AND_QL"
                        : "LOW_HIGH_TEMPLATE_PAIR_AND_QL"
                });
            }
            IDictionary<string, object> destination = new Dictionary<string, object>
            {
                ["playfield_identity"] = IdentityPayload(offer.Playfield),
                ["coordinates"] = VectorPayload(offer.Location),
                ["availability"] = "DIRECT_AOSHARP_MISSIONINFO"
            };
            return new Dictionary<string, object>
            {
                ["offer_index"] = offerIndex,
                ["mission_identity"] = IdentityPayload(offer.MissionIdentity),
                ["title"] = offer.Title,
                ["description"] = offer.Description,
                ["terminal_identity"] = IdentityPayload(offer.TerminalIdentity),
                ["reward_descriptor_version"] = offer.RewardDescriptorVersion,
                ["credits"] = offer.Credits,
                ["xp_reward"] = offer.XpReward,
                ["mission_items"] = rewards,
                ["reward_items"] = rewards,
                ["reward_item_count"] = rewards.Count,
                ["mission_icon"] = offer.MissionIcon,
                ["mission_type"] = MissionTypePayload(offer.MissionIcon),
                ["playfield"] = IdentityPayload(offer.Playfield),
                ["location"] = VectorPayload(offer.Location),
                ["mission_destination"] = destination,
                ["roll_origin"] = rollOrigin,
                ["unknown_fields"] = new Dictionary<string, object>
                {
                    ["Unk1"] = offer.Unk1,
                    ["UnkChunk1Base64"] = Base64(offer.UnkChunk1),
                    ["UnkChunk2Base64"] = Base64(offer.UnkChunk2),
                    ["UnkChunk3Base64"] = Base64(offer.UnkChunk3),
                    ["UnkChunk4Base64"] = Base64(offer.UnkChunk4),
                    ["UnkChunk5Base64"] = Base64(offer.UnkChunk5),
                    ["UnkChunk6Base64"] = Base64(offer.UnkChunk6)
                },
                ["not_exposed_fields"] = new Dictionary<string, object>
                {
                    ["mission_ql"] = null,
                    ["mission_template_or_type_id"] = null,
                    ["objective_item_identity"] = null,
                    ["objective_item_ql"] = null,
                    ["objective_type"] = null,
                    ["token_reward"] = null,
                    ["destination_entrance_identity"] = null,
                    ["faction_requirements"] = null
                }
            };
        }

        private IDictionary<string, object> BuildRollOriginPayload(string capturePhase)
        {
            LocalPlayer player = DynelManager.LocalPlayer;
            return new Dictionary<string, object>
            {
                ["capture_phase"] = capturePhase,
                ["captured_at_utc"] = DateTime.UtcNow.ToString("o"),
                ["terminal_identity"] = _terminal == null ? null : IdentityPayload(_terminal.Identity),
                ["terminal_name"] = _terminal == null ? null : _terminal.Name,
                ["terminal_playfield_identity"] = IdentityPayload(Playfield.ModelIdentity),
                ["terminal_local_coordinates"] = _terminal == null ? null : VectorPayload(_terminal.Position),
                ["terminal_global_coordinates"] = _terminal == null ? null : VectorPayload(_terminal.GlobalPosition),
                ["terminal_rotation"] = _terminal == null ? null : QuaternionPayload(_terminal.Rotation),
                ["terminal_is_valid"] = _terminal != null && _terminal.IsValid,
                ["player_identity"] = player == null ? null : IdentityPayload(player.Identity),
                ["player_playfield_identity"] = IdentityPayload(Playfield.ModelIdentity),
                ["player_coordinates"] = player == null ? null : VectorPayload(player.Position),
                ["provenance"] = "REQUEST_TIME_CLIENT_DYNEL_SNAPSHOT"
            };
        }

        private static IDictionary<string, object> MissionTypePayload(int missionIcon)
        {
            string captureBackedType = null;
            string canonicalType = null;
            string canonicalDisplayName = null;
            string malisDisplayName = null;
            string clickSaverWireCode = null;
            switch (missionIcon)
            {
                case 11329:
                    captureBackedType = "FindItemReturn";
                    canonicalType = "RETURN_ITEM";
                    canonicalDisplayName = "Return Item";
                    malisDisplayName = "Return Item";
                    clickSaverWireCode = "0x2C41";
                    break;
                case 11330:
                    captureBackedType = "KillPerson";
                    canonicalType = "KILL_PERSON";
                    canonicalDisplayName = "Kill Person";
                    malisDisplayName = "Kill Target";
                    clickSaverWireCode = "0x2C42";
                    break;
                case 11335:
                    captureBackedType = "FindPerson";
                    canonicalType = "FIND_PERSON";
                    canonicalDisplayName = "Find Person";
                    malisDisplayName = "Find Target";
                    clickSaverWireCode = "0x2C47";
                    break;
                case 11337:
                    captureBackedType = "FindItem";
                    canonicalType = "FIND_ITEM";
                    canonicalDisplayName = "Find Item";
                    malisDisplayName = "Find Item";
                    clickSaverWireCode = "0x2C49";
                    break;
                case 11342:
                    captureBackedType = "RepairMachine";
                    canonicalType = "REPAIR";
                    canonicalDisplayName = "Repair";
                    malisDisplayName = "Use Item";
                    clickSaverWireCode = "0x2C4E";
                    break;
            }
            return new Dictionary<string, object>
            {
                ["mission_icon"] = missionIcon,
                ["capture_backed_type"] = captureBackedType,
                ["canonical_type"] = canonicalType,
                ["canonical_display_name"] = canonicalDisplayName,
                ["malis_display_name"] = malisDisplayName,
                ["clicksaver_wire_code"] = clickSaverWireCode,
                ["classification_status"] = captureBackedType == null
                    ? "UNKNOWN_ICON_RAW_VALUE_PRESERVED"
                    : "CAPTURE_BACKED_EXACT_ICON_MAPPING",
                ["catalog_source"] = "docs/generated/missions/malis/mission-type-catalog.json"
            };
        }

        private static IDictionary<string, object> SliderPayload(byte difficulty, byte goodBad, byte orderChaos, byte openHidden, byte physicalMystical, byte headonStealth, byte creditsXp)
        {
            return new Dictionary<string, object>
            {
                ["difficulty"] = difficulty,
                ["good_bad"] = goodBad,
                ["order_chaos"] = orderChaos,
                ["open_hidden"] = openHidden,
                ["physical_mystical"] = physicalMystical,
                ["headon_stealth"] = headonStealth,
                ["credits_xp"] = creditsXp
            };
        }

        private static IDictionary<string, object> IdentityPayload(Identity identity)
        {
            return new Dictionary<string, object> { ["type"] = (int)identity.Type, ["instance"] = identity.Instance };
        }

        private static IDictionary<string, object> VectorPayload(Vector3 vector)
        {
            return new Dictionary<string, object> { ["x"] = vector.X, ["y"] = vector.Y, ["z"] = vector.Z };
        }

        private static IDictionary<string, object> QuaternionPayload(Quaternion quaternion)
        {
            return new Dictionary<string, object>
            {
                ["x"] = quaternion.X,
                ["y"] = quaternion.Y,
                ["z"] = quaternion.Z,
                ["w"] = quaternion.W
            };
        }

        private static object StatValue(Dynel dynel, string statName)
        {
            Stat stat;
            return Enum.TryParse(statName, out stat) ? (object)dynel.GetStat(stat) : null;
        }

        private static string Base64(byte[] bytes)
        {
            return bytes == null ? null : Convert.ToBase64String(bytes);
        }

        private static string Sha256(string text)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
