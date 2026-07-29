using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AORebirth.Core.Actions;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Functions;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using MemBus;
using MemBus.Configurators;
using MemBus.Setup;
using MemBus.Support;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using Utility;
using Utility.Config;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.InternalMessages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

public class Playfield : PooledObject, IPlayfield, IPooledObject, IEntity, IDisposable
{
	private class PendingCorpseCreditAward
	{
		public Identity LooterIdentity { get; set; }

		public int CorpseInstance { get; set; }

		public DateTime DueAtUtc { get; set; }
	}

	private class CombatAttackSource
	{
		public int MinDamage { get; set; }

		public int MaxDamage { get; set; }

		public int DamageBonus { get; set; }

		public double Range { get; set; }

		public double RechargeSeconds { get; set; }

		public bool UsesEquippedWeapon { get; set; }

		public int AttackInfoAmmoCount { get; set; }

		public int AttackInfoWeaponSlot { get; set; }

		public int AttackInfoUnk1 { get; set; }

		public int AttackInfoHitType { get; set; }

		public int AttackInfoWeaponInstance { get; set; }

		public int WeaponLowId { get; set; }

		public int WeaponHighId { get; set; }

		public int WeaponQualityLevel { get; set; }

		public int RawDamageType { get; set; }

		public string AttackSkillDefinitions { get; set; }

		public string AttackSkillValues { get; set; }

		public int? EffectiveAttackRating { get; set; }

		public int? AddAllOff { get; set; }
	}

	private enum CombatDamageSource
	{
		WeaponAutoAttack,
		UnarmedAutoAttack,
		DamageOverTime,
		HealOverTime,
		Nano,
		Environment
	}

	private class EquippedCombatWeapon
	{
		public IItem Item { get; set; }

		public int Slot { get; set; }
	}

	private readonly DisposeContainer memBusDisposeContainer = new DisposeContainer(Array.Empty<object>());

	private readonly IBus playfieldBus;

	private readonly ZoneServer server;

	private List<PlayfieldDistrict> districts = new List<PlayfieldDistrict>();

	private readonly Timer heartBeat;

	private readonly PlayfieldRuntimeSystems runtimeSystems;

	private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, int> lastCombatWeaponSlots = new Dictionary<int, int>();

	private readonly CorpseInventoryService corpseInventoryService = new CorpseInventoryService();

	private static readonly GlobalLootRuntimeService GlobalLootRuntimeService = new GlobalLootRuntimeService();

	private readonly object corpseVisibilitySync = new object();

	private readonly Dictionary<int, CorpseState> pendingCorpseSpawns = new Dictionary<int, CorpseState>();

	private readonly Dictionary<int, PendingCorpseCreditAward> pendingCorpseCreditAwards = new Dictionary<int, PendingCorpseCreditAward>();

	private int nextCorpseInstance = 15790080;

	private int nextCorpseInventoryHandle = 112;

	private int nextCorpseLootItemInstance = 2097152;

	private const int CorpseLootItemIdentityType = 150994945;

	private static readonly TimeSpan CorpseCreditAwardDelay = TimeSpan.FromMilliseconds(500.0);

	private const int DefaultNpcDeathAnimationKey = 503;

	private const int DefaultPlayerDeathAnimationKey = 500;

	private const int DeathRespawnActionParameter1 = 1000020;

	private const int DeathRespawnActionParameter2 = 295830;

	private const int PrivateCityPlayfieldMinInstance = 1048576;

	private const int PrivateCityPlayfieldMaxInstance = 1245183;

	private const int UnknownPlayfieldSizeFallback = 100000;

	private const string CapturedOwnedPrivateCityOrganizationName = "Est. 2024";

	private const double MaxMeleeCombatDistance = 8.0;

	private const double MaxMeleeFollowHoldDistance = 3.0;

	private const double MinNpcCombatMoveDistance = 0.3;

	private const string CapturedCleaningRobotName = "Malfunctioning Cleaning Robot";

	private const int CapturedCleaningRobotMonsterData = 297023;

	private const int CapturedSubwayThiefCorpseCatMesh = 5907;

	private const int CapturedCleaningRobotCorpseCatMesh = 297018;

	private const double CapturedCleaningRobotFollowStopDistance = 0.0;

	private const int UnarmedAttackInfoAmmoCount = -1;

	private const int PlayerUnarmedAttackInfoWeaponSlot = 0;

	private const int PlayerUnarmedAttackInfoWeaponInstance = 100;

	private const int NormalAttackInfoHitType = 3;

	private const int MissingItemStatValue = 1234567890;

	private const double DefaultCombatTickSeconds = 2.0;

	private const double OutOfRangeRetrySeconds = 1.0;

	private const int RubiKaStartPlayfield = 4582;

	private const int GridPlayfield = 152;

	private const int RubiKaStartX = 939;

	private const int RubiKaStartY = 20;

	private const int RubiKaStartZ = 732;

	private const int ShadowlandsStartPlayfield = 4001;

	private const int ShadowlandsStartX = 850;

	private const int ShadowlandsStartY = 43;

	private const int ShadowlandsStartZ = 565;

	private static readonly Dictionary<int, int> MonsterDataToCorpseCatMesh = CombatCorpseVisuals.BuildMonsterDataToCorpseCatMeshMap();

	private readonly List<StatelData> statels = new List<StatelData>();

	private readonly StatelData[] collisionStatels = (StatelData[])(object)new StatelData[0];

	private float x;

	private bool disposed = false;

	private IDictionary<int, CorpseState> corpses => corpseInventoryService.States;

	public List<PlayfieldDistrict> Districts
	{
		get
		{
			return districts;
		}
		private set
		{
			districts = value;
		}
	}

	public List<Function> EnvironmentFunctions { get; private set; }

	public Expansions Expansion { get; set; }

	public IBus PlayfieldBus { get; set; }

	public float X
	{
		get
		{
			return X;
		}
		set
		{
			x = value;
		}
	}

	public float XScale { get; set; }

	public float Z { get; set; }

	public float ZScale { get; set; }

