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
        internal const string ReanimatedProfileKey = "totw.647.encounter.re-animator.reanimated-corpse";

        internal const int DefenderPrimaryNanoId = 205389;
        internal const int DefenderSecondaryNanoId = 205561;
        internal const int YatilaPrimaryNanoId = 205600;
        internal const int YatilaSecondaryNanoId = 205594;
        internal const int YatilaTertiaryNanoId = 205592;
        internal const int GulardNanoId = 205584;
        internal const int ReAnimatorNanoId = 205604;
        internal const int BetanyNanoId = 205383;

        internal const double NamedRespawnAfterNpcDespawnSeconds = 600.0;
        internal const double NamedUnlootedCorpseLifetimePolicySeconds = 120.0;
        internal const double DefenderLootedCleanupSeconds = 1.277;
        internal const double YatilaLootedCleanupSeconds = 1.640;
        internal const double GulardLootedCleanupSeconds = 1.772;
        internal const double NamedLootedCleanupPolicySeconds = 1.7;
        internal const double NamedLeashPolicyDistance = 40.0;
        internal const float NamedAutomaticAggroPolicyRadius = 7.0f;

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
                    DefenderNanoRepeatSeconds),
                new NamedEncounterState(
                    CreateYatilaDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila(),
                    true,
                    YatilaNanoCycle,
                    YatilaInitialNanoDelayPolicySeconds,
                    YatilaNanoRepeatPolicySeconds),
                new NamedEncounterState(
                    CreateGulardDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard(),
                    true,
                    new[] { GulardNanoId },
                    GulardInitialNanoDelaySeconds,
                    GulardNanoRepeatPolicySeconds),
                new NamedEncounterState(
                    CreateReAnimatorDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.ReAnimator(),
                    false,
                    new[] { ReAnimatorNanoId },
                    ReAnimatorInitialNanoDelaySeconds,
                    ReAnimatorNanoRepeatSeconds),
                new NamedEncounterState(
                    CreateBetanyDefinition(),
                    CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany(),
                    true,
                    new[] { BetanyNanoId },
                    BetanyInitialNanoDelaySeconds,
                    BetanyNanoRepeatSeconds)
            };
            this.reanimatedSlots = new[]
            {
                new ReanimatedSlotState(0, 65.80717f, 16.01125f, 292.15747f),
                new ReanimatedSlotState(1, 65.74661f, 15.53284f, 288.377f)
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
                if (state.Identity.Instance != 0 || state.RespawnDueAtUtc.HasValue)
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
            foreach (NamedEncounterState state in this.namedEncounters)
            {
                if (state.Identity.Instance != 0)
                {
                    CapturedEncounterRuntimeRegistry.Remove(state.Identity.Instance);
                }

                state.ResetAll();
            }

            foreach (ReanimatedSlotState slot in this.reanimatedSlots)
            {
                if (slot.Identity.Instance != 0)
                {
                    CapturedEncounterRuntimeRegistry.Remove(slot.Identity.Instance);
                }

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
            state.NextNanoAtUtc = utcNow.AddSeconds(state.InitialNanoDelaySeconds);
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

        internal ICharacter[] NotifyDeath(ICharacter target)
        {
            NamedEncounterState state = this.FindNamed(target);
            if (state != null)
            {
                state.Dead = true;
                state.ClearCombat();
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
                state.RespawnDueAtUtc = utcNow.AddSeconds(NamedRespawnAfterNpcDespawnSeconds);
                state.ClearCombat();
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

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(actor.FightingTarget);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            int nanoId = state.NanoCycle[state.NanoIndex % state.NanoCycle.Length];
            state.NanoIndex++;
            state.PendingNano = new PendingTempleNano(
                nanoId,
                target.Identity,
                utcNow.AddSeconds(NanoCastSeconds(nanoId)));
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

            if (pending.NanoId == ReAnimatorNanoId)
            {
                this.RequestNextReanimation(pending.FinishAtUtc);
            }

            // Captures prove the cast IDs and finish timing. Gulard's nearby
            // health changes and the reported 23-point poison tick do not have
            // safe packet ownership, so no unproven stat effect is applied.
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
                CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse(),
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
            CapturedEncounterRuntimeRegistry.Register(character.Identity.Instance, definition);
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

        private static double NanoCastSeconds(int nanoId)
        {
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
                double nanoRepeatSeconds)
            {
                this.Definition = definition;
                this.Combat = combat;
                this.AutomaticAggro = automaticAggro;
                this.NanoCycle = nanoCycle;
                this.InitialNanoDelaySeconds = initialNanoDelaySeconds;
                this.NanoRepeatSeconds = nanoRepeatSeconds;
                this.Identity = Identity.None;
            }

            internal CapturedEncounterRuntimeDefinition Definition { get; private set; }
            internal CapturedEnemyCombatContract Combat { get; private set; }
            internal bool AutomaticAggro { get; private set; }
            internal int[] NanoCycle { get; private set; }
            internal double InitialNanoDelaySeconds { get; private set; }
            internal double NanoRepeatSeconds { get; private set; }
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
            internal ReanimatedSlotState(int index, float x, float y, float z)
            {
                this.Index = index;
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.Identity = Identity.None;
            }

            internal int Index { get; private set; }
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
