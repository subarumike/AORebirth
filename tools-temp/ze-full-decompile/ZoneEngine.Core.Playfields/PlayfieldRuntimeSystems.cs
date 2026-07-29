using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Navigation;
using ZoneEngine.Core.Playfields.Content;
using ZoneEngine.Core.Subway.Quests;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldRuntimeSystems
{
	private readonly Playfield playfield;

	private readonly PlayfieldAnnouncementRuntimeService announcements;

	private readonly PlayfieldCharacterHeartbeatRuntimeService characterHeartbeat;

	private readonly PlayfieldContentCoordinator content;

	private readonly PlayfieldContentDataProvider contentData;

	private readonly PlayfieldCorpseAccessRuntimeService corpseAccess;

	private readonly PlayfieldAOtomationDeliveryRuntimeService aotomationDelivery;

	private readonly PlayfieldDbMobSpawnRuntimeService dbMobSpawns;

	private readonly PlayfieldDynelRegistry dynelRegistry;

	private readonly PlayfieldEnvironmentFunctionRuntimeService environmentFunctions;

	private readonly PlayfieldObjectLifecycleRuntimeService objectLifecycle;

	private readonly PlayfieldObjectMaterializationRuntimeService objectMaterialization;

	private readonly PlayfieldPacketSequencingRuntimeService packetSequences;

	private readonly PlayfieldPublishFanoutRuntimeService publishFanout;

	private readonly InventoryContainerRuntimeService inventoryContainer;

	private readonly NPCRuntimeService npcRuntime;

	private readonly WindcallerKarrecNpcRuntimeService windcallerKarrecNpcs;

	private readonly PlayfieldNpcCombatMovementRuntimeService npcCombatMovement;

	private readonly NpcChaseNavigationRuntimeService npcChaseNavigation;

	private readonly PlayfieldRewardRuntimeService rewards;

	private readonly PlayfieldLifecycleRuntimeService lifecycle;

	private readonly PlayfieldPlayerDeathRespawnRuntimeService playerDeathRespawn;

	private readonly PlayfieldInteractionRuntimeService interaction;

	private readonly PlayerCombatRuntimeService playerCombat;

	private readonly PlayfieldStatelTransitionRuntimeService statelTransitions;

	private readonly PlayfieldStatUpdateRuntimeService statUpdates;

	private readonly PlayfieldStaticDynelRuntimeService staticDynelRuntime;

	private readonly PlayfieldTimedLifecycleRuntimeService timedLifecycle;

	private readonly PlayfieldTransferRuntimeService transfers;

	private readonly PlayfieldVendorRuntimeService vendors;

	private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout;

	private readonly PlayfieldVisibilityInterestRuntimeService visibilityInterest;

	private readonly PlayfieldVisibilityPacketRuntimeService visibilityPackets;

	private readonly PlayfieldWallCollisionRuntimeService wallCollision;

	private readonly PacketSequencingCoordinator packetSequencing;

	private readonly PrivateCityReadyInitCoordinator privateCityReadyInit;

	internal float VisibilityEnterRadius => visibilityInterest.Policy.EnterRadius;

	internal float VisibilityLeaveRadius => visibilityInterest.Policy.LeaveRadius;

	internal PlayfieldRuntimeSystems(Playfield playfield, Identity playfieldIdentity, Func<Identity, bool> isPrivateCityPlayfieldCandidate, Func<int, bool> isCapturedMontroyalPrivateCityInstance, Func<ICharacter, int> resolveCharacterOrganizationInstance, Func<int, string> resolveOrganizationName, Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue)
	{
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null)
		{
			throw new ArgumentNullException("playfield");
		}
		this.playfield = playfield;
		announcements = new PlayfieldAnnouncementRuntimeService();
		content = new PlayfieldContentCoordinator(new AreteContentModule(), new MontroyalContentModule(), new SubwayContentModule(), new JobePlatformContentModule(), new NascenceCoreContentModule(), new NascenceLifeContentModule(), new ThrakOmniGardenContentModule(), new RomeBlueCityContentModule(), new AndromedaIccHqContentModule(), new HoloDeckContentModule(), new MissionInstanceContentModule(), new PrivateCityContentModule());
		contentData = new PlayfieldContentDataProvider(isPrivateCityPlayfieldCandidate);
		corpseAccess = new PlayfieldCorpseAccessRuntimeService();
		aotomationDelivery = new PlayfieldAOtomationDeliveryRuntimeService();
		dbMobSpawns = new PlayfieldDbMobSpawnRuntimeService();
		characterHeartbeat = new PlayfieldCharacterHeartbeatRuntimeService();
		dynelRegistry = new PlayfieldDynelRegistry(playfieldIdentity);
		environmentFunctions = new PlayfieldEnvironmentFunctionRuntimeService();
		objectLifecycle = new PlayfieldObjectLifecycleRuntimeService();
		objectMaterialization = new PlayfieldObjectMaterializationRuntimeService();
		publishFanout = new PlayfieldPublishFanoutRuntimeService();
		inventoryContainer = InventoryContainerRuntimeService.Default;
		rewards = new PlayfieldRewardRuntimeService();
		npcChaseNavigation = new NpcChaseNavigationRuntimeService(PlayfieldChaseNavigationProviderFactory.Create(((Identity)(ref playfieldIdentity)).Instance));
		npcRuntime = new NPCRuntimeService(playfield, dynelRegistry, rewards, npcChaseNavigation);
		windcallerKarrecNpcs = new WindcallerKarrecNpcRuntimeService();
		npcCombatMovement = new PlayfieldNpcCombatMovementRuntimeService(npcChaseNavigation);
		lifecycle = new PlayfieldLifecycleRuntimeService();
		playerDeathRespawn = new PlayfieldPlayerDeathRespawnRuntimeService();
		interaction = new PlayfieldInteractionRuntimeService();
		playerCombat = new PlayerCombatRuntimeService();
		statelTransitions = new PlayfieldStatelTransitionRuntimeService();
		statUpdates = new PlayfieldStatUpdateRuntimeService();
		staticDynelRuntime = new PlayfieldStaticDynelRuntimeService();
		timedLifecycle = new PlayfieldTimedLifecycleRuntimeService();
		packetSequencing = new PacketSequencingCoordinator();
		packetSequences = new PlayfieldPacketSequencingRuntimeService(packetSequencing);
		transfers = new PlayfieldTransferRuntimeService(lifecycle, packetSequences);
		vendors = new PlayfieldVendorRuntimeService();
		visibilityFanout = new PlayfieldVisibilityFanoutRuntimeService();
		PlayfieldVisibilityInterestPolicy policy = PlayfieldVisibilityInterestPolicy.FromEnvironment();
		visibilityInterest = new PlayfieldVisibilityInterestRuntimeService(policy, new PlayfieldSpatialCharacterIndex(policy));
		visibilityPackets = new PlayfieldVisibilityPacketRuntimeService(visibilityFanout, packetSequences, visibilityInterest);
		wallCollision = new PlayfieldWallCollisionRuntimeService();
		privateCityReadyInit = new PrivateCityReadyInitCoordinator(playfieldIdentity, isPrivateCityPlayfieldCandidate, isCapturedMontroyalPrivateCityInstance, resolveCharacterOrganizationInstance, resolveOrganizationName, resolveCharacterStatWireValue);
	}

	internal void RegisterContent(Identity playfieldIdentity)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		content.RegisterContent(playfield, playfieldIdentity);
	}

	internal void MaterializeStartupObjects(Identity playfieldIdentity, IEnumerable<StatelData> statels)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		objectMaterialization.MaterializeStartupObjects(playfieldIdentity, statels, dbMobSpawns.LoadMobSpawnDefinitions, ShouldSuppressDbMobSpawn, dbMobSpawns.LoadMobSpawnStats, (DBMobSpawn mob, DBMobSpawnStat[] stats) => dbMobSpawns.InstantiateDbMobSpawn(mob, stats, playfield), ActivateNpc, dbMobSpawns.AttachMobSpawnKnuBot, RegisterContent, TryResolveVendorStatels, delegate(StatelData[] vendorStatels)
		{
			vendors.SpawnVendors(playfield, vendorStatels);
		}, ResolveStaticDynels, (PlayfieldStaticDynelDefinition staticDynel) => staticDynelRuntime.CreateStaticDynel(playfieldIdentity, staticDynel), RegisterDynel, RefreshDynelRegistry);
	}

	internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.SpawnCapturedNpcContent(playfieldIdentity);
		windcallerKarrecNpcs.Spawn(playfield, playfieldIdentity, ActivateNpc, DeactivateNpc);
		vendors.SpawnCapturedSubwayVendors(playfield, playfieldIdentity, dynelRegistry, RegisterDynel);
		vendors.AttachCapturedThrakGardenVendors(playfield, playfieldIdentity, dynelRegistry);
		vendors.SpawnCapturedHoloDeckVendors(playfield, playfieldIdentity, dynelRegistry);
		vendors.SpawnCapturedAreteAlexAreaVendors(playfield, playfieldIdentity, dynelRegistry);
	}

	internal void ClearNpcRuntimeState()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		windcallerKarrecNpcs.Clear(((PooledObject)playfield).Identity, DeactivateNpc);
		vendors.ClearCapturedThrakGardenVendors(((PooledObject)playfield).Identity, dynelRegistry);
		vendors.ClearCapturedHoloDeckVendors(((PooledObject)playfield).Identity, dynelRegistry);
		vendors.ClearCapturedAreteAlexAreaVendors(((PooledObject)playfield).Identity, dynelRegistry);
		vendors.ClearCapturedSubwayVendors(((PooledObject)playfield).Identity, dynelRegistry);
		npcRuntime.ClearRuntimeState();
		npcChaseNavigation.Dispose();
		visibilityInterest.Clear();
	}

	internal List<StatelData> ResolveStatels(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return contentData.ResolveStatels(playfieldIdentity);
	}

	internal bool TryResolveVendorStatels(Identity playfieldIdentity, IEnumerable<StatelData> statels, out StatelData[] vendorStatels)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return contentData.TryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels);
	}

	internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)
	{
		return contentData.ResolveCollisionStatels(statels);
	}

	internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return contentData.ResolveStaticDynels(playfieldIdentity);
	}

	internal bool ShouldSuppressDbMobSpawn(DBMobSpawn mob)
	{
		if (mob == null)
		{
			return false;
		}
		return content.ShouldSuppressDbMobSpawn(mob.Playfield, mob.Id);
	}

	internal void RefreshDynelRegistry()
	{
		dynelRegistry.RefreshFromPool();
	}

	internal void RegisterDynel(IEntity entity)
	{
		dynelRegistry.Register(entity);
		ICharacter val = (ICharacter)(object)((entity is ICharacter) ? entity : null);
		if (val != null)
		{
			visibilityInterest.Register(val);
		}
	}

	internal void UnregisterDynel(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		visibilityInterest.Unregister(identity);
		dynelRegistry.Unregister(identity);
	}

	internal void RemoveInstancedEntity(IInstancedEntity entity)
	{
		objectLifecycle.RemoveInstancedEntity(entity);
	}

	internal int DespawnCorpses<TCorpseState>(IDictionary<int, TCorpseState> pendingCorpseSpawns, IDictionary<int, TCorpseState> corpses, Func<string, Identity, bool> shouldDespawn, Func<TCorpseState, string> corpseName, Func<TCorpseState, Identity> deadNpcIdentity, Action<int> despawnCorpse)
	{
		return objectLifecycle.DespawnCorpses(pendingCorpseSpawns, corpses, shouldDespawn, corpseName, deadNpcIdentity, despawnCorpse);
	}

	internal void DespawnCorpse(int corpseInstance, Action<Identity> sendDespawn, Action<int> clearNpcCorpseDespawn, Action<int> removeCorpseState, Action<int> removePendingCorpseCreditAward)
	{
		objectLifecycle.DespawnCorpse(corpseInstance, sendDespawn, clearNpcCorpseDespawn, removeCorpseState, removePendingCorpseCreditAward);
	}

	internal void NotifyPopulationCorpseRemoved(Identity corpseIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.NotifyCorpseRemoved(corpseIdentity);
	}

	internal void ProcessPendingCorpseSpawns<TCorpseState>(IDictionary<int, TCorpseState> pendingCorpseSpawns, Func<TCorpseState, DateTime> spawnsAtUtc, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, Identity> deadNpcIdentity, Func<Identity, ICharacter> findDeadNpc, Action<ICharacter, Identity> registerCorpse, Action<Identity, Identity> traceCorpseFullUpdate, Action<ICharacter, Identity> sendCorpseFullUpdate)
	{
		objectLifecycle.ProcessPendingCorpseSpawns(pendingCorpseSpawns, spawnsAtUtc, corpseIdentity, deadNpcIdentity, findDeadNpc, registerCorpse, traceCorpseFullUpdate, sendCorpseFullUpdate);
	}

	internal void ActivateNpc(ICharacter character)
	{
		npcRuntime.ActivateNpc(character);
		visibilityInterest.Register(character);
	}

	private void DeactivateNpc(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.RemoveNpcHome(identity);
		UnregisterDynel(identity);
	}

	internal void RegisterNpcHome(ICharacter character)
	{
		npcRuntime.RegisterNpcHome(character);
	}

	internal void DespawnNpcImmediately(ICharacter target, Action<Identity> stopFightingDeadTarget, Action<Identity> cancelPendingCorpseSpawn)
	{
		npcRuntime.DespawnNpcImmediately(target, stopFightingDeadTarget, cancelPendingCorpseSpawn);
	}

	internal void RegisterStatels(IEnumerable<StatelData> statels)
	{
		dynelRegistry.RegisterStatels(statels);
	}

	internal IInstancedEntity FindByIdentity(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return dynelRegistry.FindByIdentity(identity);
	}

	internal T FindByIdentity<T>(Identity identity) where T : class, IEntity
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return dynelRegistry.FindByIdentity<T>(identity);
	}

	internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)
	{
		return dynelRegistry.FindDynelsInRange(dynel, range);
	}

	internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)
	{
		return dynelRegistry.FindCharactersInRange(dynel, range);
	}

	internal ReadOnlyCollection<ICharacter> Characters()
	{
		return dynelRegistry.Characters();
	}

	internal ReadOnlyCollection<Character> CharacterEntities()
	{
		return dynelRegistry.CharacterEntities();
	}

	internal void AnnounceToCharacterClients(Action<Character> publishToCharacterClient)
	{
		visibilityFanout.AnnounceToCharacterClients(CharacterEntities(), publishToCharacterClient);
	}

	internal void AnnounceToOtherCharacterClients(Identity excludedIdentity, Action<Character> publishToCharacterClient)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		visibilityFanout.AnnounceToOtherCharacterClients(CharacterEntities(), excludedIdentity, publishToCharacterClient);
	}

	internal void AnnounceMessageToCharacterClients(MessageBody messageBody, Action<IZoneClient, MessageBody> sendMessageBodyToClient)
	{
		announcements.AnnounceToCharacterClients(CharacterEntities(), messageBody, sendMessageBodyToClient);
	}

	internal void AnnounceMessageToOtherCharacterClients(MessageBody messageBody, Identity excludedIdentity, Action<IZoneClient, MessageBody> sendMessageBodyToClient)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		announcements.AnnounceToOtherCharacterClients(CharacterEntities(), excludedIdentity, messageBody, sendMessageBodyToClient);
	}

	internal void SendExistingCharacterVisibilityToClient(ICharacter recipient, Action<MessageBody> sendVisibilityMessage)
	{
		visibilityPackets.SendExistingCharacterVisibilityToClient(recipient, Characters(), sendVisibilityMessage);
	}

	internal void AnnounceJoiningCharacterVisibility(ICharacter character, Action<ICharacter, MessageBody> sendVisibilityMessage, Action<ICharacter, Identity> sendLeaveVisibility)
	{
		visibilityPackets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
	}

	internal void AnnounceSpawnedCharacterVisibility(ICharacter character, Identity alreadyVisibleRecipient, Action<ICharacter, MessageBody> sendVisibilityMessage, Action<ICharacter, Identity> sendLeaveVisibility)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		visibilityInterest.Register(character);
		if (alreadyVisibleRecipient != Identity.None)
		{
			ICharacter val = dynelRegistry.FindByIdentity<ICharacter>(alreadyVisibleRecipient);
			if (val != null)
			{
				visibilityInterest.MarkVisibleEntry(val, character);
			}
		}
		visibilityPackets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
	}

	internal void RefreshCharacterVisibility(ICharacter character, Action<ICharacter, MessageBody> sendVisibilityMessage, Action<ICharacter, Identity> sendLeaveVisibility)
	{
		visibilityPackets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
	}

	internal bool TryAnnounceCharacterScopedMessage(Identity sourceIdentity, Identity excludedRecipient, MessageBody messageBody, Action<IZoneClient, MessageBody> sendMessageBodyToClient)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = dynelRegistry.FindByIdentity<ICharacter>(sourceIdentity);
		if (val == null)
		{
			return false;
		}
		foreach (ICharacter item in visibilityInterest.VisibleRecipientsForSource(sourceIdentity))
		{
			if (((IEntity)item).Identity != excludedRecipient && ((IDynel)item).Controller != null && ((IDynel)item).Controller.Client != null)
			{
				sendMessageBodyToClient(((IDynel)item).Controller.Client, messageBody);
			}
		}
		if (((IEntity)val).Identity != excludedRecipient && ((IDynel)val).Controller != null && ((IDynel)val).Controller.Client != null)
		{
			sendMessageBodyToClient(((IDynel)val).Controller.Client, messageBody);
		}
		return true;
	}

	internal bool TryDespawnVisibleCharacter(Identity sourceIdentity, Action<ICharacter, MessageBody> sendVisibilityMessage)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = dynelRegistry.FindByIdentity<ICharacter>(sourceIdentity);
		if (val == null)
		{
			return false;
		}
		DespawnMessage arg = BaseMessageHandler<DespawnMessage, DespawnMessageHandler>.Default.Create(sourceIdentity);
		foreach (ICharacter item in visibilityInterest.VisibleRecipientsForSource(sourceIdentity))
		{
			sendVisibilityMessage(item, (MessageBody)(object)arg);
		}
		visibilityInterest.Unregister(sourceIdentity);
		return true;
	}

	internal void ForgetVisibilityRecipient(Identity recipientIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		visibilityInterest.ForgetRecipient(recipientIdentity);
	}

	internal ReadOnlyCollection<ICharacter> VisibleRecipientsForSource(Identity sourceIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return visibilityInterest.VisibleRecipientsForSource(sourceIdentity);
	}

	internal void PublishMessageBodyToClient(IZoneClient client, MessageBody body, Action<object> publish)
	{
		publishFanout.PublishMessageBodyToClient(client, body, publish);
	}

	internal void PublishMessageToClient(IZoneClient client, Message message, Action<object> publish)
	{
		publishFanout.PublishMessageToClient(client, message, publish);
	}

	internal void DispatchMessageToPlayfield(MessageBody body, Action<MessageBody> announce)
	{
		publishFanout.DispatchMessageToPlayfield(body, announce);
	}

	internal void DispatchMessageToPlayfieldOthers(MessageBody body, Identity excludedIdentity, Action<MessageBody, Identity> announceOthers)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		publishFanout.DispatchMessageToPlayfieldOthers(body, excludedIdentity, announceOthers);
	}

	internal void DeliverAOtomationMessageToClient(IMSendAOtomationMessageToClient clientMessage)
	{
		aotomationDelivery.SendMessageToClient(clientMessage);
	}

	internal void DeliverAOtomationMessageBodyToClient(IMSendAOtomationMessageBodyToClient message)
	{
		aotomationDelivery.SendMessageBodyToClient(message);
	}

	internal void DeliverAOtomationMessageBodiesToClient(IMSendAOtomationMessageBodiesToClient message)
	{
		aotomationDelivery.SendMessageBodiesToClient(message);
	}

	internal void DeliverAOtomationMessageToPlayfield(IMSendAOtomationMessageToPlayfield clientMessage, Action<MessageBody> announce)
	{
		aotomationDelivery.SendMessageToPlayfield(clientMessage, delegate(MessageBody body)
		{
			publishFanout.DispatchMessageToPlayfield(body, announce);
		});
	}

	internal void DeliverAOtomationMessageToPlayfieldOthers(IMSendAOtomationMessageToPlayfieldOthers clientMessage, Action<MessageBody, Identity> announceOthers)
	{
		aotomationDelivery.SendMessageToPlayfieldOthers(clientMessage, delegate(MessageBody body, Identity excludedIdentity)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			publishFanout.DispatchMessageToPlayfieldOthers(body, excludedIdentity, announceOthers);
		});
	}

	internal bool IsInNpcCombatRange(ICharacter attacker, ICharacter target, double range)
	{
		return npcCombatMovement.IsInCombatRange(attacker, target, range);
	}

	internal bool HasActiveNpcChaseNavigation(ICharacter attacker)
	{
		return npcCombatMovement.HasActiveNavigation(attacker);
	}

	internal bool IsNpcAttackPathTraversable(ICharacter attacker, ICharacter target)
	{
		return npcCombatMovement.IsAttackPathTraversable(attacker, target);
	}

	internal void HoldNpcAtCombatPosition(ICharacter attacker, ICharacter target)
	{
		npcCombatMovement.HoldNpcAtCombatPosition(attacker, target);
	}

	internal bool TryResolveCapturedNpcMovementDestination(ICharacter attacker, ICharacter target, double range, DateTime utcNow, out Vector3 destination)
	{
		return npcCombatMovement.TryResolveCapturedMovementDestination(attacker, target, range, utcNow, out destination);
	}

	internal void UpdateNpcMeleeFollowHold(ICharacter attacker, ICharacter target, double range, Action<ICharacter, Vector3> moveNpcToPosition, Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
	{
		npcCombatMovement.UpdateNpcMeleeFollowHold(attacker, target, range, moveNpcToPosition, logNpcBrain);
	}

	internal void TryMoveNpcIntoCombatRange(ICharacter attacker, ICharacter target, double range, Action<ICharacter, Vector3> moveNpcToPosition, Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
	{
		npcCombatMovement.TryMoveNpcIntoCombatRange(attacker, target, range, moveNpcToPosition, logNpcBrain);
	}

	internal void SendChangedStats(ICharacter character, Action<ICharacter> sendChangedStats)
	{
		statUpdates.SendChangedStats(character, sendChangedStats);
	}

	internal void SendChangedStatsIfChanged(ICharacter character, bool changed, Action<ICharacter> sendChangedStats)
	{
		statUpdates.SendChangedStatsIfChanged(character, changed, sendChangedStats);
	}

	internal void SendChangedStatsIfClient(ICharacter character, Func<ICharacter, bool> hasClient, Action<ICharacter> sendChangedStats)
	{
		statUpdates.SendChangedStatsIfClient(character, hasClient, sendChangedStats);
	}

	internal void RunPlayerDeathStatUpdateSequence(ICharacter target, Action<ICharacter> sendChangedStats, Action<ICharacter> cleanupDeathCombat, Action<ICharacter> sendDeathAnimation)
	{
		statUpdates.RunPlayerDeathStatUpdateSequence(target, sendChangedStats, cleanupDeathCombat, sendDeathAnimation);
	}

	internal ReadOnlyCollection<StaticDynel> StaticDynels()
	{
		return dynelRegistry.StaticDynels();
	}

	internal void RunPlayfieldTransferBeginSequence(Action enterZoningPhase, Action sendTeleportPacket)
	{
		packetSequences.RunPlayfieldTransferBeginSequence(enterZoningPhase, sendTeleportPacket);
	}

	internal void SendPrivateCityPlayfieldReadyBlock(ZoneClient client, ICharacter character)
	{
		privateCityReadyInit.SendPlayfieldReadyBlock(client, character);
	}

	internal void SendPrivateCityPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
	{
		privateCityReadyInit.SendPreFullCharacterReadyBlock(client, character);
	}

	internal void ProcessHeartbeatTimedLifecycle(Identity playfieldIdentity, Action processPendingCorpseSpawns, Action processCorpseDespawns, Action processPendingCorpseCreditAwards, Action<ICharacter> processRegeneration, Action<ICharacter> processCombatTick, Action<ICharacter> processFollow, Action<ICharacter> processPlayerCollision)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance == 6553)
		{
			npcRuntime.EnsureAreteCapturePopulation();
		}
		timedLifecycle.ProcessHeartbeatLifecycle(playfieldIdentity, Characters, HasPendingDeadNpcDespawn, processPendingCorpseSpawns, processCorpseDespawns, processPendingCorpseCreditAwards, ProcessDeadNpcDespawn, processRegeneration, processCombatTick, ProcessNpcPatrolTick, processFollow, processPlayerCollision);
	}

	internal void ProcessCharacterRegeneration(ICharacter dynel, Action<ICharacter> sendChangedStats)
	{
		characterHeartbeat.ProcessRegeneration(dynel, sendChangedStats);
	}

	internal void NotifyNpcCombatDamage(ICharacter npc)
	{
		characterHeartbeat.NotifyNpcDamaged(npc);
	}

	internal void SuspendNpcRegen(ICharacter npc)
	{
		characterHeartbeat.SuspendNpcRegen(npc);
	}

	internal void ProcessCharacterFollow(ICharacter dynel)
	{
		characterHeartbeat.ProcessFollow(dynel);
	}

	internal void ProcessPlayerCollisionChecks(ICharacter dynel, Action<ICharacter> checkWallCollision, Action<ICharacter> checkStatelCollision)
	{
		characterHeartbeat.ProcessPlayerCollisionChecks(dynel, checkWallCollision, checkStatelCollision);
	}

	internal void ProcessPlayerRespawn(ICharacter character, Dynel dynel, Identity corpseIdentity, Coordinate destination, Identity destinationPlayfield, Action<ICharacter, Identity> logCorpseVisualSkipped, Action<ICharacter> sendDeathSocialStatus, Action<ICharacter> markPlayerRespawned, Action<ICharacter> sendDeathRespawnStateStats, Action<ICharacter> stopMovement, Action<ICharacter> sendChangedStats, Action<ICharacter, Identity, Identity, Coordinate> logRespawnRequested, Action<ICharacter> enableTimers, Func<Dynel, Coordinate, IQuaternion, Identity, bool> tryCompleteCurrentPlayfieldRespawn, Action<Dynel, Coordinate, IQuaternion, Identity> transferToRespawnPlayfield, Action<Identity> clearCombatTracking, Action<Identity> stopFightingDeadTarget, Action<ICharacter> sendCombatStop)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		playerDeathRespawn.ProcessPlayerRespawn(character, dynel, corpseIdentity, destination, destinationPlayfield, logCorpseVisualSkipped, sendDeathSocialStatus, markPlayerRespawned, sendDeathRespawnStateStats, stopMovement, delegate(ICharacter x)
		{
			CleanupPlayerDeathCombat(x, clearCombatTracking, stopFightingDeadTarget, sendCombatStop);
		}, sendChangedStats, logRespawnRequested, enableTimers, tryCompleteCurrentPlayfieldRespawn, transferToRespawnPlayfield);
	}

	internal void PreparePlayfieldTransfer(Dynel dynel, Action<int> clearTransferContactState, Action<Dynel> disableTimers)
	{
		lifecycle.PreparePlayfieldTransfer(dynel, clearTransferContactState, disableTimers);
	}

	internal void TransferToPlayfield(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield, Action<int> clearTransferContactState, Action<Dynel> disableTimers, Func<Dynel, Action> captureEnterZoningPhase, Action sendTeleportPacket, Action<Dynel> announceDespawn, Action<Dynel, Coordinate, IQuaternion> applyTransferState, Func<Dynel, ZoneClient> captureClient, Func<Identity, IPlayfield> resolveDestinationPlayfield, Action<Dynel, IPlayfield> finalizeTransferDispose, Action<ZoneClient> sendRedirect)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		transfers.TransferToPlayfield(dynel, destination, heading, playfield, clearTransferContactState, disableTimers, captureEnterZoningPhase, sendTeleportPacket, announceDespawn, applyTransferState, captureClient, resolveDestinationPlayfield, finalizeTransferDispose, sendRedirect);
	}

	internal void CompletePlayfieldTransfer(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield, Action<Dynel> announceDespawn, Action<Dynel, Coordinate, IQuaternion> applyTransferState, Func<Dynel, ZoneClient> captureClient, Func<Identity, IPlayfield> resolveDestinationPlayfield, Action<Dynel, IPlayfield> finalizeTransferDispose, Action<ZoneClient> sendRedirect)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		transfers.CompletePlayfieldTransfer(dynel, destination, heading, playfield, announceDespawn, applyTransferState, captureClient, resolveDestinationPlayfield, finalizeTransferDispose, sendRedirect);
	}

	internal void ClearStatelTransitionContactState(int dynelId)
	{
		statelTransitions.ClearContactState(dynelId);
	}

	internal void PrimeStatelCollisionContacts(ICharacter dynel, IEnumerable<StatelData> collisionStatels)
	{
		statelTransitions.PrimeStatelCollisionContacts(dynel, collisionStatels);
	}

	internal void CheckStatelCollision(ICharacter dynel, Identity playfieldIdentity, IEnumerable<StatelData> collisionStatels, Func<ICharacter, int> resolvePrivateCityDestinationPlayfield, Func<ICharacter, int> resolveCharacterOrganizationInstance, Action<ICharacter> stopMovement, Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		statelTransitions.CheckStatelCollision(dynel, playfieldIdentity, collisionStatels, resolvePrivateCityDestinationPlayfield, resolveCharacterOrganizationInstance, stopMovement, sendCapturedPrivateCityEntrySocialStatus, teleportToPlayfield);
	}

	internal void CheckWallCollision(ICharacter dynel, Func<ICharacter, bool> isPostZoneCollisionGraceActive, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		wallCollision.CheckWallCollision(dynel, isPostZoneCollisionGraceActive, teleportToPlayfield);
	}

	internal bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return interaction.TryHandleGenericCmdUse(client, message, target);
	}

	internal void ExecuteFunction(IMExecuteFunction imExecuteFunction, Func<Identity, INamedEntity> findNamedEntity, Action<Character, string> sendNoValidTargetMessage)
	{
		environmentFunctions.ExecuteFunction(imExecuteFunction, findNamedEntity, sendNoValidTargetMessage);
	}

	internal void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
	{
		inventoryContainer.EnsureWeaponVisualMeshes(character, announceAppearanceUpdate);
	}

	internal bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
	{
		return inventoryContainer.CharacterHasUniqueItemAlready(character, item);
	}

	internal CorpseLootInventoryTransferResult TryAddCorpseLootItem(ICharacter looter, IItem item, int targetPlacement)
	{
		return inventoryContainer.TryAddCorpseLootItem(looter, item, targetPlacement);
	}

	internal bool TryUseCorpse<TCorpseState>(ICharacter looter, Identity corpseIdentity, IDictionary<int, TCorpseState> corpses, TimeSpan itemLootLifetime, TimeSpan emptyCleanupDelay, Func<TCorpseState, Identity> deadNpcIdentity, Func<TCorpseState, DateTime> expiresAtUtc, Func<TCorpseState, bool> isEmpty, Func<TCorpseState, bool> opened, Action<TCorpseState, bool> setOpened, Func<TCorpseState, object> lootClass, Action<int> despawnCorpse, Action<TCorpseState, TimeSpan, string> extendCorpseLifetime, Action<TCorpseState> refreshCorpseInventoryHandle, Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate, Action<ICharacter, TCorpseState> sendCorpseCloseAction, Action<ICharacter> sendUseActionFinished, Action<ICharacter, TCorpseState> scheduleCorpseCreditAward, Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn) where TCorpseState : class
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return corpseAccess.TryUseCorpse(looter, corpseIdentity, corpses, itemLootLifetime, emptyCleanupDelay, deadNpcIdentity, expiresAtUtc, isEmpty, opened, setOpened, lootClass, despawnCorpse, extendCorpseLifetime, refreshCorpseInventoryHandle, sendCorpseInventoryUpdate, sendCorpseCloseAction, sendUseActionFinished, scheduleCorpseCreditAward, scheduleCorpseDespawn);
	}

	internal bool TryUseDeadNpcCorpse<TCorpseState>(ICharacter looter, Identity deadNpcIdentity, IEnumerable<TCorpseState> corpses, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, Identity> corpseDeadNpcIdentity, Func<TCorpseState, DateTime> createdAtUtc, Func<ICharacter, Identity, bool> tryUseCorpse, out Identity routedCorpseIdentity) where TCorpseState : class
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return corpseAccess.TryUseDeadNpcCorpse(looter, deadNpcIdentity, corpses, corpseIdentity, corpseDeadNpcIdentity, createdAtUtc, tryUseCorpse, out routedCorpseIdentity);
	}

	internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement, IEnumerable<TCorpseState> corpses, Func<TCorpseState, int> corpseInventoryHandle, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, DateTime> expiresAtUtc, Func<TCorpseState, bool> isEmpty, Func<TCorpseState, int> remainingUnlootedItems, Func<TCorpseState, TCorpseLootItem> findCorpseLootItem, Func<TCorpseLootItem, Item> lootItem, Func<TCorpseLootItem, int> lootItemSlot, Action<TCorpseLootItem, bool> setLooted, Action<TCorpseState, bool> setOpened, Func<ICharacter, Item, bool> characterHasUniqueItemAlready, Action<ICharacter, string> sendChatText, Action<ICharacter> sendUseActionFinished, Func<ICharacter, Item, int, CorpseLootInventoryTransferResult> tryAddCorpseLootItem, Action<ICharacter, Identity, int> sendCorpseContainerAddItem, Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn, Action<TCorpseState, TimeSpan, string> extendCorpseLifetime, Action<int> despawnCorpse, TimeSpan itemLootLifetime, TimeSpan emptyCleanupDelay) where TCorpseState : class where TCorpseLootItem : class
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return corpseAccess.TryLootCorpseItem(looter, sourceContainer, target, targetPlacement, corpses, corpseInventoryHandle, corpseIdentity, expiresAtUtc, isEmpty, remainingUnlootedItems, findCorpseLootItem, lootItem, lootItemSlot, setLooted, setOpened, characterHasUniqueItemAlready, sendChatText, sendUseActionFinished, tryAddCorpseLootItem, sendCorpseContainerAddItem, scheduleCorpseDespawn, extendCorpseLifetime, despawnCorpse, itemLootLifetime, emptyCleanupDelay);
	}

	internal void ProcessPendingCorpseCreditAwards<TAward, TCorpseState>(IDictionary<int, TAward> pendingCorpseCreditAwards, IDictionary<int, TCorpseState> corpses, Func<TAward, DateTime> dueAtUtc, Func<TAward, int> corpseInstance, Func<TAward, Identity> looterIdentity, Func<TCorpseState, Identity> corpseIdentity, Func<Identity, ICharacter> findLooter, Func<ICharacter, bool> looterInPlayfield, Action<ICharacter, TCorpseState> awardCorpseCredits) where TAward : class where TCorpseState : class
	{
		corpseAccess.ProcessPendingCorpseCreditAwards(pendingCorpseCreditAwards, corpses, dueAtUtc, corpseInstance, looterIdentity, corpseIdentity, findLooter, looterInPlayfield, awardCorpseCredits);
	}

	internal bool HasPendingDeadNpcDespawn(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return npcRuntime.HasPendingDeadNpcDespawn(identity);
	}

	internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);
	}

	internal void ClearNpcCorpseDespawn(int corpseInstance)
	{
		npcRuntime.ClearNpcCorpseDespawn(corpseInstance);
	}

	internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)
	{
		npcRuntime.ProcessDueNpcCorpseDespawns(utcNow, despawnCorpse);
	}

	internal void ProcessDueCapturedSubwayRespawns(DateTime utcNow)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.ProcessDueCapturedSubwayRespawns(((PooledObject)playfield).Identity, utcNow);
	}

	internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
	{
		npcRuntime.BeginNpcDeath(attacker, target);
	}

	internal bool ProcessDeadNpcDespawn(ICharacter character)
	{
		return npcRuntime.ProcessDeadNpcDespawn(character);
	}

	internal void FinalizeNpcDespawn(ICharacter target)
	{
		npcRuntime.FinalizeNpcDespawn(target);
	}

	internal void ResetNpcCombatTick(ICharacter attacker)
	{
		npcRuntime.ResetCombatTick(attacker);
	}

	internal void ProcessNpcCombatTick(ICharacter attacker)
	{
		npcRuntime.ProcessCombatTick(attacker);
	}

	internal void ClearInvalidNpcCombatTarget(ICharacter attacker)
	{
		npcRuntime.ClearInvalidCombatTarget(attacker);
	}

	internal void ClearNpcFightingTarget(ICharacter character)
	{
		npcRuntime.ClearFightingTarget(character);
	}

	internal void StopDyingNpcCombatState(ICharacter target)
	{
		npcRuntime.StopDyingNpcCombatState(target);
	}

	internal void AcquireNpcAggro(ICharacter attacker, ICharacter target)
	{
		npcRuntime.AcquireAggro(attacker, target);
	}

	internal void ForceNpcTauntAggro(ICharacter taunter, ICharacter npc)
	{
		npcRuntime.ForceTauntAggro(taunter, npc);
	}

	internal void ProcessNpcPatrolTick(ICharacter character)
	{
		npcRuntime.ProcessPatrolTick(character);
	}

	internal void ClearNpcCombatTracking(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		npcRuntime.ClearCombatTracking(identity);
	}

	internal void StartPlayerAttack(ICharacter character, Identity target, Action<Identity> resetCombatTick)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		playerCombat.StartAttack(character, target, resetCombatTick);
	}

	internal void CancelPlayerAttack(ICharacter character, Action<Identity> resetCombatTick)
	{
		playerCombat.CancelAttack(character, resetCombatTick);
	}

	internal void ResetPlayerCombatTick(Identity attacker, Action<Identity> resetCombatTick)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		playerCombat.ResetCombatTick(attacker, resetCombatTick);
	}

	internal void ProcessPlayerCombatTick(ICharacter attacker, Action<Identity> clearCombatTracking, Func<Identity, ICharacter> findTarget, Func<ICharacter, bool> isValidTarget, Action<ICharacter, ICharacter> logInvalidTarget, Action<ICharacter, ICharacter> processValidatedCombatTick)
	{
		playerCombat.ProcessCombatTick(attacker, clearCombatTracking, findTarget, isValidTarget, logInvalidTarget, processValidatedCombatTick);
	}

	internal void ClearPlayerFightingTarget(ICharacter character, Action<Identity> clearCombatTracking)
	{
		playerCombat.ClearFightingTarget(character, clearCombatTracking);
	}

	internal void BeginPlayerDeath(ICharacter target, Action<ICharacter> beginDeath)
	{
		playerCombat.BeginDeath(target, beginDeath);
	}

	internal void CleanupPlayerDeathCombat(ICharacter target, Action<Identity> clearCombatTracking, Action<Identity> stopFightingDeadTarget, Action<ICharacter> sendCombatStop)
	{
		playerCombat.CleanupDeathCombat(target, clearCombatTracking, stopFightingDeadTarget, sendCombatStop);
	}
}
