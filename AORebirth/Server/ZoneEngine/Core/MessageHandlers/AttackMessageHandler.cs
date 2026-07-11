#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Handles the client's basic Q attack toggle.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class AttackMessageHandler : BaseMessageHandler<AttackMessage, AttackMessageHandler>
    {
        private const int SimpleCharFullUpdateIsImmuneFlag = 0x00800000;
        private const int CombatStartSpecialAttackUnknown1 = 13;
        private const int CombatStartSpecialAttackUnknown2 = 25;
        private const int CombatStartSpecialAttackUnknown3 = 13;
        private const int CombatStartSpecialAttackUnknown4 = 33;
        private const int CombatStartSpecialAttackUnknown5 = 100;

        protected override void Read(AttackMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            ICharacter target = Pool.Instance.GetObject<ICharacter>(character.Playfield.Identity, message.Target);

            client.Server.Info(
                client,
                "Attack action={0} target={1} targetFound={2} targetHealth={3}",
                message.Action,
                message.Target,
                target != null,
                target == null ? 0 : target.Stats[StatIds.health].Value);
            CombatStartPacketDiagnostics.LogAttackCommand(character, message.Target, message.Action, target);

            if (target == null)
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                return;
            }

            if (ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target) || IsImmuneTarget(target))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored for non-attackable target.");
                return;
            }

            this.StartPlayerAttack(character, message.Target);
            this.EngageNpcTarget(character, target);
            this.SendCombatStartSpecialAttackWeapon(character);
            this.SendAttackState(character, message.Target, message.Action);
        }

        private void StartPlayerAttack(ICharacter character, Identity target)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.StartPlayerAttack(character, target);
                return;
            }

            character.SetTarget(target);
            character.SetFightingTarget(target);
            this.ResetCombatTick(character);
        }

        private void CancelPlayerAttack(ICharacter character)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.CancelPlayerAttack(character);
                return;
            }

            character.SetFightingTarget(Identity.None);
            this.ResetCombatTick(character);
        }

        private void ResetCombatTick(ICharacter character)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.ResetCombatTick(character.Identity);
            }
        }

        private static bool IsImmuneTarget(ICharacter target)
        {
            return target != null
                   && (target.Stats[StatIds.flags].Value & SimpleCharFullUpdateIsImmuneFlag)
                   == SimpleCharFullUpdateIsImmuneFlag;
        }

        private void EngageNpcTarget(ICharacter character, ICharacter target)
        {
            Playfield playfield = target.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.AcquireNpcAggro(character, target);
            }
        }

        private void SendAttackState(ICharacter character, Identity target, byte action)
        {
            CombatStartPacketDiagnostics.LogOutbound(
                "AttackMessageHandler.SendAttackState",
                new AttackMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Target = target,
                    Action = action
                },
                Identity.None);

            this.SendToPlayfield(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Target = target;
                    x.Action = action;
                });
        }

        private void SendCombatStartSpecialAttackWeapon(ICharacter character)
        {
            var message = new SpecialAttackWeaponMessage
                          {
                              Identity = character.Identity,
                              Specials = CreateDefaultPlayerSpecialAttacks(),
                              Unknown1 = CombatStartSpecialAttackUnknown1,
                              Unknown2 = CombatStartSpecialAttackUnknown2,
                              Unknown3 = CombatStartSpecialAttackUnknown3,
                              Unknown4 = CombatStartSpecialAttackUnknown4,
                              Unknown5 = CombatStartSpecialAttackUnknown5
                          };

            CombatStartPacketDiagnostics.LogOutbound(
                "AttackMessageHandler.SendCombatStartSpecialAttackWeapon",
                message,
                Identity.None);
            character.Playfield.Announce(message);
        }

        private static SpecialAttack[] CreateDefaultPlayerSpecialAttacks()
        {
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = 0x0000AAC0,
                           Unknown2 = 0x00023569,
                           Unknown3 = 0x00000064,
                           Unknown4 = "MAAT"
                       },
                       new SpecialAttack
                       {
                           Unknown1 = 0x0000A431,
                           Unknown2 = 0x0000A430,
                           Unknown3 = 0x00000090,
                           Unknown4 = "DIIT"
                       },
                       new SpecialAttack
                       {
                           Unknown1 = 0x00011294,
                           Unknown2 = 0x00011295,
                           Unknown3 = 0x0000008E,
                           Unknown4 = "BRAW"
                       }
                   };
        }
    }
}
