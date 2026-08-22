namespace ZoneEngine.Core.Doja
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// DOJA chip item IDs (Mike 20260822) + Nascense capture 20260821-222107.
    /// Level ranges from AO-Universe DOJA guide. Shared 18h lockout for all except
    /// Team Dark Ruins (separate lockout — not in this list).
    /// </summary>
    internal static class DojaChipInteractionRules
    {
        // Mike-provided item IDs (20260822).
        internal const int NascenseChipItemId = 284954;
        internal const int ElysiumChipItemId = 284955;
        internal const int ScheolChipItemId = 284956;
        internal const int AdonisChipItemId = 284957;
        internal const int PenumbraChipItemId = 284958;
        internal const int InfernoChipItemId = 284959;
        internal const int PandemoniumChipItemId = 284960; // Special DOJA Chip Pandemonium
        internal const int AlappaaChipItemId = 284961; // Special DOJA Chip Alappaa
        internal const int AlbtraumChipItemId = 284962; // Special DOJA Chip Albtraum

        /// <summary>Quest MissionItemData companion / daily-mission reward template (capture).</summary>
        internal const int DailyMissionRewardItemId = 285612;

        // AO-Universe usable level ranges.
        internal const int NascenseMinLevel = 1;
        internal const int NascenseMaxLevel = 60;
        internal const int ElysiumMinLevel = 61;
        internal const int ElysiumMaxLevel = 100;
        internal const int ScheolMinLevel = 101;
        internal const int ScheolMaxLevel = 130;
        internal const int AdonisMinLevel = 131;
        internal const int AdonisMaxLevel = 160;
        internal const int PenumbraMinLevel = 161;
        internal const int PenumbraMaxLevel = 204;
        internal const int InfernoMinLevel = 205;
        internal const int InfernoMaxLevel = 220;
        internal const int PandemoniumMinLevel = 220;
        internal const int PandemoniumMaxLevel = 220;
        internal const int AlappaaMinLevel = 201;
        internal const int AlappaaMaxLevel = 210;
        internal const int AlbtraumMinLevel = 211;
        internal const int AlbtraumMaxLevel = 219;

        // Backward-compatible aliases for Nascense runtime.
        internal const int MinLevel = NascenseMinLevel;
        internal const int MaxLevel = NascenseMaxLevel;

        /// <summary>Capture 20260821-222107 Nascense turn-in journal.</summary>
        internal const string QuestTurnIn = "Mission:55AA2421";

        /// <summary>Shared lockout journal (capture Unknown20/21 = 1080 minutes = 18h).</summary>
        internal const string QuestCooldown = "Mission:55AA2803";

        internal const string CooldownFlag = "doja-shared-cooldown-until-utc";
        internal const string TurnInGrantedFlag = "doja-nascense-turnin-granted";

        internal const string ScarlettName = "Scarlett Dalquist";
        internal const int ScarlettPlayfieldId = 7010; // DOJA Research / Lab R1
        internal const int ScarlettInstance = unchecked((int)0x7A18B924);
        internal const string ScarlettIdentityText = "SimpleChar:7A18B924";

        private static readonly DojaChipDefinition[] Catalog =
            {
                new DojaChipDefinition(NascenseChipItemId, "Nascense", NascenseMinLevel, NascenseMaxLevel, true),
                new DojaChipDefinition(ElysiumChipItemId, "Elysium", ElysiumMinLevel, ElysiumMaxLevel, false),
                new DojaChipDefinition(ScheolChipItemId, "Scheol", ScheolMinLevel, ScheolMaxLevel, false),
                new DojaChipDefinition(AdonisChipItemId, "Adonis", AdonisMinLevel, AdonisMaxLevel, false),
                new DojaChipDefinition(PenumbraChipItemId, "Penumbra", PenumbraMinLevel, PenumbraMaxLevel, false),
                new DojaChipDefinition(InfernoChipItemId, "Inferno", InfernoMinLevel, InfernoMaxLevel, false),
                new DojaChipDefinition(PandemoniumChipItemId, "Pandemonium", PandemoniumMinLevel, PandemoniumMaxLevel, false),
                new DojaChipDefinition(AlappaaChipItemId, "Alappaa", AlappaaMinLevel, AlappaaMaxLevel, false),
                new DojaChipDefinition(AlbtraumChipItemId, "Albtraum", AlbtraumMinLevel, AlbtraumMaxLevel, false)
            };

        internal static bool IsNascenseChip(int lowId, int highId)
        {
            return lowId == NascenseChipItemId || highId == NascenseChipItemId;
        }

        internal static bool IsKnownDojaChip(int lowId, int highId)
        {
            DojaChipDefinition unused;
            return TryResolveChip(lowId, highId, out unused);
        }

        internal static bool TryResolveChip(int lowId, int highId, out DojaChipDefinition chip)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                int id = Catalog[i].ItemId;
                if (lowId == id || highId == id)
                {
                    chip = Catalog[i];
                    return true;
                }
            }

            chip = default(DojaChipDefinition);
            return false;
        }

        internal static bool IsLevelEligible(DojaChipDefinition chip, int level)
        {
            return level >= chip.MinLevel && level <= chip.MaxLevel;
        }

        internal static bool IsScarlett(Identity identity)
        {
            return identity.Instance == ScarlettInstance;
        }

        internal static bool IsScarlettName(string name)
        {
            return string.Equals(name, ScarlettName, System.StringComparison.OrdinalIgnoreCase);
        }

        internal struct DojaChipDefinition
        {
            internal DojaChipDefinition(int itemId, string zoneName, int minLevel, int maxLevel, bool implemented)
            {
                this.ItemId = itemId;
                this.ZoneName = zoneName;
                this.MinLevel = minLevel;
                this.MaxLevel = maxLevel;
                this.IsImplemented = implemented;
            }

            internal int ItemId { get; private set; }

            internal string ZoneName { get; private set; }

            internal int MinLevel { get; private set; }

            internal int MaxLevel { get; private set; }

            /// <summary>True when Use → journal → Scarlett turn-in is capture-backed.</summary>
            internal bool IsImplemented { get; private set; }
        }
    }
}
