using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class NascenceStatueTeleportInteractionHandler
{
	public static readonly NascenceStatueTeleportInteractionHandler Default = new NascenceStatueTeleportInteractionHandler();

	private const int ShadowlandsExpansionBit = 2;

	private NascenceStatueTeleportInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null || (int)((Identity)(ref target)).Type != 51005)
		{
			return false;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!NascenceStatueTeleportCatalog.IsShadowlandsGardenPlayfield(instance))
		{
			return false;
		}
		((IClient)client).Server.Info((IClient)(object)client, "Shadowlands garden Use enter char={0} pf={1} target={2}", new object[3]
		{
			((IEntity)character).Identity,
			instance,
			target
		});
		EnsureShadowlandsExpansion(character);
		StaticDynel val = TryResolveStaticDynel(character, target);
		int num = 0;
		Event eventData = null;
		if (val != null && val.Template != null)
		{
			num = val.Template.ID;
			if (val.Events != null)
			{
				eventData = val.Events.FirstOrDefault((Event x) => (int)x.EventType == 0);
			}
		}
		else
		{
			num = TryResolveTemplateIdFromDatabase(instance, ((Identity)(ref target)).Instance);
			((IClient)client).Server.Info((IClient)(object)client, "Shadowlands garden Use: Pool miss target={0} dbTemplate={1}", new object[2] { target, num });
			eventData = TryGetTemplateEvent(num, (EventType)0);
		}
		if (TryGetTeleportDestination(eventData, out var playfieldId, out var x2, out var y, out var z))
		{
			TeleportCharacter(client, character, message, playfieldId, x2, y, z, "ShadowlandsGardenOnUseTeleport", target, "template=" + num);
			return true;
		}
		string text = TryGetItemName(num);
		if (!NascenceStatueTeleportCatalog.TryGetGardenPassageRouteByName(text, out var route) && !NascenceStatueTeleportCatalog.TryGetGardenPassageRouteByTemplateId(num, out route))
		{
			((IClient)client).Server.Info((IClient)(object)client, "Shadowlands garden Use: no Teleport/catalog template={0} name={1}", new object[2] { num, text });
			return false;
		}
		TeleportCharacter(client, character, message, route.DestinationPlayfieldId, route.DestinationX, route.DestinationY, route.DestinationZ, "ShadowlandsGardenPassageCatalog", target, route.Evidence + " template=" + num + " name=" + text);
		return true;
	}

	public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action) != UseItemOnItemInteractionRouteMode.UseItemOnItem)
		{
			return false;
		}
		if (message.Target != null && message.Target.Length >= 2)
		{
			_ = ref message.Target[1];
			if ((int)((Identity)(ref message.Target[1])).Type == 51005)
			{
				ICharacter character = client.Controller.Character;
				if (character == null || ((IInstancedEntity)character).Playfield == null)
				{
					return false;
				}
				Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				Identity val = message.Target[1];
				if (!NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(instance))
				{
					return false;
				}
				EnsureShadowlandsExpansion(character);
				IItem val2 = TryResolveSourceItem(character, message);
				if (val2 == null)
				{
					return false;
				}
				StaticDynel val3 = TryResolveStaticDynel(character, val);
				int num = 0;
				Event eventData = null;
				if (val3 != null && val3.Template != null)
				{
					num = val3.Template.ID;
					if (val3.Events != null)
					{
						eventData = val3.Events.FirstOrDefault((Event x) => (int)x.EventType == 4);
					}
				}
				else
				{
					num = TryResolveTemplateIdFromDatabase(instance, ((Identity)(ref val)).Instance);
					eventData = TryGetTemplateEvent(num, (EventType)4);
				}
				if (num == 0)
				{
					return false;
				}
				((IStats)character).Stats[(StatIds)273].Value = val2.LowID;
				if (!NascenceStatueTeleportCatalog.IsZoneReturnStatueTemplate(num) || !NascenceStatueTeleportCatalog.TryMatchReturnKey(num, val2.LowID))
				{
					return false;
				}
				if (!ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val2.LowID, val2.HighID))
				{
					ConsumeSourceInsignia(character, message, val2);
				}
				if (val2.LowID == 214789 || val2.HighID == 214789)
				{
					ThrakGardenKeyQuestRuntime.TryAdvanceToGardenOnStatueEntry(character);
				}
				if (TryGetTeleportDestination(eventData, out var playfieldId, out var x2, out var y, out var z))
				{
					TeleportCharacter(client, character, message, playfieldId, x2, y, z, "ShadowlandsZoneOnUseItemOnTeleport", val, "insignia=" + val2.LowID + " statue=" + num);
					return true;
				}
				int num2 = NascenceStatueTeleportCatalog.ResolveReturnGardenPlayfieldId(instance, ((IStats)character).Stats[(StatIds)569].Value);
				NascenceStatueTeleportCatalog.ResolveReturnGardenPosition(num2, out var x3, out var y2, out var z2);
				TeleportCharacter(client, character, message, num2, x3, y2, z2, "ShadowlandsZoneReturnCatalog", val, "insignia=" + val2.LowID + " template=" + num);
				return true;
			}
		}
		return false;
	}

	private void EnsureShadowlandsExpansion(ICharacter character)
	{
		try
		{
			int value = ((IStats)character).Stats[(StatIds)389].Value;
			if ((value & 2) == 0)
			{
				((IStats)character).Stats[(StatIds)389].Value = value | 2;
			}
		}
		catch
		{
		}
	}

	private bool TryGetTeleportDestination(Event eventData, out int playfieldId, out float x, out float y, out float z)
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		playfieldId = 0;
		x = 0f;
		y = 0f;
		z = 0f;
		if (eventData == null || eventData.Functions == null)
		{
			return false;
		}
		foreach (Function function in eventData.Functions)
		{
			if (function != null && function.FunctionType == 53016 && function.Arguments != null && function.Arguments.Values != null && function.Arguments.Values.Count >= 4)
			{
				List<MessagePackObject> values = function.Arguments.Values;
				MessagePackObject val = values[0];
				x = Convert.ToSingle(((MessagePackObject)(ref val)).ToObject());
				val = values[1];
				y = Convert.ToSingle(((MessagePackObject)(ref val)).ToObject());
				val = values[2];
				z = Convert.ToSingle(((MessagePackObject)(ref val)).ToObject());
				val = values[3];
				playfieldId = Convert.ToInt32(((MessagePackObject)(ref val)).ToObject());
				if (playfieldId != 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private Event TryGetTemplateEvent(int templateId, EventType eventType)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (templateId == 0)
		{
			return null;
		}
		if (!ItemLoader.ItemList.TryGetValue(templateId, out var value) || value.Events == null)
		{
			return null;
		}
		return value.Events.FirstOrDefault((Event x) => x.EventType == eventType);
	}

	private int TryResolveTemplateIdFromDatabase(int playfieldId, int instance)
	{
		try
		{
			DBStaticDynel val = ((Dao<DBStaticDynel, StaticDynelDao>)(object)Dao<DBStaticDynel, StaticDynelDao>.Instance).GetWhere((object)new
			{
				Playfield = playfieldId,
				Instance = instance
			}, (IDbConnection)null, (IDbTransaction)null)?.FirstOrDefault();
			if (val == null || val.stats == null)
			{
				return 0;
			}
			List<GameTuple<CharacterStat, uint>> source = MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(val.stats.ToArray());
			return (int)(source.FirstOrDefault((GameTuple<CharacterStat, uint> x) => (int)x.Value1 == 702)?.Value2 ?? 0);
		}
		catch
		{
			return 0;
		}
	}

	private string TryGetItemName(int templateId)
	{
		try
		{
			DBItemName val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(templateId);
			return (val == null) ? string.Empty : val.Name;
		}
		catch
		{
			return string.Empty;
		}
	}

	private StaticDynel TryResolveStaticDynel(ICharacter character, Identity terminalTarget)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return Pool.Instance.GetObject<StaticDynel>(((IEntity)((IInstancedEntity)character).Playfield).Identity, terminalTarget);
		}
		catch (Exception)
		{
		}
		try
		{
			foreach (StaticDynel item in Pool.Instance.GetAll<StaticDynel>(((IEntity)((IInstancedEntity)character).Playfield).Identity))
			{
				Identity identity = ((PooledObject)item).Identity;
				if (((Identity)(ref identity)).Type == ((Identity)(ref terminalTarget)).Type)
				{
					identity = ((PooledObject)item).Identity;
					if (((Identity)(ref identity)).Instance == ((Identity)(ref terminalTarget)).Instance)
					{
						return item;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private IItem TryResolveSourceItem(ICharacter character, GenericCmdMessage message)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected I4, but got Unknown
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected I4, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || message.Target == null || message.Target.Length < 1)
		{
			return null;
		}
		Identity val = message.Target[0];
		IItem val2 = null;
		try
		{
			if (((IItemContainer)character).BaseInventory != null && ((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref val)).Type, out var value) && value != null)
			{
				val2 = value[((Identity)(ref val)).Instance];
			}
			if (val2 == null)
			{
				Pool instance = Pool.Instance;
				Identity val3 = default(Identity);
				Identity identity = ((IEntity)character).Identity;
				((Identity)(ref val3)).Type = (IdentityType)((Identity)(ref identity)).Instance;
				((Identity)(ref val3)).Instance = (int)((Identity)(ref val)).Type;
				val2 = instance.GetObject<IInventoryPage>(val3)[((Identity)(ref val)).Instance];
			}
		}
		catch
		{
			val2 = null;
		}
		if (val2 == null)
		{
			return TryFindSacredGardenKey(character);
		}
		return val2;
	}

	private IItem TryFindSacredGardenKey(ICharacter character)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return null;
		}
		int[] array = new int[2] { 101, 104 };
		for (int i = 0; i < array.Length; i++)
		{
			if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(array[i], out var value) || value == null)
			{
				continue;
			}
			for (int j = value.FirstSlotNumber; j < value.FirstSlotNumber + value.MaxSlots; j++)
			{
				IItem val = value[j];
				if (val != null && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val.LowID, val.HighID))
				{
					return val;
				}
			}
		}
		return null;
	}

	private void ConsumeSourceInsignia(ICharacter character, GenericCmdMessage message, IItem sourceItem)
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Expected I4, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected I4, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected I4, but got Unknown
		Item val = (Item)(object)((sourceItem is Item) ? sourceItem : null);
		if (val != null && message.Target != null && message.Target.Length >= 1)
		{
			int multipleCount = val.MultipleCount;
			val.MultipleCount = multipleCount - 1;
			IInventoryPage value;
			if (val.MultipleCount <= 0)
			{
				((IItemContainer)character).BaseInventory.RemoveItem((int)((Identity)(ref message.Target[0])).Type, ((Identity)(ref message.Target[0])).Instance);
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, (int)((Identity)(ref message.Target[0])).Type, ((Identity)(ref message.Target[0])).Instance);
			}
			else if (((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref message.Target[0])).Type, out value))
			{
				value.Write();
			}
		}
	}

	private void TeleportCharacter(IZoneClient client, ICharacter character, GenericCmdMessage message, int destinationPlayfieldId, float destinationX, float destinationY, float destinationZ, string routeKind, Identity target, string evidence)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		((IInstancedEntity)character).DoNotDoTimers = false;
		character.StopMovement();
		((IStats)character).Stats[(StatIds)193].BaseValue = 0u;
		((IStats)character).Stats[(StatIds)192].BaseValue = 0u;
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val != null)
		{
			Coordinate val2 = new Coordinate(destinationX, destinationY, destinationZ);
			IPlayfield playfield = ((IInstancedEntity)character).Playfield;
			Quaternion heading = ((IDynel)character).Heading;
			Identity val3 = default(Identity);
			((Identity)(ref val3)).Type = (IdentityType)51101;
			((Identity)(ref val3)).Instance = destinationPlayfieldId;
			playfield.Teleport(val, val2, (IQuaternion)(object)heading, val3);
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
			ServerBase server = ((IClient)client).Server;
			object[] obj = new object[9]
			{
				((IEntity)character).Identity,
				target,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			val3 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			obj[2] = ((Identity)(ref val3)).Instance;
			obj[3] = destinationPlayfieldId;
			obj[4] = destinationX;
			obj[5] = destinationY;
			obj[6] = destinationZ;
			obj[7] = routeKind;
			obj[8] = evidence;
			server.Info((IClient)(object)client, "Shadowlands statue teleport handled char={0} target={1} sourcePf={2} destPf={3} dest=({4:F3},{5:F3},{6:F3}) route={7} evidence={8}", obj);
		}
	}
}
