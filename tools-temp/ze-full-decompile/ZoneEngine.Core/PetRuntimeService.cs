using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core;

public sealed class PetRuntimeService
{
	private struct PetSlotKey : IEquatable<PetSlotKey>
	{
		public int OwnerInstance { get; private set; }

		public int Strain { get; private set; }

		public PetSlotKey(int ownerInstance, int strain)
		{
			OwnerInstance = ownerInstance;
			Strain = strain;
		}

		public bool Equals(PetSlotKey other)
		{
			return OwnerInstance == other.OwnerInstance && Strain == other.Strain;
		}

		public override bool Equals(object obj)
		{
			if (obj is PetSlotKey)
			{
				return Equals((PetSlotKey)obj);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (OwnerInstance * 397) ^ Strain;
		}
	}

	private sealed class PendingPetRestore
	{
		public string PetHash { get; set; }

		public int PetTypeId { get; set; }

		public int PetSlotStrain { get; set; }

		public int SummonNanoId { get; set; }

		public bool ShouldRestore { get; set; }

		public bool HasSavedCombatStats { get; set; }

		public int SavedHealth { get; set; }

		public int SavedCurrentNano { get; set; }
	}

	private static readonly PetRuntimeService DefaultInstance = new PetRuntimeService();

	private readonly ConcurrentDictionary<PetSlotKey, Identity> activePetBySlot = new ConcurrentDictionary<PetSlotKey, Identity>();

	private readonly ConcurrentDictionary<PetSlotKey, PendingPetRestore> pendingRestoreBySlot = new ConcurrentDictionary<PetSlotKey, PendingPetRestore>();

	private readonly ConcurrentDictionary<int, DateTime> nextPetHealthRegenUtc = new ConcurrentDictionary<int, DateTime>();

	private readonly ConcurrentDictionary<int, DateTime> nextPetNanoRegenUtc = new ConcurrentDictionary<int, DateTime>();

	private const int SummonCaptureStatIdA = 1189;

	private const int SummonCaptureStatIdB = 1184;

	public static PetRuntimeService Default => DefaultInstance;

	private PetRuntimeService()
	{
	}

	public bool HasLivingAttackPet(ICharacter owner)
	{
		ICharacter activePetInStrain = GetActivePetInStrain(owner, 1015);
		return activePetInStrain != null && ((IStats)activePetInStrain).Stats[(StatIds)27].Value > 0;
	}

	public bool HasLivingHealingPet(ICharacter owner)
	{
		ICharacter activePetInStrain = GetActivePetInStrain(owner, 1016);
		return activePetInStrain != null && ((IStats)activePetInStrain).Stats[(StatIds)27].Value > 0;
	}

	public bool HasLivingBureaucratCompanionPet(ICharacter owner)
	{
		ICharacter activePetInStrain = GetActivePetInStrain(owner, 1017);
		return activePetInStrain != null && ((IStats)activePetInStrain).Stats[(StatIds)27].Value > 0;
	}

