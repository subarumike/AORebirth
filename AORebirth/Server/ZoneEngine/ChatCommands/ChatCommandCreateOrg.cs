#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
// conditions in the project license are met.

#endregion

namespace ZoneEngine.ChatCommands
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public class ChatCommandCreateOrg : AOChatCommand
    {
        public override bool CheckCommandArguments(string[] args)
        {
            return args.Length >= 2 && GetOrganizationName(args).Length > 0;
        }

        public override void CommandHelp(ICharacter character)
        {
            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(character, "Syntax: /command createorg <organization name>"));
        }

        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            string organizationName = GetOrganizationName(args);
            if (organizationName.Length == 0)
            {
                this.CommandHelp(character);
                return;
            }

            if (character.Stats[StatIds.clan].BaseValue != 0)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        "You are already in organization id " + character.Stats[StatIds.clan].BaseValue + "."));
                return;
            }

            if (!OrganizationDao.Instance.CreateOrganization(
                    organizationName,
                    DateTime.UtcNow,
                    character.Identity.Instance))
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        "Organization already exists: " + organizationName));
                return;
            }

            int organizationId = OrganizationDao.Instance.GetOrganizationId(organizationName);
            if (organizationId <= 0)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        "Organization was created, but its id could not be resolved: " + organizationName));
                return;
            }

            character.Stats[StatIds.clanlevel].Set(0);
            character.Stats[StatIds.clan].Set((uint)organizationId);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.clanlevel, 0);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.clan, (uint)organizationId);
            character.Stats.Write();

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    "Organization created: " + organizationName + " (" + organizationId + "). You are rank 0."));
        }

        public override int GMLevelNeeded()
        {
            return 1;
        }

        public override List<string> ListCommands()
        {
            return new List<string> { "createorg", "makeorg", "orgcreate" };
        }

        private static string GetOrganizationName(string[] args)
        {
            return string.Join(" ", args.Skip(1).Where(arg => !string.IsNullOrWhiteSpace(arg))).Trim();
        }
    }
}
