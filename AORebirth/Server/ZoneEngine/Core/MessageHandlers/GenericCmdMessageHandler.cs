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

    // TODO: Make this to EntityEnvent or something like this
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class GenericCmdMessageHandler : BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>
    {
        private static readonly TimeSpan CorpseUseAcknowledgeDelay = TimeSpan.FromMilliseconds(550);

        private static readonly TimeSpan SurgeryClinicSpecialAvailableDelay = TimeSpan.FromMilliseconds(3500);

        private static readonly ISet<int> CapturedSurgeryClinicTemplateIds =
            new HashSet<int>
            {
                43553,
                295742
            };

        private const int SurgeryClinicNanoId = 0x26732;

        private const int SurgeryClinicNanoDuration = 90000;

        private const int SurgeryClinicSpecialStatId = 124;

        private const int SurgeryClinicSpecialLockSeconds = 5;

        private const int SurgeryClinicCreditCost = 300;

        private const string SurgeryClinicFeedback =
            "~&!!!\":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.";

        private static readonly IDictionary<string, Profession> OfabProfessionVendorRequirements =
            new Dictionary<string, Profession>(StringComparer.OrdinalIgnoreCase)
            {
                { "OFADV", Profession.Adventurer },
                { "OFAGT", Profession.Agent },
                { "OFCRT", Profession.Bureaucrat },
                { "OFDOC", Profession.Doctor },
                { "OFENF", Profession.Enforcer },
                { "OFENG", Profession.Engineer },
                { "OFFIX", Profession.Fixer },
                { "OFKEE", Profession.Keeper },
                { "OFMA", Profession.MartialArtist },
                { "OFNT", Profession.Nanotechnician },
                { "OFPMQ3T", Profession.Metaphysicist },
                { "OFSHD", Profession.Shade },
                { "OFSOL", Profession.Soldier },
                { "OFTRD", Profession.Trader }
            };

        private static readonly IDictionary<Profession, string> ProfessionFeedbackNames =
            new Dictionary<Profession, string>
            {
                { Profession.Adventurer, "Adventurer" },
                { Profession.Agent, "Agent" },
                { Profession.Bureaucrat, "Bureaucrat" },
                { Profession.Doctor, "Doctor" },
                { Profession.Enforcer, "Enforcer" },
                { Profession.Engineer, "Engineer" },
                { Profession.Fixer, "Fixer" },
                { Profession.Keeper, "Keeper" },
                { Profession.MartialArtist, "Martial Artist" },
                { Profession.Metaphysicist, "Meta-Physicist" },
                { Profession.Nanotechnician, "Nano-Technician" },
                { Profession.Shade, "Shade" },
                { Profession.Soldier, "Soldier" },
                { Profession.Trader, "Trader" }
            };

        private const string OfabGmRequirementFeedback = "Your GM capabilities is required to be at least 1!";

        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        /// <exception cref="NullReferenceException">
        /// </exception>
        protected override void Read(GenericCmdMessage message, IZoneClient client)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                                  ? message.Target[0]
                                  : Identity.None;
            Identity routedCorpseIdentity;

            client.Server.Info(
                client,
                "GenericCmd action={0}({1}) temp1={2} count={3} temp4={4} user={5} target={6}",
                message.Action,
                (int)message.Action,
                message.Temp1,
                message.Count,
                message.Temp4,
                message.User,
                target);

            switch (message.Action)
            {
                case GenericCmdAction.Get:
                    break;
                case GenericCmdAction.Drop:
                    break;
                case GenericCmdAction.Use:
                    if (RexB18DBoxProgressTracker.TryObserveBoxUse(
                        client.Controller.Character,
                        target))
                    {
                        this.Acknowledge(client.Controller.Character, message);
                    }
                    else if (target.Type == IdentityType.Inventory)
                    {
                        client.Controller.UseItem(target);

                        // Acknowledge action
                        this.Acknowledge(client.Controller.Character, message);
                    }
                    else if (target.Type == IdentityType.ArmorPage || target.Type == IdentityType.SocialPage)
                    {
                        if (client.Controller.TryUseBackpackContainer(target))
                        {
                            this.Acknowledge(client.Controller.Character, message);
                        }
                    }
                    else if (target.Type == IdentityType.Container)
                    {
                        IInventoryPage backpackPage;
                        if (client.Controller.Character.BaseInventory.TryGetBackpackPage(target, out backpackPage))
                        {
                            BackpackContainerActionMessageHandler.Default.SendClose(client.Controller.Character, target);
                            client.Controller.Character.BaseInventory.MarkBackpackClosed(target);
                            this.Acknowledge(client.Controller.Character, message);
                        }
                    }
                    else if (target.Type == IdentityType.Corpse)
                    {
                        bool used = client.Controller.Character.Playfield.TryUseCorpse(
                            client.Controller.Character,
                            target);

                        client.Server.Info(
                            client,
                            "CorpseUse direct target={0} used={1}",
                            target,
                            used);

                        if (used)
                        {
                            this.AcknowledgeCorpseUseDelayed(client.Controller.Character, message, target);
                        }
                    }
                    else if (target.Type == IdentityType.CanbeAffected
                             && this.TryRouteDeadNpcCorpseUse(client, target, out routedCorpseIdentity))
                    {
                        this.AcknowledgeCorpseUseDelayed(client.Controller.Character, message, routedCorpseIdentity);
                    }
                    else if (this.TryHandleSurgeryClinicTerminalUse(client, message, target))
                    {
                        break;
                    }
                    else
                    {
                        if (Pool.Instance.Contains(target))
                        {
                            // TODO: Call OnUse of the targets controller
                            // Static dynels first
                            IEventHolder temp = null;
                            try
                            {
                                temp =
                                    Pool.Instance.GetObject<IEventHolder>(
                                        client.Controller.Character.Playfield.Identity,
                                        target);
                            }
                            catch (Exception)
                            {
                            }
                            if (temp != null)
                            {
                                var entity = temp as IEntity;
                                if (entity != null)
                                {
                                    Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUse);
                                    if (ev != null)
                                    {
                                        ev.Perform(client.Controller.Character, entity);
                                        this.Acknowledge(client.Controller.Character, message);
                                    }
                                    else
                                    {
                                        ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnTrade);
                                        if (ev != null)
                                        {
                                            var vendor = entity as Vendor;
                                            if (vendor != null
                                                && this.TryDenyOfabProfessionVendor(client, message, vendor))
                                            {
                                                break;
                                            }

                                            ev.Perform(client.Controller.Character, entity);

                                            TemporaryBag tempBag = new TemporaryBag(
                                                client.Controller.Character.Identity,
                                                new Identity()
                                                {
                                                    Type = IdentityType.TempBag,
                                                    Instance =
                                                Pool.Instance.GetFreeInstance<TemporaryBag>(
                                                    0,
                                                    IdentityType.TempBag)
                                                },
                                                client.Controller.Character.Identity,
                                                target);
                                            client.Controller.Character.ShoppingBag = tempBag;
                                            TradeMessageHandler.Default.Send(client.Controller.Character, tempBag);
                                            this.Acknowledge(client.Controller.Character, message);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Use statel (doors, grid terminals etc)
#if DEBUG
                            string s = string.Format(
                                "Generic Command received:\r\nAction: {0} ({1}){2}Target: {3} {4}",
                                message.Action,
                                (int)message.Action,
                                Environment.NewLine,
                                target.Type,
                                target.ToString(true));
                            ChatTextMessageHandler.Default.Send(client.Controller.Character, s);
#endif
                            client.Controller.UseStatel(target);
                        }
                    }

                    break;
                case GenericCmdAction.UseItemOnItem:
                    IItem item =
                        Pool.Instance.GetObject<IInventoryPage>(
                            new Identity()
                            {
                                Type = (IdentityType)client.Controller.Character.Identity.Instance,
                                Instance = (int)message.Target[0].Type
                            })[message.Target[0].Instance];
                    client.Controller.Character.Stats[StatIds.secondaryitemtemplate].Value = item.LowID;
                    //client.Controller.Character.Stats[StatIds.secondaryitemtype]
                    if (Pool.Instance.Contains(message.Target[1]))
                    {
                        StaticDynel temp =
                            Pool.Instance.GetObject<StaticDynel>(
                                client.Controller.Character.Playfield.Identity,
                                message.Target[1]);
                        if (temp != null)
                        {
                            Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                            if (ev != null)
                            {
                                ev.Perform(client.Controller.Character, temp);
                            }
                        }
                    }
                    else
                    {
                        client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);
                    }
                    break;
            }
        }

        private bool TryRouteDeadNpcCorpseUse(
            IZoneClient client,
            Identity target,
            out Identity routedCorpseIdentity)
        {
            bool routed = client.Controller.Character.Playfield.TryUseDeadNpcCorpse(
                client.Controller.Character,
                target,
                out routedCorpseIdentity);

            client.Server.Info(
                client,
                "CorpseUse deadNpc target={0} routed={1} corpse={2}",
                target,
                routed,
                routedCorpseIdentity);

            return routed;
        }

        private bool TryHandleSurgeryClinicTerminalUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);

            if (!this.IsCapturedSurgeryClinicTerminal(target, statelData))
            {
                return false;
            }

            int cashBefore = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            if (cashBefore < SurgeryClinicCreditCost)
            {
                client.Server.Info(
                    client,
                    "Surgery clinic terminal use blocked by insufficient captured-state support char={0} target={1} cash={2} cost={3}",
                    character.Identity,
                    target,
                    cashBefore,
                    SurgeryClinicCreditCost);
                return false;
            }

            int cashAfter = CashStatRules.Clamp((long)cashBefore - SurgeryClinicCreditCost);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
            this.SendSurgeryClinicFeedback(character);
            this.SendSurgeryClinicCastNano(character);
            CharacterActionMessageHandler.Default.SetNanoDuration(
                character,
                character.Identity,
                SurgeryClinicNanoId,
                SurgeryClinicNanoDuration);
            this.SendSurgeryClinicSpecialUsed(character);
            this.Acknowledge(character, message);
            this.SendSurgeryClinicSpecialAvailableDelayed(character);

            client.Server.Info(
                client,
                "Surgery clinic terminal use handled char={0} target={1} statelTemplate={2} cashBefore={3} cashAfter={4} nano={5} duration={6} evidence={7}",
                character.Identity,
                target,
                statelData == null ? 0 : statelData.TemplateId,
                cashBefore,
                cashAfter,
                SurgeryClinicNanoId.ToString("X", CultureInfo.InvariantCulture),
                SurgeryClinicNanoDuration,
                "captures/20260620-213807/events.log:51-52;captures/20260621-062224/events.log:52-71");

            return true;
        }

        private StatelData GetStatelData(ICharacter character, Identity target)
        {
            if (character == null || character.Playfield == null)
            {
                return null;
            }

            AORebirth.Core.Playfields.PlayfieldData playfieldData;
            if (!PlayfieldLoader.PFData.TryGetValue(character.Playfield.Identity.Instance, out playfieldData))
            {
                return null;
            }

            return playfieldData.Statels.FirstOrDefault(
                x => x.Identity.Type == target.Type && x.Identity.Instance == target.Instance);
        }

        private bool IsCapturedSurgeryClinicTerminal(Identity target, StatelData statelData)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            uint instance = unchecked((uint)target.Instance);
            if (instance == 0xC00204A2u || instance == 0xC00004A2u)
            {
                return true;
            }

            return statelData != null && CapturedSurgeryClinicTemplateIds.Contains(statelData.TemplateId);
        }

        private void SendSurgeryClinicFeedback(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = SurgeryClinicFeedback,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        private void SendSurgeryClinicCastNano(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CastNanoSpellMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    NanoId = SurgeryClinicNanoId,
                    Target = character.Identity,
                    Unknown1 = 1,
                    Caster = character.Identity
                });
        }

        private void SendSurgeryClinicSpecialUsed(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SpecialUsed,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = SurgeryClinicSpecialStatId,
                    Parameter2 = SurgeryClinicSpecialLockSeconds,
                    Unknown2 = 0
                });
        }

        private void SendSurgeryClinicSpecialAvailableDelayed(ICharacter character)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(SurgeryClinicSpecialAvailableDelay);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    CharacterActionMessageHandler.Default.SendSkillAvailable(
                        character,
                        SurgeryClinicSpecialStatId);
                });
        }

        #endregion

        #region Outbound

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <param name="announceToPlayfield">
        /// </param>
        public void Acknowledge(ICharacter character, GenericCmdMessage message, bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message), announceToPlayfield);
        }

        public void AcknowledgeDenied(
            ICharacter character,
            GenericCmdMessage message,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, Identity.None, message.Temp4, 2), announceToPlayfield);
        }

        public void AcknowledgeWithTarget(
            ICharacter character,
            GenericCmdMessage message,
            Identity target,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, target), announceToPlayfield);
        }

        public void AcknowledgeCorpseUse(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, corpse, 1), announceToPlayfield);
        }

        private void AcknowledgeCorpseUseDelayed(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(CorpseUseAcknowledgeDelay);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    this.AcknowledgeCorpseUse(character, message, corpse, announceToPlayfield);
                });
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message)
        {
            return this.Reply(character, message, Identity.None);
        }

        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride)
        {
            return this.Reply(character, message, targetOverride, message.Temp4);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4)
        {
            return this.Reply(character, message, targetOverride, temp4, 1);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4,
            int temp1)
        {
            return x =>
            {
                Identity[] targets = message.Target.ToList().ToArray();
                if (targetOverride != Identity.None && targets.Length > 0)
                {
                    targets[0] = targetOverride;
                }

                x.Identity = message.Identity;
                x.N3MessageType = message.N3MessageType;
                x.Target = targets;
                x.Temp1 = temp1;
                x.Count = message.Count;
                x.Action = message.Action;
                x.Temp4 = temp4;
                x.User = message.User;
                x.Unknown = 0;
            };
        }

        #endregion

        private bool TryDenyOfabProfessionVendor(IZoneClient client, GenericCmdMessage message, Vendor vendor)
        {
            Profession requiredProfession;
            if (string.IsNullOrEmpty(vendor.TemplateHash)
                || !OfabProfessionVendorRequirements.TryGetValue(vendor.TemplateHash, out requiredProfession))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            Profession characterProfession = (Profession)character.Stats[StatIds.profession].Value;
            if (characterProfession == requiredProfession)
            {
                return false;
            }

            client.Server.Info(
                client,
                "OFAB profession vendor denied character={0} profession={1} required={2} vendor={3} hash={4}",
                character.Identity,
                characterProfession,
                requiredProfession,
                vendor.Identity,
                vendor.TemplateHash);

            this.SendOfabProfessionDeniedFeedback(character, requiredProfession);
            this.AcknowledgeDenied(character, message);
            return true;
        }

        private void SendOfabProfessionDeniedFeedback(ICharacter character, Profession requiredProfession)
        {
            string professionName;
            if (!ProfessionFeedbackNames.TryGetValue(requiredProfession, out professionName))
            {
                professionName = requiredProfession.ToString();
            }

            ChatTextMessageHandler.Default.Send(
                character,
                "This effect can only be utilitized by " + professionName + ".");
            ChatTextMessageHandler.Default.Send(character, OfabGmRequirementFeedback);
        }
    }
}
