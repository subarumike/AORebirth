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
        private DateTime _pendingSinceUtc;
        private DateTime _nextRequestUtc;
        private int _difficultySlot;
        private int _targetMissionQl;
        private int _requestedRequestCount;
        private int _issuedRequestCount;
        private int _completedCohortCount;
        private double _intervalSeconds;
        private bool _active;
        private bool _observeExternalRequests;
        private Identity _pendingTerminalIdentity;
        private bool _hasPendingTerminalIdentity;
        private string _lastCohortFingerprint;
        private string _lastCohortRequestId;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        [Obsolete]
        public override void Run(string pluginDir)
        {
            Network.N3MessageReceived += OnN3MessageReceived;
            Network.N3MessageSent += OnN3MessageSent;
            Game.OnUpdate += OnUpdate;
            Chat.RegisterCommand("missionharvest", OnCommand);
            Chat.WriteLine("Mission evidence harvester loaded. Use /missionharvest observe <targetQL> before rolling with Malis.", ChatColor.Gold);
        }

        public override void Teardown()
        {
            Stop("plugin_teardown");
            Network.N3MessageReceived -= OnN3MessageReceived;
            Network.N3MessageSent -= OnN3MessageSent;
            Game.OnUpdate -= OnUpdate;
        }

        private void OnCommand(string command, string[] parameters, ChatWindow chatWindow)
        {
            if (parameters.Length == 1 && string.Equals(parameters[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                Stop("user_stop");
                return;
            }
            if (parameters.Length == 1 && string.Equals(parameters[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                string mode = !_active ? "idle" : (_observeExternalRequests ? "observe" : "active");
                string requested = _active && _observeExternalRequests ? "continuous" : _requestedRequestCount.ToString(CultureInfo.InvariantCulture);
                Chat.WriteLine(string.Format("Harvester active={0}, mode={1}, requests={2}/{3}, complete cohorts={4}, pending={5}", _active, mode, _issuedRequestCount, requested, _completedCohortCount, _pendingRequestId ?? "none"));
                return;
            }
            if (parameters.Length == 2 && string.Equals(parameters[0], "observe", StringComparison.OrdinalIgnoreCase))
            {
                int observedTargetQl;
                if (!int.TryParse(parameters[1], out observedTargetQl) || observedTargetQl < 1 || observedTargetQl > 250)
                {
                    Chat.WriteLine("Target QL must be between 1 and 250.");
                    return;
                }
                if (_terminal == null || !_terminal.IsValid)
                {
                    Chat.WriteLine("Select/use an ordinary mission terminal before observing Malis.");
                    return;
                }
                StartObserve(observedTargetQl);
                return;
            }
            if (parameters.Length < 4 || !string.Equals(parameters[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                Chat.WriteLine("Usage: /missionharvest observe <targetQL> | start <slot 1-11> <targetQL> <requests> [intervalSeconds] | stop | status");
                return;
            }
            int slot;
            int targetQl;
            int requestCount;
            double interval = DefaultIntervalSeconds;
            if (!int.TryParse(parameters[1], out slot) || slot < 1 || slot > 11 ||
                !int.TryParse(parameters[2], out targetQl) || targetQl < 1 || targetQl > 250 ||
                !int.TryParse(parameters[3], out requestCount) || requestCount < 1 || requestCount > 100000 ||
                (parameters.Length >= 5 && !double.TryParse(parameters[4], NumberStyles.Float, CultureInfo.InvariantCulture, out interval)))
            {
                Chat.WriteLine("Invalid start arguments.");
                return;
            }
            if (interval < MinimumIntervalSeconds)
            {
                Chat.WriteLine("Interval must be at least 1.5 seconds.");
                return;
            }
            if (_terminal == null || !_terminal.IsValid)
            {
                Chat.WriteLine("Select/use an ordinary mission terminal before starting.");
                return;
            }
            StartActive(slot, targetQl, requestCount, interval);
        }

        private void StartActive(int slot, int targetQl, int requestCount, double interval)
        {
            BeginSession(slot, targetQl, requestCount, interval, false);
        }

        private void StartObserve(int targetQl)
        {
            BeginSession(0, targetQl, 0, 0, true);
        }

        private void BeginSession(int slot, int targetQl, int requestCount, double interval, bool observeExternalRequests)
        {
            Stop("replaced_by_new_session");
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            _sessionId = string.Format("mission-{0}-{1}-{2}", stamp, DynelManager.LocalPlayer.Identity.Instance, Guid.NewGuid().ToString("N").Substring(0, 8));
            string sessionDirectory = System.IO.Path.Combine(PluginDataDirectory.FullName, "sessions", _sessionId);
            _journal = new JsonLineJournal(System.IO.Path.Combine(sessionDirectory, "events.jsonl"));
            _difficultySlot = slot;
            _targetMissionQl = targetQl;
            _requestedRequestCount = requestCount;
            _intervalSeconds = interval;
            _observeExternalRequests = observeExternalRequests;
            _issuedRequestCount = 0;
            _completedCohortCount = 0;
            _pendingRequestId = null;
            _hasPendingTerminalIdentity = false;
            _lastCohortFingerprint = null;
            _lastCohortRequestId = null;
            _active = true;
            _nextRequestUtc = DateTime.UtcNow;
            _journal.Append("session_started", _sessionId, null, BuildSessionPayload());
            Chat.WriteLine("Mission evidence session started in " + (_observeExternalRequests ? "Malis observe" : "active request") + " mode: " + _sessionId, ChatColor.Gold);
        }

        private void Stop(string reason)
        {
            if (_journal != null)
            {
                _journal.Append("session_stopped", _sessionId, null, new Dictionary<string, object>
                {
                    ["reason"] = reason,
                    ["issued_request_count"] = _issuedRequestCount,
                    ["completed_cohort_count"] = _completedCohortCount,
                    ["pending_request_id"] = _pendingRequestId
                });
                _journal.Dispose();
                _journal = null;
            }
            _active = false;
            _observeExternalRequests = false;
            _pendingRequestId = null;
            _hasPendingTerminalIdentity = false;
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
                    _hasPendingTerminalIdentity = false;
                    _nextRequestUtc = now.AddSeconds(_intervalSeconds);
                }
                return;
            }
            if (_observeExternalRequests)
                return;
            if (_issuedRequestCount >= _requestedRequestCount)
            {
                Stop("requested_count_completed");
                return;
            }
            if (now < _nextRequestUtc)
                return;
            if (_terminal == null || !_terminal.IsValid)
            {
                _journal.Append("error", _sessionId, null, new Dictionary<string, object> { ["code"] = "TERMINAL_NO_LONGER_VALID" });
                Stop("terminal_invalid");
                return;
            }
            IssueRequest(now);
        }

        private void IssueRequest(DateTime now)
        {
            _issuedRequestCount++;
            _pendingRequestId = string.Format("{0}/request/{1:D8}", _sessionId, _issuedRequestCount);
            _pendingSinceUtc = now;
            _pendingTerminalIdentity = _terminal.Identity;
            _hasPendingTerminalIdentity = true;
            _journal.Append("request_started", _sessionId, _pendingRequestId, new Dictionary<string, object>
            {
                ["request_sequence"] = _issuedRequestCount,
                ["difficulty_slot"] = _difficultySlot,
                ["target_mission_ql"] = _targetMissionQl,
                ["target_mission_ql_semantics"] = "planner_input_not_direct_server_offer_field",
                ["request_origin"] = "HARVESTER_ACTIVE_DRIVER",
                ["terminal_identity"] = IdentityPayload(_pendingTerminalIdentity),
                ["sliders"] = SliderPayload((byte)_difficultySlot, 255, 255, 255, 255, 255, 255)
            });
            _terminal.RequestMissions((byte)_difficultySlot, 255, 255, 255, 255, 255, 255);
        }

        private void OnN3MessageSent(object sender, N3Message message)
        {
            QuestAlternativeMessage request = message as QuestAlternativeMessage;
            if (request == null || !_active || !_observeExternalRequests)
                return;

            if (_pendingRequestId != null)
            {
                _journal.Append("request_timeout", _sessionId, _pendingRequestId, new Dictionary<string, object>
                {
                    ["timeout_seconds"] = (DateTime.UtcNow - _pendingSinceUtc).TotalSeconds,
                    ["possible_causes"] = new[] { "superseded_by_next_external_request" }
                });
            }

            _issuedRequestCount++;
            _pendingRequestId = string.Format("{0}/request/{1:D8}", _sessionId, _issuedRequestCount);
            _pendingSinceUtc = DateTime.UtcNow;
            _pendingTerminalIdentity = request.Terminal;
            _hasPendingTerminalIdentity = true;
            _difficultySlot = request.MissionSliders.Difficulty;
            _journal.Append("request_started", _sessionId, _pendingRequestId, new Dictionary<string, object>
            {
                ["request_sequence"] = _issuedRequestCount,
                ["difficulty_slot"] = _difficultySlot,
                ["target_mission_ql"] = _targetMissionQl,
                ["target_mission_ql_semantics"] = "operator_planner_input_not_direct_server_offer_field",
                ["request_origin"] = "PASSIVELY_OBSERVED_EXTERNAL_AOSHARP_PLUGIN",
                ["terminal_identity"] = IdentityPayload(_pendingTerminalIdentity),
                ["sliders"] = SliderPayload(request.MissionSliders.Difficulty, request.MissionSliders.GoodBad, request.MissionSliders.OrderChaos, request.MissionSliders.OpenHidden, request.MissionSliders.PhysicalMystical, request.MissionSliders.HeadonStealth, request.MissionSliders.CreditsXp)
            });
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
            Identity expectedTerminal = _hasPendingTerminalIdentity ? _pendingTerminalIdentity : (_terminal == null ? new Identity() : _terminal.Identity);
            if ((_hasPendingTerminalIdentity || _terminal != null) && response.Terminal != expectedTerminal)
            {
                _journal.Append("error", _sessionId, _pendingRequestId, new Dictionary<string, object>
                {
                    ["code"] = "RESPONSE_TERMINAL_MISMATCH",
                    ["response_terminal"] = IdentityPayload(response.Terminal),
                    ["selected_terminal"] = IdentityPayload(expectedTerminal)
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
            _pendingRequestId = null;
            _hasPendingTerminalIdentity = false;
            _nextRequestUtc = DateTime.UtcNow.AddSeconds(_intervalSeconds);
        }

        private IDictionary<string, object> BuildCohortPayload(QuestAlternativeMessage response, out string fingerprint)
        {
            MissionInfo[] details = response.MissionDetails ?? new MissionInfo[0];
            var offers = new List<object>();
            for (int index = 0; index < details.Length; index++)
                offers.Add(OfferPayload(details[index], index + 1));
            var envelope = new Dictionary<string, object>
            {
                ["unknown1"] = response.Unknown1,
                ["unknown2"] = response.Unknown2,
                ["scope"] = (int)response.Scope,
                ["terminal"] = IdentityPayload(response.Terminal)
            };
            var payload = new Dictionary<string, object>
            {
                ["message_envelope"] = envelope,
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
                ["capture_mode"] = _observeExternalRequests ? "PASSIVE_EXTERNAL_REQUEST_OBSERVER" : "ACTIVE_REQUEST_DRIVER",
                ["difficulty_slot"] = _observeExternalRequests ? (object)null : _difficultySlot,
                ["target_mission_ql"] = _targetMissionQl,
                ["requested_request_count"] = _observeExternalRequests ? (object)null : _requestedRequestCount,
                ["minimum_request_interval_seconds"] = MinimumIntervalSeconds,
                ["configured_request_interval_seconds"] = _observeExternalRequests ? (object)null : _intervalSeconds,
                ["one_outstanding_request_only"] = true,
                ["client_version_label"] = null,
                ["client_version_availability"] = "NOT_EXPOSED_BY_INSPECTED_AOSHARP_API",
                ["aosharp_compile_target"] = "INSTALLED_RUNTIME_ASSEMBLY_SET_RECORDED_IN_DEPLOYMENT_MANIFEST",
                ["aosharp_observed_assembly_version"] = typeof(AOPluginEntry).Assembly.GetName().Version.ToString(),
                ["harvester_version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                ["raw_event_format"] = "incremental_jsonl_flush_true"
            };
        }

        private static IDictionary<string, object> OfferPayload(MissionInfo offer, int offerIndex)
        {
            var rewards = new List<object>();
            foreach (MissionItemReward reward in offer.MissionItemData ?? new MissionItemReward[0])
            {
                rewards.Add(new Dictionary<string, object>
                {
                    ["low_id"] = reward.LowId,
                    ["high_id"] = reward.HighId,
                    ["ql"] = reward.Ql,
                    ["unknown"] = reward.Unk
                });
            }
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
                ["mission_icon"] = offer.MissionIcon,
                ["playfield"] = IdentityPayload(offer.Playfield),
                ["location"] = VectorPayload(offer.Location),
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
