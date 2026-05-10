#region License

// Copyright (c) 2005-2014, CellAO Team
//
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.ChatCommands
{
    #region Usings ...

    using System.Collections.Generic;

    using CellAO.Core.Entities;
    using CellAO.Core.Network;
    using CellAO.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public class Posture : AOChatCommand
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
            string command = args[0].ToLower();
            if (command == "sit")
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(character, "Server posture command received: sit"));
                character.StopMovement();
                character.UpdateMoveType(30);
                this.SendPostureAction(character, CharacterActionType.ChangeAnimationAndStance, 0);
                return;
            }

            if (command == "stand")
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(character, "Server posture command received: stand"));
                character.UpdateMoveType(37);
                this.SendPostureAction(character, CharacterActionType.StandUp, 1);
            }
        }

        public override int GMLevelNeeded()
        {
            return 0;
        }

        public override List<string> ListCommands()
        {
            return new List<string>(new[] { "sit", "stand" });
        }

        private void SendPostureAction(ICharacter character, CharacterActionType action, int parameter1)
        {
            character.Playfield.Announce(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0x00,
                    Action = action,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = parameter1,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }
    }
}
