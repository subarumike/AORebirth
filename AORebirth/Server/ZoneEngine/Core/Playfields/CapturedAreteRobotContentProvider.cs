namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    #endregion

    public sealed class CapturedAreteRobotContentProvider
    {
        public const string RobotName = "Malfunctioning Cleaning Robot";

        public const int MonsterData = 297023;

        // Capture 20260721-Rox-robots: eleven concurrent, non-pet Malfunctioning
        // Cleaning Robots on the Rex platform. Movement is owned by schema 4.
        private static readonly CapturedAreteRobotSpawnDefinition[] SpawnDefinitions =
        {
            new CapturedAreteRobotSpawnDefinition(0x79866553, 3594.546000f, 51.745000f, 799.167700f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79866565, 3595.688480f, 51.745000f, 798.922058f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x797D36A5, 3596.811770f, 51.745000f, 788.208900f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79543CB6, 3596.979000f, 51.745000f, 783.935852f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79866547, 3602.961000f, 52.135000f, 787.817261f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x7986655E, 3609.403810f, 52.135000f, 791.897034f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x7986653C, 3612.843260f, 52.135000f, 787.514200f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79866562, 3612.874510f, 52.135000f, 787.537500f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79866518, 3612.924000f, 52.135000f, 787.641200f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x79866560, 3617.227780f, 51.745000f, 785.991800f, 12, 1, 6),
            new CapturedAreteRobotSpawnDefinition(0x7986655D, 3622.508540f, 51.745000f, 798.139500f, 12, 1, 6),
        };

        public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedAreteRobotSpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
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
            int runSpeed)
        {
            this.SourceInstance = sourceInstance;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Health = health;
            this.Level = level;
            this.RunSpeed = runSpeed;
        }

        public int SourceInstance { get; private set; }

        public float X { get; private set; }

        public float Y { get; private set; }

        public float Z { get; private set; }

        public int Health { get; private set; }

        public int Level { get; private set; }

        public int RunSpeed { get; private set; }
    }
}
