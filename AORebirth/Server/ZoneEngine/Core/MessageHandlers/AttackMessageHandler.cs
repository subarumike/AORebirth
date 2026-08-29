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
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields.OfficialPlacements;

    #endregion

    /// <summary>
    /// Handles the client's basic Q attack toggle.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class AttackMessageHandler : BaseMessageHandler<AttackMessage, AttackMessageHandler>
    {
        private const int SimpleCharFullUpdateIsImmuneFlag = 0x00800000;

        // Capture 20260719-fling-burst (ranged Fling/Burst combat start trailer).
        private const int RangedCombatStartSpecialAttackUnknown1 = -53;
        private const int RangedCombatStartSpecialAttackUnknown2 = 1306;
        private const int RangedCombatStartSpecialAttackUnknown3 = -53;
        private const int RangedCombatStartSpecialAttackUnknown4 = 2439;
        private const int RangedCombatStartSpecialAttackUnknown5 = -100;

        // Capture 20260724-001643 melee MA combat-start SpecialAttackWeapon trailer.
        private const int CombatStartSpecialAttackUnknown1 = 61;
        private const int CombatStartSpecialAttackUnknown2 = -166;
        private const int CombatStartSpecialAttackUnknown3 = 658;
        private const int CombatStartSpecialAttackUnknown4 = 969;
        private const int CombatStartSpecialAttackUnknown5 = -100;

        protected override void Read(AttackMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            ICharacter target = Pool.Instance.GetObject<ICharacter>(character.Playfield.Identity, message.Target);

            client.Server.Debug(
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

            if (AcgDevelopmentPlaceholderRuntimeRegistry.IsPlaceholder(
                    target.Identity.Instance))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored for development ACG placeholder.");
                return;
            }

            string missionIsolationFailure;
            if (!ZoneEngine.Core.Missions.MissionAcgOperationalRuntime.TryValidateCombatTarget(
                character,
                target,
                out missionIsolationFailure))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored: mission instance ownership mismatch.");
                return;
            }

            if (!ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.TryValidateCombatPair(
                character,
                target,
                out missionIsolationFailure))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored: mission spatial ownership mismatch.");
                return;
            }

            if (ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target) || IsImmuneTarget(target))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored for non-attackable target.");
                return;
            }

            if (!PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(character, target))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored: suppression gas / PvP flag rules.");
                return;
            }

            // Sparrow Flight RestrictAction 2 — cannot fight while morphed.
            if (AdventurerMorphFlightRuntime.IsFightingRestricted(character))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored: morph RestrictAction (no fighting).");
                return;
            }

            Playfield attackPlayfield = character.Playfield as Playfield;
            if (attackPlayfield != null && !attackPlayfield.IsPlayerAttackInRange(character, target))
            {
                this.CancelPlayerAttack(character);
                this.SendAttackState(character, Identity.None, 0);
                client.Server.Info(client, "Attack ignored: out of weapon range.");
                return;
            }

            this.StartPlayerAttack(character, message.Target);
            this.EngageNpcTarget(character, target);
            this.SendCombatStartSpecialAttackWeapon(character);
            this.SendAttackState(character, message.Target, message.Action);
            // First swing only after SAW+Attack so the client plays the attack anim.
            this.TryPlayerFirstCombatTick(character);
            PetCommandService.OnOwnerEngagedCombat(character, message.Target);
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

        private void TryPlayerFirstCombatTick(ICharacter character)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.TryPlayerFirstCombatTick(character);
            }
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
            bool rangedSpecials = WeaponSupportsRangedSpecials(character);
            var message = new SpecialAttackWeaponMessage
                          {
                              Identity = character.Identity,
                              Specials = CreateDefaultPlayerSpecialAttacks(),
                              Unknown1 = rangedSpecials
                                             ? RangedCombatStartSpecialAttackUnknown1
                                             : CombatStartSpecialAttackUnknown1,
                              Unknown2 = rangedSpecials
                                             ? RangedCombatStartSpecialAttackUnknown2
                                             : CombatStartSpecialAttackUnknown2,
                              Unknown3 = rangedSpecials
                                             ? RangedCombatStartSpecialAttackUnknown3
                                             : CombatStartSpecialAttackUnknown3,
                              Unknown4 = rangedSpecials
                                             ? RangedCombatStartSpecialAttackUnknown4
                                             : CombatStartSpecialAttackUnknown4,
                              Unknown5 = rangedSpecials
                                             ? RangedCombatStartSpecialAttackUnknown5
                                             : CombatStartSpecialAttackUnknown5
                          };

            CombatStartPacketDiagnostics.LogOutbound(
                "AttackMessageHandler.SendCombatStartSpecialAttackWeapon",
                message,
                Identity.None);
            character.Playfield.Announce(message);
        }

        private static bool WeaponSupportsRangedSpecials(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return false;
            }

            IItem right = weaponPage[(int)WeaponSlots.Righthand];
            IItem left = weaponPage[(int)WeaponSlots.LeftHand];
            return ItemSupportsRangedSpecial(right) || ItemSupportsRangedSpecial(left);
        }

        private static bool ItemSupportsRangedSpecial(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            int can = item.GetAttribute((int)StatIds.can);
            return ((can & (int)CanFlags.FlingShot) != 0) || ((can & (int)CanFlags.Burst) != 0);
        }

        private static SpecialAttack[] CreateDefaultPlayerSpecialAttacks()
        {
            // Capture 20260724-001643 SAW: MAAT 211357/211358, DIIT 42033/42032, BRAW 211401/211402.
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = 0x0003399D,
                           Unknown2 = 0x0003399E,
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
                           Unknown1 = 0x000339C9,
                           Unknown2 = 0x000339CA,
                           Unknown3 = 0x0000008E,
                           Unknown4 = "BRAW"
                       }
                   };
        }
    }
}
