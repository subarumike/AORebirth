namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

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
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            DateTime nextEnsure;
            if (NextEnsureUtcByPlayfield.TryGetValue(playfieldIdentity.Instance, out nextEnsure)
                && nextEnsure > utcNow)
            {
                return;
            }

            NextEnsureUtcByPlayfield[playfieldIdentity.Instance] = utcNow + EnsureInterval;
            AreteLandingSpawn.TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
            SurveillanceDroidRuntime.TickEnsurePresent(playfield, playfieldIdentity, activateNpc);
            MarcusPadAmbientCombat.TickRespawn(playfield, playfieldIdentity, activateNpc);
        }
    }
}
