using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Nanos;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core;

public sealed class PetShellItemService
{
	private struct PetShellKey
	{
		public int OwnerInstance { get; private set; }

		public int ContainerType { get; private set; }

		public int SlotInstance { get; private set; }

		public PetShellKey(int ownerInstance, int containerType, int slotInstance)
		{
			OwnerInstance = ownerInstance;
			ContainerType = containerType;
			SlotInstance = slotInstance;
		}
	}

	private static readonly PetShellItemService DefaultInstance = new PetShellItemService();

	private readonly ConcurrentDictionary<PetShellKey, PetShellDefinition> shellsBySlot = new ConcurrentDictionary<PetShellKey, PetShellDefinition>();

	public static PetShellItemService Default => DefaultInstance;

	private PetShellItemService()
	{
	}

	public bool TryGiveShellForNano(ICharacter character, int nanoId)
	{
		if (!PetShellCatalog.UsesShellOnSummon(((IStats)character).Stats[(StatIds)60].Value, nanoId))
		{
			return false;
		}
		if (!PetSummonNanoCatalog.TryResolveShellSummonParams(nanoId, out var summonParams) && !PetSummonNanoCatalog.TryResolve(character, nanoId, out summonParams))
		{
			return false;
		}
		if (!PetShellCatalog.TryGet(PetShellCatalog.ResolveKind(((IStats)character).Stats[(StatIds)60].Value), out var definition))
		{
			return false;
		}
		int displayItemLowId = definition.DisplayItemLowId;
		int displayItemHighId = definition.DisplayItemHighId;
		int displayQuality = definition.DisplayQuality;
		CapturedBureaucratPetProfile profile;
		if (PetSummonNanoCatalog.TryGetBureaucratShellDisplay(nanoId, out var shellDisplay))
		{
			displayItemLowId = shellDisplay.DisplayItemLowId;
			displayItemHighId = shellDisplay.DisplayItemHighId;
			displayQuality = shellDisplay.DisplayQuality;
		}
		else if (PetSummonNanoCatalog.TryGetBureaucratProfile(nanoId, out profile))
		{
			displayQuality = profile.Level;
		}
		PetShellDefinition definition2 = new PetShellDefinition(definition.Kind, displayItemLowId, displayQuality, nanoId, summonParams.PetHash, summonParams.PetTypeId, displayItemHighId);
		return TryGiveShell(character, definition2);
	}

	public bool TryGiveShell(ICharacter character, PetShellKind kind)
	{
		if (!PetShellCatalog.TryGet(kind, out var definition))
		{
			return false;
		}
		return TryGiveShell(character, definition);
	}

