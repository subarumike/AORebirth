namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

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
            FilthFlea(0x794DF703, 4, 93, 93.11594f, 107.61483f, 248.1135f),
            DiscardedPet(0x794DF70C, 6, 138, 158.913147f, 107.61483f, 245.805054f),
            FilthFlea(0x794DF719, 6, 138, 92.96334f, 107.61483f, 257.187653f),
            FilthFlea(0x794DF71A, 5, 115, 96.69107f, 107.61483f, 257.262329f),
            FilthFlea(0x794DF71D, 4, 93, 86.61528f, 107.61483f, 250.256943f),
            DiscardedPet(0x794DF729, 9, 205, 149.134018f, 107.61483f, 199.856888f),
            FilthFlea(0x794DF72A, 5, 115, 120.578316f, 107.61483f, 234.1854f),
            FilthFlea(0x794DF72E, 6, 138, 120.630936f, 107.61483f, 241.199829f),
            FilthFlea(0x794DF730, 6, 138, 122.1621f, 107.61483f, 236.5588f),
            DisobedientBot(0x794DF749, 8, 183, 114.395836f, 107.61483f, 231.561676f),
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
            FilthFlea(0x794E815F, 4, 93, 100.812515f, 107.61483f, 239.3656f),
            FilthFlea(0x794E8162, 4, 93, 88.36062f, 115.615f, 300.164978f),
            FilthFlea(0x794E8163, 4, 93, 86.083015f, 111.615f, 270.3774f),
            FilthFlea(0x794E8167, 6, 138, 101.651108f, 107.61483f, 237.365173f),
            FilthFlea(0x794E816B, 6, 138, 144.579971f, 107.61483f, 217.487f),
            Mugger(0x794E8174, 5, 92, 136.2028f, 107.61483f, 239.212845f),
            FilthFlea(0x794E8179, 6, 138, 84.66971f, 107.61483f, 258.626678f)
        };

        public CapturedSubwaySpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedSubwaySpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
        }

        private static CapturedSubwaySpawnDefinition FilthFlea(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z)
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
                22,
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
}
