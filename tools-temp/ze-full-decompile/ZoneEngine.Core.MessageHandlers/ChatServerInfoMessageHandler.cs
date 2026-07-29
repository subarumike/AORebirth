using System;
using System.Net;
using System.Net.Sockets;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using Utility.Config;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ChatServerInfoMessageHandler : BaseMessageHandler<ChatServerInfoMessage, ChatServerInfoMessageHandler>
{
	public void Send(ICharacter character)
	{
		((AbstractMessageHandler<ChatServerInfoMessage>)(object)this).Send(character, Filler(character), false);
	}

	private static MessageDataFiller<ChatServerInfoMessage> Filler(ICharacter character)
	{
		string chatServerIp = string.Empty;
		if (IPAddress.TryParse(ConfigReadWrite.Instance.CurrentConfig.ChatIP, out var _))
		{
			chatServerIp = ConfigReadWrite.Instance.CurrentConfig.ChatIP;
		}
		else
		{
			IPHostEntry hostEntry = Dns.GetHostEntry(ConfigReadWrite.Instance.CurrentConfig.ChatIP);
			IPAddress[] addressList = hostEntry.AddressList;
			foreach (IPAddress iPAddress in addressList)
			{
				if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					chatServerIp = iPAddress.ToString();
					break;
				}
			}
		}
		int chatPort = Convert.ToInt32(ConfigReadWrite.Instance.CurrentConfig.ChatPort);
		return delegate(ChatServerInfoMessage x)
		{
			x.HostName = chatServerIp;
			x.Port = chatPort;
		};
	}
}
