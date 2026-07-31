namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Items;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;

    /// <summary>
    /// Capture-backed Arete Landing quest world props (cargo box + gas fires + credit card).
    /// Cargo: Terminal:56D9B4AF / template 297277 (rex_b18d_cargo_box_staticdynel_result).
    /// Gas fires around Marcus pad (template 295883):
    ///   57961EAA (3599.267, 42.754, 843.976),
    ///   57961EAB (3602.093, 42.866, 842.475),
    ///   57967DD1 (3607.675, 42.246, 840.874),
    ///   579BA8B4 (3636.222, 43.587, 845.991) — capture 20260720-061810;
    ///   plus near-pad 579ADB41 / extinguish 579ADB38.
    /// Capture 20260720-064523 did not re-send Gas Fire SIFUs; pad positions remain from 061810.
    /// Credit card: Terminal:57A9CCBE / template 297315 — capture 20260730-214622.
    /// </summary>
    internal static class AreteLandingQuestPropDefinitions
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int CargoBoxTemplateId = 297277;

        private const int GasFireTemplateId = 295883;

        private const int JunkTemplateId = 297284;

        // Capture 20260720-105157 Terminal:574187E7 — Plant a Bug target in Desmond's office.
        private const int PrizedHouseplantTemplateId = 295738;

        // Capture 20260720-goldman / 20260721-afgter dog lockpick: Merchant's Strongbox.
        private const int MerchantsStrongboxTemplateId = 295604;

        // Capture 20260721-sara: Remains of Shop Thief (itemnames 295620).
        private const int RemainsOfShopThiefTemplateId = 295620;

        // Capture 20260730-214622: Bank of Rubi-Ka Credit Card on floor (instance rotates on live).
        private const int CreditCardWorldTemplateId = 297315;

        private const int CargoBoxFlags = 139265;

        // Capture Flags=-2146819551 for Gas Fire SIFU.
        private const int GasFireFlags = unchecked((int)0x800A2221);

        // Capture Flags=-2146958847 for Junk SIFU.
        private const int JunkFlags = unchecked((int)0x8007E201);

        // Capture Flags=-2147470847 (0x80003201) for Prized Houseplant SIFU.
        private const int PrizedHouseplantFlags = unchecked((int)0x80003201);

        // Same flag shell as other Arete interactable Terminal props (houseplant/chest).
        private const int MerchantsStrongboxFlags = unchecked((int)0x80003201);

        private const int RemainsOfShopThiefFlags = unchecked((int)0x80003201);

        // Capture 20260730-214622 SIFU Flags=201326593 (0x0C000001).
        private const int CreditCardWorldFlags = 201326593;

        private sealed class PropDefinition
        {
            public int Instance;
            public int TemplateId;
            public int Flags;
            public float X;
            public float Y;
            public float Z;
            public float Hx;
            public float Hy;
            public float Hz;
            public float Hw;
            public string Evidence;
        }

        private static readonly PropDefinition[] Props =
        {
            new PropDefinition
            {
                // Terminal:56D9B4AF — Open the Cargo Box
                Instance = unchecked((int)0x56D9B4AF),
                TemplateId = CargoBoxTemplateId,
                Flags = CargoBoxFlags,
                X = 3621.576f,
                Y = 51.745f,
                Z = 780.4768f,
                Hx = 0f,
                Hy = -0.7101817f,
                Hz = 0f,
                Hw = 0.7040185f,
                Evidence = "20260614-205724 SIFU Terminal:56D9B4AF"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x57961EAA),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3599.267f,
                Y = 42.75448f,
                Z = 843.9763f,
                Hx = 0f,
                Hy = 0.003129918f,
                Hz = 0f,
                Hw = 0.9999951f,
                Evidence = "20260719-Rex-Markus-stone Gas Fire"
            },
            new PropDefinition
            {
                // Capture 20260720-061810 Terminal:579BA8B4 (replaces prior 57961E8E at same pad).
                Instance = unchecked((int)0x579BA8B4),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3636.222f,
                Y = 43.58711f,
                Z = 845.9906f,
                Hx = 0f,
                Hy = 0.003037463f,
                Hz = 0f,
                Hw = 0.9999954f,
                Evidence = "20260720-061810 Gas Fire Terminal:579BA8B4"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x57961EAB),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3602.093f,
                Y = 42.86554f,
                Z = 842.4749f,
                Hx = 0f,
                Hy = 0.002859163f,
                Hz = 0f,
                Hw = 0.9999959f,
                Evidence = "20260719-Rex-Markus-stone Gas Fire"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x57967DD1),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3607.675f,
                Y = 42.24637f,
                Z = 840.8735f,
                Hx = 0f,
                Hy = 0.003388073f,
                Hz = 0f,
                Hw = -0.9999943f,
                Evidence = "20260719-Rex-Markus-stone Gas Fire"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x579ADB41),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3629.709f,
                Y = 42.90778f,
                Z = 832.0861f,
                Hx = 0f,
                Hy = 0.003335144f,
                Hz = 0f,
                Hw = -0.9999945f,
                Evidence = "20260719-Rex-Markus-stone Gas Fire"
            },
            // Extinguish target from capture UseItemOnItem (no SIFU; placed at player stand pos).
            new PropDefinition
            {
                Instance = unchecked((int)0x579ADB38),
                TemplateId = GasFireTemplateId,
                Flags = GasFireFlags,
                X = 3629.344f,
                Y = 41.5f,
                Z = 829.9046f,
                Hx = 0f,
                Hy = 0.003335144f,
                Hz = 0f,
                Hw = -0.9999945f,
                Evidence = "20260719-Rex-Markus-stone extinguish Terminal:579ADB38"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x579ADB3E),
                TemplateId = JunkTemplateId,
                Flags = JunkFlags,
                X = 3620.871f,
                Y = 51.61641f,
                Z = 784.1057f,
                Hx = 0f,
                Hy = 0.0008167704f,
                Hz = 0f,
                Hw = 0.9999996f,
                Evidence = "20260719-Rex-Markus-stone Junk 297284"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x579ADB3F),
                TemplateId = JunkTemplateId,
                Flags = JunkFlags,
                X = 3611.681f,
                Y = 52.11217f,
                Z = 781.9929f,
                Hx = 0f,
                Hy = 0.00161186f,
                Hz = 0f,
                Hw = -0.9999987f,
                Evidence = "20260719-Rex-Markus-stone Junk 297284"
            },
            new PropDefinition
            {
                Instance = unchecked((int)0x579ADB40),
                TemplateId = JunkTemplateId,
                Flags = JunkFlags,
                X = 3602.17f,
                Y = 51.74875f,
                Z = 775.8182f,
                Hx = 0f,
                Hy = 0.0005814155f,
                Hz = 0f,
                Hw = -0.9999998f,
                Evidence = "20260719-Rex-Markus-stone Junk 297284"
            },
            new PropDefinition
            {
                // Capture 20260720-105157 / 151642 — Desmond office Plant a Bug target
                Instance = unchecked((int)0x574187E7),
                TemplateId = PrizedHouseplantTemplateId,
                Flags = PrizedHouseplantFlags,
                X = 3611.686f,
                Y = 8.15207f,
                Z = 814.9171f,
                Hx = 0f,
                Hy = 0.7282565f,
                Hz = 0f,
                Hw = -0.6853046f,
                Evidence = "20260720-105157 Prized Houseplant Terminal:574187E7"
            },
            new PropDefinition
            {
                // Capture 20260720-goldman Terminal:574187CE — Lock Pick target.
                Instance = unchecked((int)0x574187CE),
                TemplateId = MerchantsStrongboxTemplateId,
                Flags = MerchantsStrongboxFlags,
                X = 3409.956f,
                Y = 9.01f,
                Z = 893.5452f,
                Hx = 0f,
                Hy = 0f,
                Hz = 0f,
                Hw = 1f,
                Evidence = "20260720-goldman Merchant's Strongbox Terminal:574187CE"
            },
            new PropDefinition
            {
                // Capture 20260721-sara Terminal:574187CF — DNA-Locked Armor loot target.
                Instance = unchecked((int)0x574187CF),
                TemplateId = RemainsOfShopThiefTemplateId,
                Flags = RemainsOfShopThiefFlags,
                X = 3424.016f,
                Y = 0.01011355f,
                Z = 887.8564f,
                Hx = 0f,
                Hy = 0f,
                Hz = 0f,
                Hw = 1f,
                Evidence = "20260721-sara Remains of Shop Thief Terminal:574187CF"
            },
            new PropDefinition
            {
                // Capture 20260730-214622 Terminal:57A9CCBE — Leonora credit card floor prop.
                Instance = unchecked((int)0x57A9CCBE),
                TemplateId = CreditCardWorldTemplateId,
                Flags = CreditCardWorldFlags,
                X = 3449.29f,
                Y = 0.01f,
                Z = 889.0669f,
                Hx = 0f,
                Hy = 0.9866799f,
                Hz = 0f,
                Hw = 0.1626736f,
                Evidence = "20260730-214622 Bank of Rubi-Ka Credit Card Terminal:57A9CCBE"
            },
            // Exit Arete Landing is playfields.dat Terminal:C0001999 (tpl 297303) — do not inject
            // a second StaticDynel (duplicate + wrong facing). Use is wired in VaughnHammondQuestRuntime.
        };

        internal static IEnumerable<PlayfieldStaticDynelDefinition> ResolveMissingProps(
            Identity playfieldIdentity,
            IEnumerable<PlayfieldStaticDynelDefinition> existing)
        {
            if (playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                yield break;
            }

            HashSet<ulong> existingKeys = new HashSet<ulong>();
            if (existing != null)
            {
                foreach (PlayfieldStaticDynelDefinition dynel in existing)
                {
                    if (dynel != null && dynel.Identity.Type == IdentityType.Terminal)
                    {
                        existingKeys.Add(dynel.Identity.Long());
                    }
                }
            }

            int spawned = 0;
            foreach (PropDefinition prop in Props)
            {
                Identity identity = new Identity
                                    {
                                        Type = IdentityType.Terminal,
                                        Instance = prop.Instance
                                    };
                if (existingKeys.Contains(identity.Long()))
                {
                    continue;
                }

                ItemTemplate template;
                if (!ItemLoader.ItemList.TryGetValue(prop.TemplateId, out template) || template == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteLandingQuestProp missing item template=" + prop.TemplateId
                        + " evidence=" + prop.Evidence);
                    continue;
                }

                spawned++;
                yield return new PlayfieldStaticDynelDefinition(
                    identity,
                    template,
                    BuildStats(prop),
                    new Coordinate(prop.X, prop.Y, prop.Z),
                    new Quaternion { X = prop.Hx, Y = prop.Hy, Z = prop.Hz, W = prop.Hw });
            }

            if (spawned > 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteLandingQuestProp injected count=" + spawned + " pf=" + AreteLandingPlayfieldId);
            }
        }

        private static List<GameTuple<CharacterStat, uint>> BuildStats(PropDefinition prop)
        {
            return new List<GameTuple<CharacterStat, uint>>
                   {
                       Stat(CharacterStat.Flags, (uint)prop.Flags),
                       Stat(CharacterStat.StaticInstance, (uint)prop.TemplateId),
                       Stat(CharacterStat.ACGItemLevel, 1),
                       Stat(CharacterStat.ACGItemTemplateID, (uint)prop.TemplateId),
                       Stat(CharacterStat.ACGItemTemplateID2, (uint)prop.TemplateId),
                       Stat(CharacterStat.MultipleCount, 1),
                       Stat(CharacterStat.AnimPlay, 0),
                       Stat(CharacterStat.AnimPos, 0)
                   };
        }

        private static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = id, Value2 = value };
        }
    }
}
