namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AODB.Common.RDBObjects;
    /// <summary>
    /// Discovers destination doors that become ExitProxy surfaces: TeleportProxy landings with
    /// <see cref="PortalDestination.RecordsReturn"/> and optional playfield-local door allow-lists.
    /// </summary>
    public static class ExitProxyDoorCatalog
    {
        /// <summary>
        /// A configured allow-list selects exit surfaces; unconfigured playfields use portal data.
        /// </summary>
        public static bool ShouldRegister(
            int destinationDoorInstance,
            IReadOnlyCollection<int>? allowedDoorInstances = null)
            => allowedDoorInstances == null || allowedDoorInstances.Contains(destinationDoorInstance);

        /// <summary>
        /// Scans source-playfield dynels for TeleportProxy returns and records their destination
        /// doors under the destination playfield id.
        /// </summary>
        public static void CollectFromDynels(
            PlayfieldDynels? dynels,
            Dictionary<int, HashSet<int>> destinationDoorsByPlayfield,
            Func<int, IReadOnlyCollection<int>?>? allowedDoorInstancesByPlayfield = null)
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

                if (!ShouldRegister(portal.DoorInstance, allowedDoorInstancesByPlayfield?.Invoke(portal.PlayfieldId)))
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
