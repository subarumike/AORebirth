namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260825-202932 + 20260826-051307 (PF 4310) outdoor hostiles:
    /// ExtTex, patrol, fight contracts, spawn stats.
    /// </summary>
    internal static class NascenceFrontierOutdoorMobRuntime
    {
        // Tight match: only spawns near a capture route endpoint get outdoor patrol (avoids 3–5 mobs sharing one route).
        private const float PatrolMatchRadiusMeters = 12f;

        private const int PredatorSabreCharacterFlags = 268980737;
        private const int DefaultAnimalCharacterFlags = 268964353;
        private const int TextureSabreStriker = 235169;
        private const int TextureSabreStalking = 208942;
        // Capture 20260825-202932 Deadly Predator 7A2ED7B6 ExtTex sabre self → 235170.
        private const int TextureSabreDeadly = 235170;
        private const int TextureLow2 = 208969;
        private const int TextureHiathlin = 234986;
        private const int TextureOmathon = 234987;
        private const int TextureSpinetooth = 302615;
        private const int TextureWeaver = 235226;
        private const int TextureCrippler = 209280;

        private sealed class PatrolRoute
        {
            public PatrolRoute(string name, float x1, float y1, float z1, float x2, float y2, float z2)
            {
                Name = name;
                X1 = x1;
                Y1 = y1;
                Z1 = z1;
                X2 = x2;
                Y2 = y2;
                Z2 = z2;
            }

            public string Name;
            public float X1;
            public float Y1;
            public float Z1;
            public float X2;
            public float Y2;
            public float Z2;
        }

        private static readonly PatrolRoute[] PatrolRoutes =
        {
            new PatrolRoute("Hiathlin", 785.971313f, 31.210001f, 1739.97461f, 790.208252f, 31.210001f, 1731.84473f),
            new PatrolRoute("Hiathlin", 792.11438f, 31.210001f, 1756.95703f, 790.121948f, 31.210001f, 1750.71814f),
            new PatrolRoute("Hiathlin", 798.418152f, 31.210001f, 1725.84546f, 791.719666f, 31.210001f, 1732.65491f),
            new PatrolRoute("Hiathlin", 811.693237f, 31.210001f, 1732.05457f, 804.681946f, 31.210001f, 1728.91113f),
            new PatrolRoute("Hiathlin", 812.571167f, 26.9156475f, 1787.34265f, 775.703125f, 31.210001f, 1774.56592f),
            new PatrolRoute("Hiathlin", 816.271729f, 28.8356628f, 1751.61377f, 811.101868f, 28.7246838f, 1761.46692f),
            new PatrolRoute("Hiathlin", 837.538635f, 26.4540386f, 1759.73474f, 824.579163f, 26.4100018f, 1782.94556f),
            new PatrolRoute("Hiathlin", 844.497314f, 31.210001f, 1734.31824f, 844.345276f, 30.7487717f, 1746.5592f),
            new PatrolRoute("Malah-Ana", 953.904358f, 29.4471722f, 1650.903f, 953.5756f, 29.7805882f, 1653.62183f),
            new PatrolRoute("Malah-Ana", 953.483948f, 29.6415386f, 1646.11768f, 953.1496f, 30.0926819f, 1641.2533f),
            new PatrolRoute("Malah-Ana", 953.2916f, 30.22936f, 1640.03918f, 954.047241f, 29.3958282f, 1651.13049f),
            new PatrolRoute("Malah-Ana", 972.107361f, 31.210001f, 1613.91943f, 960.688232f, 31.210001f, 1612.26245f),
            new PatrolRoute("Malah-Ana", 961.0456f, 31.210001f, 1608.04333f, 954.654846f, 31.210001f, 1610.90051f),
            new PatrolRoute("Malah-Ana", 958.963745f, 32.7812958f, 1600.00964f, 961.7092f, 33.431f, 1602.00757f),
            new PatrolRoute("Malah-Ana", 989.8531f, 31.210001f, 1600.02466f, 989.02594f, 31.6f, 1609.53149f),
            new PatrolRoute("Malah-Ana", 950.428467f, 31.4993076f, 1622.09961f, 951.156738f, 31.2861f, 1616.50732f),
            new PatrolRoute("Malah-Ana", 989.1882f, 31.210001f, 1608.50024f, 1000.91364f, 31.016964f, 1609.22021f),
            new PatrolRoute("Malah-Ana", 954.775757f, 31.210001f, 1600.03662f, 954.654846f, 31.210001f, 1610.90051f),
            new PatrolRoute("Malah-Ana", 953.290039f, 30.2280426f, 1640.05115f, 953.1496f, 30.0926819f, 1641.2533f),
            new PatrolRoute("Malah-Ana", 960.0035f, 29.589098f, 1634.85913f, 961.2632f, 29.4835358f, 1635.50977f),
            new PatrolRoute("Malah-Ana", 960.0063f, 31.210001f, 1608.15f, 965.2652f, 31.6002274f, 1606.18335f),
            new PatrolRoute("Malah-Ana", 960.0046f, 31.210001f, 1612.72314f, 972.195251f, 31.210001f, 1613.324f),
            new PatrolRoute("Malah-Ana", 960.0528f, 33.2276649f, 1600.7627f, 961.7092f, 33.431f, 1602.00757f),
            new PatrolRoute("Malah-Ana", 960.0055f, 31.210001f, 1612.7561f, 972.195251f, 31.210001f, 1613.324f),
            new PatrolRoute("Malah-Ana", 960.03656f, 33.21726f, 1600.74133f, 961.7092f, 33.431f, 1602.00757f),
            new PatrolRoute("Malah-Ana", 960.05426f, 31.210001f, 1612.79883f, 972.195251f, 31.210001f, 1613.324f),
            new PatrolRoute("Malah-Ana", 954.3305f, 31.210001f, 1603.42773f, 953.9687f, 31.210001f, 1600.35986f),
            new PatrolRoute("Malah-Ana", 950.6469f, 31.7454758f, 1619.5415f, 949.3924f, 31.6011429f, 1622.4646f),
            new PatrolRoute("Malah-Ana", 953.4421f, 30.1946087f, 1639.97f, 953.69165f, 31.3126373f, 1636.24377f),
            new PatrolRoute("Malah-Ana", 953.6636f, 29.5194073f, 1651.89514f, 953.1496f, 30.0926819f, 1641.2533f),
            new PatrolRoute("Malah-Ana", 959.9914f, 31.210001f, 1613.09106f, 951.156738f, 31.2861f, 1616.50732f),
            new PatrolRoute("Malah-Ana", 959.967f, 30.0191936f, 1631.05371f, 958.0838f, 30.0099983f, 1632.64709f),
            new PatrolRoute("Malah-Ana", 959.982361f, 33.09505f, 1600.36743f, 956.254944f, 31.210001f, 1598.38074f),
            new PatrolRoute("Malah-Ana", 954.9097f, 31.210001f, 1600.04138f, 954.654846f, 31.210001f, 1610.90051f),
            new PatrolRoute("Predator Striker", 808.9715f, 31.9617233f, 1640.9292f, 809.9836f, 31.9135113f, 1643.2771f),
            new PatrolRoute("Predator Striker", 809.067566f, 31.96945f, 1641.1521f, 809.9836f, 31.9135113f, 1643.2771f),
            new PatrolRoute("Predator Striker", 814.6472f, 32.2130775f, 1648.0271f, 816.554565f, 32.172184f, 1650.14f),
            // Capture 20260827-221909 Crippler cave mouth PF4311 FollowTarget NpcPath.
            new PatrolRoute("Crippler of Growth", 535.7803f, 55.8844f, 1739.284f, 539.498535f, 55.8814468f, 1743.97485f),
            new PatrolRoute("Crippler of Growth", 536.4755f, 53.59499f, 1730.014f, 534.471069f, 56.1467514f, 1736.71606f),
            new PatrolRoute("Crippler of Growth", 556.2581f, 47.41654f, 1720.475f, 573.944153f, 45.4753075f, 1717.77551f),
            new PatrolRoute("Crippler of Growth", 556.2581f, 47.41654f, 1720.475f, 547.794006f, 49.7163239f, 1721.1687f),
            // Capture 20260830-110744 PF4311 Crippler SCFU HasWaypoints (first 2-wp per identity, 8m cells).
            new PatrolRoute("Crippler of Growth", 519.9849f, 72.5213547f, 1793.49622f, 518.8054f, 72.3678055f, 1791.84619f),
            new PatrolRoute("Crippler of Growth", 548.7474f, 56.9037437f, 1753.30859f, 551.437866f, 56.45418f, 1751.42444f),
            new PatrolRoute("Crippler of Growth", 549.21f, 51.284f, 1728.885f, 546.456055f, 51.90901f, 1730.22473f),
            new PatrolRoute("Crippler of Growth", 559.9603f, 46.3379974f, 1720.36438f, 556.2111f, 47.40425f, 1720.38391f),
            new PatrolRoute("Crippler of Growth", 570.2458f, 12.8625736f, 1621.76489f, 568.385254f, 12.89774f, 1643.0813f),
            new PatrolRoute("Crippler of Growth", 594.9996f, 12.1943722f, 1624.39673f, 591.7411f, 12.1732635f, 1626.7511f),
            new PatrolRoute("Crippler of Growth", 618.841858f, 12.201931f, 1576.98572f, 615.368042f, 11.8878174f, 1576.19031f),
            new PatrolRoute("Crippler of Growth", 640.703735f, 11.896451f, 1532.5929f, 639.4386f, 11.8696041f, 1533.97107f),
            new PatrolRoute("Crippler of Growth", 597.2117f, 15.1940994f, 1480.39087f, 618.1115f, 15.0082884f, 1479.99231f),
            new PatrolRoute("Crippler of Growth", 565.2699f, 12.2437754f, 1457.3717f, 562.9642f, 12.2254906f, 1459.24109f),
            new PatrolRoute("Crippler of Growth", 669.313538f, 12.6606627f, 1440.02661f, 672.9868f, 12.7774277f, 1442.13708f),
            new PatrolRoute("Crippler of Growth", 699.1533f, 12.01f, 1367.40857f, 700.486633f, 11.7484989f, 1369.25671f),
            new PatrolRoute("Crippler of Growth", 649.842834f, 11.9051f, 1344.40759f, 651.6679f, 11.962431f, 1360.26147f),
            new PatrolRoute("Crippler of Growth", 612.919556f, 12.9023609f, 1312.0636f, 605.7775f, 11.8846989f, 1311.19617f),
            new PatrolRoute("Crippler of Growth", 643.2199f, 12.5583963f, 1283.32568f, 634.0827f, 11.90892f, 1282.4165f),
            new PatrolRoute("Crippler of Growth", 582.2088f, 12.2401953f, 1218.15759f, 592.351f, 12.228487f, 1233.4801f),
            new PatrolRoute("Crippler of Growth", 640.7513f, 12.5981522f, 1188.31042f, 627.340149f, 12.3152218f, 1178.52441f),
            new PatrolRoute("Slivering Chimera", 816.4902f, 31.210001f, 1657.716f, 818.321f, 30.8617535f, 1658.06677f),
            new PatrolRoute("Slivering Chimera", 816.7252f, 31.210001f, 1657.761f, 818.321f, 30.8617535f, 1658.06677f),
            new PatrolRoute("Slivering Chimera", 810.2743f, 31.2691441f, 1655.634f, 802.202637f, 30.3403969f, 1649.725f),
            // Capture 20260825-202932 boss-area Slivering Chimera 7A2ED7C1 / 7A2ED7C4.
            new PatrolRoute("Slivering Chimera", 786.0128f, 29.5924f, 1602.8093f, 795.7056f, 31.7217f, 1606.3458f),
            new PatrolRoute("Slivering Chimera", 780.8754f, 28.7842f, 1606.6899f, 787.9379f, 29.4007f, 1615.101f),
            new PatrolRoute("Slivering Chimera", 775.5828f, 27.894f, 1602.1064f, 781.5366f, 28.4405f, 1616.5199f),
            new PatrolRoute("Slivering Chimera", 770.5739f, 27.9961f, 1592.5359f, 757.8286f, 25.4843f, 1582.9515f),
            // Capture 20260825-202932 Corrupting Imp 7A2ED7B9.
            new PatrolRoute("Corrupting Imp", 775.64f, 31.80f, 1560.05f, 779.44f, 31.34f, 1565.29f),
            new PatrolRoute("Corrupting Imp", 783.077f, 30.4715f, 1572.797f, 784.2648f, 30.6895f, 1580.146f),
            new PatrolRoute("Corrupting Imp", 770.9235f, 27.1091f, 1585.7374f, 767.459f, 27.5289f, 1591.8265f),
            new PatrolRoute("Spinetooth Hatchling", 980.916138f, 30.1901321f, 1656.24475f, 984.4677f, 30.0100021f, 1644.71875f),
            new PatrolRoute("Spinetooth Hatchling", 978.669067f, 31.210001f, 1604.36829f, 979.681763f, 31.210001f, 1610.21423f),
            new PatrolRoute("Spinetooth Hatchling", 993.680542f, 31.210001f, 1602.7417f, 989.055542f, 31.210001f, 1593.29919f),
            new PatrolRoute("Spinetooth Hatchling", 970.9057f, 29.7115765f, 1630.01782f, 977.4646f, 30.0100021f, 1631.93445f),
            new PatrolRoute("Spinetooth Hatchling", 1019.60406f, 29.09971f, 1636.85632f, 1023.84265f, 28.9718666f, 1642.6062f),
            new PatrolRoute("Spinetooth Hatchling", 978.405151f, 31.210001f, 1600.03638f, 977.6372f, 31.210001f, 1601.78931f),
            new PatrolRoute("Spinetooth Hatchling", 1021.9505f, 28.7891273f, 1640.04016f, 1023.84265f, 28.9718666f, 1642.6062f),
            new PatrolRoute("Spinetooth Hatchling", 983.2566f, 29.9044876f, 1640.01111f, 984.4677f, 30.0100021f, 1644.71875f),
            new PatrolRoute("Spinetooth Hatchling", 969.3855f, 29.7774582f, 1629.57861f, 977.4646f, 30.0100021f, 1631.93445f),
            new PatrolRoute("Spinetooth Hatchling", 980.903931f, 31.210001f, 1609.852f, 996.447144f, 31.59922f, 1616.338f),
            new PatrolRoute("Spinetooth Hatchling", 1009.29047f, 29.9957f, 1617.42578f, 1009.03595f, 28.9053726f, 1624.40015f),
            new PatrolRoute("Spinetooth Hatchling", 986.5577f, 31.210001f, 1593.686f, 983.291748f, 31.210001f, 1593.23669f),
            new PatrolRoute("Spinetooth Hatchling", 983.4753f, 29.93729f, 1639.943f, 983.046143f, 29.4477577f, 1636.25171f),
            new PatrolRoute("Spinetooth Hatchling", 981.531067f, 29.9878273f, 1654.28113f, 984.4677f, 30.0100021f, 1644.71875f),
            new PatrolRoute("Stalking Predator", 887.2837f, 29.4100018f, 1680.0105f, 896.5546f, 29.752821f, 1682.26917f),
            new PatrolRoute("Stalking Predator", 840.012939f, 31.9863129f, 1686.54407f, 850.7941f, 31.465416f, 1685.70276f),
            new PatrolRoute("Stalking Predator", 809.2899f, 31.4978065f, 1661.89038f, 808.7023f, 32.26143f, 1667.71179f),
            new PatrolRoute("Stalking Predator", 846.4643f, 31.34169f, 1673.62634f, 850.412231f, 31.98159f, 1670.85608f),
            new PatrolRoute("Stalking Predator", 882.15564f, 29.4100018f, 1666.74866f, 879.4419f, 29.5774326f, 1663.72229f),
            new PatrolRoute("Stalking Predator", 809.265747f, 31.5337315f, 1662.12988f, 808.7023f, 32.26143f, 1667.71179f),
            new PatrolRoute("Stalking Predator", 846.662231f, 31.39189f, 1673.48962f, 850.412231f, 31.98159f, 1670.85608f),
            new PatrolRoute("Stalking Predator", 881.9937f, 29.4100018f, 1666.56812f, 879.4419f, 29.5774326f, 1663.72229f),
            new PatrolRoute("Stalking Predator", 812.010864f, 32.02396f, 1669.39807f, 814.824463f, 31.9129982f, 1671.57788f),
            new PatrolRoute("Stalking Predator", 851.8395f, 31.2462921f, 1675.78638f, 852.9829f, 31.0625648f, 1678.47156f),
            new PatrolRoute("Stalking Predator", 878.943054f, 29.4100018f, 1670.0072f, 875.9099f, 29.4235153f, 1687.75049f),
            new PatrolRoute("Stalking Predator", 888.0824f, 29.4100018f, 1679.99609f, 872.291443f, 29.9662857f, 1676.51807f),
            new PatrolRoute("Stalking Predator", 886.2302f, 29.4100018f, 1679.58813f, 872.291443f, 29.9662857f, 1676.51807f),
            new PatrolRoute("Stalking Predator", 876.909363f, 29.4100018f, 1677.48389f, 896.5546f, 29.752821f, 1682.26917f),
            new PatrolRoute("Stalking Predator", 852.4525f, 31.1463623f, 1677.04272f, 850.412231f, 31.98159f, 1670.85608f),
            new PatrolRoute("Stalking Predator", 840.038f, 32.00219f, 1686.61719f, 850.7941f, 31.465416f, 1685.70276f),
            new PatrolRoute("Weaver of Malice", 959.9394f, 29.2580242f, 1702.86963f, 953.4598f, 29.7910347f, 1699.86731f),
            new PatrolRoute("Weaver of Malice", 975.582947f, 30.61f, 1672.53979f, 978.7843f, 29.8635712f, 1657.4552f),
            new PatrolRoute("Weaver of Malice", 969.4239f, 30.0415154f, 1710.74622f, 974.592346f, 31.3788185f, 1714.66467f),
            new PatrolRoute("Weaver of Malice", 985.0206f, 30.7188854f, 1707.66528f, 991.7959f, 29.6381321f, 1701.52087f),
            new PatrolRoute("Weaver of Malice", 978.88855f, 29.32764f, 1695.42261f, 975.403259f, 29.32049f, 1690.90808f),
            new PatrolRoute("Weaver of Malice", 961.835144f, 28.8214264f, 1687.95215f, 959.4602f, 29.1909161f, 1685.46057f),
            new PatrolRoute("Weaver of Malice", 1022.70258f, 30.1398411f, 1663.38049f, 1029.83069f, 30.284605f, 1657.35974f),
            new PatrolRoute("Weaver of Malice", 1006.23022f, 29.9484062f, 1616.439f, 1008.51587f, 30.19697f, 1614.75354f),
            new PatrolRoute("Weaver of Malice", 1026.24353f, 30.01253f, 1659.98853f, 1021.63403f, 29.7977638f, 1663.317f),
            new PatrolRoute("Weaver of Malice", 1029.13806f, 30.1867161f, 1644.72827f, 1024.24951f, 29.54336f, 1649.64587f),
            new PatrolRoute("Weaver of Malice", 1035.14221f, 31.08734f, 1640.0127f, 1029.97278f, 30.3059177f, 1643.57813f),
            new PatrolRoute("Weaver of Malice", 1035.35083f, 31.1186314f, 1640.00562f, 1029.97278f, 30.3059177f, 1643.57813f),
            new PatrolRoute("Weaver of Malice", 1021.41266f, 31.210001f, 1617.97571f, 1022.6897f, 31.6f, 1618.22314f),
            new PatrolRoute("Weaver of Malice", 1017.67676f, 30.2675114f, 1679.94653f, 1020.57483f, 30.6962261f, 1672.876f),
            new PatrolRoute("Weaver of Malice", 1017.68848f, 30.26927f, 1679.995f, 1020.57483f, 30.6962261f, 1672.876f),
            new PatrolRoute("Weaver of Malice", 1035.98828f, 31.210001f, 1639.98792f, 1037.65955f, 31.210001f, 1638.92249f),
            new PatrolRoute("Weaver of Malice", 992.905151f, 29.8784676f, 1663.80115f, 989.2327f, 30.3135357f, 1667.511f),
            new PatrolRoute("Weaver of Malice", 1025.19f, 30.1266766f, 1660.74951f, 1021.63403f, 29.7977638f, 1663.317f),
            new PatrolRoute("Weaver of Malice", 1011.27649f, 30.4967232f, 1616.07166f, 1022.6897f, 31.6f, 1618.22314f),
            new PatrolRoute("Weaver of Malice", 1020.63788f, 30.71168f, 1672.10791f, 1014.67047f, 29.8105717f, 1685.68628f),
            new PatrolRoute("Weaver of Malice", 1031.59f, 30.5545f, 1642.4624f, 1029.97278f, 30.3059177f, 1643.57813f),
            new PatrolRoute("Weaver of Malice", 978.5595f, 30.5542259f, 1709.02869f, 977.135132f, 30.3941021f, 1709.42554f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 898.0914f, 30.9324684f, 1643.98157f, 901.0027f, 31.210001f, 1656.83044f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 898.281067f, 30.9581585f, 1644.802f, 901.0027f, 31.210001f, 1656.83044f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 840.084f, 28.8205566f, 1799.94641f, 851.9705f, 28.0322056f, 1790.8147f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 898.933f, 31.210001f, 1641.84644f, 900.8802f, 31.210001f, 1625.27075f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 895.9813f, 31.210001f, 1600.05334f, 900.4155f, 31.210001f, 1607.88647f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 899.0083f, 31.210001f, 1640.50415f, 898.8312f, 31.210001f, 1641.94336f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 884.7313f, 30.3005257f, 1606.61157f, 879.6836f, 30.6574631f, 1611.9729f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 880.0345f, 30.9042511f, 1613.93335f, 885.865234f, 29.7501564f, 1625.7323f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 885.1031f, 29.9786358f, 1624.23743f, 885.865234f, 29.7501564f, 1625.7323f),
            new PatrolRoute("Yuttos Nascence Geosurvey Dog", 895.8661f, 31.1818218f, 1600.05164f, 900.4155f, 31.210001f, 1607.88647f),
        };

        // Capture 20260826-160734 (folder resource 4676 / playfield 4310) MoveMode=24 PathInfo pairs.
        // Crippler→Demonic path. Excludes Hwall* and Spinetooth* (Mike: later).
        private static readonly PatrolRoute[] Garden160734PatrolRoutes =
        {
            new PatrolRoute("Barking Chimera", 810.740f, 29.316f, 1177.945f, 830.257f, 31.155f, 1181.520f),
            new PatrolRoute("Barking Chimera", 825.517f, 33.010f, 1146.048f, 841.762f, 32.410f, 1142.206f),
            new PatrolRoute("Barking Chimera", 789.936f, 31.561f, 1195.725f, 796.934f, 31.674f, 1181.981f),
            new PatrolRoute("Barking Chimera", 833.496f, 32.040f, 1123.685f, 836.286f, 32.800f, 1137.332f),
            new PatrolRoute("Barking Chimera", 805.997f, 29.410f, 1193.337f, 817.752f, 29.079f, 1187.273f),
            new PatrolRoute("Barking Chimera", 840.355f, 32.410f, 1142.535f, 849.253f, 32.410f, 1132.966f),
            new PatrolRoute("Barking Chimera", 802.094f, 31.435f, 1127.135f, 814.284f, 31.666f, 1122.553f),
            new PatrolRoute("Barking Chimera", 811.038f, 31.295f, 1164.376f, 821.208f, 31.547f, 1157.781f),
            new PatrolRoute("Cascading Spirit", 855.940f, 7.200f, 1392.160f, 877.364f, 8.918f, 1408.368f),
            new PatrolRoute("Cascading Spirit", 827.859f, 9.064f, 1365.912f, 846.332f, 7.210f, 1384.312f),
            new PatrolRoute("Cascading Spirit", 850.382f, 9.831f, 1403.558f, 867.300f, 9.845f, 1422.514f),
            new PatrolRoute("Cascading Spirit", 826.669f, 10.245f, 1380.131f, 843.402f, 9.831f, 1397.803f),
            new PatrolRoute("Cascading Spirit", 832.585f, 7.712f, 1377.307f, 849.678f, 7.210f, 1392.532f),
            new PatrolRoute("Cascading Spirit", 851.013f, 9.352f, 1351.860f, 866.879f, 9.365f, 1366.171f),
            new PatrolRoute("Cascading Spirit", 870.699f, 9.352f, 1374.640f, 882.089f, 9.365f, 1363.999f),
            new PatrolRoute("Cascading Spirit", 822.343f, 11.628f, 1368.551f, 833.600f, 7.574f, 1378.252f),
            new PatrolRoute("Corrupting Imp", 745.651f, 32.064f, 1525.956f, 765.632f, 32.046f, 1531.837f),
            new PatrolRoute("Corrupting Imp", 768.049f, 32.224f, 1537.269f, 778.022f, 31.820f, 1553.921f),
            new PatrolRoute("Corrupting Imp", 694.463f, 32.406f, 1582.439f, 697.425f, 31.810f, 1601.143f),
            new PatrolRoute("Corrupting Imp", 750.036f, 31.810f, 1538.611f, 764.763f, 31.696f, 1550.195f),
            new PatrolRoute("Corrupting Imp", 764.763f, 31.696f, 1550.195f, 776.619f, 31.663f, 1561.148f),
            new PatrolRoute("Corrupting Imp", 770.218f, 27.196f, 1586.982f, 784.265f, 30.689f, 1580.146f),
            new PatrolRoute("Corrupting Imp", 756.742f, 26.660f, 1609.623f, 768.478f, 27.252f, 1598.866f),
            new PatrolRoute("Corrupting Imp", 705.066f, 31.654f, 1607.894f, 718.824f, 33.220f, 1612.698f),
            new PatrolRoute("Disease-Ridden Rafter", 698.636f, 30.010f, 1359.363f, 733.338f, 32.310f, 1354.979f),
            new PatrolRoute("Disease-Ridden Rafter", 697.750f, 32.343f, 1373.762f, 713.672f, 32.497f, 1368.580f),
            new PatrolRoute("Disease-Ridden Rafter", 712.378f, 32.473f, 1368.996f, 726.385f, 32.941f, 1361.153f),
            new PatrolRoute("Disease-Ridden Rafter", 674.007f, 30.795f, 1365.633f, 686.633f, 30.010f, 1357.219f),
            new PatrolRoute("Disease-Ridden Rafter", 688.368f, 32.396f, 1379.156f, 700.311f, 32.410f, 1372.621f),
            new PatrolRoute("Disease-Ridden Rafter", 725.181f, 32.868f, 1361.833f, 734.621f, 32.823f, 1356.132f),
            new PatrolRoute("Disease-Ridden Rafter", 684.058f, 30.010f, 1358.274f, 694.412f, 30.010f, 1355.675f),
            new PatrolRoute("Disease-Ridden Rafter", 733.358f, 32.754f, 1356.897f, 742.040f, 32.440f, 1352.198f),
            new PatrolRoute("Hai-Tempterus", 990.984f, 31.774f, 1231.224f, 999.211f, 31.098f, 1263.571f),
            new PatrolRoute("Hai-Tempterus", 894.509f, 31.267f, 1270.888f, 921.140f, 31.176f, 1284.857f),
            new PatrolRoute("Hai-Tempterus", 912.930f, 32.507f, 1199.380f, 940.174f, 32.410f, 1198.272f),
            new PatrolRoute("Hai-Tempterus", 989.706f, 30.272f, 1248.109f, 993.992f, 29.709f, 1273.231f),
            new PatrolRoute("Hai-Tempterus", 973.784f, 31.210f, 1215.306f, 991.287f, 31.709f, 1232.448f),
            new PatrolRoute("Hai-Tempterus", 873.369f, 32.205f, 1247.411f, 880.193f, 31.810f, 1270.163f),
            new PatrolRoute("Hai-Tempterus", 1012.224f, 33.740f, 1259.360f, 1013.781f, 36.362f, 1238.263f),
            new PatrolRoute("Hai-Tempterus", 919.893f, 31.236f, 1284.176f, 938.351f, 30.929f, 1294.196f),
            new PatrolRoute("Malah-Ana", 959.635f, 31.210f, 1612.829f, 972.195f, 31.210f, 1613.324f),
            new PatrolRoute("Malah-Ana", 953.315f, 30.256f, 1639.811f, 954.047f, 29.396f, 1651.130f),
            new PatrolRoute("Malah-Ana", 950.857f, 31.496f, 1617.881f, 960.688f, 31.210f, 1612.262f),
            new PatrolRoute("Malah-Ana", 954.655f, 31.210f, 1610.901f, 955.249f, 31.210f, 1599.699f),
            new PatrolRoute("Malah-Ana", 954.663f, 31.210f, 1609.594f, 965.265f, 31.600f, 1606.183f),
            new PatrolRoute("Malah-Ana", 949.392f, 31.601f, 1622.465f, 952.425f, 31.210f, 1616.010f),
            new PatrolRoute("Malah-Ana", 953.225f, 30.071f, 1642.507f, 953.692f, 31.313f, 1636.244f),
            new PatrolRoute("Malah-Ana", 949.937f, 31.637f, 1621.180f, 955.072f, 31.701f, 1621.642f),
            new PatrolRoute("Nascence Spirit Hunter", 838.875f, 8.406f, 1352.181f, 857.724f, 7.210f, 1372.238f),
            new PatrolRoute("Nascence Spirit Hunter", 832.088f, 8.397f, 1359.323f, 848.013f, 7.210f, 1377.925f),
            new PatrolRoute("Nascence Spirit Hunter", 862.958f, 7.210f, 1383.835f, 881.697f, 9.519f, 1387.057f),
            new PatrolRoute("Nascence Spirit Hunter", 847.198f, 17.600f, 1425.673f, 861.054f, 17.345f, 1438.346f),
            new PatrolRoute("Nascence Spirit Hunter", 855.695f, 7.210f, 1370.311f, 868.038f, 7.200f, 1380.861f),
            new PatrolRoute("Nascence Spirit Hunter", 852.508f, 7.210f, 1384.438f, 865.759f, 7.210f, 1384.119f),
            new PatrolRoute("Nascence Spirit Hunter", 855.013f, 17.345f, 1419.562f, 857.435f, 17.331f, 1430.657f),
            new PatrolRoute("Nascence Spirit Hunter", 857.184f, 17.345f, 1429.222f, 861.559f, 17.552f, 1439.490f),
            new PatrolRoute("Papageno", 680.601f, 30.610f, 1346.179f, 688.604f, 32.410f, 1320.364f),
            new PatrolRoute("Papageno", 676.293f, 30.610f, 1369.820f, 680.798f, 30.610f, 1344.965f),
            new PatrolRoute("Papageno", 655.784f, 31.909f, 1271.341f, 679.893f, 32.410f, 1275.729f),
            new PatrolRoute("Papageno", 682.718f, 32.410f, 1288.307f, 685.594f, 32.410f, 1309.977f),
            new PatrolRoute("Papageno", 657.927f, 29.410f, 1341.206f, 670.078f, 29.145f, 1330.999f),
            new PatrolRoute("Papageno", 679.590f, 32.410f, 1274.330f, 682.907f, 32.410f, 1289.753f),
            new PatrolRoute("Papageno", 654.338f, 24.271f, 1299.158f, 663.236f, 24.400f, 1311.641f),
            new PatrolRoute("Papageno", 651.428f, 31.900f, 1371.822f, 666.123f, 30.892f, 1375.393f),
            new PatrolRoute("Predator Striker", 771.407f, 30.603f, 1627.922f, 791.902f, 29.960f, 1624.237f),
            new PatrolRoute("Predator Striker", 802.856f, 31.210f, 1626.312f, 809.984f, 31.914f, 1643.277f),
            new PatrolRoute("Predator Striker", 746.083f, 32.410f, 1310.040f, 759.320f, 31.316f, 1321.058f),
            new PatrolRoute("Predator Striker", 770.601f, 28.030f, 1318.049f, 783.861f, 26.544f, 1307.581f),
            new PatrolRoute("Predator Striker", 788.426f, 30.674f, 1597.134f, 797.140f, 31.987f, 1610.515f),
            new PatrolRoute("Predator Striker", 759.376f, 31.810f, 1629.038f, 774.322f, 30.205f, 1627.563f),
            new PatrolRoute("Predator Striker", 757.870f, 31.530f, 1321.433f, 771.814f, 27.674f, 1317.191f),
            new PatrolRoute("Predator Striker", 747.735f, 31.340f, 1622.433f, 760.858f, 31.686f, 1628.895f),
            new PatrolRoute("Slivering Chimera", 757.829f, 25.484f, 1582.952f, 771.213f, 28.098f, 1593.781f),
            new PatrolRoute("Slivering Chimera", 774.861f, 28.045f, 1600.848f, 781.537f, 28.440f, 1616.520f),
            new PatrolRoute("Slivering Chimera", 780.976f, 28.246f, 1615.226f, 784.325f, 29.361f, 1629.230f),
            new PatrolRoute("Slivering Chimera", 802.203f, 30.340f, 1649.725f, 813.361f, 31.210f, 1657.542f),
            new PatrolRoute("Slivering Chimera", 770.574f, 27.996f, 1592.536f, 776.134f, 27.711f, 1603.499f),
            new PatrolRoute("Slivering Chimera", 780.875f, 28.784f, 1606.690f, 789.358f, 29.829f, 1615.465f),
            new PatrolRoute("Slivering Chimera", 802.293f, 30.894f, 1639.000f, 803.344f, 30.518f, 1650.633f),
            new PatrolRoute("Slivering Chimera", 778.622f, 31.878f, 1637.635f, 783.992f, 29.387f, 1627.816f),
            new PatrolRoute("Stalking Predator", 871.169f, 30.139f, 1676.044f, 896.555f, 29.753f, 1682.269f),
            new PatrolRoute("Stalking Predator", 879.169f, 29.410f, 1665.192f, 896.555f, 29.753f, 1682.269f),
            new PatrolRoute("Stalking Predator", 832.234f, 31.838f, 1683.955f, 850.801f, 31.809f, 1672.036f),
            new PatrolRoute("Stalking Predator", 810.019f, 32.111f, 1668.380f, 810.717f, 31.705f, 1647.417f),
            new PatrolRoute("Stalking Predator", 856.148f, 32.154f, 1669.708f, 873.640f, 29.768f, 1676.816f),
            new PatrolRoute("Stalking Predator", 832.234f, 31.838f, 1683.955f, 839.805f, 31.612f, 1670.583f),
            new PatrolRoute("Stalking Predator", 824.339f, 31.103f, 1664.744f, 827.097f, 31.543f, 1679.124f),
            new PatrolRoute("Stalking Predator", 813.569f, 32.008f, 1670.831f, 825.321f, 31.191f, 1664.130f),
            new PatrolRoute("Swift Silvertail", 896.613f, 30.708f, 1614.367f, 899.060f, 31.210f, 1584.261f),
            new PatrolRoute("Swift Silvertail", 806.400f, 29.410f, 1224.275f, 819.452f, 28.810f, 1176.306f),
            new PatrolRoute("Swift Silvertail", 786.519f, 29.036f, 1229.889f, 816.200f, 32.410f, 1255.504f),
            new PatrolRoute("Swift Silvertail", 889.872f, 29.691f, 1517.682f, 890.670f, 30.823f, 1480.409f),
            new PatrolRoute("Swift Silvertail", 963.257f, 31.497f, 1337.169f, 966.880f, 27.610f, 1301.695f),
            new PatrolRoute("Swift Silvertail", 812.620f, 32.410f, 1288.259f, 815.215f, 32.298f, 1254.634f),
            new PatrolRoute("Swift Silvertail", 765.531f, 31.351f, 1252.481f, 783.152f, 29.289f, 1276.744f),
            new PatrolRoute("Swift Silvertail", 951.463f, 32.410f, 1364.425f, 963.257f, 31.497f, 1337.169f),
            new PatrolRoute("Swift Silvertail", 818.382f, 28.810f, 1177.308f, 837.998f, 32.410f, 1156.416f),
            new PatrolRoute("Tempterus", 654.352f, 32.225f, 1381.117f, 692.526f, 31.895f, 1375.021f),
            new PatrolRoute("Tempterus", 643.394f, 31.346f, 1348.908f, 660.850f, 31.534f, 1378.132f),
            new PatrolRoute("Tempterus", 686.899f, 31.245f, 1372.204f, 717.038f, 32.129f, 1365.088f),
            new PatrolRoute("Tempterus", 659.440f, 31.662f, 1378.454f, 689.417f, 31.372f, 1371.624f),
            new PatrolRoute("Tempterus", 715.754f, 32.023f, 1365.392f, 731.900f, 31.266f, 1348.374f),
            new PatrolRoute("Tempterus", 658.167f, 32.263f, 1385.133f, 678.415f, 31.810f, 1384.006f),
            new PatrolRoute("Tempterus", 679.168f, 32.291f, 1291.347f, 683.324f, 32.909f, 1271.641f),
            new PatrolRoute("Tempterus", 676.442f, 29.896f, 1325.378f, 683.634f, 32.245f, 1309.504f),
            // Capture 20260826-054154 Predator Striker pocket ~755-810/1900-1965.
            new PatrolRoute("Predator Striker", 794.750061f, 31.210001f, 1902.32019f, 795.5474f, 31.210001f, 1922.3949f),
            new PatrolRoute("Predator Striker", 756.767f, 31.210001f, 1909.91064f, 753.4113f, 31.210001f, 1902.90417f),
            new PatrolRoute("Predator Striker", 772.6582f, 31.210001f, 1919.97949f, 771.0486f, 33.79995f, 1912.20813f),
            new PatrolRoute("Predator Striker", 776.882935f, 31.210001f, 1929.22363f, 766.1672f, 31.210001f, 1929.35632f),
            new PatrolRoute("Predator Striker", 762.076965f, 31.210001f, 1942.66f, 769.752136f, 31.210001f, 1932.897f),
            new PatrolRoute("Predator Striker", 802.872253f, 31.210001f, 1945.83679f, 807.315369f, 31.210001f, 1942.28333f),
            new PatrolRoute("Predator Striker", 759.6166f, 31.210001f, 1920.06677f, 761.4364f, 31.210001f, 1931.85315f),
            new PatrolRoute("Predator Striker", 795.4465f, 31.210001f, 1920.036f, 795.5474f, 31.210001f, 1922.3949f),
            new PatrolRoute("Predator Striker", 799.9527f, 31.210001f, 1926.77747f, 791.0778f, 31.210001f, 1929.32117f),
            new PatrolRoute("Predator Striker", 799.9621f, 31.210001f, 1934.4873f, 791.321655f, 31.210001f, 1943.09241f),
            new PatrolRoute("Predator Striker", 755.2589f, 31.210001f, 1906.75f, 753.4113f, 31.210001f, 1902.90417f),
            new PatrolRoute("Predator Striker", 794.850647f, 31.210001f, 1904.84082f, 795.5474f, 31.210001f, 1922.3949f),
            new PatrolRoute("Predator Striker", 808.7332f, 31.210001f, 1924.254f, 791.0778f, 31.210001f, 1929.32117f),
        };

        // Capture 20260826-052537 SCFU 7A2F8AF7 Hiathlin HasExtendedTextures (92-byte 0x0BD3 wire).
        private static readonly byte[] HiathlinExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x64, 0x72, 0x61, 0x67, 0x73, 0x68, 0x61, 0x64, 0x5F, 0x6F, 0x70, 0x61,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x95, 0xEA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x64, 0x72, 0x61, 0x67, 0x73, 0x68, 0x61, 0x64, 0x5F, 0x73, 0x65, 0x6C, 0x66, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x95, 0xEA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            };

        private static readonly byte[] OmathonExtendedTextureOverrideData =
            BuildDualMaterialExtTex("dragshad_opa", "dragshad_self", TextureOmathon);

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            return TryGetExtendedTextureOverride(name, 0, out data);
        }

        internal static bool TryGetExtendedTextureOverride(string name, int playfieldId, out byte[] data)
        {
            if (string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture sabre self :235170 — 48-byte 0x07E2 only (92-byte tail is instance-specific like Weaver).
                data = BuildSingleMaterialExtTex("sabre self", TextureSabreDeadly, 1);
                return true;
            }

            // Striker/Stalking sabre ExtTex crashes client — keep default model.
            if (string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase)
                || (name != null && name.StartsWith("Hwall", StringComparison.OrdinalIgnoreCase)))
            {
                data = null;
                return false;
            }

            if (string.Equals(name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                // ExtTex via LifeSpawn capture bytes; only starter-bridge patrol (outdoor MM24 caused exit crash).
                data = null;
                return false;
            }

            if (string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-051307 frontier Slivering ExtTex low2:208969.
                data = BuildSingleMaterialExtTex("low2", TextureLow2, 1);
                return true;
            }

            if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 dragshad_opa + dragshad_self :234986 (0x0BD3, 92 bytes).
                data = (byte[])HiathlinExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 dragshad_opa + dragshad_self :234987 (same wire as Hiathlin).
                data = (byte[])OmathonExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260827-221909 corpse ExtTex anun self + Material #7 :209280 (0x0BD3).
                // Outdoor PF4310 ExtTex still crashes Demonic exit — gate to Wilds cave only.
                if (playfieldId == 4311)
                {
                    data = BuildDualMaterialExtTex("anun self", "Material #7", TextureCrippler);
                    return true;
                }

                data = null;
                return false;
            }

            if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-212737 TextureOverrides Material #13:235226 — shared 48-byte wire
                // (88-byte capture tail is instance-specific; reusing it on every Weaver crashes client).
                data = BuildSingleMaterialExtTex("Material #13", TextureWeaver, 1);
                return true;
            }

            if (string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-135727 SCFU dragoon_alpha:302615 (SCFU failures=0).
                data = BuildSingleMaterialExtTex("dragoon_alpha", TextureSpinetooth, 1);
                return true;
            }

            data = null;
            return false;
        }

        internal static void ApplySpawnStats(ICharacter mob, string name)
        {
            if (mob == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            // Capture SCFU Side=Monster → red PF map dots for hostiles only.
            // Clan=yellow, Omni=blue, Neutral=white (friendly NPCs set Side in LifeSpawn).
            if (IsOutdoorHostileMonster(name))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Monster);
                mob.Stats[StatIds.side].Value = (int)Side.Monster;
            }

            // Capture 20260826-051307 SCFU npcFamily / Side=Monster (default animal side).
            if (string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Malah-Aya", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 207u);
            }
            else if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 191u);
            }
            else if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Hesosas", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 189u);
            }
            else if (string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 181u);
            }
            else if (string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 187u);
            }
            else if (string.Equals(name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 200u);
            }
            else if (string.Equals(name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 172u);
            }
            else if (string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 SCFU 7A2ED7C3 npcFamily=174 RunSpeedBase=69.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 174u);
            }
            else if (string.Equals(name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 174u);
            }

            if (string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                // Visible spawn uses default animal flags; ExtTex supplies sabre glow (capture :235170).
                SyncMobStat(mob, StatIds.npcfamily, 207u);
                SyncMobStat(mob, StatIds.flags, (uint)DefaultAnimalCharacterFlags);
                SyncMobStat(mob, StatIds.monsterdata, 209022u);
                SyncMobStat(mob, StatIds.monsterscale, 128u);
                SyncMobStat(mob, StatIds.visualflags, 31u);
                SyncMobStat(mob, StatIds.mindamage, 18u);
                SyncMobStat(mob, StatIds.maxdamage, 29u);
                SyncMobStat(mob, StatIds.runspeed, 69u);
                SyncMobStat(mob, StatIds.life, 2375u);
                SyncMobStat(mob, StatIds.health, 2375u);
                SyncMobStat(mob, StatIds.level, 20u);
            }

            if (string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 17u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 28u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 70u);
            }
            else if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 11u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 21u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 53u);
            }
            else if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 12u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 14u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
            }
            else if (string.Equals(name, "Malah-Aya", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 12u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 14u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 69u);
            }
            else if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 AttackInfo Amount=11.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 11u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 11u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
            }
            else if (string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 AttackInfo Amount=13.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 13u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 13u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
            }
            else if (string.Equals(name, "Hesosas", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-055143 Hesosas runSpeed=63.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 63u);
            }
            else if (string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-054154 AttackInfo Amount=12; SCFU RunSpeedBase=52.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 12u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 12u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
            }
            else if (string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 45u);
            }
            else if (string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 18u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 29u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 69u);
            }
            else if (string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 AttackInfo Amount 36..69.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 36u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 69u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 69u);
            }
            else if (string.Equals(name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 32u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 32u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 52u);
            }
            else if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260827-221909 AttackInfo Amount=24; SCFU RunSpeedBase=97.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, 24u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, 24u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 97u);
            }
            else if (string.Equals(name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 34u);
            }
        }

        internal static bool TryGetCombatContract(string name, out CapturedEnemyCombatContract contract)
        {
            contract = null;
            if (string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                contract = BuildSpinetoothCombatContract();
                return true;
            }

            if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                contract = BuildSawContract(
                    "20260826-051307: Weaver of Malice SAW 101/RIJL",
                    unchecked((int)0x7A2ED6EF),
                    101,
                    11,
                    21,
                    new[] { 11, 12, 12, 21, 12, 11, 12 });
                return true;
            }

            if (string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Malah-Aya", StringComparison.OrdinalIgnoreCase))
            {
                contract = BuildSawContract(
                    "20260826-051307: Malah-Ana SAW 114/RIJL",
                    unchecked((int)0x7A2ED6B4),
                    114,
                    12,
                    14,
                    new[] { 12, 14, 14, 12, 14 });
                return true;
            }

            if (string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-054154 7A2F8B6A SAW 101/RIJL; AttackInfo Amount=12 slot=4.
                contract = BuildSawContract(
                    "20260826-054154: Predator Striker SAW 101/RIJL",
                    unchecked((int)0x7A2F8B6A),
                    101,
                    12,
                    12,
                    new[] { 12, 12, 12, 12, 12 });
                return true;
            }

            if (string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase))
            {
                contract = BuildSawContract(
                    "20260825-202932: Stalking Predator SAW 81/RIJL",
                    unchecked((int)0x7A2ED6B9),
                    81,
                    10,
                    16,
                    new[] { 10, 16, 10 });
                return true;
            }

            if (string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-054154 7A2FFA29 + 20260825-202932 SAW 171/171/171/134 RIJL; AttackInfo 18..29.
                contract = BuildSawContract(
                    "20260826-054154: Deadly Predator SAW 171/RIJL",
                    unchecked((int)0x7A2FFA29),
                    171,
                    171,
                    171,
                    134,
                    RijlSpecials(),
                    0x52494A4C,
                    18,
                    29,
                    new[] { 18, 22, 25, 29, 20 });
                return true;
            }

            if (string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 7A2ED7C3 SAW 171/171/171/134 VQIR family; AttackInfo 36|69.
                contract = BuildSawContract(
                    "20260825-202932: The Demonic Subjugator SAW 171/VQIR",
                    unchecked((int)0x7A2ED7C3),
                    171,
                    171,
                    171,
                    134,
                    VqirSpecials(),
                    0x56514952,
                    36,
                    69,
                    new[] { 36, 69, 36, 69, 36, 69 });
                return true;
            }

            if (string.Equals(name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 7A2ED7B9 SAW 134×4 VQIR family; AttackInfo Amount=32.
                contract = BuildSawContract(
                    "20260825-202932: Corrupting Imp SAW 134/VQIR",
                    unchecked((int)0x7A2ED7B9),
                    134,
                    134,
                    134,
                    134,
                    VqirSpecials(),
                    0x56514952,
                    32,
                    32,
                    new[] { 32, 32, 32, 32 });
                return true;
            }

            if (string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260825-202932 SAW 88×4 BGVX family; AttackInfo Amount=27.
                contract = BuildSawContract(
                    "20260825-202932: Slivering Chimera SAW 88/BGVX",
                    unchecked((int)0x7A2ED7C1),
                    88,
                    88,
                    88,
                    88,
                    BgvxSpecials(),
                    0x42475658,
                    27,
                    27,
                    new[] { 27, 27, 27 });
                return true;
            }

            if (string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 7A2F8AC2 SAW 94/RIJL; AttackInfo Amount=11.
                contract = BuildSawContract(
                    "20260826-052537: Hiathlin SAW 94/RIJL",
                    unchecked((int)0x7A2F8AC2),
                    94,
                    94,
                    94,
                    94,
                    RijlSpecials(),
                    0x52494A4C,
                    11,
                    11,
                    new[] { 11, 11, 11 });
                return true;
            }

            if (string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260826-052537 7A2ED787 SAW 139/139/139/101 RIJL; AttackInfo Amount=13.
                contract = BuildSawContract(
                    "20260826-052537: Omathon SAW 139/RIJL",
                    unchecked((int)0x7A2ED787),
                    139,
                    139,
                    139,
                    101,
                    RijlSpecials(),
                    0x52494A4C,
                    13,
                    13,
                    new[] { 13, 13, 13, 13, 13 });
                return true;
            }

            if (string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260827-221909 7A372E06 SAW Unknown1-4=181; AttackInfo Amount=24.
                contract = BuildSawContract(
                    "20260827-221909: Crippler of Growth SAW 181",
                    unchecked((int)0x7A372E06),
                    181,
                    24,
                    24,
                    new[] { 24, 24, 24, 24, 24 });
                return true;
            }

            return false;
        }

        internal static bool TryResolvePatrolWaypoints(
            string name,
            float spawnX,
            float spawnY,
            float spawnZ,
            out float[][] waypoints)
        {
            waypoints = null;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("Hwall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hesosas", StringComparison.OrdinalIgnoreCase))
            {
                // Geosurvey Dog: starter-bridge capture patrol only (7A202B50); outdoor MM24 routes crash client.
                // Hesosas: static spawn only (055143 FollowTarget was death/respawn wire, not patrol).
                return false;
            }

            PatrolRoute best = null;
            float bestDistSq = PatrolMatchRadiusMeters * PatrolMatchRadiusMeters;
            TryMatchPatrolRoute(Garden160734PatrolRoutes, name, spawnX, spawnZ, ref best, ref bestDistSq);
            TryMatchPatrolRoute(PatrolRoutes, name, spawnX, spawnZ, ref best, ref bestDistSq);

            if (best == null)
            {
                return false;
            }

            waypoints = new[]
                {
                    new[] { best.X1, best.Y1, best.Z1 },
                    new[] { best.X2, best.Y2, best.Z2 },
                };
            return true;
        }

        /// <summary>
        /// PF map: Side.Monster → red dots. Do not overwrite Clan/Omni/Neutral NPCs.
        /// </summary>
        private static bool IsOutdoorHostileMonster(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return string.Equals(name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Malah-Aya", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Malah-Ana", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Predator Striker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Deadly Predator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Stalking Predator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Omathon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Barking Chimera", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Soul Dredge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryMatchPatrolRoute(
            PatrolRoute[] routes,
            string name,
            float spawnX,
            float spawnZ,
            ref PatrolRoute best,
            ref float bestDistSq)
        {
            if (routes == null)
            {
                return;
            }

            for (int i = 0; i < routes.Length; i++)
            {
                PatrolRoute route = routes[i];
                if (route == null || !string.Equals(route.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float d1 = DistSq(spawnX, spawnZ, route.X1, route.Z1);
                float d2 = DistSq(spawnX, spawnZ, route.X2, route.Z2);
                float d = d1 < d2 ? d1 : d2;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = route;
                }
            }
        }

        private static CapturedEnemySpecialAttackDefinition[] RijlSpecials()
        {
            return new[]
            {
                new CapturedEnemySpecialAttackDefinition(236699, 236700, 0x52494A4C, "RIJL"),
                new CapturedEnemySpecialAttackDefinition(236696, 236697, 0x4441544A, "DATJ"),
                new CapturedEnemySpecialAttackDefinition(236693, 236694, 0x555A424D, "UZBM"),
                new CapturedEnemySpecialAttackDefinition(211013, 211014, 0x43484346, "CHCF"),
                new CapturedEnemySpecialAttackDefinition(211010, 211011, 0x49464F48, "IFOH"),
            };
        }

        private static CapturedEnemySpecialAttackDefinition[] VqirSpecials()
        {
            // Capture 20260825-202932 Demonic Subjugator / Corrupting Imp.
            return new[]
            {
                new CapturedEnemySpecialAttackDefinition(236781, 236782, 0x56514952, "VQIR"),
                new CapturedEnemySpecialAttackDefinition(236778, 236779, 0x4346574C, "CFWL"),
                new CapturedEnemySpecialAttackDefinition(236772, 236773, 0x4D54524E, "MTRN"),
                new CapturedEnemySpecialAttackDefinition(236769, 236770, 0x504B4C47, "PKLG"),
                new CapturedEnemySpecialAttackDefinition(236766, 236767, 0x464D474F, "FMGO"),
            };
        }

        private static CapturedEnemySpecialAttackDefinition[] BgvxSpecials()
        {
            // Capture 20260825-202932 Slivering Chimera.
            return new[]
            {
                new CapturedEnemySpecialAttackDefinition(233069, 233070, 0x42475658, "BGVX"),
                new CapturedEnemySpecialAttackDefinition(233066, 233067, 0x5941504B, "YAPK"),
                new CapturedEnemySpecialAttackDefinition(233063, 233064, 0x4C57454B, "LWEK"),
                new CapturedEnemySpecialAttackDefinition(233060, 233061, 0x4D584C50, "MXLP"),
                new CapturedEnemySpecialAttackDefinition(233057, 233058, 0x544B5251, "TKRQ"),
            };
        }

        private static CapturedEnemyCombatContract BuildSpinetoothCombatContract()
        {
            // Capture 20260826-135727 fights 7A2FFE3A / 7A2FFD65 + 20260826-200902 7A2FFE39:
            // SAW 134/RIJL, AttackInfo Amount=17|28, CastNano 149807→149800 "Intense Mind Blast".
            return BuildSawContract(
                "20260826-135727: Spinetooth Hatchling SAW 134/RIJL 3m aggro",
                unchecked((int)0x7A2FFE39),
                134,
                17,
                28,
                new[] { 17, 17, 17, 17, 17, 28, 17 },
                3.0d);
        }

        private static CapturedEnemyCombatContract BuildSawContract(
            string evidence,
            int captureIdentity,
            int saw,
            int minDamage,
            int maxDamage,
            int[] damageObservations)
        {
            return BuildSawContract(
                evidence,
                captureIdentity,
                saw,
                minDamage,
                maxDamage,
                damageObservations,
                NpcCombatAttackRules.MaxMeleeCombatDistance);
        }

        private static CapturedEnemyCombatContract BuildSawContract(
            string evidence,
            int captureIdentity,
            int saw,
            int minDamage,
            int maxDamage,
            int[] damageObservations,
            double attackRangeMeters)
        {
            return BuildSawContract(
                evidence,
                captureIdentity,
                saw,
                saw,
                saw,
                saw,
                RijlSpecials(),
                0x52494A4C,
                minDamage,
                maxDamage,
                damageObservations,
                attackRangeMeters);
        }

        private static CapturedEnemyCombatContract BuildSawContract(
            string evidence,
            int captureIdentity,
            int sawUnknown1,
            int sawUnknown2,
            int sawUnknown3,
            int sawUnknown4,
            CapturedEnemySpecialAttackDefinition[] specials,
            int primarySpecialHash,
            int minDamage,
            int maxDamage,
            int[] damageObservations)
        {
            return BuildSawContract(
                evidence,
                captureIdentity,
                sawUnknown1,
                sawUnknown2,
                sawUnknown3,
                sawUnknown4,
                specials,
                primarySpecialHash,
                minDamage,
                maxDamage,
                damageObservations,
                NpcCombatAttackRules.MaxMeleeCombatDistance);
        }

        private static CapturedEnemyCombatContract BuildSawContract(
            string evidence,
            int captureIdentity,
            int sawUnknown1,
            int sawUnknown2,
            int sawUnknown3,
            int sawUnknown4,
            CapturedEnemySpecialAttackDefinition[] specials,
            int primarySpecialHash,
            int minDamage,
            int maxDamage,
            int[] damageObservations,
            double attackRangeMeters)
        {
            double[] attackStartDelays = { 0.0, 0.0, 0.0, 0.0, 0.0 };
            double[] firstHitDelays = { 3.0, 3.2, 3.4, 3.5, 3.6 };
            double[] landedIntervals = { 4.0, 4.1, 4.2, 4.0, 4.3 };

            return CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                evidence,
                captureIdentity,
                NpcAiProfile.Passive,
                minDamage,
                maxDamage,
                landedIntervals[0],
                specials,
                0,
                sawUnknown1,
                sawUnknown2,
                sawUnknown3,
                sawUnknown4,
                0,
                0,
                0,
                -1,
                0,
                0,
                3,
                primarySpecialHash,
                0,
                false,
                damageObservations,
                attackStartDelays,
                firstHitDelays,
                landedIntervals,
                0,
                false,
                attackRangeMeters,
                true);
        }

        private static void SyncMobStat(ICharacter mob, StatIds stat, uint value)
        {
            mob.Stats.SetBaseValueWithoutTriggering((int)stat, value);
            mob.Stats[stat].Value = (int)value;
        }

        private static float DistSq(float x1, float z1, float x2, float z2)
        {
            float dx = x1 - x2;
            float dz = z1 - z2;
            return (dx * dx) + (dz * dz);
        }

        private static byte[] BuildSingleMaterialExtTex(string material, int textureId, byte terminalFlag)
        {
            byte[] buffer = new byte[48];
            buffer[2] = 0x07;
            buffer[3] = 0xE2;
            WriteAsciiField(buffer, 4, material, 32);
            WriteTextureId(buffer, 36, textureId);
            if (terminalFlag != 0)
            {
                buffer[47] = terminalFlag;
            }

            return buffer;
        }

        private static byte[] BuildConcatenatedSingleMaterialExtTex(
            string primaryMaterial,
            string secondaryMaterial,
            int textureId)
        {
            byte[] block1 = BuildSingleMaterialExtTex(primaryMaterial, textureId, 1);
            byte[] block2 = BuildSingleMaterialExtTex(secondaryMaterial, textureId, 1);
            byte[] buffer = new byte[96];
            Buffer.BlockCopy(block1, 0, buffer, 0, 48);
            Buffer.BlockCopy(block2, 0, buffer, 48, 48);
            return buffer;
        }

        private static byte[] BuildDualMaterialExtTex(string primaryMaterial, string secondaryMaterial, int textureId)
        {
            // Capture 20260826-052537 Hiathlin SCFU: 0x0BD3 dual block with terminal=1 on bytes 47 and 91.
            byte[] buffer = new byte[92];
            buffer[2] = 0x0B;
            buffer[3] = 0xD3;
            WriteAsciiField(buffer, 4, primaryMaterial, 32);
            WriteTextureId(buffer, 36, textureId);
            buffer[47] = 1;
            WriteAsciiField(buffer, 48, secondaryMaterial, 32);
            WriteTextureId(buffer, 80, textureId);
            buffer[91] = 1;
            return buffer;
        }

        private static void WriteAsciiField(byte[] buffer, int offset, string text, int fieldLength)
        {
            if (buffer == null || string.IsNullOrEmpty(text) || fieldLength <= 0)
            {
                return;
            }

            byte[] ascii = Encoding.ASCII.GetBytes(text);
            int copy = Math.Min(ascii.Length, fieldLength - 1);
            Array.Copy(ascii, 0, buffer, offset, copy);
        }

        private static void WriteTextureId(byte[] buffer, int offset, int textureId)
        {
            buffer[offset] = 0;
            buffer[offset + 1] = (byte)((textureId >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((textureId >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(textureId & 0xFF);
        }
    }
}
