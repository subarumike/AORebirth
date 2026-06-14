#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
// conditions in the project license are met.

#endregion

namespace ZoneEngine.ChatCommands
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public class ChatCommandGiveCredits : AOChatCommand
    {
        public override bool CheckCommandArguments(string[] args)
        {
            return CheckArgumentHelper(new List<Type> { typeof(int) }, args);
        }

        public override void CommandHelp(ICharacter character)
        {
            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(character, "Syntax: /command givecredits <amount>"));
        }

        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            int amount;
            if (!int.TryParse(args[1], out amount) || amount <= 0)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(character, "Credit amount must be a positive number."));
                return;
            }

            int currentCash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            int newCash = CashStatRules.Clamp((long)currentCash + amount);

            character.Stats[StatIds.cash].Set((uint)newCash);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)newCash);
            character.Stats.Write();

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    "Credits added. Old: " + currentCash + " New: " + newCash));
        }

        public override int GMLevelNeeded()
        {
            return 1;
        }

        public override List<string> ListCommands()
        {
            return new List<string> { "givecredits" };
        }
    }
}
