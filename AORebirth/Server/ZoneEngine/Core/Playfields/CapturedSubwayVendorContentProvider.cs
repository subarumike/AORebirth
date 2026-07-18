// Capture-derived Subway merchant content. Appearance and owner/terminal links:
// AOSharpLiveCapture 20260709-212115. Container stock uses exact template 99634
// slot evidence from AOSharpLiveCapture 20260613-221619; dialogue remains unresolved.

namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.ObjectModel;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    internal static class CapturedSubwayVendorContentProvider
    {
        internal const int SubwayPlayfieldResource = 127;

        private const string Evidence = "AOSharpLiveCapture/20260709-212115";

        private const string ContainerStockEvidence =
            "AOSharpLiveCapture/20260613-221619;"
            + "identity=VendingMachine:C0000317;template=99634;slots=62;"
            + "exact-template-reuse";

        private static readonly ReadOnlyCollection<CapturedSubwayVendorDefinition> CapturedDefinitions =
            Array.AsReadOnly(
                new[]
                {
                    Create(
                        0x79135F51,
                        0x12ECC394,
                        "Tailor",
                        99637,
                        256.556519f,
                        107.611687f,
                        281.52475f,
                        0f,
                        -0.0354215f,
                        0f,
                        0.999372542f,
                        1832,
                        Breed.Solitus,
                        Gender.Female,
                        26076,
                        40635,
                        "80000000000000000000000003010001000100010001000000020000",
                        true,
                        false,
                        TailorStock()),
                    Create(
                        0x79135F52,
                        0x12ECC395,
                        "Basic Quality Weaponsdealer",
                        99572,
                        254.01207f,
                        107.611687f,
                        299.2679f,
                        0f,
                        0.950199544f,
                        0f,
                        -0.311642349f,
                        1576,
                        Breed.Solitus,
                        Gender.Male,
                        26092,
                        40694,
                        "80000000000000008000000003010001000100010001000000020000",
                        false,
                        false,
                        WeaponsdealerStock()),
                    Create(
                        0x79135F53,
                        0x12ECC396,
                        "Basic Quality Armorer",
                        99570,
                        229.937576f,
                        107.611687f,
                        288.3191f,
                        0f,
                        0.00296102231f,
                        0f,
                        0.9999958f,
                        1416,
                        Breed.Atrox,
                        Gender.Neutral,
                        26097,
                        40111,
                        "00000000000000000000000003010001000100010001000000020000",
                        false,
                        false,
                        ArmorerStock()),
                    Create(
                        0x79135F54,
                        0x12ECC397,
                        "Basic Quality Pharmacist",
                        99574,
                        228.509552f,
                        107.611687f,
                        305.6732f,
                        0f,
                        -0.9119379f,
                        0f,
                        0.4103283f,
                        1640,
                        Breed.Nanomage,
                        Gender.Male,
                        26151,
                        40171,
                        "80000000000000008000000003010001000100010001000000020000",
                        false,
                        true,
                        PharmacistStock()),
                    Create(
                        0x79135F55,
                        0x12ECC398,
                        "Basic Tools Merchant",
                        99601,
                        210.80394f,
                        107.611687f,
                        306.908478f,
                        0f,
                        0.8790978f,
                        0f,
                        -0.476641446f,
                        1864,
                        Breed.Opifex,
                        Gender.Female,
                        26137,
                        40209,
                        "80000000000000008000000003010001000100010001000000020000",
                        false,
                        false,
                        ToolsStock()),
                    Create(
                        0x79135F56,
                        0x12ECC399,
                        "Container Supplier",
                        99634,
                        203.742722f,
                        107.611687f,
                        299.442017f,
                        0f,
                        0.140770257f,
                        0f,
                        0.9900424f,
                        1832,
                        Breed.Solitus,
                        Gender.Female,
                        26082,
                        40634,
                        "00000000000000000000000003010001000100010001000000020000",
                        false,
                        false,
                        ContainerStock(),
                        ContainerStockEvidence)
                });

        internal static ReadOnlyCollection<CapturedSubwayVendorDefinition> Definitions
        {
            get { return CapturedDefinitions; }
        }

        private static CapturedSubwayVendorDefinition Create(
            int sourceNpcInstance,
            int sourceVendorInstance,
            string displayName,
            int vendorTemplateId,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            int appearanceValue,
            Breed breed,
            Gender gender,
            int monsterData,
            int headMesh,
            string unknown1Hex,
            bool isPet,
            bool hasWaypoint,
            CapturedSubwayVendorStockDefinition[] stock,
            string stockEvidence = null)
        {
            SimpleCharFullUpdateFlags flags =
                SimpleCharFullUpdateFlags.IsNpc
                | SimpleCharFullUpdateFlags.UnknownFlag
                | SimpleCharFullUpdateFlags.UnknownFlag6
                | SimpleCharFullUpdateFlags.HasPlayfieldId
                | SimpleCharFullUpdateFlags.HasHeadMesh
                | SimpleCharFullUpdateFlags.HasHeading
                | SimpleCharFullUpdateFlags.HasSmallHealth
                | SimpleCharFullUpdateFlags.HasExtendedRunSpeed
                | SimpleCharFullUpdateFlags.HasSmallHealthDamage
                | SimpleCharFullUpdateFlags.HasSmallNpcFamily
                | SimpleCharFullUpdateFlags.HasSmallNpcLosHeight
                | SimpleCharFullUpdateFlags.UnknownDataFlag;
            if (isPet)
            {
                flags |= SimpleCharFullUpdateFlags.IsPet;
            }

            if (hasWaypoint)
            {
                flags |= SimpleCharFullUpdateFlags.HasWaypoints;
            }

            CapturedSubwayVendorWaypointDefinition[] waypoints = hasWaypoint
                ? new[] { new CapturedSubwayVendorWaypointDefinition(x, y, z) }
                : new CapturedSubwayVendorWaypointDefinition[0];

            return new CapturedSubwayVendorDefinition(
                sourceNpcInstance,
                sourceVendorInstance,
                displayName,
                vendorTemplateId,
                x,
                y,
                z,
                headingX,
                headingY,
                headingZ,
                headingW,
                appearanceValue,
                (int)Side.Neutral,
                (int)Fatness.Normal,
                (int)breed,
                (int)gender,
                1,
                monsterData,
                119,
                headMesh,
                180,
                17841,
                448,
                unchecked((int)0x10281201),
                31,
                (uint)flags,
                HexToBytes(unknown1Hex),
                new[]
                {
                    new CapturedSubwayVendorTextureDefinition(0, 0, 0),
                    new CapturedSubwayVendorTextureDefinition(1, 30862, 0),
                    new CapturedSubwayVendorTextureDefinition(2, 40903, 0),
                    new CapturedSubwayVendorTextureDefinition(3, 30839, 0),
                    new CapturedSubwayVendorTextureDefinition(4, 30886, 0)
                },
                new[]
                {
                    new CapturedSubwayVendorMeshDefinition(0, (uint)headMesh, 0, 4),
                    new CapturedSubwayVendorMeshDefinition(1, 7777, 0, 2)
                },
                waypoints,
                stock,
                Evidence,
                stock == null ? string.Empty : stockEvidence ?? Evidence);
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

        private static CapturedSubwayVendorStockDefinition[] TailorStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 41006, 41006, 1),
                new CapturedSubwayVendorStockDefinition(1, 31244, 31244, 1),
                new CapturedSubwayVendorStockDefinition(2, 31246, 31246, 1),
                new CapturedSubwayVendorStockDefinition(3, 42318, 42318, 1),
                new CapturedSubwayVendorStockDefinition(4, 31261, 31261, 1),
                new CapturedSubwayVendorStockDefinition(5, 31509, 31509, 1),
                new CapturedSubwayVendorStockDefinition(6, 41058, 41058, 1),
                new CapturedSubwayVendorStockDefinition(7, 31252, 31252, 1),
                new CapturedSubwayVendorStockDefinition(8, 41034, 41034, 1),
                new CapturedSubwayVendorStockDefinition(9, 31501, 31501, 1),
                new CapturedSubwayVendorStockDefinition(10, 40999, 40999, 1),
                new CapturedSubwayVendorStockDefinition(11, 31104, 31104, 1),
                new CapturedSubwayVendorStockDefinition(12, 41016, 41016, 1),
                new CapturedSubwayVendorStockDefinition(13, 31243, 31243, 1),
                new CapturedSubwayVendorStockDefinition(14, 31254, 31254, 1),
                new CapturedSubwayVendorStockDefinition(15, 31499, 31499, 1),
                new CapturedSubwayVendorStockDefinition(16, 41008, 41008, 1),
                new CapturedSubwayVendorStockDefinition(17, 41007, 41007, 1),
                new CapturedSubwayVendorStockDefinition(18, 42335, 42335, 1),
                new CapturedSubwayVendorStockDefinition(19, 42355, 42355, 1),
                new CapturedSubwayVendorStockDefinition(20, 41059, 41059, 1)
            };
        }

        private static CapturedSubwayVendorStockDefinition[] WeaponsdealerStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 122070, 122071, 76),
                new CapturedSubwayVendorStockDefinition(1, 122275, 122276, 27),
                new CapturedSubwayVendorStockDefinition(2, 123154, 123155, 53),
                new CapturedSubwayVendorStockDefinition(3, 160186, 160187, 50),
                new CapturedSubwayVendorStockDefinition(4, 122970, 122971, 96),
                new CapturedSubwayVendorStockDefinition(5, 122756, 122757, 96),
                new CapturedSubwayVendorStockDefinition(6, 122962, 122963, 2),
                new CapturedSubwayVendorStockDefinition(7, 122047, 122048, 39),
                new CapturedSubwayVendorStockDefinition(8, 124027, 124028, 35),
                new CapturedSubwayVendorStockDefinition(9, 142821, 142822, 46),
                new CapturedSubwayVendorStockDefinition(10, 123666, 123666, 1),
                new CapturedSubwayVendorStockDefinition(11, 124321, 124322, 96),
                new CapturedSubwayVendorStockDefinition(12, 129640, 129641, 27),
                new CapturedSubwayVendorStockDefinition(13, 123118, 123119, 49),
                new CapturedSubwayVendorStockDefinition(14, 125347, 125348, 58),
                new CapturedSubwayVendorStockDefinition(15, 122909, 122909, 41),
                new CapturedSubwayVendorStockDefinition(16, 122186, 122187, 84),
                new CapturedSubwayVendorStockDefinition(17, 124366, 124367, 64),
                new CapturedSubwayVendorStockDefinition(18, 124366, 124367, 83),
                new CapturedSubwayVendorStockDefinition(19, 123971, 123972, 96),
                new CapturedSubwayVendorStockDefinition(20, 123748, 123749, 83),
                new CapturedSubwayVendorStockDefinition(21, 124243, 124244, 44),
                new CapturedSubwayVendorStockDefinition(22, 142857, 142857, 21),
                new CapturedSubwayVendorStockDefinition(23, 128710, 128711, 7),
                new CapturedSubwayVendorStockDefinition(24, 160233, 160234, 96),
                new CapturedSubwayVendorStockDefinition(25, 122987, 122988, 68),
                new CapturedSubwayVendorStockDefinition(26, 159040, 159041, 31),
                new CapturedSubwayVendorStockDefinition(27, 159040, 159041, 43),
                new CapturedSubwayVendorStockDefinition(28, 122140, 122141, 12),
                new CapturedSubwayVendorStockDefinition(29, 128499, 128500, 60),
                new CapturedSubwayVendorStockDefinition(30, 123179, 123180, 84)
            };
        }

        private static CapturedSubwayVendorStockDefinition[] ArmorerStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 85639, 22124, 16),
                new CapturedSubwayVendorStockDefinition(1, 85687, 22046, 39),
                new CapturedSubwayVendorStockDefinition(2, 85645, 85644, 25),
                new CapturedSubwayVendorStockDefinition(3, 85562, 85561, 16),
                new CapturedSubwayVendorStockDefinition(4, 85521, 85520, 35),
                new CapturedSubwayVendorStockDefinition(5, 85533, 85532, 31),
                new CapturedSubwayVendorStockDefinition(6, 70562, 85597, 48),
                new CapturedSubwayVendorStockDefinition(7, 70560, 85688, 22),
                new CapturedSubwayVendorStockDefinition(8, 85554, 85553, 38),
                new CapturedSubwayVendorStockDefinition(9, 85737, 85736, 49),
                new CapturedSubwayVendorStockDefinition(10, 85681, 85680, 20),
                new CapturedSubwayVendorStockDefinition(11, 162427, 162428, 2),
                new CapturedSubwayVendorStockDefinition(12, 162427, 162428, 9),
                new CapturedSubwayVendorStockDefinition(13, 162426, 162437, 47),
                new CapturedSubwayVendorStockDefinition(14, 162433, 162434, 28),
                new CapturedSubwayVendorStockDefinition(15, 85626, 85625, 7),
                new CapturedSubwayVendorStockDefinition(16, 85730, 85729, 10),
                new CapturedSubwayVendorStockDefinition(17, 85730, 85729, 42),
                new CapturedSubwayVendorStockDefinition(18, 85650, 22108, 26),
                new CapturedSubwayVendorStockDefinition(19, 85692, 22026, 40),
                new CapturedSubwayVendorStockDefinition(20, 85691, 22004, 5),
                new CapturedSubwayVendorStockDefinition(21, 85636, 85635, 47),
                new CapturedSubwayVendorStockDefinition(22, 85655, 22104, 7),
                new CapturedSubwayVendorStockDefinition(23, 85572, 22219, 17),
                new CapturedSubwayVendorStockDefinition(24, 85477, 85477, 1),
                new CapturedSubwayVendorStockDefinition(25, 85638, 85637, 28),
                new CapturedSubwayVendorStockDefinition(26, 85557, 85556, 15),
                new CapturedSubwayVendorStockDefinition(27, 85512, 85511, 41),
                new CapturedSubwayVendorStockDefinition(28, 85686, 85685, 24)
            };
        }

        private static CapturedSubwayVendorStockDefinition[] PharmacistStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 204267, 204267, 1),
                new CapturedSubwayVendorStockDefinition(1, 204267, 204268, 5),
                new CapturedSubwayVendorStockDefinition(2, 204267, 204268, 10),
                new CapturedSubwayVendorStockDefinition(3, 204267, 204268, 15),
                new CapturedSubwayVendorStockDefinition(4, 204267, 204268, 20),
                new CapturedSubwayVendorStockDefinition(5, 204270, 204270, 1),
                new CapturedSubwayVendorStockDefinition(6, 204270, 204271, 5),
                new CapturedSubwayVendorStockDefinition(7, 204270, 204271, 10),
                new CapturedSubwayVendorStockDefinition(8, 204270, 204271, 15),
                new CapturedSubwayVendorStockDefinition(9, 204270, 204271, 20),
                new CapturedSubwayVendorStockDefinition(10, 55697, 55697, 1),
                new CapturedSubwayVendorStockDefinition(11, 55697, 55696, 5),
                new CapturedSubwayVendorStockDefinition(12, 55697, 55696, 10),
                new CapturedSubwayVendorStockDefinition(13, 55697, 55696, 15),
                new CapturedSubwayVendorStockDefinition(14, 55697, 55696, 20),
                new CapturedSubwayVendorStockDefinition(15, 85368, 85368, 1),
                new CapturedSubwayVendorStockDefinition(16, 85368, 85369, 5),
                new CapturedSubwayVendorStockDefinition(17, 85368, 85369, 10),
                new CapturedSubwayVendorStockDefinition(18, 85368, 85369, 15),
                new CapturedSubwayVendorStockDefinition(19, 85368, 85369, 20),
                new CapturedSubwayVendorStockDefinition(20, 204103, 204103, 1),
                new CapturedSubwayVendorStockDefinition(21, 204103, 204104, 5),
                new CapturedSubwayVendorStockDefinition(22, 204103, 204104, 10),
                new CapturedSubwayVendorStockDefinition(23, 204103, 204104, 15),
                new CapturedSubwayVendorStockDefinition(24, 204103, 204104, 20),
                new CapturedSubwayVendorStockDefinition(25, 291082, 291082, 1),
                new CapturedSubwayVendorStockDefinition(26, 291082, 291083, 5),
                new CapturedSubwayVendorStockDefinition(27, 291082, 291083, 10),
                new CapturedSubwayVendorStockDefinition(28, 291082, 291083, 15),
                new CapturedSubwayVendorStockDefinition(29, 291082, 291083, 20),
                new CapturedSubwayVendorStockDefinition(30, 291043, 291043, 1),
                new CapturedSubwayVendorStockDefinition(31, 291043, 291044, 5),
                new CapturedSubwayVendorStockDefinition(32, 291043, 291044, 10),
                new CapturedSubwayVendorStockDefinition(33, 291043, 291044, 15),
                new CapturedSubwayVendorStockDefinition(34, 291043, 291044, 20),
                new CapturedSubwayVendorStockDefinition(35, 43552, 43552, 1),
                new CapturedSubwayVendorStockDefinition(36, 43552, 43551, 5),
                new CapturedSubwayVendorStockDefinition(37, 43552, 43551, 10),
                new CapturedSubwayVendorStockDefinition(38, 43552, 43551, 15),
                new CapturedSubwayVendorStockDefinition(39, 43552, 43551, 20)
            };
        }

        private static CapturedSubwayVendorStockDefinition[] ToolsStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 36778, 36786, 37),
                new CapturedSubwayVendorStockDefinition(1, 300751, 300751, 1),
                new CapturedSubwayVendorStockDefinition(2, 206904, 206904, 1),
                new CapturedSubwayVendorStockDefinition(3, 36782, 36777, 35),
                new CapturedSubwayVendorStockDefinition(4, 95576, 95576, 1),
                new CapturedSubwayVendorStockDefinition(5, 70253, 70252, 35),
                new CapturedSubwayVendorStockDefinition(6, 31837, 31837, 1),
                new CapturedSubwayVendorStockDefinition(7, 87810, 87814, 25),
                new CapturedSubwayVendorStockDefinition(8, 121305, 121304, 35),
                new CapturedSubwayVendorStockDefinition(9, 121306, 121307, 15),
                new CapturedSubwayVendorStockDefinition(10, 121309, 121308, 44),
                new CapturedSubwayVendorStockDefinition(11, 95577, 95577, 1),
                new CapturedSubwayVendorStockDefinition(12, 81757, 81756, 6),
                new CapturedSubwayVendorStockDefinition(13, 81753, 99727, 22),
                new CapturedSubwayVendorStockDefinition(14, 28564, 28564, 1),
                new CapturedSubwayVendorStockDefinition(15, 95514, 95515, 43),
                new CapturedSubwayVendorStockDefinition(16, 161699, 161699, 1),
                new CapturedSubwayVendorStockDefinition(17, 29738, 29738, 1),
                new CapturedSubwayVendorStockDefinition(18, 88373, 88374, 33)
            };
        }

        private static CapturedSubwayVendorStockDefinition[] ContainerStock()
        {
            return new[]
            {
                new CapturedSubwayVendorStockDefinition(0, 99302, 99302, 1),
                new CapturedSubwayVendorStockDefinition(1, 143832, 143832, 1),
                new CapturedSubwayVendorStockDefinition(2, 157684, 157684, 1),
                new CapturedSubwayVendorStockDefinition(3, 157689, 157689, 1),
                new CapturedSubwayVendorStockDefinition(4, 157686, 157686, 1),
                new CapturedSubwayVendorStockDefinition(5, 157691, 157691, 1),
                new CapturedSubwayVendorStockDefinition(6, 157692, 157692, 1),
                new CapturedSubwayVendorStockDefinition(7, 157683, 157683, 1),
                new CapturedSubwayVendorStockDefinition(8, 157693, 157693, 1),
                new CapturedSubwayVendorStockDefinition(9, 157682, 157682, 1),
                new CapturedSubwayVendorStockDefinition(10, 157685, 157685, 1),
                new CapturedSubwayVendorStockDefinition(11, 157688, 157688, 1),
                new CapturedSubwayVendorStockDefinition(12, 157687, 157687, 1),
                new CapturedSubwayVendorStockDefinition(13, 157694, 157694, 1),
                new CapturedSubwayVendorStockDefinition(14, 157695, 157695, 1),
                new CapturedSubwayVendorStockDefinition(15, 157690, 157690, 1),
                new CapturedSubwayVendorStockDefinition(16, 99241, 99241, 1),
                new CapturedSubwayVendorStockDefinition(17, 99228, 99228, 1),
                new CapturedSubwayVendorStockDefinition(18, 287422, 287422, 1),
                new CapturedSubwayVendorStockDefinition(19, 287421, 287421, 1),
                new CapturedSubwayVendorStockDefinition(20, 287423, 287423, 1),
                new CapturedSubwayVendorStockDefinition(21, 287609, 287609, 1),
                new CapturedSubwayVendorStockDefinition(22, 287610, 287610, 1),
                new CapturedSubwayVendorStockDefinition(23, 287427, 287427, 1),
                new CapturedSubwayVendorStockDefinition(24, 287417, 287417, 1),
                new CapturedSubwayVendorStockDefinition(25, 287424, 287424, 1),
                new CapturedSubwayVendorStockDefinition(26, 287425, 287425, 1),
                new CapturedSubwayVendorStockDefinition(27, 287426, 287426, 1),
                new CapturedSubwayVendorStockDefinition(28, 287611, 287611, 1),
                new CapturedSubwayVendorStockDefinition(29, 287428, 287428, 1),
                new CapturedSubwayVendorStockDefinition(30, 287418, 287418, 1),
                new CapturedSubwayVendorStockDefinition(31, 287612, 287612, 1),
                new CapturedSubwayVendorStockDefinition(32, 287613, 287613, 1),
                new CapturedSubwayVendorStockDefinition(33, 287429, 287429, 1),
                new CapturedSubwayVendorStockDefinition(34, 287430, 287430, 1),
                new CapturedSubwayVendorStockDefinition(35, 287614, 287614, 1),
                new CapturedSubwayVendorStockDefinition(36, 287431, 287431, 1),
                new CapturedSubwayVendorStockDefinition(37, 287615, 287615, 1),
                new CapturedSubwayVendorStockDefinition(38, 287432, 287432, 1),
                new CapturedSubwayVendorStockDefinition(39, 287433, 287433, 1),
                new CapturedSubwayVendorStockDefinition(40, 287434, 287434, 1),
                new CapturedSubwayVendorStockDefinition(41, 287435, 287435, 1),
                new CapturedSubwayVendorStockDefinition(42, 287437, 287437, 1),
                new CapturedSubwayVendorStockDefinition(43, 287436, 287436, 1),
                new CapturedSubwayVendorStockDefinition(44, 287616, 287616, 1),
                new CapturedSubwayVendorStockDefinition(45, 287438, 287438, 1),
                new CapturedSubwayVendorStockDefinition(46, 287439, 287439, 1),
                new CapturedSubwayVendorStockDefinition(47, 287419, 287419, 1),
                new CapturedSubwayVendorStockDefinition(48, 287440, 287440, 1),
                new CapturedSubwayVendorStockDefinition(49, 287441, 287441, 1),
                new CapturedSubwayVendorStockDefinition(50, 287617, 287617, 1),
                new CapturedSubwayVendorStockDefinition(51, 287442, 287442, 1),
                new CapturedSubwayVendorStockDefinition(52, 287618, 287618, 1),
                new CapturedSubwayVendorStockDefinition(53, 287420, 287420, 1),
                new CapturedSubwayVendorStockDefinition(54, 287443, 287443, 1),
                new CapturedSubwayVendorStockDefinition(55, 287444, 287444, 1),
                new CapturedSubwayVendorStockDefinition(56, 287445, 287445, 1),
                new CapturedSubwayVendorStockDefinition(57, 287446, 287446, 1),
                new CapturedSubwayVendorStockDefinition(58, 287619, 287619, 1),
                new CapturedSubwayVendorStockDefinition(59, 287447, 287447, 1),
                new CapturedSubwayVendorStockDefinition(60, 287620, 287620, 1),
                new CapturedSubwayVendorStockDefinition(61, 287448, 287448, 1)
            };
        }
    }

    internal sealed class CapturedSubwayVendorDefinition
    {
        internal CapturedSubwayVendorDefinition(
            int sourceNpcInstance,
            int sourceVendorInstance,
            string displayName,
            int vendorTemplateId,
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
            int level,
            int health,
            int runSpeed,
            int characterFlags,
            int visualFlags,
            uint capturedScfuFlags,
            byte[] capturedScfuUnknown1,
            CapturedSubwayVendorTextureDefinition[] textures,
            CapturedSubwayVendorMeshDefinition[] meshes,
            CapturedSubwayVendorWaypointDefinition[] waypoints,
            CapturedSubwayVendorStockDefinition[] stock,
            string evidence,
            string stockEvidence)
        {
            this.SourceNpcInstance = sourceNpcInstance;
            this.SourceVendorInstance = sourceVendorInstance;
            this.DisplayName = displayName;
            this.VendorTemplateId = vendorTemplateId;
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
            this.Level = level;
            this.Health = health;
            this.RunSpeed = runSpeed;
            this.CharacterFlags = characterFlags;
            this.VisualFlags = visualFlags;
            this.CapturedScfuFlags = capturedScfuFlags;
            this.CapturedScfuUnknown1 = Array.AsReadOnly((byte[])capturedScfuUnknown1.Clone());
            this.Textures = Array.AsReadOnly((CapturedSubwayVendorTextureDefinition[])textures.Clone());
            this.Meshes = Array.AsReadOnly((CapturedSubwayVendorMeshDefinition[])meshes.Clone());
            this.Waypoints = Array.AsReadOnly((CapturedSubwayVendorWaypointDefinition[])waypoints.Clone());
            this.Stock = Array.AsReadOnly(
                stock == null
                    ? new CapturedSubwayVendorStockDefinition[0]
                    : (CapturedSubwayVendorStockDefinition[])stock.Clone());
            this.HasCapturedStock = stock != null;
            this.Evidence = evidence;
            this.StockEvidence = stockEvidence ?? string.Empty;
        }

        internal int SourceNpcInstance { get; private set; }
        internal int SourceVendorInstance { get; private set; }
        internal string DisplayName { get; private set; }
        internal int VendorTemplateId { get; private set; }
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
        internal int Level { get; private set; }
        internal int Health { get; private set; }
        internal int RunSpeed { get; private set; }
        internal int CharacterFlags { get; private set; }
        internal int VisualFlags { get; private set; }
        internal uint CapturedScfuFlags { get; private set; }
        internal ReadOnlyCollection<byte> CapturedScfuUnknown1 { get; private set; }
        internal ReadOnlyCollection<CapturedSubwayVendorTextureDefinition> Textures { get; private set; }
        internal ReadOnlyCollection<CapturedSubwayVendorMeshDefinition> Meshes { get; private set; }
        internal ReadOnlyCollection<CapturedSubwayVendorWaypointDefinition> Waypoints { get; private set; }
        internal ReadOnlyCollection<CapturedSubwayVendorStockDefinition> Stock { get; private set; }
        internal bool HasCapturedStock { get; private set; }
        internal string Evidence { get; private set; }
        internal string StockEvidence { get; private set; }
    }

    internal sealed class CapturedSubwayVendorStockDefinition
    {
        internal CapturedSubwayVendorStockDefinition(int slot, int lowId, int highId, int quality)
        {
            this.Slot = slot;
            this.LowId = lowId;
            this.HighId = highId;
            this.Quality = quality;
        }

        internal int Slot { get; private set; }
        internal int LowId { get; private set; }
        internal int HighId { get; private set; }
        internal int Quality { get; private set; }
    }

    internal sealed class CapturedSubwayVendorTextureDefinition
    {
        internal CapturedSubwayVendorTextureDefinition(int place, int id, int unknown)
        {
            this.Place = place;
            this.Id = id;
            this.Unknown = unknown;
        }

        internal int Place { get; private set; }
        internal int Id { get; private set; }
        internal int Unknown { get; private set; }
    }

    internal sealed class CapturedSubwayVendorMeshDefinition
    {
        internal CapturedSubwayVendorMeshDefinition(int position, uint id, int overrideTextureId, int layer)
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

    internal sealed class CapturedSubwayVendorWaypointDefinition
    {
        internal CapturedSubwayVendorWaypointDefinition(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
    }
}
