#region License



// Copyright (c) 2005-2014, CellAO Team

//

// All rights reserved.



#endregion



namespace ZoneEngine.ChatCommands

{

    #region Usings ...



    using System;

    using System.Collections.Generic;



    using AORebirth.Core.Entities;



    using SmokeLounge.AOtomation.Messaging.GameData;



    using ZoneEngine.Core;

    using ZoneEngine.Core.MessageHandlers;



    #endregion



    public class ChatCommandGivePetShell : AOChatCommand

    {

        public override bool CheckCommandArguments(string[] args)

        {

            return CheckArgumentHelper(new List<Type> { typeof(string) }, args);

        }



        public override void CommandHelp(ICharacter character)

        {

            character.Playfield.Publish(

                ChatTextMessageHandler.Default.CreateIM(

                    character,

                    "Usage: /command givepetshell engineer|bureaucrat|mp\r\nGives a clickable pet shell (test command only)."));

        }



        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)

        {

            PetShellKind kind;

            if (!TryParseKind(args[1], out kind))

            {

                this.CommandHelp(character);

                return;

            }



            if (!PetShellItemService.Default.TryGiveShell(character, kind))

            {

                character.Playfield.Publish(

                    ChatTextMessageHandler.Default.CreateIM(character, "Could not give pet shell."));

                return;

            }



            character.Playfield.Publish(

                ChatTextMessageHandler.Default.CreateIM(

                    character,

                    string.Format(
                        "Pet shell added ({0}, item {1}). Right-click to summon your pet.",
                        kind,
                        kind == PetShellKind.Engineer ? 43328 : 96235)));

        }



        public override int GMLevelNeeded()

        {

            return 1;

        }



        public override List<string> ListCommands()

        {

            return new List<string> { "givepetshell" };

        }



        private static bool TryParseKind(string value, out PetShellKind kind)

        {

            if (string.Equals(value, "engineer", StringComparison.OrdinalIgnoreCase)

                || string.Equals(value, "eng", StringComparison.OrdinalIgnoreCase))

            {

                kind = PetShellKind.Engineer;

                return true;

            }



            if (string.Equals(value, "bureaucrat", StringComparison.OrdinalIgnoreCase)

                || string.Equals(value, "bureau", StringComparison.OrdinalIgnoreCase)

                || string.Equals(value, "crat", StringComparison.OrdinalIgnoreCase))

            {

                kind = PetShellKind.Bureaucrat;

                return true;

            }



            if (string.Equals(value, "metaphysicist", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "mp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "meta", StringComparison.OrdinalIgnoreCase))
            {
                kind = PetShellKind.MetaPhysicist;
                return true;
            }

            kind = default(PetShellKind);

            return false;

        }

    }

}



