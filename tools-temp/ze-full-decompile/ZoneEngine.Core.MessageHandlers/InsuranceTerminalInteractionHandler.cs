using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Statels;
using AORebirth.Interfaces;
using Cell.Core;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class InsuranceTerminalInteractionHandler
{
	public static readonly InsuranceTerminalInteractionHandler Default = new InsuranceTerminalInteractionHandler();

	private const int OmniTradeInsuranceTerminalInstance = -1073413489;

	private static readonly int[] InsuranceTemplateIds = new int[10] { 261415, 261416, 261417, 261418, 261419, 261420, 261421, 261422, 261423, 261424 };

	private static readonly MessagePackObject[] NoArguments = (MessagePackObject[])(object)new MessagePackObject[0];

	private static HashSet<int> cachedSaveCharTemplateIds;

	private InsuranceTerminalInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null || (int)((Identity)(ref target)).Type != 51005)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		StatelData statelData = GetStatelData(character, target);
		int num = statelData?.TemplateId ?? 0;
		if (!IsInsuranceTerminal(target, num, statelData))
		{
			return false;
		}
		((IInstancedEntity)character).DoNotDoTimers = false;
		bool flag = FunctionCollection.Instance.CallFunction(53032, (INamedEntity)(object)character, (IEntity)(object)character, (IInstancedEntity)(object)character, NoArguments);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		ServerBase server = ((IClient)client).Server;
		object[] obj = new object[5]
		{
			((IEntity)character).Identity,
			null,
			null,
			null,
			null
		};
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		obj[1] = ((Identity)(ref identity)).Instance;
		obj[2] = target;
		obj[3] = num;
		obj[4] = flag;
		server.Info((IClient)(object)client, "Insurance terminal Use handled char={0} pf={1} target={2} template={3} saveCharOk={4}", obj);
		return true;
	}

	private bool IsInsuranceTerminal(Identity target, int templateId, StatelData statelData)
	{
		if (((Identity)(ref target)).Instance == -1073413489)
		{
			return true;
		}
		if (templateId != 0)
		{
			if (InsuranceTemplateIds.Contains(templateId))
			{
				return true;
			}
			if (TemplateHasSaveChar(templateId))
			{
				return true;
			}
		}
		return StatelHasSaveChar(statelData);
	}

	private static bool TemplateHasSaveChar(int templateId)
	{
		EnsureSaveCharTemplateCache();
		return cachedSaveCharTemplateIds.Contains(templateId);
	}

	private static void EnsureSaveCharTemplateCache()
	{
		if (cachedSaveCharTemplateIds != null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		if (ItemLoader.ItemList != null)
		{
			foreach (KeyValuePair<int, ItemTemplate> item2 in ItemLoader.ItemList)
			{
				if (item2.Value != null && EventsHaveSaveChar(item2.Value.Events))
				{
					hashSet.Add(item2.Key);
				}
			}
		}
		int[] insuranceTemplateIds = InsuranceTemplateIds;
		foreach (int item in insuranceTemplateIds)
		{
			hashSet.Add(item);
		}
		cachedSaveCharTemplateIds = hashSet;
	}

	private static bool StatelHasSaveChar(StatelData statelData)
	{
		return statelData != null && EventsHaveSaveChar(statelData.Events);
	}

	private static bool EventsHaveSaveChar(IEnumerable<Event> events)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (events == null)
		{
			return false;
		}
		foreach (Event @event in events)
		{
			if (@event == null || (int)@event.EventType != 0 || @event.Functions == null)
			{
				continue;
			}
			foreach (Function function in @event.Functions)
			{
				if (function != null && function.FunctionType == 53032)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static StatelData GetStatelData(ICharacter character, Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!PlayfieldLoader.PFData.ContainsKey(instance))
		{
			return null;
		}
		return PlayfieldLoader.PFData[instance].Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			Identity identity2 = x.Identity;
			int result;
			if (((Identity)(ref identity2)).Type == ((Identity)(ref target)).Type)
			{
				identity2 = x.Identity;
				result = ((((Identity)(ref identity2)).Instance == ((Identity)(ref target)).Instance) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		});
	}
}
