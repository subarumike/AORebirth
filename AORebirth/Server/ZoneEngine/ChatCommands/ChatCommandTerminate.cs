#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.ChatCommands
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Live client /terminate Yes path (chat name "terminate"):
    /// apply death XP rules and force player death for Insurance Terminal respawn testing.
    /// </summary>
    public class ChatCommandTerminate : AOChatCommand
    {
        public override bool CheckCommandArguments(string[] args)
        {
            return true;
        }

        public override void CommandHelp(ICharacter character)
        {
            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    "Usage: /terminate — Yes confirmation suicides, moves uninsured XP to UnsavedXP pool (level under 220), dies at Insurance bind."));
        }

        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield == null)
            {
                ChatTextMessageHandler.Default.Send(character, "Terminate failed: not on a ZoneEngine playfield.");
                return;
            }

            bool alreadyMarkedDead = character.Stats[StatIds.deadtimer].Value != 0
                || character.Stats[StatIds.health].Value <= 0;

            if (!alreadyMarkedDead)
            {
                CombatXpRuntimeService.ApplyDeathUninsuredXpLoss(character);
            }

            // Do not pre-zero Health — leave MarkPlayerDead to ForcePlayerDeath.
            playfield.ForcePlayerDeath(character);

            if (alreadyMarkedDead)
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Terminate: resent death state. Use Die when the corpse UI appears.");
            }
        }

        public override int GMLevelNeeded()
        {
            return 0;
        }

        public override List<string> ListCommands()
        {
            return new List<string> { "terminate" };
        }
    }
}
