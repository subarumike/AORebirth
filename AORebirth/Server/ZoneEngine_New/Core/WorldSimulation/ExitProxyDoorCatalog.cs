namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    /// <summary>
    /// Discovers destination doors that become ExitProxy surfaces: TeleportProxy landings with
    /// <see cref="PortalDestination.RecordsReturn"/>. Matches legacy PlayfieldLoader synthesis,
    /// including the Subway restriction that only the confirmed entrance door may exit PF 127.
    /// </summary>
    public static class ExitProxyDoorCatalog
    {
        public const int SubwayPlayfieldId = 127;

        public const int SubwayEntranceDestinationDoorInstance = unchecked((int)0xC006007F);

        /// <summary>
        /// PF 127 has ordinary interior doors; only the confirmed entrance may be an exit surface.
        /// </summary>
        public static bool ShouldRegister(int destinationPlayfieldId, int destinationDoorInstance)
            => destinationPlayfieldId != SubwayPlayfieldId
               || destinationDoorInstance == SubwayEntranceDestinationDoorInstance;

        /// <summary>
        /// Scans source-playfield dynels for TeleportProxy returns and records their destination
        /// doors under the destination playfield id.
        /// </summary>
        public static void CollectFromDynels(
            PlayfieldDynels? dynels,
            Dictionary<int, HashSet<int>> destinationDoorsByPlayfield)
        {
            ArgumentNullException.ThrowIfNull(destinationDoorsByPlayfield);
            List<PlayfieldDynel>? list = dynels?.Dynels;
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (!PortalDoorLandingResolver.TryReadPortal(list[i], out PortalDestination portal))
                    continue;

                if (!portal.RecordsReturn
                    || portal.Kind != PortalLandingKind.DoorDynel
                    || portal.PlayfieldId <= 0
                    || portal.DoorInstance == 0)
                    continue;

                if (!ShouldRegister(portal.PlayfieldId, portal.DoorInstance))
                    continue;

                if (!destinationDoorsByPlayfield.TryGetValue(portal.PlayfieldId, out HashSet<int>? doors))
                {
                    doors = new HashSet<int>();
                    destinationDoorsByPlayfield[portal.PlayfieldId] = doors;
                }

                doors.Add(portal.DoorInstance);
            }
        }
    }
}
