namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Vector;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    public static class SubwayTeleportProxyDestinationRules
    {
        public const int CapturedSubwayPlayfieldId = 127;

        public const int CapturedEntranceDoorInstance = unchecked((int)0xC006007F);

        public const float CapturedEntranceLandingX = 71.4f;

        public const float CapturedEntranceLandingY = 115.6f;

        public const float CapturedEntranceLandingZ = 319.0f;

        public const float CapturedEntranceHeadingX = 0.707102f;

        public const float CapturedEntranceHeadingY = 0.0f;

        public const float CapturedEntranceHeadingZ = 0.707112f;

        public const float CapturedEntranceHeadingW = 0.0f;

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
    }
}
