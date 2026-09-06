namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    public static class WallZoneLandingResolver
    {
        public static bool TryResolve(
            DestinationsCatalog destinations,
            ZoneTriggerVolume wall,
            float factor,
            Vector3 currentPosition,
            out int destPlayfieldId,
            out Vector3 landing)
        {
            destPlayfieldId = wall.DestPlayfieldId;
            landing = currentPosition;
            if (destPlayfieldId <= 0)
                return false;

            if (!destinations.TryGetDestination(destPlayfieldId, wall.DestIndex, out PlayfieldDestination? dest)
                || dest == null)
                return false;

            float newX = ((dest.EndX - dest.StartX) * factor) + dest.StartX;
            float newZ = ((dest.EndZ - dest.StartZ) * factor) + dest.StartZ;
            float dist = MathF.Sqrt(
                ((dest.EndX - dest.StartX) * (dest.EndX - dest.StartX))
                + ((dest.EndZ - dest.StartZ) * (dest.EndZ - dest.StartZ)));
            if (dist > 1e-4f)
            {
                float headX = (dest.EndX - dest.StartX) / dist;
                float headZ = (dest.EndZ - dest.StartZ) / dist;
                newX -= headZ * 8f;
                newZ += headX * 8f;
            }

            landing = new Vector3(newX, (float)currentPosition.y, newZ);
            return true;
        }
    }
}
