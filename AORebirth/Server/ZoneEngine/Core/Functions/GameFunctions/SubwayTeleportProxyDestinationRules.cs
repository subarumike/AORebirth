namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Vector;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    public static class SubwayTeleportProxyDestinationRules
    {
        public const int CapturedSubwayPlayfieldId = 127;

        public const int CapturedEntranceDoorInstance = unchecked((int)0xC006007F);

        // Official-live SCFU landing repeated in captures 20260708-004038,
        // 20260708-175514, 20260708-181729, 20260708-182237,
        // 20260709-164219, 20260710-212455, 20260712-155528, and
        // 20260717-012522. Representative row: 20260708-004038 events.log:741
        // (IN-N3 packet #510).
        public const float CapturedEntranceLandingX = 65.80835f;

        public const float CapturedEntranceLandingY = 115.6148f;

        public const float CapturedEntranceLandingZ = 318.9879f;

        public const float CapturedEntranceHeadingX = 0.0f;

        public const float CapturedEntranceHeadingY = 0.7071124f;

        public const float CapturedEntranceHeadingZ = 0.0f;

        public const float CapturedEntranceHeadingW = 0.7071012f;

        public const int CapturedMainExitPlayfieldId = 655;

        public const uint CapturedMainExitExternalDoorInstance = 0xC01A028F;

        // Official-live main-exit landing repeated in captures 20260708-004038,
        // 20260710-211346, 20260710-212455, and 20260712-154941.
        // Representative row: 20260708-004038 events.log:1075
        // (IN-N3 packet #714).
        public const float CapturedMainExitLandingX = 3304.028f;

        public const float CapturedMainExitLandingY = 35.11f;

        public const float CapturedMainExitLandingZ = 837.9951f;

        public const float CapturedMainExitHeadingX = 0.0f;

        public const float CapturedMainExitHeadingY = -0.4771534f;

        public const float CapturedMainExitHeadingZ = 0.0f;

        public const float CapturedMainExitHeadingW = 0.87882f;

        public static bool TryResolveDestinationOverride(
            int destinationPlayfieldId,
            int destinationDoorInstance,
            out Coordinate destination,
            out Quaternion heading)
        {
            if (destinationPlayfieldId == CapturedSubwayPlayfieldId
                && destinationDoorInstance == CapturedEntranceDoorInstance)
            {
                destination = new Coordinate(
                    CapturedEntranceLandingX,
                    CapturedEntranceLandingY,
                    CapturedEntranceLandingZ);
                heading = new Quaternion(
                    CapturedEntranceHeadingX,
                    CapturedEntranceHeadingY,
                    CapturedEntranceHeadingZ,
                    CapturedEntranceHeadingW);
                return true;
            }

            destination = null;
            heading = null;
            return false;
        }

        public static bool TryResolveMainExitOverride(
            int destinationPlayfieldId,
            uint externalDoorInstance,
            out Coordinate destination,
            out Quaternion heading)
        {
            if (destinationPlayfieldId == CapturedMainExitPlayfieldId
                && externalDoorInstance == CapturedMainExitExternalDoorInstance)
            {
                destination = new Coordinate(
                    CapturedMainExitLandingX,
                    CapturedMainExitLandingY,
                    CapturedMainExitLandingZ);
                heading = new Quaternion(
                    CapturedMainExitHeadingX,
                    CapturedMainExitHeadingY,
                    CapturedMainExitHeadingZ,
                    CapturedMainExitHeadingW);
                return true;
            }

            destination = null;
            heading = null;
            return false;
        }
    }
}
