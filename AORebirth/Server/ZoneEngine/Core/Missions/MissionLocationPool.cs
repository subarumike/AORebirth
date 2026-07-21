namespace ZoneEngine.Core.Missions
{
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
    /// Capture-backed RK mission marker locations (playfield + XYZ + entrance ids)
    /// extracted from live QuestAlternative rolls in capture 20260718-053650.
    /// </summary>
    internal static class MissionLocationPool
    {
        internal sealed class Spot
        {
            public int Playfield;
            public float X;
            public float Y;
            public float Z;
            public int EntranceLow;
            public int EntranceHigh;
        }

        /// <summary>
        /// Side of the mission terminal / city the player rolled from (Omni Trade, Rome, Tir, Athens…).
        /// Wilderness / unknown playfields stay Neutral (full pool).
        /// </summary>
        internal static MissionLocationSide ResolveTerminalSide(int playfieldId)
        {
            switch (playfieldId)
            {
                // Clan cities / training
                case 540: // Old Athen
                case 545: // West Athens
                case 640: // Tir
                case 641: // Tir Arena
                case 646: // Tir County
                case 647: // Greater Tir County
                case 952: // Clan Training
                case 953: // Clan Backyard
                    return MissionLocationSide.Clan;

                // Omni cities / Rome / training
                case 700: // Omni1-HEADQUARTER
                case 705: // Omni1 Entertainment
                case 706: // Omni Entertainment Arena
                case 710: // Omni-1 Trade
                case 716: // Omni Forest
                case 717: // Greater Omni Forest
                case 730: // Rome Park
                case 735: // Rome Blue
                case 740: // Rome Green
                case 950: // Omni Training
                    return MissionLocationSide.Omni;

                default:
                    return MissionLocationSide.Neutral;
            }
        }

        /// <summary>
        /// Outdoor marker playfield affiliation. Neutral markers are valid for either side.
        /// </summary>
        internal static MissionLocationSide ResolveMarkerSide(int playfieldId)
        {
            switch (playfieldId)
            {
                // Clan / Athens–Tir side
                case 545: // West Athens
                case 550: // Athen Shire
                case 551: // Wailing Wastes
                case 585: // Aegean
                case 586: // Wartorn Valley
                case 600: // Varmint Woods (Tir)
                    return MissionLocationSide.Clan;

                // Omni / southern RK side
                case 635: // Stret East Bank
                case 650: // Upper Stret East Bank
                case 655: // Andromeda
                case 665: // Broken Shores
                case 670: // Clondyke
                case 685: // Galway County
                case 695: // Lush Fields
                case 696: // Mutant Domain
                case 760: // 4 Holes
                case 790: // Stret West Bank
                case 791: // Holes in the Wall
                case 795: // The Longest Road
                    return MissionLocationSide.Omni;

                default:
                    return MissionLocationSide.Neutral;
            }
        }

        /// <summary>
        /// Omni terminals may use Omni + Neutral markers; Clan terminals Clan + Neutral.
        /// Neutral terminals (or unknown) keep the full pool.
        /// </summary>
        internal static bool IsSpotAllowedForTerminal(int markerPlayfieldId, MissionLocationSide terminalSide)
        {
            if (terminalSide == MissionLocationSide.Neutral)
            {
                return true;
            }

            MissionLocationSide markerSide = ResolveMarkerSide(markerPlayfieldId);
            return markerSide == MissionLocationSide.Neutral || markerSide == terminalSide;
        }

        internal static readonly Spot[] Spots =
        {
            new Spot { Playfield = 791, X = 251.943F, Y = 6.817F, Z = 1647.62F, EntranceLow = 38080, EntranceHigh = 37660 },
            new Spot { Playfield = 795, X = 2008.322F, Y = 17.356F, Z = 675.536F, EntranceLow = 33859, EntranceHigh = 39226 },
            new Spot { Playfield = 790, X = 1172.884F, Y = 1.748F, Z = 2883.817F, EntranceLow = 38061, EntranceHigh = 36351 },
            new Spot { Playfield = 550, X = 2649.489F, Y = 17.537F, Z = 202.555F, EntranceLow = 37084, EntranceHigh = 39822 },
            new Spot { Playfield = 545, X = 466.752F, Y = 4.732F, Z = 393.448F, EntranceLow = 39122, EntranceHigh = 40646 },
            new Spot { Playfield = 585, X = 443.524F, Y = 16.812F, Z = 366.69F, EntranceLow = 39720, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1496.656F, Y = 32.095F, Z = 1183.757F, EntranceLow = 38486, EntranceHigh = 33943 },
            new Spot { Playfield = 551, X = 2526.175F, Y = 21.505F, Z = 3022.011F, EntranceLow = 37078, EntranceHigh = 41004 },
            new Spot { Playfield = 600, X = 1336.069F, Y = 16.756F, Z = 911.098F, EntranceLow = 41975, EntranceHigh = 39147 },
            new Spot { Playfield = 650, X = 1774.032F, Y = 19.234F, Z = 2217.048F, EntranceLow = 40865, EntranceHigh = 36043 },
            new Spot { Playfield = 655, X = 776.689F, Y = 6.109F, Z = 611.184F, EntranceLow = 37181, EntranceHigh = 31310 },
            new Spot { Playfield = 685, X = 809.697F, Y = 31.568F, Z = 2754.562F, EntranceLow = 34494, EntranceHigh = 29814 },
            new Spot { Playfield = 505, X = 2790.56F, Y = 18.913F, Z = 1411.434F, EntranceLow = 35213, EntranceHigh = 44565 },
            new Spot { Playfield = 625, X = 1622.992F, Y = 76.516F, Z = 2644.911F, EntranceLow = 41891, EntranceHigh = 30613 },
            new Spot { Playfield = 595, X = 1319.505F, Y = 12.73F, Z = 1123.048F, EntranceLow = 46218, EntranceHigh = 35171 },
            new Spot { Playfield = 665, X = 731.364F, Y = 26.213F, Z = 1430.468F, EntranceLow = 31001, EntranceHigh = 29383 },
            new Spot { Playfield = 670, X = 1509.753F, Y = 45.138F, Z = 1553.929F, EntranceLow = 37169, EntranceHigh = 27031 },
            new Spot { Playfield = 695, X = 1469.645F, Y = 38.906F, Z = 636.102F, EntranceLow = 39961, EntranceHigh = 27027 },
            new Spot { Playfield = 630, X = 808.716F, Y = 15.805F, Z = 2272.775F, EntranceLow = 43498, EntranceHigh = 27918 },
            new Spot { Playfield = 696, X = 1058.199F, Y = 7.605F, Z = 403.672F, EntranceLow = 41950, EntranceHigh = 27705 },
            new Spot { Playfield = 570, X = 1127.185F, Y = 39.009F, Z = 1135.309F, EntranceLow = 45485, EntranceHigh = 45363 },
            new Spot { Playfield = 565, X = 790.341F, Y = 46.187F, Z = 1579.832F, EntranceLow = 41958, EntranceHigh = 42152 },
            new Spot { Playfield = 635, X = 1586.889F, Y = 11.501F, Z = 2130.275F, EntranceLow = 40889, EntranceHigh = 33040 },
            new Spot { Playfield = 586, X = 311.304F, Y = 15.78F, Z = 933.627F, EntranceLow = 39743, EntranceHigh = 40341 },
            new Spot { Playfield = 791, X = 202.272F, Y = 12.799F, Z = 1784.574F, EntranceLow = 38081, EntranceHigh = 37660 },
            new Spot { Playfield = 795, X = 3850.318F, Y = 6.387F, Z = 1386.086F, EntranceLow = 33859, EntranceHigh = 39226 },
            new Spot { Playfield = 790, X = 1255.864F, Y = 1.572F, Z = 2830.502F, EntranceLow = 38061, EntranceHigh = 36352 },
            new Spot { Playfield = 550, X = 1593.722F, Y = 37.132F, Z = 311.454F, EntranceLow = 37085, EntranceHigh = 39822 },
            new Spot { Playfield = 545, X = 379.785F, Y = 13.862F, Z = 452.217F, EntranceLow = 39122, EntranceHigh = 40646 },
            new Spot { Playfield = 585, X = 563.783F, Y = 71.113F, Z = 1085.991F, EntranceLow = 39720, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1673.983F, Y = 31.056F, Z = 1951.098F, EntranceLow = 38486, EntranceHigh = 33942 },
            new Spot { Playfield = 551, X = 2434.97F, Y = 24.355F, Z = 3360.971F, EntranceLow = 37077, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 720.316F, Y = 30.884F, Z = 2231.831F, EntranceLow = 41975, EntranceHigh = 39148 },
            new Spot { Playfield = 650, X = 1756.964F, Y = 8.441F, Z = 2937.478F, EntranceLow = 40866, EntranceHigh = 36043 },
            new Spot { Playfield = 655, X = 2553.794F, Y = 14.08F, Z = 2568.971F, EntranceLow = 37181, EntranceHigh = 31311 },
            new Spot { Playfield = 685, X = 1963.619F, Y = 49.216F, Z = 790.617F, EntranceLow = 34494, EntranceHigh = 29814 },
            new Spot { Playfield = 505, X = 1951.886F, Y = 32.691F, Z = 2075.415F, EntranceLow = 35214, EntranceHigh = 44565 },
            new Spot { Playfield = 625, X = 3655.649F, Y = 40.931F, Z = 877.994F, EntranceLow = 41890, EntranceHigh = 30614 },
            new Spot { Playfield = 595, X = 1187.595F, Y = 18.627F, Z = 2581.574F, EntranceLow = 46218, EntranceHigh = 35171 },
            new Spot { Playfield = 670, X = 1602.884F, Y = 20.104F, Z = 1019.075F, EntranceLow = 37168, EntranceHigh = 27030 },
            new Spot { Playfield = 695, X = 3287.12F, Y = 21.324F, Z = 856.316F, EntranceLow = 39962, EntranceHigh = 27028 },
            new Spot { Playfield = 565, X = 2761.952F, Y = 18.643F, Z = 1603.984F, EntranceLow = 41959, EntranceHigh = 42152 },
            new Spot { Playfield = 635, X = 1370.519F, Y = 30.355F, Z = 2618.614F, EntranceLow = 40889, EntranceHigh = 33040 },
            new Spot { Playfield = 795, X = 3508.681F, Y = 11.716F, Z = 1476.082F, EntranceLow = 33859, EntranceHigh = 39226 },
            new Spot { Playfield = 790, X = 1383.196F, Y = 10.717F, Z = 1924.584F, EntranceLow = 38061, EntranceHigh = 36351 },
            new Spot { Playfield = 550, X = 1540.457F, Y = 38.611F, Z = 314.573F, EntranceLow = 37084, EntranceHigh = 39823 },
            new Spot { Playfield = 545, X = 541.333F, Y = 24.006F, Z = 553.031F, EntranceLow = 39121, EntranceHigh = 40646 },
            new Spot { Playfield = 585, X = 2071.823F, Y = 30.421F, Z = 734.167F, EntranceLow = 39721, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1678.967F, Y = 30.954F, Z = 2061.281F, EntranceLow = 38486, EntranceHigh = 33943 },
            new Spot { Playfield = 551, X = 2407.016F, Y = 23.637F, Z = 3358.193F, EntranceLow = 37078, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 3284.116F, Y = 32.307F, Z = 1220.606F, EntranceLow = 41975, EntranceHigh = 39147 },
            new Spot { Playfield = 650, X = 1787.106F, Y = 19.734F, Z = 2217.608F, EntranceLow = 40865, EntranceHigh = 36044 },
            new Spot { Playfield = 655, X = 3083.008F, Y = 10.162F, Z = 2611.168F, EntranceLow = 37182, EntranceHigh = 31311 },
            new Spot { Playfield = 685, X = 1395.307F, Y = 12.021F, Z = 1780.668F, EntranceLow = 34495, EntranceHigh = 29813 },
            new Spot { Playfield = 505, X = 2552.298F, Y = 18.212F, Z = 879.947F, EntranceLow = 35213, EntranceHigh = 44565 },
            new Spot { Playfield = 625, X = 3619.681F, Y = 37.444F, Z = 845.158F, EntranceLow = 41890, EntranceHigh = 30613 },
            new Spot { Playfield = 695, X = 3277.754F, Y = 21.457F, Z = 806.08F, EntranceLow = 39961, EntranceHigh = 27028 },
            new Spot { Playfield = 635, X = 1086.549F, Y = 50.847F, Z = 1736.905F, EntranceLow = 40889, EntranceHigh = 33041 },
            new Spot { Playfield = 795, X = 2229.763F, Y = 4.577F, Z = 1607.534F, EntranceLow = 33859, EntranceHigh = 39227 },
            new Spot { Playfield = 790, X = 1746.856F, Y = 13.378F, Z = 2088.865F, EntranceLow = 38061, EntranceHigh = 36352 },
            new Spot { Playfield = 550, X = 1587.373F, Y = 42.068F, Z = 934.76F, EntranceLow = 37084, EntranceHigh = 39823 },
            new Spot { Playfield = 545, X = 451.751F, Y = 17.534F, Z = 449.005F, EntranceLow = 39121, EntranceHigh = 40645 },
            new Spot { Playfield = 585, X = 1044.952F, Y = 16.048F, Z = 2117.389F, EntranceLow = 39721, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 976.414F, Y = 9.556F, Z = 1836.11F, EntranceLow = 38485, EntranceHigh = 33942 },
            new Spot { Playfield = 551, X = 2572.299F, Y = 55.734F, Z = 2111.519F, EntranceLow = 37077, EntranceHigh = 41004 },
            new Spot { Playfield = 600, X = 3244.423F, Y = 19.468F, Z = 2689.824F, EntranceLow = 41974, EntranceHigh = 39148 },
            new Spot { Playfield = 655, X = 491.793F, Y = 9.914F, Z = 2451.975F, EntranceLow = 37181, EntranceHigh = 31311 },
            new Spot { Playfield = 685, X = 290.873F, Y = 17.268F, Z = 2556.594F, EntranceLow = 34494, EntranceHigh = 29813 },
            new Spot { Playfield = 625, X = 1455.381F, Y = 77.846F, Z = 2426.669F, EntranceLow = 41891, EntranceHigh = 30613 },
            new Spot { Playfield = 695, X = 1469.645F, Y = 38.906F, Z = 760.902F, EntranceLow = 39961, EntranceHigh = 27028 },
            new Spot { Playfield = 635, X = 619.721F, Y = 21.992F, Z = 2797.495F, EntranceLow = 40889, EntranceHigh = 33041 },
            new Spot { Playfield = 795, X = 3596.132F, Y = 11.888F, Z = 1500.208F, EntranceLow = 33860, EntranceHigh = 39226 },
            new Spot { Playfield = 790, X = 1299.618F, Y = 1.296F, Z = 2799.794F, EntranceLow = 38061, EntranceHigh = 36351 },
            new Spot { Playfield = 550, X = 1609.965F, Y = 41.718F, Z = 987.136F, EntranceLow = 37085, EntranceHigh = 39823 },
            new Spot { Playfield = 545, X = 466.958F, Y = 9.632F, Z = 413.367F, EntranceLow = 39121, EntranceHigh = 40645 },
            new Spot { Playfield = 585, X = 614.047F, Y = 71.113F, Z = 1152.176F, EntranceLow = 39721, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1402.339F, Y = 50.672F, Z = 1831.417F, EntranceLow = 38486, EntranceHigh = 33943 },
            new Spot { Playfield = 551, X = 697.482F, Y = 48.786F, Z = 2712.075F, EntranceLow = 37077, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 3655.392F, Y = 28.864F, Z = 2169.188F, EntranceLow = 41974, EntranceHigh = 39148 },
            new Spot { Playfield = 655, X = 710.877F, Y = 8.609F, Z = 656.553F, EntranceLow = 37181, EntranceHigh = 31310 },
            new Spot { Playfield = 685, X = 2195.19F, Y = 11.83F, Z = 2413.541F, EntranceLow = 34495, EntranceHigh = 29814 },
            new Spot { Playfield = 625, X = 1813.573F, Y = 69.953F, Z = 2142.946F, EntranceLow = 41891, EntranceHigh = 30613 },
            new Spot { Playfield = 635, X = 1607.531F, Y = 12.801F, Z = 2110.653F, EntranceLow = 40889, EntranceHigh = 33040 },
            new Spot { Playfield = 795, X = 3870.664F, Y = 59.97F, Z = 1566.744F, EntranceLow = 33859, EntranceHigh = 39227 },
            new Spot { Playfield = 790, X = 1805.157F, Y = 8.714F, Z = 2167.83F, EntranceLow = 38061, EntranceHigh = 36352 },
            new Spot { Playfield = 550, X = 1594.965F, Y = 41.718F, Z = 987.136F, EntranceLow = 37085, EntranceHigh = 39823 },
            new Spot { Playfield = 585, X = 465.044F, Y = 16.643F, Z = 347.994F, EntranceLow = 39720, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1245.395F, Y = 23.999F, Z = 862.513F, EntranceLow = 38485, EntranceHigh = 33942 },
            new Spot { Playfield = 551, X = 1432.273F, Y = 29.408F, Z = 1149.982F, EntranceLow = 37078, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 1693.86F, Y = 29.89F, Z = 812.718F, EntranceLow = 41974, EntranceHigh = 39148 },
            new Spot { Playfield = 655, X = 713.306F, Y = 8.609F, Z = 638.421F, EntranceLow = 37181, EntranceHigh = 31310 },
            new Spot { Playfield = 685, X = 2861.732F, Y = 17.115F, Z = 2116.429F, EntranceLow = 34494, EntranceHigh = 29814 },
            new Spot { Playfield = 635, X = 1842.25F, Y = 33.437F, Z = 2373.307F, EntranceLow = 40889, EntranceHigh = 33041 },
            new Spot { Playfield = 795, X = 1526.236F, Y = 13.009F, Z = 1499.663F, EntranceLow = 33859, EntranceHigh = 39227 },
            new Spot { Playfield = 790, X = 1248.157F, Y = 1.57F, Z = 2800.209F, EntranceLow = 38061, EntranceHigh = 36351 },
            new Spot { Playfield = 550, X = 1601.804F, Y = 42.805F, Z = 960.256F, EntranceLow = 37085, EntranceHigh = 39822 },
            new Spot { Playfield = 585, X = 625.702F, Y = 11.139F, Z = 848.172F, EntranceLow = 39721, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 1028.698F, Y = 25.408F, Z = 1030.646F, EntranceLow = 38486, EntranceHigh = 33942 },
            new Spot { Playfield = 551, X = 1394.881F, Y = 33.916F, Z = 1390.115F, EntranceLow = 37077, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 925.276F, Y = 64.557F, Z = 1425.641F, EntranceLow = 41975, EntranceHigh = 39147 },
            new Spot { Playfield = 655, X = 710.266F, Y = 8.609F, Z = 621.77F, EntranceLow = 37181, EntranceHigh = 31310 },
            new Spot { Playfield = 635, X = 1630.73F, Y = 11.901F, Z = 2127.087F, EntranceLow = 40889, EntranceHigh = 33040 },
            new Spot { Playfield = 795, X = 480.434F, Y = 33.523F, Z = 696.375F, EntranceLow = 33859, EntranceHigh = 39227 },
            new Spot { Playfield = 790, X = 1713.09F, Y = 14.566F, Z = 1795.901F, EntranceLow = 38061, EntranceHigh = 36352 },
            new Spot { Playfield = 550, X = 1583.221F, Y = 41.709F, Z = 969.956F, EntranceLow = 37085, EntranceHigh = 39823 },
            new Spot { Playfield = 585, X = 471.244F, Y = 16.644F, Z = 357.15F, EntranceLow = 39720, EntranceHigh = 39417 },
            new Spot { Playfield = 760, X = 1566.071F, Y = 30.956F, Z = 2030.866F, EntranceLow = 38485, EntranceHigh = 33943 },
            new Spot { Playfield = 551, X = 960.806F, Y = 45.843F, Z = 1139.529F, EntranceLow = 37077, EntranceHigh = 41003 },
            new Spot { Playfield = 600, X = 1165.147F, Y = 13.429F, Z = 1000.577F, EntranceLow = 41975, EntranceHigh = 39148 },
            new Spot { Playfield = 655, X = 1763.334F, Y = 17.81F, Z = 2421.955F, EntranceLow = 37182, EntranceHigh = 31311 },
            new Spot { Playfield = 635, X = 1097.344F, Y = 50.097F, Z = 2101.161F, EntranceLow = 40889, EntranceHigh = 33040 },
            new Spot { Playfield = 795, X = 567.349F, Y = 19.947F, Z = 714.768F, EntranceLow = 33859, EntranceHigh = 39227 },
            new Spot { Playfield = 790, X = 2227.586F, Y = 21.749F, Z = 3125.0F, EntranceLow = 38062, EntranceHigh = 36352 },
            new Spot { Playfield = 550, X = 1504.222F, Y = 37.132F, Z = 338.454F, EntranceLow = 37085, EntranceHigh = 39822 },
            new Spot { Playfield = 585, X = 2340.189F, Y = 38.124F, Z = 2851.131F, EntranceLow = 39720, EntranceHigh = 39418 },
            new Spot { Playfield = 760, X = 779.526F, Y = 34.746F, Z = 1742.733F, EntranceLow = 38486, EntranceHigh = 33943 },
            new Spot { Playfield = 551, X = 946.335F, Y = 40.842F, Z = 994.484F, EntranceLow = 37078, EntranceHigh = 41004 },
            new Spot { Playfield = 600, X = 1278.216F, Y = 35.154F, Z = 2350.677F, EntranceLow = 41974, EntranceHigh = 39147 },
            new Spot { Playfield = 655, X = 1800.1F, Y = 10.293F, Z = 2411.475F, EntranceLow = 37182, EntranceHigh = 31311 },
            new Spot { Playfield = 635, X = 1295.175F, Y = 46.323F, Z = 878.181F, EntranceLow = 40889, EntranceHigh = 33040 },
        };
    }
}
