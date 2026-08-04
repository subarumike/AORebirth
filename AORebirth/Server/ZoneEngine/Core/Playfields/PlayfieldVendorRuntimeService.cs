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

        private readonly CapturedHoloDeckVendorRuntimeService capturedHoloDeck =
            new CapturedHoloDeckVendorRuntimeService();

        private readonly CapturedAreteAlexAreaVendorRuntimeService capturedAreteAlexArea =
            new CapturedAreteAlexAreaVendorRuntimeService();

        private readonly CapturedAreteMarcoSpidaVendorRuntimeService capturedAreteMarcoSpida =
            new CapturedAreteMarcoSpidaVendorRuntimeService();

        private readonly CapturedAreteLoreleiVendorRuntimeService capturedAreteLorelei =
            new CapturedAreteLoreleiVendorRuntimeService();

        private readonly CapturedAreteAntonioStacklundVendorRuntimeService capturedAreteAntonio =
            new CapturedAreteAntonioStacklundVendorRuntimeService();

        private readonly CapturedAreteRemiGalloisVendorRuntimeService capturedAreteRemi =
            new CapturedAreteRemiGalloisVendorRuntimeService();

        private readonly CapturedAreteBarryFoodVendorRuntimeService capturedAreteBarry =
            new CapturedAreteBarryFoodVendorRuntimeService();

        private readonly CapturedAreteSarahGreeneVendorRuntimeService capturedAreteSarah =
            new CapturedAreteSarahGreeneVendorRuntimeService();

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
            this.capturedAreteAntonio.Attach(playfield, playfieldIdentity, dynelRegistry);
            this.capturedAreteRemi.Attach(playfield, playfieldIdentity, dynelRegistry);
            this.capturedAreteBarry.Attach(playfield, playfieldIdentity, dynelRegistry);
            this.capturedAreteSarah.Attach(playfield, playfieldIdentity, dynelRegistry);
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
            this.capturedAreteSarah.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteBarry.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteRemi.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteAntonio.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteLorelei.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteMarcoSpida.Clear(playfieldIdentity, dynelRegistry);
            this.capturedAreteAlexArea.Clear(playfieldIdentity, dynelRegistry);
        }
    }
}
