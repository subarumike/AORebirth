namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    /// Captures 20260823-000659 / 20260823-103458: only spawns with PatrolCaptureInstance patrol.
    internal static class NascenceLifeStarterBridgePatrolRuntime
    {
        // Must match NPCController.WalkFollowSpeedPerSecond / EnemyBehaviorContract.MaxNpcFollowSpeedPerSecond
        // so DelayAfterSeconds expires when the FollowTarget motion actually reaches the end.
        private const double WalkSpeedPerSecond = 1.5d;
        private const double RunSpeedPerSecond = 6.0d;
        private const double SegmentArrivalSlackSeconds = 0.2d;
        private const double MinimumSegmentDelaySeconds = 0.75d;

        private static readonly Dictionary<string, PatrolRoute> RoutesByInstance =
            BuildRoutes();

        internal static bool TryApply(
            string patrolCaptureInstance,
            int playfieldId,
            float spawnX,
            float spawnY,
            float spawnZ,
            Character mob,
            NPCController controller)
        {
            if (playfieldId != 4310
                || mob == null
                || controller == null
                || string.IsNullOrWhiteSpace(patrolCaptureInstance))
            {
                return false;
            }

            PatrolRoute route;
            if (!RoutesByInstance.TryGetValue(patrolCaptureInstance, out route))
            {
                return false;
            }

            float[][] loop = BuildLoopWaypoints(route, spawnX, spawnY, spawnZ);
            NpcPatrolReplaySegment[] segments = BuildClosedLoop(loop, route.MoveMode);
            if (segments.Length == 0)
            {
                return false;
            }

            mob.Waypoints.Clear();
            // useRuntimeStart=true: never teleport to absolute segment StartXYZ mid-loop.
            // Fixed short delays + capturedStart caused the synchronized yank Mike reported.
            controller.SetCapturedPatrolReplaySegments(segments, true, false, false);
            controller.State = CharacterState.Patrolling;
            mob.DoNotDoTimers = false;
            controller.Walk();
            controller.StartPatrolling();
            return true;
        }

        private static Dictionary<string, PatrolRoute> BuildRoutes()
        {
            PatrolRoute[] routes =
                {
                    Route(
                        "7A1B4450",
                        24,
                        834.6104f,
                        31.6076f,
                        1167.0066f,
                        837.7926f,
                        32.0789f,
                        1166.3058f,
                        828.0011f,
                        30.3731f,
                        1169.5807f,
                        822.9438f,
                        30.6363f,
                        1166.6660f,
                        828.0011f,
                        30.3731f,
                        1169.5807f),
                    Route(
                        "7A1EFE40",
                        24,
                        819.4066f,
                        31.1555f,
                        1160.2098f,
                        820.2645f,
                        31.9079f,
                        1151.8766f,
                        821.2514f,
                        31.3181f,
                        1159.2791f,
                        811.0381f,
                        31.2955f,
                        1164.3765f,
                        821.2514f,
                        31.3181f,
                        1159.2791f),
                    Route(
                        "7A202AC7",
                        24,
                        790.8917f,
                        32.4100f,
                        1167.9335f,
                        792.9133f,
                        32.7654f,
                        1184.5261f,
                        793.9838f,
                        32.1124f,
                        1178.9496f,
                        792.5205f,
                        32.3319f,
                        1171.1520f,
                        790.2947f,
                        32.4100f,
                        1166.5192f,
                        792.5205f,
                        32.3319f,
                        1171.1520f,
                        793.9838f,
                        32.1124f,
                        1178.9496f),
                    Route(
                        "7A202B28",
                        24,
                        825.1705f,
                        29.8181f,
                        1170.4897f,
                        874.7349f,
                        31.8072f,
                        1127.9938f,
                        870.5892f,
                        32.3944f,
                        1131.8962f,
                        856.7491f,
                        32.4100f,
                        1139.4478f,
                        836.9616f,
                        32.4000f,
                        1157.3423f,
                        818.3820f,
                        28.8101f,
                        1177.3077f,
                        806.3998f,
                        29.4100f,
                        1224.2747f,
                        815.0111f,
                        31.6617f,
                        1236.1578f,
                        806.3998f,
                        29.4100f,
                        1224.2747f,
                        818.3820f,
                        28.8101f,
                        1177.3077f,
                        836.9616f,
                        32.4000f,
                        1157.3423f,
                        856.7491f,
                        32.4100f,
                        1139.4478f,
                        870.5892f,
                        32.3944f,
                        1131.8962f),
                    Route(
                        "7A202B2B",
                        24,
                        796.3040f,
                        31.7686f,
                        1175.6415f,
                        797.1176f,
                        31.6424f,
                        1183.3798f,
                        795.5468f,
                        31.8780f,
                        1176.4987f,
                        797.0338f,
                        32.1842f,
                        1173.6570f,
                        795.5468f,
                        31.8780f,
                        1176.4987f),
                    Route(
                        "7A202B2D",
                        24,
                        825.9318f,
                        29.7058f,
                        1180.6898f,
                        812.1759f,
                        29.1391f,
                        1177.6302f,
                        830.2565f,
                        31.1545f,
                        1181.5204f,
                        819.5839f,
                        28.8100f,
                        1181.8765f,
                        817.0764f,
                        29.0604f,
                        1188.5931f,
                        805.9973f,
                        29.4100f,
                        1193.3367f,
                        802.2364f,
                        30.5391f,
                        1184.5519f,
                        807.8224f,
                        29.7839f,
                        1177.8626f),
                    Route(
                        "7A202B50",
                        25,
                        799.5469f,
                        29.2789f,
                        1208.9022f,
                        828.2459f,
                        32.2000f,
                        1157.6675f,
                        826.5912f,
                        30.8591f,
                        1164.9307f,
                        832.6381f,
                        30.8014f,
                        1176.6951f,
                        831.9631f,
                        31.2614f,
                        1188.3795f,
                        825.2365f,
                        31.3955f,
                        1200.2180f,
                        815.7023f,
                        29.9654f,
                        1207.0614f,
                        796.0570f,
                        31.1108f,
                        1188.6044f,
                        797.8997f,
                        31.5251f,
                        1177.0745f,
                        798.8379f,
                        31.9317f,
                        1168.3507f,
                        799.9075f,
                        32.4100f,
                        1159.6807f,
                        801.0945f,
                        32.3589f,
                        1155.6594f,
                        808.3396f,
                        32.3591f,
                        1155.0531f,
                        815.2357f,
                        31.8100f,
                        1153.6084f,
                        824.1155f,
                        31.9976f,
                        1155.3746f,
                        815.2357f,
                        31.8100f,
                        1153.6084f,
                        808.3396f,
                        32.3591f,
                        1155.0531f,
                        801.0945f,
                        32.3589f,
                        1155.6594f,
                        799.9075f,
                        32.4100f,
                        1159.6807f,
                        798.8379f,
                        31.9317f,
                        1168.3507f,
                        797.8997f,
                        31.5251f,
                        1177.0745f,
                        796.0570f,
                        31.1108f,
                        1188.6044f,
                        798.0888f,
                        30.2967f,
                        1194.7561f,
                        797.4119f,
                        29.9612f,
                        1198.9137f,
                        799.8334f,
                        29.0542f,
                        1210.3721f,
                        832.6381f,
                        30.8014f,
                        1176.6951f,
                        826.5912f,
                        30.8591f,
                        1164.9307f),
                    // Nascence Spirit Hunter capture 20260823-103458
                    Route("7A19FD9E", 24, 857.9946f, 17.345f, 1435.7676f,
                        847.1978f, 17.6f, 1425.6728f,
                        855.9786f, 17.3307f, 1418.6598f,
                        857.435f, 17.3307f, 1430.6569f,
                        861.559f, 17.552f, 1439.49f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1B444F", 24, 853.9039f, 16.865f, 1340.2701f,
                        869.2632f, 21.9f, 1330.2902f,
                        877.1029f, 21.9f, 1338.6064f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1C3B42", 24, 877.6614f, 9.365f, 1369.1194f,
                        881.0114f, 9.3516f, 1364.9321f,
                        890.3619f, 9.604f, 1356.8365f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1C3B70", 24, 848.916f, 16.865f, 1341.5485f,
                        854.0075f, 16.8516f, 1342.8385f,
                        848.7213f, 16.8516f, 1341.5194f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1C3B73", 24, 839.4312f, 7.21f, 1370.654f,
                        832.5851f, 7.7122f, 1377.3065f,
                        822.3429f, 11.6284f, 1368.551f,
                        827.1f, 10.14f, 1381.3249f,
                        843.4018f, 9.8307f, 1397.8031f,
                        848.3501f, 9.8307f, 1400.7358f,
                        849.8079f, 9.8307f, 1398.9678f,
                        852.6795f, 7.2f, 1396.2991f,
                        852.4985f, 7.2f, 1391.3025f,
                        848.2312f, 7.6f, 1392.7837f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1C3B88", 24, 869.8517f, 9.365f, 1364.7692f,
                        851.0129f, 9.3516f, 1351.8605f,
                        865.7759f, 9.3516f, 1367.1587f,
                        876.7742f, 9.6047f, 1357.3304f,
                        865.7759f, 9.3516f, 1367.1587f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A1C3CA1", 24, 906.8674f, 16.865f, 1369.0242f,
                        901.4009f, 17.1f, 1376.3552f,
                        890.8144f, 16.8515f, 1384.1061f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A2260E0", 24, 840.1165f, 7.21f, 1370.1171f,
                        845.3004f, 7.2f, 1383.4739f,
                        855.9403f, 7.2f, 1392.1599f,
                        876.041f, 7.8307f, 1407.9128f,
                        883.4186f, 14.2f, 1410.3816f,
                        876.041f, 7.8307f, 1407.9128f,
                        855.9403f, 7.2f, 1392.1599f),
                    // Nascence Spirit Hunter capture 20260823-103458
                    Route("7A226146", 24, 850.3147f, 7.21f, 1376.6442f,
                        847.0833f, 7.21f, 1376.7822f,
                        852.5084f, 7.21f, 1384.4375f,
                        864.4174f, 7.21f, 1383.8623f,
                        881.6967f, 9.519f, 1387.0575f,
                        864.4174f, 7.21f, 1383.8623f,
                        852.5084f, 7.21f, 1384.4375f),
                    // Nascence Spirit Hunter capture 20260823-103458
                    Route("7A226153", 24, 862.5264f, 7.21f, 1376.4276f,
                        856.6473f, 7.2f, 1371.3376f,
                        838.8752f, 8.4061f, 1352.1805f),
                    // Swift Silvertail capture 20260823-103458
                    Route("7A22672D", 24, 798.6816f, 31.6182f, 1287.8506f,
                        792.4955f, 30.6843f, 1288.0269f,
                        763.9832f, 30.0125f, 1281.8191f,
                        748.2361f, 31.8417f, 1267.5527f,
                        737.1407f, 32.41f, 1267.7443f,
                        753.6835f, 31.81f, 1260.7007f,
                        770.2178f, 29.0773f, 1272.358f,
                        784.2844f, 29.4527f, 1277.1495f,
                        765.5314f, 31.3506f, 1252.4812f,
                        775.9058f, 30.34f, 1237.9885f,
                        787.5867f, 28.872f, 1229.0293f,
                        816.1999f, 32.4097f, 1255.504f,
                        812.6197f, 32.41f, 1288.2593f),
                    // Swift Silvertail capture 20260823-103458
                    Route("7A22672E", 24, 798.1287f, 29.9252f, 1253.266f,
                        795.7567f, 30.4142f, 1254.9429f,
                        802.5425f, 30.1728f, 1250.1455f),
                    // Swift Silvertail capture 20260823-103458
                    Route("7A226731", 24, 807.4224f, 32.0287f, 1265.9959f,
                        811.1464f, 32.282f, 1260.6874f,
                        806.9409f, 32.0852f, 1266.8936f),
                    // Swift Silvertail capture 20260823-103458
                    Route("7A226735", 24, 795.1619f, 31.0903f, 1276.5057f,
                        800.5549f, 31.81f, 1280.0952f,
                        793.5135f, 30.837f, 1275.4321f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A233A2E", 24, 865.7973f, 9.845f, 1420.8308f,
                        868.058f, 10.1095f, 1423.3637f,
                        850.3817f, 9.8307f, 1403.5577f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A233B6C", 24, 876.0099f, 9.365f, 1357.9469f,
                        865.7759f, 9.3516f, 1367.1587f,
                        851.0129f, 9.3516f, 1351.8605f),
                    // Cascading Spirit capture 20260823-103458
                    Route("7A233B75", 24, 854.4845f, 16.865f, 1342.1422f,
                        854.0075f, 16.8516f, 1342.8385f,
                        848.7213f, 16.8516f, 1341.5194f),
                    // Nascence Spirit Hunter capture 20260823-103458
                    Route("7A233B7B", 24, 842.0331f, 7.2769f, 1355.5825f,
                        856.6473f, 7.2f, 1371.3376f,
                        868.0376f, 7.1998f, 1380.8608f),
                    // Nascence Spirit Hunter capture 20260823-103458
                    Route("7A233B7D", 24, 849.6416f, 7.21f, 1380.4756f,
                        847.0833f, 7.21f, 1376.7822f,
                        852.5084f, 7.21f, 1384.4375f),
                };

            var map = new Dictionary<string, PatrolRoute>(StringComparer.OrdinalIgnoreCase);
            foreach (PatrolRoute route in routes)
            {
                map[route.CaptureInstance] = route;
            }

            return map;
        }

        private static float[][] BuildLoopWaypoints(PatrolRoute route, float spawnX, float spawnY, float spawnZ)
        {
            float offsetX = spawnX - route.SpawnX;
            float offsetY = spawnY - route.SpawnY;
            float offsetZ = spawnZ - route.SpawnZ;
            var points = new List<float[]>(route.CyclePoints.Length + 1)
                         {
                             new[] { spawnX, spawnY, spawnZ },
                         };
            foreach (float[] point in route.CyclePoints)
            {
                points.Add(
                    new[]
                    {
                        point[0] + offsetX,
                        point[1] + offsetY,
                        point[2] + offsetZ,
                    });
            }

            return points.ToArray();
        }

        private static NpcPatrolReplaySegment[] BuildClosedLoop(float[][] waypoints, byte moveMode)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return new NpcPatrolReplaySegment[0];
            }

            double speedPerSecond = moveMode == EnemyBehaviorContract.RunMoveMode
                                        ? RunSpeedPerSecond
                                        : WalkSpeedPerSecond;
            var segments = new NpcPatrolReplaySegment[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                float[] start = waypoints[i];
                float[] end = waypoints[(i + 1) % waypoints.Length];
                segments[i] = new NpcPatrolReplaySegment(
                    EstimateTravelDelaySeconds(start, end, speedPerSecond),
                    start[0],
                    start[1],
                    start[2],
                    end[0],
                    end[1],
                    end[2],
                    moveMode);
            }

            return segments;
        }

        private static double EstimateTravelDelaySeconds(float[] start, float[] end, double speedPerSecond)
        {
            double dx = end[0] - start[0];
            double dy = end[1] - start[1];
            double dz = end[2] - start[2];
            double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            double travelSeconds = distance / Math.Max(0.1d, speedPerSecond);
            return Math.Max(MinimumSegmentDelaySeconds, travelSeconds + SegmentArrivalSlackSeconds);
        }

        private static PatrolRoute Route(string captureInstance, byte moveMode, float spawnX, float spawnY, float spawnZ, params float[] cycle)
        {
            var cyclePoints = new float[cycle.Length / 3][];
            for (int i = 0; i < cyclePoints.Length; i++)
            {
                cyclePoints[i] = new[] { cycle[i * 3], cycle[(i * 3) + 1], cycle[(i * 3) + 2] };
            }

            return new PatrolRoute
                   {
                       CaptureInstance = captureInstance,
                       MoveMode = moveMode,
                       SpawnX = spawnX,
                       SpawnY = spawnY,
                       SpawnZ = spawnZ,
                       CyclePoints = cyclePoints,
                   };
        }

        private sealed class PatrolRoute
        {
            internal string CaptureInstance { get; set; }

            internal byte MoveMode { get; set; }

            internal float SpawnX { get; set; }

            internal float SpawnY { get; set; }

            internal float SpawnZ { get; set; }

            internal float[][] CyclePoints { get; set; }
        }
    }
}
