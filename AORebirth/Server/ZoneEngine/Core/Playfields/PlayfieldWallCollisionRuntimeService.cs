namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Interfaces;

    using Utility;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using RuntimeQuaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    internal sealed class PlayfieldWallCollisionRuntimeService
    {
        internal void CheckWallCollision(
            ICharacter dynel,
            Func<ICharacter, bool> isPostZoneCollisionGraceActive,
            Action<Dynel, Coordinate, RuntimeQuaternion, int> teleportToPlayfield)
        {
            if (isPostZoneCollisionGraceActive(dynel))
            {
                return;
            }

            if (!PlayfieldLoader.PFData.ContainsKey(dynel.Playfield.Identity.Instance))
            {
                return;
            }

            WallCollisionResult wcr = WallCollision.CheckCollision(
                dynel.Coordinates(),
                dynel.Playfield.Identity.Instance);
            if (wcr != null)
            {
                int destPlayfield = wcr.SecondWall.DestinationPlayfield;
                if (destPlayfield > 0)
                {
                    LogUtil.Debug(DebugInfoDetail.Zoning, wcr.ToString());

                    if (!PlayfieldLoader.PFData.ContainsKey(destPlayfield))
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Wall collision ignored character={0} fromPlayfield={1} missingDestinationPlayfield={2}",
                                dynel.Identity.ToString(true),
                                dynel.Playfield.Identity.Instance,
                                destPlayfield));
                        return;
                    }

                    PlayfieldData destinationPlayfieldData = PlayfieldLoader.PFData[destPlayfield];
                    byte destinationIndex = wcr.SecondWall.DestinationIndex;
                    // Destinations is Dictionary<byte, PlayfieldDestination> keyed by destinationIndex
                    // (sparse). Do NOT compare against .Count — that broke Jobe→Nascense (index 4,
                    // count 3) after the June 7 bounds guard. Match lineteleport.cs: TryGetValue.
                    PlayfieldDestination dest;
                    if (!destinationPlayfieldData.Destinations.TryGetValue(destinationIndex, out dest)
                        || dest == null)
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Wall collision ignored character={0} fromPlayfield={1} fromCoords={2:F1},{3:F1},{4:F1} toPlayfield={5} missingDestinationIndex={6} destinationCount={7}",
                                dynel.Identity.ToString(true),
                                dynel.Playfield.Identity.Instance,
                                dynel.RawCoordinates.X,
                                dynel.RawCoordinates.Y,
                                dynel.RawCoordinates.Z,
                                destPlayfield,
                                destinationIndex,
                                destinationPlayfieldData.Destinations.Count));
                        return;
                    }

                    LogUtil.Debug(DebugInfoDetail.Zoning, dest.ToString());

                    float newX = (dest.EndX - dest.StartX) * wcr.Factor + dest.StartX;
                    float newZ = (dest.EndZ - dest.StartZ) * wcr.Factor + dest.StartZ;
                    float dist = WallCollision.Distance(dest.StartX, dest.StartZ, dest.EndX, dest.EndZ);
                    float headDistX = (dest.EndX - dest.StartX) / dist;
                    float headDistZ = (dest.EndZ - dest.StartZ) / dist;
                    newX -= headDistZ * 8;
                    newZ += headDistX * 8;

                    Coordinate destinationCoordinate = new Coordinate(newX, dynel.RawCoordinates.Y, newZ);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Wall collision zoning character={0} fromPlayfield={1} fromCoords={2:F1},{3:F1},{4:F1} toPlayfield={5} toCoords={6:F1},{7:F1},{8:F1}",
                            dynel.Identity.ToString(true),
                            dynel.Playfield.Identity.Instance,
                            dynel.RawCoordinates.X,
                            dynel.RawCoordinates.Y,
                            dynel.RawCoordinates.Z,
                            destPlayfield,
                            destinationCoordinate.x,
                            destinationCoordinate.y,
                            destinationCoordinate.z));

                    teleportToPlayfield(
                        (Dynel)dynel,
                        destinationCoordinate,
                        dynel.RawHeading,
                        destPlayfield);
                    return;
                }
            }
        }
    }
}
