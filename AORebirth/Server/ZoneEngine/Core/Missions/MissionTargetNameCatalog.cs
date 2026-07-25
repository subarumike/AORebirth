namespace ZoneEngine.Core.Missions
{
    using System;

    /// <summary>
    /// Invented RK mission target names for KillPerson / FindPerson objectives.
    /// Shapes are shared across types; only the objective NPC name changes.
    /// </summary>
    internal static class MissionTargetNameCatalog
    {
        private static readonly string[] KillNames =
            {
                "Vance Korr",
                "Mira Solenne",
                "Jett Harlan",
                "Soren Vale",
                "Kara Nyx",
                "Drex Mallor",
                "Lina Quell",
                "Orin Drake",
                "Tess Vargo",
                "Rook Halden",
                "Nila Crowe",
                "Pax Renner",
                "Gage Torvin",
                "Sera Quill",
                "Vex Marrow",
                "Cade Riven",
                "Yara Skoll",
                "Finn Calder",
                "Rhea Vos",
                "Kurt Belen"
            };

        private static readonly string[] FindNames =
            {
                "Agent Hale",
                "Courier Bren",
                "Contact Ilya",
                "Scout Maren",
                "Informant Kade",
                "Broker Quinn",
                "Witness Orth",
                "Guide Petra",
                "Handler Vos",
                "Liaison Remy",
                "Operative Nox",
                "Emissary Vale",
                "Asset Torin",
                "Runner Cale",
                "Source Mira",
                "Ally Joren",
                "Patron Elis",
                "Advocate Ryn",
                "Mediator Skye",
                "Envoy Dax"
            };

        public static string PickKillName(Random rng)
        {
            return Pick(KillNames, rng);
        }

        public static string PickFindName(Random rng)
        {
            return Pick(FindNames, rng);
        }

        public static string PickForType(MissionRollType type, Random rng)
        {
            if (type == MissionRollType.FindPerson)
            {
                return PickFindName(rng);
            }

            if (type == MissionRollType.KillPerson)
            {
                return PickKillName(rng);
            }

            return string.Empty;
        }

        private static string Pick(string[] pool, Random rng)
        {
            if (pool == null || pool.Length == 0)
            {
                return "Mission Target";
            }

            if (rng == null)
            {
                return pool[0];
            }

            return pool[rng.Next(pool.Length)];
        }
    }
}
