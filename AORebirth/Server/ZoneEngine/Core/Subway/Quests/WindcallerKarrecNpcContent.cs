namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal static class WindcallerKarrecNpcContent
    {
        internal const int PlayfieldId = 655;

        internal const int KarrecSourceInstance = 0x796360BB;

        internal const int MaddyCardileSourceInstance = 0x796360BC;

        internal const int AnnoyingDudeSourceInstance = 0x796360BD;

        internal const string Evidence = "AOSharpLiveCapture/20260717-223626";

        /// <summary>
        /// Karrec sit + full mesh/texture: dynel focus capture 20260719-174340 (no SCFU;
        /// already spawned) plus same-day SCFU wire from 20260719-ICC-Capture.
        /// </summary>
        internal const string KarrecAppearanceEvidence =
            "AOSharpLiveCapture/20260719-174340+20260719-ICC-Capture";

        private static readonly WindcallerKarrecNpcDefinition KarrecDefinition = CreateKarrec();

        private static readonly WindcallerKarrecNpcDefinition MaddyCardileDefinition = CreateMaddyCardile();

        private static readonly WindcallerKarrecNpcDefinition AnnoyingDudeDefinition = CreateAnnoyingDude();

        private static readonly ReadOnlyCollection<WindcallerKarrecNpcDefinition> CapturedDefinitions =
            Array.AsReadOnly(
                new[]
                {
                    KarrecDefinition,
                    AnnoyingDudeDefinition,
                    MaddyCardileDefinition
                });

        internal static ReadOnlyCollection<WindcallerKarrecNpcDefinition> Definitions
        {
            get { return CapturedDefinitions; }
        }

        internal static WindcallerKarrecNpcDefinition Karrec
        {
            get { return KarrecDefinition; }
        }

        internal static WindcallerKarrecNpcDefinition AnnoyingDude
        {
            get { return AnnoyingDudeDefinition; }
        }

        internal static WindcallerKarrecNpcDefinition MaddyCardile
        {
            get { return MaddyCardileDefinition; }
        }

        internal static bool TryGetBySourceInstance(
            int sourceInstance,
            out WindcallerKarrecNpcDefinition definition)
        {
            foreach (WindcallerKarrecNpcDefinition candidate in CapturedDefinitions)
            {
                if (candidate.SourceNpcInstance == sourceInstance)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private static WindcallerKarrecNpcDefinition CreateKarrec()
        {
            return new WindcallerKarrecNpcDefinition(
                KarrecSourceInstance,
                "Windcaller Karrec",
                3212.36963f,
                35.975f,
                788.7493f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                1576,
                (int)Side.Neutral,
                (int)Fatness.Normal,
                (int)Breed.Solitus,
                (int)Gender.Male,
                1,
                40818,
                121,
                40696,
                136,
                0,
                200,
                51008,
                515,
                277352961,
                31,
                1,
                170552011u,
                // Unknown1 move-mode byte 0x08 = Sit (MoveModes.Sit).
                HexToBytes("00000000000000000000000008010001000100010001000000020000"),
                new[]
                {
                    new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
                    new WindcallerKarrecNpcTextureDefinition(1, 161710, 0),
                    new WindcallerKarrecNpcTextureDefinition(2, 161715, 0),
                    new WindcallerKarrecNpcTextureDefinition(3, 161705, 0),
                    new WindcallerKarrecNpcTextureDefinition(4, 161725, 0)
                },
                new[]
                {
                    new WindcallerKarrecNpcMeshDefinition(0, 20108, 161720, 2),
                    new WindcallerKarrecNpcMeshDefinition(0, 40696, 0, 4)
                },
                new WindcallerKarrecNpcWaypointDefinition[0],
                new WindcallerKarrecNpcPatrolSegment[0],
                new[]
                {
                    new WindcallerKarrecNpcActiveNanoDefinition(53019, 0x3233F, 0, 29050327, 20192939)
                },
                KarrecAppearanceEvidence);
        }

        private static WindcallerKarrecNpcDefinition CreateAnnoyingDude()
        {
            return new WindcallerKarrecNpcDefinition(
                AnnoyingDudeSourceInstance,
                "Annoying Dude",
                3185.87134f,
                35.1100006f,
                963.378967f,
                0.0f,
                -0.7624053f,
                0.0f,
                0.6470998f,
                1672,
                (int)Side.Neutral,
                (int)Fatness.Normal,
                (int)Breed.Atrox,
                (int)Gender.Male,
                1,
                26103,
                104,
                40117,
                103,
                0,
                45,
                1958,
                154,
                277352961,
                31,
                0,
                168512203u,
                HexToBytes("00000000000000000000000002010001000100010001000000020000"),
                new[]
                {
                    new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
                    new WindcallerKarrecNpcTextureDefinition(1, 247946, 0),
                    new WindcallerKarrecNpcTextureDefinition(2, 247981, 0),
                    new WindcallerKarrecNpcTextureDefinition(3, 247900, 0),
                    new WindcallerKarrecNpcTextureDefinition(4, 248021, 0)
                },
                new[]
                {
                    new WindcallerKarrecNpcMeshDefinition(0, 40117, 0, 4),
                    new WindcallerKarrecNpcMeshDefinition(1, 136570, 0, 2)
                },
                new[]
                {
                    new WindcallerKarrecNpcWaypointDefinition(3185.87134f, 35.1100006f, 963.378967f)
                },
                AnnoyingDudePatrol(),
                new WindcallerKarrecNpcActiveNanoDefinition[0],
                Evidence);
        }

        private static WindcallerKarrecNpcDefinition CreateMaddyCardile()
        {
            return new WindcallerKarrecNpcDefinition(
                MaddyCardileSourceInstance,
                "Maddy Cardile",
                3332.37524f,
                35.1100006f,
                931.1814f,
                0.0f,
                -0.5605756f,
                0.0f,
                0.828103244f,
                1832,
                (int)Side.Neutral,
                (int)Fatness.Normal,
                (int)Breed.Solitus,
                (int)Gender.Female,
                1,
                26090,
                121,
                40647,
                103,
                0,
                200,
                365,
                515,
                277352961,
                31,
                0,
                168520395u,
                // Capture decode for Maddy Unknown1 was corrupt (float garbage in the first
                // 12 bytes). Use the same NPC Unknown1 shape as Dude/Karrec.
                HexToBytes("00000000000000000000000002010001000100010001000000020000"),
                new[]
                {
                    new WindcallerKarrecNpcTextureDefinition(0, 0, 0),
                    new WindcallerKarrecNpcTextureDefinition(1, 247974, 0),
                    new WindcallerKarrecNpcTextureDefinition(2, 248003, 0),
                    new WindcallerKarrecNpcTextureDefinition(3, 247927, 0),
                    new WindcallerKarrecNpcTextureDefinition(4, 248040, 0)
                },
                new[]
                {
                    // Head mesh only. Capture also listed mesh 290019; keep that out until
                    // SCFU is stable — secondary mesh was a crash suspect with forced flags.
                    new WindcallerKarrecNpcMeshDefinition(0, 40647, 0, 4)
                },
                new[]
                {
                    new WindcallerKarrecNpcWaypointDefinition(3332.37524f, 35.1100006f, 931.1814f)
                },
                MaddyCardilePatrol(),
                new WindcallerKarrecNpcActiveNanoDefinition[0],
                Evidence);
        }

        private static WindcallerKarrecNpcPatrolSegment[] AnnoyingDudePatrol()
        {
            return new[]
            {
                new WindcallerKarrecNpcPatrolSegment(1.2518461, 3185.87134f, 35.1100006f, 963.378967f, 3183.13818f, 35.1100006f, 963.90625f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.0699980, 3184.43091f, 35.1100006f, 963.656799f, 3179.62305f, 35.1100006f, 966.895264f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.1199181, 3180.61621f, 35.1100006f, 966.191711f, 3176.29053f, 35.1100006f, 966.785339f, 24),
                new WindcallerKarrecNpcPatrolSegment(1.1513932, 3177.75879f, 35.1100006f, 966.64209f, 3175.02539f, 35.1100006f, 964.88031f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.8999266, 3176.09229f, 35.1100006f, 965.695618f, 3176.85815f, 35.1100006f, 960.427917f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.7219631, 3176.59302f, 35.1100006f, 961.591125f, 3179.3623f, 35.1100006f, 956.972656f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.7397159, 3178.58008f, 35.1100006f, 958.218567f, 3183.01025f, 35.1100006f, 955.028564f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.6405055, 3181.99487f, 35.1100006f, 955.722473f, 3188.08887f, 35.1100006f, 955.118347f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.5103682, 3186.94946f, 35.1100006f, 955.200012f, 3192.25293f, 35.1100006f, 956.632202f, 24),
                new WindcallerKarrecNpcPatrolSegment(4.3023294, 3190.99561f, 35.1100006f, 956.263672f, 3193.98438f, 35.1100006f, 963.076416f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.4452929, 3193.50903f, 35.1100006f, 961.860352f, 3196.06567f, 35.1100006f, 966.216187f, 24),
                new WindcallerKarrecNpcPatrolSegment(1.9668067, 3195.44995f, 35.1100006f, 965.192749f, 3194.46362f, 35.1100006f, 969.180298f, 24),
                new WindcallerKarrecNpcPatrolSegment(1.8002349, 3194.85718f, 35.1100006f, 967.977356f, 3191.19824f, 35.1100006f, 969.312805f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.2201903, 3192.55103f, 35.1100006f, 968.957703f, 3189.87671f, 35.1100006f, 965.374878f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.1293892, 3190.59326f, 35.1100006f, 966.593018f, 3188.67847f, 35.1100006f, 963.080872f, 24),
                new WindcallerKarrecNpcPatrolSegment(4.2700076, 3189.2793f, 35.1100006f, 964.190735f, 3185.70703f, 35.1100006f, 963.35199f, 24)
            };
        }

        private static WindcallerKarrecNpcPatrolSegment[] MaddyCardilePatrol()
        {
            return new[]
            {
                new WindcallerKarrecNpcPatrolSegment(4.3708276, 3331.09204f, 35.1100006f, 931.694885f, 3328.16504f, 35.1100006f, 938.848145f, 24),
                new WindcallerKarrecNpcPatrolSegment(4.7539665, 3328.61816f, 35.1100006f, 937.61615f, 3334.11792f, 35.1100006f, 943.119141f, 24),
                new WindcallerKarrecNpcPatrolSegment(6.8341767, 3333.1355f, 35.1100006f, 942.240051f, 3344.75806f, 35.1100006f, 945.087158f, 24),
                new WindcallerKarrecNpcPatrolSegment(5.5713148, 3343.38892f, 35.1100006f, 944.770386f, 3352.49878f, 35.1100006f, 941.203552f, 24),
                new WindcallerKarrecNpcPatrolSegment(5.4357095, 3351.20825f, 35.1100006f, 941.737549f, 3352.76831f, 35.1100006f, 932.732605f, 24),
                new WindcallerKarrecNpcPatrolSegment(5.2456334, 3352.61011f, 35.1100006f, 933.927795f, 3350.84814f, 35.1100006f, 924.866455f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.8700161, 3351.12354f, 35.1100006f, 926.201233f, 3347.55029f, 35.1100006f, 920.69873f, 24),
                new WindcallerKarrecNpcPatrolSegment(6.8971459, 3348.28491f, 35.1100006f, 921.783936f, 3336.63647f, 35.1100006f, 918.530457f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.0849565, 3337.87671f, 35.1100006f, 918.851685f, 3332.91382f, 35.1100006f, 915.320435f, 24),
                new WindcallerKarrecNpcPatrolSegment(1.5295171, 3334.10156f, 35.1100006f, 916.202332f, 3331.51709f, 35.1100006f, 913.763184f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.8867470, 3332.43701f, 35.1100006f, 914.654663f, 3326.88892f, 35.1100006f, 915.19574f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.6604726, 3328.31934f, 35.1100006f, 914.972351f, 3323.97485f, 35.1100006f, 918.013f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.5598438, 3325.02759f, 35.1100006f, 917.232056f, 3321.20337f, 35.1100006f, 920.767212f, 24),
                new WindcallerKarrecNpcPatrolSegment(1.9879705, 3322.17896f, 35.1100006f, 919.851807f, 3320.11963f, 35.1100006f, 923.598633f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.7351056, 3320.71411f, 35.1100006f, 922.44458f, 3326.41333f, 35.1100006f, 925.535889f, 24),
                new WindcallerKarrecNpcPatrolSegment(4.7399797, 3325.08179f, 35.1100006f, 924.962952f, 3333.34058f, 35.1100006f, 924.618835f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.2594041, 3332.13452f, 35.1100006f, 924.690369f, 3337.28589f, 35.1100006f, 927.924072f, 24),
                new WindcallerKarrecNpcPatrolSegment(2.1707049, 3336.26538f, 35.1100006f, 927.233521f, 3334.62817f, 35.1100006f, 930.946777f, 24),
                new WindcallerKarrecNpcPatrolSegment(3.2493866, 3335.30371f, 35.1100006f, 929.863892f, 3329.78906f, 35.1100006f, 932.215942f, 24)
            };
        }

        private static byte[] HexToBytes(string value)
        {
            var result = new byte[value.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            }

            return result;
        }
    }

    internal sealed class WindcallerKarrecNpcDefinition
    {
        internal WindcallerKarrecNpcDefinition(
            int sourceNpcInstance,
            string displayName,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            int appearanceValue,
            int side,
            int fatness,
            int breed,
            int sex,
            int race,
            int monsterData,
            int monsterScale,
            int headMesh,
            int npcFamily,
            int npcLosHeight,
            int level,
            int health,
            int runSpeed,
            int characterFlags,
            int visualFlags,
            int visibleTitle,
            uint capturedScfuFlags,
            byte[] capturedScfuUnknown1,
            WindcallerKarrecNpcTextureDefinition[] textures,
            WindcallerKarrecNpcMeshDefinition[] meshes,
            WindcallerKarrecNpcWaypointDefinition[] scfuWaypoints,
            WindcallerKarrecNpcPatrolSegment[] patrolSegments,
            WindcallerKarrecNpcActiveNanoDefinition[] activeNanos,
            string evidence)
        {
            this.SourceNpcInstance = sourceNpcInstance;
            this.DisplayName = displayName;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.HeadingX = headingX;
            this.HeadingY = headingY;
            this.HeadingZ = headingZ;
            this.HeadingW = headingW;
            this.AppearanceValue = appearanceValue;
            this.Side = side;
            this.Fatness = fatness;
            this.Breed = breed;
            this.Sex = sex;
            this.Race = race;
            this.MonsterData = monsterData;
            this.MonsterScale = monsterScale;
            this.HeadMesh = headMesh;
            this.NpcFamily = npcFamily;
            this.NpcLosHeight = npcLosHeight;
            this.Level = level;
            this.Health = health;
            this.RunSpeed = runSpeed;
            this.CharacterFlags = characterFlags;
            this.VisualFlags = visualFlags;
            this.VisibleTitle = visibleTitle;
            this.CapturedScfuFlags = capturedScfuFlags;
            this.CapturedScfuUnknown1 = Array.AsReadOnly((byte[])capturedScfuUnknown1.Clone());
            this.Textures = Array.AsReadOnly((WindcallerKarrecNpcTextureDefinition[])textures.Clone());
            this.Meshes = Array.AsReadOnly((WindcallerKarrecNpcMeshDefinition[])meshes.Clone());
            this.ScfuWaypoints = Array.AsReadOnly((WindcallerKarrecNpcWaypointDefinition[])scfuWaypoints.Clone());
            this.PatrolSegments = Array.AsReadOnly((WindcallerKarrecNpcPatrolSegment[])patrolSegments.Clone());
            this.ActiveNanos = Array.AsReadOnly((WindcallerKarrecNpcActiveNanoDefinition[])activeNanos.Clone());
            this.Evidence = evidence;
        }

        internal int SourceNpcInstance { get; private set; }

        internal string SourceNpcIdentity
        {
            get
            {
                return "SimpleChar:"
                       + this.SourceNpcInstance.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        internal string DisplayName { get; private set; }

        internal int PlayfieldId
        {
            get { return WindcallerKarrecNpcContent.PlayfieldId; }
        }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }

        internal float HeadingX { get; private set; }

        internal float HeadingY { get; private set; }

        internal float HeadingZ { get; private set; }

        internal float HeadingW { get; private set; }

        internal int AppearanceValue { get; private set; }

        internal int Side { get; private set; }

        internal int Fatness { get; private set; }

        internal int Breed { get; private set; }

        internal int Sex { get; private set; }

        internal int Race { get; private set; }

        internal int MonsterData { get; private set; }

        internal int MonsterScale { get; private set; }

        internal int HeadMesh { get; private set; }

        internal int NpcFamily { get; private set; }

        internal int NpcLosHeight { get; private set; }

        internal int Level { get; private set; }

        internal int Health { get; private set; }

        internal int RunSpeed { get; private set; }

        internal int CharacterFlags { get; private set; }

        internal int VisualFlags { get; private set; }

        internal int VisibleTitle { get; private set; }

        internal uint CapturedScfuFlags { get; private set; }

        internal ReadOnlyCollection<byte> CapturedScfuUnknown1 { get; private set; }

        internal ReadOnlyCollection<WindcallerKarrecNpcTextureDefinition> Textures { get; private set; }

        internal ReadOnlyCollection<WindcallerKarrecNpcMeshDefinition> Meshes { get; private set; }

        internal ReadOnlyCollection<WindcallerKarrecNpcWaypointDefinition> ScfuWaypoints { get; private set; }

        internal WindcallerKarrecNpcWaypointDefinition ResolveScfuCoordinates(
            bool hasActivePatrolDestination,
            float existingX,
            float existingY,
            float existingZ,
            float currentX,
            float currentY,
            float currentZ)
        {
            return hasActivePatrolDestination && this.HasPatrol
                       ? new WindcallerKarrecNpcWaypointDefinition(currentX, currentY, currentZ)
                       : new WindcallerKarrecNpcWaypointDefinition(existingX, existingY, existingZ);
        }

        internal WindcallerKarrecNpcWaypointDefinition[] ResolveScfuWaypoints(
            bool hasActivePatrolDestination,
            float currentX,
            float currentY,
            float currentZ,
            float destinationX,
            float destinationY,
            float destinationZ)
        {
            if (hasActivePatrolDestination && this.HasPatrol)
            {
                return new[]
                       {
                           new WindcallerKarrecNpcWaypointDefinition(
                               currentX,
                               currentY,
                               currentZ),
                           new WindcallerKarrecNpcWaypointDefinition(
                               destinationX,
                               destinationY,
                               destinationZ)
                       };
            }

            var initialWaypoints = new WindcallerKarrecNpcWaypointDefinition[this.ScfuWaypoints.Count];
            for (int index = 0; index < initialWaypoints.Length; index++)
            {
                initialWaypoints[index] = this.ScfuWaypoints[index];
            }

            return initialWaypoints;
        }

        internal ReadOnlyCollection<WindcallerKarrecNpcPatrolSegment> PatrolSegments { get; private set; }

        internal ReadOnlyCollection<WindcallerKarrecNpcActiveNanoDefinition> ActiveNanos { get; private set; }

        internal bool HasPatrol
        {
            get { return this.PatrolSegments.Count > 0; }
        }

        internal string Evidence { get; private set; }
    }

    internal sealed class WindcallerKarrecNpcTextureDefinition
    {
        internal WindcallerKarrecNpcTextureDefinition(int place, int id, int unknown)
        {
            this.Place = place;
            this.Id = id;
            this.Unknown = unknown;
        }

        internal int Place { get; private set; }

        internal int Id { get; private set; }

        internal int Unknown { get; private set; }
    }

    internal sealed class WindcallerKarrecNpcMeshDefinition
    {
        internal WindcallerKarrecNpcMeshDefinition(int position, uint id, int overrideTextureId, int layer)
        {
            this.Position = position;
            this.Id = id;
            this.OverrideTextureId = overrideTextureId;
            this.Layer = layer;
        }

        internal int Position { get; private set; }

        internal uint Id { get; private set; }

        internal int OverrideTextureId { get; private set; }

        internal int Layer { get; private set; }
    }

    internal sealed class WindcallerKarrecNpcWaypointDefinition
    {
        internal WindcallerKarrecNpcWaypointDefinition(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }
    }

    internal sealed class WindcallerKarrecNpcPatrolSegment
    {
        internal WindcallerKarrecNpcPatrolSegment(
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

        internal double DelayAfterSeconds { get; private set; }

        internal float StartX { get; private set; }

        internal float StartY { get; private set; }

        internal float StartZ { get; private set; }

        internal float EndX { get; private set; }

        internal float EndY { get; private set; }

        internal float EndZ { get; private set; }

        internal byte MoveMode { get; private set; }
    }

    internal sealed class WindcallerKarrecNpcActiveNanoDefinition
    {
        internal WindcallerKarrecNpcActiveNanoDefinition(
            int nanoIdentityType,
            int nanoIdentityInstance,
            int nanoInstance,
            int time1,
            int time2)
        {
            this.NanoIdentityType = nanoIdentityType;
            this.NanoIdentityInstance = nanoIdentityInstance;
            this.NanoInstance = nanoInstance;
            this.Time1 = time1;
            this.Time2 = time2;
        }

        internal int NanoIdentityType { get; private set; }

        internal int NanoIdentityInstance { get; private set; }

        internal int NanoInstance { get; private set; }

        internal int Time1 { get; private set; }

        internal int Time2 { get; private set; }
    }
}
