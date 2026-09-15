namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class CryptOfHomeDocumentedDropDefinition
    {
        internal string EnemyKey { get; set; }
        internal string EnemyDisplayName { get; set; }
        internal string ItemName { get; set; }
        internal int ItemTemplateId { get; set; }
        internal int Quality { get; set; }
        internal int DropChanceBasisPoints { get; set; }
        internal string SourceProbability { get; set; }
        internal bool IsActive { get; set; }
    }

    internal static class DocumentedCryptOfHomeLootDefinitions
    {
        internal const int PlayfieldInstance = 4805;
        internal const string DocumentedLootSourceUrl =
            "https://wiki.aodb.us/wiki/Crypt_of_Home";
        internal const string DocumentedLootGroupPrefix =
            "documented.aowiki.crypt-of-home.";

        internal const string DarkCenobiteKey = "crypt-of-home.4805.enemy.dark-cenobite";
        internal const string DarkSanitaryKey = "crypt-of-home.4805.enemy.dark-sanitary";
        internal const string DarkSummonerKey = "crypt-of-home.4805.enemy.dark-summoner";
        internal const string CenobiteShadowKey = "crypt-of-home.4805.enemy.cenobite-shadow";
        internal const string BlorrgKey = "crypt-of-home.4805.enemy.blorrg";
        internal const string EclipserKey = "crypt-of-home.4805.enemy.eclipser";
        internal const string NecromancerKey = "crypt-of-home.4805.enemy.necromancer";
        internal const string KizzermoleKey = "crypt-of-home.4805.enemy.kizzermole";
        internal const string PitDemonKey = "crypt-of-home.4805.boss.awakened-pit-demon";
        internal const string CryptGuardianKey = "crypt-of-home.4805.enemy.crypt-guardian";
        internal const string AlphaSkincrawlerKey = "crypt-of-home.4805.boss.alpha-skincrawler";
        internal const string BaneKey = "crypt-of-home.4805.boss.bane";
        internal const string TentacleKey = "crypt-of-home.4805.enemy.tentacle-of-cerubin";
        internal const string CerubinKey = "crypt-of-home.4805.boss.cerubin-the-rejected";

        private static readonly CryptOfHomeDocumentedDropDefinition[] Drops = BuildDrops();

        internal static CryptOfHomeDocumentedDropDefinition[] DocumentedDrops
        {
            get { return Drops.ToArray(); }
        }

        internal static int[] DocumentedSourceItemIds
        {
            get
            {
                return Drops
                    .Select(value => value.ItemTemplateId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
            }
        }

        internal static CryptOfHomeDocumentedDropDefinition[] DropsForDisplayName(
            int playfieldId,
            string displayName)
        {
            if (playfieldId != PlayfieldInstance)
            {
                return new CryptOfHomeDocumentedDropDefinition[0];
            }

            string enemyKey = EnemyKeyForDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(enemyKey))
            {
                return new CryptOfHomeDocumentedDropDefinition[0];
            }

            return Drops
                .Where(value => string.Equals(value.EnemyKey, enemyKey, StringComparison.Ordinal))
                .ToArray();
        }

        internal static bool ApplyDocumentedLoot(
            LootTableDefinition table,
            int playfieldId,
            string displayName)
        {
            if (table == null)
            {
                return false;
            }

            CryptOfHomeDocumentedDropDefinition[] active =
                DropsForDisplayName(playfieldId, displayName)
                    .Where(value => value.IsActive)
                    .ToArray();
            if (active.Length == 0)
            {
                return false;
            }

            LootGroupDefinition[] existing = table.RollGroups
                ?? new LootGroupDefinition[0];
            var existingItemIds = new HashSet<int>(
                existing
                    .Where(value => value != null && value.Entries != null)
                    .SelectMany(value => value.Entries)
                    .Where(value => value != null)
                    .Select(value => value.ItemTemplateId)
                    .Concat(
                        (table.ObservedCorpseSnapshots
                         ?? new ObservedCorpseSnapshotDefinition[0])
                            .Where(value => value != null && value.Entries != null)
                            .SelectMany(value => value.Entries)
                            .Where(value => value != null)
                            .Select(value => value.ItemTemplateId))
                    .Where(value => value > 0));
            var groupKeys = new HashSet<string>(
                existing
                    .Where(value => value != null)
                    .Select(value => value.LootGroupKey),
                StringComparer.Ordinal);
            var additions = new List<LootGroupDefinition>();
            foreach (CryptOfHomeDocumentedDropDefinition drop in active)
            {
                string groupKey = DocumentedLootGroupPrefix
                                  + drop.EnemyKey
                                  + "."
                                  + drop.ItemTemplateId;
                if (existingItemIds.Contains(drop.ItemTemplateId)
                    || groupKeys.Contains(groupKey))
                {
                    continue;
                }

                additions.Add(DocumentedIndependentGroup(groupKey, drop));
                existingItemIds.Add(drop.ItemTemplateId);
                groupKeys.Add(groupKey);
            }

            if (additions.Count == 0)
            {
                return false;
            }

            table.RollGroups = existing.Concat(additions).ToArray();
            table.AllowsDocumentedSupplement = true;
            return true;
        }

        private static LootGroupDefinition DocumentedIndependentGroup(
            string groupKey,
            CryptOfHomeDocumentedDropDefinition drop)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = groupKey,
                RollMode = LootRollMode.Independent,
                RollCount = 1,
                EmptyWeight = 0,
                DropChanceBasisPoints = drop.DropChanceBasisPoints,
                Entries = new[]
                {
                    new LootEntryDefinition
                    {
                        ItemTemplateId = drop.ItemTemplateId,
                        HighItemTemplateId = drop.ItemTemplateId,
                        FixedQuality = drop.Quality,
                        MinimumQuality = drop.Quality,
                        MaximumQuality = drop.Quality,
                        MinimumQuantity = 1,
                        MaximumQuantity = 1,
                        Weight = 1,
                        DropChanceBasisPoints = drop.DropChanceBasisPoints,
                        UniquePerCorpse = true,
                        Semantics = LootSemantics.WeightedDocumented,
                        Evidence = LootEvidenceConfidence.CommunityDocumented,
                        EvidenceReference = DocumentedLootSourceUrl,
                        ProbabilityEvidence = "documented-exact:" + drop.SourceProbability
                    }
                },
                Conditions = new string[0]
            };
        }

        private static CryptOfHomeDocumentedDropDefinition[] BuildDrops()
        {
            var values = new List<CryptOfHomeDocumentedDropDefinition>();

            Add(values, DarkCenobiteKey, "Dark Cenobite", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Cloak of the Revoked", 245135, 100, "lower than the near-50% cloak comparison; exact rate not published");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Necromancer Cloak", 245170, 100, "near 50% for a full team; not an exact rate");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Dark Pistol", 245222, 99, "membership published; rate not published");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Dark Pistol of The Revoked", 245223, 100, "membership published; rate not published");
            Add(values, DarkCenobiteKey, "Dark Cenobite", "Floating Torch", 31837, 1, "membership published; rate not published");

            Add(values, DarkSanitaryKey, "Dark Sanitary", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, DarkSanitaryKey, "Dark Sanitary", "Cloak of the Revoked", 245135, 100, "membership published; rate not published");
            Add(values, DarkSanitaryKey, "Dark Sanitary", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, DarkSanitaryKey, "Dark Sanitary", "Floating Torch", 31837, 1, "membership published; rate not published");

            Add(values, DarkSummonerKey, "Dark Summoner", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, DarkSummonerKey, "Dark Summoner", "Cloak of the Revoked", 245135, 100, "membership published; rate not published");
            Add(values, DarkSummonerKey, "Dark Summoner", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, DarkSummonerKey, "Dark Summoner", "Dark Pistol", 245222, 99, "membership published; rate not published");
            Add(values, DarkSummonerKey, "Dark Summoner", "Sacrificial Ensigns of Cerubin", 246219, 100, "membership published; rate not published");
            Add(values, DarkSummonerKey, "Dark Summoner", "Floating Torch", 31837, 1, "membership published; rate not published");

            Add(values, CenobiteShadowKey, "Cenobite Shadow", "Blackbird", 246720, 100, "membership published; rate not published");
            Add(values, CenobiteShadowKey, "Cenobite Shadow", "Chiroptera", 246710, 100, "membership published; rate not published");
            Add(values, CenobiteShadowKey, "Cenobite Shadow", "Howlet", 246705, 100, "membership published; rate not published");
            Add(values, CenobiteShadowKey, "Cenobite Shadow", "Panther", 246715, 100, "membership published; rate not published");

            Add(values, BlorrgKey, "Blorrg", "Gas Bladder", 245115, 100, "membership published; rate not published");
            Add(values, BlorrgKey, "Blorrg", "Bundle of Twisting Nerves", 246218, 100, "membership published; rate not published");

            Add(values, EclipserKey, "Eclipser", "Cloak of the Revoked", 245135, 100, "membership published; rate not published");
            Add(values, EclipserKey, "Eclipser", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, EclipserKey, "Eclipser", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, EclipserKey, "Eclipser", "Floating Torch", 31837, 1, "membership published; rate not published");

            Add(values, NecromancerKey, "Necromancer", "Necromancer Cloak", 245170, 100, "membership published; rate not published");
            Add(values, NecromancerKey, "Necromancer", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, NecromancerKey, "Necromancer", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, NecromancerKey, "Necromancer", "Sacrificial Ensigns of Cerubin", 246219, 100, "membership published; rate not published");

            Add(values, KizzermoleKey, "Kizzermole", "Kizzermole Tongue", 245092, 100, "membership published; rate not published");
            Add(values, KizzermoleKey, "Kizzermole", "Kizzermole Gumboil", 245323, 100, "membership published; rate not published");

            Add(values, PitDemonKey, "Awakened Pit Demon", "Hood of Wicked Inspiration", 246217, 100, "membership published; rate not published");
            Add(values, PitDemonKey, "Awakened Pit Demon", "Pit Demon Heart", 245171, 100, "membership published; rate not published");
            Add(values, PitDemonKey, "Awakened Pit Demon", "Pit Demon Spit", 245169, 100, "membership published; rate not published");

            Add(values, CryptGuardianKey, "Crypt Guardian", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, CryptGuardianKey, "Crypt Guardian", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, CryptGuardianKey, "Crypt Guardian", "Essence of Eucalyptus", 287133, 200, "membership published; rate not published");

            Add(values, AlphaSkincrawlerKey, "Alpha Skincrawler", "Brother's Brass Knuckles", 246223, 100, "membership published; rate not published");
            Add(values, AlphaSkincrawlerKey, "Alpha Skincrawler", "Damaged Proliferation Unit", 245215, 100, "membership published; rate not published");
            Add(values, AlphaSkincrawlerKey, "Alpha Skincrawler", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, AlphaSkincrawlerKey, "Alpha Skincrawler", "Pit Demon Heart", 245171, 100, "membership published; rate not published");

            Add(values, BaneKey, "Bane", "Human Skin Hood", 246226, 100, "membership published; rate not published");
            Add(values, BaneKey, "Bane", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");
            Add(values, BaneKey, "Bane", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, BaneKey, "Bane", "Bracer of Recondite Flames", 245276, 100, "membership published; rate not published");
            Add(values, BaneKey, "Bane", "Bracer of Dark Flame", 245278, 100, "membership published; rate not published");

            Add(values, TentacleKey, "Tentacle of Cerubin", "Intimate Tentacle Things", 246829, 1, "membership published; rate not published");
            Add(values, TentacleKey, "Tentacle of Cerubin", "Tentacle Tape", 246831, 1, "membership published; rate not published");
            Add(values, TentacleKey, "Tentacle of Cerubin", "Tentacle Threads", 246832, 1, "membership published; rate not published");
            Add(values, TentacleKey, "Tentacle of Cerubin", "Tentacle Thongs", 246830, 1, "membership published; rate not published");
            Add(values, TentacleKey, "Tentacle of Cerubin", "Neck Eye", 246834, 1, "membership published; rate not published");
            Add(values, TentacleKey, "Tentacle of Cerubin", "Grasping Ring", 246833, 1, "membership published; rate not published");

            Add(values, CerubinKey, "Cerubin the Rejected", "SpiritTech Circlet of Cerubin", 245139, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Gamboling Master's Wear", 246216, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Shapeshifter's Vest", 246222, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Bracer of Recondite Flames", 245276, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Jagged Claw", 245273, 175, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Focus Funneling Device", 246206, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Necromancer Cloak", 245170, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Pit Demon Heart", 245171, 100, "membership and uniqueness published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Collar Casero de la Cripta", 246125, 100, "membership published; rate not published");
            Add(values, CerubinKey, "Cerubin the Rejected", "Anillo Casero de la Cripta", 246123, 100, "membership published; rate not published");

            return values.ToArray();
        }

        private static void Add(
            ICollection<CryptOfHomeDocumentedDropDefinition> values,
            string enemyKey,
            string enemyDisplayName,
            string itemName,
            int itemTemplateId,
            int quality,
            string sourceProbability)
        {
            values.Add(
                new CryptOfHomeDocumentedDropDefinition
                {
                    EnemyKey = enemyKey,
                    EnemyDisplayName = enemyDisplayName,
                    ItemName = itemName,
                    ItemTemplateId = itemTemplateId,
                    Quality = quality,
                    DropChanceBasisPoints = 0,
                    SourceProbability = sourceProbability,
                    IsActive = false
                });
        }

        private static string EnemyKeyForDisplayName(string displayName)
        {
            string value = (displayName ?? string.Empty).Trim();
            if (EqualsName(value, "Dark Cenobite")) return DarkCenobiteKey;
            if (EqualsName(value, "Dark Sanitary")) return DarkSanitaryKey;
            if (EqualsName(value, "Dark Summoner")) return DarkSummonerKey;
            if (EqualsName(value, "Cenobite Shadow")) return CenobiteShadowKey;
            if (EqualsName(value, "Blorrg")) return BlorrgKey;
            if (EqualsName(value, "Eclipser")) return EclipserKey;
            if (EqualsName(value, "Necromancer")) return NecromancerKey;
            if (EqualsName(value, "Kizzermole")) return KizzermoleKey;
            if (EqualsName(value, "Awakened Pit Demon")) return PitDemonKey;
            if (EqualsName(value, "Crypt Guardian")) return CryptGuardianKey;
            if (EqualsName(value, "Alpha Skincrawler")) return AlphaSkincrawlerKey;
            if (EqualsName(value, "Bane")) return BaneKey;
            if (value.StartsWith("Tentacle of ", StringComparison.OrdinalIgnoreCase)
                || EqualsName(value, "Lazy Tentacle")) return TentacleKey;
            if (EqualsName(value, "Cerubin the Rejected")) return CerubinKey;
            return null;
        }

        private static bool EqualsName(string value, string candidate)
        {
            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
