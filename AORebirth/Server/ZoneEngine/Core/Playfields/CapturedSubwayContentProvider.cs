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
            new CapturedSubwaySpawnDefinition(
                0x794DF18C,
                "A096",
                "Filth Flea",
                17657,
                5,
                115,
                130,
                0,
                22,
                138,
                268964353,
                6,
                5,
                84.53492f,
                107.61483f,
                258.829254f),
            new CapturedSubwaySpawnDefinition(
                0x794DF195,
                "A096",
                "Filth Flea",
                17657,
                5,
                115,
                130,
                0,
                22,
                138,
                268964353,
                6,
                5,
                122.01f,
                107.61483f,
                236.633163f),
            new CapturedSubwaySpawnDefinition(
                0x794DF1B1,
                "A120",
                "Discarded Pet",
                17720,
                7,
                160,
                94,
                0,
                33,
                138,
                268980737,
                7,
                5,
                151.7985f,
                107.61483f,
                237.857925f),
            new CapturedSubwaySpawnDefinition(
                0x794DF1E0,
                "A120",
                "Discarded Pet",
                17720,
                7,
                160,
                94,
                0,
                32,
                138,
                268980737,
                7,
                5,
                182.7737f,
                107.61483f,
                250.147f,
                187.1f,
                107.6f,
                251.3f),
            new CapturedSubwaySpawnDefinition(
                0x794DF1D7,
                "A051",
                "Mugger",
                203734,
                5,
                92,
                94,
                40705,
                21,
                138,
                268964353,
                1,
                6,
                124.5729f,
                107.61483f,
                237.769943f),
            new CapturedSubwaySpawnDefinition(
                0x794DF076,
                "A051",
                "Violent Vagabond",
                203733,
                6,
                110,
                93,
                40676,
                18,
                3,
                268964353,
                1,
                6,
                148.7867f,
                107.61483f,
                275.0773f,
                153.6035f,
                107.61483f,
                269.5549f)
        };

        public CapturedSubwaySpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedSubwaySpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
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
