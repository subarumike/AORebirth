namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NpcCombatTickCoordinator
    {
        private const int MissingItemStatValue = 1234567890;

        private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, int> lastNpcCombatWeaponSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, int> lastNpcUnarmedAttackInfoSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, int> lastNpcSpecialAttackWeaponTargets = new Dictionary<int, int>();

        private readonly HashSet<int> completedCapturedOpeningAttacks = new HashSet<int>();

        private readonly Dictionary<int, DateTime> pendingCapturedAttackStarts =
            new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime> pendingCapturedMovementTransitions =
            new Dictionary<int, DateTime>();

        private readonly Playfield playfield;

        internal NpcCombatTickCoordinator(Playfield playfield)
        {
            this.playfield = playfield;
        }

        internal void ResetCombatTick(ICharacter attacker)
        {
            CapturedEnemyCombatContract capturedContract;
            bool hasCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                attacker.Identity.Instance,
                out capturedContract)
                                       && capturedContract.IsCombatReady;
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence =
                hasCapturedContract ? capturedContract.SpecialAttackSequence : null;
            bool hasCapturedAttackStart = hasCapturedContract
                                          && capturedContract.HasCapturedAttackStartContext;
            double initialDelaySeconds = specialAttackSequence != null
                                             ? specialAttackSequence.InitialAttackDelaySeconds
                                             : Playfield.IsCapturedCleaningRobot(attacker)
                                                   ? NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds
                                                   : NpcCombatAttackRules.DefaultCombatTickSeconds;
            DateTime now = DateTime.UtcNow;
            if (hasCapturedAttackStart && capturedContract.AttackStartDelaySeconds > 0)
            {
                this.pendingCapturedAttackStarts[attacker.Identity.Instance] =
                    now + TimeSpan.FromSeconds(capturedContract.AttackStartDelaySeconds);
                if (capturedContract.HasCapturedCombatStopSequence)
                {
                    this.pendingCapturedMovementTransitions[attacker.Identity.Instance] =
                        now + TimeSpan.FromSeconds(
                            capturedContract.AttackStartDelaySeconds
                            + capturedContract.MovementTransitionDelaySeconds);
                }

                this.nextCombatTicks[attacker.Identity.Instance] =
                    now + TimeSpan.FromSeconds(
                        capturedContract.AttackStartDelaySeconds + capturedContract.FirstHitDelaySeconds);
            }
            else
            {
                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                this.nextCombatTicks[attacker.Identity.Instance] =
                    now + TimeSpan.FromSeconds(initialDelaySeconds);
            }

            this.lastNpcSpecialAttackWeaponTargets.Remove(attacker.Identity.Instance);
            this.completedCapturedOpeningAttacks.Remove(attacker.Identity.Instance);
            if (specialAttackSequence != null)
            {
                this.AnnounceCapturedSpecialAttackSequenceContext(attacker, specialAttackSequence);
            }
            else if (!Playfield.IsCapturedCleaningRobot(attacker))
            {
                if (hasCapturedAttackStart && capturedContract.AttackStartDelaySeconds <= 0)
                {
                    this.AnnounceCapturedEnemyAttackStartContext(attacker, capturedContract);
                }
            }
        }

        internal void ClearTracking(Identity identity)
        {
            this.nextCombatTicks.Remove(identity.Instance);
            this.lastNpcCombatWeaponSlots.Remove(identity.Instance);
            this.lastNpcUnarmedAttackInfoSlots.Remove(identity.Instance);
            this.lastNpcSpecialAttackWeaponTargets.Remove(identity.Instance);
            this.completedCapturedOpeningAttacks.Remove(identity.Instance);
            this.pendingCapturedAttackStarts.Remove(identity.Instance);
            this.pendingCapturedMovementTransitions.Remove(identity.Instance);
        }

        internal void ProcessCombatTick(ICharacter attacker)
        {
            if (attacker == null || this.playfield == null)
            {
                return;
            }

            if (attacker.FightingTarget.Instance == 0)
            {
                this.playfield.ClearNpcCombatTracking(attacker.Identity);
                return;
            }

            ICharacter target = this.playfield.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null
                || !target.InPlayfield(this.playfield.Identity)
                || target.Stats[StatIds.health].Value <= 0
                || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(attacker, target))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "CombatTickTargetInvalid attacker={0} target={1} found={2} inPlayfield={3} health={4}",
                        attacker.Identity,
                        attacker.FightingTarget,
                        target != null,
                        target != null && target.InPlayfield(this.playfield.Identity),
                        target == null ? 0 : target.Stats[StatIds.health].Value));
                double invalidDistance = target == null
                                             ? -1.0
                                             : Playfield.GetCombatDistance(attacker, target);
                Playfield.LogNpcBrain("Idle", "target-invalid", attacker, target, 0.0, invalidDistance);

                this.playfield.ClearInvalidNpcCombatTarget(attacker);
                return;
            }

            DateTime pendingAttackStart;
            if (this.pendingCapturedAttackStarts.TryGetValue(
                    attacker.Identity.Instance,
                    out pendingAttackStart))
            {
                if (pendingAttackStart > DateTime.UtcNow)
                {
                    return;
                }

                CapturedEnemyCombatContract pendingContract;
                if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                        attacker.Identity.Instance,
                        out pendingContract)
                    && pendingContract.IsCombatReady)
                {
                    this.AnnounceCapturedEnemyAttackStartContext(attacker, pendingContract);
                }

                this.pendingCapturedAttackStarts.Remove(attacker.Identity.Instance);
                return;
            }

            DateTime pendingMovementTransition;
            if (this.pendingCapturedMovementTransitions.TryGetValue(
                    attacker.Identity.Instance,
                    out pendingMovementTransition))
            {
                if (pendingMovementTransition > DateTime.UtcNow)
                {
                    return;
                }

                CapturedEnemyCombatContract movementContract;
                NPCController npcController = attacker.Controller as NPCController;
                if (npcController != null
                    && CapturedEnemyCombatRuntimeRegistry.TryGet(
                        attacker.Identity.Instance,
                        out movementContract)
                    && movementContract.IsCombatReady
                    && movementContract.HasCapturedCombatStopSequence)
                {
                    npcController.StopFollowForCapturedCombatRange(target.Coordinates().coordinate);
                }

                this.pendingCapturedMovementTransitions.Remove(attacker.Identity.Instance);
                return;
            }

            CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);
            CapturedEnemyCombatContract activeCapturedContract;
            bool maintainMovementDuringRecharge =
                Playfield.IsCapturedCleaningRobot(attacker)
                || PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker)
                || (CapturedEnemyCombatRuntimeRegistry.TryGet(
                        attacker.Identity.Instance,
                        out activeCapturedContract)
                    && activeCapturedContract.IsCombatReady);
            DateTime nextTick;
            DateTime now = DateTime.UtcNow;
            if (this.nextCombatTicks.TryGetValue(attacker.Identity.Instance, out nextTick)
                && nextTick > now)
            {
                if (maintainMovementDuringRecharge
                    && !this.playfield.IsInCombatRange(attacker, target, attackSource.Range))
                {
                    this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                }
                else if (maintainMovementDuringRecharge
                         && attackSource.Range <= NpcCombatAttackRules.MaxMeleeCombatDistance)
                {
                    this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
                }

                return;
            }

            if (!this.playfield.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                this.nextCombatTicks[attacker.Identity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(NpcCombatAttackRules.OutOfRangeRetrySeconds);
                return;
            }

            if (attackSource.Range <= NpcCombatAttackRules.MaxMeleeCombatDistance)
            {
                this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
            }

            int currentHealth = target.Stats[StatIds.health].Value;
            int damage = this.CalculateCombatDamage(attacker, attackSource);
            int newHealth = Math.Max(0, currentHealth - damage);
            bool killingHit = newHealth == 0;

            this.AnnounceNpcSpecialAttackWeaponContextIfNeeded(attacker, target, attackSource);
            this.AnnounceCombatDamage(
                attacker,
                target,
                damage,
                attackSource,
                attackSource.UsesEquippedWeapon
                    ? CombatDamageSource.WeaponAutoAttack
                    : CombatDamageSource.UnarmedAutoAttack);
            target.Stats[StatIds.health].Value = newHealth;
            if (attackSource.CompletesCapturedOpeningAttack)
            {
                this.completedCapturedOpeningAttacks.Add(attacker.Identity.Instance);
            }
            target.SendChangedStats();
            this.playfield.NotifyNpcCombatDamage(target);
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Combat hit attacker={0} target={1} damage={2} health={3}/{4} weaponBased={5} slot={6}",
                    attacker.Identity,
                    target.Identity,
                    damage,
                    newHealth,
                    target.Stats[StatIds.life].Value,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attackSource.AttackInfoWeaponSlot));

            if (killingHit)
            {
                if (PetCombatRules.IsPlayerOwnedMeleeCombatPet(attacker))
                {
                    this.playfield.Announce(
                        new StopFightMessage
                        {
                            Identity = attacker.Identity,
                            Unknown1 = 1
                        });
                }

                this.playfield.HandleCombatKillingHit(attacker, target);
                return;
            }

            this.nextCombatTicks[attacker.Identity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(attackSource.RechargeSeconds);
        }

        private int CalculateCombatDamage(ICharacter attacker, CombatAttackSource attackSource)
        {
            return CombatDamageRules.Calculate(
                attackSource.MinDamage,
                attackSource.MaxDamage,
                attackSource.DamageBonus,
                attacker.Stats[StatIds.level].Value,
                false);
        }

        private void AnnounceNpcSpecialAttackWeaponContextIfNeeded(
            ICharacter attacker,
            ICharacter target,
            CombatAttackSource attackSource)
        {
            int attackerInstance = attacker.Identity.Instance;
            int targetInstance = target.Identity.Instance;
            int previousTargetInstance;
            int? previousTarget = this.lastNpcSpecialAttackWeaponTargets.TryGetValue(
                                      attackerInstance,
                                      out previousTargetInstance)
                                      ? previousTargetInstance
                                      : (int?)null;
            if (!NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    Playfield.IsCapturedCleaningRobot(attacker),
                    attackSource.UsesEquippedWeapon,
                    previousTarget,
                    targetInstance)
                && !NpcCombatAttackRules.ShouldSendPlayerOwnedAttackPetAttackStartContext(
                    PetCombatRules.IsPlayerOwnedMewAttackPet(attacker)
                    || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker),
                    previousTarget,
                    targetInstance))
            {
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets[attackerInstance] = targetInstance;
            if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
            {
                this.playfield.Announce(
                    new AttackMessage
                    {
                        Identity = attacker.Identity,
                        Target = target.Identity,
                        Action = 0
                    });
                return;
            }

            if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker)
                || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker))
            {
                this.AnnouncePlayerOwnedAttackPetAttackStartContext(attacker, target);
                return;
            }

            this.playfield.Announce(
                new SpecialAttackWeaponMessage
                {
                    Identity = attacker.Identity,
                    Specials = CreateCapturedCleaningRobotSpecialAttacks(),
                    Unknown1 = NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    Unknown2 = NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    Unknown3 = NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    Unknown4 = NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponValue,
                    Unknown5 = NpcCombatAttackRules.CapturedCleaningRobotSpecialAttackWeaponLastValue
                });
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                PlayfieldLifecycleTrace.MessageSpecialAttackWeapon,
                attacker.Identity,
                "target=" + target.Identity);

            this.playfield.Announce(
                new AttackMessage
                {
                    Identity = attacker.Identity,
                    Target = target.Identity,
                    Action = 0
                });
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                PlayfieldLifecycleTrace.MessageAttack,
                attacker.Identity,
                "target=" + target.Identity);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatNpcAttackStartContextSend attacker={0} target={1} monsterData={2}",
                    attacker.Identity,
                    target.Identity,
                    attacker.Stats[StatIds.monsterdata].Value));
        }

        private void AnnouncePlayerOwnedAttackPetAttackStartContext(ICharacter attacker, ICharacter target)
        {
            this.playfield.Announce(
                new SpecialAttackWeaponMessage
                {
                    Identity = attacker.Identity,
                    Specials = CreatePlayerOwnedAttackPetSpecialAttacks(),
                    Unknown1 = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    Unknown2 = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    Unknown3 = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    Unknown4 = PetCombatRules.AttackPetSpecialAttackWeaponValue,
                    Unknown5 = 0
                });
            this.playfield.Announce(
                new AttackMessage
                {
                    Identity = attacker.Identity,
                    Target = target.Identity,
                    Action = 0
                });

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatPetAttackStartContextSend attacker={0} target={1}",
                    attacker.Identity,
                    target.Identity));
        }

        private void AnnounceCapturedSpecialAttackSequenceContext(
            ICharacter attacker,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            if (attacker.FightingTarget.Instance == 0 || specialAttackSequence == null)
            {
                return;
            }

            this.lastNpcSpecialAttackWeaponTargets[attacker.Identity.Instance] = attacker.FightingTarget.Instance;
            this.playfield.Announce(
                new SpecialAttackWeaponMessage
                {
                    Identity = attacker.Identity,
                    Specials = CreateCapturedSpecialAttacks(specialAttackSequence.SpecialAttacks),
                    Unknown1 = specialAttackSequence.SpecialAttackWeaponUnknown1,
                    Unknown2 = specialAttackSequence.SpecialAttackWeaponUnknown2,
                    Unknown3 = specialAttackSequence.SpecialAttackWeaponUnknown3,
                    Unknown4 = specialAttackSequence.SpecialAttackWeaponUnknown4,
                    Unknown5 = specialAttackSequence.SpecialAttackWeaponUnknown5
                });
            this.playfield.Announce(
                new AttackMessage
                {
                    Identity = attacker.Identity,
                    Target = attacker.FightingTarget,
                    Action = 0
                });
        }

        private void AnnounceCapturedEnemyAttackStartContext(
            ICharacter attacker,
            CapturedEnemyCombatContract capturedContract)
        {
            if (attacker == null || capturedContract == null || attacker.FightingTarget.Instance == 0)
            {
                return;
            }

            if (capturedContract.HasEmptySpecialAttackWeaponContext)
            {
                this.lastNpcSpecialAttackWeaponTargets[attacker.Identity.Instance] =
                    attacker.FightingTarget.Instance;
                this.playfield.Announce(
                    new SpecialAttackWeaponMessage
                    {
                        Identity = attacker.Identity,
                        Unknown = 0,
                        Specials = new SpecialAttack[0],
                        Unknown1 = capturedContract.SpecialAttackWeaponUnknown1,
                        Unknown2 = capturedContract.SpecialAttackWeaponUnknown2,
                        Unknown3 = capturedContract.SpecialAttackWeaponUnknown3,
                        Unknown4 = capturedContract.SpecialAttackWeaponUnknown4,
                        Unknown5 = capturedContract.SpecialAttackWeaponUnknown5
                    });
            }

            this.playfield.Announce(
                new AttackMessage
                {
                    Identity = attacker.Identity,
                    Unknown = 0,
                    Target = attacker.FightingTarget,
                    Action = 0
                });

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CombatCapturedEnemyAttackStartContextSend attacker={0} target={1}",
                    attacker.Identity,
                    attacker.FightingTarget));
        }

        private static SpecialAttack[] CreatePlayerOwnedAttackPetSpecialAttacks()
        {
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = PetCombatRules.AttackPetLeftWeaponTemplate,
                           Unknown2 = PetCombatRules.AttackPetRightWeaponTemplate,
                           Unknown3 = PetCombatRules.AttackPetLeftWeaponTag,
                           Unknown4 = PetCombatRules.AttackPetLeftWeaponName
                       },
                       new SpecialAttack
                       {
                           Unknown1 = PetCombatRules.AttackPetLeftWeaponHighTemplate,
                           Unknown2 = PetCombatRules.AttackPetRightWeaponHighTemplate,
                           Unknown3 = PetCombatRules.AttackPetRightWeaponTag,
                           Unknown4 = PetCombatRules.AttackPetRightWeaponName
                       }
                   };
        }

        private static SpecialAttack[] CreateCapturedCleaningRobotSpecialAttacks()
        {
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTemplate,
                           Unknown2 = NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTemplate,
                           Unknown3 = NpcCombatAttackRules.CapturedCleaningRobotLeftWeaponTag,
                           Unknown4 = "LIW2"
                       },
                       new SpecialAttack
                       {
                           Unknown1 = NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTemplate,
                           Unknown2 = NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTemplate,
                           Unknown3 = NpcCombatAttackRules.CapturedCleaningRobotRightWeaponTag,
                           Unknown4 = "LIW1"
                       }
                   };
        }

        private static SpecialAttack[] CreateCapturedSpecialAttacks(
            CapturedEnemySpecialAttackDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return new SpecialAttack[0];
            }

            return definitions.Select(
                definition =>
                    new SpecialAttack
                    {
                        Unknown1 = definition.LowTemplate,
                        Unknown2 = definition.HighTemplate,
                        Unknown3 = definition.Tag,
                        Unknown4 = definition.Name
                    }).ToArray();
        }

        private void AnnounceCombatDamage(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatAttackSource attackSource,
            CombatDamageSource source)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackInfoSend source={0} attacker={1} target={2} dmg={3} u2={4} u3={5} u4={6} u5={7} u6={8} weaponBased={9} atkDefault={10} atkDamageType={11} atkWeaponType={12} atkEquippedWeapons={13}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage,
                    attackSource.AttackInfoAmmoCount,
                    attackSource.AttackInfoWeaponSlot,
                    attackSource.AttackInfoUnk1,
                    attackSource.AttackInfoHitType,
                    attackSource.AttackInfoWeaponInstance,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attacker.Stats[StatIds.defaultattacktype].Value,
                    attacker.Stats[StatIds.damagetype].Value,
                    attacker.Stats[StatIds.weapontype].Value,
                    attacker.Stats[StatIds.equippedweapons].Value));

            if (attackSource.SendAttackInfo)
            {
                this.playfield.Announce(
                    new AttackInfoMessage
                    {
                        Identity = attacker.Identity,
                        Unknown = 0,
                        Target = target.Identity,
                        Unknown1 = damage,
                        Unknown2 = attackSource.AttackInfoAmmoCount,
                        Unknown3 = attackSource.AttackInfoWeaponSlot,
                        Unknown4 = attackSource.AttackInfoUnk1,
                        Unknown5 = attackSource.AttackInfoHitType,
                        Unknown6 = attackSource.AttackInfoWeaponInstance
                    });
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo,
                    PlayfieldLifecycleTrace.MessageAttackInfo,
                    attacker.Identity,
                    "target=" + target.Identity);
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatAttackInfoSkip source={0} attacker={1} target={2} dmg={3} reason=no_captured_or_equipped_context",
                        source,
                        attacker.Identity,
                        target.Identity,
                        damage));
            }

            this.AnnounceHealthDamageIfNeeded(attacker, target, damage, source);
        }

        private void AnnounceHealthDamageIfNeeded(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatDamageSource source)
        {
            if (!ShouldSendHealthDamage(source))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatHealthDamageSkip source={0} attacker={1} target={2} dmg={3}",
                        source,
                        attacker.Identity,
                        target.Identity,
                        damage));
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatHealthDamageSend source={0} attacker={1} target={2} dmg={3}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage));

            this.playfield.Announce(
                new HealthDamageMessage
                {
                    Identity = attacker.Identity,
                    Unknown1 = damage,
                    Unknown2 = 0,
                    Unknown3 = 0,
                    Unknown4 = 0,
                    Target = target.Identity,
                    Unknown5 = 0
                });
        }

        private static bool ShouldSendHealthDamage(CombatDamageSource source)
        {
            return source != CombatDamageSource.WeaponAutoAttack
                   && source != CombatDamageSource.UnarmedAutoAttack;
        }

        private CombatAttackSource GetCombatAttackSource(ICharacter attacker)
        {
            if (PetBureaucratGuardianAppearance.IsGuardianPet(attacker))
            {
                int rawMinDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0);
                int rawMaxDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0);
                int fallbackMinDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(
                    attacker.Stats[StatIds.level].Value);
                int fallbackMaxDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(
                    attacker.Stats[StatIds.level].Value);
                int petMinDamage = rawMinDamage > 0
                                       ? rawMinDamage
                                       : (rawMaxDamage > 0 ? rawMaxDamage : fallbackMinDamage);
                int petMaxDamage = rawMaxDamage > 0
                                       ? rawMaxDamage
                                       : (rawMinDamage > 0 ? rawMinDamage : fallbackMaxDamage);

                return new CombatAttackSource
                       {
                           MinDamage = petMinDamage,
                           MaxDamage = petMaxDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = PetCombatRules.AttackPetRechargeSeconds,
                           UsesEquippedWeapon = true,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = (int)WeaponSlots.Righthand,
                           AttackInfoUnk1 = 4,
                           AttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = 0,
                           SendAttackInfo = true
                       };
            }

            if (PetCombatRules.IsPlayerOwnedMewAttackPet(attacker)
                || PetCombatRules.IsPlayerOwnedBureaucratCompanionPet(attacker))
            {
                int rawMinDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0);
                int rawMaxDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0);
                int fallbackMinDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(
                    attacker.Stats[StatIds.level].Value);
                int fallbackMaxDamage = PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(
                    attacker.Stats[StatIds.level].Value);
                int petMinDamage = rawMinDamage > 0
                                       ? rawMinDamage
                                       : (rawMaxDamage > 0 ? rawMaxDamage : fallbackMinDamage);
                int petMaxDamage = rawMaxDamage > 0
                                       ? rawMaxDamage
                                       : (rawMinDamage > 0 ? rawMinDamage : fallbackMaxDamage);
                int attackInfoWeaponInstance = this.GetAttackPetAttackInfoWeaponInstance(attacker);

                return new CombatAttackSource
                       {
                           MinDamage = petMinDamage,
                           MaxDamage = petMaxDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = PetCombatRules.AttackPetRechargeSeconds,
                           UsesEquippedWeapon = true,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = PetCombatRules.AttackPetAttackInfoWeaponSlot,
                           AttackInfoUnk1 = PetCombatRules.AttackPetAttackInfoUnk1,
                           AttackInfoHitType = PetCombatRules.AttackPetAttackInfoHitType,
                           AttackInfoWeaponInstance = attackInfoWeaponInstance,
                           SendAttackInfo = true
                       };
            }

            CapturedEnemyCombatContract capturedContract;
            bool hasCapturedContract = CapturedEnemyCombatRuntimeRegistry.TryGet(
                                           attacker.Identity.Instance,
                                           out capturedContract)
                                       && capturedContract.IsCombatReady;
            if (hasCapturedContract
                && capturedContract.AttackModel == CapturedEnemyAttackModel.Specialized
                && capturedContract.SpecialAttackSequence != null)
            {
                CapturedEnemySpecialAttackSequenceDefinition sequence =
                    capturedContract.SpecialAttackSequence;
                bool openingAttackCompleted = sequence.OpeningAttack == null
                                              || this.completedCapturedOpeningAttacks.Contains(
                                                  attacker.Identity.Instance);
                CapturedEnemyCombatAttackDefinition attack = openingAttackCompleted
                                                                  ? sequence.RepeatingAttack
                                                                  : sequence.OpeningAttack;
                return new CombatAttackSource
                       {
                           MinDamage = attack.MinDamage,
                           MaxDamage = attack.MaxDamage,
                           DamageBonus = attack.DamageBonus,
                           Range = attack.Range,
                           RechargeSeconds = attack.RechargeSeconds,
                           UsesEquippedWeapon = attack.UsesEquippedWeapon,
                           AttackInfoAmmoCount = attack.AttackInfoAmmoCount,
                           AttackInfoWeaponSlot = attack.AttackInfoWeaponSlot,
                           AttackInfoUnk1 = attack.AttackInfoUnknown,
                           AttackInfoHitType = attack.AttackInfoHitType,
                           AttackInfoWeaponInstance = attack.AttackInfoWeaponInstance,
                           SendAttackInfo = attack.SendAttackInfo,
                           CompletesCapturedOpeningAttack = !openingAttackCompleted
                       };
            }

            if (hasCapturedContract
                && capturedContract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            {
                return new CombatAttackSource
                       {
                           MinDamage = capturedContract.MinDamage,
                           MaxDamage = capturedContract.MaxDamage,
                           DamageBonus = 0,
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = capturedContract.RechargeSeconds > 0
                                                 ? capturedContract.RechargeSeconds
                                                 : NpcCombatAttackRules.DefaultCombatTickSeconds,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = capturedContract.AttackInfoWeaponSlot,
                           AttackInfoUnk1 = capturedContract.AttackInfoUnknown,
                           AttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = capturedContract.AttackInfoWeaponInstance,
                           SendAttackInfo = true
                       };
            }

            EquippedCombatWeapon equippedWeapon = this.GetEquippedCombatWeapon(attacker);
            if (equippedWeapon == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatAttackSource unarmed attacker={0} mindmg={1} maxdmg={2} bonus={3} defaultattack={4} damagetype={5} weapontype={6} equippedweapons={7}",
                        attacker.Identity,
                        attacker.Stats[StatIds.mindamage].Value,
                        attacker.Stats[StatIds.maxdamage].Value,
                        attacker.Stats[StatIds.damagebonus].Value,
                        attacker.Stats[StatIds.defaultattacktype].Value,
                        attacker.Stats[StatIds.damagetype].Value,
                        attacker.Stats[StatIds.weapontype].Value,
                        attacker.Stats[StatIds.equippedweapons].Value));
                int attackInfoWeaponSlot = this.GetUnarmedAttackInfoWeaponSlot(attacker);
                int attackInfoDamage = this.GetUnarmedAttackDamage(attacker, attackInfoWeaponSlot);
                return new CombatAttackSource
                       {
                           MinDamage = attackInfoDamage,
                           MaxDamage = attackInfoDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = NpcCombatAttackRules.MaxMeleeCombatDistance,
                           RechargeSeconds = Playfield.IsCapturedCleaningRobot(attacker)
                                                 ? NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds
                                                 : NpcCombatAttackRules.DefaultCombatTickSeconds,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = attackInfoWeaponSlot,
                           AttackInfoUnk1 = 0,
                           AttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = this.GetUnarmedAttackInfoWeaponInstance(attacker),
                           SendAttackInfo = Playfield.IsCapturedCleaningRobot(attacker)
                        };
            }

            IItem weapon = equippedWeapon.Item;
            bool hasCapturedEquippedAttackInfo = capturedContract != null
                                                  && capturedContract.HasCapturedEquippedAttackInfo;
            int minDamage = hasCapturedEquippedAttackInfo && capturedContract.MinDamage > 0
                                ? capturedContract.MinDamage
                                : NormalizeCombatItemStat(weapon.GetAttribute((int)StatIds.mindamage), 0);
            int maxDamage = hasCapturedEquippedAttackInfo && capturedContract.MaxDamage > 0
                                ? capturedContract.MaxDamage
                                : NormalizeCombatItemStat(weapon.GetAttribute((int)StatIds.maxdamage), 0);
            int damageBonus = hasCapturedEquippedAttackInfo
                                  ? 0
                                  : NormalizeCombatItemStat(
                                      weapon.GetAttribute((int)StatIds.damagebonus),
                                      0);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackSource weapon attacker={0} item={1}/{2} slot={3} min={4} max={5} damageBonus={6} rangeRaw={7}",
                    attacker.Identity,
                    weapon.LowID,
                    weapon.HighID,
                    equippedWeapon.Slot,
                    minDamage,
                    maxDamage,
                    damageBonus,
                    weapon.GetAttribute((int)StatIds.attackrange)));

            return new CombatAttackSource
                   {
                       MinDamage = minDamage,
                       MaxDamage = maxDamage,
                       DamageBonus = 0,
                       Range = NormalizeCombatRange(weapon.GetAttribute((int)StatIds.attackrange)),
                       RechargeSeconds = hasCapturedEquippedAttackInfo
                                             && capturedContract.RechargeSeconds > 0
                                             ? capturedContract.RechargeSeconds
                                             : NormalizeCombatDelaySeconds(
                                                 weapon.GetAttribute((int)StatIds.itemdelay),
                                                 weapon.GetAttribute((int)StatIds.rechargedelay)),
                       UsesEquippedWeapon = true,
                       AttackInfoAmmoCount = hasCapturedEquippedAttackInfo
                                                 ? capturedContract.AttackInfoAmmoCount
                                                 : 40,
                       AttackInfoWeaponSlot = hasCapturedEquippedAttackInfo
                                                  ? capturedContract.AttackInfoWeaponSlot
                                                  : equippedWeapon.Slot,
                       AttackInfoUnk1 = hasCapturedEquippedAttackInfo
                                            ? capturedContract.AttackInfoUnknown
                                            : 4,
                       AttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType,
                       AttackInfoWeaponInstance = hasCapturedEquippedAttackInfo
                                                      ? capturedContract.AttackInfoWeaponInstance
                                                      : 0,
                       SendAttackInfo = true
                    };
        }

        private int GetAttackPetAttackInfoWeaponInstance(ICharacter attacker)
        {
            int attackerInstance = attacker.Identity.Instance;
            int lastSlot;
            if (this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attackerInstance, out lastSlot)
                && lastSlot == PetCombatRules.AttackPetAttackInfoWeaponSlot)
            {
                this.lastNpcUnarmedAttackInfoSlots[attackerInstance] =
                    NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
                return PetCombatRules.AttackPetRightWeaponTag;
            }

            this.lastNpcUnarmedAttackInfoSlots[attackerInstance] = PetCombatRules.AttackPetAttackInfoWeaponSlot;
            return PetCombatRules.AttackPetLeftWeaponTag;
        }

        private int GetUnarmedAttackInfoWeaponSlot(ICharacter attacker)
        {
            int lastSlot;
            if (this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attacker.Identity.Instance, out lastSlot)
                && lastSlot == NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot)
            {
                this.lastNpcUnarmedAttackInfoSlots[attacker.Identity.Instance] =
                    NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot;
                return NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot;
            }

            this.lastNpcUnarmedAttackInfoSlots[attacker.Identity.Instance] =
                NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
            return NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot;
        }

        private int GetUnarmedAttackDamage(ICharacter attacker, int attackInfoWeaponSlot)
        {
            if (Playfield.IsCapturedCleaningRobot(attacker))
            {
                return attackInfoWeaponSlot == NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponSlot
                           ? NpcCombatAttackRules.CapturedCleaningRobotLeftHandDamage
                           : NpcCombatAttackRules.CapturedCleaningRobotRightHandDamage;
            }

            return Math.Max(
                NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0),
                NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0));
        }

        private int GetUnarmedAttackInfoWeaponInstance(ICharacter attacker)
        {
            int slot;
            if (!this.lastNpcUnarmedAttackInfoSlots.TryGetValue(attacker.Identity.Instance, out slot)
                || slot == NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot)
            {
                return NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponInstance;
            }

            return NpcCombatAttackRules.NpcUnarmedLeftAttackInfoWeaponInstance;
        }

        private EquippedCombatWeapon GetEquippedCombatWeapon(ICharacter attacker)
        {
            if (attacker.BaseInventory == null
                || !attacker.BaseInventory.Pages.ContainsKey((int)IdentityType.WeaponPage))
            {
                this.lastNpcCombatWeaponSlots.Remove(attacker.Identity.Instance);
                return null;
            }

            IInventoryPage weaponPage = attacker.BaseInventory.Pages[(int)IdentityType.WeaponPage];
            IItem rightHand = weaponPage[(int)WeaponSlots.Righthand];
            IItem leftHand = weaponPage[(int)WeaponSlots.LeftHand];
            bool rightHandUsable = IsWieldableCombatWeapon(rightHand);
            bool leftHandUsable = IsWieldableCombatWeapon(leftHand);

            if (rightHandUsable && leftHandUsable)
            {
                int attackerInstance = attacker.Identity.Instance;
                int lastSlot;
                if (this.lastNpcCombatWeaponSlots.TryGetValue(attackerInstance, out lastSlot)
                    && lastSlot == (int)WeaponSlots.Righthand)
                {
                    this.lastNpcCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.LeftHand;
                    return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
                }

                this.lastNpcCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (rightHandUsable)
            {
                this.lastNpcCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (leftHandUsable)
            {
                this.lastNpcCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.LeftHand;
                return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
            }

            this.lastNpcCombatWeaponSlots.Remove(attacker.Identity.Instance);
            return null;
        }

        private static int NormalizeCombatItemStat(int value, int fallback)
        {
            return value == MissingItemStatValue ? fallback : value;
        }

        private static bool IsWieldableCombatWeapon(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.ItemActions != null && item.ItemActions.Any(x => x.ActionType == ActionType.ToWield))
            {
                return true;
            }

            return NormalizeCombatItemStat(item.GetAttribute((int)StatIds.mindamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.maxdamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.attackrange), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.itemdelay), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.rechargedelay), 0) > 0;
        }

        private static double NormalizeCombatRange(int range)
        {
            int normalizedRange = NormalizeCombatItemStat(range, 0);
            if (normalizedRange <= 0)
            {
                return NpcCombatAttackRules.MaxMeleeCombatDistance;
            }

            return normalizedRange > 1000 ? normalizedRange / 100.0 : normalizedRange;
        }

        private static double NormalizeCombatDelaySeconds(int attackDelay, int rechargeDelay)
        {
            int normalizedAttackDelay = NormalizeCombatItemStat(attackDelay, 0);
            int normalizedRechargeDelay = NormalizeCombatItemStat(rechargeDelay, 0);
            int totalCentiseconds = normalizedAttackDelay + normalizedRechargeDelay;

            if (totalCentiseconds <= 0)
            {
                return NpcCombatAttackRules.DefaultCombatTickSeconds;
            }

            return Math.Max(0.25, totalCentiseconds / 100.0);
        }

        private sealed class CombatAttackSource
        {
            public int MinDamage { get; set; }

            public int MaxDamage { get; set; }

            public int DamageBonus { get; set; }

            public double Range { get; set; }

            public double RechargeSeconds { get; set; }

            public bool UsesEquippedWeapon { get; set; }

            public int AttackInfoAmmoCount { get; set; }

            public int AttackInfoWeaponSlot { get; set; }

            public int AttackInfoUnk1 { get; set; }

            public int AttackInfoHitType { get; set; }

            public int AttackInfoWeaponInstance { get; set; }

            public bool SendAttackInfo { get; set; }

            public bool CompletesCapturedOpeningAttack { get; set; }
        }

        private enum CombatDamageSource
        {
            WeaponAutoAttack,
            UnarmedAutoAttack,
            DamageOverTime,
            HealOverTime,
            Nano,
            Environment
        }

        private sealed class EquippedCombatWeapon
        {
            public IItem Item { get; set; }

            public int Slot { get; set; }
        }
    }
}