	public bool TryUsePetShell(ICharacter character, Identity itemPosition, Item item)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || item == null || IsNanoCrystalItem(item))
		{
			return false;
		}
		if (!TryResolveDefinition(character, itemPosition, item, out var definition))
		{
			return false;
		}
		int num = PetSlotClassifier.ResolveStrain(definition.PetHash);
		if (num == 1015 && PetRuntimeService.Default.HasLivingAttackPet(character))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You can have just 1 Attack Pet.", 0, 0);
			return true;
		}
		if (num == 1016 && PetRuntimeService.Default.HasLivingHealingPet(character))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You can have just 1 Heal Pet.", 0, 0);
			return true;
		}
		if (PetSlotClassifier.IsBureaucratCompanionStrain(num) && PetRuntimeService.Default.HasLivingBureaucratCompanionPet(character))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You can have just 1 Bureaucrat Companion Pet.", 0, 0);
			return true;
		}
		bool flag = PetRuntimeService.Default.SummonPet(character, definition.PetHash, definition.PetTypeId, PetSlotClassifier.ResolveStrain(definition.PetHash), definition.NanoId);
		if (!flag)
		{
			return true;
		}
		ConsumeShell(character, itemPosition);
		LogUtil.Debug((DebugInfoDetail)256, $"UsePetShell kind={definition.Kind} owner={((IEntity)character).Identity} slot={((Identity)(ref itemPosition)).Instance} item={item.LowID} hash={definition.PetHash} type={definition.PetTypeId} ok={flag}");
		return true;
	}

	public void ConsumeShell(ICharacter character, Identity itemPosition)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected I4, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected I4, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected I4, but got Unknown
		if (character != null && ((IItemContainer)character).BaseInventory != null)
		{
			Identity identity = ((IEntity)character).Identity;
			PetShellKey key = new PetShellKey(((Identity)(ref identity)).Instance, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
			shellsBySlot.TryRemove(key, out var _);
			((IItemContainer)character).BaseInventory.RemoveItem((int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
			((IItemContainer)character).BaseInventory.Write();
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		}
	}

	public void GiveShellAfterNanoRestore(ICharacter character, int nanoId)
	{
		if (character != null && NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId) && PetShellCatalog.UsesShellOnSummon(((IStats)character).Stats[(StatIds)60].Value, nanoId) && !CharacterAlreadyHasShell(character, nanoId))
		{
			TryGiveShellForNano(character, nanoId);
		}
	}

	public void RegisterInventoryShells(ICharacter character)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IItemContainer)character).BaseInventory == null || ((IItemContainer)character).BaseInventory.Pages == null)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			IInventoryPage value = page.Value;
			if (value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item in value.List())
			{
				IItem value2 = item.Value;
				Item val = (Item)(object)((value2 is Item) ? value2 : null);
				if (val != null && IsPetShellItem(val))
				{
					int key = item.Key;
					if (TryBuildDefinitionFromShellItem(character, val, out var definition))
					{
						shellsBySlot[new PetShellKey(instance, page.Key, key)] = definition;
					}
				}
			}
		}
	}

	private bool TryGiveShell(ICharacter character, PetShellDefinition definition)
	{
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Invalid comparison between Unknown and I4
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IItemContainer)character).BaseInventory == null || definition == null)
		{
			return false;
		}
		if (CharacterAlreadyHasShell(character, definition.NanoId))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You already have a pet shell.", 0, 0);
			return false;
		}
		PetShellDisplayItemCatalog.EnsureRegistered(definition.DisplayItemLowId, definition.DisplayItemHighId, definition.NanoId);
		if (!ItemLoader.ItemList.ContainsKey(definition.DisplayItemLowId) || !ItemLoader.ItemList.ContainsKey(definition.DisplayItemHighId))
		{
			LogUtil.Debug((DebugInfoDetail)256, $"GivePetShell missing item template low={definition.DisplayItemLowId} high={definition.DisplayItemHighId}");
			return false;
		}
		EnsureNanoUploaded(character, definition.NanoId);
		int standardPage = ((IItemContainer)character).BaseInventory.StandardPage;
		IInventoryPage val = ((IItemContainer)character).BaseInventory.Pages[standardPage];
		int num = val.FindFreeSlot();
		if (num < 0)
		{
			return false;
		}
		Item val2 = new Item(definition.DisplayQuality, definition.DisplayItemLowId, definition.DisplayItemHighId);
		InventoryError val3 = ((IItemContainer)character).BaseInventory.AddToPage(standardPage, num, (IItem)(object)val2);
		if ((int)val3 > 0)
		{
			return false;
		}
		((IItemContainer)character).BaseInventory.Write();
		Identity identity = ((IEntity)character).Identity;
		PetShellKey key = new PetShellKey(((Identity)(ref identity)).Instance, standardPage, num);
		shellsBySlot[key] = definition;
		BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(character, val2);
		LogUtil.Debug((DebugInfoDetail)256, $"GivePetShell kind={definition.Kind} owner={((IEntity)character).Identity} slot={num} item={definition.DisplayItemLowId} ql={definition.DisplayQuality} hash={definition.PetHash} nano={definition.NanoId}");
		return true;
	}

	private bool CharacterAlreadyHasShell(ICharacter character, int nanoId = 0)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return false;
		}
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<PetShellKey, PetShellDefinition> item in shellsBySlot)
		{
			if (item.Key.OwnerInstance != instance || (nanoId > 0 && item.Value.NanoId != nanoId))
			{
				continue;
			}
			return true;
		}
		if (((IItemContainer)character).BaseInventory == null || ((IItemContainer)character).BaseInventory.Pages == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			IInventoryPage value = page.Value;
			if (value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item2 in value.List())
			{
				IItem value2 = item2.Value;
				Item val = (Item)(object)((value2 is Item) ? value2 : null);
				if (val != null && IsPetShellItem(val))
				{
					if (nanoId <= 0)
					{
						return true;
					}
					if (TryBuildDefinitionFromShellItem(character, val, out var definition) && definition.NanoId == nanoId)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool TryResolveDefinition(ICharacter character, Identity itemPosition, Item item, out PetShellDefinition definition)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected I4, but got Unknown
		Identity identity = ((IEntity)character).Identity;
		PetShellKey key = new PetShellKey(((Identity)(ref identity)).Instance, (int)((Identity)(ref itemPosition)).Type, ((Identity)(ref itemPosition)).Instance);
		if (shellsBySlot.TryGetValue(key, out definition))
		{
			return true;
		}
		if (!TryBuildDefinitionFromShellItem(character, item, out definition))
		{
			definition = null;
			return false;
		}
		shellsBySlot[key] = definition;
		return true;
	}

	private bool TryBuildDefinitionFromShellItem(ICharacter character, Item item, out PetShellDefinition definition)
	{
		definition = null;
		if (character == null || item == null)
		{
			return false;
		}
		if (!PetSummonNanoCatalog.TryResolveShellSummonForItem(character, item.LowID, item.HighID, item.Quality, ((IStats)character).Stats[(StatIds)60].Value, out var summonParams))
		{
			return false;
		}
		if (!PetShellCatalog.TryGetByDisplayLowId(item.LowID, out var definition2) && !PetShellCatalog.TryGetBureaucratFallback(out definition2))
		{
			return false;
		}
		definition = new PetShellDefinition(definition2.Kind, item.LowID, item.Quality, summonParams.NanoId, summonParams.PetHash, summonParams.PetTypeId, item.HighID);
		return true;
	}

	public static bool IsPetShellItem(Item item)
	{
		return item != null && !IsNanoCrystalItem(item) && IsDisplayShellItem(item.LowID);
	}

	public static bool IsDisplayShellItem(int lowId)
	{
		if (PetSummonNanoCatalog.IsBureaucratShellItemLowId(lowId))
		{
			return true;
		}
		PetShellDefinition definition;
		return PetShellCatalog.TryGetByDisplayLowId(lowId, out definition);
	}

	private static bool IsNanoCrystalItem(Item item)
	{
		if (item == null || item.Events == null)
		{
			return false;
		}
		foreach (Event item2 in item.Events.Where((Event x) => (int)x.EventType == 0))
		{
			foreach (Function function in item2.Functions)
			{
				if (function.FunctionType == 53019)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool TryEnsureNanoUploaded(ICharacter character, int nanoId)
	{
		EnsureNanoUploaded(character, nanoId);
		return character != null && character.UploadedNanos.Any((IUploadedNanos x) => x.NanoId == nanoId);
	}

	private void EnsureNanoUploaded(ICharacter character, int nanoId)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected O, but got Unknown
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val != null && !val.UploadedNanos.Any((IUploadedNanos x) => x.NanoId == nanoId) && NanoLoader.NanoList.ContainsKey(nanoId))
		{
			UploadedNano val2 = new UploadedNano
			{
				NanoId = nanoId
			};
			val.UploadedNanos.Add((IUploadedNanos)(object)val2);
			UploadedNanosDao instance = Dao<DBUploadedNano, UploadedNanosDao>.Instance;
			Identity identity = ((PooledObject)val).Identity;
			instance.WriteNano(((Identity)(ref identity)).Instance, (IUploadedNanos)(object)val2);
			if (((Dynel)val).Controller != null && ((Dynel)val).Controller.Client != null)
			{
				CharacterActionMessage val3 = new CharacterActionMessage
				{
					Identity = ((IEntity)character).Identity,
					Action = (CharacterActionType)204,
					Target = ((IEntity)character).Identity,
					Parameter1 = 53019,
					Parameter2 = nanoId,
					Unknown = 0
				};
				((Dynel)val).Controller.Client.SendCompressed((MessageBody)(object)val3);
			}
		}
	}
}
