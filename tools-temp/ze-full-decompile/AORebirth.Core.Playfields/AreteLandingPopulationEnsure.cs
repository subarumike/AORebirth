using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal static class AreteLandingPopulationEnsure
{
	private const int AreteLandingPlayfieldId = 6553;

	private static readonly TimeSpan EnsureInterval = TimeSpan.FromSeconds(5.0);

	private static readonly Dictionary<int, DateTime> NextEnsureUtcByPlayfield = new Dictionary<int, DateTime>();

	public static void ClearPlayfield(int playfieldInstance)
	{
		NextEnsureUtcByPlayfield.Remove(playfieldInstance);
	}

	public static void Tick(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (playfield != null && activateNpc != null && ((Identity)(ref playfieldIdentity)).Instance == 6553)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (!NextEnsureUtcByPlayfield.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value) || !(value > utcNow))
			{
				NextEnsureUtcByPlayfield[((Identity)(ref playfieldIdentity)).Instance] = utcNow + EnsureInterval;
				AreteLandingSpawn.TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
				SurveillanceDroidRuntime.TickEnsurePresent(playfield, playfieldIdentity, activateNpc);
				MarcusPadAmbientCombat.TickRespawn(playfield, playfieldIdentity, activateNpc);
			}
		}
	}
}
