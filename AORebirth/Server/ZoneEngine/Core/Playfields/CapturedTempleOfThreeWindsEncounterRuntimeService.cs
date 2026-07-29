namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    internal sealed class CapturedTempleOfThreeWindsEncounterRuntimeService
    {
        internal const int PlayfieldInstance =
            CapturedTempleOfThreeWindsLootDefinitions.PlayfieldInstance;
        internal const int DefenderMonsterData = 38394;
        internal const int YatilaMonsterData = 26151;
        internal const int GulardMonsterData = 26147;
        internal const int ReAnimatorMonsterData = 26155;
        internal const int BetanyMonsterData = 26143;
        internal const int CuratorMonsterData = 22802;
        internal const int NematetMonsterData = 26159;
        internal const int GuardianMonsterData = 22798;
        internal const int GartuaMonsterData = 159085;
        internal const int UkleshMonsterData = 40515;
        internal const int KhalumMonsterData = 95352;
        internal const int AzturMonsterData = 159966;
        internal const int ReanimatedMonsterData = 41690;

        internal const string DefenderProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.DefenderProfileKey;
        internal const string YatilaProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey;
        internal const string GulardProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.GulardProfileKey;
        internal const string ReAnimatorProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorProfileKey;
        internal const string BetanyProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.BetanyProfileKey;
        internal const string CuratorProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey;
        internal const string NematetProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey;
        internal const string GuardianProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey;
        internal const string GartuaProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.GartuaProfileKey;
        internal const string UkleshProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.UkleshProfileKey;
        internal const string KhalumProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.KhalumProfileKey;
        internal const string AzturProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.AzturProfileKey;
        internal const string ReanimatedProfileKey = "totw.647.encounter.re-animator.reanimated-corpse";

        internal const int DefenderPrimaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.DefenderPrimaryNanoId;
        internal const int DefenderSecondaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.DefenderSecondaryNanoId;
        internal const int DefenderUnscheduledNanoId =
            CapturedTempleOfThreeWindsEncounterRules.DefenderUnscheduledNanoId;
        internal const int YatilaPrimaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.YatilaPrimaryNanoId;
        internal const int YatilaSecondaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.YatilaSecondaryNanoId;
        internal const int YatilaTertiaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.YatilaTertiaryNanoId;
        internal const int GulardNanoId =
            CapturedTempleOfThreeWindsEncounterRules.GulardNanoId;
        internal const int ReAnimatorNanoId =
            CapturedTempleOfThreeWindsEncounterRules.ReAnimatorNanoId;
        internal const int ReAnimatorUnscheduledNanoId =
            CapturedTempleOfThreeWindsEncounterRules.YatilaTertiaryNanoId;
        internal const int BetanyNanoId =
            CapturedTempleOfThreeWindsEncounterRules.BetanyNanoId;
        internal const int CuratorNanoId =
            CapturedTempleOfThreeWindsEncounterRules.CuratorNanoId;
        internal const int NematetPrimaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.NematetPrimaryNanoId;
        internal const int NematetSecondaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.NematetSecondaryNanoId;
        internal const int NematetTertiaryNanoId =
            CapturedTempleOfThreeWindsEncounterRules.YatilaTertiaryNanoId;
        internal const int GartuaNanoId =
            CapturedTempleOfThreeWindsEncounterRules.GartuaNanoId;
        internal const int UkleshUnscheduledNanoId =
            CapturedTempleOfThreeWindsEncounterRules.UkleshUnscheduledNanoId;
        internal const int MurialNanoId =
            CapturedTempleOfThreeWindsEncounterRules.MurialUnscheduledNanoId;

        internal const double NamedRespawnAfterNpcDespawnSeconds =
            CapturedTempleOfThreeWindsEncounterRules.NamedRespawnAfterNpcDespawnSeconds;
        internal const double NamedUnlootedCorpseLifetimePolicySeconds = 120.0;
        internal const double GuardianUnlootedCorpseLifetimeSeconds = 1800.0;
        internal const double DefenderLootedCleanupSeconds = 1.277;
        internal const double YatilaLootedCleanupSeconds = 1.640;
        internal const double GulardLootedCleanupSeconds = 1.772;
        internal const double NamedLootedCleanupPolicySeconds = 1.7;
        internal const double NamedLeashPolicyDistance = 40.0;
        internal const float NamedAutomaticAggroPolicyRadius = 7.0f;
        internal const double KhalumSpawnAfterUkleshDeathSeconds = 0.6822027;
        internal const double AzturSpawnAfterKhalumDeathSeconds = 0.211;

        private const double DefenderInitialNanoDelaySeconds = 1.147246;
        private const double DefenderNanoRepeatSeconds = 10.272;
        private const double YatilaInitialNanoDelayPolicySeconds = 5.0;
        private const double YatilaNanoRepeatPolicySeconds = 10.0;
        private const double GulardInitialNanoDelaySeconds = 15.4;
        private const double GulardNanoRepeatPolicySeconds = 60.0;
        private const double ReAnimatorInitialNanoDelaySeconds = 21.718;
        private const double ReAnimatorNanoRepeatSeconds = 10.291;
        private const double BetanyInitialNanoDelaySeconds = 6.444;
        private const double BetanyNanoRepeatSeconds = 10.116;
        private const double CuratorInitialNanoDelaySeconds = 15.4643854;
        private const double CuratorNanoRepeatSeconds = 10.1841983;
        private const double NematetInitialNanoDelaySeconds = 1.1071981;
        private const double NematetNanoRepeatSeconds = 10.1701624;
        private const double GartuaInitialNanoDelaySeconds = 1.3091279;
        private const double GartuaNanoRepeatSeconds = 41.5473945;
        private const double ReanimatedSpawnAfterCastSeconds = 1.578;
        private const double ReanimatedSpawnAfterNpcDespawnSeconds = 1.123;
        private const double ReanimatedResetRefillSeconds = 1.0;

        private static readonly int[] DefenderNanoCycle =
        {
            DefenderPrimaryNanoId,
            DefenderPrimaryNanoId,
            DefenderSecondaryNanoId,
            DefenderPrimaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId,
            DefenderSecondaryNanoId
        };

        private static readonly int[] YatilaNanoCycle =
        {
            YatilaPrimaryNanoId,
            YatilaSecondaryNanoId,
            YatilaPrimaryNanoId,
            YatilaPrimaryNanoId,
            YatilaSecondaryNanoId,
            YatilaTertiaryNanoId
        };

        private static readonly int[] NematetNanoCycle =
        {
            NematetPrimaryNanoId,
            NematetSecondaryNanoId,
            NematetSecondaryNanoId,
            NematetSecondaryNanoId,
            NematetSecondaryNanoId,
            NematetPrimaryNanoId,
            NematetTertiaryNanoId,
            NematetSecondaryNanoId
        };

        private readonly Playfield playfield;
        private readonly PlayfieldDynelRegistry dynelRegistry;
        private readonly Action<ICharacter> activateNpc;
        private readonly NamedEncounterState[] namedEncounters;
        private readonly ReanimatedSlotState[] reanimatedSlots;

        internal CapturedTempleOfThreeWindsEncounterRuntimeService(
            Playfield playfield,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> activateNpc)
        {
            this.playfield = playfield;
            this.dynelRegistry = dynelRegistry;
            this.activateNpc = activateNpc;
            this.namedEncounters = new[]
            {
                new NamedEncounterState(
                    CreateDefenderDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree(),
                    false,
                    DefenderNanoCycle,
                    DefenderInitialNanoDelaySeconds,
                    DefenderNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn),
                new NamedEncounterState(
                    CreateYatilaDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila(),
                    true,
                    YatilaNanoCycle,
                    YatilaInitialNanoDelayPolicySeconds,
                    YatilaNanoRepeatPolicySeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateGulardDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard(),
                    true,
                    new[] { GulardNanoId },
                    GulardInitialNanoDelaySeconds,
                    GulardNanoRepeatPolicySeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateReAnimatorDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.ReAnimator(),
                    false,
                    new[] { ReAnimatorNanoId },
                    ReAnimatorInitialNanoDelaySeconds,
                    ReAnimatorNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateBetanyDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany(),
                    true,
                    new[] { BetanyNanoId },
                    BetanyInitialNanoDelaySeconds,
                    BetanyNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateCuratorDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.TheCurator(),
                    true,
                    new[] { CuratorNanoId },
                    CuratorInitialNanoDelaySeconds,
                    CuratorNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateNematetDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.NematetTheCustodianOfTime(),
                    false,
                    NematetNanoCycle,
                    NematetInitialNanoDelaySeconds,
                    NematetNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn),
                new NamedEncounterState(
                    CreateGuardianDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.GuardianOfTomorrow(),
                    false,
                    new int[0],
                    0.0,
                    0.0,
                    CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn),
                new NamedEncounterState(
                    CreateGartuaDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper(),
                    true,
                    new[] { GartuaNanoId },
                    GartuaInitialNanoDelaySeconds,
                    GartuaNanoRepeatSeconds,
                    CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn,
                    nanoTargetsSelf: true),
                new NamedEncounterState(
                    CreateUkleshDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.UkleshTheFrozen(),
                    true,
                    new int[0],
                    0.0,
                    0.0,
                    CapturedTempleNamedRespawnMode.SuccessorOnly,
                    spawnOnActivation: true),
                new NamedEncounterState(
                    CreateKhalumDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.Khalum(),
                    true,
                    new int[0],
                    0.0,
                    0.0,
                    CapturedTempleNamedRespawnMode.SuccessorOnly,
                    spawnOnActivation: false),
                new NamedEncounterState(
                    CreateAzturDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal(),
                    true,
                    new int[0],
                    0.0,
                    0.0,
                    CapturedTempleNamedRespawnMode.ChainResetAfterNpcDespawn,
                    spawnOnActivation: false)
            };
            this.reanimatedSlots = new[]
            {
                new ReanimatedSlotState(
                    0,
                    CapturedTempleOfThreeWindsCombatCatalog
                        .ReanimatedFirstAnchorCaptureSourceIdentity,
                    65.80717f,
                    16.01125f,
                    292.15747f),
                new ReanimatedSlotState(
                    1,
                    CapturedTempleOfThreeWindsCombatCatalog
                        .ReanimatedSecondAnchorCaptureSourceIdentity,
                    65.74661f,
                    15.53284f,
                    288.377f)
            };
        }

        internal void ActivatePlayfield(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != PlayfieldInstance)
            {
                return;
            }

            foreach (NamedEncounterState state in this.namedEncounters)
            {
                if (!state.SpawnOnActivation
                    || state.Identity.Instance != 0
                    || state.RespawnDueAtUtc.HasValue)
                {
                    continue;
                }

                Character character = this.SpawnCharacter(state.Definition, state.Combat, Identity.None);
                if (character != null)
                {
                    state.Identity = character.Identity;
                    state.Dead = false;
                }
            }

            this.SpawnInitialReanimatedAdds();
        }

        internal void ClearRuntimeState()
        {
            CapturedEncounterRuntimeRegistry.RemoveForPlayfield(this.playfield.Identity.Instance);
            foreach (NamedEncounterState state in this.namedEncounters)
            {
                state.ResetAll();
            }

            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                slot.Reset();
            }
        }

        internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            NamedEncounterState state = this.FindNamed(npc);
            if (state == null
                || !state.AutomaticAggro
                || npc.FightingTarget.Instance != 0
                || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            return this.dynelRegistry
                .FindCharactersInRange(npc, NamedAutomaticAggroPolicyRadius)
                .Where(
                    candidate => candidate != null
                                 && candidate.Controller is PlayerController
                                 && candidate.Stats[StatIds.health].Value > 0)
                .OrderBy(
                    candidate => candidate.Coordinates().coordinate.Distance2D(
                        npc.Coordinates().coordinate))
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
        }

        internal void ProcessDue(
            DateTime utcNow,
            Action<ICharacter, ICharacter> acquireAggro)
        {
            this.ProcessNamedRespawns(utcNow);
            this.ProcessReanimatedSpawns(utcNow, acquireAggro);

            foreach (NamedEncounterState state in this.namedEncounters)
            {
                this.ProcessNamedNano(state, utcNow);
            }
        }

        internal void NotifyCombatStarted(ICharacter npc, ICharacter target, DateTime utcNow)
        {
            NamedEncounterState state = this.FindNamed(npc);
            if (state == null || target == null || state.CombatActive)
            {
                return;
            }

            state.CombatActive = true;
            state.Dead = false;
            state.NextNanoAtUtc = state.NanoCycle.Length == 0
                                      ? (DateTime?)null
                                      : utcNow.AddSeconds(state.InitialNanoDelaySeconds);
        }

        internal ICharacter[] NotifyCombatReset(ICharacter npc)
        {
            NamedEncounterState state = this.FindNamed(npc);
            if (state == null)
            {
                return new ICharacter[0];
            }

            state.ClearCombat();
            if (!string.Equals(state.Definition.ProfileKey, ReAnimatorProfileKey, StringComparison.Ordinal))
            {
                return new ICharacter[0];
            }

            ICharacter[] living = this.DetachLivingReanimatedAdds();
            DateTime refillAtUtc = DateTime.UtcNow.AddSeconds(ReanimatedResetRefillSeconds);
            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                slot.SpawnDueAtUtc = refillAtUtc;
            }

            return living;
        }

        internal ICharacter[] NotifyDeath(ICharacter target, DateTime diedAtUtc)
        {
            NamedEncounterState state = this.FindNamed(target);
            if (state != null)
            {
                state.Dead = true;
                state.ClearCombat();
                this.ScheduleMainRoomSuccessor(state, diedAtUtc);
                if (string.Equals(
                    state.Definition.ProfileKey,
                    ReAnimatorProfileKey,
                    StringComparison.Ordinal))
                {
                    return this.DetachLivingReanimatedAdds();
                }

                return new ICharacter[0];
            }

            ReanimatedSlotState slot = this.FindReanimated(target);
            if (slot != null)
            {
                slot.Dead = true;
            }

            return new ICharacter[0];
        }

        internal void NotifyNpcDespawn(ICharacter target, DateTime utcNow)
        {
            NamedEncounterState state = this.FindNamed(target);
            if (state != null)
            {
                state.Identity = Identity.None;
                state.RespawnDueAtUtc =
                    CapturedTempleOfThreeWindsEncounterRules.ResolveNamedRespawnDueAtUtc(
                    state.RespawnMode,
                    state.RespawnDueAtUtc,
                    utcNow);
                state.ClearCombat();
                if (state.RespawnMode
                    == CapturedTempleNamedRespawnMode.ChainResetAfterNpcDespawn)
                {
                    this.ScheduleMainRoomReset(utcNow);
                }

                return;
            }

            ReanimatedSlotState slot = this.FindReanimated(target);
            if (slot == null)
            {
                return;
            }

            slot.Identity = Identity.None;
            slot.Dead = false;
            if (slot.ReanimationRequested)
            {
                slot.SpawnDueAtUtc = utcNow.AddSeconds(ReanimatedSpawnAfterNpcDespawnSeconds);
            }
        }

        internal bool IsCapturedNanoCastInProgress(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            return this.namedEncounters.Any(
                state => state.Identity == character.Identity && state.PendingNano != null);
        }

        internal static CapturedEncounterRuntimeDefinition CreateDefenderDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                DefenderProfileKey,
                "totw.647.boss.defender-of-the-three.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.DefenderEncounterKey,
                "Defender of the Three",
                DefenderMonsterData,
                true,
                false,
                42,
                7091,
                104,
                145,
                144,
                0,
                3,
                173.1958f,
                31.9949989f,
                266.324951f,
                0.0f,
                0.0569359064f,
                0.0f,
                0.99837786f,
                1227u,
                unchecked((int)0x022A4A43),
                0,
                HexToBytes("00000000000000000000000003010001000100010001000000020000"),
                0,
                38265,
                NamedUnlootedCorpseLifetimePolicySeconds,
                DefenderLootedCleanupSeconds,
                "20260721-035526/040324 exact SCFU, fight, death, corpse and chase; "
                + "20260721-040249/040324 loot; first NPC despawn to replacement SCFU "
                + "is 600.193 seconds; Mike identified the corpse as Temporary: 2m",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 6,
                sex: 0,
                race: 1,
                headMesh: 0,
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateYatilaDefinition()
        {
            return HumanDefinition(
                YatilaProfileKey,
                "totw.647.named.windcaller-yatila.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.YatilaEncounterKey,
                "Windcaller Yatila",
                YatilaMonsterData,
                56,
                13863,
                106,
                214,
                213,
                95.31601f,
                13.0112486f,
                258.637878f,
                0f,
                0.9821756f,
                0f,
                0.187965825f,
                1643u,
                "00000000000000008000000003010001000100010001000000020000",
                5921,
                YatilaLootedCleanupSeconds,
                40171,
                161710,
                161715,
                161705,
                161725,
                20040u,
                161720,
                204738u,
                2,
                "20260721-032547 exact SCFU; 041439 auto aggro, multi-stream combat, nanos, "
                + "two approximately 40-unit leash returns, exact corpse and loot");
        }

        internal static CapturedEncounterRuntimeDefinition CreateGulardDefinition()
        {
            return HumanDefinition(
                GulardProfileKey,
                "totw.647.named.reverend-gulard.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.GulardEncounterKey,
                "Reverend Gulard",
                GulardMonsterData,
                38,
                3052,
                103,
                171,
                170,
                60.4321442f,
                16.0409985f,
                291.730774f,
                0f,
                0.9991284f,
                0f,
                0.0417430773f,
                1643u,
                "00000000000000008000000003010001000100010001000000020000",
                17905,
                GulardLootedCleanupSeconds,
                40172,
                161709,
                161714,
                161704,
                161724,
                20040u,
                161719,
                204738u,
                2,
                "20260721-042139 exact SCFU, proactive attack, nano 205584, two approximately "
                + "40-unit leash returns, two corpses and two identical loot snapshots");
        }

        internal static CapturedEncounterRuntimeDefinition CreateReAnimatorDefinition()
        {
            return HumanDefinition(
                ReAnimatorProfileKey,
                "totw.647.boss.the-re-animator.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorEncounterKey,
                "The Re-Animator",
                ReAnimatorMonsterData,
                60,
                12441,
                107,
                231,
                230,
                60.20344f,
                16.0112476f,
                295.703949f,
                0f,
                -0.03815717f,
                0f,
                0.99927175f,
                1899u,
                "80000000000000000000000003010001000100010001000000020000",
                23370,
                NamedLootedCleanupPolicySeconds,
                40138,
                161708,
                161713,
                161703,
                161723,
                20023u,
                161718,
                204738u,
                3,
                "20260721-043204 exact level-60 SCFU, retaliation, 72-point combat, 205604 "
                + "reanimation sequence, corpse and loot; Mike identified the corpse as Temporary: 2m");
        }

        internal static CapturedEncounterRuntimeDefinition CreateBetanyDefinition()
        {
            return HumanDefinition(
                BetanyProfileKey,
                "totw.647.named.acolyte-betany.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.BetanyEncounterKey,
                "Acolyte Betany",
                BetanyMonsterData,
                32,
                1734,
                102,
                144,
                143,
                46.1443329f,
                12.01125f,
                259.741333f,
                0f,
                0.9998477f,
                0f,
                0.0174523834f,
                1899u,
                "00000000000000008000000003010001000100010001000000020000",
                23368,
                NamedLootedCleanupPolicySeconds,
                40137,
                161712,
                161717,
                161707,
                161727,
                20023u,
                161722,
                7835u,
                3,
                "20260721-041439 exact SCFU; 044256 proactive attack, ranged AttackInfo, "
                + "three nano 205383 casts, greater-than-35-unit leash, corpse and loot");
        }

        internal static CapturedEncounterRuntimeDefinition CreateCuratorDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                CuratorProfileKey,
                "totw.647.boss.the-curator.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.CuratorEncounterKey,
                "The Curator",
                CuratorMonsterData,
                true,
                false,
                52,
                9740,
                106,
                198,
                205,
                0,
                3,
                121.159302f,
                34.0749969f,
                352.137634f,
                0f,
                -0.0102066733f,
                0f,
                0.9999479f,
                1227u,
                unchecked((int)0x022A4A43),
                0,
                HexToBytes("80000000000000000000000003010001000100010001000000020000"),
                0,
                21499,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260721-052115 exact SCFU appearance; 225404 exact level-52/9740-health "
                + "visible generation, approximately four-unit proactive aggro, 33/57 combat, "
                + "nano 205565 sequence, death, corpse, and exact loot snapshot",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 6,
                sex: 0,
                race: 1,
                headMesh: 0,
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateNematetDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                NematetProfileKey,
                "totw.647.boss.nematet-the-custodian-of-time.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.NematetEncounterKey,
                "Nematet the Custodian of Time",
                NematetMonsterData,
                true,
                false,
                66,
                25318,
                107,
                255,
                263,
                0,
                3,
                171.324936f,
                36.0112457f,
                340.074097f,
                0f,
                -0.7193397f,
                0f,
                0.694658458f,
                1643u,
                unchecked((int)0x022A6ACB),
                0,
                HexToBytes("80000000000000008000000003010001000100010001000000020000"),
                0,
                17909,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260721-052115 exact SCFU appearance; 225743 exact level-66/25318-health "
                + "visible generation, player-initiated fight, three captured weapon streams, "
                + "nanos 205395/205563/205592, chase, death, corpse, and exact loot snapshot",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 3,
                sex: 2,
                race: 1,
                headMesh: 40173,
                textures: new[]
                {
                    new CapturedSubwayTextureDefinition(0, 0, 0),
                    new CapturedSubwayTextureDefinition(1, 161708, 0),
                    new CapturedSubwayTextureDefinition(2, 161713, 0),
                    new CapturedSubwayTextureDefinition(3, 161703, 0),
                    new CapturedSubwayTextureDefinition(4, 161723, 0)
                },
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(0, 20040u, 161718, 2),
                    new CapturedSubwayMeshDefinition(0, 40173u, 0, 4)
                },
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateGuardianDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                GuardianProfileKey,
                "totw.1931.boss.guardian-of-tomorrow.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.GuardianEncounterKey,
                "Guardian of Tomorrow",
                GuardianMonsterData,
                true,
                false,
                68,
                26500,
                108,
                264,
                263,
                0,
                3,
                274.823364f,
                13.01125f,
                388.980774f,
                0f,
                0.379739523f,
                0f,
                -0.925093353f,
                1227u,
                unchecked((int)0x022A6A43),
                0,
                HexToBytes("80000000000000000000000003010001000100010001000000020000"),
                0,
                21082,
                GuardianUnlootedCorpseLifetimeSeconds,
                NamedLootedCleanupPolicySeconds,
                "20260721-230426 exact level-68/26500-health SCFU, player-initiated dual-stream "
                + "fight, death, corpse, and exact loot; Mike measured a ten-minute respawn and "
                + "a 30-minute unlooted corpse lifetime",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 6,
                sex: 0,
                race: 1,
                headMesh: 0,
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateGartuaDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                GartuaProfileKey,
                "totw.1931.boss.gartua-the-doorkeeper.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.GartuaEncounterKey,
                "Gartua the Doorkeeper",
                GartuaMonsterData,
                true,
                false,
                65,
                14130,
                107,
                229,
                228,
                0,
                3,
                274.99f,
                14.2112513f,
                426.642548f,
                0f,
                1f,
                0f,
                -0.00000004371139f,
                1419u,
                unchecked((int)0x020A4ACB),
                0,
                HexToBytes("80000000000000008000000002010001000100010001000000020000"),
                0,
                23366,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260721-230426 exact SCFU; 20260721-230824 proactive attack, 76..114 combat, "
                + "self-cast nano 205590, death, corpse, and exact loot; Mike measured a "
                + "ten-minute respawn and a 120-second unlooted corpse lifetime",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 4,
                sex: 1,
                race: 1,
                headMesh: 40105,
                textures: new[]
                {
                    new CapturedSubwayTextureDefinition(0, 0, 0),
                    new CapturedSubwayTextureDefinition(1, 21827, 0),
                    new CapturedSubwayTextureDefinition(2, 0, 0),
                    new CapturedSubwayTextureDefinition(3, 21822, 0),
                    new CapturedSubwayTextureDefinition(4, 19698, 0)
                },
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(0, 40105u, 0, 4),
                    new CapturedSubwayMeshDefinition(1, 96336u, 0, 2)
                },
                waypoints: new[]
                {
                    new CapturedSubwayWaypointDefinition(275.379242f, 13.0112476f, 417.979675f),
                    new CapturedSubwayWaypointDefinition(274.75f, 14.0012474f, 408.15f),
                    new CapturedSubwayWaypointDefinition(271.116425f, 14.0112476f, 409.686f)
                },
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateUkleshDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                UkleshProfileKey,
                "totw.1931.boss.uklesh-the-frozen.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.UkleshEncounterKey,
                "Uklesh the Frozen",
                UkleshMonsterData,
                true,
                false,
                73,
                21039,
                108,
                283,
                283,
                0,
                3,
                274.950745f,
                16.611248f,
                531.1443f,
                0f,
                0.9149572f,
                0f,
                -0.4035505f,
                1227u,
                unchecked((int)0x022A6A43),
                0,
                HexToBytes("80000000000000008000000003010001000100010001000000020000"),
                0,
                40495,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260722-045421 exact SCFU; 20260722-045835 complete fight, exact "
                + "two-stream combat, death, 0.6822027-second Khalum succession, corpse, "
                + "625 credits, and exact loot snapshot",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 6,
                sex: 0,
                race: 1,
                headMesh: 0,
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateKhalumDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                KhalumProfileKey,
                "totw.1931.boss.khalum.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.KhalumEncounterKey,
                "Khalum",
                KhalumMonsterData,
                true,
                false,
                73,
                25247,
                108,
                283,
                283,
                0,
                3,
                281.30542f,
                16.611248f,
                529.3965f,
                0f,
                0.9760028f,
                0f,
                0.217757955f,
                1227u,
                unchecked((int)0x022A6A43),
                0,
                HexToBytes("00000000000000008000000003010001000100010001000000020000"),
                0,
                95294,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260722-045835 exact post-Uklesh SCFU, two-stream combat, death, "
                + "0.211-second Aztur succession, corpse, 625 credits, and exact loot snapshot",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 6,
                sex: 0,
                race: 1,
                headMesh: 0,
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        internal static CapturedEncounterRuntimeDefinition CreateAzturDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                AzturProfileKey,
                "totw.1931.boss.aztur-the-immortal.spawn",
                CapturedTempleOfThreeWindsLootDefinitions.AzturEncounterKey,
                "Aztur the Immortal",
                AzturMonsterData,
                true,
                false,
                74,
                38630,
                163,
                522,
                522,
                0,
                3,
                280.845642f,
                16.611248f,
                518.7123f,
                0f,
                0.9622065f,
                0f,
                0.272320867f,
                1419u,
                unchecked((int)0x020A6A43),
                0,
                HexToBytes("00000000000000008000000003010001000100010001000000020000"),
                0,
                159384,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260722-045835 exact post-Khalum SCFU, complete three-stream fight, "
                + "ordered mutable SAW state, death, corpse, 3184 credits, and exact loot",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 4,
                sex: 1,
                race: 1,
                headMesh: 0,
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(1, 160016u, 0, 2)
                },
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        private static CapturedEncounterRuntimeDefinition HumanDefinition(
            string profileKey,
            string spawnKey,
            string encounterKey,
            string name,
            int monsterData,
            int level,
            int health,
            int scale,
            int runSpeed,
            int capturedRunSpeed,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            uint appearance,
            string unknown1,
            int corpseMesh,
            double lootedCleanup,
            int headMesh,
            int texture1,
            int texture2,
            int texture3,
            int texture4,
            uint bodyMesh,
            int bodyOverrideTexture,
            uint weaponMesh,
            int sex,
            string evidence)
        {
            return new CapturedEncounterRuntimeDefinition(
                profileKey,
                spawnKey,
                encounterKey,
                name,
                monsterData,
                true,
                false,
                level,
                health,
                scale,
                runSpeed,
                capturedRunSpeed,
                0,
                3,
                x,
                y,
                z,
                headingX,
                headingY,
                headingZ,
                headingW,
                appearance,
                unchecked((int)0x020A4ACB),
                0,
                HexToBytes(unknown1),
                0,
                corpseMesh,
                NamedUnlootedCorpseLifetimePolicySeconds,
                lootedCleanup,
                evidence,
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 3,
                sex: sex,
                race: 1,
                headMesh: headMesh,
                textures: new[]
                {
                    new CapturedSubwayTextureDefinition(0, 0, 0),
                    new CapturedSubwayTextureDefinition(1, texture1, 0),
                    new CapturedSubwayTextureDefinition(2, texture2, 0),
                    new CapturedSubwayTextureDefinition(3, texture3, 0),
                    new CapturedSubwayTextureDefinition(4, texture4, 0)
                },
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(0, bodyMesh, bodyOverrideTexture, 2),
                    new CapturedSubwayMeshDefinition(0, (uint)headMesh, 0, 4),
                    new CapturedSubwayMeshDefinition(1, weaponMesh, 0, 2)
                },
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        private void ProcessNamedRespawns(DateTime utcNow)
        {
            foreach (NamedEncounterState state in this.namedEncounters)
            {
                if (!state.RespawnDueAtUtc.HasValue
                    || state.RespawnDueAtUtc.Value > utcNow
                    || state.Identity.Instance != 0)
                {
                    continue;
                }

                Character character = this.SpawnCharacter(state.Definition, state.Combat, Identity.None);
                if (character == null)
                {
                    continue;
                }

                state.Identity = character.Identity;
                state.RespawnDueAtUtc = null;
                state.Dead = false;
                state.ClearCombat();
                if (string.Equals(
                    state.Definition.ProfileKey,
                    ReAnimatorProfileKey,
                    StringComparison.Ordinal))
                {
                    this.SpawnInitialReanimatedAdds();
                }
            }
        }

        private void ScheduleMainRoomSuccessor(
            NamedEncounterState state,
            DateTime diedAtUtc)
        {
            string successorProfileKey;
            double delaySeconds;
            if (!TryGetMainRoomSuccessor(
                    state.Definition.ProfileKey,
                    out successorProfileKey,
                    out delaySeconds))
            {
                return;
            }

            NamedEncounterState successor = this.FindNamed(successorProfileKey);
            if (successor == null
                || successor.Identity.Instance != 0
                || successor.RespawnDueAtUtc.HasValue)
            {
                return;
            }

            successor.RespawnDueAtUtc = diedAtUtc.AddSeconds(delaySeconds);
        }

        internal static bool TryGetMainRoomSuccessor(
            string profileKey,
            out string successorProfileKey,
            out double delaySeconds)
        {
            if (string.Equals(profileKey, UkleshProfileKey, StringComparison.Ordinal))
            {
                successorProfileKey = KhalumProfileKey;
                delaySeconds = KhalumSpawnAfterUkleshDeathSeconds;
                return true;
            }

            if (string.Equals(profileKey, KhalumProfileKey, StringComparison.Ordinal))
            {
                successorProfileKey = AzturProfileKey;
                delaySeconds = AzturSpawnAfterKhalumDeathSeconds;
                return true;
            }

            successorProfileKey = string.Empty;
            delaySeconds = 0.0;
            return false;
        }

        internal static bool TryResolveNamedRespawnDelay(
            CapturedTempleNamedRespawnMode mode,
            out double delaySeconds)
        {
            return CapturedTempleOfThreeWindsEncounterRules.TryResolveNamedRespawnDelay(
                mode,
                out delaySeconds);
        }

        internal static bool TryGetCapturedNanoEffectOwnership(
            int nanoId,
            out CapturedTempleNanoEffectOwnership ownership)
        {
            return CapturedTempleOfThreeWindsEncounterRules
                .TryGetCapturedNanoEffectOwnership(nanoId, out ownership);
        }

        private void ScheduleMainRoomReset(DateTime resetAtUtc)
        {
            NamedEncounterState uklesh = this.FindNamed(UkleshProfileKey);
            NamedEncounterState khalum = this.FindNamed(KhalumProfileKey);
            NamedEncounterState aztur = this.FindNamed(AzturProfileKey);
            if (uklesh == null
                || khalum == null
                || aztur == null)
            {
                return;
            }

            DateTime resetDueAtUtc;
            if (!CapturedTempleOfThreeWindsEncounterRules.TryResolveMainRoomResetDue(
                    resetAtUtc,
                    CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(
                        uklesh.Identity.Instance,
                        uklesh.Dead),
                    CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(
                        khalum.Identity.Instance,
                        khalum.Dead),
                    CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(
                        aztur.Identity.Instance,
                        aztur.Dead),
                    uklesh.RespawnDueAtUtc.HasValue,
                    khalum.RespawnDueAtUtc.HasValue,
                    aztur.RespawnDueAtUtc.HasValue,
                    out resetDueAtUtc))
            {
                return;
            }

            uklesh.ClearCombat();
            khalum.ClearCombat();
            aztur.ClearCombat();
            uklesh.RespawnDueAtUtc = resetDueAtUtc;
        }

        private void ProcessNamedNano(NamedEncounterState state, DateTime utcNow)
        {
            if (!state.CombatActive || state.Dead || state.Identity.Instance == 0)
            {
                return;
            }

            ICharacter actor = this.playfield.FindByIdentity<ICharacter>(state.Identity);
            if (actor == null || actor.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            if (state.PendingNano != null)
            {
                if (state.PendingNano.FinishAtUtc <= utcNow)
                {
                    this.FinishNamedNano(state, actor, state.PendingNano);
                }

                return;
            }

            if (!state.NextNanoAtUtc.HasValue || state.NextNanoAtUtc.Value > utcNow)
            {
                return;
            }

            ICharacter target = state.NanoTargetsSelf
                                    ? actor
                                    : this.playfield.FindByIdentity<ICharacter>(actor.FightingTarget);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            int nanoId = state.NanoCycle[state.NanoIndex % state.NanoCycle.Length];
            state.NanoIndex++;
            state.PendingNano = new PendingTempleNano(
                nanoId,
                target.Identity,
                utcNow.AddSeconds(NanoCastSeconds(state, nanoId)));
            state.NextNanoAtUtc = utcNow.AddSeconds(state.NanoRepeatSeconds);
            CastNanoSpellMessageHandler.Default.SendCapturedNpcCast(actor, nanoId, target.Identity);
        }

        private void FinishNamedNano(
            NamedEncounterState state,
            ICharacter actor,
            PendingTempleNano pending)
        {
            state.PendingNano = null;
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                actor,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                pending.NanoId);

            CapturedTempleNanoEffectOwnership ownership;
            if (TryGetCapturedNanoEffectOwnership(pending.NanoId, out ownership)
                && ownership == CapturedTempleNanoEffectOwnership.ReanimatedAddLifecycle)
            {
                this.RequestNextReanimation(pending.FinishAtUtc);
            }

            // Captures prove the cast IDs and finish timing. Gulard's nearby
            // health changes, the reported 23-point poison tick, and the
            // Curator/Nematet nano effects do not have safe packet ownership,
            // so no unproven stat effect is applied.
        }

        private void RequestNextReanimation(DateTime finishedAtUtc)
        {
            ReanimatedSlotState slot = this.reanimatedSlots
                .Where(value => value.Dead || value.Identity.Instance == 0)
                .OrderBy(value => value.Index)
                .FirstOrDefault(value => !value.ReanimationRequested);
            if (slot == null)
            {
                return;
            }

            slot.ReanimationRequested = true;
            if (slot.Identity.Instance == 0)
            {
                slot.SpawnDueAtUtc = finishedAtUtc.AddSeconds(ReanimatedSpawnAfterCastSeconds);
            }
        }

        private void ProcessReanimatedSpawns(
            DateTime utcNow,
            Action<ICharacter, ICharacter> acquireAggro)
        {
            NamedEncounterState bossState = this.FindNamed(ReAnimatorProfileKey);
            if (bossState == null || bossState.Identity.Instance == 0 || bossState.Dead)
            {
                return;
            }

            ICharacter boss = this.playfield.FindByIdentity<ICharacter>(bossState.Identity);
            if (boss == null || boss.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                if (slot.Identity.Instance != 0
                    || !slot.SpawnDueAtUtc.HasValue
                    || slot.SpawnDueAtUtc.Value > utcNow)
                {
                    continue;
                }

                Character add = this.SpawnReanimated(slot, boss.Identity);
                slot.SpawnDueAtUtc = null;
                slot.ReanimationRequested = false;
                slot.Dead = false;
                if (add == null)
                {
                    continue;
                }

                slot.Identity = add.Identity;
                ICharacter target = this.playfield.FindByIdentity<ICharacter>(boss.FightingTarget);
                if (target != null && target.Stats[StatIds.health].Value > 0)
                {
                    acquireAggro(target, add);
                }
            }
        }

        private void SpawnInitialReanimatedAdds()
        {
            NamedEncounterState bossState = this.FindNamed(ReAnimatorProfileKey);
            if (bossState == null || bossState.Identity.Instance == 0)
            {
                return;
            }

            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                if (slot.Identity.Instance != 0)
                {
                    continue;
                }

                Character add = this.SpawnReanimated(slot, bossState.Identity);
                if (add != null)
                {
                    slot.Identity = add.Identity;
                    slot.Dead = false;
                    slot.ReanimationRequested = false;
                    slot.SpawnDueAtUtc = null;
                }
            }
        }

        private Character SpawnReanimated(ReanimatedSlotState slot, Identity bossIdentity)
        {
            CapturedEncounterRuntimeDefinition definition =
                CreateReanimatedDefinition(slot);
            return this.SpawnCharacter(
                definition,
                CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse(
                    slot.CaptureSourceIdentity),
                bossIdentity);
        }

        private static CapturedEncounterRuntimeDefinition CreateReanimatedDefinition(
            ReanimatedSlotState slot)
        {
            return new CapturedEncounterRuntimeDefinition(
                ReanimatedProfileKey,
                "totw.647.encounter.re-animator.reanimated-corpse."
                + slot.Index.ToString(CultureInfo.InvariantCulture),
                CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorEncounterKey,
                "Reanimated Corpse",
                ReanimatedMonsterData,
                false,
                true,
                18,
                247,
                98,
                93,
                92,
                0,
                3,
                slot.X,
                slot.Y,
                slot.Z,
                0f,
                0f,
                0f,
                1f,
                1067u,
                unchecked((int)0x020A4A43),
                0,
                HexToBytes("80000000000000000000000003010001000100010001000000020000"),
                0,
                41664,
                NamedUnlootedCorpseLifetimePolicySeconds,
                NamedLootedCleanupPolicySeconds,
                "20260721-043204: two level-18 Reanimated Corpse room anchors; dead add NPCs "
                + "were replaced after The Re-Animator finished nano 205604, and living "
                + "replacements disappeared with the boss",
                npcFamily: 136,
                npcLosHeight: 0,
                fatness: 1,
                breed: 1,
                sex: 0,
                race: 1,
                headMesh: 0,
                textures: new[]
                {
                    new CapturedSubwayTextureDefinition(0, 0, 0),
                    new CapturedSubwayTextureDefinition(1, 0, 0),
                    new CapturedSubwayTextureDefinition(2, 0, 0),
                    new CapturedSubwayTextureDefinition(3, 0, 0),
                    new CapturedSubwayTextureDefinition(4, 0, 0)
                },
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(1, 96330u, 0, 2)
                },
                maximumNpcLeashDistanceFromHome: NamedLeashPolicyDistance);
        }

        private ICharacter[] DetachLivingReanimatedAdds()
        {
            var living = new List<ICharacter>();
            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                ICharacter add = slot.Identity.Instance == 0
                                     ? null
                                     : this.playfield.FindByIdentity<ICharacter>(slot.Identity);
                if (add != null && add.Stats[StatIds.health].Value > 0)
                {
                    add.Stats[StatIds.petmaster].Value = 0;
                    add.SendChangedStats();
                    living.Add(add);
                }

                slot.Reset();
            }

            return living.ToArray();
        }

        private Character SpawnCharacter(
            CapturedEncounterRuntimeDefinition definition,
            CapturedEnemyCombatContract combat,
            Identity ownerIdentity)
        {
            int instance = Pool.Instance.GetFreeInstance<Character>(
                1000000,
                IdentityType.CanbeAffected);
            var identity = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = instance
            };
            var controller = new NPCController();
            var character = new Character(this.playfield.Identity, identity, controller);
            character.Read();
            controller.Character = character;
            character.Playfield = this.playfield;
            character.Name = definition.DisplayName;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Coordinates(
                new Coordinate
                {
                    x = definition.X,
                    y = definition.Y,
                    z = definition.Z
                });
            character.RawHeading = new AORebirth.Core.Vector.Quaternion(
                definition.HeadingX,
                definition.HeadingY,
                definition.HeadingZ,
                definition.HeadingW);

            SetStat(character, StatIds.side, definition.Side);
            SetStat(character, StatIds.fatness, definition.Fatness);
            SetStat(character, StatIds.breed, definition.Breed);
            SetStat(character, StatIds.sex, definition.Sex);
            SetStat(character, StatIds.race, definition.Race);
            SetStat(character, StatIds.flags, unchecked((int)0x10081201));
            SetStat(character, StatIds.accountflags, 0);
            SetStat(character, StatIds.expansion, 0);
            SetStat(character, StatIds.npcfamily, definition.NpcFamily);
            SetStat(character, StatIds.losheight, definition.NpcLosHeight);
            SetStat(character, StatIds.monsterdata, definition.MonsterData);
            SetStat(character, StatIds.monsterscale, definition.MonsterScale);
            SetStat(character, StatIds.headmesh, definition.HeadMesh);
            SetStat(character, StatIds.visualflags, 31);
            SetStat(character, StatIds.currentmovementmode, (int)MoveModes.Run);
            SetStat(character, StatIds.prevmovementmode, (int)MoveModes.Run);
            SetStat(character, StatIds.runspeed, definition.RunSpeed);
            SetStat(character, StatIds.profession, 1);
            SetStat(character, StatIds.titlelevel, 1);
            SetStat(character, StatIds.level, definition.Level);
            SetStat(character, StatIds.life, definition.Health);
            SetStat(character, StatIds.health, definition.Health);
            SetStat(character, StatIds.computerliteracy, 6);
            if (ownerIdentity.Instance != 0)
            {
                SetStat(character, StatIds.petmaster, ownerIdentity.Instance);
            }

            character.Textures.Clear();
            foreach (CapturedSubwayTextureDefinition texture in definition.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            foreach (CapturedSubwayMeshDefinition mesh in definition.Meshes)
            {
                character.MeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
                character.SocialMeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
            }

            character.Waypoints.Clear();
            foreach (CapturedSubwayWaypointDefinition waypoint in definition.Waypoints)
            {
                character.AddWaypoint(
                    new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                    false);
            }

            string combatFailure;
            if (!CapturedEnemyCombatRuntime.Prepare(
                    character,
                    controller,
                    combat,
                    out combatFailure))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Captured Temple encounter combat refused actor=" + definition.ProfileKey
                    + " reason=" + combatFailure);
                Pool.Instance.RemoveObject(character);
                return null;
            }

            character.DoNotDoTimers = false;
            CapturedEncounterRuntimeRegistry.Register(
                character.Identity.Instance,
                this.playfield.Identity.Instance,
                definition);
            this.activateNpc(character);
            this.playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Temple encounter actor spawned profile={0} identity={1} position=({2},{3},{4}) evidence={5}",
                    definition.ProfileKey,
                    character.Identity,
                    definition.X,
                    definition.Y,
                    definition.Z,
                    definition.Evidence));
            return character;
        }

        private NamedEncounterState FindNamed(ICharacter character)
        {
            return character == null
                       ? null
                       : this.namedEncounters.FirstOrDefault(
                           state => state.Identity == character.Identity);
        }

        private NamedEncounterState FindNamed(string profileKey)
        {
            return this.namedEncounters.FirstOrDefault(
                state => string.Equals(
                    state.Definition.ProfileKey,
                    profileKey,
                    StringComparison.Ordinal));
        }

        private ReanimatedSlotState FindReanimated(ICharacter character)
        {
            return character == null
                       ? null
                       : this.reanimatedSlots.FirstOrDefault(
                           slot => slot.Identity == character.Identity);
        }

        private static double NanoCastSeconds(NamedEncounterState state, int nanoId)
        {
            if (state != null
                && string.Equals(
                    state.Definition.ProfileKey,
                    NematetProfileKey,
                    StringComparison.Ordinal))
            {
                switch (nanoId)
                {
                    case NematetPrimaryNanoId: return 5.2211694;
                    case NematetSecondaryNanoId: return 5.6058988;
                    case NematetTertiaryNanoId: return 3.6813144;
                }
            }

            switch (nanoId)
            {
                case DefenderPrimaryNanoId: return 5.28395;
                case DefenderSecondaryNanoId: return 6.1904;
                case YatilaPrimaryNanoId: return 5.96;
                case YatilaSecondaryNanoId: return 4.945;
                case YatilaTertiaryNanoId: return 5.0;
                case GulardNanoId: return 4.562;
                case ReAnimatorNanoId: return 7.04;
                case BetanyNanoId: return 5.337;
                case CuratorNanoId: return 6.2402402;
                case GartuaNanoId: return 0.960617;
                default: throw new InvalidOperationException("Unknown Temple nano id.");
            }
        }

        private static byte[] HexToBytes(string value)
        {
            byte[] bytes = new byte[value.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(
                    value.Substring(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering(
                (int)stat,
                (uint)Math.Max(0, value));
        }

        private sealed class NamedEncounterState
        {
            internal NamedEncounterState(
                CapturedEncounterRuntimeDefinition definition,
                CapturedEnemyCombatContract combat,
                bool automaticAggro,
                int[] nanoCycle,
                double initialNanoDelaySeconds,
                double nanoRepeatSeconds,
                CapturedTempleNamedRespawnMode respawnMode,
                bool nanoTargetsSelf = false,
                bool spawnOnActivation = true)
            {
                this.Definition = definition;
                this.Combat = combat;
                this.AutomaticAggro = automaticAggro;
                this.NanoCycle = nanoCycle;
                this.InitialNanoDelaySeconds = initialNanoDelaySeconds;
                this.NanoRepeatSeconds = nanoRepeatSeconds;
                this.RespawnMode = respawnMode;
                this.NanoTargetsSelf = nanoTargetsSelf;
                this.SpawnOnActivation = spawnOnActivation;
                this.Identity = Identity.None;
            }

            internal CapturedEncounterRuntimeDefinition Definition { get; private set; }
            internal CapturedEnemyCombatContract Combat { get; private set; }
            internal bool AutomaticAggro { get; private set; }
            internal int[] NanoCycle { get; private set; }
            internal double InitialNanoDelaySeconds { get; private set; }
            internal double NanoRepeatSeconds { get; private set; }
            internal CapturedTempleNamedRespawnMode RespawnMode { get; private set; }
            internal bool NanoTargetsSelf { get; private set; }
            internal bool SpawnOnActivation { get; private set; }
            internal Identity Identity { get; set; }
            internal DateTime? RespawnDueAtUtc { get; set; }
            internal DateTime? NextNanoAtUtc { get; set; }
            internal PendingTempleNano PendingNano { get; set; }
            internal bool CombatActive { get; set; }
            internal bool Dead { get; set; }
            internal int NanoIndex { get; set; }

            internal void ClearCombat()
            {
                this.CombatActive = false;
                this.NextNanoAtUtc = null;
                this.PendingNano = null;
                this.NanoIndex = 0;
            }

            internal void ResetAll()
            {
                this.Identity = Identity.None;
                this.RespawnDueAtUtc = null;
                this.Dead = false;
                this.ClearCombat();
            }
        }

        private sealed class PendingTempleNano
        {
            internal PendingTempleNano(
                int nanoId,
                Identity targetIdentity,
                DateTime finishAtUtc)
            {
                this.NanoId = nanoId;
                this.TargetIdentity = targetIdentity;
                this.FinishAtUtc = finishAtUtc;
            }

            internal int NanoId { get; private set; }
            internal Identity TargetIdentity { get; private set; }
            internal DateTime FinishAtUtc { get; private set; }
        }

        private sealed class ReanimatedSlotState
        {
            internal ReanimatedSlotState(
                int index,
                int captureSourceIdentity,
                float x,
                float y,
                float z)
            {
                this.Index = index;
                this.CaptureSourceIdentity = captureSourceIdentity;
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.Identity = Identity.None;
            }

            internal int Index { get; private set; }
            internal int CaptureSourceIdentity { get; private set; }
            internal float X { get; private set; }
            internal float Y { get; private set; }
            internal float Z { get; private set; }
            internal Identity Identity { get; set; }
            internal bool Dead { get; set; }
            internal bool ReanimationRequested { get; set; }
            internal DateTime? SpawnDueAtUtc { get; set; }

            internal void Reset()
            {
                this.Identity = Identity.None;
                this.Dead = false;
                this.ReanimationRequested = false;
                this.SpawnDueAtUtc = null;
            }
        }
    }
}
