namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Proxy playfield instances: an interior or backyard (a shop, a training area, a backyard building) reached through
    /// a walk-in door that records a way back gets its own copy per source door, as live did. The copy's id is derived from the door, so it is the same on every
    /// zone instance and after a restart with no table: <see cref="First"/> + the door instance's low 24 bits (door
    /// index in bits 16-23, source playfield in bits 0-15). The copy loads the destination playfield's data
    /// (<see cref="Playfield.ResourceId"/>) under its own identity.
    /// </summary>
    public static class ProxyPlayfieldIds
    {
        /// <summary>First proxy instance id: the top 24-bit block of positive ints, above RDB ids, retired mission ids and quest dungeons.</summary>
        public const int First = 0x7F000000;

        public static bool IsProxy(int playfieldId) => playfieldId >= First;

        /// <summary>
        /// The instance id for the interior behind <paramref name="sourceDoorInstance"/> on <paramref name="sourcePlayfieldId"/>.
        /// False when the door's instance does not carry its playfield in the low 16 bits (every placed door does).
        /// </summary>
        public static bool TryCreate(int sourcePlayfieldId, int sourceDoorInstance, out int proxyPlayfieldId)
        {
            proxyPlayfieldId = 0;
            if (sourcePlayfieldId <= 0 || sourcePlayfieldId > 0xFFFF || sourceDoorInstance == 0
                || (sourceDoorInstance & 0xFFFF) != sourcePlayfieldId)
                return false;

            proxyPlayfieldId = First + (sourceDoorInstance & 0x00FFFFFF);
            return true;
        }

        /// <summary>Source playfield and the door's low 24 bits, from an instance id.</summary>
        public static bool TryDecode(int proxyPlayfieldId, out int sourcePlayfieldId, out int doorLow24)
        {
            sourcePlayfieldId = 0;
            doorLow24 = 0;
            if (!IsProxy(proxyPlayfieldId))
                return false;
            doorLow24 = proxyPlayfieldId - First;
            sourcePlayfieldId = doorLow24 & 0xFFFF;
            return sourcePlayfieldId > 0;
        }

        /// <summary>
        /// Every RDB playfield behind a walk-in door that records a return is instanced. Across the client data those
        /// are 133 playfields: 130 indoor interiors and the three training backyards (950, 952, 954), which live
        /// instanced as well although their metadata calls them outdoor. Doors into shared zones are one-way
        /// TeleportProxy2 / LineTeleport portals and never reach here.
        /// </summary>
        public static bool ShouldInstance(IGameData gameData, int destinationPlayfieldId)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            return destinationPlayfieldId > 0 && destinationPlayfieldId <= 0xFFFF;
        }

        /// <summary>
        /// The playfield a door-use or a door crossing should open: the per-door instance when the door records a way
        /// back into an RDB playfield, else <paramref name="destinationPlayfieldId"/>.
        /// </summary>
        public static int ResolveTarget(IGameData gameData, int sourcePlayfieldId, int sourceDoorInstance, int destinationPlayfieldId)
        {
            if (TryCreate(sourcePlayfieldId, sourceDoorInstance, out int proxy) && ShouldInstance(gameData, destinationPlayfieldId))
                return proxy;
            return destinationPlayfieldId;
        }

        /// <summary>
        /// A TeleportRoutes.json route on a door replaces its portal's destination, when it names a door of the routed
        /// playfield (0xC0nnPPPP, PPPP the playfield). The same rule as the door-use path.
        /// </summary>
        public static bool TryApplyRoute(IGameData gameData, int sourcePlayfieldId, int sourceDoorInstance, ref int destinationPlayfieldId, ref int destinationDoorInstance)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            if (sourceDoorInstance == 0
                || !gameData.TryGetTeleportRoute(sourcePlayfieldId, (int)IdentityType.Door, unchecked((uint)sourceDoorInstance),
                    out int routedPlayfield, out int routedType, out uint routedInstance))
                return false;

            if (routedType != (int)IdentityType.Door
                || routedPlayfield <= 0
                || routedPlayfield > 0xFFFF
                || (routedInstance & 0xFF000000u) != 0xC0000000u
                || (routedInstance & 0xFFFFu) != (uint)routedPlayfield)
                return false;

            destinationPlayfieldId = routedPlayfield;
            destinationDoorInstance = unchecked((int)routedInstance);
            return true;
        }

        /// <summary>
        /// The interior an instance id stands for: finds the source door in the source playfield's dynels, reads its
        /// portal and applies any route. False when the door is gone or no longer records a return into an RDB playfield.
        /// </summary>
        public static bool TryResolveResource(IGameData gameData, int proxyPlayfieldId, out int resourcePlayfieldId, out int sourcePlayfieldId, out int sourceDoorInstance)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            resourcePlayfieldId = 0;
            sourceDoorInstance = 0;
            if (!TryDecode(proxyPlayfieldId, out sourcePlayfieldId, out int doorLow24))
                return false;

            List<PlayfieldDynel>? dynels = gameData.GetPlayfieldGeometry(sourcePlayfieldId).Dynels?.Dynels;
            if (dynels == null)
                return false;

            for (int i = 0; i < dynels.Count; i++)
            {
                PlayfieldDynel dynel = dynels[i];
                if (dynel.IdentityType != (int)IdentityType.Door || (dynel.IdentityInstance & 0x00FFFFFF) != doorLow24)
                    continue;

                int destination = 0;
                int destinationDoor = 0;
                // A vicinity-fired TeleportProxy instances its interior the same way (ItemUseFunctions.TeleportProxy
                // runs ResolveTarget for both), so it must resolve here too or the instance can never be created.
                if (PortalDoorLandingResolver.TryReadPortal(dynel, out PortalDestination portal)
                    || PortalDoorLandingResolver.TryReadVicinityProxy(dynel, out portal))
                {
                    if (portal.Kind != PortalLandingKind.DoorDynel || !portal.RecordsReturn)
                        continue;
                    destination = portal.PlayfieldId;
                    destinationDoor = portal.DoorInstance;
                    TryApplyRoute(gameData, sourcePlayfieldId, dynel.IdentityInstance, ref destination, ref destinationDoor);
                }
                // A plain door with a teleports route is an entrance too (PlayfieldWorldSimulation.BakeExitDoorTriggers).
                else if (!TryApplyRoute(gameData, sourcePlayfieldId, dynel.IdentityInstance, ref destination, ref destinationDoor))
                    continue;

                if (!ShouldInstance(gameData, destination))
                    return false;

                resourcePlayfieldId = destination;
                sourceDoorInstance = dynel.IdentityInstance;
                return true;
            }

            return false;
        }
    }
}
