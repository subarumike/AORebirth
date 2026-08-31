namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    #endregion

    public sealed class CapturedAreteRobotContentProvider
    {
        public delegate void ContentLogHandler(bool isError, string message);

        public const string RobotName = "Malfunctioning Cleaning Robot";

        public const int MonsterData = 297023;

        public const string CombatProfileSelector = "1481e2f9f1b55bde-ec1c75204dea6fc7";

        public const int CombatEvidenceSourceIdentity = unchecked((int)0x789753AC);

        public const string PatrolReplayRelativePath =
            @"Content\Captured\Arete\cleaning_robot_patrol_replay.csv";

        public const string PatrolReplaySourceRelativePath =
            @"AORebirth\Server\ZoneEngine\Content\Captured\Arete\cleaning_robot_patrol_replay.csv";

        // Capture 20260721-Rox-robots: 11 concurrent Malfunctioning Cleaning Robots
        // on Rex platform (lifecycle pet=False). Excludes Burning/Cleaning Robot / pets.
        private static readonly CapturedAreteRobotSpawnDefinition[] SpawnDefinitions =
        {
            new CapturedAreteRobotSpawnDefinition(0x79866553, 3594.546000f, 51.745000f, 799.167700f, 12, 1, 6, 3621.257320f, 52.500000f, 784.154419f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79866565, 3595.688480f, 51.745000f, 798.922058f, 12, 1, 6, 3594.186770f, 52.500000f, 799.765198f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x797D36A5, 3596.811770f, 51.745000f, 788.208900f, 12, 1, 6, 3596.055660f, 52.244999f, 788.684570f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79543CB6, 3596.979000f, 51.745000f, 783.935852f, 12, 1, 6, 3596.105220f, 51.744999f, 783.924805f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79866547, 3602.961000f, 52.135000f, 787.817261f, 12, 1, 6, 3596.975100f, 52.500000f, 772.298950f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x7986655E, 3609.403810f, 52.135000f, 791.897034f, 12, 1, 6, 3622.765380f, 52.500000f, 798.812988f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x7986653C, 3612.843260f, 52.135000f, 787.514200f, 12, 1, 6, 3612.093260f, 52.135000f, 787.764200f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79866562, 3612.874510f, 52.135000f, 787.537500f, 12, 1, 6, 3611.219730f, 52.500000f, 778.204224f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79866518, 3612.924000f, 52.135000f, 787.641200f, 12, 1, 6, 3598.853520f, 52.500000f, 787.261780f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x79866560, 3617.227780f, 51.745000f, 785.991800f, 12, 1, 6, 3599.246580f, 52.500000f, 786.828979f, CombatProfileSelector, CombatEvidenceSourceIdentity),
            new CapturedAreteRobotSpawnDefinition(0x7986655D, 3622.508540f, 51.745000f, 798.139500f, 12, 1, 6, 3612.194090f, 52.500000f, 787.771240f, CombatProfileSelector, CombatEvidenceSourceIdentity),
        };

        private readonly object replayLock = new object();

        private readonly ContentLogHandler logHandler;

        private readonly string[] patrolReplayPathCandidates;

        private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> patrolReplaySegments;

        public CapturedAreteRobotContentProvider()
            : this(null, null)
        {
        }

        public CapturedAreteRobotContentProvider(IEnumerable<string> patrolReplayPathCandidates)
            : this(patrolReplayPathCandidates, null)
        {
        }

        public CapturedAreteRobotContentProvider(ContentLogHandler logHandler)
            : this(null, logHandler)
        {
        }

        private CapturedAreteRobotContentProvider(
            IEnumerable<string> patrolReplayPathCandidates,
            ContentLogHandler logHandler)
        {
            this.logHandler = logHandler;
            if (patrolReplayPathCandidates == null)
            {
                return;
            }

            this.patrolReplayPathCandidates = new List<string>(patrolReplayPathCandidates).ToArray();
        }

        public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedAreteRobotSpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
        }

        public CapturedAreteRobotPatrolReplaySegment[] GetPatrolReplaySegments(int sourceInstance)
        {
            Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> routes = this.GetPatrolReplayRoutes();
            CapturedAreteRobotPatrolReplaySegment[] route;
            return routes.TryGetValue(sourceInstance, out route)
                       ? route
                       : new CapturedAreteRobotPatrolReplaySegment[0];
        }

        public string FindPatrolReplayPath()
        {
            if (this.patrolReplayPathCandidates != null)
            {
                for (int i = 0; i < this.patrolReplayPathCandidates.Length; i++)
                {
                    string candidate = this.patrolReplayPathCandidates[i];
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return string.Empty;
            }

            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 8 && directory != null; i++)
            {
                string candidate = Path.Combine(directory.FullName, PatrolReplayRelativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                candidate = Path.Combine(directory.FullName, PatrolReplaySourceRelativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            string currentDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), PatrolReplayRelativePath);
            if (File.Exists(currentDirectoryCandidate))
            {
                return currentDirectoryCandidate;
            }

            currentDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), PatrolReplaySourceRelativePath);
            return File.Exists(currentDirectoryCandidate) ? currentDirectoryCandidate : string.Empty;
        }

        private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> GetPatrolReplayRoutes()
        {
            lock (this.replayLock)
            {
                if (this.patrolReplaySegments == null)
                {
                    this.patrolReplaySegments = this.LoadPatrolReplayRoutes();
                }

                return this.patrolReplaySegments;
            }
        }

        private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> LoadPatrolReplayRoutes()
        {
            var routes = new Dictionary<int, List<CapturedPatrolReplayCsvRow>>();
            string path = this.FindPatrolReplayPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                this.Log(false, "Captured cleaning robot patrol replay CSV not found; using waypoint fallback.");
                return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
            {
                return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
            }

            string[] header = SplitCapturedMovementCsvLine(lines[0]);
            int capturedUtcIndex = FindCsvColumn(header, "CapturedUtc");
            int messageTypeIndex = FindCsvColumn(header, "MessageType");
            int sourceInstanceIndex = FindCsvColumn(header, "SourceInstance");
            int followKindIndex = FindCsvColumn(header, "FollowKind");
            int currentXIndex = FindCsvColumn(header, "CurrentX");
            int currentYIndex = FindCsvColumn(header, "CurrentY");
            int currentZIndex = FindCsvColumn(header, "CurrentZ");
            int destinationXIndex = FindCsvColumn(header, "DestinationX");
            int destinationYIndex = FindCsvColumn(header, "DestinationY");
            int destinationZIndex = FindCsvColumn(header, "DestinationZ");

            if (capturedUtcIndex < 0 || messageTypeIndex < 0 || sourceInstanceIndex < 0 || followKindIndex < 0
                || currentXIndex < 0 || currentYIndex < 0 || currentZIndex < 0
                || destinationXIndex < 0 || destinationYIndex < 0 || destinationZIndex < 0)
            {
                this.Log(true, "Captured cleaning robot patrol replay CSV header is missing required columns.");
                return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = SplitCapturedMovementCsvLine(lines[i]);
                if (fields.Length <= destinationZIndex
                    || !string.Equals(fields[messageTypeIndex], "FollowTarget", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(fields[followKindIndex], "NpcPath", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(fields[currentXIndex])
                    || string.IsNullOrWhiteSpace(fields[destinationXIndex]))
                {
                    continue;
                }

                int sourceInstance;
                DateTime capturedUtc;
                float currentX;
                float currentY;
                float currentZ;
                float destinationX;
                float destinationY;
                float destinationZ;

                if (!int.TryParse(fields[sourceInstanceIndex], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out sourceInstance)
                    || !DateTime.TryParse(fields[capturedUtcIndex], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out capturedUtc)
                    || !float.TryParse(fields[currentXIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out currentX)
                    || !float.TryParse(fields[currentYIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out currentY)
                    || !float.TryParse(fields[currentZIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out currentZ)
                    || !float.TryParse(fields[destinationXIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out destinationX)
                    || !float.TryParse(fields[destinationYIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out destinationY)
                    || !float.TryParse(fields[destinationZIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out destinationZ))
                {
                    continue;
                }

                List<CapturedPatrolReplayCsvRow> route;
                if (!routes.TryGetValue(sourceInstance, out route))
                {
                    route = new List<CapturedPatrolReplayCsvRow>();
                    routes[sourceInstance] = route;
                }

                route.Add(
                    new CapturedPatrolReplayCsvRow
                    {
                        CapturedUtc = capturedUtc,
                        StartX = currentX,
                        StartY = currentY,
                        StartZ = currentZ,
                        EndX = destinationX,
                        EndY = destinationY,
                        EndZ = destinationZ
                    });
            }

            var result = new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
            foreach (KeyValuePair<int, List<CapturedPatrolReplayCsvRow>> route in routes)
            {
                result[route.Key] = BuildPatrolReplay(route.Value);
            }

            return result;
        }

        private static CapturedAreteRobotPatrolReplaySegment[] BuildPatrolReplay(
            List<CapturedPatrolReplayCsvRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return new CapturedAreteRobotPatrolReplaySegment[0];
            }

            rows.Sort((x, y) => x.CapturedUtc.CompareTo(y.CapturedUtc));
            var result = new CapturedAreteRobotPatrolReplaySegment[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                double delayAfterSeconds = 0.25;
                if (i + 1 < rows.Count)
                {
                    delayAfterSeconds = Math.Max(0.01, (rows[i + 1].CapturedUtc - rows[i].CapturedUtc).TotalSeconds);
                }

                result[i] = new CapturedAreteRobotPatrolReplaySegment(
                    delayAfterSeconds,
                    rows[i].StartX,
                    rows[i].StartY,
                    rows[i].StartZ,
                    rows[i].EndX,
                    rows[i].EndY,
                    rows[i].EndZ);
            }

            return result;
        }

        private static int FindCsvColumn(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] SplitCapturedMovementCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    result.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private void Log(bool isError, string message)
        {
            if (this.logHandler != null)
            {
                this.logHandler(isError, message);
            }
        }

        private sealed class CapturedPatrolReplayCsvRow
        {
            public DateTime CapturedUtc { get; set; }

            public float StartX { get; set; }

            public float StartY { get; set; }

            public float StartZ { get; set; }

            public float EndX { get; set; }

            public float EndY { get; set; }

            public float EndZ { get; set; }
        }
    }

    public sealed class CapturedAreteRobotSpawnDefinition
    {
        public CapturedAreteRobotSpawnDefinition(
            int sourceInstance,
            float x,
            float y,
            float z,
            int health,
            int level,
            int runSpeed,
            float patrolX,
            float patrolY,
            float patrolZ,
            string combatProfileSelector,
            int combatEvidenceSourceIdentity)
        {
            this.SourceInstance = sourceInstance;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Health = health;
            this.Level = level;
            this.RunSpeed = runSpeed;
            this.PatrolX = patrolX;
            this.PatrolY = patrolY;
            this.PatrolZ = patrolZ;
            this.CombatProfileSelector = combatProfileSelector;
            this.CombatEvidenceSourceIdentity = combatEvidenceSourceIdentity;
        }

        public int SourceInstance { get; private set; }

        public float X { get; private set; }

        public float Y { get; private set; }

        public float Z { get; private set; }

        public int Health { get; private set; }

        public int Level { get; private set; }

        public int RunSpeed { get; private set; }

        public float PatrolX { get; private set; }

        public float PatrolY { get; private set; }

        public float PatrolZ { get; private set; }

        public string CombatProfileSelector { get; private set; }

        public int CombatEvidenceSourceIdentity { get; private set; }
    }

    public sealed class CapturedAreteRobotPatrolReplaySegment
    {
        public CapturedAreteRobotPatrolReplaySegment(
            double delayAfterSeconds,
            float startX,
            float startY,
            float startZ,
            float endX,
            float endY,
            float endZ)
        {
            this.DelayAfterSeconds = delayAfterSeconds;
            this.StartX = startX;
            this.StartY = startY;
            this.StartZ = startZ;
            this.EndX = endX;
            this.EndY = endY;
            this.EndZ = endZ;
        }

        public double DelayAfterSeconds { get; private set; }

        public float StartX { get; private set; }

        public float StartY { get; private set; }

        public float StartZ { get; private set; }

        public float EndX { get; private set; }

        public float EndY { get; private set; }

        public float EndZ { get; private set; }
    }
}
