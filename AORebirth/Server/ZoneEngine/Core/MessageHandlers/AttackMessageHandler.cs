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

            if (target == null)
            {
                character.SetFightingTarget(Identity.None);
                this.ResetCombatTick(character);
                this.SendAttackState(character, Identity.None, 0);
                return;
            }

            if (ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target) || IsImmuneTarget(target))
            {
                character.SetFightingTarget(Identity.None);
                this.ResetCombatTick(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored for non-attackable target.");
                return;
            }

            character.SetTarget(message.Target);
            if (!this.CanReachTarget(character, target))
            {
                character.SetFightingTarget(Identity.None);
                this.ResetCombatTick(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored because target is out of range.");
                return;
            }

            character.SetFightingTarget(message.Target);
            this.ResetCombatTick(character);
            this.EngageNpcTarget(character, target);
            this.SendAttackState(character, message.Target, message.Action);
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

        private bool CanReachTarget(ICharacter character, ICharacter target)
        {
            Playfield playfield = character.Playfield as Playfield;
            return playfield == null || playfield.CanReachCombatTarget(character, target);
        }

        private void EngageNpcTarget(ICharacter character, ICharacter target)
        {
            NPCController npcController = target.Controller as NPCController;
            if (npcController == null
                || npcController.KnuBot != null
                || !NpcAiProfiles.CanRetaliate(npcController.AiProfile)
                || target.Stats[StatIds.health].Value <= 0
                || target.FightingTarget.Instance != 0)
            {
                return;
            }

            target.SetTarget(character.Identity);
            target.SetFightingTarget(character.Identity);
            this.ResetCombatTick(target);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "NPC combat engaged attacker={0} npc={1}",
                    character.Identity.ToString(true),
                    target.Identity.ToString(true)));
        }

        private void SendAttackState(ICharacter character, Identity target, byte action)
        {
            this.SendToPlayfield(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Target = target;
                    x.Action = action;
                });
        }
    }
}