	public Playfield(ZoneServer zoneServer, Identity playfieldIdentity)
		: base(Identity.None, playfieldIdentity)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		server = zoneServer;
		playfieldBus = BusSetup.StartWith<AsyncConfiguration>(Array.Empty<ISetup<IConfigurableBus>>()).Construct();
		runtimeSystems = new PlayfieldRuntimeSystems(this, ((PooledObject)this).Identity, IsPrivateCityPlayfieldCandidate, PlayfieldStatelTransitionRuntimeService.IsCapturedMontroyalPrivateCityInstance, ResolveCharacterOrganizationInstance, ResolveOrganizationName, ResolveCharacterStatWireValue);
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendAOtomationMessageToClient>((Action<IMSendAOtomationMessageToClient>)runtimeSystems.DeliverAOtomationMessageToClient));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendAOtomationMessageToPlayfield>((Action<IMSendAOtomationMessageToPlayfield>)delegate(IMSendAOtomationMessageToPlayfield message)
		{
			runtimeSystems.DeliverAOtomationMessageToPlayfield(message, Announce);
		}));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendAOtomationMessageToPlayfieldOthers>((Action<IMSendAOtomationMessageToPlayfieldOthers>)delegate(IMSendAOtomationMessageToPlayfieldOthers message)
		{
			runtimeSystems.DeliverAOtomationMessageToPlayfieldOthers(message, AnnounceOthers);
		}));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendAOtomationMessageBodyToClient>((Action<IMSendAOtomationMessageBodyToClient>)runtimeSystems.DeliverAOtomationMessageBodyToClient));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendAOtomationMessageBodiesToClient>((Action<IMSendAOtomationMessageBodiesToClient>)runtimeSystems.DeliverAOtomationMessageBodiesToClient));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMSendPlayerSCFUs>((Action<IMSendPlayerSCFUs>)SendSCFUsToClient));
		memBusDisposeContainer.Add(((ISubscriber)playfieldBus).Subscribe<IMExecuteFunction>((Action<IMExecuteFunction>)ExecuteFunction));
		statels = runtimeSystems.ResolveStatels(playfieldIdentity);
		runtimeSystems.RegisterStatels(statels);
		collisionStatels = runtimeSystems.ResolveCollisionStatels(statels);
		runtimeSystems.MaterializeStartupObjects(playfieldIdentity, statels);
		heartBeat = new Timer(HeartBeatTimer, null, 10, 0);
	}

	internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.SpawnCapturedNpcContent(playfieldIdentity);
	}

	public void Announce(Message message)
	{
		Announce(message.Body);
	}

	public void Announce(MessageBody messageBody)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		CombatStartPacketDiagnostics.LogOutbound("Playfield.Announce", messageBody, Identity.None);
		N3Message val = (N3Message)(object)((messageBody is N3Message) ? messageBody : null);
		if (val != null)
		{
			ICharacter val2 = this.FindByIdentity<ICharacter>(val.Identity);
			if (val2 != null && IsVisibilityMovementMessage(messageBody))
			{
				RefreshCharacterVisibility(val2);
			}
			if (runtimeSystems.TryAnnounceCharacterScopedMessage(val.Identity, Identity.None, messageBody, Send))
			{
				return;
			}
		}
		runtimeSystems.AnnounceMessageToCharacterClients(messageBody, Send);
	}

	public void AnnounceAppearanceUpdate(ICharacter character)
	{
		BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>.Default.Send(character);
	}

	public static void ArmPostZoneCollisionGrace(ICharacter character)
	{
		PlayfieldStatelTransitionRuntimeService.ArmPostZoneCollisionGrace(character);
	}

	public void AnnounceOthers(MessageBody messageBody, Identity dontSend)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		N3Message val = (N3Message)(object)((messageBody is N3Message) ? messageBody : null);
		if (val == null || !runtimeSystems.TryAnnounceCharacterScopedMessage(val.Identity, dontSend, messageBody, Send))
		{
			runtimeSystems.AnnounceMessageToOtherCharacterClients(messageBody, dontSend, Send);
		}
	}

	public void Despawn(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!runtimeSystems.TryDespawnVisibleCharacter(identity, SendVisibilityMessage))
		{
			Announce((MessageBody)(object)BaseMessageHandler<DespawnMessage, DespawnMessageHandler>.Default.Create(identity));
		}
	}

	public void AnnounceSpawnedCharacterVisibility(ICharacter character, Identity alreadyVisibleRecipient)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.AnnounceSpawnedCharacterVisibility(character, alreadyVisibleRecipient, SendVisibilityMessage, SendVisibilityLeave);
	}

	public void RefreshCharacterVisibility(ICharacter character)
	{
		runtimeSystems.RefreshCharacterVisibility(character, SendVisibilityMessage, SendVisibilityLeave);
		RefreshCorpseVisibilityForRecipient(character);
	}

	public void ForgetVisibilityRecipient(Identity recipientIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.ForgetVisibilityRecipient(recipientIdentity);
		lock (corpseVisibilitySync)
		{
			foreach (CorpseState value in corpses.Values)
			{
				if (value.VisibleRecipients != null)
				{
					value.VisibleRecipients.Remove(recipientIdentity);
				}
			}
		}
	}

	private void SendVisibilityMessage(ICharacter recipient, MessageBody messageBody)
	{
		if (recipient != null && ((IDynel)recipient).Controller != null && ((IDynel)recipient).Controller.Client != null)
		{
			Send(((IDynel)recipient).Controller.Client, messageBody);
		}
	}

	private void SendVisibilityLeave(ICharacter recipient, Identity identity)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		SendVisibilityMessage(recipient, (MessageBody)(object)BaseMessageHandler<DespawnMessage, DespawnMessageHandler>.Default.Create(identity));
	}

	private static bool IsVisibilityMovementMessage(MessageBody messageBody)
	{
		return messageBody is CharDCMoveMessage || messageBody is FollowTargetMessage || messageBody is SetPosMessage;
	}

	private Coordinate DynelDropPosition(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		IDynel val = runtimeSystems.FindByIdentity<IDynel>(identity);
		return (Coordinate)((val != null) ? ((object)val.Coordinates()) : ((object)new Coordinate()));
	}

	public void DespawnNpcImmediately(ICharacter target)
	{
		runtimeSystems.DespawnNpcImmediately(target, StopFightingDeadTarget, CancelPendingNpcCorpseSpawn);
	}

	private void CancelPendingNpcCorpseSpawn(Identity deadNpcIdentity)
	{
		pendingCorpseSpawns.Remove(((Identity)(ref deadNpcIdentity)).Instance);
	}

	public void RegisterNpcHome(ICharacter character)
	{
		runtimeSystems.RegisterNpcHome(character);
	}

	public void ActivateNpc(ICharacter character)
	{
		runtimeSystems.ActivateNpc(character);
	}

	public void AcquireNpcAggro(ICharacter attacker, ICharacter target)
	{
		runtimeSystems.AcquireNpcAggro(attacker, target);
	}

	public void ForceNpcTauntAggro(ICharacter taunter, ICharacter npc)
	{
		runtimeSystems.ForceNpcTauntAggro(taunter, npc);
	}

	internal IEnumerable<ICharacter> EnumerateActiveCharacters()
	{
		return runtimeSystems.Characters();
	}

	internal void NotifyNpcCombatDamage(ICharacter npc)
	{
		runtimeSystems.NotifyNpcCombatDamage(npc);
	}

	internal void SuspendNpcRegen(ICharacter npc)
	{
		runtimeSystems.SuspendNpcRegen(npc);
	}

	internal void ClearInvalidNpcCombatTarget(ICharacter attacker)
	{
		runtimeSystems.ClearInvalidNpcCombatTarget(attacker);
	}

	internal void ClearNpcCombatTracking(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.ClearNpcCombatTracking(identity);
	}

	internal void ClearNpcFightingTarget(ICharacter character)
	{
		runtimeSystems.ClearNpcFightingTarget(character);
	}

	public int DespawnCorpses(Func<string, Identity, bool> shouldDespawn)
	{
		return runtimeSystems.DespawnCorpses(pendingCorpseSpawns, corpses, shouldDespawn, (CorpseState corpse) => corpse.Name, (CorpseState corpse) => corpse.DeadNpcIdentity, DespawnCorpse);
	}

	public void DisconnectAllClients()
	{
		IEnumerable<Character> source = Pool.Instance.GetAll<Character>(50000).ToList();
		for (int num = source.Count() - 1; num >= 0; num--)
		{
			IEntity val = (IEntity)(object)source.ElementAt(num);
			if (val is Character)
			{
				if (((Dynel)((val is Character) ? val : null)).Controller.Client != null)
				{
					((ServerBase)server).DisconnectClient((IClient)(object)((Dynel)((val is Character) ? val : null)).Controller.Client);
				}
				((PooledObject)((val is Character) ? val : null)).Dispose();
			}
		}
	}

	public IInstancedEntity FindByIdentity(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return runtimeSystems.FindByIdentity(identity);
	}

	public T FindByIdentity<T>(Identity identity) where T : class, IEntity
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return runtimeSystems.FindByIdentity<T>(identity);
	}

	public List<IDynel> FindInRange(IDynel dynel, float range)
	{
		return runtimeSystems.FindDynelsInRange(dynel, range).ToList();
	}

	public bool IsInstancedPlayfield()
	{
		throw new NotImplementedException();
	}

	public int NumberOfDynels()
	{
		return Pool.Instance.GetAll(50000).Count();
	}

	public int NumberOfPlayers()
	{
		return Pool.Instance.GetAll<Character>(50000).Count();
	}

	public static bool IsPrivateCityPlayfieldCandidate(Identity playfieldIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref playfieldIdentity)).Type != 51101 && (int)((Identity)(ref playfieldIdentity)).Type != 40016)
		{
			return false;
		}
		int instance = ((Identity)(ref playfieldIdentity)).Instance;
		if (instance < 1048576 || instance > 1245183)
		{
			return false;
		}
		if (PlayfieldStatelTransitionRuntimeService.IsCapturedMontroyalPrivateCityInstance(instance))
		{
			return true;
		}
		return ZoneEngine.Core.Playfields.Playfields.GetPlayfieldX(instance) == 100000 && ZoneEngine.Core.Playfields.Playfields.GetPlayfieldZ(instance) == 100000;
	}

	public void SendPrivateCityPlayfieldReadyBlock(ZoneClient client, ICharacter character)
	{
		runtimeSystems.SendPrivateCityPlayfieldReadyBlock(client, character);
	}

	public void SendPrivateCityPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
	{
		runtimeSystems.SendPrivateCityPreFullCharacterReadyBlock(client, character);
	}

	public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return runtimeSystems.TryHandleGenericCmdUse(client, message, target);
	}

	public void Publish(object obj)
	{
		((IPublisher)playfieldBus).Publish(obj);
	}

	public void Send(IZoneClient client, MessageBody body)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		CombatStartPacketDiagnostics.LogOutbound("Playfield.SendBodyToClient", body, (client == null || client.Controller == null || client.Controller.Character == null) ? Identity.None : ((IEntity)client.Controller.Character).Identity);
		runtimeSystems.PublishMessageBodyToClient(client, body, Publish);
	}

	public void Send(IZoneClient client, Message message)
	{
		runtimeSystems.PublishMessageToClient(client, message, Publish);
	}

	public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Teleport(dynel, destination, heading, playfield, null);
	}

	internal void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield, Action<ICharacter> sendTeleportPacket)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		if (dynel.DoNotDoTimers || TryCompleteGridTeleportInCurrentPlayfield(dynel, destination, heading, playfield))
		{
			return;
		}
		runtimeSystems.TransferToPlayfield(dynel, destination, heading, playfield, ClearPlayfieldTransferContactState, DisableTimersForPlayfieldTransfer, CapturePlayfieldTransferEnterZoningPhase, delegate
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Expected O, but got Unknown
			Dynel obj = dynel;
			ICharacter val = (ICharacter)(object)((obj is ICharacter) ? obj : null);
			if (sendTeleportPacket == null)
			{
				BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>.Default.Send(val, destination.coordinate, (Quaternion)heading, playfield);
			}
			else
			{
				sendTeleportPacket(val);
			}
		}, AnnouncePlayfieldTransferDespawn, ApplyPlayfieldTransferState, CapturePlayfieldTransferClient, ResolveOrCreatePlayfieldTransferDestination, CompletePlayfieldTransferDispose, delegate(ZoneClient client)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			SendPlayfieldTransferRedirect(client, playfield);
		});
	}

	private void ClearPlayfieldTransferContactState(int dynelId)
	{
		runtimeSystems.ClearStatelTransitionContactState(dynelId);
	}

	private static void DisableTimersForPlayfieldTransfer(Dynel dynel)
	{
		dynel.DoNotDoTimers = true;
	}

	private void AnnouncePlayfieldTransferDespawn(Dynel dynel)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Despawn(((PooledObject)dynel).Identity);
	}

	private static void ApplyPlayfieldTransferState(Dynel dynel, Coordinate destination, IQuaternion heading)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		ICharacter val = (ICharacter)(object)((dynel is ICharacter) ? dynel : null);
		if (val != null)
		{
			ActiveNanoRuntimeService.Default.HandlePlayfieldLeave(val);
		}
		dynel.RawCoordinates = new Vector3
		{
			X = destination.x,
			Y = destination.y,
			Z = destination.z
		};
		dynel.RawHeading = new Quaternion((double)heading.xf, (double)heading.yf, (double)heading.zf, (double)heading.wf);
	}

	private static ZoneClient CapturePlayfieldTransferClient(Dynel dynel)
	{
		return (ZoneClient)(object)dynel.Controller.Client;
	}

	private static Action CapturePlayfieldTransferEnterZoningPhase(Dynel dynel)
	{
		ZoneClient zoneClient = ((dynel.Controller == null) ? null : (dynel.Controller.Client as ZoneClient));
		return (zoneClient == null) ? null : new Action(zoneClient.SessionLifecycle.EnterZoningForPlayfieldTransfer);
	}

	private IPlayfield ResolveOrCreatePlayfieldTransferDestination(Identity playfield)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		IPlayfield val = server.PlayfieldById(playfield);
		Pool instance = Pool.Instance;
		Identity none = Identity.None;
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = ((Identity)(ref playfield)).Type;
		((Identity)(ref val2)).Instance = ((Identity)(ref playfield)).Instance;
		instance.GetObject<Playfield>(none, val2);
		if (val == null)
		{
			val = (IPlayfield)(object)new Playfield(server, playfield);
		}
		return val;
	}

	private static void CompletePlayfieldTransferDispose(Dynel dynel, IPlayfield newPlayfield)
	{
		dynel.Playfield = newPlayfield;
		dynel.Controller.Client = null;
		dynel.IsTeleporting = true;
		((PooledObject)dynel).Dispose();
	}

	private void SendPlayfieldTransferRedirect(ZoneClient client, Identity playfield)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		LogUtil.Debug((DebugInfoDetail)8, "Saving to pf " + ((Identity)(ref playfield)).Instance);
		if (!IPAddress.TryParse(ConfigReadWrite.Instance.CurrentConfig.ZoneIP, out var address))
		{
			IPHostEntry hostEntry = Dns.GetHostEntry(ConfigReadWrite.Instance.CurrentConfig.ZoneIP);
			IPAddress[] addressList = hostEntry.AddressList;
			foreach (IPAddress iPAddress in addressList)
			{
				if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					address = iPAddress;
					break;
				}
			}
		}
		ZoneRedirectionMessage messageBody = new ZoneRedirectionMessage
		{
			ServerIpAddress = address,
			ServerPort = (ushort)((ServerBase)server).TcpEndPoint.Port
		};
		client?.SendCompressed((MessageBody)(object)messageBody);
	}

	private bool TryCompleteGridTeleportInCurrentPlayfield(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Expected O, but got Unknown
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Expected O, but got Unknown
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((PooledObject)this).Identity;
		if (((Identity)(ref identity)).Instance == 152)
		{
			IdentityType type = ((Identity)(ref playfield)).Type;
			identity = ((PooledObject)this).Identity;
			if (type == ((Identity)(ref identity)).Type)
			{
				int instance = ((Identity)(ref playfield)).Instance;
				identity = ((PooledObject)this).Identity;
				if (instance == ((Identity)(ref identity)).Instance)
				{
					ICharacter val = (ICharacter)(object)((dynel is ICharacter) ? dynel : null);
					if (val == null || ((IDynel)val).Controller == null || ((IDynel)val).Controller.Client == null)
					{
						return false;
					}
					float num = dynel.RawCoordinates.X;
					float y = dynel.RawCoordinates.Y;
					float z = dynel.RawCoordinates.Z;
					BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>.Default.SendLocal(val, destination.coordinate, new Quaternion((double)heading.xf, (double)heading.yf, (double)heading.zf, (double)heading.wf));
					dynel.RawCoordinates = Vector3.op_Implicit(new Vector3
					{
						x = destination.x,
						y = destination.y,
						z = destination.z
					});
					dynel.RawHeading = new Quaternion((double)heading.xf, (double)heading.yf, (double)heading.zf, (double)heading.wf);
					RefreshCharacterVisibility(val);
					PrimeStatelCollisionContacts(val);
					CultureInfo invariantCulture = CultureInfo.InvariantCulture;
					object[] array = new object[8];
					identity = ((PooledObject)dynel).Identity;
					array[0] = ((Identity)(ref identity)).ToString(true);
					identity = ((PooledObject)this).Identity;
					array[1] = ((Identity)(ref identity)).Instance;
					array[2] = num;
					array[3] = y;
					array[4] = z;
					array[5] = destination.x;
					array[6] = destination.y;
					array[7] = destination.z;
					LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture, "Grid current-playfield teleport completed character={0} playfield={1} fromCoords={2:F1},{3:F1},{4:F1} toCoords={5:F1},{6:F1},{7:F1}", array));
					return true;
				}
			}
		}
		return false;
	}

	public void DisconnectClient(IInstancedEntity entity)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = (ICharacter)(object)((entity is ICharacter) ? entity : null);
		if (val != null)
		{
			Despawn(((IEntity)val).Identity);
			ForgetVisibilityRecipient(((IEntity)val).Identity);
			runtimeSystems.UnregisterDynel(((IEntity)val).Identity);
		}
		runtimeSystems.RemoveInstancedEntity(entity);
	}

	public void ExecuteFunction(IMExecuteFunction imExecuteFunction)
	{
		runtimeSystems.ExecuteFunction(imExecuteFunction, FindNamedEntityByIdentity, SendNoValidFunctionTargetMessage);
	}

	private static void SendNoValidFunctionTargetMessage(Character character, string text)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new ChatTextMessage
		{
			Identity = ((PooledObject)character).Identity,
			Text = text
		});
	}

	public List<ICharacter> FindCharacterInRange(IDynel dynel, float range)
	{
		return runtimeSystems.FindCharactersInRange(dynel, range).ToList();
	}

	public INamedEntity FindNamedEntityByIdentity(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return runtimeSystems.FindByIdentity<INamedEntity>(identity);
	}

	public Dictionary<Identity, string> ListAvailablePlayfields(bool global = true)
	{
		return server.ListAvailablePlayfields(global);
	}

	public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)
	{
		runtimeSystems.SendExistingCharacterVisibilityToClient(sendSCFUs.toClient.Controller.Character, delegate(MessageBody body)
		{
			sendSCFUs.toClient.SendCompressed(body);
		});
		SendExistingCorpseVisibilityToClient(sendSCFUs.toClient.Controller.Character);
	}

	public void SendStaticDynelsToClient(ICharacter character)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		List<StaticDynel> list = new List<StaticDynel>(Pool.Instance.GetAll<StaticDynel>(((PooledObject)this).Identity));
		Identity identity = ((PooledObject)this).Identity;
		LogUtil.Debug((DebugInfoDetail)8, "SendStaticDynelsToClient pf=" + ((Identity)(ref identity)).Instance + " count=" + list.Count);
		foreach (StaticDynel item in list)
		{
			BaseMessageHandler<SimpleItemFullUpdateMessage, SimpleItemFullUpdateMessageHandler>.Default.Send(character, item);
		}
	}

	public void AnnouncePlayerVisibility(ICharacter character)
	{
		runtimeSystems.AnnounceJoiningCharacterVisibility(character, SendVisibilityMessage, SendVisibilityLeave);
	}

	private void CheckStatelCollision(ICharacter dynel)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.CheckStatelCollision(dynel, ((PooledObject)this).Identity, collisionStatels, ResolveCapturedMontroyalPrivateCityInstance, ResolveCharacterOrganizationInstance, delegate(ICharacter x)
		{
			x.StopMovement();
		}, SendCapturedPrivateCityEntrySocialStatus, TeleportToPlayfield);
	}

	private void PrimeStatelCollisionContacts(ICharacter dynel)
	{
		runtimeSystems.PrimeStatelCollisionContacts(dynel, collisionStatels);
	}

	private static int ResolveCapturedMontroyalPrivateCityInstance(ICharacter character)
	{
		int organizationInstance = ResolveCharacterOrganizationInstance(character);
		int organizationCityId = ResolveOrganizationCityId(organizationInstance);
		return PlayfieldStatelTransitionRuntimeService.ResolveCapturedMontroyalPrivateCityInstance(organizationInstance, organizationCityId);
	}

	private void TeleportToPlayfield(Dynel dynel, Coordinate destination, Quaternion heading, int playfieldInstance)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51101;
		((Identity)(ref val)).Instance = playfieldInstance;
		Identity playfield = val;
		if (playfieldInstance != 7001)
		{
			val = ((PooledObject)this).Identity;
			if (((Identity)(ref val)).Instance != 7001)
			{
				Teleport(dynel, destination, (IQuaternion)(object)heading, playfield);
				return;
			}
		}
		Vector3 envelope = new Vector3((double)dynel.RawCoordinates.X, (double)dynel.RawCoordinates.Y, (double)dynel.RawCoordinates.Z);
		Vector3 landing = new Vector3((double)destination.x, (double)destination.y, (double)destination.z);
		Teleport(dynel, destination, (IQuaternion)(object)heading, playfield, delegate(ICharacter character)
		{
			BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>.Default.SendCapturedGatewayTransfer(character, envelope, landing, heading, playfieldInstance);
		});
	}

	private static int ResolveOrganizationCityId(int organizationInstance)
	{
		if (organizationInstance <= 0)
		{
			return 0;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(organizationInstance);
			return (val != null) ? val.CityId : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static string ResolveOrganizationName(int organizationInstance)
	{
		if (organizationInstance <= 0)
		{
			return string.Empty;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(organizationInstance);
			if (val != null && !string.IsNullOrEmpty(val.Name))
			{
				return val.Name;
			}
		}
		catch
		{
		}
		return PlayfieldStatelTransitionRuntimeService.IsCapturedOwnedPrivateCityOrganization(organizationInstance) ? "Est. 2024" : string.Empty;
	}

	private static int ResolveCharacterOrganizationInstance(ICharacter character)
	{
		return ResolveCharacterStatValue(character, (StatIds)5);
	}

	private static int ResolveCharacterStatValue(ICharacter character, StatIds statId)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return 0;
		}
		uint baseValue = ((IStats)character).Stats[statId].BaseValue;
		if (baseValue != 0 && baseValue <= int.MaxValue)
		{
			return (int)baseValue;
		}
		return ((IStats)character).Stats[statId].Value;
	}

	private static uint ResolveCharacterStatWireValue(ICharacter character, StatIds statId)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		int num = ResolveCharacterStatValue(character, statId);
		return (num >= 0) ? ((uint)num) : 0u;
	}

	private void CheckWallCollision(ICharacter dynel)
	{
		runtimeSystems.CheckWallCollision(dynel, PlayfieldStatelTransitionRuntimeService.IsPostZoneCollisionGraceActive, TeleportToPlayfield);
	}

	private void HeartBeatTimer(object sender)
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			runtimeSystems.ProcessHeartbeatTimedLifecycle(((PooledObject)this).Identity, ProcessPendingCorpseSpawns, ProcessCorpseDespawns, ProcessPendingCorpseCreditAwards, delegate(ICharacter dynel)
			{
				runtimeSystems.ProcessCharacterRegeneration(dynel, SendChangedStats);
			}, DoCombatTick, runtimeSystems.ProcessCharacterFollow, delegate(ICharacter dynel)
			{
				runtimeSystems.ProcessPlayerCollisionChecks(dynel, CheckWallCollision, CheckStatelCollision);
			});
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex, false, "Playfield heartbeat failed for {0}", new object[1] { ((PooledObject)this).Identity });
		}
		finally
		{
			try
			{
				heartBeat.Change(10, 0);
			}
			catch (ObjectDisposedException)
			{
			}
		}
	}

	public void ResetCombatTick(Identity attacker)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = this.FindByIdentity<ICharacter>(attacker);
		if (val != null && ((IDynel)val).Controller is NPCController)
		{
			runtimeSystems.ResetNpcCombatTick(val);
		}
		else
		{
			runtimeSystems.ResetPlayerCombatTick(attacker, ResetPlayerCombatTick);
		}
	}

	public void StartPlayerAttack(ICharacter character, Identity target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.StartPlayerAttack(character, target, ResetCombatTick);
	}

	public void CancelPlayerAttack(ICharacter character)
	{
		runtimeSystems.CancelPlayerAttack(character, ResetCombatTick);
	}

	public bool TryApplyPlayerSpecialAttack(ICharacter attacker, ICharacter target, int specialStatId, out int damage, out int ammoCount, out int equipSlot)
	{
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		damage = 0;
		ammoCount = 0;
		equipSlot = 6;
		if (attacker == null || target == null || !PlayerSpecialAttackRules.IsSupportedSpecial(specialStatId))
		{
			return false;
		}
		CombatAttackSource combatAttackSource = GetCombatAttackSource(attacker);
		if (combatAttackSource == null)
		{
			return false;
		}
		equipSlot = ((combatAttackSource.AttackInfoWeaponSlot > 0) ? combatAttackSource.AttackInfoWeaponSlot : 6);
		ammoCount = Math.Max(0, combatAttackSource.AttackInfoAmmoCount);
		int num = PlayerSpecialAttackRules.ResolveHitCount(specialStatId);
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			num2 += CalculateCombatDamage(attacker, combatAttackSource);
		}
		damage = Math.Max(1, num2);
		int value = ((IStats)target).Stats[(StatIds)27].Value;
		int num3 = Math.Max(0, value - damage);
		bool flag = num3 == 0;
		((IStats)target).Stats[(StatIds)27].Value = num3;
		runtimeSystems.SendChangedStats(target, SendChangedStats);
		LogUtil.Debug((DebugInfoDetail)4, $"SpecialAttack hit attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} special={specialStatId} damage={damage} health={num3}/{((IStats)target).Stats[(StatIds)1].Value} hits={num}");
		if (flag)
		{
			HandleCombatKillingHit(attacker, target);
		}
		return true;
	}

	private void ResetPlayerCombatTick(Identity attacker)
	{
		nextCombatTicks.Remove(((Identity)(ref attacker)).Instance);
	}

	public void RespawnPlayer(ICharacter character)
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Player death respawn skipped: character=null.");
			return;
		}
		if (!(((IDynel)character).Controller is PlayerController))
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "Player death respawn skipped: controller={0} character={1}", (((IDynel)character).Controller == null) ? "null" : ((object)((IDynel)character).Controller).GetType().FullName, ((IEntity)character).Identity));
			return;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "Player death respawn skipped: character is not Dynel character={0}", ((IEntity)character).Identity));
			return;
		}
		LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "Player death respawn entered target={0} pf={1}", ((IEntity)character).Identity, ((PooledObject)this).Identity));
		ResolvePlayerRespawnLocation(character, out var destination, out var destinationPlayfield);
		Identity corpseIdentity = AllocateCorpseIdentity();
		runtimeSystems.ProcessPlayerRespawn(character, val, corpseIdentity, destination, destinationPlayfield, LogSkippedPlayerCorpseVisual, SendDeathSocialStatus, MarkPlayerRespawned, SendDeathRespawnStateStats, StopCharacterMovement, SendChangedStats, LogPlayerRespawnRequested, EnableCharacterTimers, TryCompleteDeathRespawnInCurrentPlayfield, Teleport, ClearCombatTracking, StopFightingDeadTarget, SendCombatStopMessage);
	}

	private void LogSkippedPlayerCorpseVisual(ICharacter character, Identity corpseIdentity)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player corpse visual skipped target={0} corpse={1}; current CorpseFullUpdate template is NPC-loot oriented and breaks modern death teleport flow.", ((IEntity)character).Identity, corpseIdentity));
	}

	private void LogPlayerRespawnRequested(ICharacter character, Identity corpseIdentity, Identity destinationPlayfield, Coordinate destination)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player death respawn requested target={0} corpse={1} destination={2}:{3} pos={4:0.00},{5:0.00},{6:0.00}", ((IEntity)character).Identity, corpseIdentity, ((Identity)(ref destinationPlayfield)).Type, ((Identity)(ref destinationPlayfield)).Instance, destination.x, destination.y, destination.z));
	}

	private static void StopCharacterMovement(ICharacter character)
	{
		character.StopMovement();
	}

	private static void SendChangedStats(ICharacter character)
	{
		((IDynel)character).SendChangedStats();
	}

	private static void EnableCharacterTimers(ICharacter character)
	{
		((IInstancedEntity)character).DoNotDoTimers = false;
	}

	private bool TryCompleteDeathRespawnInCurrentPlayfield(Dynel dynel, Coordinate destination, IQuaternion heading, Identity destinationPlayfield)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Expected O, but got Unknown
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		IdentityType type = ((Identity)(ref destinationPlayfield)).Type;
		Identity identity = ((PooledObject)this).Identity;
		if (type == ((Identity)(ref identity)).Type)
		{
			int instance = ((Identity)(ref destinationPlayfield)).Instance;
			identity = ((PooledObject)this).Identity;
			if (instance == ((Identity)(ref identity)).Instance)
			{
				ICharacter val = (ICharacter)(object)((dynel is ICharacter) ? dynel : null);
				ZoneClient zoneClient = ((dynel.Controller == null) ? null : (dynel.Controller.Client as ZoneClient));
				if (val == null || zoneClient == null)
				{
					return false;
				}
				BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>.Default.Send(val, destination.coordinate, new Quaternion((double)heading.xf, (double)heading.yf, (double)heading.zf, (double)heading.wf), destinationPlayfield);
				dynel.RawCoordinates = Vector3.op_Implicit(new Vector3
				{
					x = destination.x,
					y = destination.y,
					z = destination.z
				});
				dynel.RawHeading = new Quaternion((double)heading.xf, (double)heading.yf, (double)heading.zf, (double)heading.wf);
				BaseMessageHandler<PlayfieldAnarchyFMessage, PlayfieldAnarchyFMessageHandler>.Default.Send(val);
				SimpleCharFullUpdate.SendToPlayfield((IZoneClient)(object)zoneClient);
				SendDeathSocialStatus(val);
				SendDeathRespawnStateStats(val);
				IMSendPlayerSCFUs sendSCFUs = new IMSendPlayerSCFUs
				{
					toClient = (IZoneClient)(object)zoneClient
				};
				SendSCFUsToClient(sendSCFUs);
				RefreshCharacterVisibility(val);
				foreach (StaticDynel item in runtimeSystems.StaticDynels())
				{
					BaseMessageHandler<SimpleItemFullUpdateMessage, SimpleItemFullUpdateMessageHandler>.Default.Send(val, item);
				}
				WeaponItemFullUpdate.SendWeaponDefinitions(val);
				SendDeathRespawnGameTime(val);
				SendDeathSocialStatus(val);
				BaseMessageHandler<FullCharacterMessage, FullCharacterMessageHandler>.Default.Send(val);
				SendDeathRespawnPlayfieldReadyBlock(zoneClient, val);
				SendDeathRespawnAction(val);
				runtimeSystems.EnsureWeaponVisualMeshes(val, announceAppearanceUpdate: false);
				BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>.Default.Send(val);
				LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "Player death respawn completed in current playfield target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}", ((IEntity)val).Identity, ((Identity)(ref destinationPlayfield)).Type, ((Identity)(ref destinationPlayfield)).Instance, destination.x, destination.y, destination.z));
				return true;
			}
		}
		return false;
	}

	private void SendDeathRespawnGameTime(ICharacter character)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		((IDynel)character).Send((MessageBody)new GameTimeMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown1 = 30024f,
			Unknown3 = 185408,
			Unknown4 = 80183.31f
		}, false);
		ZoneClient zoneClient = ((((IDynel)character).Controller != null) ? (((IDynel)character).Controller.Client as ZoneClient) : null);
		if (zoneClient != null)
		{
			zoneClient.LastGameTimeSyncUtc = DateTime.UtcNow;
		}
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player death respawn game time target={0}", ((IEntity)character).Identity));
	}

	private void DoCombatTick(ICharacter attacker)
	{
		if (((IDynel)attacker).Controller is NPCController)
		{
			runtimeSystems.ProcessNpcCombatTick(attacker);
			return;
		}
		runtimeSystems.ProcessPlayerCombatTick(attacker, ClearCombatTracking, FindPlayerCombatTarget, (ICharacter target) => IsValidPlayerCombatTarget(attacker, target), LogInvalidPlayerCombatTickTarget, ProcessValidatedPlayerCombatTick);
	}

	private ICharacter FindPlayerCombatTarget(Identity target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return this.FindByIdentity<ICharacter>(target);
	}

	private bool IsValidPlayerCombatTarget(ICharacter attacker, ICharacter target)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return target != null && ((IDynel)target).InPlayfield(((PooledObject)this).Identity) && ((IStats)target).Stats[(StatIds)27].Value > 0 && PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(attacker, target);
	}

	private void LogInvalidPlayerCombatTickTarget(ICharacter attacker, ICharacter target)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		LogUtil.Debug((DebugInfoDetail)512, $"CombatTickTargetInvalid attacker={((IEntity)attacker).Identity} target={((ITargetingEntity)attacker).FightingTarget} found={target != null} inPlayfield={target != null && ((IDynel)target).InPlayfield(((PooledObject)this).Identity)} health={((target != null) ? ((IStats)target).Stats[(StatIds)27].Value : 0)}");
	}

	private void ProcessValidatedPlayerCombatTick(ICharacter attacker, ICharacter target)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		CombatAttackSource combatAttackSource = GetCombatAttackSource(attacker);
		DateTime utcNow = DateTime.UtcNow;
		Dictionary<int, DateTime> dictionary = nextCombatTicks;
		Identity identity = ((IEntity)attacker).Identity;
		if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value) && value > utcNow)
		{
			return;
		}
		if (!IsInCombatRange(attacker, target, combatAttackSource.Range))
		{
			TryMoveNpcIntoCombatRange(attacker, target, combatAttackSource.Range);
			Dictionary<int, DateTime> dictionary2 = nextCombatTicks;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(1.0);
			return;
		}
		int value2 = ((IStats)target).Stats[(StatIds)27].Value;
		DamageCalculationResult damageCalculationResult = CalculateCombatDamageDetailed(attacker, combatAttackSource);
		int finalTargetDamage = damageCalculationResult.FinalTargetDamage;
		int num = Math.Max(0, value2 - finalTargetDamage);
		bool flag = num == 0;
		AnnounceCombatDamage(attacker, target, finalTargetDamage, combatAttackSource, (!combatAttackSource.UsesEquippedWeapon) ? CombatDamageSource.UnarmedAutoAttack : CombatDamageSource.WeaponAutoAttack);
		((IStats)target).Stats[(StatIds)27].Value = num;
		runtimeSystems.SendChangedStats(target, SendChangedStats);
		LogUtil.Debug((DebugInfoDetail)4, $"Combat hit attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} damage={finalTargetDamage} health={num}/{((IStats)target).Stats[(StatIds)1].Value} weaponBased={(combatAttackSource.UsesEquippedWeapon ? 1 : 0)} slot={combatAttackSource.AttackInfoWeaponSlot}");
		TryWriteWeaponDamageEvidence(attacker, target, combatAttackSource, damageCalculationResult, value2, num);
		if (flag)
		{
			HandleCombatKillingHit(attacker, target);
			return;
		}
		Dictionary<int, DateTime> dictionary3 = nextCombatTicks;
		identity = ((IEntity)attacker).Identity;
		dictionary3[((Identity)(ref identity)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(combatAttackSource.RechargeSeconds);
	}

	private int CalculateCombatDamage(ICharacter attacker, CombatAttackSource attackSource)
	{
		return CalculateCombatDamageDetailed(attacker, attackSource).FinalTargetDamage;
	}

	private DamageCalculationResult CalculateCombatDamageDetailed(ICharacter attacker, CombatAttackSource attackSource)
	{
		return CombatDamageRules.CalculateDetailed(attackSource.MinDamage, attackSource.MaxDamage, attackSource.DamageBonus, ((IStats)attacker).Stats[(StatIds)54].Value, ((IDynel)attacker).Controller is PlayerController, null);
	}

	internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
	{
		return runtimeSystems.IsInNpcCombatRange(attacker, target, range);
	}

	internal static double GetCombatDistance(ICharacter attacker, ICharacter target)
	{
		return PlayfieldNpcCombatMovementRuntimeService.GetCombatDistance(attacker, target);
	}

	internal static bool IsCapturedCleaningRobot(ICharacter character)
	{
		return PlayfieldNpcCombatMovementRuntimeService.IsCapturedCleaningRobot(character);
	}

	internal static void LogNpcBrain(string state, string reason, ICharacter attacker, ICharacter target, double range, double distance)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] obj = new object[6] { state, reason, null, null, null, null };
		Identity val = ((IEntity)attacker).Identity;
		obj[2] = ((Identity)(ref val)).ToString(true);
		string text;
		if (target != null)
		{
			val = ((IEntity)target).Identity;
			text = ((Identity)(ref val)).ToString(true);
		}
		else
		{
			val = Identity.None;
			text = ((Identity)(ref val)).ToString(true);
		}
		obj[3] = text;
		obj[4] = distance;
		obj[5] = range;
		LogUtil.Debug((DebugInfoDetail)4, string.Format(invariantCulture, "NPCBRAIN state={0} reason={1} npc={2} target={3} dist={4:0.00} range={5:0.00}", obj));
	}

	private void AnnounceCombatDamage(ICharacter attacker, ICharacter target, int damage, CombatAttackSource attackSource, CombatDamageSource source)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Expected O, but got Unknown
		LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackInfoSend source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage} u2={attackSource.AttackInfoAmmoCount} u3={attackSource.AttackInfoWeaponSlot} u4={attackSource.AttackInfoUnk1} u5={attackSource.AttackInfoHitType} u6={attackSource.AttackInfoWeaponInstance} weaponBased={(attackSource.UsesEquippedWeapon ? 1 : 0)} atkDefault={((IStats)attacker).Stats[(StatIds)292].Value} atkDamageType={((IStats)attacker).Stats[(StatIds)436].Value} atkWeaponType={((IStats)attacker).Stats[(StatIds)1003].Value} atkEquippedWeapons={((IStats)attacker).Stats[(StatIds)274].Value}");
		Announce((MessageBody)new AttackInfoMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Unknown = 0,
			Target = ((IEntity)target).Identity,
			Unknown1 = damage,
			Unknown2 = attackSource.AttackInfoAmmoCount,
			Unknown3 = attackSource.AttackInfoWeaponSlot,
			Unknown4 = attackSource.AttackInfoUnk1,
			Unknown5 = attackSource.AttackInfoHitType,
			Unknown6 = attackSource.AttackInfoWeaponInstance
		});
		AnnounceHealthDamageIfNeeded(attacker, target, damage, source);
	}

	private void AnnounceHealthDamageIfNeeded(ICharacter attacker, ICharacter target, int damage, CombatDamageSource source)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (!ShouldSendHealthDamage(source))
		{
			LogUtil.Debug((DebugInfoDetail)4, $"CombatHealthDamageSkip source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage}");
			return;
		}
		LogUtil.Debug((DebugInfoDetail)4, $"CombatHealthDamageSend source={source} attacker={((IEntity)attacker).Identity} target={((IEntity)target).Identity} dmg={damage}");
		Announce((MessageBody)new HealthDamageMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Unknown1 = damage,
			Unknown2 = 0,
			Unknown3 = 0,
			Unknown4 = 0,
			Target = ((IEntity)target).Identity,
			Unknown5 = 0
		});
	}

	private static bool ShouldSendHealthDamage(CombatDamageSource source)
	{
		return source != 0 && source != CombatDamageSource.UnarmedAutoAttack;
	}

	private CombatAttackSource GetCombatAttackSource(ICharacter attacker)
	{
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		EquippedCombatWeapon equippedCombatWeapon = GetEquippedCombatWeapon(attacker);
		if (equippedCombatWeapon == null)
		{
			LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackSource unarmed attacker={((IEntity)attacker).Identity} mindmg={((IStats)attacker).Stats[(StatIds)286].Value} maxdmg={((IStats)attacker).Stats[(StatIds)285].Value} bonus={((IStats)attacker).Stats[(StatIds)284].Value} defaultattack={((IStats)attacker).Stats[(StatIds)292].Value} damagetype={((IStats)attacker).Stats[(StatIds)436].Value} weapontype={((IStats)attacker).Stats[(StatIds)1003].Value} equippedweapons={((IStats)attacker).Stats[(StatIds)274].Value}");
			int unarmedAttackInfoWeaponSlot = GetUnarmedAttackInfoWeaponSlot(attacker);
			int unarmedAttackDamage = GetUnarmedAttackDamage(attacker, unarmedAttackInfoWeaponSlot);
			return new CombatAttackSource
			{
				MinDamage = unarmedAttackDamage,
				MaxDamage = unarmedAttackDamage,
				DamageBonus = NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)284].Value, 0),
				Range = 8.0,
				RechargeSeconds = (IsCapturedCleaningRobot(attacker) ? 2.7 : 2.0),
				UsesEquippedWeapon = false,
				AttackInfoAmmoCount = -1,
				AttackInfoWeaponSlot = unarmedAttackInfoWeaponSlot,
				AttackInfoUnk1 = 0,
				AttackInfoHitType = 3,
				AttackInfoWeaponInstance = GetUnarmedAttackInfoWeaponInstance(attacker)
			};
		}
		IItem item = equippedCombatWeapon.Item;
		int num = NormalizeCombatItemStat(item.GetAttribute(286), 0);
		int num2 = NormalizeCombatItemStat(item.GetAttribute(285), 0);
		int num3 = NormalizeCombatItemStat(item.GetAttribute(284), 0);
		LogUtil.Debug((DebugInfoDetail)4, $"CombatAttackSource weapon attacker={((IEntity)attacker).Identity} item={item.LowID}/{item.HighID} slot={equippedCombatWeapon.Slot} min={num} max={num2} rangeRaw={item.GetAttribute(287)}");
		return new CombatAttackSource
		{
			MinDamage = num,
			MaxDamage = num2,
			DamageBonus = 0,
			WeaponLowId = item.LowID,
			WeaponHighId = item.HighID,
			WeaponQualityLevel = item.Quality,
			RawDamageType = item.GetAttribute(436),
			AttackSkillDefinitions = GetAttackSkillDefinitions(item),
			AttackSkillValues = GetAttackSkillValues(attacker, item),
			EffectiveAttackRating = GetEffectiveAttackRating(attacker, item),
			AddAllOff = TryGetStatValue(attacker, 276),
			Range = NormalizeCombatRange(item.GetAttribute(287)),
			RechargeSeconds = NormalizeCombatDelaySeconds(item.GetAttribute(294), item.GetAttribute(210)),
			UsesEquippedWeapon = true,
			AttackInfoAmmoCount = 40,
			AttackInfoWeaponSlot = equippedCombatWeapon.Slot,
			AttackInfoUnk1 = 4,
			AttackInfoHitType = 3,
			AttackInfoWeaponInstance = 0
		};
	}

	private void TryWriteWeaponDamageEvidence(ICharacter attacker, ICharacter target, CombatAttackSource attackSource, DamageCalculationResult damageResult, int targetHealthBefore, int targetHealthAfter)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		string environmentVariable = Environment.GetEnvironmentVariable("AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_SESSION");
		if (string.IsNullOrEmpty(environmentVariable) || attacker == null || target == null || attackSource == null || !attackSource.UsesEquippedWeapon)
		{
			return;
		}
		string text = Environment.GetEnvironmentVariable("AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_DIR");
		if (string.IsNullOrEmpty(text))
		{
			text = Path.Combine(".local", "weapon-damage-evidence", environmentVariable);
		}
		try
		{
			string text2 = Path.Combine(text, "raw");
			Directory.CreateDirectory(text2);
			string text3 = "null";
			if (TryMapRawDamageType(attackSource.RawDamageType, out var damageType) && DamageCalculator.TryGetArmorStatForDamageType(damageType, out var statId))
			{
				int? num = TryGetStatValue(target, statId);
				text3 = (num.HasValue ? num.Value.ToString(CultureInfo.InvariantCulture) : "null");
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			object[] array = new object[24];
			array[0] = JsonEscape(environmentVariable);
			array[1] = DateTime.UtcNow;
			Identity identity = ((IEntity)attacker).Identity;
			array[2] = JsonEscape(((Identity)(ref identity)).ToString(true));
			identity = ((IEntity)target).Identity;
			array[3] = JsonEscape(((Identity)(ref identity)).ToString(true));
			array[4] = JsonEscape(attackSource.WeaponLowId.ToString(CultureInfo.InvariantCulture));
			array[5] = attackSource.WeaponHighId;
			array[6] = attackSource.WeaponQualityLevel;
			array[7] = attackSource.MinDamage;
			array[8] = attackSource.MaxDamage;
			array[9] = attackSource.DamageBonus;
			array[10] = attackSource.RawDamageType;
			array[11] = JsonEscape(damageType.ToString());
			array[12] = JsonEscape(attackSource.AttackSkillDefinitions);
			array[13] = JsonEscape(attackSource.AttackSkillValues);
			array[14] = NullableIntJson(attackSource.EffectiveAttackRating);
			array[15] = NullableIntJson(attackSource.AddAllOff);
			array[16] = text3;
			array[17] = ((attackSource.AttackInfoHitType == 3) ? "KnownNormal" : "UnknownHitKind");
			array[18] = attackSource.AttackInfoHitType;
			array[19] = damageResult.BaseRoll;
			array[20] = JsonEscape(damageResult.Strategy.ToString());
			array[21] = damageResult.FinalTargetDamage;
			array[22] = targetHealthBefore;
			array[23] = targetHealthAfter;
			string text4 = string.Format(invariantCulture, "{{\"schemaVersion\":\"1.0\",\"sessionId\":\"{0}\",\"timestampUtc\":\"{1:O}\",\"sourceKind\":\"PrivateServerControlled\",\"eventKind\":\"ordinary-weapon-hit\",\"attackerIdentity\":\"{2}\",\"targetIdentity\":\"{3}\",\"weaponTemplateIdentity\":\"{4}\",\"weaponHighId\":{5},\"weaponQualityLevel\":{6},\"weaponMinimum\":{7},\"weaponMaximum\":{8},\"legacyDamageBonus\":{9},\"rawDamageType\":{10},\"mappedDamageType\":\"{11}\",\"attackSkillDefinitions\":\"{12}\",\"attackSkillValues\":\"{13}\",\"effectiveAttackRating\":{14},\"addAllOff\":{15},\"targetMatchingArmor\":{16},\"hitKind\":\"{17}\",\"attackInfoHitType\":{18},\"baseRoll\":{19},\"selectedProductionStrategy\":\"{20}\",\"observedDamage\":{21},\"targetHealthBefore\":{22},\"targetHealthAfter\":{23},\"multipleDamageSourcesPossible\":false,\"externalDamagePossible\":false,\"packetOrderComplete\":true,\"criticalStateEvidencePresent\":true,\"evidenceReference\":\"ZoneEngine weapon-damage evidence log\"}}", array);
			File.AppendAllText(Path.Combine(text2, "server-weapon-damage-events.jsonl"), text4 + Environment.NewLine);
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "WeaponDamageEvidenceLog failed: " + ex.Message);
		}
	}

	private static string GetAttackSkillDefinitions(IItem weapon)
	{
		if (weapon == null || !ItemLoader.ItemList.TryGetValue(weapon.LowID, out var value) || value.Attack == null)
		{
			return string.Empty;
		}
		return string.Join(",", from x in value.Attack
			orderby x.Key
			select x.Key + ":" + x.Value);
	}

	private static string GetAttackSkillValues(ICharacter attacker, IItem weapon)
	{
		if (attacker == null || weapon == null || !ItemLoader.ItemList.TryGetValue(weapon.LowID, out var value) || value.Attack == null)
		{
			return string.Empty;
		}
		return string.Join(",", value.Attack.OrderBy((KeyValuePair<int, int> x) => x.Key).Select(delegate(KeyValuePair<int, int> x)
		{
			int? num = TryGetStatValue(attacker, x.Key);
			return x.Key + ":" + (num.HasValue ? num.Value.ToString(CultureInfo.InvariantCulture) : "missing");
		}));
	}

	private static int? GetEffectiveAttackRating(ICharacter attacker, IItem weapon)
	{
		if (attacker == null || weapon == null || !ItemLoader.ItemList.TryGetValue(weapon.LowID, out var value) || value.Attack == null || value.Attack.Count == 0)
		{
			return null;
		}
		int num = 0;
		foreach (KeyValuePair<int, int> item in value.Attack)
		{
			int? num2 = TryGetStatValue(attacker, item.Key);
			if (!num2.HasValue)
			{
				return null;
			}
			num += num2.Value * item.Value / 100;
		}
		return num;
	}

	private static int? TryGetStatValue(ICharacter character, int statId)
	{
		if (character == null || ((IStats)character).Stats == null || ((IStats)character).Stats.All == null)
		{
			return null;
		}
		IStat val = ((IStats)character).Stats.All.SingleOrDefault((IStat x) => x.StatId == statId);
		return (val == null) ? null : new int?(val.Value);
	}

	private static bool TryMapRawDamageType(int rawDamageType, out DamageType damageType)
	{
		switch (rawDamageType)
		{
		case 90:
			damageType = DamageType.Projectile;
			return true;
		case 91:
			damageType = DamageType.Melee;
			return true;
		case 92:
			damageType = DamageType.Energy;
			return true;
		case 93:
			damageType = DamageType.Chemical;
			return true;
		case 94:
			damageType = DamageType.Radiation;
			return true;
		case 95:
			damageType = DamageType.Cold;
			return true;
		case 96:
			damageType = DamageType.Poison;
			return true;
		case 97:
			damageType = DamageType.Fire;
			return true;
		case 168:
			damageType = DamageType.Nano;
			return true;
		default:
			damageType = DamageType.Unknown;
			return false;
		}
	}

	private static string NullableIntJson(int? value)
	{
		return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
	}

	private static string JsonEscape(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private int GetUnarmedAttackInfoWeaponSlot(ICharacter attacker)
	{
		return 0;
	}

	private int GetUnarmedAttackDamage(ICharacter attacker, int attackInfoWeaponSlot)
	{
		return Math.Max(NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)286].Value, 0), NormalizeCombatItemStat(((IStats)attacker).Stats[(StatIds)285].Value, 0));
	}

	private int GetUnarmedAttackInfoWeaponInstance(ICharacter attacker)
	{
		return 100;
	}

	private EquippedCombatWeapon GetEquippedCombatWeapon(ICharacter attacker)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		if (((IItemContainer)attacker).BaseInventory == null || !((IItemContainer)attacker).BaseInventory.Pages.ContainsKey(101))
		{
			Dictionary<int, int> dictionary = lastCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary.Remove(((Identity)(ref identity)).Instance);
			return null;
		}
		IInventoryPage val = ((IItemContainer)attacker).BaseInventory.Pages[101];
		IItem item = val[6];
		IItem item2 = val[8];
		bool flag = IsWieldableCombatWeapon(item);
		bool flag2 = IsWieldableCombatWeapon(item2);
		if (flag && flag2)
		{
			identity = ((IEntity)attacker).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			if (lastCombatWeaponSlots.TryGetValue(instance, out var value) && value == 6)
			{
				lastCombatWeaponSlots[instance] = 8;
				return new EquippedCombatWeapon
				{
					Item = item2,
					Slot = 8
				};
			}
			lastCombatWeaponSlots[instance] = 6;
			return new EquippedCombatWeapon
			{
				Item = item,
				Slot = 6
			};
		}
		if (flag)
		{
			Dictionary<int, int> dictionary2 = lastCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary2[((Identity)(ref identity)).Instance] = 6;
			return new EquippedCombatWeapon
			{
				Item = item,
				Slot = 6
			};
		}
		if (flag2)
		{
			Dictionary<int, int> dictionary3 = lastCombatWeaponSlots;
			identity = ((IEntity)attacker).Identity;
			dictionary3[((Identity)(ref identity)).Instance] = 8;
			return new EquippedCombatWeapon
			{
				Item = item2,
				Slot = 8
			};
		}
		Dictionary<int, int> dictionary4 = lastCombatWeaponSlots;
		identity = ((IEntity)attacker).Identity;
		dictionary4.Remove(((Identity)(ref identity)).Instance);
		return null;
	}

	private static int NormalizeCombatItemStat(int value, int fallback)
	{
		return (value == 1234567890) ? fallback : value;
	}

	private bool IsWieldableCombatWeapon(IItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (item.ItemActions != null && item.ItemActions.Any((AOAction x) => (int)x.ActionType == 8))
		{
			return true;
		}
		return NormalizeCombatItemStat(item.GetAttribute(286), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(285), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(287), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(294), 0) > 0 || NormalizeCombatItemStat(item.GetAttribute(210), 0) > 0;
	}

	private static double NormalizeCombatRange(int range)
	{
		int num = NormalizeCombatItemStat(range, 0);
		if (num <= 0)
		{
			return 8.0;
		}
		return (num > 1000) ? ((double)num / 100.0) : ((double)num);
	}

	private static double NormalizeCombatDelaySeconds(int attackDelay, int rechargeDelay)
	{
		int num = NormalizeCombatItemStat(attackDelay, 0);
		int num2 = NormalizeCombatItemStat(rechargeDelay, 0);
		int num3 = num + num2;
		if (num3 <= 0)
		{
			return 2.0;
		}
		return Math.Max(0.25, (double)num3 / 100.0);
	}

	internal void UpdateNpcMeleeFollowHold(ICharacter attacker, ICharacter target, double range)
	{
		runtimeSystems.UpdateNpcMeleeFollowHold(attacker, target, range, MoveNpcToCombatPosition, LogNpcBrain);
	}

	internal bool HasActiveNpcChaseNavigation(ICharacter attacker)
	{
		return runtimeSystems.HasActiveNpcChaseNavigation(attacker);
	}

	internal bool IsNpcAttackPathTraversable(ICharacter attacker, ICharacter target)
	{
		return runtimeSystems.IsNpcAttackPathTraversable(attacker, target);
	}

	internal void HoldNpcAtCombatPosition(ICharacter attacker, ICharacter target)
	{
		runtimeSystems.HoldNpcAtCombatPosition(attacker, target);
	}

	internal bool TryResolveCapturedNpcMovementDestination(ICharacter attacker, ICharacter target, double range, DateTime utcNow, out Vector3 destination)
	{
		return runtimeSystems.TryResolveCapturedNpcMovementDestination(attacker, target, range, utcNow, out destination);
	}

	internal void TryMoveNpcIntoCombatRange(ICharacter attacker, ICharacter target, double range)
	{
		runtimeSystems.TryMoveNpcIntoCombatRange(attacker, target, range, MoveNpcToCombatPosition, LogNpcBrain);
	}

	private void MoveNpcToCombatPosition(ICharacter attacker, Vector3 nextPosition)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		((IDynel)attacker).Coordinates(nextPosition);
		Announce((MessageBody)new SetPosMessage
		{
			Identity = ((IEntity)attacker).Identity,
			Coordinates = new Vector3
			{
				X = nextPosition.xf,
				Y = nextPosition.yf,
				Z = nextPosition.zf
			},
			Unknown1 = 0
		});
	}

	private void KillNpcTarget(ICharacter attacker, ICharacter target)
	{
		if (((IDynel)target).Controller is NPCController)
		{
			runtimeSystems.BeginNpcDeath(attacker, target);
		}
	}

	internal void HandleCombatKillingHit(ICharacter attacker, ICharacter target)
	{
		if (((IDynel)target).Controller is NPCController)
		{
			KillNpcTarget(attacker, target);
		}
		else if (((IDynel)target).Controller is PlayerController)
		{
			AlienXpRuntimeService.RecordPlayerKilledByInvader(attacker, target);
			CombatXpRuntimeService.ApplyDeathUninsuredXpLoss(target);
			runtimeSystems.BeginPlayerDeath(target, KillPlayerTarget);
		}
		else if (((IDynel)attacker).Controller is NPCController)
		{
			ClearNpcFightingTarget(attacker);
		}
		else
		{
			runtimeSystems.ClearPlayerFightingTarget(attacker, ClearCombatTracking);
		}
	}

	public void ForcePlayerDeath(ICharacter target)
	{
		if (target != null && ((IDynel)target).Controller is PlayerController)
		{
			runtimeSystems.BeginPlayerDeath(target, KillPlayerTarget);
		}
	}

	internal void StopDyingNpcCombatState(ICharacter target)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		runtimeSystems.StopDyingNpcCombatState(target);
		bool flag = IsCapturedCleaningRobot(target);
		Identity identity = ((IEntity)target).Identity;
		CapturedEnemyCombatContract contract;
		bool flag2 = CapturedEnemyCombatRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out contract) && contract.SendStopFightOnDeath;
		if (flag)
		{
			PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "robot-stop-fight", "StopFight", ((IEntity)target).Identity);
		}
		if (flag || flag2)
		{
			SendCombatStopMessage(target);
		}
	}

	internal void AwardCombatXp(ICharacter attacker, ICharacter target)
	{
		CombatXpRuntimeService.AwardCombatXp(attacker, target, delegate(ICharacter character, string text)
		{
			SendRewardFeedback(character, text);
		});
		MissionCompleteService.TryCompleteIfMissionTargetKilled(attacker, target, "KillTarget");
	}

	private void KillPlayerTarget(ICharacter target)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if (((IDynel)target).Controller is PlayerController)
		{
			MarkPlayerDead(target);
			runtimeSystems.RunPlayerDeathStatUpdateSequence(target, SendChangedStats, delegate(ICharacter x)
			{
				runtimeSystems.CleanupPlayerDeathCombat(x, ClearCombatTracking, StopFightingDeadTarget, SendCombatStopMessage);
			}, SendPlayerDeathAnimation);
			LogUtil.Debug((DebugInfoDetail)4, $"Player died target={((IEntity)target).Identity}");
		}
	}

	public bool TryUseCorpse(ICharacter looter, Identity corpseIdentity)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		TimeSpan itemLootLifetime = CombatCorpseRules.RegularLootCorpseLifetime;
		TimeSpan emptyCleanupDelay = CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay;
		if (corpses.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
		{
			itemLootLifetime = value.ItemLootLifetime;
			emptyCleanupDelay = value.EmptyCleanupDelay;
		}
		return runtimeSystems.TryUseCorpse(looter, corpseIdentity, corpses, itemLootLifetime, emptyCleanupDelay, (CorpseState corpse) => corpse.DeadNpcIdentity, (CorpseState corpse) => corpse.ExpiresAtUtc, (CorpseState corpse) => corpse.IsEmpty, (CorpseState corpse) => corpse.Opened, delegate(CorpseState corpse, bool opened)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			corpseInventoryService.MarkOpened(corpse.CorpseIdentity, opened, DateTime.UtcNow);
		}, (CorpseState corpse) => corpse.LootClass, DespawnCorpse, ExtendCorpseLifetime, delegate(CorpseState corpse)
		{
			corpse.InventoryHandle = AllocateCorpseInventoryHandle();
		}, SendCorpseInventoryUpdate, SendCorpseCloseAction, SendUseActionFinished, ScheduleCorpseCreditAward, ScheduleCorpseDespawn);
	}

	public bool TryUseDeadNpcCorpse(ICharacter looter, Identity deadNpcIdentity, out Identity corpseIdentity)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return runtimeSystems.TryUseDeadNpcCorpse(looter, deadNpcIdentity, corpses.Values, (CorpseState corpse) => corpse.CorpseIdentity, (CorpseState corpse) => corpse.DeadNpcIdentity, (CorpseState corpse) => corpse.CreatedAtUtc, TryUseCorpse, out corpseIdentity);
	}

	public bool TryLootCorpseItem(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		int requestedLootSlot = ((Identity)(ref sourceContainer)).Instance & 0xFFFF;
		int corpseInventoryHandle = (((Identity)(ref sourceContainer)).Instance >> 16) & 0xFFFF;
		CorpseState selectedCorpse = corpses.Values.FirstOrDefault((CorpseState corpse) => corpse.InventoryHandle == corpseInventoryHandle);
		TimeSpan itemLootLifetime = ((selectedCorpse == null) ? CombatCorpseRules.RegularLootCorpseLifetime : selectedCorpse.ItemLootLifetime);
		TimeSpan emptyCleanupDelay = ((selectedCorpse == null) ? CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay : selectedCorpse.EmptyCleanupDelay);
		return runtimeSystems.TryLootCorpseItem(looter, sourceContainer, target, targetPlacement, corpses.Values, (CorpseState corpse) => corpse.InventoryHandle, (CorpseState corpse) => corpse.CorpseIdentity, (CorpseState corpse) => corpse.ExpiresAtUtc, (CorpseState corpse) => corpse.IsEmpty, (CorpseState corpse) => corpse.LootItems.Count((CorpseLootItem x) => !x.Looted), (CorpseState corpse) => FindCorpseLootItem(corpse, requestedLootSlot), (CorpseLootItem lootItem) => lootItem.Item, (CorpseLootItem lootItem) => lootItem.Slot, delegate(CorpseLootItem lootItem, bool looted)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (looted && selectedCorpse != null)
			{
				corpseInventoryService.RemoveItem(selectedCorpse.CorpseIdentity, lootItem.Slot, DateTime.UtcNow);
			}
		}, delegate(CorpseState corpse, bool opened)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			corpseInventoryService.MarkOpened(corpse.CorpseIdentity, opened, DateTime.UtcNow);
		}, runtimeSystems.CharacterHasUniqueItemAlready, delegate(ICharacter character, string text)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, text, 0, 0);
		}, SendUseActionFinished, runtimeSystems.TryAddCorpseLootItem, SendCorpseContainerAddItem, ScheduleCorpseDespawn, ExtendCorpseLifetime, DespawnCorpse, itemLootLifetime, emptyCleanupDelay);
	}

	private void SendCorpseContainerAddItem(ICharacter looter, Identity sourceContainer, int targetPlacement)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		if (((IDynel)looter).Controller != null && ((IDynel)looter).Controller.Client != null)
		{
			IZoneClient client = ((IDynel)looter).Controller.Client;
			ContainerAddItemMessage val = new ContainerAddItemMessage
			{
				Identity = ((IEntity)looter).Identity,
				SourceContainer = sourceContainer,
				TargetPlacement = targetPlacement,
				Target = ((IEntity)looter).Identity,
				Unknown = 0
			};
			Identity identity = ((IEntity)looter).Identity;
			client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
		}
	}

	internal void MarkNpcDead(ICharacter target)
	{
		((IStats)target).Stats[(StatIds)27].Value = 0;
		((IStats)target).Stats[(StatIds)7].Value = 0;
		((IStats)target).Stats[(StatIds)423].Value = 0;
		((IStats)target).Stats[(StatIds)588].Value = 0;
		((IStats)target).Stats[(StatIds)34].Value = 1;
		((IStats)target).Stats[(StatIds)99].Value = DeathAnimationKeyFor(target);
		((IStats)target).Stats[(StatIds)417].Value = DeathAnimationKeyFor(target);
		((IStats)target).Stats[(StatIds)387].Value = DeathAnimationKeyFor(target);
		((IStats)target).Stats[(StatIds)343].Value = 0;
		((IStats)target).Stats[(StatIds)364].Value = 0;
		((IInstancedEntity)target).DoNotDoTimers = true;
	}

	private void MarkPlayerDead(ICharacter target)
	{
		((IStats)target).Stats[(StatIds)27].Value = 0;
		((IStats)target).Stats[(StatIds)7].Value = 0;
		((IStats)target).Stats[(StatIds)423].Value = 0;
		((IStats)target).Stats[(StatIds)588].Value = 0;
		((IStats)target).Stats[(StatIds)34].Value = 1;
		((IStats)target).Stats[(StatIds)343].Value = 0;
		((IStats)target).Stats[(StatIds)364].Value = 0;
	}

	private void MarkPlayerRespawned(ICharacter target)
	{
		target.CalculateSkills();
		int num = Math.Max(1, ((IStats)target).Stats[(StatIds)1].Value);
		((IStats)target).Stats[(StatIds)27].Value = Math.Max(1, num / 3);
		((IStats)target).Stats[(StatIds)7].Value = 0;
		((IStats)target).Stats[(StatIds)423].Value = 0;
		((IStats)target).Stats[(StatIds)588].Value = 0;
		((IStats)target).Stats[(StatIds)34].Value = 0;
		((IStats)target).Stats[(StatIds)34].BaseValue = 0u;
		((IStats)target).Stats[(StatIds)173].Value = 3;
		((IStats)target).Stats[(StatIds)173].BaseValue = 3u;
		((IStats)target).Stats[(StatIds)174].Value = 3;
		((IStats)target).Stats[(StatIds)174].BaseValue = 3u;
		((IStats)target).Stats[(StatIds)348].Value = 3;
		((IStats)target).Stats[(StatIds)348].BaseValue = 3u;
		((IStats)target).Stats[(StatIds)339].Value = 0;
		((IStats)target).Stats[(StatIds)339].BaseValue = 0u;
		((IStats)target).Stats[(StatIds)338].Value = 0;
		((IStats)target).Stats[(StatIds)338].BaseValue = 0u;
	}

	private void SendDeathRespawnStateStats(ICharacter target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)target).Identity;
		((N3Message)val).Unknown = 0;
		val.Stats = new GameTuple<CharacterStat, uint>[9]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)27,
				Value2 = (uint)Math.Max(0, ((IStats)target).Stats[(StatIds)27].Value)
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)214,
				Value2 = (uint)Math.Max(0, ((IStats)target).Stats[(StatIds)214].Value)
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)34,
				Value2 = 0u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)7,
				Value2 = 0u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)423,
				Value2 = 0u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)588,
				Value2 = 0u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)348,
				Value2 = 3u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)339,
				Value2 = 0u
			},
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)338,
				Value2 = 0u
			}
		};
		((IDynel)target).Send((MessageBody)(object)val, false);
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player death respawn state stats target={0} hp={1}/{2} nano={3} deadTimer=0", ((IEntity)target).Identity, ((IStats)target).Stats[(StatIds)27].Value, ((IStats)target).Stats[(StatIds)1].Value, ((IStats)target).Stats[(StatIds)214].Value));
	}

	private void SendDeathSocialStatus(ICharacter target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)target).Identity;
		((N3Message)val).Unknown = 1;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)521,
				Value2 = 0u
			}
		};
		((IDynel)target).Send((MessageBody)(object)val, false);
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player death social status target={0} socialStatus=0 unknown=1", ((IEntity)target).Identity));
	}

	private void SendCapturedPrivateCityEntrySocialStatus(ICharacter target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)target).Identity;
		((N3Message)val).Unknown = 1;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)521,
				Value2 = 4u
			}
		};
		((IDynel)target).Send((MessageBody)(object)val, false);
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Private city entry social status target={0} socialStatus=4 unknown=1 evidence=live_capture_20260622-101935", ((IEntity)target).Identity));
	}

	private void ResolvePlayerRespawnLocation(ICharacter character, out Coordinate destination, out Identity destinationPlayfield)
	{
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected O, but got Unknown
		ResolveStarterRespawnLocation(character, out destination, out destinationPlayfield);
		int value = ((IStats)character).Stats[(StatIds)595].Value;
		int value2 = ((IStats)character).Stats[(StatIds)596].Value;
		int value3 = ((IStats)character).Stats[(StatIds)597].Value;
		if (value <= 0 || value2 <= 0 || value3 <= 0)
		{
			LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player respawn using starter fallback target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}", ((IEntity)character).Identity, ((Identity)(ref destinationPlayfield)).Type, ((Identity)(ref destinationPlayfield)).Instance, destination.x, destination.y, destination.z));
			return;
		}
		destination = new Coordinate((float)value2, ((IDynel)character).RawCoordinates.Y, (float)value3);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51101;
		((Identity)(ref val)).Instance = value;
		destinationPlayfield = val;
		if (ShadowlandsGardenSaveRuntimeService.IsGardenPlayfield(value))
		{
			ShadowlandsGardenSaveRuntimeService.GetGardenSaveSpot(out var num, out var y, out var z);
			destination = new Coordinate(num, y, z);
		}
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player respawn using temp save target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}", ((IEntity)character).Identity, ((Identity)(ref destinationPlayfield)).Type, ((Identity)(ref destinationPlayfield)).Instance, destination.x, destination.y, destination.z));
	}

	private static void ResolveStarterRespawnLocation(ICharacter character, out Coordinate destination, out Identity destinationPlayfield)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		int instance = 4582;
		int num = 939;
		int num2 = 20;
		int num3 = 732;
		Identity val;
		if (character != null && ((IInstancedEntity)character).Playfield != null)
		{
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			if (((Identity)(ref val)).Instance == 4001)
			{
				instance = 4001;
				num = 850;
				num2 = 43;
				num3 = 565;
			}
		}
		destination = new Coordinate((float)num, (float)num2, (float)num3);
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51101;
		((Identity)(ref val)).Instance = instance;
		destinationPlayfield = val;
	}

	internal void SendNpcDeathAnimation(ICharacter target)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected O, but got Unknown
		PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "character-action-death-parameter2", "CharacterAction Death", ((IEntity)target).Identity, "Parameter2=" + DeathAnimationKeyFor(target));
		Announce((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)target).Identity,
			Unknown = 0,
			Action = (CharacterActionType)99,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = DeathAnimationKeyFor(target),
			Unknown2 = 0
		});
	}

	private void SendPlayerDeathAnimation(ICharacter target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		Announce((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)target).Identity,
			Unknown = 0,
			Action = (CharacterActionType)99,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = 500,
			Unknown2 = 0
		});
	}

	private void SendDeathRespawnAction(ICharacter character)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		((IDynel)character).Send((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)171,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 1000020,
			Parameter2 = 295830,
			Unknown2 = 0
		}, false);
	}

	private void SendDeathRespawnPlayfieldReadyBlock(ZoneClient client, ICharacter character)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		SendEmptyPlayfieldTowersAndCities(client);
		client.SendCompressed((MessageBody)new SpecialAttackWeaponMessage
		{
			Identity = ((IEntity)character).Identity,
			Specials = CreateDefaultPlayerSpecialAttacks(),
			Unknown1 = 6,
			Unknown2 = 6,
			Unknown3 = 6,
			Unknown4 = 6,
			Unknown5 = 100
		});
	}

	private void SendEmptyPlayfieldTowersAndCities(ZoneClient client)
	{
		SendPlayfieldTowersAndCities(client, 0, new byte[0]);
	}

	private void SendPlayfieldTowersAndCities(ZoneClient client, byte cityUnknown, byte[] cityPayload)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		Identity identity = ((PooledObject)this).Identity;
		((Identity)(ref val)).Instance = ((Identity)(ref identity)).Instance;
		Identity identity2 = val;
		PlayfieldAllTowersMessage val2 = new PlayfieldAllTowersMessage();
		((N3Message)val2).Identity = identity2;
		val2.Unknown1 = (TowerProxyBase[])(object)new TowerProxyBase[0];
		client.SendCompressed((MessageBody)(object)val2);
		PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-playfield-all-towers", "PlayfieldAllTowers", identity2);
		PlayfieldAllCitiesMessage val3 = new PlayfieldAllCitiesMessage();
		((N3Message)val3).Identity = identity2;
		((N3Message)val3).Unknown = cityUnknown;
		val3.Payload = cityPayload ?? new byte[0];
		client.SendCompressed((MessageBody)(object)val3);
		PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-playfield-all-cities", "PlayfieldAllCities", identity2);
		PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-towers-cities-sent", "PrivateCityTowersCitiesSent", identity2, "cityUnknown=" + cityUnknown + " cityPayloadBytes=" + ((cityPayload != null) ? cityPayload.Length : 0));
	}

	private static SpecialAttack[] CreateDefaultPlayerSpecialAttacks()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		return (SpecialAttack[])(object)new SpecialAttack[3]
		{
			new SpecialAttack
			{
				Unknown1 = 43712,
				Unknown2 = 144745,
				Unknown3 = 100,
				Unknown4 = "MAAT"
			},
			new SpecialAttack
			{
				Unknown1 = 42033,
				Unknown2 = 42032,
				Unknown3 = 144,
				Unknown4 = "DIIT"
			},
			new SpecialAttack
			{
				Unknown1 = 70292,
				Unknown2 = 70293,
				Unknown3 = 142,
				Unknown4 = "BRAW"
			}
		};
	}

	internal void ClearCombatTracking(Identity identity)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		nextCombatTicks.Remove(((Identity)(ref identity)).Instance);
		lastCombatWeaponSlots.Remove(((Identity)(ref identity)).Instance);
		runtimeSystems.ClearNpcCombatTracking(identity);
	}

	private void SendPlayerCorpseFullUpdate(ICharacter target, Identity corpseIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		SendCorpseFullUpdate(target, corpseIdentity);
		LogUtil.Debug((DebugInfoDetail)4, string.Format(CultureInfo.InvariantCulture, "Player corpse visual sent target={0} corpse={1}", ((IEntity)target).Identity, corpseIdentity));
	}

	private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		int num = CorpseCatMeshFor(target);
		int num2 = CorpseMonsterDataFor(target);
		int num3 = 0;
		if (!corpses.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
		{
			return;
		}
		value.VisualSource = target;
		List<ICharacter> list = runtimeSystems.VisibleRecipientsForSource(((IEntity)target).Identity).ToList();
		if (((IDynel)target).Controller != null && ((IDynel)target).Controller.Client != null && list.All((ICharacter x) => ((IEntity)x).Identity != ((IEntity)target).Identity))
		{
			list.Add(target);
		}
		foreach (ICharacter item in list.OrderBy(delegate(ICharacter x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Expected I4, but got Unknown
			Identity identity2 = ((IEntity)x).Identity;
			return (int)((Identity)(ref identity2)).Type;
		}).ThenBy(delegate(ICharacter x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity identity = ((IEntity)x).Identity;
			return ((Identity)(ref identity)).Instance;
		}))
		{
			if (SendCorpseFullUpdateToRecipient(value, item, num, num2))
			{
				num3++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "CorpseFullUpdate visual target={0} corpse={1} catMesh={2} monsterData={3} credits={4} scale={5} sex={6} breed={7} race={8} recipients={9} pos=({10},{11},{12})", ((IEntity)target).Identity, corpseIdentity, num, num2, CorpseCreditsFor(corpseIdentity), ((IStats)target).Stats[(StatIds)360].Value, ((IStats)target).Stats[(StatIds)59].Value, ((IStats)target).Stats[(StatIds)4].Value, ((IStats)target).Stats[(StatIds)89].Value, num3, ((IDynel)target).RawCoordinates.X, ((IDynel)target).RawCoordinates.Y, ((IDynel)target).RawCoordinates.Z));
	}

	private bool SendCorpseFullUpdateToRecipient(CorpseState corpse, ICharacter recipient, int corpseCatMesh, int corpseMonsterData)
	{
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		ZoneClient zoneClient = ((recipient == null || ((IDynel)recipient).Controller == null) ? null : (((IDynel)recipient).Controller.Client as ZoneClient));
		if (corpse == null || corpse.VisualSource == null || recipient == null || zoneClient == null)
		{
			return false;
		}
		lock (corpseVisibilitySync)
		{
			if (!corpse.VisibleRecipients.Add(((IEntity)recipient).Identity))
			{
				return false;
			}
		}
		try
		{
			zoneClient.SendCompressed(CorpseFullUpdate.Build(corpse.VisualSource, corpse.CorpseIdentity, ((IEntity)recipient).Identity, server.Id, corpseCatMesh, corpseMonsterData, corpse.Credits));
		}
		catch
		{
			lock (corpseVisibilitySync)
			{
				corpse.VisibleRecipients.Remove(((IEntity)recipient).Identity);
			}
			throw;
		}
		return true;
	}

	private void SendExistingCorpseVisibilityToClient(ICharacter recipient)
	{
		if (recipient == null)
		{
			return;
		}
		foreach (CorpseState item in corpses.Values.OrderBy(delegate(CorpseState x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity corpseIdentity = x.CorpseIdentity;
			return ((Identity)(ref corpseIdentity)).Instance;
		}))
		{
			if (item.VisualSource != null && !(((IDynel)item.VisualSource).Coordinates().Distance2D(((IDynel)recipient).Coordinates()) > (double)runtimeSystems.VisibilityEnterRadius))
			{
				SendCorpseFullUpdateToRecipient(item, recipient, CorpseCatMeshFor(item.VisualSource), CorpseMonsterDataFor(item.VisualSource));
			}
		}
	}

	private void RefreshCorpseVisibilityForRecipient(ICharacter recipient)
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		if (recipient == null || ((IDynel)recipient).Controller == null || ((IDynel)recipient).Controller.Client == null)
		{
			return;
		}
		foreach (CorpseState item in corpses.Values.OrderBy(delegate(CorpseState x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity corpseIdentity = x.CorpseIdentity;
			return ((Identity)(ref corpseIdentity)).Instance;
		}))
		{
			if (item.VisualSource == null)
			{
				continue;
			}
			double num = ((IDynel)item.VisualSource).Coordinates().Distance2D(((IDynel)recipient).Coordinates());
			bool flag;
			lock (corpseVisibilitySync)
			{
				flag = item.VisibleRecipients.Contains(((IEntity)recipient).Identity);
			}
			if (!flag && num <= (double)runtimeSystems.VisibilityEnterRadius)
			{
				SendCorpseFullUpdateToRecipient(item, recipient, CorpseCatMeshFor(item.VisualSource), CorpseMonsterDataFor(item.VisualSource));
			}
			else if (flag && num > (double)runtimeSystems.VisibilityLeaveRadius)
			{
				SendVisibilityLeave(recipient, item.CorpseIdentity);
				lock (corpseVisibilitySync)
				{
					item.VisibleRecipients.Remove(((IEntity)recipient).Identity);
				}
			}
		}
	}

	private void SendCorpseDespawn(Identity corpseIdentity)
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		if (!corpses.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
		{
			return;
		}
		Identity[] array;
		lock (corpseVisibilitySync)
		{
			array = (from x in value.VisibleRecipients
				orderby (int)((Identity)(ref x)).Type, ((Identity)(ref x)).Instance
				select x).ToArray();
			value.VisibleRecipients.Clear();
		}
		Identity[] array2 = array;
		foreach (Identity identity in array2)
		{
			ICharacter val = this.FindByIdentity<ICharacter>(identity);
			if (val != null)
			{
				SendVisibilityLeave(val, corpseIdentity);
			}
		}
	}

	private int CorpseCreditsFor(Identity corpseIdentity)
	{
		CorpseState value;
		return corpses.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out value) ? value.Credits : 0;
	}

	private static TimeSpan CorpseLifetimeFor(ICharacter target, CombatCorpseLootClass lootClass)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
			{
				return (lootClass == CombatCorpseLootClass.Empty) ? TimeSpan.FromSeconds(definition.LootedCleanupSeconds) : TimeSpan.FromSeconds(definition.UnlootedCorpseLifetimeSeconds);
			}
		}
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition2))
			{
				return (lootClass == CombatCorpseLootClass.Empty) ? TimeSpan.FromSeconds(definition2.Profile.Corpse.EmptyLifetimeSeconds) : TimeSpan.FromSeconds(definition2.Profile.Corpse.UnlootedLifetimeSeconds);
			}
		}
		return CombatCorpseRules.LifetimeFor(lootClass);
	}

	private static CombatCorpseLootClass CorpseLootClassFor(ICharacter target, IList<CorpseLootItem> lootItems, int credits)
	{
		return CombatCorpseRules.LootClassFor(lootItems.Count, credits, isMajorBoss: false);
	}

	private void ProcessCorpseDespawns()
	{
		DateTime utcNow = DateTime.UtcNow;
		runtimeSystems.ProcessDueNpcCorpseDespawns(utcNow, DespawnCorpse);
		runtimeSystems.ProcessDueCapturedSubwayRespawns(utcNow);
	}

	private void ProcessPendingCorpseSpawns()
	{
		runtimeSystems.ProcessPendingCorpseSpawns(pendingCorpseSpawns, (CorpseState corpse) => corpse.SpawnsAtUtc, (CorpseState corpse) => corpse.CorpseIdentity, (CorpseState corpse) => corpse.DeadNpcIdentity, (Identity identity) => this.FindByIdentity<ICharacter>(identity), RegisterCorpse, TraceCorpseFullUpdate, SendCorpseFullUpdate);
	}

	internal void ScheduleCorpseSpawn(ICharacter target, Identity corpseIdentity)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		DateTime spawnsAtUtc = DateTime.UtcNow + NpcCorpseLifecycleRules.CorpseSpawnDelay;
		Dictionary<int, CorpseState> dictionary = pendingCorpseSpawns;
		Identity identity = ((IEntity)target).Identity;
		dictionary[((Identity)(ref identity)).Instance] = new CorpseState
		{
			CorpseIdentity = corpseIdentity,
			DeadNpcIdentity = ((IEntity)target).Identity,
			Name = "Remains of " + ((INamedEntity)target).Name,
			LootClass = CombatCorpseLootClass.Empty,
			CreatedAtUtc = DateTime.UtcNow,
			SpawnsAtUtc = spawnsAtUtc
		};
		identity = ((IEntity)target).Identity;
		PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "corpse-spawn-scheduled", "CorpseSpawnScheduled", corpseIdentity, "deadNpc=" + ((object)(Identity)(ref identity)).ToString() + " delayMs=" + (int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds);
		LogUtil.Debug((DebugInfoDetail)4, $"Corpse scheduled corpse={corpseIdentity} deadNpc={((IEntity)target).Identity} delayMs={(int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds}");
	}

	private void RegisterCorpse(ICharacter target, Identity corpseIdentity)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Expected O, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		GlobalLootRuntimeService globalLootRuntimeService = GlobalLootRuntimeService;
		Identity identity = ((PooledObject)this).Identity;
		LootGenerationResult lootGenerationResult = globalLootRuntimeService.Generate(target, ((Identity)(ref identity)).Instance);
		List<CorpseLootItem> list = lootGenerationResult.Items.Select((GeneratedLootItem value, int index) => new CorpseLootItem
		{
			Slot = index,
			Item = new Item(value.Quality, value.ItemTemplateId, (value.HighItemTemplateId > 0) ? value.HighItemTemplateId : value.ItemTemplateId)
			{
				MultipleCount = value.Quantity
			},
			LootIdentity = AllocateCorpseLootItemIdentity()
		}).ToList();
		identity = ((PooledObject)this).Identity;
		if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance) && target != null)
		{
			if (MissionInstanceMobCombat.IsFindItemHost(((IEntity)target).Identity))
			{
				identity = ((IEntity)target).Identity;
				MissionInstanceLootCatalog.LootDrop lootDrop = MissionInstanceLootCatalog.ResolveFindItemDrop(((Identity)(ref identity)).Instance);
				list.Insert(0, new CorpseLootItem
				{
					Slot = 0,
					Item = new Item(lootDrop.Quality, lootDrop.LowId, lootDrop.HighId)
					{
						MultipleCount = 1
					},
					LootIdentity = AllocateCorpseLootItemIdentity()
				});
				for (int i = 1; i < list.Count; i++)
				{
					list[i].Slot = i;
				}
			}
			else if (list.Count == 0)
			{
				int value2 = ((IStats)target).Stats[(StatIds)359].Value;
				if (MissionInstanceLootCatalog.TryGetDrop(value2, out var drop) && drop != null)
				{
					list.Add(new CorpseLootItem
					{
						Slot = 0,
						Item = new Item(drop.Quality, drop.LowId, drop.HighId)
						{
							MultipleCount = 1
						},
						LootIdentity = AllocateCorpseLootItemIdentity()
					});
				}
			}
		}
		int credits = lootGenerationResult.Credits;
		CombatCorpseLootClass lootClass = CorpseLootClassFor(target, list, credits);
		TimeSpan timeSpan = CorpseLifetimeFor(target, lootClass);
		TimeSpan itemLootLifetime = CombatCorpseRules.RegularLootCorpseLifetime;
		TimeSpan emptyCleanupDelay = CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay;
		identity = ((IEntity)target).Identity;
		if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
		{
			itemLootLifetime = TimeSpan.FromSeconds(definition.UnlootedCorpseLifetimeSeconds);
			emptyCleanupDelay = TimeSpan.FromSeconds(definition.LootedCleanupSeconds);
		}
		else
		{
			identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition2))
			{
				itemLootLifetime = TimeSpan.FromSeconds(definition2.Profile.Corpse.UnlootedLifetimeSeconds);
				emptyCleanupDelay = TimeSpan.FromSeconds(definition2.Profile.Corpse.LootedCleanupSeconds);
			}
		}
		DateTime expiresAtUtc = DateTime.UtcNow + timeSpan;
		CorpseState obj = new CorpseState
		{
			CorpseIdentity = corpseIdentity,
			DeadNpcIdentity = ((IEntity)target).Identity
		};
		identity = ((PooledObject)this).Identity;
		obj.PlayfieldId = ((Identity)(ref identity)).Instance;
		obj.VisualSource = target;
		obj.VisibleRecipients = new HashSet<Identity>();
		obj.Name = "Remains of " + ((INamedEntity)target).Name;
		obj.LootClass = lootClass;
		obj.CreatedAtUtc = DateTime.UtcNow;
		obj.LootItems = list;
		obj.Credits = credits;
		obj.GenerationResult = lootGenerationResult;
		obj.LootUnresolved = lootGenerationResult.LootUnresolved || lootGenerationResult.CreditsUnresolved;
		obj.RightsPolicy = CorpseLootRightsPolicy.Public;
		obj.InventoryHandle = AllocateCorpseInventoryHandle();
		obj.ItemLootLifetime = itemLootLifetime;
		obj.EmptyCleanupDelay = emptyCleanupDelay;
		obj.ExpiresAtUtc = expiresAtUtc;
		CorpseState corpseState = obj;
		corpseInventoryService.Create(corpseState);
		runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);
		LogUtil.Debug((DebugInfoDetail)128, $"Corpse registered corpse={corpseIdentity} deadNpc={((IEntity)target).Identity} lifetimeSeconds={(int)timeSpan.TotalSeconds} lootClass={corpseState.LootClass} credits={corpseState.Credits}");
	}

	private void TraceCorpseFullUpdate(Identity corpseIdentity, Identity deadNpcIdentity)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		Identity val = deadNpcIdentity;
		PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "corpse-full-update", "CorpseFullUpdate", corpseIdentity, "deadNpc=" + ((object)(Identity)(ref val)).ToString());
	}

	private void DespawnCorpse(int corpseInstance)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (corpses.TryGetValue(corpseInstance, out var value))
		{
			runtimeSystems.NotifyPopulationCorpseRemoved(value.CorpseIdentity);
		}
		runtimeSystems.DespawnCorpse(corpseInstance, SendCorpseDespawn, runtimeSystems.ClearNpcCorpseDespawn, delegate(int x)
		{
			corpseInventoryService.Remove(x);
		}, delegate(int x)
		{
			pendingCorpseCreditAwards.Remove(x);
		});
	}

	private void ScheduleCorpseDespawn(CorpseState corpse, TimeSpan delay, string reason)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		DateTime expiresAtUtc = (corpse.ExpiresAtUtc = DateTime.UtcNow + delay);
		runtimeSystems.ScheduleNpcCorpseDespawn(corpse.CorpseIdentity, expiresAtUtc);
		LogUtil.Debug((DebugInfoDetail)128, $"Corpse despawn scheduled corpse={corpse.CorpseIdentity} delaySeconds={delay.TotalSeconds} reason={reason} remainingLoot={((corpse.LootItems != null) ? corpse.LootItems.Count((CorpseLootItem x) => !x.Looted) : 0)}");
	}

	private void ExtendCorpseLifetime(CorpseState corpse, TimeSpan minimumRemaining, string reason)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		DateTime dateTime = DateTime.UtcNow + minimumRemaining;
		if (!(corpse.ExpiresAtUtc >= dateTime))
		{
			corpse.ExpiresAtUtc = dateTime;
			runtimeSystems.ScheduleNpcCorpseDespawn(corpse.CorpseIdentity, dateTime);
			LogUtil.Debug((DebugInfoDetail)128, $"Corpse lifetime extended corpse={corpse.CorpseIdentity} minimumRemainingSeconds={minimumRemaining.TotalSeconds} reason={reason} remainingLoot={((corpse.LootItems != null) ? corpse.LootItems.Count((CorpseLootItem x) => !x.Looted) : 0)}");
		}
	}

	internal Identity AllocateCorpseIdentity()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		nextCorpseInstance++;
		if (nextCorpseInstance > 15794175)
		{
			nextCorpseInstance = 15790081;
		}
		Identity result = default(Identity);
		((Identity)(ref result)).Type = (IdentityType)51050;
		((Identity)(ref result)).Instance = nextCorpseInstance;
		return result;
	}

	private int AllocateCorpseInventoryHandle()
	{
		int result = nextCorpseInventoryHandle++;
		if (nextCorpseInventoryHandle > 255)
		{
			nextCorpseInventoryHandle = 112;
		}
		return result;
	}

	private Identity AllocateCorpseLootItemIdentity()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		nextCorpseLootItemInstance++;
		if (nextCorpseLootItemInstance > 16777215)
		{
			nextCorpseLootItemInstance = 2097153;
		}
		Identity result = default(Identity);
		((Identity)(ref result)).Type = (IdentityType)150994945;
		((Identity)(ref result)).Instance = nextCorpseLootItemInstance;
		return result;
	}

	internal bool CanBuildKnownCorpseVisual(ICharacter target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition) && CombatCorpseVisuals.IsUsableVisualId(definition.CorpseCatMesh))
			{
				goto IL_00c8;
			}
		}
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition2) && definition2.Profile.Corpse.CapturedCatMesh.HasValue && CombatCorpseVisuals.IsUsableVisualId(definition2.Profile.Corpse.CapturedCatMesh.Value))
			{
				goto IL_00c8;
			}
		}
		if (IsCapturedCleaningRobot(target) || UsesCapturedThiefCorpseProfile(target) || CombatCorpseVisuals.IsUsableVisualId(((IStats)target).Stats[(StatIds)42].Value))
		{
			goto IL_00c8;
		}
		int result = (MonsterDataToCorpseCatMesh.ContainsKey(((IStats)target).Stats[(StatIds)359].Value) ? 1 : 0);
		goto IL_00c9;
		IL_00c9:
		return (byte)result != 0;
		IL_00c8:
		result = 1;
		goto IL_00c9;
	}

	private static int CorpseCatMeshFor(ICharacter target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
			{
				return definition.CorpseCatMesh;
			}
		}
		if (target != null)
		{
			identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition2) && definition2.Profile.Corpse.CapturedCatMesh.HasValue)
			{
				return definition2.Profile.Corpse.CapturedCatMesh.Value;
			}
		}
		if (IsCapturedCleaningRobot(target))
		{
			return 297018;
		}
		if (UsesCapturedThiefCorpseProfile(target))
		{
			return 5907;
		}
		return CombatCorpseVisuals.CorpseCatMeshFor(((IStats)target).Stats[(StatIds)42].Value, ((IStats)target).Stats[(StatIds)359].Value, MonsterDataToCorpseCatMesh);
	}

	private static bool UsesCapturedThiefCorpseProfile(ICharacter target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (target != null)
		{
			Identity identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
			{
				result = ((definition.Profile.Corpse.PacketProfile == OrdinaryEnemyCorpsePacketProfile.CapturedThief) ? 1 : 0);
				goto IL_0031;
			}
		}
		result = 0;
		goto IL_0031;
		IL_0031:
		return (byte)result != 0;
	}

	private static int DeathAnimationKeyFor(ICharacter target)
	{
		if (IsCapturedCleaningRobot(target))
		{
			return 500;
		}
		return CombatCorpseVisuals.DeathAnimationKeyFor(((IStats)target).Stats[(StatIds)417].Value, ((IStats)target).Stats[(StatIds)99].Value, 503);
	}

	private static int CorpseMonsterDataFor(ICharacter target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (target != null)
		{
			Identity identity = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
			{
				return definition.MonsterData;
			}
		}
		return CombatCorpseVisuals.CorpseMonsterDataFor(((IStats)target).Stats[(StatIds)359].Value, CorpseCatMeshFor(target));
	}

	internal void StopFightingDeadTarget(Identity deadTarget)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		foreach (ICharacter item in runtimeSystems.Characters())
		{
			if (!(((ITargetingEntity)item).FightingTarget == deadTarget))
			{
				continue;
			}
			if (((IDynel)item).Controller is NPCController)
			{
				ClearNpcFightingTarget(item);
				if (PetCombatRules.IsPlayerOwnedPet(item))
				{
					PetCommandService.ReturnPetToOwner(item);
				}
			}
			else
			{
				runtimeSystems.ClearPlayerFightingTarget(item, ClearCombatTracking);
			}
			Identity identity = ((IEntity)item).Identity;
			Identity val = deadTarget;
			PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "attacker-stop-fight", "StopFight", identity, "deadTarget=" + ((object)(Identity)(ref val)).ToString());
			SendCombatStopMessage(item);
		}
	}

	private void SendCombatStopMessage(ICharacter character)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		StopFightMessage messageBody = new StopFightMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown1 = 1
		};
		Announce((MessageBody)(object)messageBody);
	}

	private static CorpseLootItem FindCorpseLootItem(CorpseState corpse, int requestedLootSlot)
	{
		return CombatCorpseRules.FindLootItem(corpse.LootItems, requestedLootSlot, (CorpseLootItem x) => x.Slot, (CorpseLootItem x) => x.Looted);
	}

	private static InventoryEntry CreateCorpseInventoryEntry(CorpseLootItem lootItem)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		return new InventoryEntry
		{
			Slotnumber = lootItem.Slot,
			UnknownFlags = 161,
			Unknown1 = InventoryEntryCountFor(lootItem.Item),
			Identity = lootItem.LootIdentity,
			LowId = lootItem.Item.LowID,
			HighId = lootItem.Item.HighID,
			Quality = lootItem.Item.Quality,
			Unknown2 = 0
		};
	}

	private static short InventoryEntryCountFor(Item item)
	{
		return CombatCorpseRules.InventoryEntryCountFor(item.MultipleCount);
	}

	private void SendCorpseInventoryUpdate(ICharacter looter, CorpseState corpse)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		if (((IDynel)looter).Controller.Client != null)
		{
			InventoryEntry[] array = (InventoryEntry[])((corpse.LootItems == null) ? ((Array)new InventoryEntry[0]) : ((Array)corpse.LootItems.Where((CorpseLootItem x) => !x.Looted).Select(CreateCorpseInventoryEntry).ToArray()));
			IZoneClient client = ((IDynel)looter).Controller.Client;
			InventoryUpdateMessage val = new InventoryUpdateMessage
			{
				Identity = ((IEntity)looter).Identity,
				Unknown = 1,
				NumberOfSlots = 21,
				Unknown1 = 2,
				Entries = array,
				BagIdentity = corpse.CorpseIdentity,
				SlotnumberInMainInventory = corpse.InventoryHandle,
				Unknown2 = 1
			};
			Identity identity = ((IEntity)looter).Identity;
			client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
			LogUtil.Debug((DebugInfoDetail)128, $"Corpse InventoryUpdate sent looter={((IEntity)looter).Identity} corpse={corpse.CorpseIdentity} slots={21} unknown1=2 handle={corpse.InventoryHandle} unknown2=1 entries={array.Length}");
		}
	}

	private void SendCorpseCloseAction(ICharacter looter, CorpseState corpse)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (((IDynel)looter).Controller.Client != null)
		{
			((IDynel)looter).Controller.Client.SendCompressed((MessageBody)new ActionMessage
			{
				Identity = corpse.CorpseIdentity,
				Unknown = 1,
				ActionCode = 1,
				ActionIdentity = 102,
				Target = ((IEntity)looter).Identity
			});
			LogUtil.Debug((DebugInfoDetail)128, $"Corpse close Action sent looter={((IEntity)looter).Identity} corpse={corpse.CorpseIdentity} action=0x66");
		}
	}

	private void ScheduleCorpseCreditAward(ICharacter looter, CorpseState corpse)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		if (looter != null && corpse != null && !corpse.CreditsLooted && corpse.Credits > 0)
		{
			Dictionary<int, PendingCorpseCreditAward> dictionary = pendingCorpseCreditAwards;
			Identity corpseIdentity = corpse.CorpseIdentity;
			if (!dictionary.ContainsKey(((Identity)(ref corpseIdentity)).Instance))
			{
				DateTime dueAtUtc = DateTime.UtcNow + CorpseCreditAwardDelay;
				Dictionary<int, PendingCorpseCreditAward> dictionary2 = pendingCorpseCreditAwards;
				corpseIdentity = corpse.CorpseIdentity;
				int instance = ((Identity)(ref corpseIdentity)).Instance;
				PendingCorpseCreditAward pendingCorpseCreditAward = new PendingCorpseCreditAward();
				corpseIdentity = corpse.CorpseIdentity;
				pendingCorpseCreditAward.CorpseInstance = ((Identity)(ref corpseIdentity)).Instance;
				pendingCorpseCreditAward.LooterIdentity = ((IEntity)looter).Identity;
				pendingCorpseCreditAward.DueAtUtc = dueAtUtc;
				dictionary2[instance] = pendingCorpseCreditAward;
				LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Corpse credits scheduled corpse={0} looter={1} credits={2} delayMs={3}", corpse.CorpseIdentity, ((IEntity)looter).Identity, corpse.Credits, (int)CorpseCreditAwardDelay.TotalMilliseconds));
			}
		}
	}

	private void ProcessPendingCorpseCreditAwards()
	{
		runtimeSystems.ProcessPendingCorpseCreditAwards(pendingCorpseCreditAwards, corpses, (PendingCorpseCreditAward award) => award.DueAtUtc, (PendingCorpseCreditAward award) => award.CorpseInstance, (PendingCorpseCreditAward award) => award.LooterIdentity, (CorpseState corpse) => corpse.CorpseIdentity, (Identity identity) => this.FindByIdentity<ICharacter>(identity), (ICharacter looter) => ((IDynel)looter).InPlayfield(((PooledObject)this).Identity), AwardCorpseCredits);
	}

	private void AwardCorpseCredits(ICharacter looter, CorpseState corpse)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		if (looter == null || corpse == null || corpse.CreditsLooted || corpse.Credits <= 0)
		{
			return;
		}
		uint baseValue = ((IStats)looter).Stats[(StatIds)61].BaseValue;
		int num = CashStatRules.Clamp(baseValue);
		if (corpseInventoryService.RemoveCredits(corpse.CorpseIdentity, DateTime.UtcNow))
		{
			if (corpse.IsEmpty)
			{
				ScheduleCorpseDespawn(corpse, corpse.EmptyCleanupDelay, "credits-empty");
			}
			int num2 = CashStatRules.Clamp((long)num + (long)corpse.Credits);
			((IStats)looter).Stats[(StatIds)61].Set((uint)num2, false);
			runtimeSystems.SendChangedStatsIfClient(looter, CharacterHasClient, SendStatChangedMessage);
			LogUtil.Debug((DebugInfoDetail)128, $"Corpse credits awarded corpse={corpse.CorpseIdentity} looter={((IEntity)looter).Identity} credits={corpse.Credits} cashBeforeBase={baseValue} cashAfter={num2} inventoryHandle={corpse.InventoryHandle}");
			((IDatabaseObject)((IStats)looter).Stats).Write();
		}
	}

	private static bool CharacterHasClient(ICharacter character)
	{
		return ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null;
	}

	private static void SendStatChangedMessage(ICharacter character)
	{
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(character);
	}

	private void SendRewardFeedback(ICharacter character, string text)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		IZoneClient client = ((IDynel)character).Controller.Client;
		FormatFeedbackMessage val = new FormatFeedbackMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown1 = 0,
			FormattedMessage = text,
			Unknown2 = 0
		};
		Identity identity = ((IEntity)character).Identity;
		client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Reward feedback sent char={0} text={1}", ((IEntity)character).Identity, text));
	}

	private void SendUseActionFinished(ICharacter character)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		if (((IDynel)character).Controller.Client != null)
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Action = (CharacterActionType)110,
				Unknown1 = 0,
				Target = Identity.None,
				Parameter1 = 0,
				Parameter2 = 0,
				Unknown2 = 0
			});
		}
	}

	private void SendTargetClearMessage(ICharacter character)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		LookAtMessage val = new LookAtMessage
		{
			Identity = ((IEntity)character).Identity,
			Target = Identity.None
		};
		if (((IDynel)character).Controller.Client != null)
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)(object)val);
		}
		Announce((MessageBody)(object)val);
	}

	private void SendCombatIdleState(ICharacter character)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected O, but got Unknown
		((IStats)character).Stats[(StatIds)7].Value = 0;
		((IStats)character).Stats[(StatIds)423].Value = 0;
		((IStats)character).Stats[(StatIds)588].Value = 0;
		if (((IDynel)character).Controller.Client != null)
		{
			IZoneClient client = ((IDynel)character).Controller.Client;
			StatMessage val = new StatMessage();
			((N3Message)val).Identity = ((IEntity)character).Identity;
			val.Stats = new GameTuple<CharacterStat, uint>[3]
			{
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)7,
					Value2 = 0u
				},
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)423,
					Value2 = 0u
				},
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)588,
					Value2 = 0u
				}
			};
			client.SendCompressed((MessageBody)(object)val);
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)(object)SimpleCharFullUpdate.ConstructMessage((Character)character));
		}
	}

	protected override void Dispose(bool disposing)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (disposing && !disposed)
		{
			runtimeSystems.ClearNpcRuntimeState();
			CorpseInventoryService obj = corpseInventoryService;
			Identity identity = ((PooledObject)this).Identity;
			obj.ClearPlayfield(((Identity)(ref identity)).Instance);
			DisconnectAllClients();
			if (memBusDisposeContainer != null)
			{
				memBusDisposeContainer.Dispose();
			}
			if (heartBeat != null)
			{
				heartBeat.Dispose();
			}
		}
		disposed = true;
		((PooledObject)this).Dispose(disposing);
	}
}
