using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandCreateOrg : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return args.Length >= 2 && GetOrganizationName(args).Length > 0;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Syntax: /command createorg <organization name>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		string organizationName = GetOrganizationName(args);
		if (organizationName.Length == 0)
		{
			CommandHelp(character);
			return;
		}
		if (organizationName.Length > 32)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Organization name must be 32 characters or less.", 0, 0));
			return;
		}
		if (((IStats)character).Stats[(StatIds)5].BaseValue != 0)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "You are already in organization id " + ((IStats)character).Stats[(StatIds)5].BaseValue + ".", 0, 0));
			return;
		}
		bool flag;
		try
		{
			OrganizationDao instance = Dao<DBOrganization, OrganizationDao>.Instance;
			DateTime utcNow = DateTime.UtcNow;
			Identity identity = ((IEntity)character).Identity;
			flag = instance.CreateOrganization(organizationName, utcNow, ((Identity)(ref identity)).Instance);
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Organization create failed; see ZoneEngine log.", 0, 0));
			return;
		}
		if (!flag)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Organization already exists: " + organizationName, 0, 0));
			return;
		}
		int organizationId = Dao<DBOrganization, OrganizationDao>.Instance.GetOrganizationId(organizationName);
		if (organizationId <= 0)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Organization was created, but its id could not be resolved: " + organizationName, 0, 0));
			return;
		}
		((IStats)character).Stats[(StatIds)48].Set(0u, false);
		((IStats)character).Stats[(StatIds)5].Set((uint)organizationId, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 48, 0u);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 5, (uint)organizationId);
		((IDatabaseObject)((IStats)character).Stats).Write();
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Organization created: " + organizationName + " (" + organizationId + "). You are rank 0.", 0, 0));
	}

	public override int GMLevelNeeded()
	{
		return 0;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "createorg", "makeorg", "orgcreate" };
	}

	private static string GetOrganizationName(string[] args)
	{
		return string.Join(" ", from arg in args.Skip(1)
			where !string.IsNullOrWhiteSpace(arg)
			select arg).Trim();
	}
}
