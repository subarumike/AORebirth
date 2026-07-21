namespace AORebirth.Core.Playfields
{
    using System;
    using System.Globalization;

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
        internal const string DefenderProfileKey =
            CapturedTempleOfThreeWindsLootDefinitions.DefenderProfileKey;
        internal const string DefenderSpawnKey = "totw.647.boss.defender-of-the-three.spawn";
        internal const string DefenderEncounterKey =
            CapturedTempleOfThreeWindsLootDefinitions.DefenderEncounterKey;
        internal const int DefenderPrimaryNanoId = 205389;
        internal const int DefenderSecondaryNanoId = 205561;
        internal const double DefenderRespawnAfterNpcDespawnSeconds = 600.0;
        internal const double DefenderUnlootedCorpseLifetimeSeconds = 120.0;
        internal const double DefenderLootedCleanupSeconds = 1.277;
        internal const double DefenderMaximumObservedChaseDistance = 34.469125;
        internal const double DefenderLeashPolicyDistance = 40.0;
        internal const double DefenderInitialNanoDelaySeconds = 1.147246;
        internal const double DefenderNanoRepeatSeconds = 10.272;
        internal const double DefenderPrimaryNanoCastSeconds = 5.28395;
        internal const double DefenderSecondaryNanoCastSeconds = 6.1904;

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

        private readonly Playfield playfield;
        private readonly Action<ICharacter> activateNpc;
        private Identity defenderIdentity = Identity.None;
        private DateTime? defenderRespawnDueAtUtc;
        private DateTime? defenderNextNanoAtUtc;
        private PendingDefenderNano pendingNano;
        private bool defenderCombatActive;
        private bool defenderDead;
        private int defenderNanoIndex;

        internal CapturedTempleOfThreeWindsEncounterRuntimeService(
            Playfield playfield,
            Action<ICharacter> activateNpc)
        {
            this.playfield = playfield;
            this.activateNpc = activateNpc;
        }

        internal void ActivatePlayfield(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != PlayfieldInstance
                || this.defenderIdentity.Instance != 0
                || this.defenderRespawnDueAtUtc.HasValue)
            {
                return;
            }

            Character defender = this.SpawnDefender();
            if (defender != null)
            {
                this.defenderIdentity = defender.Identity;
                this.defenderDead = false;
            }
        }

        internal void ClearRuntimeState()
        {
            if (this.defenderIdentity.Instance != 0)
            {
                CapturedEncounterRuntimeRegistry.Remove(this.defenderIdentity.Instance);
            }

            this.defenderIdentity = Identity.None;
            this.defenderRespawnDueAtUtc = null;
            this.ClearCombatState();
            this.defenderDead = false;
            this.defenderNanoIndex = 0;
        }

        internal void ProcessDue(DateTime utcNow)
        {
            if (this.defenderRespawnDueAtUtc.HasValue
                && this.defenderRespawnDueAtUtc.Value <= utcNow
                && this.defenderIdentity.Instance == 0)
            {
                Character spawnedDefender = this.SpawnDefender();
                if (spawnedDefender != null)
                {
                    this.defenderIdentity = spawnedDefender.Identity;
                    this.defenderRespawnDueAtUtc = null;
                    this.defenderDead = false;
                    this.ClearCombatState();
                }
            }

            if (!this.defenderCombatActive
                || this.defenderDead
                || this.defenderIdentity.Instance == 0)
            {
                return;
            }

            ICharacter defender = this.playfield.FindByIdentity<ICharacter>(this.defenderIdentity);
            if (defender == null || defender.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            if (this.pendingNano != null)
            {
                if (this.pendingNano.FinishAtUtc <= utcNow)
                {
                    this.FinishDefenderNano(defender, this.pendingNano);
                }

                return;
            }

            if (!this.defenderNextNanoAtUtc.HasValue
                || this.defenderNextNanoAtUtc.Value > utcNow)
            {
                return;
            }

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(defender.FightingTarget);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            this.StartDefenderNano(defender, target, utcNow);
        }

        internal void NotifyCombatStarted(ICharacter npc, ICharacter target, DateTime utcNow)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (npc == null
                || target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || definition.ProfileKey != DefenderProfileKey)
            {
                return;
            }

            if (!this.defenderCombatActive)
            {
                this.defenderCombatActive = true;
                this.defenderNextNanoAtUtc = utcNow.AddSeconds(DefenderInitialNanoDelaySeconds);
            }
        }

        internal void NotifyCombatReset(ICharacter npc)
        {
            if (npc != null && npc.Identity == this.defenderIdentity)
            {
                this.ClearCombatState();
            }
        }

        internal void NotifyDeath(ICharacter target)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(target.Identity.Instance, out definition)
                || definition.ProfileKey != DefenderProfileKey)
            {
                return;
            }

            this.defenderDead = true;
            this.ClearCombatState();
        }

        internal void NotifyNpcDespawn(ICharacter target, DateTime utcNow)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(target.Identity.Instance, out definition)
                || definition.ProfileKey != DefenderProfileKey)
            {
                return;
            }

            this.defenderIdentity = Identity.None;
            this.defenderRespawnDueAtUtc = utcNow.AddSeconds(
                DefenderRespawnAfterNpcDespawnSeconds);
            this.ClearCombatState();
        }

        internal bool IsCapturedNanoCastInProgress(ICharacter character)
        {
            return character != null
                   && this.pendingNano != null
                   && character.Identity == this.defenderIdentity;
        }

        internal static CapturedEncounterRuntimeDefinition CreateDefenderDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                DefenderProfileKey,
                DefenderSpawnKey,
                DefenderEncounterKey,
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
                DefenderUnlootedCorpseLifetimeSeconds,
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
                maximumNpcLeashDistanceFromHome: DefenderLeashPolicyDistance);
        }

        private void StartDefenderNano(
            ICharacter defender,
            ICharacter target,
            DateTime utcNow)
        {
            int nanoId = DefenderNanoCycle[
                this.defenderNanoIndex % DefenderNanoCycle.Length];
            this.defenderNanoIndex++;
            double castSeconds = nanoId == DefenderPrimaryNanoId
                                     ? DefenderPrimaryNanoCastSeconds
                                     : DefenderSecondaryNanoCastSeconds;
            this.pendingNano = new PendingDefenderNano(
                nanoId,
                target.Identity,
                utcNow.AddSeconds(castSeconds));
            this.defenderNextNanoAtUtc = utcNow.AddSeconds(DefenderNanoRepeatSeconds);
            CastNanoSpellMessageHandler.Default.SendCapturedNpcCast(
                defender,
                nanoId,
                target.Identity);
        }

        private void FinishDefenderNano(ICharacter defender, PendingDefenderNano pending)
        {
            this.pendingNano = null;
            ICharacter target = this.playfield.FindByIdentity<ICharacter>(pending.TargetIdentity);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            CharacterActionMessageHandler.Default.FinishNanoCasting(
                defender,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                pending.NanoId);
            // The captures prove the two cast IDs and completion timing, but do
            // not isolate a target stat delta that can safely be attributed to
            // either nano. Preserve the cast wire sequence without inventing an
            // effect until a dedicated effect capture proves it.
        }

        private Character SpawnDefender()
        {
            CapturedEncounterRuntimeDefinition definition = CreateDefenderDefinition();
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

            character.Textures.Clear();
            foreach (CapturedSubwayTextureDefinition texture in definition.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            string combatFailure;
            if (!CapturedEnemyCombatRuntime.Prepare(
                    character,
                    controller,
                    CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree(),
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

        private void ClearCombatState()
        {
            this.defenderCombatActive = false;
            this.defenderNextNanoAtUtc = null;
            this.pendingNano = null;
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

        private sealed class PendingDefenderNano
        {
            internal PendingDefenderNano(
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
    }
}
