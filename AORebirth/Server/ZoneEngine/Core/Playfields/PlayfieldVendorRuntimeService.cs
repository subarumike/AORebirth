namespace ZoneEngine.Core.Playfields
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.VendorHandler;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldVendorRuntimeService
    {
        private readonly CapturedSubwayVendorRuntimeService capturedSubway =
            new CapturedSubwayVendorRuntimeService();

        internal void SpawnVendors(Playfield playfield, StatelData[] vendorStatels)
        {
            VendorHandler.SpawnVendorsForPlayfield(playfield, vendorStatels);
        }

        internal void SpawnCapturedSubwayVendors(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> registerNpc)
        {
            this.capturedSubway.Spawn(playfield, playfieldIdentity, dynelRegistry, registerNpc);
        }

        internal void ClearCapturedSubwayVendors(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedSubway.Clear(playfieldIdentity, dynelRegistry);
        }
    }
}
