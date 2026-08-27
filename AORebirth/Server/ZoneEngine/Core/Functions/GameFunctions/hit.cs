#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// FunctionType.Hit — adjust a.stat on the function target (heal/damage).
    /// Hit args are: Stat, Min, Max [, DamageType/AC ].
    /// Some nanos store collapsed Min==Max as Stat, Amount, ACType (3 args) — the third value
    /// is AC type when it is positive while Amount is negative, not a damage maximum.
    /// </summary>
    internal class hit : FunctionPrototype
    {
        private const FunctionType functionId = FunctionType.Hit;

        private const int UnarmedAttackInfoAmmoCount = -1;

        private const int AttackInfoWeaponSlot = 0;

        private const int AttackInfoUnk1 = 4;

        private const int AttackInfoHitType = 1;

        public override FunctionType FunctionId
        {
            get
            {
                return functionId;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            if (target == null)
            {
                return false;
            }

            lock (target)
            {
                return this.FunctionExecute(self, caller, target, arguments);
            }
        }

        public bool FunctionExecute(
            INamedEntity Self,
            IEntity Caller,
            IInstancedEntity Target,
            MessagePackObject[] Arguments)
        {
            if (Arguments == null || Arguments.Length < 2)
            {
                return false;
            }

            Character affected = Target as Character;
            if (affected == null)
            {
                return false;
            }

            Character source = Self as Character;
            if (source == null)
            {
                source = Caller as Character;
            }

            int statNumber = Arguments[0].AsInt32();
            int delta = ResolveHitDelta(Arguments);

            if (statNumber == (int)StatIds.health)
            {
                return ApplyHealthDelta(source, affected, delta);
            }

            if (statNumber == (int)StatIds.currentnano || statNumber == (int)StatIds.nanoenergypool)
            {
                return ApplyNanoDelta(affected, delta);
            }

            affected.Stats[statNumber].Value += delta;
            SendStats(affected);
            return true;
        }

        internal static int ResolveHitDelta(MessagePackObject[] arguments)
        {
            int minHit = arguments[1].AsInt32();
            int maxHit = minHit;

            if (arguments.Length >= 3)
            {
                maxHit = arguments[2].AsInt32();

                // Stat, Amount, ACType — AC type is a positive type id, not a max roll.
                if (arguments.Length == 3 && minHit < 0 && maxHit > 0)
                {
                    maxHit = minHit;
                }
                else if (arguments.Length >= 4 && minHit < 0 && maxHit > 0)
                {
                    // Stat, Min, Max, ACType with Max accidentally positive: prefer Min.
                    maxHit = minHit;
                }
            }

            if (minHit > maxHit)
            {
                int swap = minHit;
                minHit = maxHit;
                maxHit = swap;
            }

            return minHit == maxHit
                ? minHit
                : new Random().Next(minHit, maxHit + 1);
        }

        private static bool ApplyHealthDelta(Character source, Character affected, int delta)
        {
            int maxLife = Math.Max(1, affected.Stats[StatIds.life].Value);
            int current = affected.Stats[StatIds.health].Value;

            if (delta >= 0)
            {
                int room = Math.Max(0, maxLife - current);
                int applied = Math.Min(delta, room);
                if (applied <= 0)
                {
                    return true;
                }

                affected.Stats[StatIds.health].Value = current + applied;
                SendStats(affected);
                AnnounceHeal(source, affected, applied);
                return true;
            }

            CapturedEnemyCombatContract capturedContract;
            if (source != null
                && CapturedEnemyCombatRuntimeRegistry.TryGet(
                    source.Identity.Instance,
                    out capturedContract))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "CapturedEnemyCombatFunctionHitQuarantined source=" + source.Identity
                    + " target=" + affected.Identity
                    + " reason=FunctionType.Hit packet/chat semantics are not part of the proven combat contract"
                    + " evidence=" + (capturedContract == null ? string.Empty : capturedContract.Evidence));
                return true;
            }

            int newHealth = Math.Max(0, current + delta);
            int actualDamage = current - newHealth;
            affected.Stats[StatIds.health].Value = newHealth;
            SendStats(affected);

            if (actualDamage <= 0 || source == null || affected.Playfield == null)
            {
                return true;
            }

            Playfield playfield = affected.Playfield as Playfield;
            if (playfield == null)
            {
                return true;
            }

            playfield.Announce(
                new AttackInfoMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    Target = affected.Identity,
                    Unknown1 = actualDamage,
                    Unknown2 = UnarmedAttackInfoAmmoCount,
                    Unknown3 = AttackInfoWeaponSlot,
                    Unknown4 = AttackInfoUnk1,
                    Unknown5 = AttackInfoHitType,
                    Unknown6 = 0
                });

            if (source.Controller != null && source.Controller.Client != null)
            {
                ChatTextMessageHandler.Default.Send(
                    source,
                    string.Format(
                        "You hit {0} for {1} points of energy damage.",
                        string.IsNullOrWhiteSpace(affected.Name) ? "target" : affected.Name,
                        actualDamage));
            }

            // Incoming hit text: AttackInfo alone fills Combat chat; ChatText would also flood General.

            if (source.Identity != affected.Identity)
            {
                playfield.AcquireNpcAggro(source, affected);
                playfield.SuspendNpcRegen(affected);
            }

            if (newHealth == 0)
            {
                playfield.HandleCombatKillingHit(source, affected);
            }

            return true;
        }

        private static bool ApplyNanoDelta(Character affected, int delta)
        {
            int maxNano = Math.Max(0, affected.Stats[StatIds.maxnanoenergy].Value);
            int current = affected.Stats[StatIds.currentnano].Value;

            if (delta >= 0)
            {
                int room = Math.Max(0, maxNano - current);
                int applied = Math.Min(delta, room);
                if (applied <= 0)
                {
                    return true;
                }

                affected.Stats[StatIds.currentnano].Value = current + applied;
            }
            else
            {
                affected.Stats[StatIds.currentnano].Value = Math.Max(0, current + delta);
            }

            SendStats(affected);
            return true;
        }

        private static void AnnounceHeal(Character source, Character affected, int healAmount)
        {
            if (healAmount <= 0)
            {
                return;
            }

            if (source != null && source.Controller != null && source.Controller.Client != null)
            {
                string targetName = source.Identity == affected.Identity
                    ? "yourself"
                    : (string.IsNullOrWhiteSpace(affected.Name) ? "target" : affected.Name);
                ChatTextMessageHandler.Default.Send(
                    source,
                    string.Format("You healed {0} for {1} points.", targetName, healAmount));
            }

            SendStats(affected);
        }

        private static void SendStats(Character character)
        {
            if (character.Controller != null)
            {
                character.Controller.SendChangedStats();
                return;
            }

            StatMessageHandler.Default.SendChanged(character);
        }
    }
}
