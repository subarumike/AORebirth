namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    internal sealed class CapturedSubwayContentProvider
    {
        public const int SubwayPlayfieldInstance = 127;

        private static readonly CapturedSubwaySpawnDefinition[] SpawnDefinitions =
        {
            FilthFlea(0x794ADC01, 5, 115, 145.625015f, 107.61483f, 199.491653f),
            FilthFlea(0x794ADC04, 5, 115, 150.841934f, 107.61483f, 200.950867f),
            FilthFlea(0x794ADC0E, 6, 138, 146.663925f, 107.61483f, 201.587692f),
            FilthFlea(0x794D7AAF, 6, 138, 152.891312f, 107.61483f, 203.949051f),
            DiscardedPet(0x794DF1E3, 7, 160, 185.44902f, 107.61483f, 241.865524f),
            DiscardedPet(0x794DF1E5, 5, 115, 184.843964f, 107.61483f, 240.569778f),
            ViolentVagabond(0x794DF1F4, 7, 128, 169.0753f, 107.61483f, 246.01f),
            FilthFlea(0x794DF2C6, 5, 115, 156.022324f, 107.61483f, 247.582184f),
            DiscardedPet(0x794DF2CC, 7, 160, 159.4724f, 107.61483f, 233.273438f),
            DiscardedPet(0x794DF2D8, 5, 115, 158.730713f, 107.61483f, 235.305878f),
            FilthFlea(0x794DF2E9, 6, 138, 148.609024f, 107.61483f, 199.3537f),
            DiscardedPet(0x794DF2F1, 6, 138, 159.2063f, 107.61483f, 240.190948f),
            FirstLowerSectionSpawn(FilthFlea(0x794DF703, 4, 93, 93.11594f, 107.61483f, 248.1135f, 18)),
            DiscardedPet(0x794DF70C, 6, 138, 158.913147f, 107.61483f, 245.805054f),
            FirstLowerSectionSpawn(FilthFlea(0x794DF719, 6, 138, 92.96334f, 107.61483f, 257.187653f, 25)),
            FirstLowerSectionSpawn(FilthFlea(0x794DF71A, 5, 115, 96.69107f, 107.61483f, 257.262329f)),
            FirstLowerSectionSpawn(FilthFlea(0x794DF71D, 4, 93, 86.61528f, 107.61483f, 250.256943f)),
            DiscardedPet(0x794DF729, 9, 205, 149.134018f, 107.61483f, 199.856888f),
            FirstLowerSectionSpawn(FilthFlea(0x794DF72A, 5, 115, 120.578316f, 107.61483f, 234.1854f)),
            FirstLowerSectionSpawn(FilthFlea(0x794DF72E, 6, 138, 120.630936f, 107.61483f, 241.199829f)),
            FirstLowerSectionSpawn(FilthFlea(0x794DF730, 6, 138, 122.1621f, 107.61483f, 236.5588f)),
            FirstLowerSectionSpawn(DisobedientBot(0x794DF749, 8, 183, 114.395836f, 107.61483f, 231.561676f)),
            DisobedientBot(0x794DF7F1, 7, 160, 173.6774f, 107.61483f, 232.1588f),
            DisobedientBot(0x794E807A, 5, 115, 179.707809f, 107.61483f, 231.963226f),
            Thief(
                0x794E80A1,
                5,
                115,
                80.79672f,
                115.765f,
                323.7208f,
                86.76542f,
                115.9823f,
                322.6304f),
            FirstLowerSectionSpawn(FilthFlea(0x794E815F, 4, 93, 100.812515f, 107.61483f, 239.3656f)),
            FilthFlea(0x794E8162, 4, 93, 88.36062f, 115.615f, 300.164978f),
            FilthFlea(0x794E8163, 4, 93, 86.083015f, 111.615f, 270.3774f),
            FirstLowerSectionSpawn(FilthFlea(0x794E8167, 6, 138, 101.651108f, 107.61483f, 237.365173f)),
            FilthFlea(0x794E816B, 6, 138, 144.579971f, 107.61483f, 217.487f),
            Mugger(0x794E8174, 5, 92, 136.2028f, 107.61483f, 239.212845f),
            FirstLowerSectionSpawn(FilthFlea(0x794E8179, 6, 138, 84.66971f, 107.61483f, 258.626678f))
        };

        // Source: capture 20260709-164414 movement-packets.csv. These are complete
        // periodic NpcPath cycles, not the earlier partial samples that snapped when looped.
        private static readonly Dictionary<int, CapturedSubwayPatrolReplaySegment[]> PatrolReplaySegments =
            new Dictionary<int, CapturedSubwayPatrolReplaySegment[]>
            {
                {
                    0x794DF703,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.665506, 90.9275284f, 107.61483f, 248.660339f, 93.7800903f, 107.61483f, 246.556122f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.450008, 93.2423325f, 107.61483f, 247.87915f, 96.4353943f, 107.61483f, 245.377533f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.433567, 95.7327881f, 107.61483f, 245.781525f, 96.4992371f, 107.61483f, 243.006943f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.65, 96.6621399f, 107.61483f, 244.05574f, 94.3061218f, 107.61483f, 240.678986f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.932947, 95.3061066f, 107.61483f, 241.413315f, 91.1917419f, 107.61483f, 239.323898f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.617, 92.3508301f, 107.61483f, 239.737793f, 88.4544373f, 107.61483f, 240.479248f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.433049, 89.2933578f, 107.61483f, 240.028229f, 86.9726868f, 107.61483f, 243.484451f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.617011, 87.3437653f, 107.61483f, 242.336823f, 87.5973511f, 107.61483f, 246.433868f, 25),
                        new CapturedSubwayPatrolReplaySegment(1.149506, 87.2730255f, 107.61483f, 245.358917f, 91.8999939f, 108.604828f, 249.100006f, 25)
                    }
                },
                {
                    0x794DF719,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.441025, 94.3362808f, 107.61483f, 257.132813f, 95.413147f, 108.601692f, 258.466431f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200505, 94.8347321f, 107.61483f, 257.172943f, 95.6919556f, 107.611687f, 256.584564f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.366001, 95.0244675f, 107.61483f, 257.222717f, 94.1281738f, 107.611687f, 256.459503f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200507, 95.1658936f, 107.61483f, 257.186981f, 94.1888733f, 107.611687f, 257.977692f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.949525, 95.1728973f, 107.61483f, 257.181f, 92.7790985f, 107.611687f, 258.242126f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.684136, 94.0233002f, 107.61483f, 257.701538f, 91.8856354f, 107.611687f, 256.911926f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.4331, 93.0863495f, 107.61483f, 257.547333f, 93.2575684f, 107.611687f, 255.791214f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.466668, 92.7739334f, 107.61483f, 257.126617f, 91.8856354f, 107.611687f, 256.911926f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.233, 92.6570053f, 107.61483f, 256.900421f, 92.7790985f, 107.611687f, 258.242126f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.900008, 92.5241089f, 107.61483f, 256.814789f, 94.1888733f, 107.611687f, 257.977692f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 93.0371857f, 107.61483f, 257.226532f, 94.1281738f, 107.611687f, 256.459503f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.833564, 93.3161469f, 107.61483f, 257.312469f, 95.6919556f, 107.611687f, 256.584564f, 24)
                    }
                },
                {
                    0x794DF72A,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.250007, 120.377983f, 107.61483f, 238.187988f, 120.357513f, 107.61483f, 238.598038f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.367001, 120.468063f, 107.61483f, 238.436417f, 119.140091f, 107.61483f, 237.279144f, 24),
                        new CapturedSubwayPatrolReplaySegment(2.199574, 120.261276f, 107.61483f, 238.25618f, 120.197006f, 107.61483f, 234.030853f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.883575, 120.148712f, 107.61483f, 235.243713f, 121.031387f, 107.61483f, 232.799698f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.65, 120.531502f, 107.61483f, 233.990005f, 121.690552f, 107.61483f, 232.025986f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 121.015503f, 107.61483f, 233.102234f, 121.300003f, 109.104828f, 231.699997f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.251002, 121.146011f, 107.61483f, 232.840668f, 121.690552f, 107.61483f, 232.025986f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.198999, 121.296318f, 107.61483f, 232.563324f, 121.031387f, 107.61483f, 232.799698f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.099945, 121.39901f, 107.61483f, 232.374069f, 120.197006f, 107.61483f, 234.030853f, 24),
                        new CapturedSubwayPatrolReplaySegment(2.199565, 120.925278f, 107.61483f, 232.96994f, 119.140091f, 107.61483f, 237.279144f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.950509, 119.64579f, 107.61483f, 235.990005f, 120.357513f, 107.61483f, 238.598038f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.683313, 119.875526f, 107.61483f, 237.319199f, 121.137146f, 107.61483f, 239.347321f, 24)
                    }
                },
                {
                    0x794DF730,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.750019, 122.448677f, 107.61483f, 236.743958f, 120.128296f, 107.61483f, 236.93544f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.883642, 121.360664f, 107.61483f, 236.610458f, 119.547424f, 107.61483f, 238.470001f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.899767, 120.38768f, 107.61483f, 237.447113f, 120.098579f, 109.104828f, 239.65303f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.233, 120.125732f, 107.61483f, 238.247757f, 119.547424f, 107.61483f, 238.470001f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.617008, 120.0289f, 107.61483f, 238.470566f, 120.128296f, 107.61483f, 236.93544f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.116564, 119.850159f, 107.61483f, 238.367874f, 121.652603f, 107.61483f, 235.707397f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.417505, 120.783905f, 107.61483f, 236.876495f, 122.734451f, 107.61483f, 236.43454f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.665506, 121.274498f, 107.61483f, 236.591461f, 122.990768f, 107.61483f, 237.321808f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 121.894073f, 107.61483f, 236.689957f, 122.526489f, 107.61483f, 238.292267f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200001, 122.137306f, 107.61483f, 236.882767f, 122.990768f, 107.61483f, 237.321808f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.250007, 122.346909f, 107.61483f, 237.05751f, 122.734451f, 107.61483f, 236.43454f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.683567, 122.601311f, 107.61483f, 237.150742f, 121.652603f, 107.61483f, 235.707397f, 24)
                    }
                }
            };

        public CapturedSubwaySpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedSubwaySpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
        }

        public CapturedSubwayPatrolReplaySegment[] GetPatrolReplaySegments(int sourceInstance)
        {
            CapturedSubwayPatrolReplaySegment[] segments;
            if (!PatrolReplaySegments.TryGetValue(sourceInstance, out segments))
            {
                return new CapturedSubwayPatrolReplaySegment[0];
            }

            var result = new CapturedSubwayPatrolReplaySegment[segments.Length];
            Array.Copy(segments, result, segments.Length);
            return result;
        }

        private static CapturedSubwaySpawnDefinition FirstLowerSectionSpawn(
            CapturedSubwaySpawnDefinition spawn)
        {
            spawn.ContentSection = "FirstLowerSection";
            return spawn;
        }

        private static CapturedSubwaySpawnDefinition FilthFlea(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int runSpeed = 22)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A096",
                "Filth Flea",
                17657,
                level,
                health,
                130,
                0,
                runSpeed,
                138,
                268964353,
                6,
                5,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition DiscardedPet(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A120",
                "Discarded Pet",
                17720,
                level,
                health,
                94,
                0,
                33,
                138,
                268980737,
                7,
                5,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition DisobedientBot(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A120",
                "Disobedient Bot",
                17649,
                level,
                health,
                90,
                0,
                33,
                95,
                403182081,
                7,
                5,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition Mugger(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Mugger",
                203734,
                level,
                health,
                94,
                40705,
                21,
                138,
                268964353,
                1,
                6,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition Thief(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            float patrolX,
            float patrolY,
            float patrolZ)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Thief",
                26092,
                level,
                health,
                93,
                40694,
                20,
                138,
                268964353,
                1,
                6,
                x,
                y,
                z,
                patrolX,
                patrolY,
                patrolZ);
        }

        private static CapturedSubwaySpawnDefinition ViolentVagabond(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Violent Vagabond",
                203733,
                level,
                health,
                93,
                40676,
                18,
                3,
                268964353,
                1,
                6,
                x,
                y,
                z);
        }
    }

    internal sealed class CapturedSubwaySpawnDefinition
    {
        public CapturedSubwaySpawnDefinition(
            int sourceInstance,
            string templateHash,
            string name,
            int monsterData,
            int level,
            int health,
            int monsterScale,
            int headMesh,
            int runSpeed,
            int npcFamily,
            int characterFlags,
            int breed,
            int sex,
            float x,
            float y,
            float z,
            float? patrolX = null,
            float? patrolY = null,
            float? patrolZ = null)
        {
            this.SourceInstance = sourceInstance;
            this.ContentSection = "CapturedPopulation";
            this.TemplateHash = templateHash;
            this.Name = name;
            this.MonsterData = monsterData;
            this.Level = level;
            this.Health = health;
            this.MonsterScale = monsterScale;
            this.HeadMesh = headMesh;
            this.RunSpeed = runSpeed;
            this.NpcFamily = npcFamily;
            this.CharacterFlags = characterFlags;
            this.Breed = breed;
            this.Sex = sex;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.PatrolX = patrolX;
            this.PatrolY = patrolY;
            this.PatrolZ = patrolZ;
        }

        public int SourceInstance { get; private set; }

        public string ContentSection { get; internal set; }

        public string TemplateHash { get; private set; }

        public string Name { get; private set; }

        public int MonsterData { get; private set; }

        public int Level { get; private set; }

        public int Health { get; private set; }

        public int MonsterScale { get; private set; }

        public int HeadMesh { get; private set; }

        public int RunSpeed { get; private set; }

        public int NpcFamily { get; private set; }

        public int CharacterFlags { get; private set; }

        public int Breed { get; private set; }

        public int Sex { get; private set; }

        public float X { get; private set; }

        public float Y { get; private set; }

        public float Z { get; private set; }

        public float? PatrolX { get; private set; }

        public float? PatrolY { get; private set; }

        public float? PatrolZ { get; private set; }

        public bool HasPatrolWaypoint
        {
            get
            {
                return this.PatrolX.HasValue && this.PatrolY.HasValue && this.PatrolZ.HasValue;
            }
        }
    }

    internal sealed class CapturedSubwayPatrolReplaySegment
    {
        public CapturedSubwayPatrolReplaySegment(
            double delayAfterSeconds,
            float startX,
            float startY,
            float startZ,
            float endX,
            float endY,
            float endZ,
            byte moveMode)
        {
            this.DelayAfterSeconds = delayAfterSeconds;
            this.StartX = startX;
            this.StartY = startY;
            this.StartZ = startZ;
            this.EndX = endX;
            this.EndY = endY;
            this.EndZ = endZ;
            this.MoveMode = moveMode;
        }

        public double DelayAfterSeconds { get; private set; }

        public float StartX { get; private set; }

        public float StartY { get; private set; }

        public float StartZ { get; private set; }

        public float EndX { get; private set; }

        public float EndY { get; private set; }

        public float EndZ { get; private set; }

        public byte MoveMode { get; private set; }
    }
}
