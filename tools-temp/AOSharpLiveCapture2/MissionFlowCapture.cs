namespace AOSharpLiveCapture
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.Messages;

    /// <summary>
    /// Focused mission-flow extractor for RK mission terminal work. Writes <c>mission-flow.log</c> with
    /// roll / accept / key / teleport / playfield-init evidence needed to reconstruct the right mission
    /// instance id on pull. Additive only — does not change existing capture pipelines.
    /// Uses reflection for offer/teleport details because AOSharp's messaging fork names fields differently
    /// from AORebirth server messaging (MissionDetails vs QuestInfos, etc.).
    /// </summary>
    internal sealed class MissionFlowCapture
    {
        // Live mission-key template from captures (SimpleItemFullUpdate ACGItemTemplateID).
        private const int MissionKeyTemplateId = 28577;

        // Live repair-tool template observed immediately before accepted RepairMachine missions.
        private const int MissionRepairToolTemplateId = 100292;

        // Mission instance playfields observed in live AO are in a high id band (e.g. 1419307).
        private const int MissionInstancePlayfieldMin = 1_000_000;

        private readonly object syncRoot = new object();

        private readonly Action<string, string> logEvent;

        private readonly Func<MissionPlayerSnapshot> localPlayerSnapshotProvider;

        private readonly HashSet<string> activeQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> knownQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private StreamWriter log;

        private string logPath = string.Empty;

        private string lastPlayfieldId = string.Empty;

        private string observedLocalIdentity = string.Empty;

        private string pendingCreateQuest = string.Empty;

        private DateTime pendingCreateQuestUtc = DateTime.MinValue;

        private string pendingMissionKey = string.Empty;

        private long rollOrdinal;

        private long pendingRollOrdinal;

        private DateTime pendingRollUtc = DateTime.MinValue;

        private ulong? pendingRollCashBefore;

        private ulong? pendingRollCashAfter;

        private bool pendingRollCashObserved;

        private ulong? lastCashBalance;

        public MissionFlowCapture(Action<string, string> logEvent)
            : this(logEvent, null)
        {
        }

        public MissionFlowCapture(
            Action<string, string> logEvent,
            Func<MissionPlayerSnapshot> localPlayerSnapshotProvider)
        {
            this.logEvent = logEvent;
            this.localPlayerSnapshotProvider = localPlayerSnapshotProvider;
        }

        public void BindSession(string sessionDirectory)
        {
            lock (this.syncRoot)
            {
                this.CloseLog_NoLock();
                this.ResetSessionState_NoLock();
                if (string.IsNullOrEmpty(sessionDirectory))
                {
                    return;
                }

                this.logPath = Path.Combine(sessionDirectory, "mission-flow.log");
                this.log = new StreamWriter(this.logPath, false, Encoding.UTF8) { AutoFlush = true };
                this.WriteLine_NoLock(
                    "MISSION-FLOW",
                    "session bound path=" + this.logPath
                    + " (roll/accept/key/teleport/playfield-init for mission instance reconstruction)");
                if (this.logEvent != null)
                {
                    this.logEvent("PLUGIN", "Mission flow log: " + this.logPath);
                }
            }
        }

        public void Teardown()
        {
            lock (this.syncRoot)
            {
                this.CloseLog_NoLock();
            }
        }

        public void OnN3MessageReceived(N3Message message)
        {
            this.HandleMessage("IN", string.Empty, message);
        }

        public void OnN3MessageSent(N3Message message)
        {
            this.HandleMessage("OUT", string.Empty, message);
        }

        public void OnN3MessageReceived(int decodedSequence, N3Message message)
        {
            this.HandleMessage(
                "IN",
                "decodedSequence=" + decodedSequence.ToString(CultureInfo.InvariantCulture),
                message);
        }

        public void OnN3MessageSent(int decodedSequence, N3Message message)
        {
            this.HandleMessage(
                "OUT",
                "decodedSequence=" + decodedSequence.ToString(CultureInfo.InvariantCulture),
                message);
        }

        public void OnCapturedN3Message(
            string direction,
            DateTime capturedUtc,
            long globalOrdinal,
            int rawSequence,
            N3Message message)
        {
            this.HandleMessage(
                direction,
                "capturedUtc=" + capturedUtc.ToString("o", CultureInfo.InvariantCulture)
                + " globalOrdinal=" + globalOrdinal.ToString(CultureInfo.InvariantCulture)
                + " rawSequence=" + rawSequence.ToString(CultureInfo.InvariantCulture),
                message);
        }

        public void OnPlayfieldInit(uint playfieldId)
        {
            lock (this.syncRoot)
            {
                this.lastPlayfieldId = playfieldId.ToString(CultureInfo.InvariantCulture);
                bool likelyMissionInstance = playfieldId >= MissionInstancePlayfieldMin;
                this.WriteLine_NoLock(
                    "PLAYFIELD-INIT",
                    "pf=" + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + " hex=0x" + playfieldId.ToString("X", CultureInfo.InvariantCulture)
                    + " likelyMissionInstance=" + likelyMissionInstance
                    + " activeQuests=" + this.FormatActiveQuests_NoLock()
                    + " playerSnapshot=" + this.FormatLocalPlayerSnapshot_NoLock(true));
            }
        }

        public void OnTeleportStarted()
        {
            this.WriteLine(
                "TELEPORT",
                "started lastPf=" + this.lastPlayfieldId
                + " activeQuests=" + this.FormatActiveQuests()
                + " playerSnapshot=" + this.FormatLocalPlayerSnapshot(false));
        }

        public void OnTeleportEnded()
        {
            this.WriteLine(
                "TELEPORT",
                "ended lastPf=" + this.lastPlayfieldId
                + " activeQuests=" + this.FormatActiveQuests()
                + " playerSnapshot=" + this.FormatLocalPlayerSnapshot(false));
        }

        private void HandleMessage(string direction, string sequenceContext, N3Message message)
        {
            if (message == null)
            {
                return;
            }

            lock (this.syncRoot)
            {
                try
                {
                    string context = string.IsNullOrEmpty(sequenceContext)
                                         ? string.Empty
                                         : sequenceContext + " ";
                    string typeName = message.N3MessageType.ToString();
                    if (string.Equals(typeName, "GenericCmd", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogGenericCommand_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "Stat", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(typeName, "SetStat", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogTrackedStats_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "CreateQuest", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase))
                        {
                            this.ObserveLocalIdentity_NoLock(
                                GetProp(message, "Identity") ?? GetProp(message, "User"));
                        }

                        if (!this.IsLocalMessage_NoLock(message))
                        {
                            return;
                        }

                        this.pendingCreateQuest =
                            FormatAnyIdentity(
                                GetProp(message, "MissionId")
                                ?? GetProp(message, "QuestIdentity"))
                            ?? "unknown";
                        this.pendingCreateQuestUtc = DateTime.UtcNow;
                        this.pendingMissionKey = string.Empty;
                        this.WriteLine_NoLock(
                            direction + "-CREATE-QUEST",
                            context
                            + "selectedOffer=" + this.pendingCreateQuest
                            + " identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                            + " rollOrdinal=" + this.FormatNullableOrdinal_NoLock(this.rollOrdinal)
                            + " playerSnapshot=" + this.FormatLocalPlayerSnapshot_NoLock(true));
                        return;
                    }

                    if (string.Equals(typeName, "QuestAlternative", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogQuestAlternative_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "QuestFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogQuestFullUpdate_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "Quest", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogQuestMessage_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "SimpleItemFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogMissionItem_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "ContainerAddItem", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogContainerAddItem_NoLock(direction, context, message);
                        return;
                    }

                    if (string.Equals(typeName, "N3Teleport", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(typeName, "Teleport", StringComparison.OrdinalIgnoreCase))
                    {
                        this.LogTeleport_NoLock(direction, context, message);
                    }
                }
                catch (Exception ex)
                {
                    this.WriteLine_NoLock(
                        "MISSION-FLOW-ERROR",
                        (string.IsNullOrEmpty(sequenceContext) ? string.Empty : sequenceContext + " ")
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private void LogGenericCommand_NoLock(string direction, string context, N3Message message)
        {
            object user = GetProp(message, "User") ?? GetProp(message, "Identity");
            if (string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase))
            {
                this.ObserveLocalIdentity_NoLock(user);
            }

            if (!this.IsLocalIdentity_NoLock(user) && !this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            object target = GetProp(message, "Target");
            object source = GetProp(message, "Source");
            string action = Convert.ToString(GetProp(message, "Action") ?? string.Empty) ?? string.Empty;
            bool isTerminalUse =
                string.Equals(action, "Use", StringComparison.OrdinalIgnoreCase)
                && IsIdentityType(target, "MissionTerminal", 0xDAC1);
            if (!isTerminalUse && !this.HasMissionContext_NoLock())
            {
                return;
            }

            this.WriteLine_NoLock(
                direction + (isTerminalUse ? "-TERMINAL-USE" : "-MISSION-INTERACTION"),
                context
                + "action=" + action
                + " user=" + (FormatAnyIdentity(user) ?? string.Empty)
                + " source=" + (FormatAnyIdentity(source) ?? string.Empty)
                + " target=" + (FormatAnyIdentity(target) ?? string.Empty)
                + " temp1=" + Convert.ToString(GetProp(message, "Temp1") ?? string.Empty, CultureInfo.InvariantCulture)
                + " count=" + Convert.ToString(GetProp(message, "Count") ?? string.Empty, CultureInfo.InvariantCulture)
                + " temp4=" + Convert.ToString(GetProp(message, "Temp4") ?? string.Empty, CultureInfo.InvariantCulture)
                + " activeQuests=" + this.FormatActiveQuests_NoLock()
                + " playerSnapshot=" + this.FormatLocalPlayerSnapshot_NoLock(isTerminalUse));
        }

        private void LogTrackedStats_NoLock(string direction, string context, N3Message message)
        {
            if (!this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            object stats = GetProp(message, "Stats");
            if (stats != null)
            {
                foreach (object stat in Enumerate(stats))
                {
                    this.LogTrackedStat_NoLock(
                        direction,
                        context,
                        GetProp(message, "Identity"),
                        GetProp(stat, "Value1") ?? GetProp(stat, "Stat"),
                        GetProp(stat, "Value2") ?? GetProp(stat, "Value"));
                }

                return;
            }

            this.LogTrackedStat_NoLock(
                direction,
                context,
                GetProp(message, "Identity"),
                GetProp(message, "Stat"),
                GetProp(message, "Value"));
        }

        private void LogTrackedStat_NoLock(
            string direction,
            string context,
            object identity,
            object statValue,
            object value)
        {
            string statName = Convert.ToString(statValue, CultureInfo.InvariantCulture) ?? string.Empty;
            int statNumber;
            bool isCash = string.Equals(statName, "Cash", StringComparison.OrdinalIgnoreCase)
                          || (TryToInt(statValue, out statNumber) && statNumber == 0x3D);
            bool isXp = string.Equals(statName, "XP", StringComparison.OrdinalIgnoreCase)
                        || (TryToInt(statValue, out statNumber) && statNumber == 0x34);
            bool isLevel = string.Equals(statName, "Level", StringComparison.OrdinalIgnoreCase)
                           || (TryToInt(statValue, out statNumber) && statNumber == 0x36);
            if (!isCash && !isXp && !isLevel)
            {
                return;
            }

            ulong numeric;
            if (!TryToUInt64(value, out numeric))
            {
                return;
            }

            string deltaText = "unknown";
            bool observedSinceRoll = this.pendingRollOrdinal > 0
                                     && this.pendingRollUtc != DateTime.MinValue;
            if (isCash)
            {
                if (this.lastCashBalance.HasValue)
                {
                    long delta = (long)numeric - (long)this.lastCashBalance.Value;
                    deltaText = delta.ToString(CultureInfo.InvariantCulture);
                }

                this.lastCashBalance = numeric;
                if (observedSinceRoll)
                {
                    this.pendingRollCashObserved = true;
                    this.pendingRollCashAfter = numeric;
                }
            }

            this.WriteLine_NoLock(
                direction + "-MISSION-STAT",
                context
                + "identity=" + (FormatAnyIdentity(identity) ?? string.Empty)
                + " stat=" + statName
                + " value=" + numeric.ToString(CultureInfo.InvariantCulture)
                + " delta=" + deltaText
                + " rollOrdinal=" + this.FormatNullableOrdinal_NoLock(this.pendingRollOrdinal)
                + " observedSinceRollRequest=" + observedSinceRoll
                + " activeQuests=" + this.FormatActiveQuests_NoLock());
        }

        private void LogQuestAlternative_NoLock(string direction, string context, N3Message message)
        {
            if (string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase))
            {
                this.ObserveLocalIdentity_NoLock(GetProp(message, "Identity"));
            }

            if (!this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            object offers = GetProp(message, "MissionDetails") ?? GetProp(message, "QuestInfos");
            int offerCount = CountEnumerable(offers);
            object sliders = GetProp(message, "MissionSliders");
            bool isRequest = string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase);
            long correlatedRollOrdinal;
            if (isRequest)
            {
                this.rollOrdinal++;
                this.pendingRollOrdinal = this.rollOrdinal;
                this.pendingRollUtc = DateTime.UtcNow;
                this.pendingRollCashObserved = false;
                this.pendingRollCashAfter = null;
                this.FormatLocalPlayerSnapshot_NoLock(true);
                this.pendingRollCashBefore = this.lastCashBalance;
                correlatedRollOrdinal = this.pendingRollOrdinal;
            }
            else
            {
                correlatedRollOrdinal = this.pendingRollOrdinal;
            }

            var sb = new StringBuilder();
            sb.Append(context);
            sb.Append("rollOrdinal=").Append(this.FormatNullableOrdinal_NoLock(correlatedRollOrdinal));
            sb.Append(" identity=").Append(FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty);
            sb.Append(" terminal=").Append(
                FormatAnyIdentity(GetProp(message, "Terminal") ?? GetProp(message, "MissionTerminalIdentity"))
                ?? string.Empty);
            sb.Append(" scope=").Append(GetProp(message, "Scope") ?? GetProp(message, "Unknown5") ?? string.Empty);
            sb.Append(" unknown1=").Append(GetProp(message, "Unknown1") ?? string.Empty);
            sb.Append(" unknown2=").Append(GetProp(message, "Unknown2") ?? string.Empty);
            if (isRequest)
            {
                sb.Append(" sliders=[difficulty=")
                    .Append(GetProp(sliders, "Difficulty") ?? GetProp(sliders, "Level") ?? GetProp(message, "LevelSlider") ?? "?");
                sb.Append(" goodBad=").Append(GetProp(sliders, "GoodBad") ?? GetProp(message, "GoodBadSlider") ?? "?");
                sb.Append(" orderChaos=").Append(GetProp(sliders, "OrderChaos") ?? GetProp(message, "OrderChaosSlider") ?? "?");
                sb.Append(" openHidden=").Append(GetProp(sliders, "OpenHidden") ?? GetProp(message, "OpenHiddenSlider") ?? "?");
                sb.Append(" physicalMystical=").Append(GetProp(sliders, "PhysicalMystical") ?? GetProp(message, "PhysicalMysticalSlider") ?? "?");
                sb.Append(" headonStealth=")
                    .Append(
                        GetProp(sliders, "HeadonStealth")
                        ?? GetProp(sliders, "HeadOnStealth")
                        ?? GetProp(message, "HeadOnStealthSlider")
                        ?? "?");
                sb.Append(" creditsXp=")
                    .Append(
                        GetProp(sliders, "CreditsXp")
                        ?? GetProp(sliders, "MoneyExperience")
                        ?? GetProp(message, "MoneyExperienceSlider")
                        ?? "?");
                sb.Append(']');
                sb.Append(" cashBefore=").Append(FormatNullableUnsigned(this.pendingRollCashBefore));
                sb.Append(" playerSnapshot=").Append(this.FormatLocalPlayerSnapshot_NoLock(false));
            }
            else
            {
                sb.Append(" responseSliderBytes=[difficulty=")
                    .Append(GetProp(sliders, "Difficulty") ?? "?");
                sb.Append(" goodBad=").Append(GetProp(sliders, "GoodBad") ?? "?");
                sb.Append(" orderChaos=").Append(GetProp(sliders, "OrderChaos") ?? "?");
                sb.Append(" openHidden=").Append(GetProp(sliders, "OpenHidden") ?? "?");
                sb.Append(" physicalMystical=").Append(GetProp(sliders, "PhysicalMystical") ?? "?");
                sb.Append(" headonStealth=")
                    .Append(GetProp(sliders, "HeadonStealth") ?? GetProp(sliders, "HeadOnStealth") ?? "?");
                sb.Append(" creditsXp=").Append(GetProp(sliders, "CreditsXp") ?? "?");
                sb.Append("] semanticRequestValues=false");
                sb.Append(" cashBefore=").Append(FormatNullableUnsigned(this.pendingRollCashBefore));
                sb.Append(" cashAfter=").Append(FormatNullableUnsigned(this.pendingRollCashAfter));
                sb.Append(" cashUpdateObserved=").Append(this.pendingRollCashObserved);
                if (this.pendingRollCashBefore.HasValue && this.pendingRollCashAfter.HasValue)
                {
                    sb.Append(" cashDelta=")
                        .Append(
                            ((long)this.pendingRollCashAfter.Value - (long)this.pendingRollCashBefore.Value)
                                .ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(" cashDelta=unknown");
                }
            }

            sb.Append(" offers=").Append(offerCount);
            this.WriteLine_NoLock(
                direction + (isRequest ? "-ROLL-REQUEST" : "-ROLL-RESPONSE"),
                sb.ToString());

            int index = 0;
            foreach (object info in Enumerate(offers))
            {
                if (info != null)
                {
                    this.LogMissionOffer_NoLock(direction, context, correlatedRollOrdinal, index, info);
                }

                index++;
            }

            if (!isRequest)
            {
                this.pendingRollOrdinal = 0;
                this.pendingRollUtc = DateTime.MinValue;
                this.pendingRollCashBefore = null;
                this.pendingRollCashAfter = null;
                this.pendingRollCashObserved = false;
            }
        }

        private void LogMissionOffer_NoLock(
            string direction,
            string context,
            long correlatedRollOrdinal,
            int index,
            object info)
        {
            object missionIdentity =
                GetProp(info, "MissionIdentity")
                ?? GetProp(info, "QuestIdentity")
                ?? GetProp(info, "Identity");
            object playfield =
                GetProp(info, "Playfield")
                ?? GetProp(info, "PlayfieldId")
                ?? GetProp(info, "PlayfieldIdentity");
            object location = GetProp(info, "Location") ?? GetProp(info, "Position");
            object rewards =
                GetProp(info, "MissionItemData")
                ?? GetProp(info, "ItemRewards")
                ?? GetProp(info, "Rewards");

            var sb = new StringBuilder();
            sb.Append(context);
            sb.Append("rollOrdinal=").Append(this.FormatNullableOrdinal_NoLock(correlatedRollOrdinal));
            sb.Append(" index=").Append(index.ToString(CultureInfo.InvariantCulture));
            sb.Append(" mission=").Append(FormatAnyIdentity(missionIdentity) ?? string.Empty);
            sb.Append(" title=").Append(Quote(Convert.ToString(GetProp(info, "Title") ?? GetProp(info, "ShortInfo") ?? string.Empty)));
            sb.Append(" description=")
                .Append(Quote(Convert.ToString(GetProp(info, "Description") ?? GetProp(info, "Info") ?? string.Empty)));
            sb.Append(" terminal=")
                .Append(
                    FormatAnyIdentity(GetProp(info, "TerminalIdentity") ?? GetProp(info, "Terminal"))
                    ?? string.Empty);
            sb.Append(" rewardDescriptorVersion=").Append(GetProp(info, "RewardDescriptorVersion") ?? string.Empty);
            sb.Append(" credits=").Append(GetProp(info, "Credits") ?? string.Empty);
            sb.Append(" xp=").Append(GetProp(info, "XpReward") ?? GetProp(info, "XPReward") ?? string.Empty);
            sb.Append(" unk1=").Append(GetProp(info, "Unk1") ?? string.Empty);
            sb.Append(" icon=")
                .Append(GetProp(info, "MissionIcon") ?? GetProp(info, "MissionIconId") ?? GetProp(info, "Icon") ?? string.Empty);
            sb.Append(" playfield=").Append(FormatAnyIdentity(playfield) ?? Convert.ToString(playfield ?? string.Empty));
            sb.Append(" location=").Append(FormatVector(location));
            sb.Append(" rewards=").Append(FormatRewards(rewards));
            sb.Append(" unkChunk1=").Append(HexBytes(GetProp(info, "UnkChunk1")));
            sb.Append(" unkChunk2=").Append(HexBytes(GetProp(info, "UnkChunk2")));
            sb.Append(" unkChunk3=").Append(HexBytes(GetProp(info, "UnkChunk3")));
            sb.Append(" unkChunk4=").Append(HexBytes(GetProp(info, "UnkChunk4")));
            sb.Append(" unkChunk5=").Append(HexBytes(GetProp(info, "UnkChunk5")));
            sb.Append(" unkChunk6=").Append(HexBytes(GetProp(info, "UnkChunk6")));
            sb.Append(" missionQlNamedFieldAvailable=false");
            this.WriteLine_NoLock(direction + "-MISSION-OFFER", sb.ToString());
        }

        private void LogQuestFullUpdate_NoLock(string direction, string context, N3Message message)
        {
            if (!this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            object quests =
                GetProp(message, "Quests")
                ?? GetProp(message, "MissionDetails")
                ?? GetProp(message, "QuestInfos");
            int count = CountEnumerable(quests);
            bool hasRecentCreate = this.HasRecentPendingCreate_NoLock();
            string selectedOffer = hasRecentCreate ? this.pendingCreateQuest : string.Empty;
            string missionKey = hasRecentCreate ? this.pendingMissionKey : string.Empty;
            string firstNewQuest = string.Empty;

            this.WriteLine_NoLock(
                direction + "-QUEST-FULL",
                context
                + "identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                + " unknown=" + (GetProp(message, "Unknown") ?? string.Empty)
                + " quests=" + count.ToString(CultureInfo.InvariantCulture)
                + " pendingSelectedOffer=" + selectedOffer
                + " pendingMissionKey=" + missionKey);

            int index = 0;
            foreach (object quest in Enumerate(quests))
            {
                if (quest == null)
                {
                    index++;
                    continue;
                }

                string questIdentity =
                    FormatAnyIdentity(
                        GetProp(quest, "QuestId")
                        ?? GetProp(quest, "QuestIdentity")
                        ?? GetProp(quest, "Identity"))
                    ?? string.Empty;
                bool isNew = !string.IsNullOrEmpty(questIdentity)
                             && this.knownQuestIds.Add(questIdentity);
                if (!string.IsNullOrEmpty(questIdentity))
                {
                    this.activeQuestIds.Add(questIdentity);
                }

                if (isNew && string.IsNullOrEmpty(firstNewQuest))
                {
                    firstNewQuest = questIdentity;
                }

                object rewards =
                    GetProp(quest, "MissionItemData")
                    ?? GetProp(quest, "ItemRewards")
                    ?? GetProp(quest, "Rewards");
                this.WriteLine_NoLock(
                    direction + "-MISSION-QUEST",
                    context
                    + "index=" + index.ToString(CultureInfo.InvariantCulture)
                    + " quest=" + questIdentity
                    + " isNew=" + isNew
                    + " selectedOffer=" + (isNew ? selectedOffer : string.Empty)
                    + " temporalCorrelationOnly=" + (isNew && hasRecentCreate)
                    + " icon=" + (GetProp(quest, "MissionIconId") ?? GetProp(quest, "MissionIcon") ?? string.Empty)
                    + " title=" + Quote(Convert.ToString(GetProp(quest, "ShortInfo") ?? string.Empty))
                    + " description=" + Quote(Convert.ToString(GetProp(quest, "LongInfo") ?? string.Empty))
                    + " rewards=" + FormatRewards(rewards)
                    + " decoded=" + DescribeStructured(quest, 3, 24000));

                int actionIndex = 0;
                object actions = GetProp(quest, "QuestActions") ?? GetProp(quest, "Actions");
                foreach (object action in Enumerate(actions))
                {
                    if (action != null)
                    {
                        this.WriteLine_NoLock(
                            direction + "-MISSION-ACTION",
                            context
                            + "quest=" + questIdentity
                            + " index=" + actionIndex.ToString(CultureInfo.InvariantCulture)
                            + " version=" + (GetProp(action, "Version") ?? string.Empty)
                            + " action=" + (FormatAnyIdentity(GetProp(action, "Action")) ?? string.Empty)
                            + " playfield="
                            + (FormatAnyIdentity(GetProp(action, "PlayfieldId") ?? GetProp(action, "Playfield")) ?? string.Empty)
                            + " position=" + FormatVector(GetProp(action, "Position"))
                            + " decoded=" + DescribeStructured(action, 2, 8000));
                    }

                    actionIndex++;
                }

                index++;
            }

            if (hasRecentCreate && !string.IsNullOrEmpty(firstNewQuest))
            {
                this.WriteLine_NoLock(
                    direction + "-MISSION-ACCEPT-CORRELATION",
                    context
                    + "selectedOffer=" + selectedOffer
                    + " acceptedQuest=" + firstNewQuest
                    + " missionKey=" + missionKey
                    + " temporalCorrelationOnly=true");
                this.pendingCreateQuest = string.Empty;
                this.pendingCreateQuestUtc = DateTime.MinValue;
                this.pendingMissionKey = string.Empty;
            }
        }

        private void LogQuestMessage_NoLock(string direction, string context, N3Message message)
        {
            if (string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase))
            {
                this.ObserveLocalIdentity_NoLock(GetProp(message, "Identity"));
            }

            if (!this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            string action = Convert.ToString(GetProp(message, "Action") ?? string.Empty) ?? string.Empty;
            string mission = FormatAnyIdentity(GetProp(message, "Mission")) ?? string.Empty;
            if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(mission))
            {
                this.activeQuestIds.Remove(mission);
            }

            this.WriteLine_NoLock(
                direction + "-QUEST",
                context
                + "action=" + action
                + " mission=" + mission
                + " identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                + " unknown1=" + (GetProp(message, "Unknown1") ?? string.Empty)
                + " unknown2=" + (GetProp(message, "Unknown2") ?? string.Empty)
                + " unknown3=" + (GetProp(message, "Unknown3") ?? string.Empty)
                + " activeQuests=" + this.FormatActiveQuests_NoLock()
                + " playerSnapshot=" + this.FormatLocalPlayerSnapshot_NoLock(true));
        }

        private void LogMissionItem_NoLock(string direction, string context, N3Message message)
        {
            int lowId = 0;
            int highId = 0;
            int ql = 0;
            object stats = GetProp(message, "Stats");
            foreach (object stat in Enumerate(stats))
            {
                if (stat == null)
                {
                    continue;
                }

                object value1 = GetProp(stat, "Value1") ?? GetProp(stat, "Stat") ?? GetProp(stat, "Key");
                object value2 = GetProp(stat, "Value2") ?? GetProp(stat, "Value");
                string statName = Convert.ToString(value1) ?? string.Empty;
                int numeric;
                if (!TryToInt(value2, out numeric))
                {
                    continue;
                }

                if (statName.IndexOf("ACGItemTemplateID2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    highId = numeric;
                }
                else if (statName.IndexOf("ACGItemTemplateID", StringComparison.OrdinalIgnoreCase) >= 0
                         || string.Equals(statName, "StaticInstance", StringComparison.OrdinalIgnoreCase))
                {
                    lowId = numeric;
                }
                else if (statName.IndexOf("ACGItemLevel", StringComparison.OrdinalIgnoreCase) >= 0
                         || string.Equals(statName, "Level", StringComparison.OrdinalIgnoreCase))
                {
                    ql = numeric;
                }
            }

            object identity = GetProp(message, "Identity");
            bool isMissionKey =
                lowId == MissionKeyTemplateId
                || highId == MissionKeyTemplateId
                || IsIdentityType(identity, "MissionKey", 0xC76D);
            bool isRepairTool =
                lowId == MissionRepairToolTemplateId
                || highId == MissionRepairToolTemplateId;
            bool isAcceptSidecar = this.HasRecentPendingCreate_NoLock();
            if (!isMissionKey && !isRepairTool && !isAcceptSidecar)
            {
                return;
            }

            object ownerType = GetProp(message, "OwnerType") ?? GetProp(message, "Identitytype");
            object ownerInstance = GetProp(message, "OwnerInstance") ?? GetProp(message, "Instance");
            string owner = FormatIdentityParts(ownerType, ownerInstance);
            if (!string.IsNullOrEmpty(owner)
                && !this.IsLocalIdentityText_NoLock(owner)
                && !isAcceptSidecar)
            {
                return;
            }

            string category = isMissionKey
                                  ? "MISSION-KEY"
                                  : isRepairTool
                                      ? "MISSION-REPAIR-TOOL"
                                      : "MISSION-ACCEPT-ITEM";
            string itemIdentity = FormatAnyIdentity(identity) ?? string.Empty;
            if (isMissionKey)
            {
                this.pendingMissionKey = itemIdentity;
            }

            this.WriteLine_NoLock(
                direction + "-" + category,
                context
                + "item=" + itemIdentity
                + " owner=" + owner
                + " playfieldId=" + (GetProp(message, "PlayfieldId") ?? GetProp(message, "Playfield") ?? string.Empty)
                + " stateMachine=" + (FormatAnyIdentity(GetProp(message, "StateMachine")) ?? string.Empty)
                + " unknown1=" + (GetProp(message, "Unknown1") ?? string.Empty)
                + " unknown2=" + (GetProp(message, "Unknown2") ?? string.Empty)
                + " position=" + FormatVector(GetProp(message, "Position"))
                + " rotation=" + DescribeStructured(GetProp(message, "Rotation"), 1, 1000)
                + " low=" + lowId.ToString(CultureInfo.InvariantCulture)
                + " high=" + highId.ToString(CultureInfo.InvariantCulture)
                + " ql=" + ql.ToString(CultureInfo.InvariantCulture)
                + " stats=" + FormatStats(stats)
                + " nameSource=raw-only"
                + " pendingSelectedOffer=" + (this.pendingCreateQuest ?? string.Empty));
        }

        private void LogContainerAddItem_NoLock(string direction, string context, N3Message message)
        {
            if (!this.HasMissionContext_NoLock() || !this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            this.WriteLine_NoLock(
                direction + "-MISSION-CONTAINER-ADD",
                context
                + "identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                + " source=" + (FormatAnyIdentity(GetProp(message, "Source")) ?? string.Empty)
                + " target=" + (FormatAnyIdentity(GetProp(message, "Target")) ?? string.Empty)
                + " slot=" + (GetProp(message, "Slot") ?? string.Empty)
                + " pendingSelectedOffer=" + (this.pendingCreateQuest ?? string.Empty)
                + " activeQuests=" + this.FormatActiveQuests_NoLock());
        }

        private void LogTeleport_NoLock(string direction, string context, N3Message message)
        {
            if (!this.IsLocalMessage_NoLock(message))
            {
                return;
            }

            object destPf = GetProp(message, "Playfield")
                            ?? GetProp(message, "DestinationPlayfield")
                            ?? GetProp(message, "PlayfieldId");
            object changePf = GetProp(message, "ChangePlayfield");
            object playfield2 = GetProp(message, "Playfield2");
            int destInstance = ExtractInstance(destPf);
            int changeInstance = ExtractInstance(changePf);
            int pf2Instance = ExtractInstance(playfield2);
            bool likelyMissionInstance = destInstance >= MissionInstancePlayfieldMin
                                         || changeInstance >= MissionInstancePlayfieldMin
                                         || pf2Instance >= MissionInstancePlayfieldMin;

            var sb = new StringBuilder();
            sb.Append(context);
            sb.Append("identity=").Append(FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty);
            sb.Append(" destPf=").Append(FormatAnyIdentity(destPf) ?? Convert.ToString(destPf ?? string.Empty));
            sb.Append(" changePf=").Append(FormatAnyIdentity(changePf) ?? Convert.ToString(changePf ?? string.Empty));
            sb.Append(" playfield2=").Append(FormatAnyIdentity(playfield2) ?? Convert.ToString(playfield2 ?? string.Empty));
            sb.Append(" gameServerId=").Append(GetProp(message, "GameServerId") ?? string.Empty);
            sb.Append(" sgId=").Append(GetProp(message, "SgId") ?? string.Empty);
            sb.Append(" destination=").Append(FormatVector(GetProp(message, "Destination") ?? GetProp(message, "Position")));
            sb.Append(" likelyMissionInstance=").Append(likelyMissionInstance);
            sb.Append(" activeQuests=").Append(this.FormatActiveQuests_NoLock());
            sb.Append(" playerSnapshot=").Append(this.FormatLocalPlayerSnapshot_NoLock(false));
            sb.Append(" detail=").Append(DescribeStructured(message, 2, 8000));
            this.WriteLine_NoLock(direction + "-N3-TELEPORT", sb.ToString());
        }

        private void ResetSessionState_NoLock()
        {
            this.lastPlayfieldId = string.Empty;
            this.observedLocalIdentity = string.Empty;
            this.pendingCreateQuest = string.Empty;
            this.pendingCreateQuestUtc = DateTime.MinValue;
            this.pendingMissionKey = string.Empty;
            this.rollOrdinal = 0;
            this.pendingRollOrdinal = 0;
            this.pendingRollUtc = DateTime.MinValue;
            this.pendingRollCashBefore = null;
            this.pendingRollCashAfter = null;
            this.pendingRollCashObserved = false;
            this.lastCashBalance = null;
            this.activeQuestIds.Clear();
            this.knownQuestIds.Clear();
        }

        private MissionPlayerSnapshot TryGetLocalPlayerSnapshot_NoLock()
        {
            if (this.localPlayerSnapshotProvider == null)
            {
                return null;
            }

            try
            {
                return this.localPlayerSnapshotProvider();
            }
            catch
            {
                return null;
            }
        }

        private string FormatLocalPlayerSnapshot(bool refreshCash)
        {
            lock (this.syncRoot)
            {
                return this.FormatLocalPlayerSnapshot_NoLock(refreshCash);
            }
        }

        private string FormatLocalPlayerSnapshot_NoLock(bool refreshCash)
        {
            MissionPlayerSnapshot snapshot = this.TryGetLocalPlayerSnapshot_NoLock();
            if (snapshot == null)
            {
                return "{player=" + (this.observedLocalIdentity ?? string.Empty)
                       + " level=unknown cash=unknown xp=unknown}";
            }

            string identity = FormatAnyIdentity(snapshot.Identity) ?? string.Empty;
            if (!string.IsNullOrEmpty(identity))
            {
                this.observedLocalIdentity = identity;
            }

            if (refreshCash && snapshot.Cash.HasValue && snapshot.Cash.Value >= 0)
            {
                this.lastCashBalance = (ulong)snapshot.Cash.Value;
            }

            return "{player=" + identity
                   + " level=" + FormatNullableSigned(snapshot.Level)
                   + " cash=" + FormatNullableSigned(snapshot.Cash)
                   + " xp=" + FormatNullableSigned(snapshot.Xp)
                   + "}";
        }

        private void ObserveLocalIdentity_NoLock(object identity)
        {
            MissionPlayerSnapshot snapshot = this.TryGetLocalPlayerSnapshot_NoLock();
            string provided = snapshot == null ? string.Empty : FormatAnyIdentity(snapshot.Identity);
            if (!string.IsNullOrEmpty(provided))
            {
                this.observedLocalIdentity = provided;
                return;
            }

            if (identity == null || !IsIdentityType(identity, "SimpleChar", 0xC350))
            {
                return;
            }

            string observed = FormatAnyIdentity(identity);
            if (!string.IsNullOrEmpty(observed))
            {
                this.observedLocalIdentity = observed;
            }
        }

        private bool IsLocalMessage_NoLock(N3Message message)
        {
            return message != null && this.IsLocalIdentity_NoLock(GetProp(message, "Identity"));
        }

        private bool IsLocalIdentity_NoLock(object identity)
        {
            if (identity == null)
            {
                return false;
            }

            MissionPlayerSnapshot snapshot = this.TryGetLocalPlayerSnapshot_NoLock();
            string expected = snapshot == null
                                  ? this.observedLocalIdentity
                                  : FormatAnyIdentity(snapshot.Identity);
            if (string.IsNullOrEmpty(expected))
            {
                expected = this.observedLocalIdentity;
            }

            string actual = FormatAnyIdentity(identity);
            return !string.IsNullOrEmpty(expected)
                   && !string.IsNullOrEmpty(actual)
                   && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLocalIdentityText_NoLock(string identity)
        {
            if (string.IsNullOrEmpty(identity))
            {
                return false;
            }

            MissionPlayerSnapshot snapshot = this.TryGetLocalPlayerSnapshot_NoLock();
            string expected = snapshot == null
                                  ? this.observedLocalIdentity
                                  : FormatAnyIdentity(snapshot.Identity);
            if (string.IsNullOrEmpty(expected))
            {
                expected = this.observedLocalIdentity;
            }

            return !string.IsNullOrEmpty(expected)
                   && string.Equals(expected, identity, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasMissionContext_NoLock()
        {
            return this.HasRecentPendingCreate_NoLock() || this.activeQuestIds.Count > 0;
        }

        private bool HasRecentPendingCreate_NoLock()
        {
            if (string.IsNullOrEmpty(this.pendingCreateQuest)
                || this.pendingCreateQuestUtc == DateTime.MinValue)
            {
                return false;
            }

            if ((DateTime.UtcNow - this.pendingCreateQuestUtc).TotalMinutes <= 5)
            {
                return true;
            }

            this.pendingCreateQuest = string.Empty;
            this.pendingCreateQuestUtc = DateTime.MinValue;
            this.pendingMissionKey = string.Empty;
            return false;
        }

        private string FormatActiveQuests()
        {
            lock (this.syncRoot)
            {
                return this.FormatActiveQuests_NoLock();
            }
        }

        private string FormatActiveQuests_NoLock()
        {
            if (this.activeQuestIds.Count == 0)
            {
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (string questId in this.activeQuestIds)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(questId);
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private string FormatNullableOrdinal_NoLock(long value)
        {
            return value <= 0 ? "unknown" : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatNullableUnsigned(ulong? value)
        {
            return value.HasValue
                       ? value.Value.ToString(CultureInfo.InvariantCulture)
                       : "unknown";
        }

        private static string FormatNullableSigned(long? value)
        {
            return value.HasValue
                       ? value.Value.ToString(CultureInfo.InvariantCulture)
                       : "unknown";
        }

        private static string FormatNullableSigned(int? value)
        {
            return value.HasValue
                       ? value.Value.ToString(CultureInfo.InvariantCulture)
                       : "unknown";
        }

        private static bool IsIdentityType(object identity, string expectedName, int expectedValue)
        {
            if (identity == null)
            {
                return false;
            }

            object type = GetProp(identity, "Type");
            if (type == null)
            {
                return false;
            }

            if (string.Equals(
                    Convert.ToString(type, CultureInfo.InvariantCulture),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int numeric;
            return TryToInt(type, out numeric) && numeric == expectedValue;
        }

        private static string FormatIdentityParts(object type, object instance)
        {
            if (type == null || instance == null)
            {
                return string.Empty;
            }

            string typeText = Convert.ToString(type, CultureInfo.InvariantCulture) ?? string.Empty;
            int numericType;
            if (TryToInt(type, out numericType) && numericType == 0xC350)
            {
                typeText = "SimpleChar";
            }

            int numericInstance;
            string instanceText = TryToInt(instance, out numericInstance)
                                      ? numericInstance.ToString("X", CultureInfo.InvariantCulture)
                                      : Convert.ToString(instance, CultureInfo.InvariantCulture);
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}:{1})",
                typeText,
                instanceText ?? string.Empty);
        }

        private static string FormatRewards(object rewards)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (object reward in Enumerate(rewards))
            {
                if (reward == null)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(';');
                }

                sb.Append(GetProp(reward, "LowId") ?? GetProp(reward, "LowID") ?? "?");
                sb.Append('/');
                sb.Append(GetProp(reward, "HighId") ?? GetProp(reward, "HighID") ?? "?");
                sb.Append('@');
                sb.Append(GetProp(reward, "Ql") ?? GetProp(reward, "Quality") ?? "?");
                sb.Append(':');
                sb.Append(GetProp(reward, "Unk") ?? GetProp(reward, "Unknown") ?? "?");
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string FormatStats(object stats)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (object stat in Enumerate(stats))
            {
                if (stat == null)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(';');
                }

                sb.Append(GetProp(stat, "Value1") ?? GetProp(stat, "Stat") ?? GetProp(stat, "Key") ?? "?");
                sb.Append('=');
                sb.Append(GetProp(stat, "Value2") ?? GetProp(stat, "Value") ?? "?");
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string FormatVector(object vector)
        {
            if (vector == null)
            {
                return "null";
            }

            object x = GetProp(vector, "X");
            object y = GetProp(vector, "Y");
            object z = GetProp(vector, "Z");
            if (x == null && y == null && z == null)
            {
                return Quote(Convert.ToString(vector, CultureInfo.InvariantCulture));
            }

            return "(" + FormatFloatRoundTrip(x)
                   + "," + FormatFloatRoundTrip(y)
                   + "," + FormatFloatRoundTrip(z)
                   + ")";
        }

        private static string FormatFloatRoundTrip(object value)
        {
            if (value == null)
            {
                return "?";
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture)
                    .ToString("R", CultureInfo.InvariantCulture);
            }
            catch
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?";
            }
        }

        private static string HexBytes(object value)
        {
            var bytes = value as byte[];
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\""
                   + value.TrimEnd('\0')
                       .Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                   + "\"";
        }

        private static string DescribeStructured(object value, int depth, int maxLength)
        {
            var sb = new StringBuilder();
            AppendStructured(value, depth, maxLength, sb);
            return sb.ToString();
        }

        private static void AppendStructured(object value, int depth, int maxLength, StringBuilder sb)
        {
            if (sb.Length >= maxLength)
            {
                return;
            }

            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string)
            {
                sb.Append(Quote((string)value));
                return;
            }

            var bytes = value as byte[];
            if (bytes != null)
            {
                sb.Append(HexBytes(bytes));
                return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            object identityType = GetProp(value, "Type");
            object identityInstance = GetProp(value, "Instance");
            if (identityType != null && identityInstance != null)
            {
                sb.Append(FormatAnyIdentity(value));
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                sb.Append('[');
                int index = 0;
                foreach (object item in enumerable)
                {
                    if (index > 0)
                    {
                        sb.Append(',');
                    }

                    if (index >= 128 || sb.Length >= maxLength)
                    {
                        sb.Append("...");
                        break;
                    }

                    AppendStructured(item, depth - 1, maxLength, sb);
                    index++;
                }

                sb.Append(']');
                return;
            }

            if (depth <= 0)
            {
                sb.Append(Quote(Convert.ToString(value, CultureInfo.InvariantCulture)));
                return;
            }

            sb.Append('{');
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            bool first = true;
            for (int i = 0; i < properties.Length && sb.Length < maxLength; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value, null);
                }
                catch
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(property.Name).Append('=');
                AppendStructured(propertyValue, depth - 1, maxLength, sb);
                first = false;
            }

            if (sb.Length >= maxLength)
            {
                sb.Append("...");
            }

            sb.Append('}');
        }

        private void WriteLine(string category, string message)
        {
            lock (this.syncRoot)
            {
                this.WriteLine_NoLock(category, message);
            }
        }

        private void WriteLine_NoLock(string category, string message)
        {
            if (this.log == null)
            {
                return;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-ddTHH:mm:ss.fffffffZ} [{1}] {2}",
                DateTime.UtcNow,
                category,
                message ?? string.Empty);
            this.log.WriteLine(line);
            if (this.logEvent != null
                && (category.IndexOf("TELEPORT", StringComparison.OrdinalIgnoreCase) >= 0
                    || category.IndexOf("PLAYFIELD-INIT", StringComparison.OrdinalIgnoreCase) >= 0
                    || category.IndexOf("CREATE-QUEST", StringComparison.OrdinalIgnoreCase) >= 0
                    || category.IndexOf("MISSION-KEY", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                this.logEvent("MISSION-FLOW", category + " " + message);
            }
        }

        private void CloseLog_NoLock()
        {
            if (this.log != null)
            {
                try
                {
                    this.log.Flush();
                    this.log.Dispose();
                }
                catch
                {
                }

                this.log = null;
            }
        }

        private static object GetProp(object target, string name)
        {
            if (target == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            PropertyInfo prop = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop == null)
            {
                return null;
            }

            try
            {
                return prop.GetValue(target, null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetNested(object target, string outer, string inner)
        {
            return GetProp(GetProp(target, outer), inner);
        }

        private static string FormatAnyIdentity(object identity)
        {
            if (identity == null)
            {
                return null;
            }

            object type = GetProp(identity, "Type");
            object instance = GetProp(identity, "Instance");
            if (type == null && instance == null)
            {
                return Convert.ToString(identity);
            }

            int inst;
            string instText = TryToInt(instance, out inst)
                                  ? inst.ToString("X", CultureInfo.InvariantCulture)
                                  : Convert.ToString(instance);
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}:{1})",
                type ?? "?",
                instText ?? "?");
        }

        private static int ExtractInstance(object identityOrInt)
        {
            if (identityOrInt == null)
            {
                return 0;
            }

            int value;
            if (TryToInt(identityOrInt, out value))
            {
                return value;
            }

            object instance = GetProp(identityOrInt, "Instance");
            return TryToInt(instance, out value) ? value : 0;
        }

        private static bool TryToInt(object value, out int result)
        {
            result = 0;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryToUInt64(object value, out ulong result)
        {
            result = 0;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatFloat(object value)
        {
            if (value == null)
            {
                return "?";
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture);
            }
            catch
            {
                return Convert.ToString(value) ?? "?";
            }
        }

        private static int CountEnumerable(object value)
        {
            int count = 0;
            foreach (object unused in Enumerate(value))
            {
                count++;
            }

            return count;
        }

        private static object FirstOf(object value)
        {
            foreach (object item in Enumerate(value))
            {
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static IEnumerable Enumerate(object value)
        {
            if (value == null)
            {
                yield break;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                yield break;
            }

            foreach (object item in enumerable)
            {
                yield return item;
            }
        }

        private static string DescribeObject(object value, int max)
        {
            if (value == null)
            {
                return string.Empty;
            }

            try
            {
                return TrimTo(value.ToString(), max);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TrimTo(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.TrimEnd('\0').Trim();
            if (trimmed.Length <= max)
            {
                return trimmed;
            }

            return trimmed.Substring(0, max);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        }
    }

    internal sealed class MissionPlayerSnapshot
    {
        public object Identity { get; set; }

        public int? Level { get; set; }

        public long? Cash { get; set; }

        public long? Xp { get; set; }
    }
}
