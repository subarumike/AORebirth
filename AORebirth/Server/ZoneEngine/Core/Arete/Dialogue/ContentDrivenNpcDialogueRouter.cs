namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Subway.Quests;
    using ZoneEngine.Core.Thrak.Quests;
    using ZoneEngine.Core.Thrak.Vendors;

    #endregion

    public static class ContentDrivenNpcDialogueRouter
    {
        public const string RexLarssonGateEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

        public const string SubwayTailorGateEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_SUBWAY_TAILOR_DIALOGUE_ROUTING";

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const int BarryFoodVendorInstance = unchecked((int)0x78E0FC7D);

        private const int BorisPeacekeeperInstance = unchecked((int)0x782DE699);

        private const int DesmondCalitriInstance = unchecked((int)0x78E0FC77);

        private const int FlintNovakInstance = unchecked((int)0x78E0FC64);

        private const int AlexGibbsInstance = unchecked((int)0x78E0FC61);

        private const int BillInstance = unchecked((int)0x78E0FC66);

        private const int StanGoodmanInstance = unchecked((int)0x78E0FC65);
        private const int SarahGreeneInstance = unchecked((int)0x78E0FC69);
        private const int VernonGodfrayInstance = unchecked((int)0x78E0FC68);
        private const int DoctorMasonInstance = unchecked((int)0x78E0FC6C);
        private const int LoreleiBartenderInstance = unchecked((int)0x78E0FC6B);
        private const int LollyTheReetInstance = unchecked((int)0x7985CAEC);
        private const int ShippingManifestTerminalInstance = unchecked((int)0x78E0FC6A);
        private const int MarcoSpidaInstance = unchecked((int)0x78E0FC81);
        private const int VaughnHammondInstance = unchecked((int)0x78E0FC73);
        private const int AntonioStacklundInstance = unchecked((int)0x78E0FC7C);
        private const int KarliCappelleriInstance = unchecked((int)0x799AD394);
        private const int LeonoraMartyInstance = unchecked((int)0x78E0FC74);
        private const int PatrickSunInstance = unchecked((int)0x78E0FC7B);
        private const int RemiGalloisInstance = unchecked((int)0x78E0FC75);
        private const int GreedyDesertReetInstance = unchecked((int)0x79978BB8);

        private const string RexLarssonNpcIdentity = "SimpleChar:782DE568";

        private const string MarcusStoneNpcIdentity = "SimpleChar:782DE567";

        private const string BarryFoodVendorNpcIdentity = "SimpleChar:78E0FC7D";

        private const string BorisPeacekeeperNpcIdentity = "SimpleChar:782DE699";

        private const string DesmondCalitriNpcIdentity = "SimpleChar:78E0FC77";

        private const string FlintNovakNpcIdentity = "SimpleChar:78E0FC64";

        private const string AlexGibbsNpcIdentity = "SimpleChar:78E0FC61";

        private const string StanGoodmanNpcIdentity = "SimpleChar:78E0FC65";
        private const string SarahGreeneNpcIdentity = "SimpleChar:78E0FC69";
        private const string VernonGodfrayNpcIdentity = "SimpleChar:78E0FC68";
        private const string DoctorMasonNpcIdentity = "SimpleChar:78E0FC6C";
        private const string LoreleiBartenderNpcIdentity = "SimpleChar:78E0FC6B";
        private const string LollyTheReetNpcIdentity = "SimpleChar:7985CAEC";
        private const string ShippingManifestTerminalNpcIdentity = "SimpleChar:78E0FC6A";
        private const string MarcoSpidaNpcIdentity = "SimpleChar:78E0FC81";
        private const string VaughnHammondNpcIdentity = "SimpleChar:78E0FC73";
        private const string AntonioStacklundNpcIdentity = "SimpleChar:78E0FC7C";
        private const string KarliCappelleriNpcIdentity = "SimpleChar:799AD394";
        private const string LeonoraMartyNpcIdentity = "SimpleChar:78E0FC74";
        private const string PatrickSunNpcIdentity = "SimpleChar:78E0FC7B";
        private const string RemiGalloisNpcIdentity = "SimpleChar:78E0FC75";
        private const string GreedyDesertReetNpcIdentity = "SimpleChar:79978BB8";

        private const string RexB18EReturnNodeId = "rex_194454_006";

        private const int KnuBotPacketPacingMilliseconds = 20;

        private static readonly ContentDrivenNpcDialogueRegistration RexLarssonRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Rex Larsson",
                ExpectedNpcName = "Rex Larsson",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = RexLarssonInstance
                    },
                NpcIdentityText = RexLarssonNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_REX_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration MarcusStoneRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Marcus Stone",
                ExpectedNpcName = "Marcus Stone",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = MarcusStoneInstance
                    },
                NpcIdentityText = MarcusStoneNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_MARCUS_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration BarryFoodVendorRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Barry the Food Vendor",
                ExpectedNpcName = "Barry the Food Vendor",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = BarryFoodVendorInstance
                    },
                NpcIdentityText = BarryFoodVendorNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_BARRY_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration BorisPeacekeeperRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Boris the Peacekeeper",
                ExpectedNpcName = "Boris the Peacekeeper",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = BorisPeacekeeperInstance
                    },
                NpcIdentityText = BorisPeacekeeperNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_BORIS_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration DesmondCalitriRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Desmond Calitri",
                ExpectedNpcName = "Desmond Calitri",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = DesmondCalitriInstance
                    },
                NpcIdentityText = DesmondCalitriNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_DESMOND_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration FlintNovakRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Flint Novak",
                ExpectedNpcName = "Flint Novak",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = FlintNovakInstance
                    },
                NpcIdentityText = FlintNovakNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_FLINT_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration AlexGibbsRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Alex Gibbs",
                ExpectedNpcName = "Alex Gibbs",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = AlexGibbsInstance
                    },
                NpcIdentityText = AlexGibbsNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_ALEX_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration BillRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "ICC Immigration Officer Bill",
                ExpectedNpcName = "ICC Immigration Officer Bill",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = BillInstance
                    },
                NpcIdentityText = "SimpleChar:78E0FC66",
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_BILL_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration StanGoodmanRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                // Live/spawn name is "Stanley Goodman" (AreteLandingSpawn + capture 20260801-101034).
                // Pool ids ≠ capture id 78E0FC65, so FindRegistration falls back to this name.
                Name = "Stan Goodman",
                ExpectedNpcName = "Stanley Goodman",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = StanGoodmanInstance
                    },
                NpcIdentityText = StanGoodmanNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_STAN_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration SarahGreeneRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Sarah Greene",
                ExpectedNpcName = "Sarah Greene",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = SarahGreeneInstance
                    },
                NpcIdentityText = SarahGreeneNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_SARAH_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration VernonGodfrayRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Vernon Godfray",
                ExpectedNpcName = "Vernon Godfray",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = VernonGodfrayInstance
                    },
                NpcIdentityText = VernonGodfrayNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_VERNON_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration DoctorMasonRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Dr. Mason",
                ExpectedNpcName = "Dr. Mason",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = DoctorMasonInstance
                    },
                NpcIdentityText = DoctorMasonNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_MASON_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration LoreleiBartenderRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Lorelei the Bartender",
                ExpectedNpcName = "Lorelei the Bartender",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = LoreleiBartenderInstance
                    },
                NpcIdentityText = LoreleiBartenderNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_LORELEI_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration LollyTheReetRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Lolly the Reet",
                ExpectedNpcName = "Lolly the Reet",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = LollyTheReetInstance
                    },
                NpcIdentityText = LollyTheReetNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_LOLLY_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration ShippingManifestTerminalRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Shipping Manifest Terminal",
                ExpectedNpcName = "Shipping Manifest Terminal",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ShippingManifestTerminalInstance
                    },
                NpcIdentityText = ShippingManifestTerminalNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_SMT_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration MarcoSpidaRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Marco Spida",
                ExpectedNpcName = "Marco Spida",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = MarcoSpidaInstance
                    },
                NpcIdentityText = MarcoSpidaNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_MARCO_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration VaughnHammondRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Vaughn Hammond",
                ExpectedNpcName = "Vaughn Hammond",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = VaughnHammondInstance
                    },
                NpcIdentityText = VaughnHammondNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                LogPrefix = "ARETE_VAUGHN_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration AntonioStacklundRegistration =
            CreateAreteRegistration(
                "Antonio Stacklund",
                AntonioStacklundInstance,
                AntonioStacklundNpcIdentity,
                AreteLandingPlayfieldId,
                "ARETE_ANTONIO_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration KarliCappelleriRegistration =
            CreateAreteRegistration(
                "Karli Cappelleri",
                KarliCappelleriInstance,
                KarliCappelleriNpcIdentity,
                8009,
                "ARETE_KARLI_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration LeonoraMartyRegistration =
            CreateAreteRegistration(
                "Leonora Marty",
                LeonoraMartyInstance,
                LeonoraMartyNpcIdentity,
                AreteLandingPlayfieldId,
                "ARETE_LEONORA_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration PatrickSunRegistration =
            CreateAreteRegistration(
                "Patrick Sun",
                PatrickSunInstance,
                PatrickSunNpcIdentity,
                AreteLandingPlayfieldId,
                "ARETE_PATRICK_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration RemiGalloisRegistration =
            CreateAreteRegistration(
                "Remi Gallois",
                RemiGalloisInstance,
                RemiGalloisNpcIdentity,
                AreteLandingPlayfieldId,
                "ARETE_REMI_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration GreedyDesertReetRegistration =
            CreateAreteRegistration(
                "Greedy Desert Reet",
                GreedyDesertReetInstance,
                GreedyDesertReetNpcIdentity,
                AreteLandingPlayfieldId,
                "ARETE_GREEDY_REET_DIALOGUE");

        private static readonly ContentDrivenNpcDialogueRegistration WindcallerKarrecRegistration =
            CreateWindcallerRegistration(WindcallerKarrecNpcContent.Karrec);

        private static readonly ContentDrivenNpcDialogueRegistration AnnoyingDudeRegistration =
            CreateWindcallerRegistration(WindcallerKarrecNpcContent.AnnoyingDude);

        private static readonly ContentDrivenNpcDialogueRegistration MaddyCardileRegistration =
            CreateWindcallerRegistration(WindcallerKarrecNpcContent.MaddyCardile);

        private static readonly ContentDrivenNpcDialogueRegistration SubwayTailorRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Tailor",
                ExpectedNpcName = "Tailor",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = CapturedSubwayTailorDialogueContent.SourceNpcInstance
                    },
                NpcIdentityText = CapturedSubwayTailorDialogueContent.SourceNpcIdentity,
                PlayfieldId = CapturedSubwayVendorContentProvider.SubwayPlayfieldResource,
                GateEnvironmentVariableName = SubwayTailorGateEnvironmentVariableName,
                LogPrefix = "SUBWAY_TAILOR_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration VeronicaEscobarRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Scientist Veronica Escobar",
                ExpectedNpcName = ThrakGardenKeyInteractionRules.VeronicaName,
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ThrakGardenKeyInteractionRules.VeronicaInstance
                    },
                NpcIdentityText = "SimpleChar:787B54B2",
                PlayfieldId = ThrakGardenKeyInteractionRules.VeronicaPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "THRAK_GARDEN_KEY"
            };

        private static readonly ContentDrivenNpcDialogueRegistration ProphetYuttRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Prophet Yutt Thrak",
                ExpectedNpcName = ThrakGardenKeyInteractionRules.ProphetName,
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ThrakGardenKeyInteractionRules.ProphetInstance
                    },
                NpcIdentityText = "SimpleChar:78D280F6",
                PlayfieldId = ThrakGardenKeyInteractionRules.ProphetPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "THRAK_GARDEN_KEY"
            };

        private static readonly ContentDrivenNpcDialogueRegistration HypnagogicUrgaLumRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Hypnagogic Urga-Lum Thrak",
                ExpectedNpcName = ThrakGardenKeyInteractionRules.HypnagogicName,
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ThrakGardenKeyInteractionRules.HypnagogicInstance
                    },
                NpcIdentityText = "SimpleChar:79758F3A",
                PlayfieldId = ThrakGardenKeyInteractionRules.HypnagogicPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "THRAK_GARDEN_KEY"
            };

        private static readonly ContentDrivenNpcDialogueRegistration DreamingSilvertailRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Dreaming Silvertail",
                ExpectedNpcName = ThrakGardenKeyInteractionRules.DreamingSilvertailName,
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ThrakGardenKeyInteractionRules.SilvertailInstanceA
                    },
                NpcIdentityText = "SimpleChar:797652A0",
                PlayfieldId = ThrakGardenKeyInteractionRules.SilvertailPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "THRAK_GARDEN_KEY"
            };

        // Capture 20260723-221330 Nascence Life dialogs (options-only / Say what?).
        private const int NascenceFrontierPlayfieldId = 4310;
        private const int GoldmanAretePlayfieldId = 4531;
        private const int ScientistDrakeRodriguezInstance = unchecked((int)0x7963A853);
        private const int JoshuaFalkerInstance = unchecked((int)0x787B5401);
        private const int PrinceCreehanInstance = unchecked((int)0x78CCD541);

        private static readonly ContentDrivenNpcDialogueRegistration ScientistDrakeRodriguezRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Scientist Drake Rodriguez",
                ExpectedNpcName = "Scientist Drake Rodriguez",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = ScientistDrakeRodriguezInstance
                    },
                NpcIdentityText = "SimpleChar:7963A853",
                PlayfieldId = NascenceFrontierPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "NASCENCE_LIFE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration JoshuaFalkerRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Joshua Falker",
                ExpectedNpcName = "Joshua Falker",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = JoshuaFalkerInstance
                    },
                NpcIdentityText = "SimpleChar:787B5401",
                PlayfieldId = NascenceFrontierPlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "NASCENCE_LIFE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration PrinceCreehanRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Prince Creehan",
                ExpectedNpcName = "Prince Creehan",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = PrinceCreehanInstance
                    },
                NpcIdentityText = "SimpleChar:78CCD541",
                PlayfieldId = GoldmanAretePlayfieldId,
                GateEnvironmentVariableName = null,
                LogPrefix = "NASCENCE_LIFE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration CraigOrFuriousFistsRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.FuriousFistsName,
                ThrakGardenVendorInteractionRules.FuriousFistsInstance,
                ThrakGardenVendorInteractionRules.FuriousFistsIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration CraigOrPreservationRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.PreservationName,
                ThrakGardenVendorInteractionRules.PreservationInstance,
                ThrakGardenVendorInteractionRules.PreservationIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration CraigOrFlamingBarrelsRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.FlamingBarrelsName,
                ThrakGardenVendorInteractionRules.FlamingBarrelsInstance,
                ThrakGardenVendorInteractionRules.FlamingBarrelsIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration CraigOrGearAndAmmoRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.GearAndAmmoName,
                ThrakGardenVendorInteractionRules.GearAndAmmoInstance,
                ThrakGardenVendorInteractionRules.GearAndAmmoIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration CraigOrProtectionRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.ProtectionName,
                ThrakGardenVendorInteractionRules.ProtectionInstance,
                ThrakGardenVendorInteractionRules.ProtectionIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration SonLenRegistration =
            CreateThrakGardenVendorRegistration(
                ThrakGardenVendorInteractionRules.SonLenName,
                ThrakGardenVendorInteractionRules.SonLenInstance,
                ThrakGardenVendorInteractionRules.SonLenIdentityText);

        private static readonly ContentDrivenNpcDialogueRegistration[] Registrations =
        {
            RexLarssonRegistration,
            MarcusStoneRegistration,
            BarryFoodVendorRegistration,
            BorisPeacekeeperRegistration,
            DesmondCalitriRegistration,
            FlintNovakRegistration,
            AlexGibbsRegistration,
            BillRegistration,
            StanGoodmanRegistration,
            SarahGreeneRegistration,
            VernonGodfrayRegistration,
            DoctorMasonRegistration,
            LoreleiBartenderRegistration,
            LollyTheReetRegistration,
            ShippingManifestTerminalRegistration,
            MarcoSpidaRegistration,
            VaughnHammondRegistration,
            AntonioStacklundRegistration,
            KarliCappelleriRegistration,
            LeonoraMartyRegistration,
            PatrickSunRegistration,
            RemiGalloisRegistration,
            GreedyDesertReetRegistration,
            WindcallerKarrecRegistration,
            AnnoyingDudeRegistration,
            MaddyCardileRegistration,
            SubwayTailorRegistration,
            VeronicaEscobarRegistration,
            ProphetYuttRegistration,
            HypnagogicUrgaLumRegistration,
            DreamingSilvertailRegistration,
            ScientistDrakeRodriguezRegistration,
            JoshuaFalkerRegistration,
            PrinceCreehanRegistration,
            CraigOrFuriousFistsRegistration,
            CraigOrPreservationRegistration,
            CraigOrFlamingBarrelsRegistration,
            CraigOrGearAndAmmoRegistration,
            CraigOrProtectionRegistration,
            SonLenRegistration
        };

        private static readonly Dictionary<string, DialogueSessionRecord> SessionsByCharacter =
            new Dictionary<string, DialogueSessionRecord>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConditionalWeakTable<ICharacter, object> TailorOpenHistoryByCharacter =
            new ConditionalWeakTable<ICharacter, object>();

        private static readonly object SyncRoot = new object();

        private static DialogueSessionService sharedDialogueSessionService;

        private static ContentDrivenNpcDialogueRegistration CreateWindcallerRegistration(
            WindcallerKarrecNpcDefinition definition)
        {
            return new ContentDrivenNpcDialogueRegistration
                   {
                       Name = definition.DisplayName,
                       ExpectedNpcName = definition.DisplayName,
                       NpcIdentity = new Identity
                                     {
                                         Type = IdentityType.CanbeAffected,
                                         Instance = definition.SourceNpcInstance
                                     },
                       NpcIdentityText = definition.SourceNpcIdentity,
                       PlayfieldId = definition.PlayfieldId,
                       GateEnvironmentVariableName = null,
                       LogPrefix = "SUBWAY_KARREC_DIALOGUE"
                   };
        }

        private static ContentDrivenNpcDialogueRegistration CreateAreteRegistration(
            string displayName,
            int sourceNpcInstance,
            string npcIdentityText,
            int playfieldId,
            string logPrefix)
        {
            return new ContentDrivenNpcDialogueRegistration
                   {
                       Name = displayName,
                       ExpectedNpcName = displayName,
                       NpcIdentity = new Identity
                                     {
                                         Type = IdentityType.CanbeAffected,
                                         Instance = sourceNpcInstance
                                     },
                       NpcIdentityText = npcIdentityText,
                       PlayfieldId = playfieldId,
                       GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                       LogPrefix = logPrefix
                   };
        }

        private static ContentDrivenNpcDialogueRegistration CreateThrakGardenVendorRegistration(
            string displayName,
            int sourceNpcInstance,
            string npcIdentityText)
        {
            return new ContentDrivenNpcDialogueRegistration
                   {
                       Name = displayName,
                       ExpectedNpcName = displayName,
                       NpcIdentity = ThrakGardenVendorInteractionRules.CreateIdentity(sourceNpcInstance),
                       NpcIdentityText = npcIdentityText,
                       PlayfieldId = ThrakGardenVendorInteractionRules.PlayfieldId,
                       GateEnvironmentVariableName = null,
                       LogPrefix = "THRAK_GARDEN_VENDOR"
                   };
        }

        public static bool IsRexLarssonRoutingEnabled
        {
            get
            {
                return IsRegistrationEnabled(RexLarssonRegistration);
            }
        }

        public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
        {
            ContentDrivenNpcDialogueRegistration registration = FindRegistration(npc);
            if (registration == null)
            {
                return false;
            }

            if (!IsRegistrationEnabled(registration))
            {
                return false;
            }

            if (!IsExpectedPlayfield(npc, registration))
            {
                LogSkipped(
                    registration,
                    "routing skipped because NPC is not in expected playfield "
                    + registration.PlayfieldId + ".");
                return false;
            }

            ICharacter source = ResolveCharacter(npc, sourceIdentity);
            if (source == null)
            {
                LogSkipped(registration, "routing skipped because source character was not found.");
                return false;
            }

            if (!IsExpectedPlayfield(source, registration))
            {
                LogSkipped(
                    registration,
                    "routing skipped because source character is not in expected playfield "
                    + registration.PlayfieldId + ".");
                return false;
            }

            return TryStartDialogueForSource(source, npc, registration);
        }

        public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
        {
            if (source == null || source.Playfield == null)
            {
                return false;
            }

            ICharacter npc = null;
            try
            {
                npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, targetIdentity);
            }
            catch
            {
                npc = null;
            }

            ContentDrivenNpcDialogueRegistration registration = FindRegistration(npc)
                                                                  ?? FindRegistration(targetIdentity);
            if (registration == null)
            {
                return false;
            }

            if (!IsRegistrationEnabled(registration))
            {
                LogSkipped(
                    registration,
                    "direct trade routing skipped because "
                    + registration.GateEnvironmentVariableName + " is not enabled.");
                return false;
            }

            if (!IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            if (!IsRegisteredNpc(npc, registration) || !IsExpectedPlayfield(npc, registration))
            {
                LogSkipped(
                    registration,
                    "direct trade routing skipped because registered target was not found in expected playfield.");
                return false;
            }

            return TryStartDialogueForSource(source, npc, registration);
        }

        /// <summary>
        /// After a content-driven KnuBot trade completes, advance past the trade-hold node and emit the
        /// next prompt/options (capture 20260718-185306: RejectedItems then KnubotAnswerList).
        /// </summary>
        public static bool TryResumeAfterNpcTrade(ICharacter source, Identity npcIdentity)
        {
            if (source == null)
            {
                return false;
            }

            // Prefer the FinishTrade target's session. Walking Registrations in declaration
            // order (FindActiveSessionRegistration) can latch onto a stale earlier NPC
            // session — Shipping Manifest then CloseChat with no Re-route / tip handoff
            // (ZoneEngineLog 2026-08-02 05:44: smt-hack-inspect done, no post-trade advanced).
            ContentDrivenNpcDialogueRegistration registration;
            string sessionKey;
            DialogueSessionRecord record;
            if (!TryGetActiveSessionForNpc(source, npcIdentity, out registration, out sessionKey, out record))
            {
                return false;
            }

            if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            DialogueSessionService service;
            if (!TryGetSessionService(registration, out service))
            {
                return false;
            }

            // Auto-select the synthetic "(Continue after trade)" / first option on the hold node.
            DialogueSessionResult result = service.SelectOption(record.Session, 0);
            if (!result.IsValid)
            {
                LogValidation(registration, "post-trade dialogue advance failed", result.Validation);
                LogDialogue(
                    registration,
                    "post-trade advance failed character=" + source.Identity.ToString(true)
                    + " target=" + npcIdentity.ToString(true)
                    + " node=" + (record.Session == null ? "<none>" : record.Session.CurrentNodeId));
                return false;
            }

            if (result.Session == null || !result.Session.IsActive)
            {
                CloseSession(source, sessionKey, registration, true);
                return true;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[sessionKey] =
                    new DialogueSessionRecord { Registration = registration, Session = result.Session };
            }

            LogDialogue(
                registration,
                "post-trade advanced character=" + source.Identity.ToString(true)
                + " to=" + result.Session.CurrentNodeId);

            // Bill capture: tip update is on FinishTrade; re-assert after dialogue advance.
            if (IsRegistration(registration, BillRegistration))
            {
                SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
            }

            if (IsRegistration(registration, StanGoodmanRegistration))
            {
                SafeQuestFullUpdateSender.TrySendDeliverFactoryToSarahAndNanoTipsHandoff(source);
            }

            if (IsRegistration(registration, SarahGreeneRegistration))
            {
                SafeQuestFullUpdateSender.TrySendDeliverArmorToVernonHandoff(source);
            }

            SendDialogueNode(source, result, registration, null);
            return true;
        }

        public static bool ShouldSuppressCombat(ICharacter target)
        {
            ContentDrivenNpcDialogueRegistration registration = FindRegistration(target);
            return registration != null
                   && IsRegistrationEnabled(registration)
                   && IsExpectedPlayfield(target, registration);
        }

        public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
        {
            if (source == null)
            {
                return false;
            }

            // Name-bound Arete NPCs (Rex/Marcus) use dynamic spawn ids; resolve via live NPC
            // first so answers keep the bound registration that opened the window.
            // Capture-id Targets (e.g. Stan 78E0FC65) are not in the playfield pool — GetObject
            // can throw; never let that abort answer routing.
            ICharacter targetNpc = null;
            if (source.Playfield != null)
            {
                try
                {
                    targetNpc = Pool.Instance.GetObject<ICharacter>(
                        source.Playfield.Identity,
                        targetIdentity);
                }
                catch
                {
                    targetNpc = null;
                }
            }

            ContentDrivenNpcDialogueRegistration registration = FindRegistration(targetNpc)
                                                                  ?? FindRegistration(targetIdentity);
            if (registration == null)
            {
                registration = FindActiveSessionRegistration(source);
                if (registration == null)
                {
                    return false;
                }
            }

            if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            bool isRegisteredTarget = IsRegisteredIdentity(targetIdentity, registration);
            if (!isRegisteredTarget && !HasActiveSession(source, registration))
            {
                return false;
            }

            DialogueSessionService service;
            if (!TryGetSessionService(registration, out service))
            {
                return false;
            }

            string sessionKey = CreateSessionKey(source.Identity, registration);
            DialogueSession session;
            lock (SyncRoot)
            {
                DialogueSessionRecord record;
                SessionsByCharacter.TryGetValue(sessionKey, out record);
                session = record == null ? null : record.Session;
            }

            if (session == null)
            {
                LogDialogue(
                    registration,
                    "answer ignored because no routed session exists for character="
                    + source.Identity.ToString(true)
                    + " target=" + targetIdentity.ToString(true)
                    + " answer=" + answerIndex);
                return false;
            }

            string previousNodeId = session.CurrentNodeId;
            string selectedOptionText = ResolveSelectedOptionText(service, session, answerIndex);
            LogDialogue(
                registration,
                "answer received character=" + source.Identity.ToString(true)
                + " target=" + targetIdentity.ToString(true)
                + " answer=" + answerIndex
                + " node=" + previousNodeId);

            DialogueSessionResult result = service.SelectOption(session, answerIndex);
            if (!result.IsValid)
            {
                LogValidation(registration, "dialogue option failed", result.Validation);
                CloseSession(source, sessionKey, registration, true);
                return true;
            }

            if (IsRegistration(registration, KarliCappelleriRegistration))
            {
                DialogueSessionResult branchOverride =
                    KarliCappelleriQuestRuntime.ApplyDoingBranchOverride(
                        service,
                        result,
                        previousNodeId,
                        answerIndex,
                        source);
                if (branchOverride != null)
                {
                    result = branchOverride;
                }
            }

            LogRecordedActions(source, result, registration);
            TryHandleTailorMeasurementGrant(
                source,
                registration,
                previousNodeId,
                answerIndex);
            Func<bool> suppressOptionsForTradeHold = delegate
            {
                return TryHandleWindcallerSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex)
                       || TryHandleThrakGardenKeySideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex)
                       || TryHandleThrakGardenVendorSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex)
                       || TryHandleRexMarcusTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleAlexTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleBillTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleStanTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleSarahTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleDesmondTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleVernonTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleDoctorMasonTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleLoreleiTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleLoreleiVendorSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex)
                       || TryHandleVaughnTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleLeonoraTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleShinySwordTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity)
                       || TryHandleShippingManifestTradeHoldSideEffect(
                           source,
                           registration,
                           previousNodeId,
                           answerIndex,
                           targetIdentity);
            };

            if (result.Session == null || !result.Session.IsActive)
            {
                DispatchCapturedQuestAnswerSideEffects(
                    source,
                    registration,
                    previousNodeId,
                    answerIndex,
                    targetIdentity,
                    false);
                LogDialogue(
                    registration,
                    "answer closed session character=" + source.Identity.ToString(true)
                    + " previousNode=" + previousNodeId
                    + " answer=" + answerIndex);
                CloseSession(source, sessionKey, registration, true);
                return true;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[sessionKey] =
                    new DialogueSessionRecord { Registration = registration, Session = result.Session };
            }

            LogDialogue(
                registration,
                "answer advanced character=" + source.Identity.ToString(true)
                + " from=" + previousNodeId
                + " to=" + result.Session.CurrentNodeId
                + " answer=" + answerIndex);

            // Coordinator owns quest packets; router only selects nodes + trade hold.
            if (IsRegistration(registration, MarcusStoneRegistration))
            {
                RexMarcusChainCoordinator.OnMarcusAnswer(
                    source,
                    previousNodeId,
                    answerIndex,
                    selectedOptionText,
                    IsRegistrationEnabled(registration));
            }

            if (IsRegistration(registration, FlintNovakRegistration))
            {
                FlintBioComQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, AlexGibbsRegistration))
            {
                KneecappingQuestRuntime.TryHandleAlexDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, BillRegistration))
            {
                SurveillanceUplinkQuestRuntime.TryHandleBillDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, StanGoodmanRegistration))
            {
                StanGoodmanQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, SarahGreeneRegistration))
            {
                SarahGreeneQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, VernonGodfrayRegistration))
            {
                VernonGodfrayQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, DoctorMasonRegistration))
            {
                DoctorMasonQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, LoreleiBartenderRegistration)
                || IsRegistration(registration, LollyTheReetRegistration))
            {
                LoreleiQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (IsRegistration(registration, ShippingManifestTerminalRegistration))
            {
                ShippingManifestTerminalQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            PaceKnuBotPackets();

            // Capture 20260722-Alex-dialog / 074847: AppendText then StartTrade only.
            // Do not emit AnswerList after StartTrade — that leaves the drag/drop
            // instruction with no slots/Accept (dialogue chrome stuck).
            if (IsAlexTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                // Capture 20260722-Alex-dialog #45→#46: prompt before trade chrome.
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryOpenAlexTradeHoldWithoutDialogue(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260722-bill-dialog #151→#144: AppendText then StartTrade.
            if (IsBillTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleBillTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260722-212421: AppendText (Excellent...) then StartTrade.
            if (IsStanTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleStanTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260722-214957: AppendText (clap / passin' it over) then StartTrade.
            if (IsSarahTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleSarahTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260801-Desmond Calitri: burger answer AppendText then StartTrade.
            if (IsDesmondTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleDesmondTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260722-214957: AppendText (Very well, leave it here) then StartTrade.
            if (IsVernonTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleVernonTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            // Capture 20260722-230902 / 231133: AppendText then StartTrade (show 1 slot / chip 2 slots).
            if (IsDoctorMasonTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleDoctorMasonTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            if (TryHandleLoreleiTradeHoldSideEffect(
                    source,
                    registration,
                    previousNodeId,
                    answerIndex,
                    targetIdentity))
            {
                return true;
            }

            if (TryHandleLoreleiVendorSideEffect(
                    source,
                    registration,
                    previousNodeId,
                    answerIndex))
            {
                return true;
            }

            // Capture 20260722-233205: AppendText ("Very well then, let's see it.") then StartTrade.
            if (IsVaughnTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleVaughnTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            if (TryHandleShippingManifestTradeHoldSideEffect(
                    source,
                    registration,
                    previousNodeId,
                    answerIndex,
                    targetIdentity))
            {
                return true;
            }

            if (IsLeonoraTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleLeonoraTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            if (IsShinySwordTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                SendDialoguePromptOnly(source, result, registration);
                PaceKnuBotPackets();
                if (TryHandleShinySwordTradeHoldSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex,
                        targetIdentity))
                {
                    return true;
                }
            }

            SendDialogueNode(source, result, registration, suppressOptionsForTradeHold);

            DispatchCapturedQuestAnswerSideEffects(
                source,
                registration,
                previousNodeId,
                answerIndex,
                targetIdentity,
                true);

            if (IsRegistration(registration, RexLarssonRegistration))
            {
                RexMarcusChainCoordinator.OnRexAnswer(
                    source,
                    previousNodeId,
                    answerIndex,
                    IsRegistrationEnabled(registration));
            }

            return true;
        }

        private static void TryHandleTailorMeasurementGrant(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, SubwayTailorRegistration)
                || !string.Equals(
                    previousNodeId,
                    CapturedSubwayTailorDialogueContent.MeasurementNodeId,
                    StringComparison.OrdinalIgnoreCase)
                || answerIndex < 0
                || answerIndex > 7)
            {
                return;
            }

            bool granted = CapturedSubwayTailorDialogueRuntime.TryGrantMeasurementItem(source, answerIndex);
            LogDialogue(
                registration,
                "measurement item grant " + (granted ? "succeeded" : "failed")
                + " character=" + source.Identity.ToString(true)
                + " answer=" + answerIndex);
        }

        public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
        {
            if (source == null)
            {
                return false;
            }

            ContentDrivenNpcDialogueRegistration registration = FindRegistration(targetIdentity);
            if (registration == null)
            {
                registration = FindActiveSessionRegistration(source);
                if (registration == null)
                {
                    return false;
                }
            }

            if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            if (!IsRegisteredIdentity(targetIdentity, registration) && !HasActiveSession(source, registration))
            {
                return false;
            }

            string sessionKey = CreateSessionKey(source.Identity, registration);
            bool hadSession;
            lock (SyncRoot)
            {
                hadSession = SessionsByCharacter.Remove(sessionKey);
            }

            if (hadSession)
            {
                ResumeCapturedDialoguePatrol(source, registration, targetIdentity);
                LogDialogue(registration, "session closed by client character=" + source.Identity.ToString(true));
                return true;
            }

            return false;
        }

        private static bool TryStartDialogueForSource(
            ICharacter source,
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (IsRegistration(registration, RemiGalloisRegistration)
                && RemiGalloisQuestRuntime.IsCompleted(source))
            {
                // No post-completion interaction was captured. Fail closed rather than
                // promoting the inferred remi_done vendor branch from the draft pack.
                return CloseRegisteredDialogueSafely(source, npc, registration);
            }

            if (IsRegistration(registration, WindcallerKarrecRegistration))
            {
                WindcallerKarrecTradeAdapter.TryResumeDurableCompletion(source, registration.NpcIdentity);
                if (WindcallerKarrecQuestRuntime.IsCompleted(source))
                {
                    return CloseRegisteredDialogueSafely(source, npc, registration);
                }

                if (WindcallerKarrecQuestRuntime.IsActive(source)
                    && !WindcallerKarrecQuestRuntime.HasBothOfferingItems(source))
                {
                    WindcallerKarrecPacketSender.TrySendQuestFullUpdate(source, registration.NpcIdentity);
                    return CloseRegisteredDialogueSafely(source, npc, registration);
                }
            }
            else if ((IsRegistration(registration, AnnoyingDudeRegistration)
                      || IsRegistration(registration, MaddyCardileRegistration))
                     && !WindcallerKarrecQuestRuntime.IsActive(source))
            {
                return CloseRegisteredDialogueSafely(source, npc, registration);
            }

            DialogueSessionService service;
            if (!TryGetSessionService(registration, out service))
            {
                return false;
            }

            string requestedStartNodeId = ResolveRequestedStartNodeId(source, registration);
            DialogueSessionResult result = string.IsNullOrWhiteSpace(requestedStartNodeId)
                                               ? service.StartSession(registration.NpcIdentityText)
                                               : service.StartSessionAtNode(
                                                   registration.NpcIdentityText,
                                                   requestedStartNodeId);
            if ((!result.IsValid || result.Session == null)
                && !string.IsNullOrWhiteSpace(requestedStartNodeId)
                && IsRegistration(registration, AlexGibbsRegistration)
                && string.Equals(
                    requestedStartNodeId,
                    PersonalizedRobotBrainQuestRuntime.AlexBrainTurnInNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Prefer showing default Alex options over an immediate close if the
                // brain turn-in node failed to resolve from content.
                LogValidation(registration, "brain turn-in start failed; falling back to root", result.Validation);
                requestedStartNodeId = null;
                result = service.StartSession(registration.NpcIdentityText);
            }

            if (!result.IsValid || result.Session == null)
            {
                LogValidation(registration, "dialogue start failed", result.Validation);
                if (!string.IsNullOrWhiteSpace(requestedStartNodeId))
                {
                    SendOpenChatWindow(source, registration);
                    PaceKnuBotPackets();
                    KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
                    LogDialogue(
                        registration,
                        "return-state start node unavailable; closed safely character="
                        + source.Identity.ToString(true)
                        + " requestedNode=" + requestedStartNodeId
                        + " chainState=" + DescribeChainState(source, registration));
                    return true;
                }

                return false;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[CreateSessionKey(source.Identity, registration)] =
                    new DialogueSessionRecord { Registration = registration, Session = result.Session };
            }

            PauseCapturedDialoguePatrol(npc, registration);

            FaceNpcTowardSource(npc, source);
            SendOpenChatWindow(source, registration);
            PaceKnuBotPackets();

            // Emit prompt/options first. Coordinator owns all quest packet projection —
            // open must never re-inject Talk to Marcus or complete B196.
            SendDialogueNode(source, result, registration);

            if (IsRegistration(registration, RexLarssonRegistration))
            {
                RexMarcusChainCoordinator.OnRexOpen(source, IsRegistrationEnabled(registration));
            }
            else if (IsRegistration(registration, MarcusStoneRegistration))
            {
                RexMarcusChainCoordinator.OnMarcusOpen(source);
            }

            LogDialogue(
                registration,
                "started character=" + source.Identity.ToString(true)
                + " node=" + result.Session.CurrentNodeId
                + " requestedStartNode=" + (string.IsNullOrWhiteSpace(requestedStartNodeId)
                                                ? "<default>"
                                                : requestedStartNodeId)
                + " chainState=" + DescribeChainState(source, registration));

            return true;
        }

        private static bool TryHandleRexMarcusTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveMarcusIdentity)
        {
            if (!IsRegistration(registration, MarcusStoneRegistration) || answerIndex != 0)
            {
                return false;
            }

            Identity tradeTarget = liveMarcusIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            if (string.Equals(
                previousNodeId,
                RexMarcusChainCoordinator.MarcusReturnNodeId,
                StringComparison.OrdinalIgnoreCase))
            {
                return RexMarcusChainCoordinator.TryBeginMarcusReturnTrade(source, tradeTarget);
            }

            if (string.Equals(
                previousNodeId,
                RexMarcusChainCoordinator.MarcusHealReturnNodeId,
                StringComparison.OrdinalIgnoreCase))
            {
                return MarcusWoundedWorkersQuestRuntime.TryBeginStimReturnTrade(source, tradeTarget);
            }

            return false;
        }

        private static bool TryOpenAlexTradeHoldWithoutDialogue(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveAlexIdentity)
        {
            if (!IsAlexTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            // Match OpenChatWindow / AnswerList target (registration identity).
            // Mismatched live targets can show StartTrade text without slot chrome.
            Identity tradeTarget = registration.NpcIdentity;
            if (liveAlexIdentity.Type == IdentityType.CanbeAffected
                && liveAlexIdentity.Instance == AlexGibbsInstance)
            {
                tradeTarget = liveAlexIdentity;
            }

            if (string.Equals(
                previousNodeId,
                PersonalizedRobotBrainQuestRuntime.AlexBrainTurnInNodeId,
                StringComparison.OrdinalIgnoreCase))
            {
                return PersonalizedRobotBrainQuestRuntime.TryBeginBrainTurnInTrade(source, tradeTarget);
            }

            return FlintBioComQuestRuntime.TryBeginAlexTrade(source, tradeTarget);
        }

        private static bool IsAlexTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, AlexGibbsRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                       previousNodeId,
                       FlintBioComQuestRuntime.AlexTradeOfferNodeId,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       previousNodeId,
                       PersonalizedRobotBrainQuestRuntime.AlexBrainTurnInNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleAlexTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveAlexIdentity)
        {
            // Preferred path is TryOpenAlexTradeHoldWithoutDialogue (no trade-hold packets).
            // Keep this as a suppress fallback if SendDialogueNode is still invoked.
            return TryOpenAlexTradeHoldWithoutDialogue(
                source,
                registration,
                previousNodeId,
                answerIndex,
                liveAlexIdentity);
        }

        private static bool TryHandleBillTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveBillIdentity)
        {
            if (!IsBillTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            Identity tradeTarget = liveBillIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            return SurveillanceUplinkQuestRuntime.TryBeginBillTrade(source, tradeTarget);
        }

        private static bool IsBillTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, BillRegistration) || answerIndex != 0)
            {
                return false;
            }

            // Trade option can be selected from root or any question hub (index 0 = Alex/HC-12).
            return string.Equals(
                       previousNodeId,
                       SurveillanceUplinkQuestRuntime.BillTradeOfferNodeId,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(previousNodeId, "bill_intro", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(previousNodeId, "bill_what_do", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(previousNodeId, "bill_locations", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStanTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, StanGoodmanRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                previousNodeId,
                StanGoodmanQuestRuntime.DeliverOfferNodeId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleStanTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveStanIdentity)
        {
            if (!IsRegistration(registration, StanGoodmanRegistration) || answerIndex != 0)
            {
                return false;
            }

            if (!string.Equals(
                    previousNodeId,
                    StanGoodmanQuestRuntime.DeliverOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Identity tradeTarget = liveStanIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            return StanGoodmanQuestRuntime.TryBeginStanTrade(source, tradeTarget);
        }

        private static bool IsSarahTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, SarahGreeneRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                previousNodeId,
                SarahGreeneQuestRuntime.DeliverOfferNodeId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleSarahTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveSarahIdentity)
        {
            if (!IsSarahTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            Identity tradeTarget = liveSarahIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            return SarahGreeneQuestRuntime.TryBeginSarahTrade(source, tradeTarget);
        }

        private static bool IsDesmondTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            return IsRegistration(registration, DesmondCalitriRegistration)
                   && answerIndex == 0
                   && string.Equals(
                       previousNodeId,
                       DesmondCalitriQuestRuntime.BurgerDeliverNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleDesmondTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveDesmondIdentity)
        {
            if (!IsDesmondTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            return DesmondCalitriQuestRuntime.TryBeginDesmondTrade(
                source,
                ResolveLiveNpcIdentity(liveDesmondIdentity, registration));
        }

        private static bool IsVernonTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, VernonGodfrayRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                       previousNodeId,
                       VernonGodfrayQuestRuntime.HackOfferNodeId,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       previousNodeId,
                       "vernon_hack_first",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       previousNodeId,
                       VernonGodfrayQuestRuntime.ReturnOfferNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleVernonTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveVernonIdentity)
        {
            if (!IsVernonTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            Identity tradeTarget = liveVernonIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            if (string.Equals(
                    previousNodeId,
                    VernonGodfrayQuestRuntime.ReturnOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return VernonGodfrayQuestRuntime.TryBeginVernonReturnTrade(source, tradeTarget);
            }

            return VernonGodfrayQuestRuntime.TryBeginVernonHackTrade(source, tradeTarget);
        }

        private static bool IsDoctorMasonTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, DoctorMasonRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                       previousNodeId,
                       DoctorMasonQuestRuntime.ShowOfferNodeId,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       previousNodeId,
                       DoctorMasonQuestRuntime.ChipOfferNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleDoctorMasonTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveMasonIdentity)
        {
            if (!IsDoctorMasonTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            Identity tradeTarget = liveMasonIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            if (string.Equals(
                    previousNodeId,
                    DoctorMasonQuestRuntime.ChipOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return DoctorMasonQuestRuntime.TryBeginChipTrade(source, tradeTarget);
            }

            return DoctorMasonQuestRuntime.TryBeginShowTrade(source, tradeTarget);
        }

        private static bool TryHandleLoreleiTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveNpcIdentity)
        {
            if (answerIndex != 0)
            {
                return false;
            }

            Identity tradeTarget = liveNpcIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            if (IsRegistration(registration, LollyTheReetRegistration)
                && string.Equals(
                    previousNodeId,
                    LoreleiQuestRuntime.LollyCookieTradeNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LoreleiQuestRuntime.TryBeginLollyCookieTrade(source, tradeTarget);
            }

            if (IsRegistration(registration, LoreleiBartenderRegistration)
                && string.Equals(
                    previousNodeId,
                    LoreleiQuestRuntime.DeliverOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LoreleiQuestRuntime.TryBeginDeliverTrade(source, tradeTarget);
            }

            return false;
        }

        private static bool IsVaughnTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsRegistration(registration, VaughnHammondRegistration) || answerIndex != 0)
            {
                return false;
            }

            return string.Equals(
                previousNodeId,
                VaughnHammondQuestRuntime.IdOfferNodeId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleVaughnTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveVaughnIdentity)
        {
            if (!IsVaughnTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            Identity tradeTarget = liveVaughnIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            return VaughnHammondQuestRuntime.TryBeginVaughnTrade(source, tradeTarget);
        }

        private static bool IsLeonoraTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            return IsRegistration(registration, LeonoraMartyRegistration)
                   && answerIndex == 0
                   && string.Equals(
                       previousNodeId,
                       LeonoraMartyQuestRuntime.WithCardStartNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleLeonoraTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveLeonoraIdentity)
        {
            if (!IsLeonoraTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            return LeonoraMartyQuestRuntime.TryBeginLeonoraTrade(
                source,
                ResolveLiveNpcIdentity(liveLeonoraIdentity, registration));
        }

        private static bool IsShinySwordTradeHoldAnswer(
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            return IsRegistration(registration, GreedyDesertReetRegistration)
                   && answerIndex == 1
                   && string.Equals(
                       previousNodeId,
                       ShinySwordQuestRuntime.DemandNodeId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryHandleShinySwordTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveReetIdentity)
        {
            if (!IsShinySwordTradeHoldAnswer(registration, previousNodeId, answerIndex))
            {
                return false;
            }

            return ShinySwordQuestRuntime.TryBeginSwordTrade(
                source,
                ResolveLiveNpcIdentity(liveReetIdentity, registration));
        }

        private static void DispatchCapturedQuestAnswerSideEffects(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveNpcIdentity,
            bool responseNodeSent)
        {
            if (IsRegistration(registration, DesmondCalitriRegistration))
            {
                if (responseNodeSent)
                {
                    DesmondCalitriQuestRuntime.TryHandleDesmondDialogueAnswer(
                        source,
                        previousNodeId,
                        answerIndex);
                }

                return;
            }

            if (IsRegistration(registration, BarryFoodVendorRegistration))
            {
                if (responseNodeSent)
                {
                    DesmondCalitriQuestRuntime.TryHandleBarryDialogueAnswer(
                        source,
                        previousNodeId,
                        answerIndex);
                }

                return;
            }

            if (IsRegistration(registration, AntonioStacklundRegistration))
            {
                if (responseNodeSent)
                {
                    AntonioStacklundQuestRuntime.TryHandleDialogueAnswer(
                        source,
                        previousNodeId,
                        answerIndex);
                    if (string.Equals(
                            previousNodeId,
                            "antonio_001",
                            StringComparison.OrdinalIgnoreCase)
                        && answerIndex == 1)
                    {
                        CapturedAreteAntonioStacklundVendorInteractionHandler.Default.TryOpenShop(
                            source,
                            ResolveLiveNpcIdentity(liveNpcIdentity, registration));
                    }
                }

                return;
            }

            if (IsRegistration(registration, KarliCappelleriRegistration))
            {
                if (responseNodeSent)
                {
                    KarliCappelleriQuestRuntime.TryHandleDialogueAnswer(
                        source,
                        previousNodeId,
                        answerIndex);
                }

                return;
            }

            if (IsRegistration(registration, PatrickSunRegistration))
            {
                if (responseNodeSent)
                {
                    PatrickSunQuestRuntime.TryHandleDialogueAnswer(
                        source,
                        previousNodeId,
                        answerIndex);
                }

                return;
            }

            if (!IsRegistration(registration, RemiGalloisRegistration))
            {
                return;
            }

            bool capturedSellOption =
                (string.Equals(previousNodeId, RemiGalloisQuestRuntime.RootNodeId, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(previousNodeId, RemiGalloisQuestRuntime.DoingNodeId, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(previousNodeId, RemiGalloisQuestRuntime.ReturnNodeId, StringComparison.OrdinalIgnoreCase))
                && answerIndex == 1;
            if (capturedSellOption)
            {
                CapturedAreteRemiGalloisVendorInteractionHandler.Default.TryOpenShop(
                    source,
                    ResolveLiveNpcIdentity(liveNpcIdentity, registration));
            }

            if (responseNodeSent
                || string.Equals(
                    previousNodeId,
                    RemiGalloisQuestRuntime.AcceptNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                RemiGalloisQuestRuntime.TryHandleDialogueAnswer(
                    source,
                    previousNodeId,
                    answerIndex);
            }

            if (responseNodeSent
                && answerIndex == 0
                && string.Equals(
                    previousNodeId,
                    RemiGalloisQuestRuntime.OfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                RemiGalloisQuestRuntime.EmitAcceptTipAndHellfyre(source);
            }
        }

        private static Identity ResolveLiveNpcIdentity(
            Identity candidate,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return candidate.Type == IdentityType.CanbeAffected && candidate.Instance != 0
                       ? candidate
                       : registration.NpcIdentity;
        }

        private static bool TryHandleLoreleiVendorSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            // Capture 20260721-loralei: greet/deliver option "What do you have?" (index 1).
            // Knubot Shop cart also sends GenericCmd Use (handled separately).
            if (!IsRegistration(registration, LoreleiBartenderRegistration) || answerIndex != 1)
            {
                return false;
            }

            if (!string.Equals(previousNodeId, "lorelei_greet", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    previousNodeId,
                    LoreleiQuestRuntime.DeliverOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            CapturedAreteLoreleiVendorInteractionHandler.Default.TryOpenShop(
                source,
                registration.NpcIdentity);
            return false;
        }

        private static bool TryHandleShippingManifestTradeHoldSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            Identity liveTerminalIdentity)
        {
            if (!IsRegistration(registration, ShippingManifestTerminalRegistration) || answerIndex != 0)
            {
                return false;
            }

            if (!string.Equals(
                    previousNodeId,
                    ShippingManifestTerminalQuestRuntime.ApplyHackerToolNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Identity tradeTarget = liveTerminalIdentity;
            if (tradeTarget.Type != IdentityType.CanbeAffected || tradeTarget.Instance == 0)
            {
                tradeTarget = registration.NpcIdentity;
            }

            return ShippingManifestTerminalQuestRuntime.TryBeginTerminalTrade(source, tradeTarget);
        }

        private static RexQuestPreviewEmissionResult TryHandleDialogueSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            // Quest packet side-effects moved to RexMarcusChainCoordinator.
            return RexQuestPreviewEmissionResult.NotApplicable();
        }

        private static MarcusB18FCompletionResult TryHandleMarcusB18FCompletion(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex,
            string optionText)
        {
            // Quest packet side-effects moved to RexMarcusChainCoordinator.OnMarcusAnswer.
            return MarcusB18FCompletionResult.NotApplicable();
        }

        private static bool TryHandleWindcallerSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (IsRegistration(registration, WindcallerKarrecRegistration)
                && string.Equals(previousNodeId, "karrec_223626_005", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                MissionOperationResult acceptance = WindcallerKarrecQuestRuntime.Accept(source);
                if (acceptance.Status == MissionOperationStatus.Applied
                    || acceptance.Status == MissionOperationStatus.AlreadyApplied)
                {
                    WindcallerKarrecPacketSender.TrySendQuestFullUpdate(source, registration.NpcIdentity);
                }

                return false;
            }

            if (IsRegistration(registration, WindcallerKarrecRegistration)
                && string.Equals(
                    previousNodeId,
                    "karrec_223626_return_offer",
                    StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                WindcallerKarrecTradeAdapter.BeginTrade(source, registration.NpcIdentity);
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Move the items you want to give to Windcaller Karrec into the available slots in the Give Item Tab on the right side of this window and press \"Accept'.",
                    2);
                return true;
            }

            if (IsRegistration(registration, AnnoyingDudeRegistration)
                && string.Equals(previousNodeId, "annoying_223626_006", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                WindcallerKarrecQuestRuntime.TryGrantBurger(source);
                return false;
            }

            if (IsRegistration(registration, MaddyCardileRegistration)
                && string.Equals(previousNodeId, "maddy_223626_004", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                WindcallerKarrecQuestRuntime.TryGrantCreditCard(source);
            }

            return false;
        }

        private static bool TryHandleThrakGardenKeySideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (IsRegistration(registration, VeronicaEscobarRegistration)
                && string.Equals(previousNodeId, "veronica_004", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                ThrakGardenKeyQuestRuntime.AcceptQuest(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
                ThrakGardenKeyQuestRuntime.TryGrantAnalyzer(source);
                return false;
            }

            if (IsRegistration(registration, ProphetYuttRegistration)
                && string.Equals(previousNodeId, "prophet_001", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "ProphetDevice");
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Drag and drop the item(s) you want to give to Prophet Yutt Thrak into one of the slots available and press \"accept\"",
                    1);
                return true;
            }

            // Capture 20260718-230923: commitment grants Insignia and replaces Veronica journal
            // with VeronicaUpdated — Veronica stays until Insignia of Thrak trade.
            if (IsRegistration(registration, ProphetYuttRegistration)
                && string.Equals(previousNodeId, "prophet_004", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                ThrakGardenKeyQuestRuntime.AcceptQuest(
                    source,
                    ThrakGardenKeyInteractionRules.QuestInsignia);
                return false;
            }

            // Capture: "I have proof..." only opens insignia trade (quest already Active from prophet_004).
            if (IsRegistration(registration, ProphetYuttRegistration)
                && (string.Equals(previousNodeId, "prophet_005", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(previousNodeId, "prophet_need_insignia", StringComparison.OrdinalIgnoreCase))
                && answerIndex == 0)
            {
                if (!ThrakGardenKeyQuestRuntime.IsMissionActive(
                        source,
                        ThrakGardenKeyInteractionRules.QuestInsignia)
                    || !ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source))
                {
                    // Device step not finished — do not open insignia trade or spam journal QFUs.
                    return false;
                }

                ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "ProphetInsignia");
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Drag and drop the item(s) you want to give to Prophet Yutt Thrak into one of the slots available and press \"accept\"",
                    1);
                return true;
            }

            // Capture: after the scientist/Jobe answer, StartTrade opens immediately — no "(Open trade)" click.
            if (IsRegistration(registration, HypnagogicUrgaLumRegistration)
                && ((string.Equals(previousNodeId, "hyp_001", StringComparison.OrdinalIgnoreCase)
                     && answerIndex == 1)
                    || (string.Equals(previousNodeId, "hyp_002", StringComparison.OrdinalIgnoreCase)
                        && answerIndex == 0)))
            {
                ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "HypAnalyzer");
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Drag and drop the item(s) you want to give to Hypnagogic Urga-Lum Thrak into one of the slots available and press \"accept\"",
                    1);
                return true;
            }

            if (IsRegistration(registration, HypnagogicUrgaLumRegistration)
                && string.Equals(previousNodeId, "hyp_return", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "HypReturn");
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Drag and drop the item(s) you want to give to Hypnagogic Urga-Lum Thrak into one of the slots available and press \"accept\"",
                    1);
                return true;
            }

            if (IsRegistration(registration, DreamingSilvertailRegistration)
                && string.Equals(previousNodeId, "silver_001", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "Silvertail");
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    "Drag and drop the item(s) you want to give to Dreaming Silvertail into one of the slots available and press \"accept\"",
                    1);
                return true;
            }

            return false;
        }

        private static bool TryHandleThrakGardenVendorSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (!IsThrakGardenCraigOrRegistration(registration)
                || !string.Equals(
                    previousNodeId,
                    ThrakGardenVendorInteractionRules.CraigOrRootNodeId,
                    StringComparison.OrdinalIgnoreCase)
                || answerIndex != 0)
            {
                return false;
            }

            // Capture 20260718-210135: "Business. Let's see what you've got." opens the vendor shop.
            CapturedThrakGardenVendorInteractionHandler.Default.TryOpenShop(
                source,
                registration.NpcIdentity);
            return false;
        }

        private static bool IsThrakGardenCraigOrRegistration(
            ContentDrivenNpcDialogueRegistration registration)
        {
            return IsRegistration(registration, CraigOrFuriousFistsRegistration)
                   || IsRegistration(registration, CraigOrPreservationRegistration)
                   || IsRegistration(registration, CraigOrFlamingBarrelsRegistration)
                   || IsRegistration(registration, CraigOrGearAndAmmoRegistration)
                   || IsRegistration(registration, CraigOrProtectionRegistration);
        }

        private static string ResolveRequestedStartNodeId(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (IsRegistration(registration, SubwayTailorRegistration))
            {
                lock (SyncRoot)
                {
                    object marker;
                    bool hasPriorOpen = TailorOpenHistoryByCharacter.TryGetValue(source, out marker);
                    if (!hasPriorOpen)
                    {
                        TailorOpenHistoryByCharacter.Add(source, new object());
                    }

                    return CapturedSubwayTailorDialogueContent.ResolveRootNodeId(hasPriorOpen);
                }
            }

            if (IsRegistration(registration, WindcallerKarrecRegistration))
            {
                return WindcallerKarrecQuestRuntime.HasBothOfferingItems(source)
                           ? "karrec_223626_return_offer"
                           : null;
            }

            if (IsRegistration(registration, AlexGibbsRegistration))
            {
                return KneecappingQuestRuntime.ResolveAlexStartNodeId(source);
            }

            if (IsRegistration(registration, DesmondCalitriRegistration))
            {
                return DesmondCalitriQuestRuntime.ResolveDesmondStartNodeId(source);
            }

            if (IsRegistration(registration, BarryFoodVendorRegistration))
            {
                return DesmondCalitriQuestRuntime.ResolveBarryStartNodeId(source);
            }

            if (IsRegistration(registration, StanGoodmanRegistration))
            {
                return StanGoodmanQuestRuntime.ResolveStanStartNodeId(source);
            }

            if (IsRegistration(registration, SarahGreeneRegistration))
            {
                return SarahGreeneQuestRuntime.ResolveSarahStartNodeId(source);
            }

            if (IsRegistration(registration, VernonGodfrayRegistration))
            {
                return VernonGodfrayQuestRuntime.ResolveVernonStartNodeId(source);
            }

            if (IsRegistration(registration, DoctorMasonRegistration))
            {
                return DoctorMasonQuestRuntime.ResolveMasonStartNodeId(source);
            }

            if (IsRegistration(registration, LoreleiBartenderRegistration))
            {
                return LoreleiQuestRuntime.ResolveLoreleiStartNodeId(source);
            }

            if (IsRegistration(registration, LollyTheReetRegistration))
            {
                return LoreleiQuestRuntime.ResolveLollyStartNodeId(source);
            }

            if (IsRegistration(registration, LeonoraMartyRegistration))
            {
                return LeonoraMartyQuestRuntime.ResolveLeonoraStartNodeId(source);
            }

            if (IsRegistration(registration, PatrickSunRegistration))
            {
                return PatrickSunQuestRuntime.ResolvePatrickStartNodeId(source);
            }

            if (IsRegistration(registration, RemiGalloisRegistration))
            {
                return RemiGalloisQuestRuntime.ResolveRemiStartNodeId(source);
            }

            if (IsRegistration(registration, ProphetYuttRegistration))
            {
                // Capture: Ancient Device first until inspect. Then Insignia Active → mark speech.
                if (!ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source))
                {
                    return null;
                }

                if (ThrakGardenKeyQuestRuntime.IsMissionActive(
                        source,
                        ThrakGardenKeyInteractionRules.QuestInsignia))
                {
                    ThrakGardenKeyQuestRuntime.ApplyInsigniaCommitmentHandoff(source);
                    return "prophet_need_insignia";
                }

                return null;
            }

            if (IsRegistration(registration, HypnagogicUrgaLumRegistration))
            {
                // Prior Hyp trades could delete Ancient Device; restore so combine/souls chain can continue.
                if (ThrakGardenKeyQuestRuntime.IsMissionActive(
                        source,
                        ThrakGardenKeyInteractionRules.QuestSouls)
                    || ThrakGardenKeyQuestRuntime.IsMissionActive(
                        source,
                        ThrakGardenKeyInteractionRules.QuestReturn)
                    || ThrakGardenKeyQuestRuntime.IsMissionCompleted(
                        source,
                        ThrakGardenKeyInteractionRules.QuestGarden))
                {
                    ThrakGardenKeyQuestRuntime.TryForceReturnAncientDevice(source);
                }

                if (ThrakGardenKeyQuestRuntime.GetSoulCount(source) >= 3
                    || ThrakGardenKeyQuestRuntime.IsMissionActive(
                        source,
                        ThrakGardenKeyInteractionRules.QuestReturn)
                    || ThrakGardenKeyQuestRuntime.IsMissionCompleted(
                        source,
                        ThrakGardenKeyInteractionRules.QuestSouls))
                {
                    return "hyp_return";
                }

                return null;
            }

            if (!IsRegistration(registration, RexLarssonRegistration)
                && !IsRegistration(registration, MarcusStoneRegistration))
            {
                return null;
            }

            if (IsRegistration(registration, MarcusStoneRegistration))
            {
                return RexMarcusChainCoordinator.ResolveMarcusStartNodeId(source);
            }

            return RexMarcusChainCoordinator.ResolveRexStartNodeId(source);
        }

        private static string DescribeChainState(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (IsRegistration(registration, RexLarssonRegistration)
                || IsRegistration(registration, MarcusStoneRegistration))
            {
                return RexMarcusChainCoordinator.GetPhase(source).ToString();
            }

            if (IsRegistration(registration, WindcallerKarrecRegistration)
                || IsRegistration(registration, AnnoyingDudeRegistration)
                || IsRegistration(registration, MaddyCardileRegistration))
            {
                return WindcallerKarrecQuestRuntime.IsCompleted(source)
                           ? "Completed"
                           : WindcallerKarrecQuestRuntime.IsActive(source) ? "Active" : "NotStarted";
            }

            return "<none>";
        }

        private static string ResolveSelectedOptionText(
            DialogueSessionService service,
            DialogueSession session,
            int answerIndex)
        {
            if (service == null || session == null)
            {
                return null;
            }

            DialogueOption selectedOption = service.ListAvailableOptions(session)
                .FirstOrDefault(option => option != null && option.Index == answerIndex);
            return selectedOption == null ? null : selectedOption.Text;
        }

        private static void FaceNpcTowardSource(ICharacter npc, ICharacter source)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller != null)
            {
                controller.FaceDialoguePartner(source);
            }
        }

        private static bool HasActiveSession(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (source == null || registration == null)
            {
                return false;
            }

            DialogueSessionRecord record;
            lock (SyncRoot)
            {
                SessionsByCharacter.TryGetValue(CreateSessionKey(source.Identity, registration), out record);
            }

            return record != null && record.Session != null && record.Session.IsActive;
        }

        private static ContentDrivenNpcDialogueRegistration FindActiveSessionRegistration(ICharacter source)
        {
            ContentDrivenNpcDialogueRegistration registration;
            string sessionKey;
            DialogueSessionRecord record;
            return TryGetActiveSessionForNpc(source, Identity.None, out registration, out sessionKey, out record)
                       ? registration
                       : null;
        }

        /// <summary>
        /// Resolves the active content-driven dialogue session for a character, optionally
        /// constrained to the NPC that just finished a KnuBot trade.
        /// </summary>
        private static bool TryGetActiveSessionForNpc(
            ICharacter source,
            Identity npcIdentity,
            out ContentDrivenNpcDialogueRegistration registration,
            out string sessionKey,
            out DialogueSessionRecord record)
        {
            registration = null;
            sessionKey = null;
            record = null;
            if (source == null)
            {
                return false;
            }

            bool requireNpc = npcIdentity.Type != IdentityType.None && npcIdentity.Instance != 0;
            if (requireNpc)
            {
                ContentDrivenNpcDialogueRegistration byTarget = FindRegistration(npcIdentity);
                if (byTarget != null)
                {
                    string key = CreateSessionKey(source.Identity, byTarget);
                    DialogueSessionRecord found;
                    lock (SyncRoot)
                    {
                        SessionsByCharacter.TryGetValue(key, out found);
                    }

                    if (found != null && found.Session != null && found.Session.IsActive)
                    {
                        registration = found.Registration ?? byTarget;
                        sessionKey = key;
                        record = found;
                        return true;
                    }
                }
            }

            string characterPrefix = source.Identity.Type + ":" + source.Identity.Instance + "|";
            lock (SyncRoot)
            {
                foreach (KeyValuePair<string, DialogueSessionRecord> pair in SessionsByCharacter)
                {
                    if (pair.Value == null
                        || pair.Value.Session == null
                        || !pair.Value.Session.IsActive
                        || pair.Key == null
                        || !pair.Key.StartsWith(characterPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ContentDrivenNpcDialogueRegistration candidate = pair.Value.Registration;
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (requireNpc && !IsRegisteredIdentity(npcIdentity, candidate))
                    {
                        continue;
                    }

                    registration = candidate;
                    sessionKey = pair.Key;
                    record = pair.Value;
                    return true;
                }
            }

            if (requireNpc)
            {
                return false;
            }

            foreach (ContentDrivenNpcDialogueRegistration candidate in Registrations)
            {
                string key = CreateSessionKey(source.Identity, candidate);
                DialogueSessionRecord found;
                lock (SyncRoot)
                {
                    SessionsByCharacter.TryGetValue(key, out found);
                }

                if (found != null && found.Session != null && found.Session.IsActive)
                {
                    registration = found.Registration ?? candidate;
                    sessionKey = key;
                    record = found;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSessionService(
            ContentDrivenNpcDialogueRegistration registration,
            out DialogueSessionService service)
        {
            lock (SyncRoot)
            {
                if (sharedDialogueSessionService == null)
                {
                    AreteFrameworkRegistries registries;
                    try
                    {
                        registries = AreteFrameworkBootstrap.Current;
                    }
                    catch (Exception exception)
                    {
                        LogSkipped(registration, "central content bootstrap failed: " + exception.Message);
                        service = null;
                        return false;
                    }

                    if (registries == null || !registries.IsValid)
                    {
                        service = null;
                        return false;
                    }

                    sharedDialogueSessionService = new DialogueSessionService(registries.DialogueRegistry);
                }

                service = sharedDialogueSessionService;
                return service != null;
            }
        }

        private static void SendDialogueNode(
            ICharacter source,
            DialogueSessionResult result,
            ContentDrivenNpcDialogueRegistration registration,
            Func<bool> afterPromptBeforeOptions = null)
        {
            // Trade holds: StartTrade first, then suppress all dialogue packets.
            // AppendText/AnswerList after StartTrade strips slots/Accept on the client.
            bool suppressOptions = false;
            if (afterPromptBeforeOptions != null)
            {
                suppressOptions = afterPromptBeforeOptions();
                PaceKnuBotPackets();
            }

            if (suppressOptions)
            {
                return;
            }

            SendDialoguePromptOnly(source, result, registration);

            string[] choices = result.AvailableOptions
                .OrderBy(option => option.Index)
                .Select(option => FormatDialogueOptionText(source, option.Text))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Where(text => text.IndexOf("(Continue after trade)", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();

            if (choices.Length == 0)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
                return;
            }

            KnuBotAnswerListMessageHandler.Default.Send(source, registration.NpcIdentity, choices);
            LogDialogue(
                registration,
                "sent node=" + (result.CurrentNode == null ? "<none>" : result.CurrentNode.Id)
                + " options=" + choices.Length
                + " character=" + source.Identity.ToString(true));
        }

        private static void SendDialoguePromptOnly(
            ICharacter source,
            DialogueSessionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            DialogueNode node = result == null ? null : result.CurrentNode;
            bool sentPromptSegment = false;
            if (node != null && node.PromptSegments != null && node.PromptSegments.Count > 0)
            {
                foreach (DialoguePromptSegment segment in node.PromptSegments)
                {
                    if (segment == null || segment.Text == null)
                    {
                        continue;
                    }

                    KnuBotAppendTextMessageHandler.Default.Send(
                        source,
                        registration.NpcIdentity,
                        FormatDialoguePromptText(source, segment.Text),
                        segment.Unknown2);
                    PaceKnuBotPackets();
                    sentPromptSegment = true;
                }
            }

            if (!sentPromptSegment && node != null && !string.IsNullOrWhiteSpace(node.PromptText))
            {
                KnuBotAppendTextMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    FormatDialoguePromptText(source, node.PromptText));
                PaceKnuBotPackets();
            }
        }

        private static string FormatDialoguePromptText(ICharacter source, string text)
        {
            return FormatDialogueOptionText(source, NormalizeDialoguePromptText(text));
        }

        private static bool CloseRegisteredDialogueSafely(
            ICharacter source,
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            FaceNpcTowardSource(npc, source);
            SendOpenChatWindow(source, registration);
            PaceKnuBotPackets();
            KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
            return true;
        }

        private static void SendOpenChatWindow(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            // Capture 20260719-Rex-Markus-stone: Rex OpenChatWindow Unknown2=0; Marcus=1.
            int unknown2 = 1;
            if (IsRegistration(registration, SubwayTailorRegistration)
                || IsRegistration(registration, RexLarssonRegistration))
            {
                unknown2 = 0;
            }

            KnuBotOpenChatWindowMessageHandler.Default.Send(
                source,
                registration.NpcIdentity,
                unknown2);
        }

        private static void CloseSession(
            ICharacter source,
            string sessionKey,
            ContentDrivenNpcDialogueRegistration registration,
            bool sendClose)
        {
            lock (SyncRoot)
            {
                SessionsByCharacter.Remove(sessionKey);
            }

            ResumeCapturedDialoguePatrol(source, registration, registration.NpcIdentity);

            if (sendClose)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
            }

            LogDialogue(registration, "session ended character=" + source.Identity.ToString(true));
        }

        private static void PauseCapturedDialoguePatrol(
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (IsRegistration(registration, KarliCappelleriRegistration))
            {
                KarliCappelleriQuestRuntime.PausePatrolForDialogue(npc);
            }
            else if (IsRegistration(registration, LeonoraMartyRegistration))
            {
                LeonoraMartyQuestRuntime.PausePatrolForDialogue(npc);
            }
        }

        private static void ResumeCapturedDialoguePatrol(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            Identity npcIdentity)
        {
            if (source?.Playfield == null
                || (!IsRegistration(registration, KarliCappelleriRegistration)
                    && !IsRegistration(registration, LeonoraMartyRegistration)))
            {
                return;
            }

            Identity resolvedIdentity = ResolveLiveNpcIdentity(npcIdentity, registration);
            ICharacter npc = Pool.Instance.GetObject<ICharacter>(
                source.Playfield.Identity,
                resolvedIdentity);
            if (IsRegistration(registration, KarliCappelleriRegistration))
            {
                KarliCappelleriQuestRuntime.ResumePatrolAfterDialogue(npc);
            }
            else
            {
                LeonoraMartyQuestRuntime.ResumePatrolAfterDialogue(npc);
            }
        }

        private static void LogRecordedActions(
            ICharacter source,
            DialogueSessionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            int actionCount = result.RecordedActions == null ? 0 : result.RecordedActions.Count;
            if (actionCount == 0)
            {
                return;
            }

            LogDialogue(
                registration,
                "recorded " + actionCount
                + " no-op action(s) for character=" + source.Identity.ToString(true));
        }

        private static void LogQuestPreviewResult(
            RexQuestPreviewEmissionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void LogB18ECompletionResult(
            RexB18ECompletionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void LogMarcusB18FCompletionResult(
            MarcusB18FCompletionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void LogMarcusB196CompletionResult(
            MarcusB196CompletionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void PaceKnuBotPackets()
        {
            Thread.Sleep(KnuBotPacketPacingMilliseconds);
        }

        private static string NormalizeDialoguePromptText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Content JSON stores capture newlines as literal "\n" sequences.
            return text.Replace("\\n", "\n");
        }

        private static string FormatDialogueOptionText(ICharacter source, string text)
        {
            if (string.IsNullOrEmpty(text)
                || text.IndexOf("{player}", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return text;
            }

            string name = source == null ? null : source.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "stranger";
            }

            return text.Replace("{player}", name);
        }

        private static void LogDialogue(ContentDrivenNpcDialogueRegistration registration, string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, registration.LogPrefix + " " + message);
        }

        private static void LogSkipped(ContentDrivenNpcDialogueRegistration registration, string message)
        {
            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Content-driven NPC dialogue for " + registration.Name + " " + message);
        }

        private static void LogValidation(
            ContentDrivenNpcDialogueRegistration registration,
            string prefix,
            AreteValidationResult validation)
        {
            if (validation == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Content-driven NPC dialogue for " + registration.Name
                    + " " + prefix + ": validation result was missing.");
                return;
            }

            foreach (string error in validation.Errors)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Content-driven NPC dialogue for " + registration.Name
                    + " " + prefix + ": " + error);
            }
        }

        private static ICharacter ResolveCharacter(ICharacter npc, Identity sourceIdentity)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            return Pool.Instance.GetObject<ICharacter>(npc.Playfield.Identity, sourceIdentity);
        }

        private static ContentDrivenNpcDialogueRegistration FindRegistration(ICharacter npc)
        {
            if (npc == null)
            {
                return null;
            }

            ContentDrivenNpcDialogueRegistration runtimeRegistration =
                FindCapturedSubwayVendorRuntimeRegistration(npc)
                ?? FindWindcallerRuntimeRegistration(npc)
                ?? FindGreedyDesertReetRuntimeRegistration(npc);
            if (runtimeRegistration != null)
            {
                return runtimeRegistration;
            }

            ContentDrivenNpcDialogueRegistration byIdentity = FindRegistration(npc.Identity);
            if (byIdentity != null)
            {
                // Always bind to the live pool identity. Capture SCFU ids (e.g. Stan
                // 78E0FC65) are not client-visible; sending them as Knubot Target shows
                // "NoName" and breaks answer routing (capture 20260801-101034 / ZoneEngineLog
                // 2026-08-01 10:22 Target=CanbeAffected:2028010597).
                return BindRegistration(byIdentity, npc.Identity);
            }

            ContentDrivenNpcDialogueRegistration byName = Registrations.FirstOrDefault(
                registration => !IsRuntimeBoundRegistration(registration)
                                && !string.IsNullOrWhiteSpace(registration.ExpectedNpcName)
                                && string.Equals(
                                    npc.Name,
                                    registration.ExpectedNpcName,
                                    StringComparison.OrdinalIgnoreCase));
            return byName == null ? null : BindRegistration(byName, npc.Identity);
        }

        private static ContentDrivenNpcDialogueRegistration FindRegistration(Identity identity)
        {
            WindcallerKarrecNpcRuntimeDefinition runtime;
            if (WindcallerKarrecNpcRuntimeRegistry.TryGet(identity.Instance, out runtime)
                && runtime != null
                && runtime.Content != null)
            {
                ContentDrivenNpcDialogueRegistration bound = Registrations.FirstOrDefault(
                    candidate => IsWindcallerQuestRegistration(candidate)
                                 && candidate.NpcIdentity.Instance
                                 == runtime.Content.SourceNpcInstance);
                if (bound != null)
                {
                    return BindRegistration(bound, runtime.NpcIdentity);
                }
            }

            foreach (ContentDrivenNpcDialogueRegistration registration in Registrations)
            {
                if (!IsRegisteredIdentity(identity, registration))
                {
                    continue;
                }

                int poolInstance;
                if (AORebirth.Core.Playfields.AreteLandingSpawn.TryGetLivingPoolInstance(
                        registration.NpcIdentity.Instance,
                        out poolInstance)
                    && poolInstance != 0)
                {
                    return BindRegistration(
                        registration,
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = poolInstance
                        });
                }

                return BindRegistration(registration, identity);
            }

            return null;
        }

        private static bool IsRegisteredNpc(
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return npc != null
                   && (IsRegisteredIdentity(npc.Identity, registration)
                       || (!IsRuntimeBoundRegistration(registration)
                           && !string.IsNullOrWhiteSpace(registration.ExpectedNpcName)
                           && string.Equals(
                               npc.Name,
                               registration.ExpectedNpcName,
                               StringComparison.OrdinalIgnoreCase)));
        }

        private static ContentDrivenNpcDialogueRegistration FindCapturedSubwayVendorRuntimeRegistration(
            ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            CapturedSubwayVendorRuntimeDefinition runtime;
            if (!CapturedSubwayVendorRuntimeRegistry.TryGet(npc.Identity.Instance, out runtime)
                || runtime == null
                || runtime.Content == null
                || runtime.Content.SourceNpcInstance != CapturedSubwayTailorDialogueContent.SourceNpcInstance
                || !CapturedSubwayVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    npc.Playfield.Identity))
            {
                return null;
            }

            return BindRegistration(SubwayTailorRegistration, runtime.NpcIdentity);
        }

        private static ContentDrivenNpcDialogueRegistration FindWindcallerRuntimeRegistration(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            WindcallerKarrecNpcRuntimeDefinition runtime;
            if (!WindcallerKarrecNpcRuntimeRegistry.TryGet(
                    npc.Playfield.Identity,
                    npc.Identity,
                    out runtime)
                || runtime == null
                || runtime.Content == null)
            {
                return null;
            }

            ContentDrivenNpcDialogueRegistration registration = Registrations.FirstOrDefault(
                candidate => IsWindcallerQuestRegistration(candidate)
                             && candidate.NpcIdentity.Instance == runtime.Content.SourceNpcInstance);
            return registration == null ? null : BindRegistration(registration, runtime.NpcIdentity);
        }

        private static ContentDrivenNpcDialogueRegistration FindGreedyDesertReetRuntimeRegistration(
            ICharacter npc)
        {
            return npc != null && LoreleiOasisMobRuntime.MatchesGreedyDesertReetIdentity(npc)
                       ? BindRegistration(GreedyDesertReetRegistration, npc.Identity)
                       : null;
        }

        private static ContentDrivenNpcDialogueRegistration BindRegistration(
            ContentDrivenNpcDialogueRegistration registration,
            Identity npcIdentity)
        {
            return new ContentDrivenNpcDialogueRegistration
                   {
                       Name = registration.Name,
                       ExpectedNpcName = registration.ExpectedNpcName,
                       NpcIdentity = npcIdentity,
                       NpcIdentityText = registration.NpcIdentityText,
                       PlayfieldId = registration.PlayfieldId,
                       GateEnvironmentVariableName = registration.GateEnvironmentVariableName,
                       LogPrefix = registration.LogPrefix
                   };
        }

        private static bool IsWindcallerQuestRegistration(
            ContentDrivenNpcDialogueRegistration registration)
        {
            return IsRegistration(registration, WindcallerKarrecRegistration)
                   || IsRegistration(registration, AnnoyingDudeRegistration)
                   || IsRegistration(registration, MaddyCardileRegistration);
        }

        private static bool IsRuntimeBoundRegistration(
            ContentDrivenNpcDialogueRegistration registration)
        {
            return IsWindcallerQuestRegistration(registration)
                   || IsRegistration(registration, SubwayTailorRegistration)
                   || IsRegistration(registration, GreedyDesertReetRegistration);
        }

        private static bool IsRegistration(
            ContentDrivenNpcDialogueRegistration registration,
            ContentDrivenNpcDialogueRegistration expected)
        {
            return registration != null
                   && expected != null
                   && (ReferenceEquals(registration, expected)
                       || string.Equals(
                           registration.NpcIdentityText,
                           expected.NpcIdentityText,
                           StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRegisteredIdentity(
            Identity identity,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration == null
                || identity.Type != registration.NpcIdentity.Type)
            {
                return false;
            }

            if (identity.Instance == registration.NpcIdentity.Instance)
            {
                return true;
            }

            // AreteLandingSpawn maps capture SCFU ids → live pool instances.
            int poolInstance;
            return AORebirth.Core.Playfields.AreteLandingSpawn.TryGetLivingPoolInstance(
                       registration.NpcIdentity.Instance,
                       out poolInstance)
                   && identity.Instance == poolInstance;
        }

        private static bool IsExpectedPlayfield(
            ICharacter character,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration == null || !registration.PlayfieldId.HasValue)
            {
                return true;
            }

            return character != null
                   && character.Playfield != null
                   && character.Playfield.Identity.Instance == registration.PlayfieldId.Value;
        }

        private static string CreateSessionKey(
            Identity characterIdentity,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return characterIdentity.Type + ":" + characterIdentity.Instance + "|" + registration.NpcIdentityText;
        }

        private static bool IsRegistrationEnabled(ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            // Null/empty gate = always on (Windcaller Karrec quest NPCs on PF655).
            if (string.IsNullOrWhiteSpace(registration.GateEnvironmentVariableName))
            {
                return true;
            }

            return AreteEnvironmentGate.IsDefaultEnabled(registration.GateEnvironmentVariableName);
        }

        private sealed class ContentDrivenNpcDialogueRegistration
        {
            public string Name { get; set; }

            public string ExpectedNpcName { get; set; }

            public Identity NpcIdentity { get; set; }

            public string NpcIdentityText { get; set; }

            public int? PlayfieldId { get; set; }

            public string GateEnvironmentVariableName { get; set; }

            public string LogPrefix { get; set; }
        }

        private sealed class DialogueSessionRecord
        {
            public ContentDrivenNpcDialogueRegistration Registration { get; set; }

            public DialogueSession Session { get; set; }
        }
    }
}
