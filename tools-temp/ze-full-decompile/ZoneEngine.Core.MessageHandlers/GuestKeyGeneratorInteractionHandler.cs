using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class GuestKeyGeneratorInteractionHandler
{
	public static readonly GuestKeyGeneratorInteractionHandler Default = new GuestKeyGeneratorInteractionHandler();

	private const int CapturedCityAccessCardIdentityType = 51056;

	private const int CapturedCityAccessCardInstance = 7174157;

	private const int CapturedCityAccessCardStateMachineType = 1000015;

	private const int CapturedCityAccessCardBuildingType = 51102;

	private const int CapturedCityAccessCardBuildingInstance = 6010;

	private const int CapturedCityAccessCardOwnerType = 100001;

	private const int CapturedCityAccessCardOwnerInstance = 71546;

	private const uint CapturedCityAccessCardBuildingComplexInstance = 3222733455u;

	private const int CapturedCityAccessCardTimeExist = 90000;

	private const int CityAccessCardExpiresAtUnixSecondsStat = 1801812273;

	private const int CapturedPrivateCityOrganizationInstance = 1370122;

	private static readonly object CityAccessCardExpirationSync = new object();

	private static readonly Dictionary<ulong, Timer> CityAccessCardExpirationTimers = new Dictionary<ulong, Timer>();

	private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private static int cityAccessCardInstanceSeed = Math.Max(7174157, (int)(DateTime.UtcNow.Ticks & 0x3FFFFFFF));

	private GuestKeyGeneratorInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Expected O, but got Unknown
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null || !GuestKeyGeneratorInteractionRules.IsPrivateCityGuestKeyTerminalTarget(target) || !Playfield.IsPrivateCityPlayfieldCandidate(((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		if (!TryCreateAndPersistCityAccessCard(character, out var cityAccessCard, out var inventorySlot, out var inventoryError))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Could not generate a guest key. (" + ((object)(InventoryError)(ref inventoryError)).ToString() + ")", 0, 0);
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
			return true;
		}
		client.SendCompressed((MessageBody)(object)CreateCapturedCityAccessCardItem(character, cityAccessCard.Identity));
		ContainerAddItemMessage val = new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0
		};
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		((Identity)(ref val2)).Instance = 0;
		val.SourceContainer = val2;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		Identity identity = ((IEntity)character).Identity;
		((Identity)(ref val2)).Instance = ((Identity)(ref identity)).Instance;
		val.Target = val2;
		val.TargetPlacement = 111;
		client.SendCompressed((MessageBody)val);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		((IClient)client).Server.Info((IClient)(object)client, "Private city guest key terminal created captured City Access Card character={0} terminal={1} template={2} overflowSlot={3} inventorySlot={4} item={5} lifetimeMs={6} evidence=private_city_capture_20260623_012720 runtime_target=574B84AB persisted=1", new object[7]
		{
			((IEntity)character).Identity,
			target,
			280642,
			111,
			inventorySlot,
			cityAccessCard.Identity,
			900000
		});
		return true;
	}

	public static void ProcessCityAccessCardLifetimes(ICharacter character)
	{
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				if (IsCityAccessCard(item.Value))
				{
					DateTime cityAccessCardExpirationUtc = GetCityAccessCardExpirationUtc(item.Value);
					if (cityAccessCardExpirationUtc <= utcNow)
					{
						Identity identity = item.Value.Identity;
						TryRemoveCityAccessCard(character, ((Identity)(ref identity)).Long(), notifyClient: false);
					}
					else
					{
						RegisterCityAccessCardExpiration(item.Value.Identity, cityAccessCardExpirationUtc);
					}
				}
			}
		}
	}

	private static bool TryCreateAndPersistCityAccessCard(ICharacter character, out Item cityAccessCard, out int inventorySlot, out InventoryError inventoryError)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected I4, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		cityAccessCard = null;
		inventorySlot = -1;
		inventoryError = (InventoryError)(-1);
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(((IItemContainer)character).BaseInventory.StandardPage, out var value))
		{
			return false;
		}
		inventorySlot = value.FindFreeSlot();
		if (inventorySlot == -1)
		{
			inventoryError = (InventoryError)2;
			return false;
		}
		try
		{
			cityAccessCard = CreateCapturedCityAccessCardInventoryItem(character);
		}
		catch
		{
			inventoryError = (InventoryError)(-1);
			return false;
		}
		inventoryError = (InventoryError)(int)value.Add(inventorySlot, (IItem)(object)cityAccessCard);
		if (inventoryError)
		{
			return false;
		}
		try
		{
			if (!((IItemContainer)character).BaseInventory.Write())
			{
				TryRemoveInventorySlot(value, inventorySlot);
				inventoryError = (InventoryError)(-1);
				return false;
			}
		}
		catch
		{
			TryRemoveInventorySlot(value, inventorySlot);
			inventoryError = (InventoryError)(-1);
			return false;
		}
		RegisterCityAccessCardExpiration(cityAccessCard.Identity);
		return true;
	}

	private static void TryRemoveInventorySlot(IInventoryPage inventoryPage, int inventorySlot)
	{
		try
		{
			if (inventoryPage != null && inventoryPage[inventorySlot] != null)
			{
				inventoryPage.Remove(inventorySlot);
			}
		}
		catch
		{
		}
	}

	private static Item CreateCapturedCityAccessCardInventoryItem(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected I4, but got Unknown
		Item val = new Item(1, 280642, 280642);
		Identity identity = default(Identity);
		((Identity)(ref identity)).Type = (IdentityType)51056;
		((Identity)(ref identity)).Instance = CreateCityAccessCardInstance();
		val.Identity = identity;
		val.Flags = 1;
		Item val2 = val;
		GameTuple<CharacterStat, uint>[] array = CreateCapturedCityAccessCardStats(ResolvePrivateCityOrganizationInstance(character));
		foreach (GameTuple<CharacterStat, uint> val3 in array)
		{
			val2.SetAttribute((int)val3.Value1, (int)val3.Value2);
		}
		val2.MultipleCount = 1;
		val2.SetAttribute(1801812273, GetUnixTimeSecondsUtc(DateTime.UtcNow.AddMilliseconds(900000.0)));
		return val2;
	}

	private static int CreateCityAccessCardInstance()
	{
		int num = Interlocked.Increment(ref cityAccessCardInstanceSeed) & 0x7FFFFFFF;
		return (num == 0) ? CreateCityAccessCardInstance() : num;
	}

	private static void RegisterCityAccessCardExpiration(Identity itemIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		RegisterCityAccessCardExpiration(itemIdentity, DateTime.UtcNow.AddMilliseconds(900000.0));
	}

	private static void RegisterCityAccessCardExpiration(Identity itemIdentity, DateTime expiresAtUtc)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref itemIdentity)).Type == 0)
		{
			return;
		}
		ulong num = ((Identity)(ref itemIdentity)).Long();
		int timerDueTimeMilliseconds = GetTimerDueTimeMilliseconds(expiresAtUtc);
		Timer value = null;
		lock (CityAccessCardExpirationSync)
		{
			if (CityAccessCardExpirationTimers.TryGetValue(num, out value))
			{
				CityAccessCardExpirationTimers.Remove(num);
			}
			CityAccessCardExpirationTimers[num] = new Timer(ExpireCityAccessCard, num, timerDueTimeMilliseconds, -1);
		}
		value?.Dispose();
	}

	private static void ExpireCityAccessCard(object state)
	{
		ulong num = (ulong)state;
		Timer value = null;
		lock (CityAccessCardExpirationSync)
		{
			if (CityAccessCardExpirationTimers.TryGetValue(num, out value))
			{
				CityAccessCardExpirationTimers.Remove(num);
			}
		}
		value?.Dispose();
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(50000))
		{
			if (TryRemoveCityAccessCard(item, num, notifyClient: true))
			{
				break;
			}
		}
	}

	private static bool TryRemoveCityAccessCard(ICharacter character, ulong itemKey, bool notifyClient)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List().ToList())
			{
				if (!IsCityAccessCard(item.Value, itemKey))
				{
					continue;
				}
				try
				{
					page.Value.Remove(item.Key);
					((IItemContainer)character).BaseInventory.Write();
					if (notifyClient && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, page.Value.Page, item.Key);
					}
				}
				catch
				{
					return false;
				}
				return true;
			}
		}
		return false;
	}

	private static bool IsCityAccessCard(IItem item, ulong itemKey)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (IsCityAccessCard(item))
		{
			Identity identity = item.Identity;
			result = ((((Identity)(ref identity)).Long() == itemKey) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static bool IsCityAccessCard(IItem item)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Invalid comparison between Unknown and I4
		int result;
		if (item != null && item.LowID == 280642 && item.HighID == 280642)
		{
			_ = item.Identity;
			Identity identity = item.Identity;
			result = (((int)((Identity)(ref identity)).Type == 51056) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static DateTime GetCityAccessCardExpirationUtc(IItem item)
	{
		int attribute = item.GetAttribute(1801812273);
		if (attribute <= 0)
		{
			return DateTime.UtcNow.AddMilliseconds(900000.0);
		}
		return UnixEpochUtc.AddSeconds(attribute);
	}

	private static int GetUnixTimeSecondsUtc(DateTime value)
	{
		return (int)Math.Max(0.0, (value.ToUniversalTime() - UnixEpochUtc).TotalSeconds);
	}

	private static int GetTimerDueTimeMilliseconds(DateTime expiresAtUtc)
	{
		double totalMilliseconds = (expiresAtUtc.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
		if (totalMilliseconds <= 0.0)
		{
			return 0;
		}
		return (totalMilliseconds > 2147483647.0) ? int.MaxValue : ((int)totalMilliseconds);
	}

	private static SimpleItemFullUpdateMessage CreateCapturedCityAccessCardItem(ICharacter character, Identity itemIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected I4, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Expected O, but got Unknown
		SimpleItemFullUpdateMessage val = new SimpleItemFullUpdateMessage();
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = ((Identity)(ref itemIdentity)).Type;
		((Identity)(ref val2)).Instance = ((Identity)(ref itemIdentity)).Instance;
		((N3Message)val).Identity = val2;
		((N3Message)val).Unknown = 0;
		val.MsgVersion = 11;
		val2 = ((IEntity)character).Identity;
		val.Identitytype = (int)((Identity)(ref val2)).Type;
		val2 = ((IEntity)character).Identity;
		val.Instance = ((Identity)(ref val2)).Instance;
		val2 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		val.Playfield = ((Identity)(ref val2)).Instance;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)1000015;
		((Identity)(ref val2)).Instance = 0;
		val.Unknown1 = val2;
		val.Unknown2 = 113;
		val.Unknown3 = 111;
		val.Stats = CreateCapturedCityAccessCardStats(ResolvePrivateCityOrganizationInstance(character));
		val.Name = string.Empty;
		return val;
	}

	private static GameTuple<CharacterStat, uint>[] CreateCapturedCityAccessCardStats(int organizationInstance)
	{
		return new GameTuple<CharacterStat, uint>[14]
		{
			CityAccessCardStat((CharacterStat)0, 1),
			CityAccessCardStat((CharacterStat)23, 280642),
			CityAccessCardStat((CharacterStat)701, 1),
			CityAccessCardStat((CharacterStat)702, 280642),
			CityAccessCardStat((CharacterStat)703, 280642),
			CityAccessCardStat((CharacterStat)412, 1),
			CityAccessCardStat((CharacterStat)184, 51102),
			CityAccessCardStat((CharacterStat)185, 6010),
			CityAccessCardStat((CharacterStat)186, 100001),
			CityAccessCardStat((CharacterStat)187, 71546),
			CityAccessCardStat((CharacterStat)188, 3222733455u),
			CityAccessCardStat((CharacterStat)195, 0),
			CityAccessCardStat((CharacterStat)192, organizationInstance),
			CityAccessCardStat((CharacterStat)8, 90000)
		};
	}

	private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, int value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return CityAccessCardStat(stat, (uint)value);
	}

	private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, uint value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return new GameTuple<CharacterStat, uint>
		{
			Value1 = stat,
			Value2 = value
		};
	}

	private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
	{
		int num = ResolveCharacterOrganizationInstance(character);
		return (num > 0) ? num : 1370122;
	}

	private static int ResolveCharacterOrganizationInstance(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)5].BaseValue;
		if (baseValue != 0 && baseValue <= int.MaxValue)
		{
			return (int)baseValue;
		}
		int value = ((IStats)character).Stats[(StatIds)5].Value;
		return (value > 0) ? value : 0;
	}
}
