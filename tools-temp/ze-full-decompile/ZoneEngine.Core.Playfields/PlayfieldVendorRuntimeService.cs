using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.VendorHandler;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVendorRuntimeService
{
	private readonly CapturedSubwayVendorRuntimeService capturedSubway = new CapturedSubwayVendorRuntimeService();

	private readonly CapturedThrakGardenVendorRuntimeService capturedThrakGarden = new CapturedThrakGardenVendorRuntimeService();

	private readonly CapturedHoloDeckVendorRuntimeService capturedHoloDeck = new CapturedHoloDeckVendorRuntimeService();

	private readonly CapturedAreteAlexAreaVendorRuntimeService capturedAreteAlexArea = new CapturedAreteAlexAreaVendorRuntimeService();

	internal void SpawnVendors(Playfield playfield, StatelData[] vendorStatels)
	{
		VendorHandler.SpawnVendorsForPlayfield((IPlayfield)(object)playfield, vendorStatels);
	}

	internal void SpawnCapturedSubwayVendors(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry, Action<ICharacter> registerNpc)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		capturedSubway.Spawn(playfield, playfieldIdentity, dynelRegistry, registerNpc);
	}

	internal void ClearCapturedSubwayVendors(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		capturedSubway.Clear(playfieldIdentity, dynelRegistry);
	}

	internal void AttachCapturedThrakGardenVendors(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		capturedThrakGarden.Attach(playfield, playfieldIdentity, dynelRegistry);
	}

	internal void ClearCapturedThrakGardenVendors(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		capturedThrakGarden.Clear(playfieldIdentity, dynelRegistry);
	}

	internal void SpawnCapturedHoloDeckVendors(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		capturedHoloDeck.Spawn(playfield, playfieldIdentity, dynelRegistry);
	}

	internal void ClearCapturedHoloDeckVendors(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		capturedHoloDeck.Clear(playfieldIdentity, dynelRegistry);
	}

	internal void SpawnCapturedAreteAlexAreaVendors(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		capturedAreteAlexArea.Spawn(playfield, playfieldIdentity, dynelRegistry);
	}

	internal void ClearCapturedAreteAlexAreaVendors(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		capturedAreteAlexArea.Clear(playfieldIdentity, dynelRegistry);
	}
}
