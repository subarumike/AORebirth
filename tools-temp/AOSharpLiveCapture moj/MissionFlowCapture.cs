namespace AOSharpLiveCapture
{
    using System;
    using System.Collections;
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

        // Mission instance playfields observed in live AO are in a high id band (e.g. 1419307).
        private const int MissionInstancePlayfieldMin = 1_000_000;

        private readonly object syncRoot = new object();

        private readonly Action<string, string> logEvent;

        private StreamWriter log;

        private string logPath = string.Empty;

        private string lastPlayfieldId = string.Empty;

        private string pendingCreateQuest = string.Empty;

        private DateTime pendingCreateQuestUtc = DateTime.MinValue;

        public MissionFlowCapture(Action<string, string> logEvent)
        {
            this.logEvent = logEvent;
        }

        public void BindSession(string sessionDirectory)
        {
            lock (this.syncRoot)
            {
                this.CloseLog_NoLock();
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
            this.HandleMessage("IN", message);
        }

        public void OnN3MessageSent(N3Message message)
        {
            this.HandleMessage("OUT", message);
        }

        public void OnPlayfieldInit(uint playfieldId)
        {
            this.lastPlayfieldId = playfieldId.ToString(CultureInfo.InvariantCulture);
            bool likelyMissionInstance = playfieldId >= MissionInstancePlayfieldMin;
            this.WriteLine(
                "PLAYFIELD-INIT",
                "pf=" + playfieldId.ToString(CultureInfo.InvariantCulture)
                + " hex=0x" + playfieldId.ToString("X", CultureInfo.InvariantCulture)
                + " likelyMissionInstance=" + likelyMissionInstance
                + " pendingCreateQuest=" + (this.pendingCreateQuest ?? string.Empty));
        }

        public void OnTeleportStarted()
        {
            this.WriteLine(
                "TELEPORT",
                "started lastPf=" + this.lastPlayfieldId
                + " pendingCreateQuest=" + (this.pendingCreateQuest ?? string.Empty));
        }

        public void OnTeleportEnded()
        {
            this.WriteLine(
                "TELEPORT",
                "ended lastPf=" + this.lastPlayfieldId
                + " pendingCreateQuest=" + (this.pendingCreateQuest ?? string.Empty));
        }

        private void HandleMessage(string direction, N3Message message)
        {
            if (message == null)
            {
                return;
            }

            try
            {
                string typeName = message.N3MessageType.ToString();
                if (string.Equals(typeName, "CreateQuest", StringComparison.OrdinalIgnoreCase))
                {
                    this.pendingCreateQuest = FormatAnyIdentity(GetProp(message, "QuestIdentity"))
                                              ?? FormatAnyIdentity(GetProp(message, "Identity"))
                                              ?? "unknown";
                    this.pendingCreateQuestUtc = DateTime.UtcNow;
                    this.WriteLine(
                        direction + "-CREATE-QUEST",
                        "quest=" + this.pendingCreateQuest
                        + " identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                        + " detail=" + TrimTo(DescribeObject(message, 600), 600));
                    return;
                }

                if (string.Equals(typeName, "QuestAlternative", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogQuestAlternative(direction, message);
                    return;
                }

                if (string.Equals(typeName, "QuestFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogQuestFullUpdate(direction, message);
                    return;
                }

                if (string.Equals(typeName, "Quest", StringComparison.OrdinalIgnoreCase))
                {
                    this.WriteLine(
                        direction + "-QUEST",
                        "action=" + (GetProp(message, "Action") ?? "?")
                        + " mission=" + (FormatAnyIdentity(GetProp(message, "Mission")) ?? string.Empty)
                        + " identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                        + " detail=" + TrimTo(DescribeObject(message, 400), 400));
                    return;
                }

                if (string.Equals(typeName, "SimpleItemFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogMissionKeyItem(direction, message);
                    return;
                }

                if (string.Equals(typeName, "N3Teleport", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "Teleport", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogTeleport(direction, message);
                }
            }
            catch (Exception ex)
            {
                this.WriteLine("MISSION-FLOW-ERROR", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void LogQuestAlternative(string direction, N3Message message)
        {
            object offers = GetProp(message, "MissionDetails") ?? GetProp(message, "QuestInfos");
            int offerCount = CountEnumerable(offers);
            var sb = new StringBuilder();
            sb.Append("terminal=").Append(
                FormatAnyIdentity(GetProp(message, "Terminal") ?? GetProp(message, "MissionTerminalIdentity"))
                ?? string.Empty);
            sb.Append(" scope=").Append(GetProp(message, "Scope") ?? GetProp(message, "Unknown5") ?? string.Empty);
            object sliders = GetProp(message, "MissionSliders");
            if (sliders != null)
            {
                sb.Append(" sliders=[lvl=").Append(GetProp(sliders, "Level") ?? GetProp(sliders, "LevelSlider") ?? "?");
                sb.Append(" gb=").Append(GetProp(sliders, "GoodBad") ?? GetProp(sliders, "GoodBadSlider") ?? "?");
                sb.Append(" oc=").Append(GetProp(sliders, "OrderChaos") ?? GetProp(sliders, "OrderChaosSlider") ?? "?");
                sb.Append(" oh=").Append(GetProp(sliders, "OpenHidden") ?? GetProp(sliders, "OpenHiddenSlider") ?? "?");
                sb.Append(" pm=").Append(GetProp(sliders, "PhysicalMystical") ?? GetProp(sliders, "PhysicalMysticalSlider") ?? "?");
                sb.Append(" hs=").Append(GetProp(sliders, "HeadOnStealth") ?? GetProp(sliders, "HeadOnStealthSlider") ?? "?");
                sb.Append(" me=").Append(GetProp(sliders, "MoneyExperience") ?? GetProp(sliders, "MoneyExperienceSlider") ?? "?");
                sb.Append(']');
            }
            else
            {
                sb.Append(" sliders=[lvl=").Append(GetProp(message, "LevelSlider") ?? "?");
                sb.Append(" gb=").Append(GetProp(message, "GoodBadSlider") ?? "?");
                sb.Append(" oc=").Append(GetProp(message, "OrderChaosSlider") ?? "?");
                sb.Append(" oh=").Append(GetProp(message, "OpenHiddenSlider") ?? "?");
                sb.Append(" pm=").Append(GetProp(message, "PhysicalMysticalSlider") ?? "?");
                sb.Append(" hs=").Append(GetProp(message, "HeadOnStealthSlider") ?? "?");
                sb.Append(" me=").Append(GetProp(message, "MoneyExperienceSlider") ?? "?");
                sb.Append(']');
            }

            sb.Append(" offers=").Append(offerCount);

            int index = 0;
            foreach (object info in Enumerate(offers))
            {
                if (info == null)
                {
                    continue;
                }

                sb.Append(" |#").Append(index++);
                sb.Append(" quest=").Append(
                    FormatAnyIdentity(GetProp(info, "QuestIdentity") ?? GetProp(info, "Identity"))
                    ?? string.Empty);
                sb.Append(" icon=").Append(GetProp(info, "MissionIconId") ?? GetProp(info, "Icon") ?? "?");
                sb.Append(" objectiveType=").Append(
                    ClassifyObjectiveType(
                        ToIntSafe(GetProp(info, "MissionIconId") ?? GetProp(info, "Icon")),
                        Convert.ToString(GetProp(info, "ShortInfo") ?? string.Empty)));
                sb.Append(" ql=").Append(GetProp(info, "Quality") ?? "?");
                sb.Append(" short=").Append(Sanitize(TrimTo(Convert.ToString(GetProp(info, "ShortInfo") ?? string.Empty), 40)));

                object actions = GetProp(info, "QuestActions") ?? GetProp(info, "Actions") ?? GetProp(info, "MissionActions");
                object firstAction = FirstOf(actions);
                if (firstAction != null)
                {
                    object pf = GetProp(firstAction, "Playfield")
                                ?? GetProp(firstAction, "PlayfieldId")
                                ?? GetProp(firstAction, "PlayfieldIdentity");
                    sb.Append(" pf=").Append(FormatAnyIdentity(pf) ?? Convert.ToString(GetProp(firstAction, "PlayfieldId") ?? "?"));
                    object x = GetProp(firstAction, "X") ?? GetNested(firstAction, "Position", "X");
                    object y = GetProp(firstAction, "Y") ?? GetNested(firstAction, "Position", "Y");
                    object z = GetProp(firstAction, "Z") ?? GetNested(firstAction, "Position", "Z");
                    if (x != null && y != null && z != null)
                    {
                        sb.Append(" xyz=(")
                            .Append(FormatFloat(x)).Append(',')
                            .Append(FormatFloat(y)).Append(',')
                            .Append(FormatFloat(z)).Append(')');
                    }

                    object entranceA = GetProp(firstAction, "Unknown18") ?? GetProp(firstAction, "EntranceType");
                    object entranceB = GetProp(firstAction, "Unknown19") ?? GetProp(firstAction, "EntranceInstance");
                    if (entranceA != null || entranceB != null)
                    {
                        sb.Append(" entrance=").Append(entranceA ?? "?").Append('/').Append(entranceB ?? "?");
                    }
                }

                object rewards = GetProp(info, "ItemRewards") ?? GetProp(info, "Rewards");
                object firstReward = FirstOf(rewards);
                if (firstReward != null)
                {
                    sb.Append(" reward=")
                        .Append(GetProp(firstReward, "LowId") ?? GetProp(firstReward, "LowID") ?? "?")
                        .Append('/')
                        .Append(GetProp(firstReward, "HighId") ?? GetProp(firstReward, "HighID") ?? "?")
                        .Append('@')
                        .Append(GetProp(firstReward, "Quality") ?? "?");
                }
            }

            this.WriteLine(direction + "-QUEST-ALT", sb.ToString());
        }

        private void LogQuestFullUpdate(string direction, N3Message message)
        {
            object quests = GetProp(message, "Quests") ?? GetProp(message, "MissionDetails") ?? GetProp(message, "QuestInfos");
            int count = CountEnumerable(quests);
            var sb = new StringBuilder();
            sb.Append("identity=").Append(FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty);
            sb.Append(" unknown=").Append(GetProp(message, "Unknown") ?? string.Empty);
            sb.Append(" quests=").Append(count);
            sb.Append(" pendingCreateQuest=").Append(this.pendingCreateQuest ?? string.Empty);

            int index = 0;
            foreach (object quest in Enumerate(quests))
            {
                if (quest == null)
                {
                    continue;
                }

                sb.Append(" |#").Append(index++);
                sb.Append(" quest=").Append(
                    FormatAnyIdentity(GetProp(quest, "QuestId") ?? GetProp(quest, "QuestIdentity") ?? GetProp(quest, "Identity"))
                    ?? string.Empty);
                sb.Append(" icon=").Append(GetProp(quest, "MissionIconId") ?? "?");
                sb.Append(" objectiveType=").Append(
                    ClassifyObjectiveType(
                        ToIntSafe(GetProp(quest, "MissionIconId")),
                        Convert.ToString(GetProp(quest, "ShortInfo") ?? string.Empty)));
                sb.Append(" short=").Append(Sanitize(TrimTo(Convert.ToString(GetProp(quest, "ShortInfo") ?? string.Empty), 40)));
                object actions = GetProp(quest, "QuestActions") ?? GetProp(quest, "Actions");
                object firstAction = FirstOf(actions);
                if (firstAction != null)
                {
                    object pf = GetProp(firstAction, "PlayfieldId") ?? GetProp(firstAction, "Playfield");
                    sb.Append(" pf=").Append(FormatAnyIdentity(pf) ?? Convert.ToString(pf ?? "?"));
                }
            }

            this.WriteLine(direction + "-QUEST-FULL", sb.ToString());
        }

        private void LogMissionKeyItem(string direction, N3Message message)
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
                else if (statName.IndexOf("ACGItemLevel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ql = numeric;
                }
            }

            object identity = GetProp(message, "Identity");
            string identityType = Convert.ToString(GetProp(identity, "Type") ?? string.Empty) ?? string.Empty;
            bool isMissionKey = lowId == MissionKeyTemplateId || highId == MissionKeyTemplateId
                                || string.Equals(identityType, "MissionKey", StringComparison.OrdinalIgnoreCase)
                                || identityType.IndexOf("MissionKey", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isMissionKey)
            {
                return;
            }

            this.WriteLine(
                direction + "-MISSION-KEY",
                "item=" + (FormatAnyIdentity(identity) ?? string.Empty)
                + " ownerType=" + (GetProp(message, "Identitytype") ?? GetProp(message, "OwnerType") ?? string.Empty)
                + " ownerInstance=" + (GetProp(message, "Instance") ?? GetProp(message, "OwnerInstance") ?? string.Empty)
                + " playfield=" + (GetProp(message, "Playfield") ?? string.Empty)
                + " low=" + lowId
                + " high=" + highId
                + " ql=" + ql
                + " name=" + Sanitize(TrimTo(Convert.ToString(GetProp(message, "Name") ?? string.Empty), 48))
                + " pendingCreateQuest=" + (this.pendingCreateQuest ?? string.Empty));
        }

        private void LogTeleport(string direction, N3Message message)
        {
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
            sb.Append("identity=").Append(FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty);
            sb.Append(" destPf=").Append(FormatAnyIdentity(destPf) ?? Convert.ToString(destPf ?? string.Empty));
            sb.Append(" changePf=").Append(FormatAnyIdentity(changePf) ?? Convert.ToString(changePf ?? string.Empty));
            sb.Append(" playfield2=").Append(FormatAnyIdentity(playfield2) ?? Convert.ToString(playfield2 ?? string.Empty));
            sb.Append(" gameServerId=").Append(GetProp(message, "GameServerId") ?? string.Empty);
            sb.Append(" sgId=").Append(GetProp(message, "SgId") ?? string.Empty);
            object dest = GetProp(message, "Destination") ?? GetProp(message, "Position");
            if (dest != null)
            {
                sb.Append(" dest=(")
                    .Append(FormatFloat(GetProp(dest, "X"))).Append(',')
                    .Append(FormatFloat(GetProp(dest, "Y"))).Append(',')
                    .Append(FormatFloat(GetProp(dest, "Z"))).Append(')');
            }

            sb.Append(" likelyMissionInstance=").Append(likelyMissionInstance);
            sb.Append(" pendingCreateQuest=").Append(this.pendingCreateQuest ?? string.Empty);
            if (this.pendingCreateQuestUtc != DateTime.MinValue)
            {
                sb.Append(" msSinceCreateQuest=")
                    .Append(
                        ((long)(DateTime.UtcNow - this.pendingCreateQuestUtc).TotalMilliseconds)
                            .ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(" detail=").Append(TrimTo(DescribeObject(message, 500), 500));
            this.WriteLine(direction + "-N3-TELEPORT", sb.ToString());
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

        private static int ToIntSafe(object value)
        {
            int result;
            return TryToInt(value, out result) ? result : 0;
        }

        // Icons match ZoneEngine MissionTypeCatalog (capture-backed).
        private static string ClassifyObjectiveType(int icon, string shortInfo)
        {
            if (icon == 11330)
            {
                return "KillPerson";
            }

            if (icon == 11335)
            {
                return "FindPerson";
            }

            if (icon == 11329)
            {
                return "FindItem";
            }

            if (icon == 11337)
            {
                return "FindItemReturn";
            }

            if (icon == 11342)
            {
                return "RepairMachine";
            }

            string text = shortInfo ?? string.Empty;
            if (text.IndexOf("Repair", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "RepairMachine";
            }

            if (text.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("Find", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "FindItemReturn";
            }

            if (text.IndexOf("Find", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "FindItem";
            }

            if (text.IndexOf("Kill", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "KillPerson";
            }

            if (text.IndexOf("Find", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "FindPerson";
            }

            return string.Empty;
        }
    }
}
