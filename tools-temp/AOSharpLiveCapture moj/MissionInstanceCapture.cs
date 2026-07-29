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
    /// Capture projections for RK rolling mission instances (PF id >= 1_000_000).
    /// Additive to existing combat/loot/SCFU pipelines and <see cref="MissionFlowCapture"/>.
    /// Writes mission-instance.log plus CSV tables for map fog (PlayfieldAnarchyF), doors,
    /// world containers/chests, and NPC spawn summaries so shapes can be rebuilt without
    /// re-scraping packets.hex.log. Objective type is classified from mission icon ids.
    /// </summary>
    internal sealed class MissionInstanceCapture
    {
        private const int MissionInstancePlayfieldMin = 1_000_000;

        // Match ZoneEngine MissionTypeCatalog icons (capture-backed).
        private const int KillPersonIcon = 11330;

        private const int FindPersonIcon = 11335;

        private const int FindItemIcon = 11329;

        private const int FindItemReturnIcon = 11337;

        private const int RepairMachineIcon = 11342;

        private readonly object syncRoot = new object();

        private readonly Action<string, string> logEvent;

        private StreamWriter log;

        private StreamWriter mapCsv;

        private StreamWriter doorsCsv;

        private StreamWriter containersCsv;

        private StreamWriter spawnsCsv;

        private string sessionDirectory = string.Empty;

        private uint currentPlayfieldId;

        private bool inMissionInstance;

        private string lastObjectiveType = string.Empty;

        private int lastMissionIconId;

        private string lastMissionShort = string.Empty;

        private string pendingCreateQuest = string.Empty;

        public MissionInstanceCapture(Action<string, string> logEvent)
        {
            this.logEvent = logEvent;
        }

        public void BindSession(string sessionDirectory)
        {
            lock (this.syncRoot)
            {
                this.CloseLogs_NoLock();
                if (string.IsNullOrEmpty(sessionDirectory))
                {
                    return;
                }

                this.sessionDirectory = sessionDirectory;
                this.log = new StreamWriter(Path.Combine(sessionDirectory, "mission-instance.log"), false, Encoding.UTF8)
                           {
                               AutoFlush = true
                           };
                this.mapCsv = OpenCsv(
                    Path.Combine(sessionDirectory, "mission-instance-map.csv"),
                    "CapturedUtc,Direction,PlayfieldId,MessageType,Identity,RawDetail");
                this.doorsCsv = OpenCsv(
                    Path.Combine(sessionDirectory, "mission-instance-doors.csv"),
                    "CapturedUtc,Direction,PlayfieldId,MessageType,Identity,PositionX,PositionY,PositionZ,RawDetail");
                this.containersCsv = OpenCsv(
                    Path.Combine(sessionDirectory, "mission-instance-containers.csv"),
                    "CapturedUtc,Direction,PlayfieldId,MessageType,Identity,Name,TemplateLow,TemplateHigh,Quality,PositionX,PositionY,PositionZ,KindHint,RawDetail");
                this.spawnsCsv = OpenCsv(
                    Path.Combine(sessionDirectory, "mission-instance-spawns.csv"),
                    "CapturedUtc,Direction,PlayfieldId,MessageType,Identity,Name,MonsterData,Level,Health,Side,NpcFamily,PositionX,PositionY,PositionZ,IsPet,RawDetail");
                this.WriteLine_NoLock(
                    "MISSION-INSTANCE",
                    "session bound path=" + sessionDirectory
                    + " (map fog/doors/containers/spawns/objective while PF>=1000000;"
                    + " combat+loot remain in enemy-*/corpse-* CSVs)");
                if (this.logEvent != null)
                {
                    this.logEvent("PLUGIN", "Mission instance log: " + Path.Combine(sessionDirectory, "mission-instance.log"));
                }
            }
        }

        public void Teardown()
        {
            lock (this.syncRoot)
            {
                this.CloseLogs_NoLock();
            }
        }

        public void OnPlayfieldInit(uint playfieldId)
        {
            lock (this.syncRoot)
            {
                this.currentPlayfieldId = playfieldId;
                bool wasIn = this.inMissionInstance;
                this.inMissionInstance = playfieldId >= MissionInstancePlayfieldMin;
                this.WriteLine_NoLock(
                    "PLAYFIELD-INIT",
                    "pf=" + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + " hex=0x" + playfieldId.ToString("X", CultureInfo.InvariantCulture)
                    + " inMissionInstance=" + this.inMissionInstance
                    + " objectiveType=" + (this.lastObjectiveType ?? string.Empty)
                    + " missionIcon=" + this.lastMissionIconId.ToString(CultureInfo.InvariantCulture)
                    + " short=" + Sanitize(this.lastMissionShort)
                    + " pendingCreateQuest=" + (this.pendingCreateQuest ?? string.Empty));
                if (this.inMissionInstance && !wasIn && this.logEvent != null)
                {
                    this.logEvent(
                        "MISSION-INSTANCE",
                        "entered pf=" + playfieldId.ToString(CultureInfo.InvariantCulture)
                        + " objective=" + (this.lastObjectiveType ?? "?"));
                }
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
                    this.WriteLine(
                        direction + "-CREATE-QUEST",
                        "quest=" + this.pendingCreateQuest);
                    return;
                }

                if (string.Equals(typeName, "QuestAlternative", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "QuestFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogQuestObjective(direction, typeName, message);
                    return;
                }

                if (string.Equals(typeName, "Quest", StringComparison.OrdinalIgnoreCase))
                {
                    this.WriteLine(
                        direction + "-QUEST",
                        "action=" + (GetProp(message, "Action") ?? "?")
                        + " mission=" + (FormatAnyIdentity(GetProp(message, "Mission")) ?? string.Empty)
                        + " inMission=" + this.inMissionInstance);
                    return;
                }

                if (!this.inMissionInstance)
                {
                    return;
                }

                if (string.Equals(typeName, "PlayfieldAnarchyF", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "N3PlayfieldFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogMap(direction, typeName, message);
                    return;
                }

                if (string.Equals(typeName, "DoorFullUpdate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "DoorStatusUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogDoor(direction, typeName, message);
                    return;
                }

                if (string.Equals(typeName, "ChestItemFullUpdate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "SimpleItemFullUpdate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "VendingMachineFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogContainer(direction, typeName, message);
                    return;
                }

                if (string.Equals(typeName, "SimpleCharFullUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogSpawn(direction, typeName, message);
                    return;
                }

                if (string.Equals(typeName, "GenericCmd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "CharacterAction", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "TemplateAction", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeName, "FormatFeedback", StringComparison.OrdinalIgnoreCase))
                {
                    this.WriteLine(
                        direction + "-" + typeName.ToUpperInvariant(),
                        "identity=" + (FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty)
                        + " target=" + (FormatAnyIdentity(GetProp(message, "Target")) ?? string.Empty)
                        + " action=" + (GetProp(message, "Action") ?? GetProp(message, "Command") ?? string.Empty)
                        + " detail=" + TrimTo(DescribeObject(message, 350), 350));
                }
            }
            catch (Exception ex)
            {
                this.WriteLine("MISSION-INSTANCE-ERROR", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void LogQuestObjective(string direction, string typeName, N3Message message)
        {
            object quests = GetProp(message, "Quests")
                            ?? GetProp(message, "MissionDetails")
                            ?? GetProp(message, "QuestInfos");
            int index = 0;
            foreach (object quest in Enumerate(quests))
            {
                if (quest == null)
                {
                    continue;
                }

                int icon = ToInt(GetProp(quest, "MissionIconId") ?? GetProp(quest, "Icon"));
                string shortInfo = Convert.ToString(GetProp(quest, "ShortInfo") ?? string.Empty) ?? string.Empty;
                string objective = ClassifyObjective(icon, shortInfo);
                if (!string.IsNullOrEmpty(objective))
                {
                    this.lastObjectiveType = objective;
                    this.lastMissionIconId = icon;
                    this.lastMissionShort = shortInfo;
                }

                var sb = new StringBuilder();
                sb.Append("source=").Append(typeName);
                sb.Append(" |#").Append(index++);
                sb.Append(" quest=").Append(
                    FormatAnyIdentity(
                        GetProp(quest, "QuestId")
                        ?? GetProp(quest, "QuestIdentity")
                        ?? GetProp(quest, "Identity"))
                    ?? string.Empty);
                sb.Append(" icon=").Append(icon.ToString(CultureInfo.InvariantCulture));
                sb.Append(" objectiveType=").Append(objective);
                sb.Append(" short=").Append(Sanitize(TrimTo(shortInfo, 80)));
                object actions = GetProp(quest, "QuestActions") ?? GetProp(quest, "Actions") ?? GetProp(quest, "MissionActions");
                object firstAction = FirstOf(actions);
                if (firstAction != null)
                {
                    object pf = GetProp(firstAction, "Playfield")
                                ?? GetProp(firstAction, "PlayfieldId")
                                ?? GetProp(firstAction, "PlayfieldIdentity");
                    sb.Append(" pf=").Append(FormatAnyIdentity(pf) ?? Convert.ToString(pf ?? "?"));
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
                }

                this.WriteLine(direction + "-OBJECTIVE", sb.ToString());
            }
        }

        private void LogMap(string direction, string typeName, N3Message message)
        {
            string identity = FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty;
            string detail = TrimTo(DescribeObject(message, 500), 500);
            this.WriteCsvLine(
                this.mapCsv,
                direction,
                typeName,
                identity,
                detail);
            this.WriteLine(
                direction + "-MAP",
                "type=" + typeName
                + " identity=" + identity
                + " pf=" + this.currentPlayfieldId.ToString(CultureInfo.InvariantCulture)
                + " note=fog/undetected-map-wire"
                + " detail=" + detail);
        }

        private void LogDoor(string direction, string typeName, N3Message message)
        {
            string identity = FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty;
            float x, y, z;
            TryReadPosition(message, out x, out y, out z);
            string detail = TrimTo(DescribeObject(message, 400), 400);
            this.WriteCsvLine(
                this.doorsCsv,
                direction,
                typeName,
                identity,
                FormatFloat(x),
                FormatFloat(y),
                FormatFloat(z),
                detail);
            this.WriteLine(
                direction + "-DOOR",
                "type=" + typeName
                + " identity=" + identity
                + " xyz=(" + FormatFloat(x) + "," + FormatFloat(y) + "," + FormatFloat(z) + ")"
                + " detail=" + detail);
        }

        private void LogContainer(string direction, string typeName, N3Message message)
        {
            string identity = FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty;
            string name = Convert.ToString(GetProp(message, "Name") ?? string.Empty) ?? string.Empty;
            int lowId;
            int highId;
            int ql;
            ReadItemIds(message, out lowId, out highId, out ql);
            float x, y, z;
            TryReadPosition(message, out x, out y, out z);
            string kind = ClassifyContainerKind(name, typeName, lowId, highId);
            if (string.Equals(typeName, "SimpleItemFullUpdate", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(kind))
            {
                return;
            }

            string detail = TrimTo(DescribeObject(message, 350), 350);
            this.WriteCsvLine(
                this.containersCsv,
                direction,
                typeName,
                identity,
                Sanitize(name),
                lowId.ToString(CultureInfo.InvariantCulture),
                highId.ToString(CultureInfo.InvariantCulture),
                ql.ToString(CultureInfo.InvariantCulture),
                FormatFloat(x),
                FormatFloat(y),
                FormatFloat(z),
                kind,
                detail);
            this.WriteLine(
                direction + "-CONTAINER",
                "type=" + typeName
                + " kind=" + kind
                + " identity=" + identity
                + " name=" + Sanitize(TrimTo(name, 48))
                + " template=" + lowId + "/" + highId + "@" + ql
                + " xyz=(" + FormatFloat(x) + "," + FormatFloat(y) + "," + FormatFloat(z) + ")");
        }

        private void LogSpawn(string direction, string typeName, N3Message message)
        {
            string identity = FormatAnyIdentity(GetProp(message, "Identity")) ?? string.Empty;
            string name = Convert.ToString(GetProp(message, "Name") ?? string.Empty) ?? string.Empty;
            bool isPet = false;
            object flags = GetProp(message, "Flags");
            if (flags != null)
            {
                string flagsText = Convert.ToString(flags) ?? string.Empty;
                isPet = flagsText.IndexOf("IsPet", StringComparison.OrdinalIgnoreCase) >= 0
                        || flagsText.IndexOf("Pet", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            object owner = GetProp(message, "Owner") ?? GetProp(message, "OwnerIdentity");
            if (owner != null && !string.IsNullOrEmpty(Convert.ToString(owner)))
            {
                isPet = true;
            }

            int monsterData = 0;
            int level = 0;
            int health = 0;
            int side = 0;
            int npcFamily = 0;
            object stats = GetProp(message, "Stats");
            foreach (object stat in Enumerate(stats))
            {
                if (stat == null)
                {
                    continue;
                }

                string key = Convert.ToString(GetProp(stat, "Value1") ?? GetProp(stat, "Stat") ?? GetProp(stat, "Key"))
                             ?? string.Empty;
                int value = ToInt(GetProp(stat, "Value2") ?? GetProp(stat, "Value"));
                if (key.IndexOf("MonsterData", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(key, "monsterdata", StringComparison.OrdinalIgnoreCase))
                {
                    monsterData = value;
                }
                else if (string.Equals(key, "Level", StringComparison.OrdinalIgnoreCase)
                         || key.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    level = value;
                }
                else if (string.Equals(key, "Life", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(key, "Health", StringComparison.OrdinalIgnoreCase))
                {
                    health = value;
                }
                else if (string.Equals(key, "Side", StringComparison.OrdinalIgnoreCase))
                {
                    side = value;
                }
                else if (key.IndexOf("NpcFamily", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    npcFamily = value;
                }
            }

            monsterData = PreferPositive(ToInt(GetProp(message, "MonsterData")), monsterData);
            level = PreferPositive(ToInt(GetProp(message, "Level")), level);
            health = PreferPositive(ToInt(GetProp(message, "Health")), health);
            float x, y, z;
            TryReadPosition(message, out x, out y, out z);
            string detail = TrimTo(DescribeObject(message, 250), 250);
            this.WriteCsvLine(
                this.spawnsCsv,
                direction,
                typeName,
                identity,
                Sanitize(name),
                monsterData.ToString(CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture),
                health.ToString(CultureInfo.InvariantCulture),
                side.ToString(CultureInfo.InvariantCulture),
                npcFamily.ToString(CultureInfo.InvariantCulture),
                FormatFloat(x),
                FormatFloat(y),
                FormatFloat(z),
                isPet ? "1" : "0",
                detail);
            this.WriteLine(
                direction + "-SPAWN",
                "identity=" + identity
                + " name=" + Sanitize(TrimTo(name, 48))
                + " md=" + monsterData
                + " lvl=" + level
                + " hp=" + health
                + " pet=" + isPet
                + " xyz=(" + FormatFloat(x) + "," + FormatFloat(y) + "," + FormatFloat(z) + ")");
        }

        private static string ClassifyObjective(int icon, string shortInfo)
        {
            if (icon == KillPersonIcon)
            {
                return "KillPerson";
            }

            if (icon == FindPersonIcon)
            {
                return "FindPerson";
            }

            if (icon == FindItemIcon)
            {
                return "FindItem";
            }

            if (icon == FindItemReturnIcon)
            {
                return "FindItemReturn";
            }

            if (icon == RepairMachineIcon)
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

        private static string ClassifyContainerKind(string name, string typeName, int lowId, int highId)
        {
            if (string.Equals(typeName, "ChestItemFullUpdate", StringComparison.OrdinalIgnoreCase))
            {
                return "Chest";
            }

            string text = name ?? string.Empty;
            if (text.IndexOf("Barrel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Barrel";
            }

            if (text.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Skeleton";
            }

            if (text.IndexOf("Treasure", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Treasure";
            }

            if (text.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Crate", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Container", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Container";
            }

            if (text.IndexOf("Machine", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Broken", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Machine";
            }

            if (lowId == 0 && highId == 0)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private void WriteCsvLine(StreamWriter writer, params string[] fields)
        {
            lock (this.syncRoot)
            {
                if (writer == null || fields == null)
                {
                    return;
                }

                var sb = new StringBuilder();
                sb.Append(Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                for (int i = 0; i < fields.Length; i++)
                {
                    // Insert playfield after Direction (fields[0]).
                    if (i == 1)
                    {
                        sb.Append(',').Append(
                            Csv(this.currentPlayfieldId.ToString(CultureInfo.InvariantCulture)));
                    }

                    sb.Append(',').Append(Csv(fields[i] ?? string.Empty));
                }

                // If only direction+type+identity+... and Direction is fields[0],
                // playfield must sit between direction and messageType.
                writer.WriteLine(sb.ToString());
                writer.Flush();
            }
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

            this.log.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-ddTHH:mm:ss.fffffffZ} [{1}] {2}",
                    DateTime.UtcNow,
                    category,
                    message ?? string.Empty));
        }

        private void CloseLogs_NoLock()
        {
            CloseWriter(ref this.log);
            CloseWriter(ref this.mapCsv);
            CloseWriter(ref this.doorsCsv);
            CloseWriter(ref this.containersCsv);
            CloseWriter(ref this.spawnsCsv);
            this.sessionDirectory = string.Empty;
            this.inMissionInstance = false;
            this.currentPlayfieldId = 0;
        }

        private static StreamWriter OpenCsv(string path, string header)
        {
            var writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(header);
            return writer;
        }

        private static void CloseWriter(ref StreamWriter writer)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch
            {
            }

            writer = null;
        }

        private static void ReadItemIds(N3Message message, out int lowId, out int highId, out int ql)
        {
            lowId = 0;
            highId = 0;
            ql = 0;
            object stats = GetProp(message, "Stats");
            foreach (object stat in Enumerate(stats))
            {
                if (stat == null)
                {
                    continue;
                }

                string key = Convert.ToString(GetProp(stat, "Value1") ?? GetProp(stat, "Stat") ?? GetProp(stat, "Key"))
                             ?? string.Empty;
                int value = ToInt(GetProp(stat, "Value2") ?? GetProp(stat, "Value"));
                if (key.IndexOf("ACGItemTemplateID2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    highId = value;
                }
                else if (key.IndexOf("ACGItemTemplateID", StringComparison.OrdinalIgnoreCase) >= 0
                         || string.Equals(key, "StaticInstance", StringComparison.OrdinalIgnoreCase))
                {
                    lowId = value;
                }
                else if (key.IndexOf("ACGItemLevel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ql = value;
                }
            }

            if (lowId == 0)
            {
                lowId = ToInt(GetProp(message, "Template") ?? GetProp(message, "LowId"));
            }

            if (highId == 0)
            {
                highId = ToInt(GetProp(message, "HighId"));
            }
        }

        private static void TryReadPosition(object message, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            object pos = GetProp(message, "Position") ?? GetProp(message, "Coordinates");
            if (pos == null)
            {
                object px = GetProp(message, "X") ?? GetProp(message, "PositionX");
                object py = GetProp(message, "Y") ?? GetProp(message, "PositionY");
                object pz = GetProp(message, "Z") ?? GetProp(message, "PositionZ");
                if (px != null)
                {
                    x = ToFloat(px);
                    y = ToFloat(py);
                    z = ToFloat(pz);
                }

                return;
            }

            x = ToFloat(GetProp(pos, "X"));
            y = ToFloat(GetProp(pos, "Y"));
            z = ToFloat(GetProp(pos, "Z"));
        }

        private static int PreferPositive(int preferred, int fallback)
        {
            return preferred > 0 ? preferred : fallback;
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

        private static object GetNested(object target, string a, string b)
        {
            return GetProp(GetProp(target, a), b);
        }

        private static IEnumerable Enumerate(object value)
        {
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

        private static string FormatAnyIdentity(object identity)
        {
            if (identity == null)
            {
                return null;
            }

            object type = GetProp(identity, "Type");
            object instance = GetProp(identity, "Instance");
            if (type != null && instance != null)
            {
                return Convert.ToString(type) + ":" + Convert.ToString(instance);
            }

            return Convert.ToString(identity);
        }

        private static int ToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                int parsed;
                return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                           ? parsed
                           : 0;
            }
        }

        private static float ToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                float parsed;
                return float.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                           ? parsed
                           : 0f;
            }
        }

        private static string FormatFloat(object value)
        {
            return ToFloat(value).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        }

        private static string TrimTo(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, max);
        }

        private static string Csv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string DescribeObject(object value, int maxChars)
        {
            if (value == null)
            {
                return string.Empty;
            }

            try
            {
                return TrimTo(Convert.ToString(value) ?? value.GetType().Name, maxChars);
            }
            catch
            {
                return value.GetType().Name;
            }
        }
    }
}
