namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class StaticDynelInteractionHandler
    {
        public static readonly StaticDynelInteractionHandler Default =
            new StaticDynelInteractionHandler();

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

        private StaticDynelInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (StaticDynelInteractionRules.ResolveRouteMode(Pool.Instance.Contains(target))
                != StaticDynelInteractionRouteMode.PoolOnUseOrTrade)
            {
                return false;
            }

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
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
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
                                return true;
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
                            GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                        }
                    }
                }
            }

            return true;
        }

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
            GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
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
