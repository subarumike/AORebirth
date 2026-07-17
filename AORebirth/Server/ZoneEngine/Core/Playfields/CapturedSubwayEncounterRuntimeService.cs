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

    internal sealed class CapturedSubwayEncounterRuntimeService
    {
        internal const int SubwayPlayfieldId = 127;
        internal const int AbmouthMonsterData = 155962;
        internal const int InfectorMonsterData = 31909;
        internal const int VergilAeneidMonsterData = 203748;
        internal const string AbmouthProfileKey = "subway.127.boss.abmouth-supremus";
        internal const string InfectorProfileKey = "subway.127.encounter.abmouth-infector";
        internal const string VergilAeneidProfileKey = "subway.127.boss.vergil-aeneid";
        internal const string EncounterKey = "subway.127.encounter.abmouth";
        internal const string VergilAeneidEncounterKey = "subway.127.encounter.vergil-aeneid";

        private const float CapturedAggroRadius = 13.4151f;
        private const float CapturedReplacementInfectorOffsetX = 3.0f;
        private const string FirstInfectorUnknown1 =
            "80000000000000000000000003010001000100010001000000020000";
        private const string SecondInfectorUnknown1 =
            "80000000000000008000000003010001000100010001000000020000";
        private const string ReplacementInfectorUnknown1 =
            "00000000000000008000000003010001000100010001000000020000";
        private const double FirstInfectorDelaySeconds = 1.212281;
        private const double SecondInfectorDelaySeconds = 2.326367;
        private const int VergilDirectHealNanoId = 43827;
        private const int VergilDirectHealAmount = 187;
        private const double VergilDirectHealCastSeconds = 1.480007;
        private const int VergilSelfHealNanoId = 43880;
        private const int VergilSelfHealAmount = 34;
        private const int VergilSelfHealDurationMilliseconds = 14000;
        private const double VergilSelfHealCastSeconds = 1.763334;
        private const double VergilDirectHealCooldownSeconds = 30.654;
        private const int VergilSelfHealTriggerPermille = 180;
        private const float VergilDirectHealRange = 13.0f;
        private static readonly TimeSpan CapturedNamedBossRespawnDelay = TimeSpan.FromMinutes(10);

        private static readonly double[] CapturedRefillDelays = { 0.830, 0.380, 3.322, 3.490 };
        private static readonly CapturedEncounterLevelHealthVariant[] VergilAeneidVariants =
        {
            new CapturedEncounterLevelHealthVariant(
                29,
                6796,
                131,
                131,
                "20260716-034433 fight/corpse"),
            new CapturedEncounterLevelHealthVariant(
                30,
                7227,
                132,
                135,
                "20260709-222339 SCFU #5445; 20260712-234401 fight"),
            new CapturedEncounterLevelHealthVariant(
                31,
                7659,
                132,
                140,
                "20260712-232711 fight")
        };

        private readonly Playfield playfield;
        private readonly PlayfieldDynelRegistry dynelRegistry;
        private readonly Action<ICharacter> activateNpc;
        private readonly object spawnRandomSync = new object();
        private readonly Random spawnRandom = new Random();
        private readonly InfectorSlotState[] infectorSlots =
        {
            new InfectorSlotState(0),
            new InfectorSlotState(1)
        };

        private Identity abmouthIdentity = Identity.None;
        private Identity vergilAeneidIdentity = Identity.None;
        private bool combatActive;
        private bool abmouthDead;
        private DateTime? abmouthRespawnDueAtUtc;
        private bool vergilCombatActive;
        private bool vergilDead;
        private DateTime? vergilRespawnDueAtUtc;
        private DateTime vergilNextHealAtUtc;
        private PendingVergilHeal vergilPendingHeal;
        private int refillDelayIndex;

        internal CapturedSubwayEncounterRuntimeService(
            Playfield playfield,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> activateNpc)
        {
            this.playfield = playfield;
            this.dynelRegistry = dynelRegistry;
            this.activateNpc = activateNpc;
        }

        internal void ActivatePlayfield(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != SubwayPlayfieldId)
            {
                return;
            }

            if (this.abmouthIdentity.Instance == 0 && !this.abmouthRespawnDueAtUtc.HasValue)
            {
                CapturedEncounterRuntimeDefinition definition = CreateBossDefinition();
                Character boss = this.SpawnCharacter(definition, Identity.None);
                if (boss != null)
                {
                    this.abmouthIdentity = boss.Identity;
                    this.abmouthDead = false;
                }
            }

            if (this.vergilAeneidIdentity.Instance == 0 && !this.vergilRespawnDueAtUtc.HasValue)
            {
                Character vergil = this.SpawnCharacter(
                    this.CreateVergilAeneidDefinition(),
                    Identity.None);
                if (vergil != null)
                {
                    this.vergilAeneidIdentity = vergil.Identity;
                }
            }
        }

        internal void ClearRuntimeState()
        {
            CapturedEncounterRuntimeRegistry.RemoveForPlayfield(this.playfield.Identity.Instance);
            this.abmouthIdentity = Identity.None;
            this.vergilAeneidIdentity = Identity.None;
            this.combatActive = false;
            this.abmouthDead = false;
            this.abmouthRespawnDueAtUtc = null;
            this.ClearVergilCombatState();
            this.vergilRespawnDueAtUtc = null;
            this.refillDelayIndex = 0;
            foreach (InfectorSlotState slot in this.infectorSlots)
            {
                slot.ActiveIdentity = Identity.None;
                slot.SpawnDueAtUtc = null;
                slot.Generation = 0;
            }
        }

        internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (npc == null
                || npc.FightingTarget.Instance != 0
                || npc.Stats[StatIds.health].Value <= 0
                || !CapturedEncounterRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || !string.Equals(
                    definition.ProfileKey,
                    AbmouthProfileKey,
                    StringComparison.Ordinal))
            {
                return null;
            }

            // The capture proves proactive aggro at this horizontal distance. It does
            // not prove the live maximum, so do not extend the radius beyond it.
            return this.dynelRegistry
                .FindCharactersInRange(npc, CapturedAggroRadius)
                .Where(
                    candidate => candidate != null
                                 && candidate.Controller is PlayerController
                                 && candidate.Stats[StatIds.health].Value > 0)
                .OrderBy(candidate => candidate.Coordinates().coordinate.Distance2D(npc.Coordinates().coordinate))
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
        }

        internal void NotifyCombatStarted(ICharacter npc, ICharacter target, DateTime utcNow)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (npc == null
                || target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(npc.Identity.Instance, out definition))
            {
                return;
            }

            if (string.Equals(
                definition.ProfileKey,
                VergilAeneidProfileKey,
                StringComparison.Ordinal))
            {
                if (!this.vergilCombatActive)
                {
                    this.vergilCombatActive = true;
                    this.vergilDead = false;
                    this.vergilNextHealAtUtc = utcNow;
                    this.vergilPendingHeal = null;
                }

                return;
            }

            if (!string.Equals(
                definition.ProfileKey,
                AbmouthProfileKey,
                StringComparison.Ordinal))
            {
                return;
            }

            if (this.combatActive)
            {
                return;
            }

            this.combatActive = true;
            this.abmouthDead = false;
            this.infectorSlots[0].SpawnDueAtUtc = utcNow.AddSeconds(FirstInfectorDelaySeconds);
            this.infectorSlots[1].SpawnDueAtUtc = utcNow.AddSeconds(SecondInfectorDelaySeconds);
        }

        internal ICharacter[] NotifyCombatReset(ICharacter npc)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (npc == null
                || !CapturedEncounterRuntimeRegistry.TryGet(npc.Identity.Instance, out definition))
            {
                return new ICharacter[0];
            }

            if (string.Equals(
                definition.ProfileKey,
                VergilAeneidProfileKey,
                StringComparison.Ordinal))
            {
                this.ClearVergilCombatState();
                return new ICharacter[0];
            }

            if (!string.Equals(
                definition.ProfileKey,
                AbmouthProfileKey,
                StringComparison.Ordinal))
            {
                return new ICharacter[0];
            }

            this.combatActive = false;
            this.abmouthDead = false;
            this.refillDelayIndex = 0;
            var activeSummons = new List<ICharacter>();
            foreach (InfectorSlotState slot in this.infectorSlots)
            {
                slot.SpawnDueAtUtc = null;
                ICharacter summon = slot.ActiveIdentity.Instance == 0
                                        ? null
                                        : this.playfield.FindByIdentity<ICharacter>(slot.ActiveIdentity);
                slot.ActiveIdentity = Identity.None;
                slot.Generation = 0;
                if (summon != null && summon.Stats[StatIds.health].Value > 0)
                {
                    activeSummons.Add(summon);
                }
            }

            return activeSummons.ToArray();
        }

        internal void ProcessDue(DateTime utcNow, Action<ICharacter, ICharacter> acquireAggro)
        {
            this.ProcessNamedBossRespawns(utcNow);
            this.ProcessVergilHealing(utcNow);

            if (!this.combatActive || this.abmouthDead || this.abmouthIdentity.Instance == 0)
            {
                return;
            }

            ICharacter boss = this.playfield.FindByIdentity<ICharacter>(this.abmouthIdentity);
            if (boss == null || boss.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(boss.FightingTarget);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            foreach (InfectorSlotState slot in this.infectorSlots)
            {
                if (slot.ActiveIdentity.Instance != 0
                    || !slot.SpawnDueAtUtc.HasValue
                    || slot.SpawnDueAtUtc.Value > utcNow)
                {
                    continue;
                }

                Character infector = this.SpawnInfector(slot, boss);
                slot.SpawnDueAtUtc = null;
                if (infector != null)
                {
                    slot.ActiveIdentity = infector.Identity;
                    slot.Generation++;
                    acquireAggro(target, infector);
                }
            }
        }

        internal ICharacter[] NotifyDeath(ICharacter target, DateTime diedAtUtc)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(target.Identity.Instance, out definition))
            {
                return new ICharacter[0];
            }

            if (string.Equals(
                definition.ProfileKey,
                VergilAeneidProfileKey,
                StringComparison.Ordinal))
            {
                this.ClearVergilCombatState();
                this.vergilDead = true;
                this.vergilRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);
                return new ICharacter[0];
            }

            if (!string.Equals(
                definition.ProfileKey,
                AbmouthProfileKey,
                StringComparison.Ordinal))
            {
                return new ICharacter[0];
            }

            this.abmouthDead = true;
            this.combatActive = false;
            this.abmouthRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);
            var livingSummons = new List<ICharacter>();
            foreach (InfectorSlotState slot in this.infectorSlots)
            {
                slot.SpawnDueAtUtc = null;
                ICharacter summon = slot.ActiveIdentity.Instance == 0
                                        ? null
                                        : this.playfield.FindByIdentity<ICharacter>(slot.ActiveIdentity);
                if (summon == null || summon.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                summon.Stats[StatIds.petmaster].Value = 0;
                summon.SendChangedStats();
                livingSummons.Add(summon);
            }

            return livingSummons.ToArray();
        }

        private void ProcessNamedBossRespawns(DateTime utcNow)
        {
            if (this.abmouthRespawnDueAtUtc.HasValue
                && this.abmouthRespawnDueAtUtc.Value <= utcNow
                && this.abmouthIdentity.Instance == 0)
            {
                Character boss = this.SpawnCharacter(CreateBossDefinition(), Identity.None);
                if (boss != null)
                {
                    this.abmouthIdentity = boss.Identity;
                    this.abmouthDead = false;
                    this.combatActive = false;
                    this.refillDelayIndex = 0;
                    foreach (InfectorSlotState slot in this.infectorSlots)
                    {
                        slot.ActiveIdentity = Identity.None;
                        slot.SpawnDueAtUtc = null;
                        slot.Generation = 0;
                    }

                    this.abmouthRespawnDueAtUtc = null;
                }
            }

            if (this.vergilRespawnDueAtUtc.HasValue
                && this.vergilRespawnDueAtUtc.Value <= utcNow
                && this.vergilAeneidIdentity.Instance == 0)
            {
                Character vergil = this.SpawnCharacter(
                    this.CreateVergilAeneidDefinition(),
                    Identity.None);
                if (vergil != null)
                {
                    this.vergilAeneidIdentity = vergil.Identity;
                    this.ClearVergilCombatState();
                    this.vergilRespawnDueAtUtc = null;
                }
            }
        }

        internal void NotifyNpcDespawn(ICharacter target, DateTime utcNow)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (target == null
                || !CapturedEncounterRuntimeRegistry.TryGet(target.Identity.Instance, out definition))
            {
                return;
            }

            if (string.Equals(
                definition.ProfileKey,
                AbmouthProfileKey,
                StringComparison.Ordinal))
            {
                this.abmouthIdentity = Identity.None;
                this.combatActive = false;
                return;
            }

            if (string.Equals(
                definition.ProfileKey,
                VergilAeneidProfileKey,
                StringComparison.Ordinal))
            {
                this.vergilAeneidIdentity = Identity.None;
                this.ClearVergilCombatState();
                return;
            }

            InfectorSlotState slot = this.infectorSlots.FirstOrDefault(
                value => value.ActiveIdentity == target.Identity);
            if (slot == null)
            {
                return;
            }

            slot.ActiveIdentity = Identity.None;
            if (!this.abmouthDead && this.combatActive && this.abmouthIdentity.Instance != 0)
            {
                double delay = CapturedRefillDelays[this.refillDelayIndex % CapturedRefillDelays.Length];
                this.refillDelayIndex++;
                slot.SpawnDueAtUtc = utcNow.AddSeconds(delay);
            }
        }

        internal bool IsCapturedNanoCastInProgress(ICharacter character)
        {
            return character != null
                   && this.vergilPendingHeal != null
                   && character.Identity == this.vergilAeneidIdentity;
        }

        private void ProcessVergilHealing(DateTime utcNow)
        {
            if (!this.vergilCombatActive
                || this.vergilDead
                || this.vergilAeneidIdentity.Instance == 0)
            {
                return;
            }

            ICharacter vergil = this.playfield.FindByIdentity<ICharacter>(this.vergilAeneidIdentity);
            if (vergil == null
                || vergil.Stats[StatIds.health].Value <= 0
                || vergil.FightingTarget.Instance == 0)
            {
                return;
            }

            if (this.vergilPendingHeal != null)
            {
                if (this.vergilPendingHeal.FinishAtUtc <= utcNow)
                {
                    this.FinishVergilHeal(vergil, this.vergilPendingHeal);
                }

                return;
            }

            if (this.vergilNextHealAtUtc > utcNow)
            {
                return;
            }

            int level = vergil.Stats[StatIds.level].Value;
            if (level == 31)
            {
                ICharacter target = this.FindVergilDirectHealTarget(vergil);
                if (target == null)
                {
                    return;
                }

                this.StartVergilHeal(
                    vergil,
                    target,
                    VergilDirectHealNanoId,
                    VergilDirectHealAmount,
                    VergilDirectHealCastSeconds,
                    0,
                    utcNow);
                this.vergilNextHealAtUtc = utcNow.AddSeconds(VergilDirectHealCooldownSeconds);
                return;
            }

            if (level != 30)
            {
                return;
            }

            int maximumHealth = vergil.Stats[StatIds.life].Value;
            int currentHealth = vergil.Stats[StatIds.health].Value;
            if (maximumHealth <= 0
                || currentHealth * 1000 > maximumHealth * VergilSelfHealTriggerPermille)
            {
                return;
            }

            this.StartVergilHeal(
                vergil,
                vergil,
                VergilSelfHealNanoId,
                VergilSelfHealAmount,
                VergilSelfHealCastSeconds,
                VergilSelfHealDurationMilliseconds,
                utcNow);
            // Only one level-30 self-heal was observed before death. Do not invent
            // another cooldown or repeated HoT cycle from that short fight.
            this.vergilNextHealAtUtc = DateTime.MaxValue;
        }

        private ICharacter FindVergilDirectHealTarget(ICharacter vergil)
        {
            IEnumerable<ICharacter> nearbyCandidates = this.dynelRegistry
                .FindCharactersInRange(vergil, VergilDirectHealRange)
                .Where(
                    candidate => candidate != null
                                 && candidate.Identity != vergil.Identity
                                 && candidate.Controller is NPCController
                                 && candidate.Stats[StatIds.petmaster].Value == 0
                                 && candidate.Stats[StatIds.health].Value > 0
                                 && candidate.Stats[StatIds.life].Value > 0
                                 && candidate.Stats[StatIds.health].Value
                                    < candidate.Stats[StatIds.life].Value);

            IEnumerable<ICharacter> candidates = nearbyCandidates;
            if (vergil.Stats[StatIds.health].Value > 0
                && vergil.Stats[StatIds.health].Value < vergil.Stats[StatIds.life].Value)
            {
                candidates = candidates.Concat(new[] { vergil });
            }

            return candidates
                .OrderBy(
                    candidate => (double)candidate.Stats[StatIds.health].Value
                                 / candidate.Stats[StatIds.life].Value)
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
        }

        private void StartVergilHeal(
            ICharacter vergil,
            ICharacter target,
            int nanoId,
            int healAmount,
            double castSeconds,
            int durationMilliseconds,
            DateTime utcNow)
        {
            this.vergilPendingHeal = new PendingVergilHeal(
                target.Identity,
                nanoId,
                healAmount,
                durationMilliseconds,
                utcNow.AddSeconds(castSeconds));
            CastNanoSpellMessageHandler.Default.Send(vergil, nanoId, target.Identity);
        }

        private void FinishVergilHeal(ICharacter vergil, PendingVergilHeal pending)
        {
            this.vergilPendingHeal = null;
            ICharacter target = this.playfield.FindByIdentity<ICharacter>(pending.TargetIdentity);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            CharacterActionMessageHandler.Default.FinishNanoCasting(
                vergil,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                pending.NanoId);
            if (pending.DurationMilliseconds > 0)
            {
                CharacterActionMessageHandler.Default.NotifyActiveNanoDuration(
                    vergil,
                    target.Identity,
                    pending.NanoId,
                    pending.DurationMilliseconds);
            }

            int healthBefore = target.Stats[StatIds.health].Value;
            int maximumHealth = target.Stats[StatIds.life].Value;
            int healthAfter = Math.Min(maximumHealth, healthBefore + pending.HealAmount);
            int appliedHeal = healthAfter - healthBefore;
            if (appliedHeal <= 0)
            {
                return;
            }

            target.Stats[StatIds.health].Value = healthAfter;
            this.playfield.Announce(
                new HealthDamageMessage
                {
                    Identity = target.Identity,
                    Unknown1 = healthAfter,
                    Unknown2 = appliedHeal,
                    Unknown3 = 0,
                    Unknown4 = 0,
                    Target = vergil.Identity,
                    Unknown5 = 0
                });
        }

        private void ClearVergilCombatState()
        {
            this.vergilCombatActive = false;
            this.vergilDead = false;
            this.vergilNextHealAtUtc = DateTime.MinValue;
            this.vergilPendingHeal = null;
        }

        private Character SpawnInfector(InfectorSlotState slot, ICharacter boss)
        {
            CapturedEncounterRuntimeDefinition definition;
            if (slot.Generation == 0)
            {
                definition = slot.Slot == 0
                                 ? CreateFirstInfectorDefinition()
                                 : CreateSecondInfectorDefinition();
            }
            else
            {
                definition = CreateInfectorDefinition(
                    slot,
                    boss.RawCoordinates.X + CapturedReplacementInfectorOffsetX,
                    boss.RawCoordinates.Y,
                    boss.RawCoordinates.Z,
                    boss.RawHeading.xf,
                    boss.RawHeading.yf,
                    boss.RawHeading.zf,
                    boss.RawHeading.wf,
                    ReplacementInfectorUnknown1);
            }

            return this.SpawnCharacter(definition, boss.Identity);
        }

        private Character SpawnCharacter(
            CapturedEncounterRuntimeDefinition definition,
            Identity ownerIdentity)
        {
            int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
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
            character.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
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
            CapturedEnemyCombatContract combat = CapturedSubwayCombatCatalog.For(
                definition.DisplayName,
                definition.MonsterData);
            if (!CapturedEnemyCombatRuntime.Prepare(character, controller, combat, out combatFailure))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Captured Subway encounter combat refused actor=" + definition.ProfileKey
                    + " reason=" + combatFailure);
                Pool.Instance.RemoveObject(character);
                return null;
            }

            character.DoNotDoTimers = false;
            CapturedEncounterRuntimeRegistry.Register(character.Identity.Instance, definition);
            this.activateNpc(character);
            this.playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);
            if (ownerIdentity.Instance != 0)
            {
                this.AnnounceCapturedInfectorStat(
                    character.Identity,
                    StatIds.petmaster,
                    ownerIdentity.Instance);
                SetStat(character, StatIds.flags, unchecked((int)0x18081201));
                this.AnnounceCapturedInfectorStat(
                    character.Identity,
                    StatIds.flags,
                    unchecked((int)0x18081201));
            }
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Encounter actor spawned profile={0} identity={1} position=({2},{3},{4}) evidence={5}",
                    definition.ProfileKey,
                    character.Identity,
                    definition.X,
                    definition.Y,
                    definition.Z,
                    definition.Evidence));
            return character;
        }

        private void AnnounceCapturedInfectorStat(Identity identity, StatIds stat, int value)
        {
            this.playfield.Announce(
                new StatMessage
                {
                    Identity = identity,
                    Stats = new[]
                    {
                        new GameTuple<CharacterStat, uint>
                        {
                            Value1 = (CharacterStat)(int)stat,
                            Value2 = unchecked((uint)value)
                        }
                    }
                });
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }

        private static CapturedEncounterRuntimeDefinition CreateBossDefinition()
        {
            return new CapturedEncounterRuntimeDefinition(
                AbmouthProfileKey,
                "subway.127.boss.abmouth-supremus.spawn",
                EncounterKey,
                "Abmouth Supremus",
                AbmouthMonsterData,
                true,
                false,
                30,
                10324,
                162,
                115,
                114,
                0,
                3,
                357.088409f,
                76.107948f,
                99.123543f,
                0.0f,
                -0.713226199f,
                0.0f,
                0.700933933f,
                0x04CB,
                unchecked((int)0x022A4A43),
                0,
                HexToBytes("80000000000000008000000003010001000100010001000000020000"),
                0,
                155548,
                1800.0,
                3.0,
                "20260712-224840 SCFU #1808; 20260712-232137 fight/corpse/loot; "
                + "20260716-220400 spawn/fight/death/corpse");
        }

        private CapturedEncounterRuntimeDefinition CreateVergilAeneidDefinition()
        {
            CapturedEncounterLevelHealthVariant variant;
            lock (this.spawnRandomSync)
            {
                variant = VergilAeneidVariants[this.spawnRandom.Next(VergilAeneidVariants.Length)];
            }

            return new CapturedEncounterRuntimeDefinition(
                VergilAeneidProfileKey,
                "subway.127.boss.vergil-aeneid.spawn",
                VergilAeneidEncounterKey,
                "Vergil Aeneid",
                VergilAeneidMonsterData,
                true,
                false,
                variant.Level,
                variant.Health,
                variant.MonsterScale,
                variant.RunSpeed,
                // The exact PF127 appearance template retains the captured L30
                // SCFU RunSpeedBase because no alive L29/L31 SCFU base is available.
                134,
                0,
                3,
                278.045074f,
                73.01795f,
                98.80104f,
                0.0f,
                -0.7096085f,
                0.0f,
                0.704596162f,
                1643u,
                unchecked((int)0x020B4ACB),
                0,
                HexToBytes("00000000000000000000000002010001000100010001000000020000"),
                0,
                5921,
                1800.0,
                3.0,
                variant.Evidence
                + "; exact spawn/appearance 20260709-222339 SCFU #5445; "
                + "Mike 20260716 30-minute loot corpse and 10-minute respawn",
                npcFamily: 138,
                npcLosHeight: 0,
                fatness: 1,
                breed: 3,
                sex: 2,
                race: 1,
                headMesh: 40171,
                textures: new[]
                {
                    new CapturedSubwayTextureDefinition(0, 117653, 0),
                    new CapturedSubwayTextureDefinition(1, 9609, 0),
                    new CapturedSubwayTextureDefinition(2, 9615, 0),
                    new CapturedSubwayTextureDefinition(3, 9607, 0),
                    new CapturedSubwayTextureDefinition(4, 9622, 0)
                },
                meshes: new[]
                {
                    new CapturedSubwayMeshDefinition(0, 40171u, 0, 4),
                    new CapturedSubwayMeshDefinition(1, 21126u, 0, 2)
                },
                waypoints: new[]
                {
                    new CapturedSubwayWaypointDefinition(278.045074f, 73.01795f, 98.80104f)
                });
        }

        private static CapturedEncounterRuntimeDefinition CreateFirstInfectorDefinition()
        {
            return CreateInfectorDefinition(
                new InfectorSlotState(0),
                355.542145f,
                68.955902f,
                99.459953f,
                0.0f,
                -0.673485816f,
                0.0f,
                0.739200115f,
                FirstInfectorUnknown1);
        }

        private static CapturedEncounterRuntimeDefinition CreateSecondInfectorDefinition()
        {
            return CreateInfectorDefinition(
                new InfectorSlotState(1),
                350.425507f,
                71.647079f,
                99.786812f,
                0.0f,
                -0.715518296f,
                0.0f,
                0.698594034f,
                SecondInfectorUnknown1);
        }

        private static CapturedEncounterRuntimeDefinition CreateInfectorDefinition(
            InfectorSlotState slot,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            string capturedScfuUnknown1)
        {
            return new CapturedEncounterRuntimeDefinition(
                InfectorProfileKey,
                "subway.127.encounter.abmouth-infector.slot." + slot.Slot,
                EncounterKey,
                "Infector",
                InfectorMonsterData,
                false,
                true,
                24,
                968,
                70,
                162,
                105,
                10,
                0,
                x,
                y,
                z,
                headingX,
                headingY,
                headingZ,
                headingW,
                0x04C8,
                unchecked((int)0x022A4A43),
                2,
                HexToBytes(capturedScfuUnknown1),
                0,
                31868,
                300.0,
                3.0,
                "20260712-224840 SCFU #1835/#1870; 20260712-232137 two-slot refill");
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] value = new byte[hex.Length / 2];
            for (int index = 0; index < value.Length; index++)
            {
                value[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }
            return value;
        }

        private sealed class InfectorSlotState
        {
            internal InfectorSlotState(int slot)
            {
                this.Slot = slot;
                this.ActiveIdentity = Identity.None;
            }

            internal int Slot { get; private set; }
            internal Identity ActiveIdentity { get; set; }
            internal DateTime? SpawnDueAtUtc { get; set; }
            internal int Generation { get; set; }
        }

        private sealed class CapturedEncounterLevelHealthVariant
        {
            internal CapturedEncounterLevelHealthVariant(
                int level,
                int health,
                int monsterScale,
                int runSpeed,
                string evidence)
            {
                this.Level = level;
                this.Health = health;
                this.MonsterScale = monsterScale;
                this.RunSpeed = runSpeed;
                this.Evidence = evidence;
            }

            internal int Level { get; private set; }
            internal int Health { get; private set; }
            internal int MonsterScale { get; private set; }
            internal int RunSpeed { get; private set; }
            internal string Evidence { get; private set; }
        }

        private sealed class PendingVergilHeal
        {
            internal PendingVergilHeal(
                Identity targetIdentity,
                int nanoId,
                int healAmount,
                int durationMilliseconds,
                DateTime finishAtUtc)
            {
                this.TargetIdentity = targetIdentity;
                this.NanoId = nanoId;
                this.HealAmount = healAmount;
                this.DurationMilliseconds = durationMilliseconds;
                this.FinishAtUtc = finishAtUtc;
            }

            internal Identity TargetIdentity { get; private set; }
            internal int NanoId { get; private set; }
            internal int HealAmount { get; private set; }
            internal int DurationMilliseconds { get; private set; }
            internal DateTime FinishAtUtc { get; private set; }
        }
    }

    // Keep the established profile constants available to the existing loot and
    // corpse adapters while runtime ownership moves to the generic Subway service.
    internal static class AbmouthEncounterRuntimeService
    {
        internal const int SubwayPlayfieldId = CapturedSubwayEncounterRuntimeService.SubwayPlayfieldId;
        internal const string AbmouthProfileKey =
            CapturedSubwayEncounterRuntimeService.AbmouthProfileKey;
        internal const string InfectorProfileKey =
            CapturedSubwayEncounterRuntimeService.InfectorProfileKey;
    }

    internal sealed class CapturedEncounterRuntimeDefinition
    {
        internal CapturedEncounterRuntimeDefinition(
            string profileKey,
            string spawnKey,
            string encounterKey,
            string displayName,
            int monsterData,
            bool isBoss,
            bool isEncounterSummon,
            int level,
            int health,
            int monsterScale,
            int runSpeed,
            int capturedScfuRunSpeedBase,
            int capturedScfuNpcUnknownData,
            int side,
            float x,
            float y,
            float z,
            float headingX,
            float headingY,
            float headingZ,
            float headingW,
            uint appearanceValue,
            int capturedScfuFlags,
            int capturedScfuFlags2,
            byte[] capturedScfuUnknown1,
            int capturedScfuUnknown2,
            int corpseCatMesh,
            double unlootedCorpseLifetimeSeconds,
            double lootedCleanupSeconds,
            string evidence,
            int npcFamily = 150,
            int npcLosHeight = 0,
            int fatness = 1,
            int breed = 6,
            int sex = 0,
            int race = 1,
            int headMesh = 0,
            CapturedSubwayTextureDefinition[] textures = null,
            CapturedSubwayMeshDefinition[] meshes = null,
            CapturedSubwayWaypointDefinition[] waypoints = null)
        {
            this.ProfileKey = profileKey;
            this.SpawnKey = spawnKey;
            this.EncounterKey = encounterKey;
            this.DisplayName = displayName;
            this.MonsterData = monsterData;
            this.IsBoss = isBoss;
            this.IsEncounterSummon = isEncounterSummon;
            this.Level = level;
            this.Health = health;
            this.MonsterScale = monsterScale;
            this.RunSpeed = runSpeed;
            this.CapturedScfuRunSpeedBase = capturedScfuRunSpeedBase;
            this.CapturedScfuNpcUnknownData = capturedScfuNpcUnknownData;
            this.Side = side;
            this.NpcFamily = npcFamily;
            this.NpcLosHeight = npcLosHeight;
            this.Fatness = fatness;
            this.Breed = breed;
            this.Sex = sex;
            this.Race = race;
            this.HeadMesh = headMesh;
            this.Textures = textures ?? CreateDefaultTextures();
            this.Meshes = meshes ?? new CapturedSubwayMeshDefinition[0];
            this.Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0];
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.HeadingX = headingX;
            this.HeadingY = headingY;
            this.HeadingZ = headingZ;
            this.HeadingW = headingW;
            this.AppearanceValue = appearanceValue;
            this.CapturedScfuFlags = capturedScfuFlags;
            this.CapturedScfuFlags2 = capturedScfuFlags2;
            this.CapturedScfuUnknown1 = capturedScfuUnknown1 ?? new byte[0];
            this.CapturedScfuUnknown2 = capturedScfuUnknown2;
            this.CorpseCatMesh = corpseCatMesh;
            this.UnlootedCorpseLifetimeSeconds = unlootedCorpseLifetimeSeconds;
            this.LootedCleanupSeconds = lootedCleanupSeconds;
            this.Evidence = evidence;
        }

        internal string ProfileKey { get; private set; }
        internal string SpawnKey { get; private set; }
        internal string EncounterKey { get; private set; }
        internal string DisplayName { get; private set; }
        internal int MonsterData { get; private set; }
        internal bool IsBoss { get; private set; }
        internal bool IsEncounterSummon { get; private set; }
        internal int Level { get; private set; }
        internal int Health { get; private set; }
        internal int MonsterScale { get; private set; }
        internal int RunSpeed { get; private set; }
        internal int CapturedScfuRunSpeedBase { get; private set; }
        internal int CapturedScfuNpcUnknownData { get; private set; }
        internal int Side { get; private set; }
        internal int NpcFamily { get; private set; }
        internal int NpcLosHeight { get; private set; }
        internal int Fatness { get; private set; }
        internal int Breed { get; private set; }
        internal int Sex { get; private set; }
        internal int Race { get; private set; }
        internal int HeadMesh { get; private set; }
        internal CapturedSubwayTextureDefinition[] Textures { get; private set; }
        internal CapturedSubwayMeshDefinition[] Meshes { get; private set; }
        internal CapturedSubwayWaypointDefinition[] Waypoints { get; private set; }
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
        internal float HeadingX { get; private set; }
        internal float HeadingY { get; private set; }
        internal float HeadingZ { get; private set; }
        internal float HeadingW { get; private set; }
        internal uint AppearanceValue { get; private set; }
        internal int CapturedScfuFlags { get; private set; }
        internal int CapturedScfuFlags2 { get; private set; }
        internal byte[] CapturedScfuUnknown1 { get; private set; }
        internal int CapturedScfuUnknown2 { get; private set; }
        internal int CorpseCatMesh { get; private set; }
        internal double UnlootedCorpseLifetimeSeconds { get; private set; }
        internal double LootedCleanupSeconds { get; private set; }
        internal string Evidence { get; private set; }

        private static CapturedSubwayTextureDefinition[] CreateDefaultTextures()
        {
            return Enumerable.Range(0, 5)
                .Select(place => new CapturedSubwayTextureDefinition(place, 0, 0))
                .ToArray();
        }
    }

    internal static class CapturedEncounterRuntimeRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, CapturedEncounterRuntimeDefinition> Definitions =
            new Dictionary<int, CapturedEncounterRuntimeDefinition>();

        internal static void Register(int runtimeInstance, CapturedEncounterRuntimeDefinition definition)
        {
            lock (Sync)
            {
                Definitions[runtimeInstance] = definition;
            }
        }

        internal static bool TryGet(
            int runtimeInstance,
            out CapturedEncounterRuntimeDefinition definition)
        {
            lock (Sync)
            {
                return Definitions.TryGetValue(runtimeInstance, out definition);
            }
        }

        internal static void Remove(int runtimeInstance)
        {
            lock (Sync)
            {
                Definitions.Remove(runtimeInstance);
            }
        }

        internal static void RemoveForPlayfield(int playfieldInstance)
        {
            if (playfieldInstance != CapturedSubwayEncounterRuntimeService.SubwayPlayfieldId)
            {
                return;
            }

            lock (Sync)
            {
                Definitions.Clear();
            }
        }
    }
}