	public bool SummonPet(ICharacter owner, string petHash, int petTypeId, int petSlotStrain = 0, int summonNanoId = 0)
	{
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_0401: Unknown result type (might be due to invalid IL or missing references)
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_0483: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_071a: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05de: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_066a: Unknown result type (might be due to invalid IL or missing references)
		//IL_066f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0672: Unknown result type (might be due to invalid IL or missing references)
		//IL_067d: Unknown result type (might be due to invalid IL or missing references)
		//IL_067e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0683: Unknown result type (might be due to invalid IL or missing references)
		//IL_0691: Unknown result type (might be due to invalid IL or missing references)
		//IL_069f: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b2: Expected O, but got Unknown
		//IL_06b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c0: Expected O, but got Unknown
		//IL_0635: Unknown result type (might be due to invalid IL or missing references)
		//IL_0643: Unknown result type (might be due to invalid IL or missing references)
		//IL_0791: Unknown result type (might be due to invalid IL or missing references)
		//IL_07bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_07db: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0866: Unknown result type (might be due to invalid IL or missing references)
		//IL_0875: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null || string.IsNullOrWhiteSpace(petHash))
		{
			return false;
		}
		petSlotStrain = PetSlotClassifier.ResolveStrain(petHash);
		ActiveNanoRuntimeService.Default.PurgeOrphanSummonNanoInStrain(owner, petSlotStrain, notifyClient: true);
		string text = ((summonNanoId > 0) ? PetSummonNanoCatalog.GetPreferredPetHash(summonNanoId) : null);
		string text2 = PetMobTemplateResolver.Resolve(petHash, text);
		if (string.IsNullOrWhiteSpace(text2))
		{
			LogUtil.Debug((DebugInfoDetail)256, "SummonPet unknown pet hash " + petHash);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "Pet spawn failed: missing mob template for " + petHash + ".", 0, 0);
			return false;
		}
		DBMobTemplate mobTemplateByHash = Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(text2);
		if (mobTemplateByHash == null || string.IsNullOrWhiteSpace(mobTemplateByHash.Name))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "Pet spawn failed: mob template " + text2 + " has no data in MySQL.", 0, 0);
			return false;
		}
		LogUtil.Debug((DebugInfoDetail)256, $"SummonPet template hash={text2} name={mobTemplateByHash.Name} monsterData={mobTemplateByHash.MonsterData} npcFamily={mobTemplateByHash.NPCFamily}");
		if (petSlotStrain == 1015 && HasLivingAttackPet(owner))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "You can have just 1 Attack Pet.", 0, 0);
			return false;
		}
		if (petSlotStrain == 1016 && HasLivingHealingPet(owner))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "You can have just 1 Heal Pet.", 0, 0);
			return false;
		}
		if (PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain) && HasLivingBureaucratCompanionPet(owner))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "You can have just 1 Bureaucrat Companion Pet.", 0, 0);
			return false;
		}
		PurgeOrphanPetMobsForOwner(owner);
		Identity identity = ((IEntity)owner).Identity;
		bool flag = HasPendingRestoreForStrain(((Identity)(ref identity)).Instance, petSlotStrain);
		DismissPetByStrain(owner, petSlotStrain, !flag);
		Coordinate val = ResolvePetSpawnCoordinate(owner, petSlotStrain);
		NPCController nPCController = new NPCController();
		int num = ((petTypeId <= 0) ? 1 : petTypeId);
		int value = ((IStats)owner).Stats[(StatIds)54].Value;
		int num2;
		if (!PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain))
		{
			num2 = ((petSlotStrain != 1015 || num <= 0) ? value : num);
		}
		else
		{
			num2 = PetSummonNanoCatalog.ResolveBureaucratCompanionLevel(summonNanoId, value, num);
			num = num2;
		}
		Character val2 = NonPlayerCharacterHandler.SpawnMobFromTemplate(text2, ((IEntity)((IInstancedEntity)owner).Playfield).Identity, val, ((IDynel)owner).RawHeading, (IController)(object)nPCController, num2);
		if (val2 == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, "Pet spawn failed: could not create pet mob.", 0, 0);
			return false;
		}
		((Dynel)val2).Playfield = ((IInstancedEntity)owner).Playfield;
		FinalizePetCharacter(val2);
		ApplyAttackPetCombatProfile(val2, petHash, num, mobTemplateByHash);
		ApplyCapturedBureaucratPetProfile(val2, summonNanoId, owner, num);
		if (PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain))
		{
			PetBureaucratCompanionAppearance.Apply(val2, mobTemplateByHash);
		}
		else if (PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
		{
			PetBureaucratGuardianAppearance.Apply(val2, summonNanoId);
		}
		ApplyHealingPetNanoPool(val2, petSlotStrain, text ?? petHash);
		ApplyRestoredPetCombatStats(owner, val2, petSlotStrain);
		IStat obj = ((Dynel)val2).Stats[(StatIds)196];
		identity = ((IEntity)owner).Identity;
		obj.Value = ((Identity)(ref identity)).Instance;
		((Dynel)val2).Stats[(StatIds)512].Value = num;
		((Dynel)val2).Stats[(StatIds)671].Value = 2304001;
		((Dynel)val2).DoNotDoTimers = false;
		ZoneClient zoneClient = ((((IDynel)owner).Controller != null) ? (((IDynel)owner).Controller.Client as ZoneClient) : null);
		if (zoneClient == null)
		{
			DespawnUnlinkedPetMob(val2);
			return false;
		}
		SimpleCharFullUpdateMessage val3 = ConstructPetSpawnFullUpdate(val2, petSlotStrain, summonNanoId, owner, petHash);
		ServerBase server = ((ClientBase)zoneClient).Server;
		object[] obj2 = new object[5]
		{
			((IEntity)owner).Identity,
			((PooledObject)val2).Identity,
			null,
			null,
			null
		};
		identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
		obj2[2] = ((Identity)(ref identity)).Instance;
		obj2[3] = summonNanoId;
		obj2[4] = petSlotStrain;
		server.Info((IClient)(object)zoneClient, "SummonPet path=capture-scfu+serializer-link owner={0} pet={1} playfield={2} nano={3} strain={4}", obj2);
		if (summonNanoId > 0 && petSlotStrain == 1016)
		{
			PetSummonCaptureWireReplayer.SendHealingPetScfuToOwner(zoneClient, owner, val2, petHash, text ?? petHash);
			SendPetStatToOwner(zoneClient, ((PooledObject)val2).Identity, (StatIds)196, (uint)((Dynel)val2).Stats[(StatIds)196].Value);
			BaseMessageHandler<AddPetMessage, AddPetMessageHandler>.Default.SendAddPet(owner, ((PooledObject)val2).Identity);
			SendPetStatToOwner(zoneClient, ((PooledObject)val2).Identity, (StatIds)0, (uint)((Dynel)val2).Stats[(StatIds)0].Value);
			SendBelamorteSummonPostLink(zoneClient, owner, val2, summonNanoId, petHash, petTypeId);
			SendPetCombatStatSyncToOwner(zoneClient, val2, petSlotStrain);
		}
		else
		{
			if (PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
			{
				PetBureaucratGuardianScfuWire.SendToOwner(zoneClient, owner, val2, summonNanoId);
			}
			else
			{
				zoneClient.SendCompressed((MessageBody)(object)val3);
			}
			if (PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain) || PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
			{
				if (PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
				{
					WeaponItemFullUpdateMessage val4 = WeaponItemFullUpdate.CreateRightHandWeaponDefinitionMessage((ICharacter)(object)val2);
					if (val4 != null)
					{
						zoneClient.SendCompressed((MessageBody)(object)val4);
					}
				}
				else
				{
					WeaponItemFullUpdateMessage[] array = WeaponItemFullUpdate.CreateWeaponDefinitionMessages((ICharacter)(object)val2);
					foreach (WeaponItemFullUpdateMessage messageBody in array)
					{
						zoneClient.SendCompressed((MessageBody)(object)messageBody);
					}
				}
			}
			SendPetStatToOwner(zoneClient, ((PooledObject)val2).Identity, (StatIds)196, (uint)((Dynel)val2).Stats[(StatIds)196].Value);
			BaseMessageHandler<AddPetMessage, AddPetMessageHandler>.Default.SendAddPet(owner, ((PooledObject)val2).Identity);
			SendPetStatToOwner(zoneClient, ((PooledObject)val2).Identity, (StatIds)0, (uint)((Dynel)val2).Stats[(StatIds)0].Value);
			if (summonNanoId > 0)
			{
				PetSummonSpellListService.SendOwnerPetSummon(owner, summonNanoId, petHash, petTypeId, petSlotStrain);
				SendPostSummonPetStatsToOwner(zoneClient, val2);
				SendPetWantedDirectionToOwner(zoneClient, ((PooledObject)val2).Identity);
				PetSummonSpellListService.SendPetSummonSpellLists(owner, ((PooledObject)val2).Identity, petSlotStrain, petHash);
			}
			SendPetCombatStatSyncToOwner(zoneClient, val2, petSlotStrain);
			Coordinate val5 = ((Dynel)val2).Coordinates();
			zoneClient.SendCompressed((MessageBody)new SetPosMessage
			{
				Identity = ((PooledObject)val2).Identity,
				Coordinates = new Vector3
				{
					X = val5.x,
					Y = val5.y,
					Z = val5.z
				},
				Unknown1 = 1
			});
		}
		if (((IInstancedEntity)owner).Playfield is Playfield playfield)
		{
			playfield.ActivateNpc((ICharacter)(object)val2);
			playfield.RegisterNpcHome((ICharacter)(object)val2);
			playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val2, ((IEntity)owner).Identity);
		}
		if (!PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
		{
			((IInstancedEntity)owner).Playfield.AnnounceOthers((MessageBody)(object)val3, ((IEntity)owner).Identity);
		}
		if (PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain) || PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
		{
			if (PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
			{
				WeaponItemFullUpdate.SendRightHandWeaponDefinition((ICharacter)(object)val2, announceToPlayfield: true);
			}
			else
			{
				WeaponItemFullUpdate.SendWeaponDefinitions((ICharacter)(object)val2, announceToPlayfield: true);
			}
		}
		if (summonNanoId > 0 && !HasActiveSummonPetNanoInStrain(owner, petSlotStrain))
		{
			RegisterOwnerSummonNano(owner, summonNanoId, petSlotStrain);
		}
		nPCController.Follow(((IEntity)owner).Identity, PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain) ? 4.0 : 2.0);
		identity = ((IEntity)owner).Identity;
		PetSlotKey key = new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain);
		activePetBySlot[key] = ((PooledObject)val2).Identity;
		pendingRestoreBySlot[key] = new PendingPetRestore
		{
			PetHash = petHash,
			PetTypeId = petTypeId,
			PetSlotStrain = petSlotStrain,
			SummonNanoId = summonNanoId,
			ShouldRestore = true
		};
		SyncOwnerPetCounter(owner);
		IZoneClient val6 = ((((IDynel)owner).Controller != null) ? ((IDynel)owner).Controller.Client : null);
		if (val6 != null)
		{
			((IClient)val6).Server.Info((IClient)(object)val6, "SummonPet owner={0} pet={1} hash={2} strain={3} nano={4}", new object[5]
			{
				((IEntity)owner).Identity,
				((PooledObject)val2).Identity,
				petHash,
				petSlotStrain,
				summonNanoId
			});
		}
		LogUtil.Debug((DebugInfoDetail)256, $"SummonPet ok owner={((IEntity)owner).Identity} pet={((PooledObject)val2).Identity} hash={petHash} strain={petSlotStrain} nano={summonNanoId} type={num} level={num2}");
		return true;
	}

	public void DismissPet(ICharacter owner)
	{
		DismissAllPets(owner, clearRestoreState: true);
	}

	public void DismissPetByStrain(ICharacter owner, int petSlotStrain)
	{
		DismissPetByStrain(owner, petSlotStrain, clearRestoreState: true);
	}

	public void StashPetForZoneTransfer(ICharacter owner)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return;
		}
		Identity identity = ((IEntity)owner).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(instance))
		{
			if (!pendingRestoreBySlot.TryGetValue(item.Key, out var value))
			{
				value = new PendingPetRestore
				{
					PetSlotStrain = item.Key.Strain
				};
			}
			value.ShouldRestore = true;
			ICharacter activePetInStrain = GetActivePetInStrain(owner, item.Key.Strain);
			if (activePetInStrain != null && ((IStats)activePetInStrain).Stats[(StatIds)27].Value > 0)
			{
				value.SavedHealth = ((IStats)activePetInStrain).Stats[(StatIds)27].Value;
				value.SavedCurrentNano = ((IStats)activePetInStrain).Stats[(StatIds)214].Value;
				value.HasSavedCombatStats = true;
			}
			pendingRestoreBySlot[item.Key] = value;
			LogUtil.Debug((DebugInfoDetail)256, $"StashPet owner={((IEntity)owner).Identity} hash={value.PetHash} type={value.PetTypeId} strain={value.PetSlotStrain} hp={(value.HasSavedCombatStats ? value.SavedHealth : (-1))} np={(value.HasSavedCombatStats ? value.SavedCurrentNano : (-1))}");
		}
		PurgeOrphanPetMobsForOwner(owner);
		DismissAllPets(owner, clearRestoreState: false);
	}

	public void TryRestorePetAfterZoneIn(ICharacter owner)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null)
		{
			return;
		}
		PetShellItemService.Default.RegisterInventoryShells(owner);
		PurgeOrphanPetMobsForOwner(owner);
		Identity identity = ((IEntity)owner).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<PetSlotKey, PendingPetRestore> item in GetPendingSlotsForOwner(instance))
		{
			PendingPetRestore value = item.Value;
			if (value.ShouldRestore && !string.IsNullOrWhiteSpace(value.PetHash))
			{
				if (!HasActiveSummonPetNanoInStrain(owner, value.PetSlotStrain))
				{
					pendingRestoreBySlot.TryRemove(item.Key, out var _);
					continue;
				}
				LogUtil.Debug((DebugInfoDetail)256, $"RestorePet owner={((IEntity)owner).Identity} hash={value.PetHash} type={value.PetTypeId} strain={value.PetSlotStrain}");
				SummonPet(owner, value.PetHash, value.PetTypeId, value.PetSlotStrain, value.SummonNanoId);
			}
		}
		SyncOwnerPetCounter(owner);
		ActiveNanoRuntimeService.Default.CleanupOrphanSummonPetNanosAfterPetRestore(owner);
	}

	public bool HasPendingRestore(int ownerInstance)
	{
		return pendingRestoreBySlot.Any((KeyValuePair<PetSlotKey, PendingPetRestore> x) => x.Key.OwnerInstance == ownerInstance && x.Value.ShouldRestore && !string.IsNullOrWhiteSpace(x.Value.PetHash));
	}

	public void OnCharacterDisconnected(ICharacter owner, bool preservePendingRestore)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return;
		}
		Identity identity = ((IEntity)owner).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (preservePendingRestore)
		{
			foreach (PetSlotKey item in (from x in GetActiveSlotsForOwner(instance)
				select x.Key).ToList())
			{
				activePetBySlot.TryRemove(item, out var _);
			}
			return;
		}
		DismissAllPets(owner, clearRestoreState: true);
		ClearPendingRestoreForOwner(instance);
	}

	public void ClearPendingRestoreForOwner(int ownerInstance)
	{
		foreach (PetSlotKey item in pendingRestoreBySlot.Keys.Where((PetSlotKey k) => k.OwnerInstance == ownerInstance).ToList())
		{
			pendingRestoreBySlot.TryRemove(item, out var _);
		}
	}

	public bool HasPendingRestoreForStrain(int ownerInstance, int petSlotStrain)
	{
		if (!pendingRestoreBySlot.TryGetValue(new PetSlotKey(ownerInstance, petSlotStrain), out var value))
		{
			return false;
		}
		return value.ShouldRestore && !string.IsNullOrWhiteSpace(value.PetHash);
	}

	public bool HasActivePetInStrain(ICharacter owner, int petSlotStrain)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return false;
		}
		ConcurrentDictionary<PetSlotKey, Identity> concurrentDictionary = activePetBySlot;
		Identity identity = ((IEntity)owner).Identity;
		return concurrentDictionary.ContainsKey(new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain));
	}

	public ICharacter GetActivePetInStrain(ICharacter owner, int petSlotStrain)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null)
		{
			return null;
		}
		ConcurrentDictionary<PetSlotKey, Identity> concurrentDictionary = activePetBySlot;
		Identity identity = ((IEntity)owner).Identity;
		if (!concurrentDictionary.TryGetValue(new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain), out var value))
		{
			return null;
		}
		return ((IInstancedEntity)owner).Playfield.FindByIdentity<ICharacter>(value);
	}

	public IEnumerable<int> GetActivePetStrains(ICharacter owner)
	{
		if (owner == null)
		{
			yield break;
		}
		Identity identity = ((IEntity)owner).Identity;
		int ownerInstance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(ownerInstance))
		{
			yield return item.Key.Strain;
		}
	}

	public bool TryGetSummonNanoId(ICharacter owner, ICharacter pet, out int summonNanoId)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		summonNanoId = 0;
		if (owner == null || pet == null)
		{
			return false;
		}
		Identity val = ((IEntity)owner).Identity;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(((Identity)(ref val)).Instance))
		{
			val = item.Value;
			int instance = ((Identity)(ref val)).Instance;
			val = ((IEntity)pet).Identity;
			if (instance != ((Identity)(ref val)).Instance || !pendingRestoreBySlot.TryGetValue(item.Key, out var value) || value.SummonNanoId <= 0)
			{
				continue;
			}
			summonNanoId = value.SummonNanoId;
			return true;
		}
		return false;
	}

	public bool TryGetHealNanoId(ICharacter owner, ICharacter pet, out int healNanoId)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		healNanoId = 0;
		if (owner == null || pet == null)
		{
			return false;
		}
		string text = null;
		Identity val = ((IEntity)owner).Identity;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(((Identity)(ref val)).Instance))
		{
			val = item.Value;
			int instance = ((Identity)(ref val)).Instance;
			val = ((IEntity)pet).Identity;
			if (instance != ((Identity)(ref val)).Instance || !pendingRestoreBySlot.TryGetValue(item.Key, out var value))
			{
				continue;
			}
			int summonNanoId = value.SummonNanoId;
			text = value.PetHash;
			return PetHealNanoCatalog.TryResolveHealNano(summonNanoId, text, out healNanoId);
		}
		return false;
	}

	public void TerminatePetByIdentity(ICharacter owner, Identity petIdentity)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((Identity)(ref petIdentity)).Instance == 0)
		{
			return;
		}
		Identity val = ((IEntity)owner).Identity;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(((Identity)(ref val)).Instance))
		{
			val = item.Value;
			if (((Identity)(ref val)).Instance == ((Identity)(ref petIdentity)).Instance)
			{
				TerminatePetByStrain(owner, item.Key.Strain);
				break;
			}
		}
	}

	public void TerminatePetByStrain(ICharacter owner, int petSlotStrain)
	{
		if (owner != null && petSlotStrain > 0)
		{
			if (owner.ActiveNanos.TryGetValue(petSlotStrain, out var value) && value != null && NanoEventRuntimeService.Default.HasSummonPetOnUse(value.ID))
			{
				ActiveNanoRuntimeService.Default.RemoveActiveNanoInStrain(owner, petSlotStrain, notifyClient: true);
			}
			else
			{
				DismissPetByStrain(owner, petSlotStrain, clearRestoreState: true);
			}
		}
	}

	private void DismissPetByStrain(ICharacter owner, int petSlotStrain, bool clearRestoreState)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return;
		}
		Identity identity = ((IEntity)owner).Identity;
		PetSlotKey key = new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain);
		if (!activePetBySlot.TryRemove(key, out var value))
		{
			if (clearRestoreState)
			{
				pendingRestoreBySlot.TryRemove(key, out var _);
				SyncOwnerPetCounter(owner);
			}
			return;
		}
		if (clearRestoreState)
		{
			pendingRestoreBySlot.TryRemove(key, out var _);
		}
		DespawnPetIdentity(owner, value);
		if (clearRestoreState)
		{
			SyncOwnerPetCounter(owner);
		}
	}

	private void DismissAllPets(ICharacter owner, bool clearRestoreState)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return;
		}
		Identity identity = ((IEntity)owner).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		foreach (KeyValuePair<PetSlotKey, Identity> item in GetActiveSlotsForOwner(instance).ToList())
		{
			if (activePetBySlot.TryRemove(item.Key, out var value))
			{
				if (clearRestoreState)
				{
					pendingRestoreBySlot.TryRemove(item.Key, out var _);
				}
				DespawnPetIdentity(owner, value);
			}
		}
		if (clearRestoreState)
		{
			SyncOwnerPetCounter(owner);
		}
	}

	private void DespawnPetIdentity(ICharacter owner, Identity petIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<RemovePetMessage, RemovePetMessageHandler>.Default.SendRemovePet(owner, petIdentity);
		Character val = ResolvePetCharacter(owner, petIdentity);
		if (val != null)
		{
			if (((Dynel)val).Playfield is Playfield playfield)
			{
				playfield.DespawnNpcImmediately((ICharacter)(object)val);
				return;
			}
			if (((Dynel)val).Playfield != null)
			{
				((Dynel)val).Playfield.Announce((MessageBody)(object)BaseMessageHandler<DespawnMessage, DespawnMessageHandler>.Default.Create(petIdentity));
			}
			Pool.Instance.RemoveObject<Character>(val);
		}
		else if (((IInstancedEntity)owner).Playfield != null)
		{
			((IInstancedEntity)owner).Playfield.Announce((MessageBody)(object)BaseMessageHandler<DespawnMessage, DespawnMessageHandler>.Default.Create(petIdentity));
		}
	}

	private Character ResolvePetCharacter(ICharacter owner, Identity petIdentity)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && ((IInstancedEntity)owner).Playfield != null)
		{
			Character val = ((IInstancedEntity)owner).Playfield.FindByIdentity<Character>(petIdentity);
			if (val != null)
			{
				return val;
			}
		}
		return Pool.Instance.GetObject<Character>(petIdentity);
	}

	private void PurgeOrphanPetMobsForOwner(ICharacter owner)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null)
		{
			return;
		}
		Identity identity = ((IEntity)owner).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		HashSet<int> hashSet = new HashSet<int>(GetActiveSlotsForOwner(instance).Select(delegate(KeyValuePair<PetSlotKey, Identity> x)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			Identity value = x.Value;
			return ((Identity)(ref value)).Instance;
		}));
		foreach (Character item in Pool.Instance.GetAll<Character>(50000).ToList())
		{
			if (item == null)
			{
				continue;
			}
			identity = ((PooledObject)item).Identity;
			if (((Identity)(ref identity)).Instance != instance && ((Dynel)item).Stats[(StatIds)196].Value == instance)
			{
				identity = ((PooledObject)item).Identity;
				if (!hashSet.Contains(((Identity)(ref identity)).Instance))
				{
					DespawnPetIdentity(owner, ((PooledObject)item).Identity);
				}
			}
		}
	}

	private void DespawnUnlinkedPetMob(Character petCharacter)
	{
		if (petCharacter != null)
		{
			if (((Dynel)petCharacter).Playfield is Playfield playfield)
			{
				playfield.DespawnNpcImmediately((ICharacter)(object)petCharacter);
			}
			else
			{
				Pool.Instance.RemoveObject<Character>(petCharacter);
			}
		}
	}

	private IEnumerable<KeyValuePair<PetSlotKey, Identity>> GetActiveSlotsForOwner(int ownerInstance)
	{
		return activePetBySlot.Where((KeyValuePair<PetSlotKey, Identity> x) => x.Key.OwnerInstance == ownerInstance);
	}

	private IEnumerable<KeyValuePair<PetSlotKey, PendingPetRestore>> GetPendingSlotsForOwner(int ownerInstance)
	{
		return pendingRestoreBySlot.Where((KeyValuePair<PetSlotKey, PendingPetRestore> x) => x.Key.OwnerInstance == ownerInstance);
	}

	private void SyncOwnerPetCounter(ICharacter owner)
	{
	}

	private SimpleCharFullUpdateMessage ConstructPetSpawnFullUpdate(Character petCharacter, int petSlotStrain, int summonNanoId, ICharacter owner = null, string spawnPetHash = null)
	{
		SimpleCharFullUpdateMessage val = SimpleCharFullUpdate.ConstructMessage(petCharacter);
		if (petSlotStrain == 1016)
		{
			PetSummonScfuExtensions.ApplyCapturedMpPetMetadata(val, petSlotStrain, owner, spawnPetHash);
		}
		return val;
	}

	private void FinalizePetCharacter(Character petCharacter)
	{
		((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(673, 31u);
		((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(156, 737u);
		((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(389, 1u);
		int value = ((Dynel)petCharacter).Stats[(StatIds)54].Value;
		value = ((Dynel)petCharacter).Stats[(StatIds)17].Value;
		value = ((Dynel)petCharacter).Stats[(StatIds)64].Value;
		uint value2 = (uint)((Dynel)petCharacter).Stats[(StatIds)1].Value;
		((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(27, value2);
	}

	private Coordinate ResolvePetSpawnCoordinate(ICharacter owner, int petSlotStrain)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		Coordinate val = ((IDynel)owner).Coordinates();
		if (PetSlotClassifier.IsBureaucratCompanionStrain(petSlotStrain))
		{
			return new Coordinate(val.x - 1.75f, val.y, val.z + 2.25f);
		}
		if (HasLivingBureaucratCompanionPet(owner))
		{
			return new Coordinate(val.x + 1.75f, val.y, val.z + 1.25f);
		}
		return new Coordinate(val.x + 1.5f, val.y, val.z + 1.5f);
	}

	private void ApplyCapturedBureaucratPetProfile(Character petCharacter, int summonNanoId, ICharacter owner, int petTypeId)
	{
		if (petCharacter != null && summonNanoId > 0 && PetSummonNanoCatalog.TryGetBureaucratProfile(summonNanoId, out var profile))
		{
			int ownerLevel = ((owner != null) ? ((IStats)owner).Stats[(StatIds)54].Value : profile.Level);
			int num = PetSummonNanoCatalog.ResolveBureaucratCompanionLevel(summonNanoId, ownerLevel, petTypeId);
			int num2 = profile.Health;
			if (profile.Level > 0 && num != profile.Level)
			{
				num2 = (int)((long)profile.Health * (long)num / profile.Level);
			}
			((Dynel)petCharacter).Name = profile.Name;
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(54, (uint)num);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(1, (uint)num2);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(27, (uint)num2);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(359, (uint)profile.MonsterData);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(360, (uint)profile.MonsterScale);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(156, (uint)profile.RunSpeed);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(455, (uint)profile.NpcFamily);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(33, 2u);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(668, 2u);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(286, (uint)PetCombatRules.ResolveLevelEquivalentAttackPetMinDamage(num));
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(285, (uint)PetCombatRules.ResolveLevelEquivalentAttackPetMaxDamage(num));
			if (profile.HeadMesh > 0)
			{
				((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(64, (uint)profile.HeadMesh);
			}
		}
	}

	private void ApplyHealingPetNanoPool(Character petCharacter, int petSlotStrain, string petHash)
	{
		if (petSlotStrain == 1016)
		{
			if (!PetHealNanoCatalog.TryGetHealingPetNanoPool(petHash, out var currentNano, out var maxNano))
			{
				currentNano = 13184;
				maxNano = 13184;
			}
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(214, (uint)currentNano);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(221, (uint)maxNano);
		}
	}

	private void ApplyAttackPetCombatProfile(Character petCharacter, string petHash, int petTypeId, DBMobTemplate mobTemplate)
	{
		if (petCharacter != null && mobTemplate != null && PetAttackPetCombatCatalog.TryGet(petHash, out var profile))
		{
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(286, (uint)profile.MinDamage);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(285, (uint)profile.MaxDamage);
			if (petTypeId > 0)
			{
				int num = Math.Max(mobTemplate.MinLvl, Math.Min(petTypeId, mobTemplate.MaxLvl));
				((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(54, (uint)num);
			}
			uint num2 = (uint)Math.Max(1, mobTemplate.Health);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(1, num2);
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(27, num2);
		}
	}

	private void ApplyRestoredPetCombatStats(ICharacter owner, Character petCharacter, int petSlotStrain)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || petCharacter == null)
		{
			return;
		}
		ConcurrentDictionary<PetSlotKey, PendingPetRestore> concurrentDictionary = pendingRestoreBySlot;
		Identity identity = ((IEntity)owner).Identity;
		if (concurrentDictionary.TryGetValue(new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain), out var value) && value.HasSavedCombatStats)
		{
			int val = Math.Max(1, ((Dynel)petCharacter).Stats[(StatIds)1].Value);
			int num = Math.Max(0, Math.Min(value.SavedHealth, val));
			((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(27, (uint)num);
			if (petSlotStrain == 1016)
			{
				int num2 = Math.Max(0, value.SavedCurrentNano);
				((Dynel)petCharacter).Stats.SetBaseValueWithoutTriggering(214, (uint)num2);
			}
			value.HasSavedCombatStats = false;
			ConcurrentDictionary<PetSlotKey, PendingPetRestore> concurrentDictionary2 = pendingRestoreBySlot;
			identity = ((IEntity)owner).Identity;
			concurrentDictionary2[new PetSlotKey(((Identity)(ref identity)).Instance, petSlotStrain)] = value;
		}
	}

	private void SendPetCombatStatSyncToOwner(ZoneClient ownerClient, Character petCharacter, int petSlotStrain)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && petCharacter != null)
		{
			SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)27, (uint)Math.Max(0, ((Dynel)petCharacter).Stats[(StatIds)27].Value));
			if (petSlotStrain == 1016)
			{
				SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)214, (uint)Math.Max(0, ((Dynel)petCharacter).Stats[(StatIds)214].Value));
			}
		}
	}

	internal void SendPetStatToOwner(ZoneClient ownerClient, Identity petIdentity, StatIds statId, uint statValue)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null)
		{
			StatMessage val = new StatMessage();
			((N3Message)val).Identity = petIdentity;
			val.Stats = new GameTuple<CharacterStat, uint>[1]
			{
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)statId,
					Value2 = statValue
				}
			};
			ownerClient.SendCompressed((MessageBody)(object)val);
		}
	}

	internal void ProcessPetPassiveRegen(ICharacter pet)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		if (pet == null || !PetCombatRules.IsPlayerOwnedPet(pet))
		{
			return;
		}
		Identity identity = ((IEntity)pet).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (instance == 0)
		{
			return;
		}
		int value = ((IStats)pet).Stats[(StatIds)27].Value;
		if (value <= 0)
		{
			nextPetHealthRegenUtc.TryRemove(instance, out var value2);
			nextPetNanoRegenUtc.TryRemove(instance, out value2);
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		bool flag = false;
		bool flag2 = false;
		DateTime orAdd = nextPetHealthRegenUtc.GetOrAdd(instance, utcNow);
		if (utcNow >= orAdd)
		{
			int value3 = ((IStats)pet).Stats[(StatIds)1].Value;
			if (value < value3)
			{
				int num = PetCombatRules.ResolvePetHealthRegenDelta(value3);
				((IStats)pet).Stats[(StatIds)27].Value = Math.Min(value3, value + num);
				flag = true;
			}
			nextPetHealthRegenUtc[instance] = utcNow.AddSeconds(1.0);
		}
		if (PetCombatRules.IsPlayerOwnedHealingPet(pet))
		{
			DateTime orAdd2 = nextPetNanoRegenUtc.GetOrAdd(instance, utcNow);
			if (utcNow >= orAdd2)
			{
				int value4 = ((IStats)pet).Stats[(StatIds)214].Value;
				int value5 = ((IStats)pet).Stats[(StatIds)221].Value;
				if (value5 > 0 && value4 < value5)
				{
					int num2 = PetCombatRules.ResolvePetNanoRegenDelta(value5);
					if (num2 > 0)
					{
						int num3 = Math.Min(value5, value4 + num2);
						if (num3 != value4)
						{
							((IStats)pet).Stats[(StatIds)214].Value = num3;
							flag2 = true;
						}
					}
				}
				nextPetNanoRegenUtc[instance] = utcNow.AddSeconds(1.0);
			}
		}
		if (!flag && !flag2)
		{
			return;
		}
		ICharacter val = PetCombatRules.ResolvePetOwner(pet);
		ZoneClient zoneClient = ((val != null && ((IDynel)val).Controller != null) ? (((IDynel)val).Controller.Client as ZoneClient) : null);
		if (zoneClient != null)
		{
			if (flag)
			{
				SendPetStatToOwner(zoneClient, ((IEntity)pet).Identity, (StatIds)27, (uint)Math.Max(0, ((IStats)pet).Stats[(StatIds)27].Value));
			}
			if (flag2)
			{
				SendPetStatToOwner(zoneClient, ((IEntity)pet).Identity, (StatIds)214, (uint)Math.Max(0, ((IStats)pet).Stats[(StatIds)214].Value));
			}
		}
	}

	private void SendBelamorteSummonPostLink(ZoneClient ownerClient, ICharacter owner, Character petCharacter, int summonNanoId, string petHash, int petTypeId)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		PetSummonSpellListService.SendOwnerPetSummon(owner, summonNanoId, petHash, petTypeId, 1016);
		SendPostSummonPetStatsToOwner(ownerClient, petCharacter);
		SendPetWantedDirectionToOwner(ownerClient, ((PooledObject)petCharacter).Identity);
		PetSummonSpellListService.SendPetSummonSpellLists(owner, ((PooledObject)petCharacter).Identity, 1016, petHash);
	}

	private void SendPostSummonPetStatsToOwner(ZoneClient ownerClient, Character petCharacter)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)512, (uint)((Dynel)petCharacter).Stats[(StatIds)512].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)671, (uint)((Dynel)petCharacter).Stats[(StatIds)671].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)33, (uint)((Dynel)petCharacter).Stats[(StatIds)33].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)668, (uint)((Dynel)petCharacter).Stats[(StatIds)668].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)156, (uint)((Dynel)petCharacter).Stats[(StatIds)156].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)389, (uint)((Dynel)petCharacter).Stats[(StatIds)389].Value);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)1189, 0u);
		SendPetStatToOwner(ownerClient, ((PooledObject)petCharacter).Identity, (StatIds)1184, 0u);
	}

	private void SendPetWantedDirectionToOwner(ZoneClient ownerClient, Identity petIdentity)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_0057: Expected O, but got Unknown
		ownerClient?.SendCompressed((MessageBody)new SetWantedDirectionMessage
		{
			Identity = petIdentity,
			Unknown = 0,
			DirectinVector = new Vector3
			{
				X = 0f,
				Y = -1f,
				Z = 0f
			}
		});
	}

	private void RegisterOwnerSummonNano(ICharacter owner, int summonNanoId, int petSlotStrain)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && summonNanoId > 0 && ((IInstancedEntity)owner).Playfield != null && ActiveNanoRuntimeService.Default.ApplyActiveNano(owner, summonNanoId, 0, default(Identity), petSlotStrain))
		{
			LogUtil.Debug((DebugInfoDetail)256, $"RegisterOwnerSummonNano owner={((IEntity)owner).Identity} nano={summonNanoId} strain={petSlotStrain}");
		}
	}

	private bool HasActiveSummonPetNanoInStrain(ICharacter owner, int petSlotStrain)
	{
		if (!owner.ActiveNanos.TryGetValue(petSlotStrain, out var value) || value == null)
		{
			return false;
		}
		return NanoEventRuntimeService.Default.HasSummonPetOnUse(value.ID);
	}
}
