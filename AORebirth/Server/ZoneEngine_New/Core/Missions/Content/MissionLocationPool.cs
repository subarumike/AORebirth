namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// City / marker affiliation for RK mission rolls.
    /// Omni city terminals must not point at Clan markers (Athens / Tir side) and vice versa.
    /// </summary>
    internal enum MissionLocationSide
    {
        Neutral = 0,
        Clan = 1,
        Omni = 2
    }

    /// <summary>
    /// Existing terminal-side and geography rules. Destination placement ownership
    /// belongs solely to MissionDestinationCatalog.
    /// </summary>
    internal static class MissionLocationPool
    {
        /// <summary>
        /// Side of the mission terminal / city the player rolled from (Omni Trade, Rome, Tir, Athens…).
        /// Explicit Neutral cities (Borealis / Newland / Neutral Training) return Neutral.
        /// Wilderness / unknown playfields also return Neutral — use <see cref="TryGetCityAffiliation"/>
        /// to tell city terminals apart from wilderness.
        /// </summary>
        internal static MissionLocationSide ResolveTerminalSide(int playfieldId)
        {
            MissionLocationSide side;
            if (TryGetCityAffiliation(playfieldId, out side))
            {
                return side;
            }

            return MissionLocationSide.Neutral;
        }

        /// <summary>
        /// True when <paramref name="playfieldId"/> is a known RK city / training terminal zone
        /// with a fixed side affiliation (Clan / Omni / Neutral).
        /// </summary>
        internal static bool TryGetCityAffiliation(int playfieldId, out MissionLocationSide side)
        {
            return MissionGeographyContent.Current.Cities.TryGetValue(playfieldId, out side);
        }

        /// <summary>
        /// Omni terminals: Omni only. Clan terminals: Clan only.
        /// Neutral city terminals: Omni and Clan (and Neutral chars) can roll.
        /// Wilderness terminals stay open.
        /// </summary>
        internal static bool CanCharacterRollAtTerminal(MissionLocationSide characterSide, int terminalPlayfieldId)
        {
            MissionLocationSide citySide;
            if (!TryGetCityAffiliation(terminalPlayfieldId, out citySide))
            {
                return true;
            }

            if (citySide == MissionLocationSide.Neutral)
            {
                return true;
            }

            return characterSide == citySide;
        }

        /// <summary>
        /// Yellow chat when a wrong-side character tries an Omni/Clan city terminal.
        /// </summary>
        internal static string FormatSideRestrictedRollFeedback(int terminalPlayfieldId)
        {
            MissionLocationSide citySide;
            if (!TryGetCityAffiliation(terminalPlayfieldId, out citySide))
            {
                return "This mission terminal is not available for your side.";
            }

            if (citySide == MissionLocationSide.Omni)
            {
                return "Only Omni side can roll missions!";
            }

            if (citySide == MissionLocationSide.Clan)
            {
                return "Only Clan side can roll missions!";
            }

            return "This mission terminal is not available for your side.";
        }

        internal static MissionLocationSide ResolveCharacterSide(int sideStatValue)
        {
            if (sideStatValue == (int)Side.Clan)
            {
                return MissionLocationSide.Clan;
            }

            if (sideStatValue == (int)Side.Omni)
            {
                return MissionLocationSide.Omni;
            }

            return MissionLocationSide.Neutral;
        }

        /// <summary>
        /// Outdoor marker playfield affiliation. Neutral markers are valid for either side.
        /// </summary>
        internal static MissionLocationSide ResolveMarkerSide(int playfieldId)
        {
            return MissionGeographyContent.Current.Markers.TryGetValue(playfieldId, out var side) ? side : MissionLocationSide.Neutral;
        }

        /// <summary>
        /// Approximate outdoor travel meters between a terminal city playfield and a marker playfield.
        /// Same-playfield markers use real XYZ; cross-playfield uses city→zone travel tiers.
        /// </summary>
        internal static double ApproxTravelMeters(int terminalPlayfieldId, int markerPlayfieldId)
        {
            if (terminalPlayfieldId == 0 || markerPlayfieldId == 0)
            {
                return 3500.0;
            }

            if (terminalPlayfieldId == markerPlayfieldId)
            {
                return 0.0;
            }

            // City / training terminals: prefer explicit near-zone outdoor markers over coarse tier gaps.
            int nearRank = NearClusterRank(terminalPlayfieldId, markerPlayfieldId);
            if (nearRank == 1)
            {
                return 800.0;
            }

            if (nearRank == 2)
            {
                return 1600.0;
            }

            int fromTier = TravelTier(terminalPlayfieldId);
            int toTier = TravelTier(markerPlayfieldId);
            int delta = fromTier > toTier ? fromTier - toTier : toTier - fromTier;
            switch (delta)
            {
                case 0:
                    return 1100.0;
                case 1:
                    return 2400.0;
                case 2:
                    return 4200.0;
                case 3:
                    return 6500.0;
                default:
                    return 9000.0;
            }
        }

        /// <summary>
        /// 0 = same playfield, 1 = immediate near-zone for that city, 2 = next outdoor ring, else -1.
        /// Low-level rolls should stay in rank 0 (or 1 when the city has no marker spots).
        /// </summary>
        internal static int NearClusterRank(int terminalPlayfieldId, int markerPlayfieldId)
        {
            if (terminalPlayfieldId == 0 || markerPlayfieldId == 0) return -1;
            if (terminalPlayfieldId == markerPlayfieldId) return 0;
            var cluster = MissionGeographyContent.Current.Clusters.SingleOrDefault(value => value.From.Contains(terminalPlayfieldId));
            if (cluster != null) return cluster.Near.Contains(markerPlayfieldId) ? 1 : cluster.Next.Contains(markerPlayfieldId) ? 2 : -1;
            int delta = Math.Abs(TravelTier(terminalPlayfieldId) - TravelTier(markerPlayfieldId));
            return delta == 0 ? 1 : delta == 1 ? 2 : -1;
        }

        /// <summary>
        /// 0 = city / training, 1 = near county, 2 = mid wilderness, 3 = far, 4 = remote.
        /// </summary>
        private static int TravelTier(int playfieldId)
        {
            return MissionGeographyContent.Current.TravelTiers.TryGetValue(playfieldId, out var tier) ? tier : 2;
        }

        /// <summary>
        /// Omni terminals → Omni + Neutral markers; Clan → Clan + Neutral; Neutral city → Neutral only.
        /// </summary>
        internal static bool IsSpotAllowedForTerminal(int markerPlayfieldId, MissionLocationSide terminalSide)
        {
            MissionLocationSide markerSide = ResolveMarkerSide(markerPlayfieldId);
            if (terminalSide == MissionLocationSide.Neutral)
            {
                return markerSide == MissionLocationSide.Neutral;
            }

            return markerSide == MissionLocationSide.Neutral || markerSide == terminalSide;
        }

    }
}
