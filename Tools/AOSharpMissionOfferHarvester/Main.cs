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
        private MissionSliderState _sliderState;
        private SliderRequestGate _pendingSliderGate;
        private IDictionary<string, object> _pendingRawResponsePacket;
        private string _pendingSerializedRequestSha256;
        private string _lastRawResponseSha256;
        private Identity _sessionTerminalIdentity;
        private IList<MissionSliderPlanEntry> _matrixPlan;
        private int _matrixRequestsPerState;
        private MissionSliderPlanEntry _currentMatrixEntry;
        private MissionItemCampaignDefinition _itemCampaignDefinition;
        private MissionItemCampaignProgress _itemCampaignProgress;
        private MissionItemQlCohort _currentItemQlCohort;
        private JsonLineJournal _campaignProgressJournal;
        private string _campaignProgressPath;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        [Obsolete]
        public override void Run(string pluginDir)
        {
            Network.PacketSent += OnPacketSent;
            Network.PacketReceived += OnPacketReceived;
            Network.N3MessageReceived += OnN3MessageReceived;
            Game.OnUpdate += OnUpdate;
            Chat.RegisterCommand("missionharvest", OnCommand);
            Chat.WriteLine("Mission evidence harvester 1.6.1 loaded (Easy/Hard QL-spectrum capture + verified raw evidence + deterministic resume).", ChatColor.Gold);
        }

        public override void Teardown()
        {
            Stop("plugin_teardown", false);
            Network.PacketSent -= OnPacketSent;
            Network.PacketReceived -= OnPacketReceived;
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
            if (parameters.Length >= 2
                && parameters.Length <= 3
                && string.Equals(parameters[0], "campaign", StringComparison.OrdinalIgnoreCase))
            {
                StartCampaign(parameters);
                return;
            }
            if (parameters.Length >= 1
                && string.Equals(parameters[0], "items", StringComparison.OrdinalIgnoreCase))
            {
                Chat.WriteLine("The obsolete fixed-QL secondary-slider item mode is disabled. Use /missionharvest campaign <requestsPerQl> [intervalSeconds].", ChatColor.Red);
                return;
            }
            if (parameters.Length >= 4
                && parameters.Length <= 5
                && string.Equals(parameters[0], "matrix", StringComparison.OrdinalIgnoreCase))
            {
                StartMatrix(parameters);
                return;
            }
            bool presetStart = parameters.Length >= 4
                && parameters.Length <= 5
                && string.Equals(parameters[0], "start", StringComparison.OrdinalIgnoreCase);
            bool customStart = parameters.Length >= 9
                && parameters.Length <= 10
                && string.Equals(parameters[0], "startcustom", StringComparison.OrdinalIgnoreCase);
            if (!presetStart && !customStart)
            {
                WriteUsage();
                return;
            }
            int difficultyDetent;
            int requestCount;
            double interval = DefaultIntervalSeconds;
            int intervalIndex = presetStart ? 4 : 9;
            if (!int.TryParse(parameters[1], out difficultyDetent)
                || difficultyDetent < 1
                || difficultyDetent > MissionQlResolver.DifficultyCount
                || !int.TryParse(parameters[2], out requestCount)
                || requestCount < 1
                || requestCount > 100000
                || (parameters.Length == intervalIndex + 1
                    && !double.TryParse(
                        parameters[intervalIndex],
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
            int expectedMissionQl;
            if (!MissionQlResolver.TryGetMissionQl(characterLevel, difficultyDetent, out expectedMissionQl))
            {
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Difficulty detent {0} is not valid for character level {1}; no request was sent.",
                        difficultyDetent,
                        characterLevel),
                    ChatColor.Red);
                return;
            }
            MissionSliderState sliderState;
            string sliderError;
            bool validState = presetStart
                ? MissionSliderState.TryCreatePreset(difficultyDetent, parameters[3], out sliderState, out sliderError)
                : MissionSliderState.TryCreateCustom(
                    difficultyDetent,
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8],
                    out sliderState,
                    out sliderError);
            if (!validState)
            {
                Chat.WriteLine("Invalid slider state: " + sliderError + ". No request was sent.", ChatColor.Red);
                WriteUsage();
                return;
            }
            Chat.WriteLine("Resolved slider state: " + sliderState.Describe(), ChatColor.Gold);
            Start(characterLevel, sliderState, expectedMissionQl, requestCount, interval, null, 0, null, null, null, null);
        }

        private static void WriteUsage()
        {
            Chat.WriteLine(
                "Usage: /missionharvest campaign <requestsPerQl> [intervalSeconds] | matrix <startState 1-27> <endState 1-27> <requestsPerState> [intervalSeconds] | start <difficultyDetent> <requests> <preset> [intervalSeconds] | startcustom <difficultyDetent> <requests> <goodBad> <orderChaos> <openHidden> <physicalMystical> <headonStealth> <moneyXp> [intervalSeconds] | stop | status");
        }

        private void StartCampaign(string[] parameters)
        {
            int requestsPerQl;
            double interval = DefaultIntervalSeconds;
            if (!int.TryParse(parameters[1], out requestsPerQl)
                || requestsPerQl < 1
                || requestsPerQl > 100000
                || (parameters.Length == 3
                    && !double.TryParse(parameters[2], NumberStyles.Float, CultureInfo.InvariantCulture, out interval))
                || interval < MinimumIntervalSeconds)
            {
                Chat.WriteLine("Invalid campaign arguments. No request was sent.", ChatColor.Red);
                WriteUsage();
                return;
            }
            if (_active)
            {
                Chat.WriteLine("A mission evidence session is already active.", ChatColor.Red);
                return;
            }
            if (_terminal == null || !_terminal.IsValid)
            {
                Chat.WriteLine("Select/use an ordinary mission terminal before starting.", ChatColor.Red);
                return;
            }
            int characterLevel = DynelManager.LocalPlayer.GetStat(Stat.Level);
            MissionItemCampaignDefinition definition;
            string campaignError;
            if (!MissionItemCampaignDefinition.TryBuild(
                    characterLevel,
                    requestsPerQl,
                    out definition,
                    out campaignError))
            {
                Chat.WriteLine("Unable to build mission-item campaign: " + campaignError + ". No request was sent.", ChatColor.Red);
                return;
            }
            int characterIdentity = DynelManager.LocalPlayer.Identity.Instance;
            string progressDirectory = System.IO.Path.Combine(
                PluginDataDirectory.FullName,
                "campaigns",
                MissionItemCampaignDefinition.CampaignName,
                string.Format(CultureInfo.InvariantCulture, "level-{0:D3}-character-{1}", characterLevel, characterIdentity));
            string progressPath = System.IO.Path.Combine(progressDirectory, "progress.jsonl");
            MissionItemCampaignProgress progress;
            if (!MissionItemCampaignProgress.TryLoad(
                    progressPath,
                    definition,
                    characterIdentity,
                    out progress,
                    out campaignError))
            {
                Chat.WriteLine("Campaign progress rejected: " + campaignError + ". No request was sent.", ChatColor.Red);
                return;
            }
            if (progress.IsComplete)
            {
                Chat.WriteLine("This character campaign is already complete: " + progressPath, ChatColor.Gold);
                return;
            }
            JsonLineJournal progressJournal;
            try
            {
                progressJournal = new JsonLineJournal(progressPath);
            }
            catch (Exception exception)
            {
                Chat.WriteLine("Campaign progress journal could not be opened: " + exception.Message, ChatColor.Red);
                return;
            }
            MissionItemQlCohort first = progress.NextIncompleteCohort();
            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mission spectrum capture resolved: observedLevel={0}; difficultyPositions=11; staticDistinctQLs={1}; requestsPerActualQl={2}; completedRequests={3}; knownRemaining={4}; firstDifficulty={5}; preset=FIND_ITEM_HEAVY (Good/Bad=BAD, Money/XP=CREDITS, others=CENTERED).",
                    characterLevel,
                    definition.PlannedQlCohortCount,
                    requestsPerQl,
                    progress.TotalCompletedRequestCount,
                    progress.RemainingRequestCount,
                    first.DifficultyDetent),
                ChatColor.Gold);
            Start(
                characterLevel,
                first.SliderState,
                first.MissionQl,
                progress.MaximumRemainingRequestCount,
                interval,
                null,
                0,
                definition,
                progress,
                progressJournal,
                progressPath);
        }

        private void StartMatrix(string[] parameters)
        {
            int startIndex;
            int endIndex;
            int requestsPerState;
            double interval = DefaultIntervalSeconds;
            if (!int.TryParse(parameters[1], out startIndex)
                || !int.TryParse(parameters[2], out endIndex)
                || !int.TryParse(parameters[3], out requestsPerState)
                || requestsPerState < 1
                || requestsPerState > 1000
                || (parameters.Length == 5
                    && !double.TryParse(parameters[4], NumberStyles.Float, CultureInfo.InvariantCulture, out interval))
                || interval < MinimumIntervalSeconds)
            {
                Chat.WriteLine("Invalid matrix arguments. No request was sent.", ChatColor.Red);
                WriteUsage();
                return;
            }
            if (_active)
            {
                Chat.WriteLine("A mission evidence session is already active.", ChatColor.Red);
                return;
            }
            if (_terminal == null || !_terminal.IsValid)
            {
                Chat.WriteLine("Select/use an ordinary mission terminal before starting.", ChatColor.Red);
                return;
            }
            int characterLevel = DynelManager.LocalPlayer.GetStat(Stat.Level);
            if (characterLevel != 2)
            {
                Chat.WriteLine("The low-level discovery matrix requires character level 2; no request was sent.", ChatColor.Red);
                return;
            }
            IList<MissionSliderPlanEntry> plan;
            string matrixError;
            if (!LowLevelSliderMatrix.TryBuild(startIndex, endIndex, out plan, out matrixError))
            {
                Chat.WriteLine("Invalid matrix range: " + matrixError + ". No request was sent.", ChatColor.Red);
                return;
            }
            MissionSliderPlanEntry first = plan[0];
            int expectedMissionQl;
            if (!MissionQlResolver.TryGetMissionQl(
                    characterLevel,
                    first.SliderState.DifficultyDetent,
                    out expectedMissionQl))
            {
                Chat.WriteLine("Matrix first detent cannot be resolved for this character; no request was sent.", ChatColor.Red);
                return;
            }
            int totalRequests = plan.Count * requestsPerState;
            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Resolved matrix states {0}-{1}/27; states={2}; requestsPerState={3}; totalRequests={4}.",
                    startIndex,
                    endIndex,
                    plan.Count,
                    requestsPerState,
                    totalRequests),
                ChatColor.Gold);
            Start(
                characterLevel,
                first.SliderState,
                expectedMissionQl,
                totalRequests,
                interval,
                plan,
                requestsPerState,
                null,
                null,
                null,
                null);
        }

        private void WriteStatus()
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                Chat.WriteLine("Mission harvester has not started a session.");
                return;
            }

            if (_itemCampaignProgress != null)
            {
                int completedForQl = _currentItemQlCohort == null
                    ? 0
                    : _itemCampaignProgress.CompletedRequestCount(_currentItemQlCohort.MissionQl);
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "OBSERVED CHARACTER: Level {0}; DIFFICULTY POSITION: {1}/11; ACTUAL MISSION QL: {2}; PHASE: {3}; PRESET: FIND_ITEM_HEAVY; REQUEST: {4}/{5}; OFFERS CAPTURED THIS SESSION: {6}; QL STATUS: {7}; CHARACTER SPECTRUM STATUS: {8}; output={9}; progress={10}",
                        _characterLevel,
                        _currentItemQlCohort == null ? "none" : _currentItemQlCohort.DifficultyDetent.ToString(CultureInfo.InvariantCulture),
                        _currentItemQlCohort == null || _currentItemQlCohort.IsDifficultyDiscovery ? "pending live response" : "QL " + _currentItemQlCohort.MissionQl,
                        _currentItemQlCohort == null ? "none" : (_currentItemQlCohort.IsDifficultyDiscovery ? "DIFFICULTY_DISCOVERY" : "QL_CAPTURE"),
                        completedForQl,
                        _itemCampaignDefinition.RequiredRequestsPerQl,
                        _harvestedOfferCount,
                        completedForQl >= _itemCampaignDefinition.RequiredRequestsPerQl ? "COMPLETE" : "INCOMPLETE",
                        _itemCampaignProgress.IsComplete ? "COMPLETE" : "INCOMPLETE",
                        _sessionOutputPath ?? "unknown",
                        _campaignProgressPath ?? "unknown"),
                    ChatColor.Gold);
                return;
            }

            string matrixStatus = _matrixPlan == null
                ? "single-state"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "matrix={0}-{1}/27; current={2}",
                    _matrixPlan[0].MatrixIndex,
                    _matrixPlan[_matrixPlan.Count - 1].MatrixIndex,
                    _currentMatrixEntry == null ? "not-started" : _currentMatrixEntry.Label);
            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Harvester active={0}; captureContract=3; session={1}; level={2}; expectedQL={3}; detent={4}; sliderState={5}; preset={6}; requests={7}/{8}; completeCohorts={9}; harvestedOffers={10}; pending={11}; stopReason={12}; mode={13}; output={14}",
                    _active,
                    _sessionId,
                    _characterLevel,
                    _targetMissionQl,
                    _difficultySlot,
                    _sliderState == null ? "none" : _sliderState.SliderStateId,
                    _sliderState == null ? "none" : _sliderState.PresetName,
                    _issuedRequestCount,
                    _requestedRequestCount,
                    _completedCohortCount,
                    _harvestedOfferCount,
                    _pendingRequestId ?? "none",
                    _lastStopReason ?? "none",
                    matrixStatus,
                    _sessionOutputPath ?? "unknown"));
        }

        private void Start(
            int characterLevel,
            MissionSliderState sliderState,
            int expectedMissionQl,
            int requestCount,
            double interval,
            IList<MissionSliderPlanEntry> matrixPlan,
            int matrixRequestsPerState,
            MissionItemCampaignDefinition itemCampaignDefinition,
            MissionItemCampaignProgress itemCampaignProgress,
            JsonLineJournal campaignProgressJournal,
            string campaignProgressPath)
        {
            if (_active || _journal != null)
                Stop("replaced_by_new_session", true);
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            _sessionId = string.Format("mission-{0}-{1}-{2}", stamp, DynelManager.LocalPlayer.Identity.Instance, Guid.NewGuid().ToString("N").Substring(0, 8));
            _sessionDirectory = System.IO.Path.Combine(PluginDataDirectory.FullName, "sessions", _sessionId);
            _sessionOutputPath = System.IO.Path.Combine(_sessionDirectory, "events.jsonl");
            _journal = new JsonLineJournal(_sessionOutputPath);
            _characterLevel = characterLevel;
            _sliderState = sliderState;
            _difficultySlot = sliderState.DifficultyDetent;
            _targetMissionQl = expectedMissionQl;
            _matrixPlan = matrixPlan;
            _matrixRequestsPerState = matrixRequestsPerState;
            _currentMatrixEntry = matrixPlan == null ? null : matrixPlan[0];
            _itemCampaignDefinition = itemCampaignDefinition;
            _itemCampaignProgress = itemCampaignProgress;
            _currentItemQlCohort = itemCampaignProgress == null ? null : itemCampaignProgress.NextIncompleteCohort();
            _campaignProgressJournal = campaignProgressJournal;
            _campaignProgressPath = campaignProgressPath;
            _sessionTerminalIdentity = _terminal.Identity;
            _requestedRequestCount = requestCount;
            _intervalSeconds = interval;
            _issuedRequestCount = 0;
            _completedCohortCount = 0;
            _harvestedOfferCount = 0;
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _pendingSliderGate = null;
            _pendingRawResponsePacket = null;
            _pendingSerializedRequestSha256 = null;
            _lastRawResponseSha256 = null;
            _lastCohortFingerprint = null;
            _lastCohortRequestId = null;
            _lastStopReason = null;
            _active = true;
            _nextRequestUtc = DateTime.UtcNow;
            _journal.Append("session_started", _sessionId, null, BuildSessionPayload());
            Chat.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mission evidence session started: {0}; captureContract=3; level={1}; staticExpectedQL={2}; detent={3}; sliderState={4}; preset={5}; requests={6}; mode={7}; output={8}",
                    _sessionId,
                    _characterLevel,
                    _targetMissionQl,
                    _difficultySlot,
                    _sliderState.SliderStateId,
                    _sliderState.PresetName,
                    _requestedRequestCount,
                    CaptureModeLabel().ToLowerInvariant(),
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
                    ["pending_request_id"] = _pendingRequestId,
                    ["capture_mode"] = CaptureModeLabel(),
                    ["current_matrix_index"] = _currentMatrixEntry == null ? (object)null : _currentMatrixEntry.MatrixIndex,
                    ["campaign_progress"] = _itemCampaignProgress == null ? null : _itemCampaignProgress.ToPayload()
                });
                _journal.Dispose();
                _journal = null;
            }
            if (_campaignProgressJournal != null)
            {
                _campaignProgressJournal.Dispose();
                _campaignProgressJournal = null;
            }
            _active = false;
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _pendingSliderGate = null;
            _pendingRawResponsePacket = null;
            _pendingSerializedRequestSha256 = null;
            _lastStopReason = reason;
            if (announce && hadActiveSession)
            {
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mission evidence session stopped: {0}; reason={1}; requests={2}/{3}; completeCohorts={4}; harvestedOffers={5}; characterCampaign={6}; progress={7}; output={8}",
                        _sessionId,
                        reason,
                        _issuedRequestCount,
                        _requestedRequestCount,
                        _completedCohortCount,
                        _harvestedOfferCount,
                        _itemCampaignProgress == null ? "n/a" : (_itemCampaignProgress.IsComplete ? "COMPLETE" : "INCOMPLETE"),
                        _campaignProgressPath ?? "n/a",
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
                        ["slider_state_id"] = _sliderState.SliderStateId,
                        ["verification_phase"] = _pendingSliderGate == null ? null : _pendingSliderGate.Phase,
                        ["possible_causes"] = new[] { "terminal_rejection", "insufficient_credits", "network_interruption", "disconnect", "unknown" }
                    });
                    Stop("request_timeout_fail_closed", true);
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
            if (_terminal.Identity != _sessionTerminalIdentity)
            {
                FailClosed("TERMINAL_IDENTITY_CHANGED", new Dictionary<string, object>
                {
                    ["session_terminal"] = IdentityPayload(_sessionTerminalIdentity),
                    ["current_terminal"] = IdentityPayload(_terminal.Identity)
                });
                return;
            }
            if (!PrepareNextSliderState())
                return;
            IssueRequest(now);
        }

        private bool PrepareNextSliderState()
        {
            if (_itemCampaignProgress != null)
            {
                MissionItemQlCohort next = _itemCampaignProgress.NextIncompleteCohort();
                if (next == null)
                {
                    Stop("character_campaign_completed", true);
                    return false;
                }
                bool changed = _currentItemQlCohort == null
                    || _currentItemQlCohort.MissionQl != next.MissionQl
                    || _currentItemQlCohort.DifficultyDetent != next.DifficultyDetent
                    || _currentItemQlCohort.IsDifficultyDiscovery != next.IsDifficultyDiscovery;
                _currentItemQlCohort = next;
                _sliderState = next.SliderState;
                _difficultySlot = next.DifficultyDetent;
                _targetMissionQl = next.MissionQl;
                if (changed)
                {
                    Chat.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Mission capture: observedLevel={0}; difficultyPosition={1}/11; phase={2}; missionQL={3}; completed={4}/{5}; preset=FIND_ITEM_HEAVY.",
                            _characterLevel,
                            next.DifficultyDetent,
                            next.IsDifficultyDiscovery ? "DIFFICULTY_DISCOVERY" : "QL_CAPTURE",
                            next.IsDifficultyDiscovery ? "pending live response" : next.MissionQl.ToString(CultureInfo.InvariantCulture),
                            _itemCampaignProgress.CompletedRequestCount(next.MissionQl),
                            _itemCampaignDefinition.RequiredRequestsPerQl),
                        ChatColor.Gold);
                }
                return true;
            }
            if (_matrixPlan == null)
                return true;
            if (_matrixRequestsPerState < 1)
            {
                FailClosed("MATRIX_REQUESTS_PER_STATE_INVALID", null);
                return false;
            }
            int planOffset = _issuedRequestCount / _matrixRequestsPerState;
            if (planOffset < 0 || planOffset >= _matrixPlan.Count)
            {
                FailClosed("MATRIX_PLAN_OFFSET_OUT_OF_RANGE", new Dictionary<string, object>
                {
                    ["plan_offset"] = planOffset,
                    ["plan_count"] = _matrixPlan.Count
                });
                return false;
            }
            _currentMatrixEntry = _matrixPlan[planOffset];
            _sliderState = _currentMatrixEntry.SliderState;
            _difficultySlot = _sliderState.DifficultyDetent;
            if (!MissionQlResolver.TryGetMissionQl(_characterLevel, _difficultySlot, out _targetMissionQl))
            {
                FailClosed("MATRIX_DETENT_NOT_RESOLVABLE", new Dictionary<string, object>
                {
                    ["matrix_index"] = _currentMatrixEntry.MatrixIndex,
                    ["difficulty_detent"] = _difficultySlot,
                    ["character_level"] = _characterLevel
                });
                return false;
            }
            if (_issuedRequestCount % _matrixRequestsPerState == 0)
            {
                Chat.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Matrix state {0}/27: {1}; expectedQL={2}; {3}",
                        _currentMatrixEntry.MatrixIndex,
                        _currentMatrixEntry.Label,
                        _targetMissionQl,
                        _sliderState.Describe()),
                    ChatColor.Gold);
            }
            return true;
        }

        private void IssueRequest(DateTime now)
        {
            int sequence = _issuedRequestCount + 1;
            string requestId = string.Format("{0}/request/{1:D8}", _sessionId, sequence);
            SliderRequestGate gate = new SliderRequestGate(requestId, _sliderState);
            NativeMissionSliderValues requestedNative = _sliderState.ToNativeValues();
            MissionSliders nativeSliders = new MissionSliders
            {
                Difficulty = requestedNative.Difficulty,
                GoodBad = requestedNative.GoodBad,
                OrderChaos = requestedNative.OrderChaos,
                OpenHidden = requestedNative.OpenHidden,
                PhysicalMystical = requestedNative.PhysicalMystical,
                HeadonStealth = requestedNative.HeadonStealth,
                CreditsXp = requestedNative.CreditsXp
            };
            QuestAlternativeMessage request = new QuestAlternativeMessage
            {
                Unknown1 = 4,
                MissionSliders = nativeSliders,
                Scope = _terminal.Name.Contains("Team") ? MissionScope.Team : MissionScope.Solo,
                Terminal = _terminal.Identity,
                MissionDetails = new MissionInfo[0]
            };
            NativeMissionSliderValues appliedNative = NativeValues(request.MissionSliders);
            string verificationError;
            if (!gate.TryApplyNative(appliedNative, request.MissionSliders != null, out verificationError))
            {
                FailClosed(verificationError, GateFailurePayload(gate, requestedNative, appliedNative, null));
                return;
            }

            byte[] serializedPacket;
            QuestAlternativeMessage serializedRequest;
            string decodeError = null;
            try
            {
                serializedPacket = PacketFactory.Create(request);
            }
            catch (Exception exception)
            {
                FailClosed("REQUEST_SERIALIZATION_FAILED", new Dictionary<string, object> { ["exception"] = exception.GetType().FullName, ["message"] = exception.Message });
                return;
            }
            if (serializedPacket == null
                || !TryDecodeQuestAlternative(serializedPacket, out serializedRequest, out decodeError))
            {
                FailClosed("SERIALIZED_REQUEST_NOT_DECODABLE", new Dictionary<string, object> { ["decode_error"] = decodeError });
                return;
            }
            NativeMissionSliderValues serializedNative = NativeValues(serializedRequest.MissionSliders);
            if (serializedRequest.Terminal != _sessionTerminalIdentity
                || !gate.TryVerifySerialized(serializedNative, out verificationError))
            {
                string code = serializedRequest.Terminal != _sessionTerminalIdentity
                    ? "SERIALIZED_REQUEST_TERMINAL_MISMATCH"
                    : verificationError;
                FailClosed(code, GateFailurePayload(gate, requestedNative, serializedNative, RawPacketPayload(serializedPacket, "PACKETFACTORY_CREATE_PRE_SEND")));
                return;
            }

            _issuedRequestCount = sequence;
            _pendingRequestId = requestId;
            _pendingSliderGate = gate;
            _pendingSinceUtc = now;
            _pendingRollOrigin = BuildRollOriginPayload("REQUEST_STARTED");
            _pendingRawResponsePacket = null;
            _pendingSerializedRequestSha256 = Sha256(serializedPacket);
            _journal.Append("request_started", _sessionId, requestId, new Dictionary<string, object>
            {
                ["request_sequence"] = sequence,
                ["capture_mode"] = CaptureModeLabel(),
                ["matrix_state_index"] = _currentMatrixEntry == null ? (object)null : _currentMatrixEntry.MatrixIndex,
                ["matrix_state_label"] = _currentMatrixEntry == null ? null : _currentMatrixEntry.Label,
                ["matrix_request_within_state"] = _currentMatrixEntry == null
                    ? (object)null
                    : (object)(((_issuedRequestCount - 1) % _matrixRequestsPerState) + 1),
                ["campaign_manifest_sha256"] = _itemCampaignDefinition == null ? null : _itemCampaignDefinition.ManifestSha256,
                ["campaign_ql_cohort_index"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.CohortIndex,
                ["campaign_static_expected_mission_ql"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.StaticExpectedMissionQl,
                ["campaign_actual_mission_ql_target"] = _currentItemQlCohort == null || _currentItemQlCohort.IsDifficultyDiscovery ? (object)null : _currentItemQlCohort.MissionQl,
                ["campaign_phase"] = _currentItemQlCohort == null ? null : (_currentItemQlCohort.IsDifficultyDiscovery ? "DIFFICULTY_DISCOVERY" : "QL_CAPTURE"),
                ["campaign_difficulty_detent"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.DifficultyDetent,
                ["campaign_semantic_state_index"] = _currentItemQlCohort == null ? (object)null : 1,
                ["campaign_semantic_state_count"] = _currentItemQlCohort == null ? (object)null : 1,
                ["campaign_completed_request_within_ql_before_request"] = _currentItemQlCohort == null
                    ? (object)null
                    : _itemCampaignProgress.CompletedRequestCount(_currentItemQlCohort.MissionQl),
                ["campaign_required_requests_per_ql"] = _itemCampaignDefinition == null
                    ? (object)null
                    : _itemCampaignDefinition.RequiredRequestsPerQl,
                ["campaign_progress_path"] = _campaignProgressPath,
                ["character_level"] = _characterLevel,
                ["difficulty_detent"] = _difficultySlot,
                ["static_expected_mission_ql"] = _targetMissionQl,
                ["static_expected_mission_ql_semantics"] = "character_level_and_explicit_difficulty_detent_table_lookup_not_server_response_ql",
                ["mission_ql_table_source"] = MissionQlResolver.SourceRepositoryPath,
                ["mission_ql_table_sha256"] = MissionQlResolver.SourceSha256,
                ["slider_state_id"] = _sliderState.SliderStateId,
                ["slider_preset"] = _sliderState.PresetName,
                ["requested_semantic_state"] = _sliderState.RequestedSemanticPayload(),
                ["native_client_before"] = new Dictionary<string, object>
                {
                    ["availability"] = "NOT_APPLICABLE_DIRECT_REQUEST_CONSTRUCTION",
                    ["verification_status"] = "UNVERIFIABLE_NOT_REQUIRED_FOR_DIRECT_PACKET",
                    ["values"] = null
                },
                ["native_client_after"] = new Dictionary<string, object>
                {
                    ["availability"] = "DIRECT_REQUEST_OBJECT",
                    ["verification_status"] = "READ_BACK_MATCH",
                    ["values"] = appliedNative.ToPayload()
                },
                ["serialized_pre_send"] = new Dictionary<string, object>
                {
                    ["verification_status"] = "MATCH",
                    ["values"] = serializedNative.ToPayload(),
                    ["raw_packet"] = RawPacketPayload(serializedPacket, "PACKETFACTORY_CREATE_PRE_SEND")
                },
                ["application_verification_phase"] = gate.Phase,
                ["roll_origin"] = _pendingRollOrigin,
                ["sliders"] = requestedNative.ToPayload()
            });
            Network.Send(serializedPacket);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            if (!_active || packet == null)
                return;
            QuestAlternativeMessage request;
            string decodeError;
            if (!TryDecodeQuestAlternative(packet, out request, out decodeError)
                || request.Terminal != _sessionTerminalIdentity)
                return;
            if (_pendingRequestId == null || _pendingSliderGate == null)
            {
                FailClosed("UNMATCHED_RAW_REQUEST", new Dictionary<string, object> { ["raw_packet"] = RawPacketPayload(packet, "NETWORK_PACKET_SENT") });
                return;
            }
            NativeMissionSliderValues transmittedNative = NativeValues(request.MissionSliders);
            string observedSha256 = Sha256(packet);
            string verificationError;
            if (!_pendingSliderGate.TryMarkTransmitted(
                    _pendingRequestId,
                    transmittedNative,
                    _pendingSerializedRequestSha256,
                    observedSha256,
                    out verificationError))
            {
                FailClosed(verificationError, GateFailurePayload(_pendingSliderGate, _sliderState.ToNativeValues(), transmittedNative, RawPacketPayload(packet, "NETWORK_PACKET_SENT")));
                return;
            }
            _journal.Append("request_transmitted", _sessionId, _pendingRequestId, new Dictionary<string, object>
            {
                ["slider_state_id"] = _sliderState.SliderStateId,
                ["verification_status"] = "MATCH",
                ["verification_phase"] = _pendingSliderGate.Phase,
                ["transmitted_native_values"] = transmittedNative.ToPayload(),
                ["raw_packet"] = RawPacketPayload(packet, "NETWORK_PACKET_SENT")
            });
        }

        private void OnPacketReceived(object sender, byte[] packet)
        {
            if (!_active || packet == null)
                return;
            QuestAlternativeMessage response;
            string decodeError;
            if (!TryDecodeQuestAlternative(packet, out response, out decodeError)
                || response.Terminal != _sessionTerminalIdentity)
                return;
            IDictionary<string, object> rawPacket = RawPacketPayload(packet, "NETWORK_PACKET_RECEIVED");
            if (_pendingRequestId == null)
            {
                if (string.Equals(
                        Convert.ToString(rawPacket["sha256"], CultureInfo.InvariantCulture),
                        _lastRawResponseSha256,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                _journal.Append("error", _sessionId, null, new Dictionary<string, object>
                {
                    ["code"] = "UNMATCHED_RAW_RESPONSE_PRESERVED",
                    ["raw_packet"] = rawPacket,
                    ["returned_sliders"] = response.MissionSliders == null ? null : NativeValues(response.MissionSliders).ToPayload()
                });
                return;
            }
            _pendingRawResponsePacket = rawPacket;
            _journal.Append("raw_response_received", _sessionId, _pendingRequestId, new Dictionary<string, object>
            {
                ["slider_state_id"] = _sliderState.SliderStateId,
                ["association_status"] = "PENDING_REQUEST_MATCHED_BY_SINGLE_OUTSTANDING_REQUEST_AND_TERMINAL_IDENTITY",
                ["returned_sliders"] = NativeValues(response.MissionSliders).ToPayload(),
                ["raw_packet"] = rawPacket
            });
        }

        private void OnN3MessageReceived(object sender, N3Message message)
        {
            GenericCmdMessage generic = message as GenericCmdMessage;
            if (generic != null && generic.Identity == DynelManager.LocalPlayer.Identity &&
                generic.Action == GenericCmdAction.Use && generic.Target.Type == IdentityType.MissionTerminal)
            {
                if (_active && generic.Target != _sessionTerminalIdentity)
                {
                    FailClosed("TERMINAL_SELECTION_CHANGED_DURING_SESSION", new Dictionary<string, object>
                    {
                        ["session_terminal"] = IdentityPayload(_sessionTerminalIdentity),
                        ["new_terminal"] = IdentityPayload(generic.Target)
                    });
                    return;
                }
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
                FailClosed("RESPONSE_TERMINAL_MISMATCH", new Dictionary<string, object>
                {
                    ["response_terminal"] = IdentityPayload(response.Terminal),
                    ["selected_terminal"] = _terminal == null ? null : IdentityPayload(_terminal.Identity)
                });
                return;
            }
            if (_pendingRequestId == null)
            {
                string unmatchedFingerprint;
                IDictionary<string, object> unmatchedPayload = BuildCohortPayload(
                    response,
                    "UNMATCHED",
                    false,
                    "UNMATCHED_COHORT",
                    out unmatchedFingerprint);
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
                    Stop("unmatched_cohort_fail_closed", true);
                }
                return;
            }
            HandleCohort(response);
        }

        private void HandleCohort(QuestAlternativeMessage response)
        {
            NativeMissionSliderValues returnedNative = NativeValues(response.MissionSliders);
            string verificationError = null;
            bool verified = _pendingRawResponsePacket != null
                && _pendingSliderGate != null
                && _pendingSliderGate.TryVerifyResponse(returnedNative, out verificationError)
                && _pendingSliderGate.TryAssociateCohort(_pendingRequestId, _sliderState.SliderStateId, out verificationError);
            if (_pendingRawResponsePacket == null)
                verificationError = "RAW_RESPONSE_PACKET_MISSING";
            else if (_pendingSliderGate == null)
                verificationError = "SLIDER_REQUEST_GATE_MISSING";
            string fingerprint;
            string cohortId = _pendingRequestId + "/cohort/0001";
            IDictionary<string, object> payload = BuildCohortPayload(response, cohortId, verified, verificationError, out fingerprint);
            string completedRequestId = _pendingRequestId;
            _journal.Append("cohort_received", _sessionId, completedRequestId, payload);
            if (!verified)
            {
                FailClosed(verificationError ?? "COHORT_VERIFICATION_FAILED", new Dictionary<string, object>
                {
                    ["slider_state_id"] = _sliderState.SliderStateId,
                    ["cohort_id"] = cohortId,
                    ["returned_sliders"] = returnedNative == null ? null : returnedNative.ToPayload()
                });
                return;
            }
            _lastCohortFingerprint = fingerprint;
            _lastCohortRequestId = completedRequestId;
            _lastRawResponseSha256 = Convert.ToString(_pendingRawResponsePacket["sha256"], CultureInfo.InvariantCulture);
            _completedCohortCount++;
            _harvestedOfferCount +=
                response.MissionDetails == null ? 0 : response.MissionDetails.Length;
            if (_itemCampaignProgress != null)
            {
                int offerCount = response.MissionDetails == null ? 0 : response.MissionDetails.Length;
                if (offerCount != 5)
                {
                    FailClosed("CAMPAIGN_COHORT_SIZE_MISMATCH", new Dictionary<string, object>
                    {
                        ["expected_offer_count"] = 5,
                        ["actual_offer_count"] = offerCount,
                        ["mission_ql"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.MissionQl
                    });
                    return;
                }

                int observedMissionQl = 0;
                string candidateError = "CAMPAIGN_QL_COHORT_NOT_SELECTED";
                if (_currentItemQlCohort == null
                    || !TryGetMissionQlCandidate(response, out observedMissionQl, out candidateError))
                {
                    FailClosed("CAMPAIGN_MISSION_QL_CANDIDATE_UNAVAILABLE", new Dictionary<string, object>
                    {
                        ["candidate_error"] = candidateError,
                        ["static_expected_mission_ql"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.StaticExpectedMissionQl,
                        ["observed_mission_ql_candidate"] = observedMissionQl,
                        ["candidate_semantics"] = "STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION",
                        ["source_field"] = "MissionInfo.UnkChunk3",
                        ["byte_offset"] = 16,
                        ["byte_width"] = 4,
                        ["byte_order"] = "BIG_ENDIAN"
                    });
                    return;
                }
                if (!_currentItemQlCohort.IsDifficultyDiscovery
                    && observedMissionQl != _currentItemQlCohort.MissionQl)
                {
                    FailClosed("CAMPAIGN_DIFFICULTY_MAPPING_CHANGED", new Dictionary<string, object>
                    {
                        ["difficulty_detent"] = _currentItemQlCohort.DifficultyDetent,
                        ["previously_observed_actual_mission_ql"] = _currentItemQlCohort.MissionQl,
                        ["current_observed_actual_mission_ql"] = observedMissionQl
                    });
                    return;
                }

                string rawResponseSha = Convert.ToString(_pendingRawResponsePacket["sha256"], CultureInfo.InvariantCulture);
                string completionId = Sha256(_sessionId + "|" + completedRequestId + "|" + rawResponseSha);
                var mappingPayload = new Dictionary<string, object>
                {
                    ["campaign_name"] = MissionItemCampaignDefinition.CampaignName,
                    ["campaign_manifest_sha256"] = _itemCampaignDefinition.ManifestSha256,
                    ["character_level"] = _characterLevel,
                    ["character_identity_instance"] = DynelManager.LocalPlayer.Identity.Instance,
                    ["difficulty_detent"] = _currentItemQlCohort.DifficultyDetent,
                    ["static_expected_mission_ql"] = _currentItemQlCohort.StaticExpectedMissionQl,
                    ["actual_mission_ql"] = observedMissionQl,
                    ["validation_status"] = observedMissionQl == _currentItemQlCohort.StaticExpectedMissionQl
                        ? "MATCH"
                        : "LIVE_CONTRADICTION_RECORDED",
                    ["verification_status"] = "VERIFIED_COMPLETE_FIVE_OFFER_COHORT"
                };
                if (_currentItemQlCohort.IsDifficultyDiscovery)
                    _campaignProgressJournal.Append("difficulty_mapping_verified", _sessionId, completedRequestId, mappingPayload);
                string progressError;
                if (!_itemCampaignProgress.TryRecordMapping(_currentItemQlCohort.DifficultyDetent, observedMissionQl, out progressError))
                {
                    FailClosed(progressError ?? "CAMPAIGN_DIFFICULTY_MAPPING_UPDATE_FAILED", mappingPayload);
                    return;
                }
                var completionPayload = new Dictionary<string, object>
                {
                    ["campaign_name"] = MissionItemCampaignDefinition.CampaignName,
                    ["campaign_manifest_sha256"] = _itemCampaignDefinition.ManifestSha256,
                    ["character_level"] = _characterLevel,
                    ["character_identity_instance"] = DynelManager.LocalPlayer.Identity.Instance,
                    ["actual_mission_ql"] = observedMissionQl,
                    ["static_expected_mission_ql"] = _currentItemQlCohort.StaticExpectedMissionQl,
                    ["difficulty_detent"] = _currentItemQlCohort.DifficultyDetent,
                    ["campaign_phase"] = _currentItemQlCohort.IsDifficultyDiscovery ? "DIFFICULTY_DISCOVERY" : "QL_CAPTURE",
                    ["semantic_state_index"] = 1,
                    ["semantic_state_count"] = 1,
                    ["required_requests_per_ql"] = _itemCampaignDefinition.RequiredRequestsPerQl,
                    ["offer_count"] = offerCount,
                    ["observed_mission_ql_candidate"] = observedMissionQl,
                    ["mission_ql_candidate_semantics"] = "STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION",
                    ["raw_response_sha256"] = rawResponseSha,
                    ["completion_id"] = completionId,
                    ["verification_status"] = "VERIFIED_COMPLETE_FIVE_OFFER_COHORT"
                };
                _campaignProgressJournal.Append("campaign_request_completed", _sessionId, completedRequestId, completionPayload);
                if (!_itemCampaignProgress.TryRecordCompletion(observedMissionQl, completionId, out progressError))
                {
                    FailClosed(progressError ?? "CAMPAIGN_PROGRESS_UPDATE_FAILED", completionPayload);
                    return;
                }
                if (_itemCampaignProgress.CompletedRequestCount(observedMissionQl)
                    == _itemCampaignDefinition.RequiredRequestsPerQl)
                {
                    Chat.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "QL cohort complete: characterLevel={0}; missionQL={1}; verifiedRequests={2}; offers={3}; next cohort will start automatically.",
                            _characterLevel,
                            observedMissionQl,
                            _itemCampaignDefinition.RequiredRequestsPerQl,
                            _itemCampaignDefinition.RequiredRequestsPerQl * 5),
                        ChatColor.Gold);
                }
                if (_completedCohortCount % 10 == 0)
                {
                    Chat.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Mission capture running: rolls={0}; actualQL={1}; QL progress={2}/{3}; offers captured={4}.",
                            _completedCohortCount,
                            observedMissionQl,
                            _itemCampaignProgress.CompletedRequestCount(observedMissionQl),
                            _itemCampaignDefinition.RequiredRequestsPerQl,
                            _harvestedOfferCount),
                        ChatColor.Gold);
                }
            }
            _pendingRequestId = null;
            _pendingRollOrigin = null;
            _pendingSliderGate = null;
            _pendingRawResponsePacket = null;
            _pendingSerializedRequestSha256 = null;
            _nextRequestUtc = DateTime.UtcNow.AddSeconds(_intervalSeconds);
            if (_itemCampaignProgress != null && _itemCampaignProgress.IsComplete)
                Stop("character_campaign_completed", true);
        }

        private IDictionary<string, object> BuildCohortPayload(
            QuestAlternativeMessage response,
            string cohortId,
            bool verified,
            string verificationError,
            out string fingerprint)
        {
            MissionInfo[] details = response.MissionDetails ?? new MissionInfo[0];
            IDictionary<string, object> rollOrigin =
                _pendingRollOrigin ?? BuildRollOriginPayload("UNMATCHED_RESPONSE_CURRENT_SNAPSHOT");
            var offers = new List<object>();
            for (int index = 0; index < details.Length; index++)
                offers.Add(OfferPayload(details[index], index + 1, rollOrigin, cohortId, _sliderState == null ? null : _sliderState.SliderStateId));
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
                ["cohort_id"] = cohortId,
                ["matrix_state_index"] = _currentMatrixEntry == null ? (object)null : _currentMatrixEntry.MatrixIndex,
                ["matrix_state_label"] = _currentMatrixEntry == null ? null : _currentMatrixEntry.Label,
                ["campaign_manifest_sha256"] = _itemCampaignDefinition == null ? null : _itemCampaignDefinition.ManifestSha256,
                ["campaign_ql_cohort_index"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.CohortIndex,
                ["campaign_static_expected_mission_ql"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.StaticExpectedMissionQl,
                ["campaign_actual_mission_ql_target"] = _currentItemQlCohort == null || _currentItemQlCohort.IsDifficultyDiscovery ? (object)null : _currentItemQlCohort.MissionQl,
                ["campaign_phase"] = _currentItemQlCohort == null ? null : (_currentItemQlCohort.IsDifficultyDiscovery ? "DIFFICULTY_DISCOVERY" : "QL_CAPTURE"),
                ["campaign_difficulty_detent"] = _currentItemQlCohort == null ? (object)null : _currentItemQlCohort.DifficultyDetent,
                ["campaign_semantic_state_index"] = _currentItemQlCohort == null ? (object)null : 1,
                ["campaign_semantic_state_count"] = _currentItemQlCohort == null ? (object)null : 1,
                ["mission_ql_candidate"] = MissionQlCandidatePayload(response, _currentItemQlCohort == null ? (int?)null : _currentItemQlCohort.MissionQl),
                ["slider_state_id"] = _sliderState == null ? null : _sliderState.SliderStateId,
                ["slider_preset"] = _sliderState == null ? null : _sliderState.PresetName,
                ["requested_semantic_state"] = _sliderState == null ? null : _sliderState.RequestedSemanticPayload(),
                ["message_envelope"] = envelope,
                ["roll_origin"] = rollOrigin,
                ["returned_sliders"] = response.MissionSliders == null ? null : NativeValues(response.MissionSliders).ToPayload(),
                ["slider_verification"] = new Dictionary<string, object>
                {
                    ["status"] = verified ? "MATCH" : "FAILED_CLOSED",
                    ["failure_code"] = verificationError,
                    ["phase"] = _pendingSliderGate == null ? null : _pendingSliderGate.Phase
                },
                ["raw_response_packet"] = _pendingRawResponsePacket,
                ["offers"] = offers
            };
            var fingerprintOffers = new List<object>();
            for (int index = 0; index < details.Length; index++)
                fingerprintOffers.Add(OfferPayload(details[index], index + 1, null, null, null));
            fingerprint = Sha256(_serializer.Serialize(new Dictionary<string, object>
            {
                ["message_envelope"] = envelope,
                ["returned_sliders"] = response.MissionSliders == null ? null : NativeValues(response.MissionSliders).ToPayload(),
                ["offers"] = fingerprintOffers
            }));
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
                ["static_expected_mission_ql"] = _targetMissionQl,
                ["target_resolution"] = "STATIC_CHARACTER_LEVEL_AND_EXPLICIT_DIFFICULTY_DETENT_LOOKUP",
                ["mission_ql_table_source"] = MissionQlResolver.SourceRepositoryPath,
                ["mission_ql_table_sha256"] = MissionQlResolver.SourceSha256,
                ["slider_state_id"] = _sliderState.SliderStateId,
                ["slider_preset"] = _sliderState.PresetName,
                ["requested_semantic_state"] = _sliderState.RequestedSemanticPayload(),
                ["resolved_native_slider_values"] = _sliderState.ToNativeValues().ToPayload(),
                ["requested_request_count"] = _requestedRequestCount,
                ["capture_mode"] = CaptureModeLabel(),
                ["matrix_requests_per_state"] = _matrixPlan == null ? (object)null : _matrixRequestsPerState,
                ["matrix_start_index"] = _matrixPlan == null ? (object)null : _matrixPlan[0].MatrixIndex,
                ["matrix_end_index"] = _matrixPlan == null ? (object)null : _matrixPlan[_matrixPlan.Count - 1].MatrixIndex,
                ["matrix_plan"] = BuildMatrixPlanPayload(),
                ["campaign_definition"] = _itemCampaignDefinition == null ? null : _itemCampaignDefinition.ToPayload(),
                ["campaign_progress"] = _itemCampaignProgress == null ? null : _itemCampaignProgress.ToPayload(),
                ["campaign_progress_path"] = _campaignProgressPath,
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
                ["capture_contract_version"] = 3,
                ["mission_type_catalog_source"] = "HARVESTER_EMBEDDED_CAPTURE_BACKED_ICON_MAP",
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

        private object BuildMatrixPlanPayload()
        {
            if (_matrixPlan == null)
                return null;
            var result = new List<object>();
            foreach (MissionSliderPlanEntry entry in _matrixPlan)
            {
                IDictionary<string, object> payload = entry.ToPayload();
                int expectedQl;
                payload["static_expected_mission_ql"] = MissionQlResolver.TryGetMissionQl(
                    _characterLevel,
                    entry.SliderState.DifficultyDetent,
                    out expectedQl)
                    ? (object)expectedQl
                    : null;
                result.Add(payload);
            }
            return result;
        }

        private string CaptureModeLabel()
        {
            if (_itemCampaignProgress != null)
                return "MISSION_ITEM_QL_SPECTRUM_CAMPAIGN";
            return _matrixPlan == null ? "SINGLE_STATE" : "LOW_LEVEL_DISCOVERY_MATRIX";
        }

        private static IDictionary<string, object> MissionQlCandidatePayload(
            QuestAlternativeMessage response,
            int? expectedMissionQl)
        {
            int observedMissionQl;
            string error;
            bool available = TryGetMissionQlCandidate(response, out observedMissionQl, out error);
            string status = !available
                ? "UNAVAILABLE"
                : expectedMissionQl.HasValue && observedMissionQl != expectedMissionQl.Value
                    ? "MISMATCH"
                    : expectedMissionQl.HasValue ? "MATCH" : "OBSERVED_NOT_COMPARED";
            return new Dictionary<string, object>
            {
                ["source_field"] = "MissionInfo.UnkChunk3",
                ["byte_offset"] = 16,
                ["byte_width"] = 4,
                ["byte_order"] = "BIG_ENDIAN",
                ["semantics"] = "STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION",
                ["expected_mission_ql"] = expectedMissionQl.HasValue ? (object)expectedMissionQl.Value : null,
                ["observed_mission_ql_candidate"] = available ? (object)observedMissionQl : null,
                ["status"] = status,
                ["error"] = error
            };
        }

        private static bool TryGetMissionQlCandidate(
            QuestAlternativeMessage response,
            out int missionQl,
            out string error)
        {
            missionQl = 0;
            error = null;
            MissionInfo[] offers = response == null ? null : response.MissionDetails;
            if (offers == null || offers.Length == 0)
            {
                error = "MISSION_QL_CANDIDATE_NO_OFFERS";
                return false;
            }
            for (int index = 0; index < offers.Length; index++)
            {
                byte[] chunk = offers[index].UnkChunk3;
                if (chunk == null || chunk.Length < 20)
                {
                    error = "MISSION_QL_CANDIDATE_CHUNK_TOO_SHORT_AT_OFFER_" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    return false;
                }
                int candidate = (chunk[16] << 24)
                    | (chunk[17] << 16)
                    | (chunk[18] << 8)
                    | chunk[19];
                if (candidate < 1 || candidate > 250)
                {
                    error = "MISSION_QL_CANDIDATE_OUT_OF_RANGE_AT_OFFER_" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    return false;
                }
                if (index == 0)
                    missionQl = candidate;
                else if (candidate != missionQl)
                {
                    error = "MISSION_QL_CANDIDATE_NOT_UNIFORM_ACROSS_COHORT";
                    return false;
                }
            }
            return true;
        }

        private static IDictionary<string, object> OfferPayload(
            MissionInfo offer,
            int offerIndex,
            IDictionary<string, object> rollOrigin,
            string cohortId,
            string sliderStateId)
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
                ["cohort_id"] = cohortId,
                ["slider_state_id"] = sliderStateId,
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
            string clickSaverWireCode = null;
            switch (missionIcon)
            {
                case 11329:
                    captureBackedType = "FindItemReturn";
                    canonicalType = "RETURN_ITEM";
                    canonicalDisplayName = "Return Item";
                    clickSaverWireCode = "0x2C41";
                    break;
                case 11330:
                    captureBackedType = "KillPerson";
                    canonicalType = "KILL_PERSON";
                    canonicalDisplayName = "Kill Person";
                    clickSaverWireCode = "0x2C42";
                    break;
                case 11335:
                    captureBackedType = "FindPerson";
                    canonicalType = "FIND_PERSON";
                    canonicalDisplayName = "Find Person";
                    clickSaverWireCode = "0x2C47";
                    break;
                case 11337:
                    captureBackedType = "FindItem";
                    canonicalType = "FIND_ITEM";
                    canonicalDisplayName = "Find Item";
                    clickSaverWireCode = "0x2C49";
                    break;
                case 11342:
                    captureBackedType = "RepairMachine";
                    canonicalType = "REPAIR";
                    canonicalDisplayName = "Repair";
                    clickSaverWireCode = "0x2C4E";
                    break;
            }
            return new Dictionary<string, object>
            {
                ["mission_icon"] = missionIcon,
                ["capture_backed_type"] = captureBackedType,
                ["canonical_type"] = canonicalType,
                ["canonical_display_name"] = canonicalDisplayName,
                ["clicksaver_wire_code"] = clickSaverWireCode,
                ["classification_status"] = captureBackedType == null
                    ? "UNKNOWN_ICON_RAW_VALUE_PRESERVED"
                    : "CAPTURE_BACKED_EXACT_ICON_MAPPING",
                ["catalog_source"] = "HARVESTER_EMBEDDED_CAPTURE_BACKED_ICON_MAP"
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

        private static NativeMissionSliderValues NativeValues(MissionSliders sliders)
        {
            return sliders == null
                ? null
                : new NativeMissionSliderValues(
                    sliders.Difficulty,
                    sliders.GoodBad,
                    sliders.OrderChaos,
                    sliders.OpenHidden,
                    sliders.PhysicalMystical,
                    sliders.HeadonStealth,
                    sliders.CreditsXp);
        }

        private static bool TryDecodeQuestAlternative(
            byte[] packet,
            out QuestAlternativeMessage message,
            out string error)
        {
            message = null;
            error = null;
            if (packet == null || packet.Length == 0)
            {
                error = "PACKET_EMPTY";
                return false;
            }
            try
            {
                Message decoded = PacketFactory.Disassemble(packet);
                message = decoded == null ? null : decoded.Body as QuestAlternativeMessage;
                if (message == null)
                {
                    error = "PACKET_IS_NOT_QUEST_ALTERNATIVE";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().FullName + ": " + exception.Message;
                return false;
            }
        }

        private static IDictionary<string, object> RawPacketPayload(byte[] packet, string captureSource)
        {
            return new Dictionary<string, object>
            {
                ["capture_source"] = captureSource,
                ["byte_length"] = packet == null ? 0 : packet.Length,
                ["sha256"] = packet == null ? null : Sha256(packet),
                ["base64"] = Base64(packet)
            };
        }

        private static IDictionary<string, object> GateFailurePayload(
            SliderRequestGate gate,
            NativeMissionSliderValues requested,
            NativeMissionSliderValues observed,
            IDictionary<string, object> rawPacket)
        {
            return new Dictionary<string, object>
            {
                ["slider_state_id"] = gate == null || gate.RequestedState == null ? null : gate.RequestedState.SliderStateId,
                ["verification_phase"] = gate == null ? null : gate.Phase,
                ["requested_native_values"] = requested == null ? null : requested.ToPayload(),
                ["observed_native_values"] = observed == null ? null : observed.ToPayload(),
                ["raw_packet"] = rawPacket
            };
        }

        private void FailClosed(string code, IDictionary<string, object> details)
        {
            if (_journal != null)
            {
                var payload = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["disposition"] = "FAILED_CLOSED_NO_FURTHER_REQUESTS",
                    ["slider_state_id"] = _sliderState == null ? null : _sliderState.SliderStateId,
                    ["verification_phase"] = _pendingSliderGate == null ? null : _pendingSliderGate.Phase,
                    ["details"] = details
                };
                _journal.Append("error", _sessionId, _pendingRequestId, payload);
            }
            Stop((code ?? "unknown_error").ToLowerInvariant(), true);
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

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
