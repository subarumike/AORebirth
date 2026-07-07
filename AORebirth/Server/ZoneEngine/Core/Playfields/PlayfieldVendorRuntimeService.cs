namespace ZoneEngine.Core.Playfields
{
    using AORebirth.Core.Statels;
    using AORebirth.Core.VendorHandler;
    using AORebirth.Core.Playfields;

    internal sealed class PlayfieldVendorRuntimeService
    {
        internal void SpawnVendors(Playfield playfield, StatelData[] vendorStatels)
        {
            VendorHandler.SpawnVendorsForPlayfield(playfield, vendorStatels);
        }
    }
}
