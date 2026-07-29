using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.ChatCommands;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Script;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ChatCmdMessageHandler : BaseMessageHandler<ChatCmdMessage, ChatCmdMessageHandler>
{
	public ChatCmdMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(ChatCmdMessage message, IZoneClient client)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		string text = ChatCommandText.Normalize(message.Command);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		string[] array = text.Trim().Split(' ');
		string text2 = array[0].ToLower();
		((IClient)client).Server.Info((IClient)(object)client, "ChatCmd command={0} args={1} selectedTarget={2}", new object[3]
		{
			text2,
			text,
			((ITargetingEntity)client.Controller.Character).SelectedTarget
		});
		if (text2 == "sit" || text2 == "stand")
		{
			new Posture().ExecuteCommand(client.Controller.Character, ((ITargetingEntity)client.Controller.Character).SelectedTarget, array);
			return;
		}
		switch (text2)
		{
		case "team":
			TeamRuntime.TryHandleChatCommand(client.Controller.Character, array);
			return;
		case "pet":
			PetCommandService.HandleChatPetCommand(client, array);
			return;
		case "invite":
			if (array.Length >= 2)
			{
				TeamRuntime.TryHandleChatCommand(client.Controller.Character, new string[3]
				{
					"team",
					"invite",
					array[1]
				});
				return;
			}
			break;
		}
		ScriptCompiler.Instance.CallChatCommand(text2, client, ((ITargetingEntity)client.Controller.Character).SelectedTarget, array);
	}
}
