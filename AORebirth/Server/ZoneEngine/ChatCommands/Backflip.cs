#region License

// Copyright (c) 2005-2014, CellAO Team
//
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.ChatCommands
{
    #region Usings

    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;

    #endregion

    public class Backflip : AOChatCommand
    {
        public override bool CheckCommandArguments(string[] args)
        {
            return true;
        }

        public override void CommandHelp(ICharacter character)
        {
        }

        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            SocialActionRuntimeService.BroadcastAthleteBackflip(character);
        }

        public override int GMLevelNeeded()
        {
            return 0;
        }

        public override List<string> ListCommands()
        {
            return new List<string>(new[] { "backflip" });
        }
    }
}
