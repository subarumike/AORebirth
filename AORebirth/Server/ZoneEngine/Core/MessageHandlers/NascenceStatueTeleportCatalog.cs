namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public static class NascenceStatueTeleportCatalog
    {
        public const int CapturedTharakInsigniaTemplateId = 281129;

        public const int CapturedThrakStatueTemplateId = 222955;

        public const int UnredeemedGardenPlayfieldId = 4677;

        public const int RedeemedGardenPlayfieldId = 4676;

        public const int OldFrontierPlayfieldId = 4310;

        public const int FrontierBorderPlayfieldId = 4311;

        public const int MistyDreamsPlayfieldId = 4312;

        private const int GardenPassageTerminalSuffixMask = 0xFF;

        private static readonly HashSet<int> GardenPassageTerminalSuffixes =
            new HashSet<int>
            {
                0xC0EC,
                0xC0ED,
                0xC0EE,
                0xC0EF,
                0xC0F0,
                0xC0F1,
                0xC0F2,
                0xC0F3
            };

        private static readonly HashSet<int> ZoneThrakStatueTerminalInstances =
            new HashSet<int>
            {
                unchecked((int)0x570442E0),
                unchecked((int)0x570442E1),
                unchecked((int)0x570442E7),
                unchecked((int)0x57399927),
                unchecked((int)0x5739992A),
                unchecked((int)0x5739992C),
                unchecked((int)0x57480806),
                unchecked((int)0x57480808)
            };

        private static readonly NascenceGardenPassageRoute[] GardenPassageRoutes =
        {
            new NascenceGardenPassageRoute(
                0xC0EC,
                MistyDreamsPlayfieldId,
                1544f,
                52.83f,
                680f,
                "20260716-nascense-statues Passage to Misty Dreams Border"),
            new NascenceGardenPassageRoute(
                0xC0ED,
                MistyDreamsPlayfieldId,
                1630f,
                43.96f,
                1469f,
                "20260716-nascense-statues Passage to The Core"),
            new NascenceGardenPassageRoute(
                0xC0EE,
                FrontierBorderPlayfieldId,
                274f,
                77.716f,
                1665f,
                "20260716-nascense-statues Passage to Mil"),
            new NascenceGardenPassageRoute(
                0xC0EF,
                FrontierBorderPlayfieldId,
                242f,
                105.01f,
                1035f,
                "20260716-nascense-statues Passage to Brawl"),
            new NascenceGardenPassageRoute(
                0xC0F0,
                FrontierBorderPlayfieldId,
                608f,
                13.81f,
                556f,
                "20260716-nascense-statues Passage to Frontier Border"),
            new NascenceGardenPassageRoute(
                0xC0F1,
                OldFrontierPlayfieldId,
                792f,
                31.81f,
                1149f,
                "20260716-nascense-statues Passage to Frontier Bridge"),
            new NascenceGardenPassageRoute(
                0xC0F2,
                OldFrontierPlayfieldId,
                858f,
                31.52f,
                1479f,
                "20260716-nascense-statues Passage to Old Frontier"),
            new NascenceGardenPassageRoute(
                0xC0F3,
                OldFrontierPlayfieldId,
                684f,
                29.41f,
                1898f,
                "20260716-nascense-statues Passage to Frontier Outskirts")
        };

        public static bool IsNascenceGardenPlayfield(int playfieldId)
        {
            return playfieldId == UnredeemedGardenPlayfieldId
                   || playfieldId == RedeemedGardenPlayfieldId;
        }

        public static bool IsNascenceZonePlayfield(int playfieldId)
        {
            return playfieldId == OldFrontierPlayfieldId
                   || playfieldId == FrontierBorderPlayfieldId
                   || playfieldId == MistyDreamsPlayfieldId;
        }

        public static bool IsGardenPassageTerminal(int playfieldId, int terminalInstance)
        {
            if (!IsNascenceGardenPlayfield(playfieldId))
            {
                return false;
            }

            return GardenPassageTerminalSuffixes.Contains(terminalInstance & GardenPassageTerminalSuffixMask);
        }

        public static bool TryGetGardenPassageRoute(
            int playfieldId,
            int terminalInstance,
            out NascenceGardenPassageRoute route)
        {
            route = null;
            if (!IsGardenPassageTerminal(playfieldId, terminalInstance))
            {
                return false;
            }

            int suffix = terminalInstance & GardenPassageTerminalSuffixMask;
            for (int index = 0; index < GardenPassageRoutes.Length; index++)
            {
                if (GardenPassageRoutes[index].TerminalSuffix == suffix)
                {
                    route = GardenPassageRoutes[index];
                    return true;
                }
            }

            return false;
        }

        public static bool IsZoneThrakStatueTerminal(int playfieldId, int terminalInstance)
        {
            if (!IsNascenceZonePlayfield(playfieldId))
            {
                return false;
            }

            return ZoneThrakStatueTerminalInstances.Contains(terminalInstance);
        }

        public static bool IsReturnKeyItemTemplate(int templateId)
        {
            return templateId == CapturedTharakInsigniaTemplateId;
        }

        public static bool IsThrakStatueTemplate(int templateId)
        {
            return templateId == CapturedThrakStatueTemplateId;
        }

        public static int ResolveReturnGardenPlayfieldId(int otUnredeemedValue)
        {
            return otUnredeemedValue != 0 ? UnredeemedGardenPlayfieldId : RedeemedGardenPlayfieldId;
        }

        public static void ResolveReturnGardenPosition(
            int gardenPlayfieldId,
            out float x,
            out float y,
            out float z)
        {
            if (gardenPlayfieldId == RedeemedGardenPlayfieldId)
            {
                x = 360.958f;
                y = 119.13f;
                z = 361.896f;
                return;
            }

            x = 462f;
            y = 45.585f;
            z = 421f;
        }
    }

    public sealed class NascenceGardenPassageRoute
    {
        public NascenceGardenPassageRoute(
            int terminalSuffix,
            int destinationPlayfieldId,
            float destinationX,
            float destinationY,
            float destinationZ,
            string evidence)
        {
            this.TerminalSuffix = terminalSuffix;
            this.DestinationPlayfieldId = destinationPlayfieldId;
            this.DestinationX = destinationX;
            this.DestinationY = destinationY;
            this.DestinationZ = destinationZ;
            this.Evidence = evidence;
        }

        public int TerminalSuffix { get; private set; }

        public int DestinationPlayfieldId { get; private set; }

        public float DestinationX { get; private set; }

        public float DestinationY { get; private set; }

        public float DestinationZ { get; private set; }

        public string Evidence { get; private set; }
    }
}
