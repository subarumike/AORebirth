using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Controllers;

public class PlayerController : IController, IDisposable
{
	private WeakReference<ICharacter> character;

	private bool disposed = false;

	private CharacterState state = (CharacterState)0;

	public CharacterState State
	{
		get
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			return state;
		}
		set
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			state = value;
		}
	}

	public ICharacter Character
	{
		get
		{
			if (character == null)
			{
				return null;
			}
			return character.Target;
		}
		set
		{
			if (value == null)
			{
				throw new Exception("Dont try to weak reference null");
			}
			character = new WeakReference<ICharacter>(value);
		}
	}

	public IZoneClient Client { get; set; }

	public bool SaveToDatabase => true;

	public PlayerController(IZoneClient client)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		Client = client;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public void CallFunction(Function function, IEntity caller)
	{
		IInstancedEntity functionTarget;
		if (function != null && (function.FunctionType == 53016 || function.FunctionType == 53032))
		{
			FunctionCollection.Instance.CallFunction(function.FunctionType, (INamedEntity)(object)Character, caller, (IInstancedEntity)(object)Character, function.Arguments.Values.ToArray());
		}
		else if (TryResolveFunctionTarget(function, out functionTarget))
		{
			FunctionCollection.Instance.CallFunction(function.FunctionType, (INamedEntity)(object)Character, caller, functionTarget, function.Arguments.Values.ToArray());
		}
	}

	private bool TryResolveFunctionTarget(Function function, out IInstancedEntity functionTarget)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Invalid comparison between Unknown and I4
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Invalid comparison between Unknown and I4
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		functionTarget = (IInstancedEntity)(object)Character;
		if (function == null || Character == null || ((IInstancedEntity)Character).Playfield == null)
		{
			return functionTarget != null;
		}
		ItemTarget val = (ItemTarget)function.Target;
		ItemTarget val2 = val;
		Identity val3;
		if ((int)val2 != 3)
		{
			if ((int)val2 == 14)
			{
				val3 = ((ITargetingEntity)Character).FightingTarget;
				Identity val4 = ((((Identity)(ref val3)).Instance != 0) ? ((ITargetingEntity)Character).FightingTarget : ((ITargetingEntity)Character).SelectedTarget);
				if (((Identity)(ref val4)).Instance == 0)
				{
					return false;
				}
				functionTarget = ((IInstancedEntity)Character).Playfield.FindByIdentity(val4);
				return functionTarget != null;
			}
			if ((int)val2 != 23)
			{
				functionTarget = (IInstancedEntity)(object)Character;
				return true;
			}
		}
		Identity val5 = ((ITargetingEntity)Character).SelectedTarget;
		if (((Identity)(ref val5)).Instance != 0)
		{
			int instance = ((Identity)(ref val5)).Instance;
			val3 = ((IEntity)Character).Identity;
			if (instance != ((Identity)(ref val3)).Instance)
			{
				goto IL_00a4;
			}
		}
		val5 = ((ITargetingEntity)Character).FightingTarget;
		goto IL_00a4;
		IL_00a4:
		if (((Identity)(ref val5)).Instance != 0)
		{
			int instance2 = ((Identity)(ref val5)).Instance;
			val3 = ((IEntity)Character).Identity;
			if (instance2 != ((Identity)(ref val3)).Instance)
			{
				functionTarget = ((IInstancedEntity)Character).Playfield.FindByIdentity(val5);
				if (functionTarget == null)
				{
					functionTarget = (IInstancedEntity)(object)Character;
				}
				return true;
			}
		}
		functionTarget = (IInstancedEntity)(object)Character;
		return true;
	}

	public void MoveTo(Vector3 destination)
	{
		BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(Character, ((IDynel)Character).RawCoordinates, destination);
	}

	public void Run()
	{
		Character.UpdateMoveType((byte)25);
	}

	public void StopMovement()
	{
		Character.UpdateMoveType((byte)2);
	}

	public void Walk()
	{
		Character.UpdateMoveType((byte)24);
	}

	public bool IsFollowing()
	{
		return false;
	}

	public void DoFollow()
	{
		throw new NotImplementedException();
	}

	public void StartPatrolling()
	{
		throw new NotImplementedException();
	}

	public bool LookAt(Identity target)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref target)).Instance == 0)
		{
			return false;
		}
		int instance = ((Identity)(ref target)).Instance;
		Identity val = ((IEntity)Character).Identity;
		if (instance == ((Identity)(ref val)).Instance)
		{
			((ITargetingEntity)Character).SetTarget(((IEntity)Character).Identity);
			return true;
		}
		if (((IInstancedEntity)Character).Playfield != null)
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)Character).Playfield).Identity, target);
			if (@object == null)
			{
				Pool instance2 = Pool.Instance;
				Identity identity = ((IEntity)((IInstancedEntity)Character).Playfield).Identity;
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)50000;
				((Identity)(ref val)).Instance = ((Identity)(ref target)).Instance;
				@object = instance2.GetObject<ICharacter>(identity, val);
			}
			ICharacter val2 = @object;
			if (val2 != null)
			{
				((ITargetingEntity)Character).SetTarget(((IEntity)val2).Identity);
				return true;
			}
		}
		if (Pool.Instance.Contains(((IEntity)((IInstancedEntity)Character).Playfield).Identity, target))
		{
			((ITargetingEntity)Character).SetTarget(target);
			return true;
		}
		return false;
	}

	public bool CastNano(int nanoId, Identity target)
	{
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0323: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0379: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Unknown result type (might be due to invalid IL or missing references)
		if (!NanoLoader.NanoList.ContainsKey(nanoId))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "Unknown nano program.", 0, 0);
			return false;
		}
		if (!Character.UploadedNanos.Any((IUploadedNanos x) => x.NanoId == nanoId))
		{
			PetShellItemService.Default.TryEnsureNanoUploaded(Character, nanoId);
		}
		if (!Character.UploadedNanos.Any((IUploadedNanos x) => x.NanoId == nanoId))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "Nano is not uploaded. Use the nano crystal first.", 0, 0);
			return false;
		}
		if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
		{
			int strain = ActiveNanoRuntimeService.Default.ResolveNanoStrain(Character, nanoId);
			ActiveNanoRuntimeService.Default.PurgeOrphanSummonNanoInStrain(Character, strain, notifyClient: true);
		}
		if (!ActiveNanoRuntimeService.Default.CanActivateNano(Character, nanoId))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "Not enough NCU to activate this nano.", 0, 0);
			return false;
		}
		if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId) && !PetShellCatalog.UsesShellOnSummon(((IStats)Character).Stats[(StatIds)60].Value, nanoId) && PetSummonNanoCatalog.TryResolve(Character, nanoId, out var summonParams))
		{
			int num = PetSlotClassifier.ResolveStrain(summonParams.PetHash);
			if (num == 1015 && PetRuntimeService.Default.HasLivingAttackPet(Character))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "You can have just 1 Attack Pet.", 0, 0);
				return false;
			}
			if (num == 1016 && PetRuntimeService.Default.HasLivingHealingPet(Character))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "You can have just 1 Heal Pet.", 0, 0);
				return false;
			}
			if (PetSlotClassifier.IsBureaucratCompanionStrain(num) && PetRuntimeService.Default.HasLivingBureaucratCompanionPet(Character))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "You can have just 1 Bureaucrat Companion Pet.", 0, 0);
				return false;
			}
		}
		NanoFormula val = NanoLoader.NanoList[nanoId];
		int petSlotStrain = (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId) ? ActiveNanoRuntimeService.Default.ResolveNanoStrain(Character, nanoId) : val.NanoStrain());
		if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId) && ((int)((Identity)(ref target)).Type == 0 || ((Identity)(ref target)).Instance == 0))
		{
			target = ((IEntity)Character).Identity;
		}
		BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.Send(Character, nanoId, target);
		int num2 = Character.CalculateNanoAttackTime(val);
		Console.WriteLine("Attack-Delay: " + num2);
		if (num2 != 1234567890)
		{
			Thread.Sleep(num2 * 10);
		}
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.FinishNanoCasting(Character, (CharacterActionType)107, Identity.None, 1, nanoId);
		IStat obj = ((IStats)Character).Stats[(StatIds)214];
		obj.Value -= val.getItemAttribute(407);
		int itemAttribute = val.getItemAttribute(8);
		bool flag = NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId);
		if (flag && PetShellCatalog.UsesShellOnSummon(((IStats)Character).Stats[(StatIds)60].Value, nanoId))
		{
			PetShellItemService.Default.TryGiveShellForNano(Character, nanoId);
		}
		else if (flag)
		{
			if (PetSummonNanoCatalog.TryResolve(Character, nanoId, out var summonParams2))
			{
				PetRuntimeService.Default.SummonPet(Character, summonParams2.PetHash, summonParams2.PetTypeId, petSlotStrain, nanoId);
			}
			else
			{
				string preferredPetHash = PetSummonNanoCatalog.GetPreferredPetHash(nanoId);
				string text = (string.IsNullOrWhiteSpace(preferredPetHash) ? "Could not resolve a pet for this nano." : ("Could not resolve a pet for this nano. Import mob template " + preferredPetHash + " into the MySQL database."));
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, text, 0, 0);
			}
		}
		else
		{
			if (((Identity)(ref target)).Instance != 0)
			{
				((ITargetingEntity)Character).SetTarget(target);
			}
			NanoEventRuntimeService.Default.ExecuteOnUseEvents(Character, val);
			ICharacter obj2 = Character;
			Character val2 = (Character)(object)((obj2 is Character) ? obj2 : null);
			if (val2 != null)
			{
				MongoSlamRuntimeService.ApplyCaptureBackedSlamEffects(val2, nanoId);
			}
			if (itemAttribute > 0 && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(val))
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SetNanoDuration(Character, target, nanoId, itemAttribute);
				if (val2 != null && nanoId == 287046)
				{
					MongoSlamRuntimeService.BeginHotWhileProgramActive(val2);
				}
			}
		}
		Thread.Sleep(val.getItemAttribute(210) * 10);
		return false;
	}

	public bool Search()
	{
		return false;
	}

	public bool Sneak()
	{
		return false;
	}

	public bool ChangeVisualFlag(int visualFlag)
	{
		((IStats)Character).Stats[(StatIds)673].Value = visualFlag;
		BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>.Default.Send(Character);
		return false;
	}

	public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
	{
		LogUtil.Debug((DebugInfoDetail)1, ((object)newCoordinates).ToString() + "<->" + ((object)((IDynel)Character).Coordinates()).ToString());
		Character.SetCoordinates(newCoordinates, heading);
		Character.UpdateMoveType((byte)moveType);
		return true;
	}

	public bool ContainerAddItem(int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return InventoryContainerRuntimeService.Default.MovePlayerControllerContainerItem(Character, sourceContainerType, sourcePlacement, target, targetPlacement);
	}

	public bool Follow(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool Stand()
	{
		if (Character.InLogoutTimerPeriod())
		{
			Character.StopLogoutTimer();
		}
		Character.UpdateMoveType((byte)37);
		return true;
	}

	public bool SocialAction(SocialAction action, byte parameter1, byte parameter2, byte parameter3, byte parameter4, int parameter5)
	{
		throw new NotImplementedException();
	}

	public bool Trade(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool UseItem(Identity itemPosition)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return InventoryContainerRuntimeService.Default.UseInventoryItem(Character, itemPosition);
	}

	public bool TryUseBackpackContainer(Identity itemPosition)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return InventoryContainerRuntimeService.Default.TryUseBackpackContainer(Character, itemPosition);
	}

	public bool UseStatel(Identity identity, EventType eventType = 0)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity2 = ((IEntity)((IInstancedEntity)Character).Playfield).Identity;
		if (pFData.ContainsKey(((Identity)(ref identity2)).Instance))
		{
			Dictionary<int, PlayfieldData> pFData2 = PlayfieldLoader.PFData;
			identity2 = ((IEntity)((IInstancedEntity)Character).Playfield).Identity;
			StatelData val = pFData2[((Identity)(ref identity2)).Instance].Statels.FirstOrDefault(delegate(StatelData x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				Identity identity3 = x.Identity;
				int result;
				if (((Identity)(ref identity3)).Type == ((Identity)(ref identity)).Type)
				{
					identity3 = x.Identity;
					result = ((((Identity)(ref identity3)).Instance == ((Identity)(ref identity)).Instance) ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			});
			if (val != null)
			{
				Event val2 = val.Events.FirstOrDefault((Event x) => x.EventType == eventType);
				if (val2 != null)
				{
					val2.Perform(Character, (IEntity)(object)val);
				}
			}
		}
		return true;
	}

	public void SendChatText(string text)
	{
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, text, 0, 0);
	}

	public bool DeleteItem(int container, int slotNumber)
	{
		return InventoryContainerRuntimeService.Default.DeletePlayerControllerContainerItem(Character, container, slotNumber);
	}

	public bool SplitItemStack(Identity targetItem, int stackCount)
	{
		throw new NotImplementedException();
	}

	public bool JoinItemStack(Identity sourceItem, Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool CombineItems(Identity sourceItem, Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillSourceChanged(int inventoryPageId, int slotNumber)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillTargetChanged(int inventoryPageId, int slotNumber)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillBuildPressed(Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool ChatCommand(string command, Identity target)
	{
		throw new NotImplementedException();
	}

	public bool Logout()
	{
		throw new NotImplementedException();
	}

	public void LogoffCharacter()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		CharacterDao instance = Dao<DBCharacter, CharacterDao>.Instance;
		Identity identity = ((IEntity)Character).Identity;
		instance.SetOffline(((Identity)(ref identity)).Instance);
	}

	public bool Login()
	{
		throw new NotImplementedException();
	}

	public bool StopLogout()
	{
		throw new NotImplementedException();
	}

	public bool GetTargetInfo(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamInvite(Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.Invite(Character, target);
	}

	public bool TeamKickMember(Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.Kick(Character, target);
	}

	public bool TeamLeave()
	{
		return TeamRuntime.Leave(Character);
	}

	public bool TransferTeamLeadership(Identity target)
	{
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(Character, "Team leadership transfer is not wired yet.", 0, 0);
		return false;
	}

	public bool TeamJoinRequest(Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.Invite(Character, target);
	}

	public bool TeamJoinReply(bool accept, Identity requester)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.Reply(Character, accept, requester);
	}

	public bool TeamJoinAccepted(Identity newTeamMember)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.AcceptDirect(Character, newTeamMember);
	}

	public bool TeamJoinRejected(Identity rejectingIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return TeamRuntime.RejectDirect(Character, rejectingIdentity);
	}

	public void SendChangedStats()
	{
		Dictionary<int, uint> dictionary = new Dictionary<int, uint>();
		Dictionary<int, uint> dictionary2 = new Dictionary<int, uint>();
		((IStats)Character).Stats.GetChangedStats(dictionary2, dictionary);
		CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(dictionary2);
		CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(dictionary);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendBulk(Character, dictionary2, dictionary);
	}

	~PlayerController()
	{
		Dispose(disposing: false);
	}

	protected virtual void Dispose(bool disposing)
	{
		LogUtil.Debug((DebugInfoDetail)1024, "Disposing of PlayerController");
		if (disposing && !disposed)
		{
			Client = null;
		}
		disposed = true;
	}
}
