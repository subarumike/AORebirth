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

        private readonly CapturedThrakGardenVendorRuntimeService capturedThrakGarden =
            new CapturedThrakGardenVendorRuntimeService();

        private readonly CapturedAbanGardenVendorRuntimeService capturedAbanGarden =
            new CapturedAbanGardenVendorRuntimeService();

        private readonly CapturedHoloDeckVendorRuntimeService capturedHoloDeck =
            new CapturedHoloDeckVendorRuntimeService();

        private readonly CapturedAreteAlexAreaVendorRuntimeService capturedAreteAlexArea =
            new CapturedAreteAlexAreaVendorRuntimeService();

        private readonly CapturedAreteMarcoSpidaVendorRuntimeService capturedAreteMarcoSpida =
            new CapturedAreteMarcoSpidaVendorRuntimeService();

        private readonly CapturedAreteLoreleiVendorRuntimeService capturedAreteLorelei =
            new CapturedAreteLoreleiVendorRuntimeService();

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

        internal void AttachCapturedThrakGardenVendors(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedThrakGarden.Attach(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void ClearCapturedThrakGardenVendors(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedThrakGarden.Clear(playfieldIdentity, dynelRegistry);
        }

        internal void AttachCapturedAbanGardenVendors(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAbanGarden.Attach(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void ClearCapturedAbanGardenVendors(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAbanGarden.Clear(playfieldIdentity, dynelRegistry);
        }

        internal void SpawnCapturedHoloDeckVendors(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedHoloDeck.Spawn(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void ClearCapturedHoloDeckVendors(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedHoloDeck.Clear(playfieldIdentity, dynelRegistry);
        }

        internal void SpawnCapturedAreteAlexAreaVendors(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAreteAlexArea.Spawn(playfield, playfieldIdentity, dynelRegistry);
            this.AttachCapturedAreteMarcoSpidaVendor(playfield, playfieldIdentity, dynelRegistry);
            this.AttachCapturedAreteLoreleiVendor(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void AttachCapturedAreteMarcoSpidaVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAreteMarcoSpida.Attach(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void AttachCapturedAreteLoreleiVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAreteLorelei.Attach(playfield, playfieldIdentity, dynelRegistry);
        }

        internal void ClearCapturedAreteAlexAreaVendors(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            this.capturedAreteLorelei.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteMarcoSpida.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteAlexArea.Clear(playfieldIdentity, dynelRegistry);
        }
    }
}
