namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Combat;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Interfaces;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    internal sealed class PlayfieldCharacterCombatSubscriptions
    {
        private readonly Playfield playfield;

        private readonly Dictionary<int, Character> subscribed = new Dictionary<int, Character>();

        internal PlayfieldCharacterCombatSubscriptions(Playfield playfield)
        {
            this.playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
        }

        internal void Register(ICharacter character)
        {
            Character c = character as Character;
            if (c == null || this.subscribed.ContainsKey(c.Identity.Instance))
            {
                return;
            }

            c.Damaged += this.OnCharacterDamaged;
            c.Died += this.OnCharacterDied;
            this.subscribed[c.Identity.Instance] = c;
        }

        internal void Unregister(ICharacter character)
        {
            Character c = character as Character;
            if (c == null)
            {
                return;
            }

            c.Damaged -= this.OnCharacterDamaged;
            c.Died -= this.OnCharacterDied;
            this.subscribed.Remove(c.Identity.Instance);
        }

        private void OnCharacterDamaged(object sender, CharacterDamagedEventArgs e)
        {
            if (e == null || e.Target == null)
            {
                return;
            }

            ICharacter attacker = e.Attacker;
            ICharacter target = e.Target;

            MissionAcgOperationalRuntime.NotifyHealthChanged(target, e.NewHealth);

            if (target.Controller is NPCController)
            {
                this.playfield.NotifyNpcCombatDamage(target);
            }

            if (!e.KillingHit && target.Controller is NPCController && attacker != null)
            {
                this.playfield.AcquireNpcAggro(attacker, target);
                this.playfield.SuspendNpcRegen(target);
            }
        }

        private void OnCharacterDied(object sender, CharacterDeathEventArgs e)
        {
            if (e == null || e.Victim == null)
            {
                return;
            }

            this.playfield.HandleCombatKillingHit(e.Killer, e.Victim);
        }
    }
}
