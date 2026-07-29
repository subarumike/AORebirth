using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandGivePetShell : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(string));
		return AOChatCommand.CheckArgumentHelper(list, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: /command givepetshell engineer|bureaucrat|mp\r\nGives a clickable pet shell (test command only).", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		if (!TryParseKind(args[1], out var kind))
		{
			CommandHelp(character);
		}
		else if (!PetShellItemService.Default.TryGiveShell(character, kind))
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Could not give pet shell.", 0, 0));
		}
		else
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Pet shell added ({kind}, item {((kind == PetShellKind.Engineer) ? 43328 : 96235)}). Right-click to summon your pet.", 0, 0));
		}
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
		if (string.Equals(value, "engineer", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "eng", StringComparison.OrdinalIgnoreCase))
		{
			kind = PetShellKind.Engineer;
			return true;
		}
		if (string.Equals(value, "bureaucrat", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "bureau", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "crat", StringComparison.OrdinalIgnoreCase))
		{
			kind = PetShellKind.Bureaucrat;
			return true;
		}
		if (string.Equals(value, "metaphysicist", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "mp", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "meta", StringComparison.OrdinalIgnoreCase))
		{
			kind = PetShellKind.MetaPhysicist;
			return true;
		}
		kind = PetShellKind.Engineer;
		return false;
	}
}
