using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.GMI;

public sealed class GmiMarketTerminalInteractionHandler
{
	public static readonly GmiMarketTerminalInteractionHandler Default = new GmiMarketTerminalInteractionHandler();

	private const int MarketTerminalInstanceNerko = -1073282272;

	private const int MarketTerminalInstanceTraner = -1073216881;

	private GmiMarketTerminalInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null)
		{
			return false;
		}
		if ((int)((Identity)(ref target)).Type != 51005 || !IsMarketTerminalInstance(((Identity)(ref target)).Instance))
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null)
		{
			return false;
		}
		((IInstancedEntity)character).DoNotDoTimers = false;
		try
		{
			client.Controller.UseStatel(target, (EventType)0);
		}
		catch (Exception ex)
		{
			((IClient)client).Server.Info((IClient)(object)client, "GMI Market terminal UseStatel error char={0} target={1} ex={2}", new object[3]
			{
				((IEntity)character).Identity,
				target,
				ex.Message
			});
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		ServerBase server = ((IClient)client).Server;
		object[] obj = new object[3]
		{
			((IEntity)character).Identity,
			null,
			null
		};
		int num;
		if (((IInstancedEntity)character).Playfield == null)
		{
			num = 0;
		}
		else
		{
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			num = ((Identity)(ref identity)).Instance;
		}
		obj[1] = num;
		obj[2] = target;
		server.Info((IClient)(object)client, "GMI Market terminal Use ACK char={0} pf={1} target={2}", obj);
		return true;
	}

	private static bool IsMarketTerminalInstance(int instance)
	{
		return instance == -1073282272 || instance == -1073216881;
	}
}
