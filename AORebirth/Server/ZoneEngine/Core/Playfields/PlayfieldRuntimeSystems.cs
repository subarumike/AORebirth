namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.Playfields.Content;

    #endregion

    internal sealed class PlayfieldRuntimeSystems
    {
        private readonly Playfield playfield;

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

        private readonly PlayfieldNpcCombatMovementRuntimeService npcCombatMovement;

        private readonly PlayfieldRewardRuntimeService rewards;

        private readonly PlayfieldLifecycleRuntimeService lifecycle;

        private readonly PlayfieldPlayerDeathRespawnRuntimeService playerDeathRespawn;

        private readonly PlayfieldInteractionRuntimeService interaction;

        private readonly PlayerCombatRuntimeService playerCombat;

        private readonly PlayfieldStatelTransitionRuntimeService statelTransitions;

        private readonly PlayfieldStatUpdateRuntimeService statUpdates;

        private readonly PlayfieldStaticDynelRuntimeService staticDynelRuntime;

        private readonly PlayfieldTimedLifecycleRuntimeService timedLifecycle;

        private readonly PlayfieldVendorRuntimeService vendors;

        private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout;

        private readonly PacketSequencingCoordinator packetSequencing;

        private readonly PrivateCityReadyInitCoordinator privateCityReadyInit;

        internal PlayfieldRuntimeSystems(
            Playfield playfield,
            Identity playfieldIdentity,
            Func<Identity, bool> isPrivateCityPlayfieldCandidate,
            Func<int, bool> isCapturedMontroyalPrivateCityInstance,
            Func<ICharacter, int> resolveCharacterOrganizationInstance,
            Func<int, string> resolveOrganizationName,
            Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue)
        {
            if (playfield == null)
            {
                throw new ArgumentNullException("playfield");
            }

            this.playfield = playfield;
            this.content = new PlayfieldContentCoordinator(
                new AreteContentModule(),
                new MontroyalContentModule(),
                new PrivateCityContentModule());
            this.contentData = new PlayfieldContentDataProvider(isPrivateCityPlayfieldCandidate);
            this.corpseAccess = new PlayfieldCorpseAccessRuntimeService();
            this.aotomationDelivery = new PlayfieldAOtomationDeliveryRuntimeService();
            this.dbMobSpawns = new PlayfieldDbMobSpawnRuntimeService();
            this.dynelRegistry = new PlayfieldDynelRegistry(playfieldIdentity);
            this.environmentFunctions = new PlayfieldEnvironmentFunctionRuntimeService();
            this.objectLifecycle = new PlayfieldObjectLifecycleRuntimeService();
            this.objectMaterialization = new PlayfieldObjectMaterializationRuntimeService();
            this.publishFanout = new PlayfieldPublishFanoutRuntimeService();
            this.inventoryContainer = InventoryContainerRuntimeService.Default;
            this.rewards = new PlayfieldRewardRuntimeService();
            this.npcRuntime = new NPCRuntimeService(playfield, this.dynelRegistry, this.rewards);
            this.npcCombatMovement = new PlayfieldNpcCombatMovementRuntimeService();
            this.lifecycle = new PlayfieldLifecycleRuntimeService();
            this.playerDeathRespawn = new PlayfieldPlayerDeathRespawnRuntimeService();
            this.interaction = new PlayfieldInteractionRuntimeService();
            this.playerCombat = new PlayerCombatRuntimeService();
            this.statelTransitions = new PlayfieldStatelTransitionRuntimeService();
            this.statUpdates = new PlayfieldStatUpdateRuntimeService();
            this.staticDynelRuntime = new PlayfieldStaticDynelRuntimeService();
            this.timedLifecycle = new PlayfieldTimedLifecycleRuntimeService();
            this.vendors = new PlayfieldVendorRuntimeService();
            this.visibilityFanout = new PlayfieldVisibilityFanoutRuntimeService();
            this.packetSequencing = new PacketSequencingCoordinator();
            this.packetSequences = new PlayfieldPacketSequencingRuntimeService(this.packetSequencing);
            this.privateCityReadyInit =
                new PrivateCityReadyInitCoordinator(
                    playfieldIdentity,
                    isPrivateCityPlayfieldCandidate,
                    isCapturedMontroyalPrivateCityInstance,
                    resolveCharacterOrganizationInstance,
                    resolveOrganizationName,
                    resolveCharacterStatWireValue);
        }

        internal void RegisterContent(Identity playfieldIdentity)
        {
            this.content.RegisterContent(this.playfield, playfieldIdentity);
        }

        internal void MaterializeStartupObjects(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels)
        {
            this.objectMaterialization.MaterializeStartupObjects(
                playfieldIdentity,
                statels,
                this.dbMobSpawns.LoadMobSpawnDefinitions,
                this.ShouldSuppressDbMobSpawn,
                this.dbMobSpawns.LoadMobSpawnStats,
                (mob, stats) => this.dbMobSpawns.InstantiateDbMobSpawn(mob, stats, this.playfield),
                this.ActivateNpc,
                this.dbMobSpawns.AttachMobSpawnKnuBot,
                this.RegisterContent,
                this.TryResolveVendorStatels,
                vendorStatels => this.vendors.SpawnVendors(this.playfield, vendorStatels),
                this.ResolveStaticDynels,
                staticDynel => this.staticDynelRuntime.CreateStaticDynel(playfieldIdentity, staticDynel),
                this.RegisterDynel,
                this.RefreshDynelRegistry);
        }

        internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
        {
            this.npcRuntime.SpawnCapturedNpcContent(playfieldIdentity);
        }

        internal List<StatelData> ResolveStatels(Identity playfieldIdentity)
        {
            return this.contentData.ResolveStatels(playfieldIdentity);
        }

        internal bool TryResolveVendorStatels(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels,
            out StatelData[] vendorStatels)
        {
            return this.contentData.TryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels);
        }

        internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)
        {
            return this.contentData.ResolveCollisionStatels(statels);
        }

        internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)
        {
            return this.contentData.ResolveStaticDynels(playfieldIdentity);
        }

        internal bool ShouldSuppressDbMobSpawn(DBMobSpawn mob)
        {
            if (mob == null)
            {
                return false;
            }

            return this.content.ShouldSuppressDbMobSpawn(mob.Playfield, mob.Id);
        }

        internal void RefreshDynelRegistry()
        {
            this.dynelRegistry.RefreshFromPool();
        }

        internal void RegisterDynel(IEntity entity)
        {
            this.dynelRegistry.Register(entity);
        }

        internal void UnregisterDynel(Identity identity)
        {
            this.dynelRegistry.Unregister(identity);
        }

        internal void RemoveInstancedEntity(IInstancedEntity entity)
        {
            this.objectLifecycle.RemoveInstancedEntity(entity);
        }

        internal int DespawnCorpses<TCorpseState>(
            IDictionary<int, TCorpseState> pendingCorpseSpawns,
            IDictionary<int, TCorpseState> corpses,
            Func<string, Identity, bool> shouldDespawn,
            Func<TCorpseState, string> corpseName,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Action<int> despawnCorpse)
        {
            return this.objectLifecycle.DespawnCorpses(
                pendingCorpseSpawns,
                corpses,
                shouldDespawn,
                corpseName,
                deadNpcIdentity,
                despawnCorpse);
        }

        internal void DespawnCorpse(
            int corpseInstance,
            Action<Identity> sendDespawn,
            Action<int> clearNpcCorpseDespawn,
            Action<int> removeCorpseState,
            Action<int> removePendingCorpseCreditAward)
        {
            this.objectLifecycle.DespawnCorpse(
                corpseInstance,
                sendDespawn,
                clearNpcCorpseDespawn,
                removeCorpseState,
                removePendingCorpseCreditAward);
        }

        internal void ProcessPendingCorpseSpawns<TCorpseState>(
            IDictionary<int, TCorpseState> pendingCorpseSpawns,
            Func<TCorpseState, DateTime> spawnsAtUtc,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Func<Identity, ICharacter> findDeadNpc,
            Action<ICharacter, Identity> registerCorpse,
            Action<Identity, Identity> traceCorpseFullUpdate,
            Action<ICharacter, Identity> sendCorpseFullUpdate)
        {
            this.objectLifecycle.ProcessPendingCorpseSpawns(
                pendingCorpseSpawns,
                spawnsAtUtc,
                corpseIdentity,
                deadNpcIdentity,
                findDeadNpc,
                registerCorpse,
                traceCorpseFullUpdate,
                sendCorpseFullUpdate);
        }

        internal void ActivateNpc(ICharacter character)
        {
            this.npcRuntime.ActivateNpc(character);
        }

        internal void RegisterNpcHome(ICharacter character)
        {
            this.npcRuntime.RegisterNpcHome(character);
        }

        internal void DespawnNpcImmediately(
            ICharacter target,
            Action<Identity> stopFightingDeadTarget,
            Action<Identity> cancelPendingCorpseSpawn)
        {
            this.npcRuntime.DespawnNpcImmediately(target, stopFightingDeadTarget, cancelPendingCorpseSpawn);
        }

        internal void RegisterStatels(IEnumerable<StatelData> statels)
        {
            this.dynelRegistry.RegisterStatels(statels);
        }

        internal IInstancedEntity FindByIdentity(Identity identity)
        {
            return this.dynelRegistry.FindByIdentity(identity);
        }

        internal T FindByIdentity<T>(Identity identity) where T : class, IEntity
        {
            return this.dynelRegistry.FindByIdentity<T>(identity);
        }

        internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)
        {
            return this.dynelRegistry.FindDynelsInRange(dynel, range);
        }

        internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)
        {
            return this.dynelRegistry.FindCharactersInRange(dynel, range);
        }

        internal ReadOnlyCollection<ICharacter> Characters()
        {
            return this.dynelRegistry.Characters();
        }

        internal ReadOnlyCollection<Character> CharacterEntities()
        {
            return this.dynelRegistry.CharacterEntities();
        }

        internal void AnnounceToCharacterClients(Action<Character> publishToCharacterClient)
        {
            this.visibilityFanout.AnnounceToCharacterClients(this.CharacterEntities(), publishToCharacterClient);
        }

        internal void AnnounceToOtherCharacterClients(Identity excludedIdentity, Action<Character> publishToCharacterClient)
        {
            this.visibilityFanout.AnnounceToOtherCharacterClients(
                this.CharacterEntities(),
                excludedIdentity,
                publishToCharacterClient);
        }

        internal void FanoutExistingCharactersForScfu(
            ICharacter recipient,
            Func<ICharacter, bool> sendExistingCharacter,
            Action<ICharacter, bool, bool, bool> logVisibilityCandidate)
        {
            this.visibilityFanout.FanoutExistingCharactersForScfu(
                recipient,
                this.Characters(),
                sendExistingCharacter,
                logVisibilityCandidate);
        }

        internal void PublishMessageBodyToClient(IZoneClient client, MessageBody body, Action<object> publish)
        {
            this.publishFanout.PublishMessageBodyToClient(client, body, publish);
        }

        internal void PublishMessageToClient(IZoneClient client, Message message, Action<object> publish)
        {
            this.publishFanout.PublishMessageToClient(client, message, publish);
        }

        internal void DispatchMessageToPlayfield(MessageBody body, Action<MessageBody> announce)
        {
            this.publishFanout.DispatchMessageToPlayfield(body, announce);
        }

        internal void DispatchMessageToPlayfieldOthers(
            MessageBody body,
            Identity excludedIdentity,
            Action<MessageBody, Identity> announceOthers)
        {
            this.publishFanout.DispatchMessageToPlayfieldOthers(body, excludedIdentity, announceOthers);
        }

        internal void DeliverAOtomationMessageToClient(IMSendAOtomationMessageToClient clientMessage)
        {
            this.aotomationDelivery.SendMessageToClient(clientMessage);
        }

        internal void DeliverAOtomationMessageBodyToClient(IMSendAOtomationMessageBodyToClient message)
        {
            this.aotomationDelivery.SendMessageBodyToClient(message);
        }

        internal void DeliverAOtomationMessageBodiesToClient(IMSendAOtomationMessageBodiesToClient message)
        {
            this.aotomationDelivery.SendMessageBodiesToClient(message);
        }

        internal void DeliverAOtomationMessageToPlayfield(
            IMSendAOtomationMessageToPlayfield clientMessage,
            Action<MessageBody> announce)
        {
            this.aotomationDelivery.SendMessageToPlayfield(
                clientMessage,
                body => this.publishFanout.DispatchMessageToPlayfield(body, announce));
        }

        internal void DeliverAOtomationMessageToPlayfieldOthers(
            IMSendAOtomationMessageToPlayfieldOthers clientMessage,
            Action<MessageBody, Identity> announceOthers)
        {
            this.aotomationDelivery.SendMessageToPlayfieldOthers(
                clientMessage,
                (body, excludedIdentity) =>
                    this.publishFanout.DispatchMessageToPlayfieldOthers(body, excludedIdentity, announceOthers));
        }

        internal bool IsInNpcCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            return this.npcCombatMovement.IsInCombatRange(attacker, target, range);
        }

        internal void UpdateNpcMeleeFollowHold(
            ICharacter attacker,
            ICharacter target,
            double range,
            Action<ICharacter, AORebirth.Core.Vector.Vector3> moveNpcToPosition,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            this.npcCombatMovement.UpdateNpcMeleeFollowHold(
                attacker,
                target,
                range,
                moveNpcToPosition,
                logNpcBrain);
        }

        internal void TryMoveNpcIntoCombatRange(
            ICharacter attacker,
            ICharacter target,
            double range,
            Action<ICharacter, AORebirth.Core.Vector.Vector3> moveNpcToPosition,
            Action<string, string, ICharacter, ICharacter, double, double> logNpcBrain)
        {
            this.npcCombatMovement.TryMoveNpcIntoCombatRange(
                attacker,
                target,
                range,
                moveNpcToPosition,
                logNpcBrain);
        }

        internal void SendChangedStats(ICharacter character, Action<ICharacter> sendChangedStats)
        {
            this.statUpdates.SendChangedStats(character, sendChangedStats);
        }

        internal void SendChangedStatsIfChanged(
            ICharacter character,
            bool changed,
            Action<ICharacter> sendChangedStats)
        {
            this.statUpdates.SendChangedStatsIfChanged(character, changed, sendChangedStats);
        }

        internal void SendChangedStatsIfClient(
            ICharacter character,
            Func<ICharacter, bool> hasClient,
            Action<ICharacter> sendChangedStats)
        {
            this.statUpdates.SendChangedStatsIfClient(character, hasClient, sendChangedStats);
        }

        internal void RunPlayerDeathStatUpdateSequence(
            ICharacter target,
            Action<ICharacter> sendChangedStats,
            Action<ICharacter> cleanupDeathCombat,
            Action<ICharacter> sendDeathAnimation)
        {
            this.statUpdates.RunPlayerDeathStatUpdateSequence(
                target,
                sendChangedStats,
                cleanupDeathCombat,
                sendDeathAnimation);
        }

        internal ReadOnlyCollection<StaticDynel> StaticDynels()
        {
            return this.dynelRegistry.StaticDynels();
        }

        internal void RunVisibilityPacketPairSequence(
            Action recordSimpleCharFullUpdate,
            Action sendSimpleCharFullUpdate,
            Action prepareCharInPlay,
            Action recordCharInPlay,
            Action sendCharInPlay)
        {
            this.packetSequences.RunVisibilityPacketPairSequence(
                recordSimpleCharFullUpdate,
                sendSimpleCharFullUpdate,
                prepareCharInPlay,
                recordCharInPlay,
                sendCharInPlay);
        }

        internal void RunPlayfieldTransferBeginSequence(Action enterZoningPhase, Action sendTeleportPacket)
        {
            this.packetSequences.RunPlayfieldTransferBeginSequence(enterZoningPhase, sendTeleportPacket);
        }

        internal void SendPrivateCityPlayfieldReadyBlock(ZoneClient client, ICharacter character)
        {
            this.privateCityReadyInit.SendPlayfieldReadyBlock(client, character);
        }

        internal void SendPrivateCityPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
        {
            this.privateCityReadyInit.SendPreFullCharacterReadyBlock(client, character);
        }

        internal void ProcessHeartbeatTimedLifecycle(
            Identity playfieldIdentity,
            Action processPendingCorpseSpawns,
            Action processCorpseDespawns,
            Action processPendingCorpseCreditAwards,
            Action<ICharacter> processRegeneration,
            Action<ICharacter> processCombatTick,
            Action<ICharacter> processFollow,
            Action<ICharacter> processPlayerCollision)
        {
            this.timedLifecycle.ProcessHeartbeatLifecycle(
                playfieldIdentity,
                this.Characters,
                this.HasPendingDeadNpcDespawn,
                processPendingCorpseSpawns,
                processCorpseDespawns,
                processPendingCorpseCreditAwards,
                this.ProcessDeadNpcDespawn,
                processRegeneration,
                processCombatTick,
                this.ProcessNpcPatrolTick,
                processFollow,
                processPlayerCollision);
        }

        internal void ProcessPlayerRespawn(
            ICharacter character,
            Dynel dynel,
            Identity corpseIdentity,
            Coordinate destination,
            Identity destinationPlayfield,
            Action<ICharacter, Identity> logCorpseVisualSkipped,
            Action<ICharacter> sendDeathSocialStatus,
            Action<ICharacter> markPlayerRespawned,
            Action<ICharacter> sendDeathRespawnStateStats,
            Action<ICharacter> stopMovement,
            Action<ICharacter> sendChangedStats,
            Action<ICharacter, Identity, Identity, Coordinate> logRespawnRequested,
            Action<ICharacter> enableTimers,
            Func<Dynel, Coordinate, IQuaternion, Identity, bool> tryCompleteCurrentPlayfieldRespawn,
            Action<Dynel, Coordinate, IQuaternion, Identity> transferToRespawnPlayfield,
            Action<Identity> clearCombatTracking,
            Action<Identity> stopFightingDeadTarget,
            Action<ICharacter> sendCombatStop)
        {
            this.playerDeathRespawn.ProcessPlayerRespawn(
                character,
                dynel,
                corpseIdentity,
                destination,
                destinationPlayfield,
                logCorpseVisualSkipped,
                sendDeathSocialStatus,
                markPlayerRespawned,
                sendDeathRespawnStateStats,
                stopMovement,
                x => this.CleanupPlayerDeathCombat(x, clearCombatTracking, stopFightingDeadTarget, sendCombatStop),
                sendChangedStats,
                logRespawnRequested,
                enableTimers,
                tryCompleteCurrentPlayfieldRespawn,
                transferToRespawnPlayfield);
        }

        internal void PreparePlayfieldTransfer(
            Dynel dynel,
            Action<int> clearTransferContactState,
            Action<Dynel> disableTimers)
        {
            this.lifecycle.PreparePlayfieldTransfer(dynel, clearTransferContactState, disableTimers);
        }

        internal void ClearStatelTransitionContactState(int dynelId)
        {
            this.statelTransitions.ClearContactState(dynelId);
        }

        internal void PrimeStatelCollisionContacts(
            ICharacter dynel,
            IEnumerable<StatelData> collisionStatels)
        {
            this.statelTransitions.PrimeStatelCollisionContacts(dynel, collisionStatels);
        }

        internal void CheckStatelCollision(
            ICharacter dynel,
            Identity playfieldIdentity,
            IEnumerable<StatelData> collisionStatels,
            Func<ICharacter, int> resolvePrivateCityDestinationPlayfield,
            Func<ICharacter, int> resolveCharacterOrganizationInstance,
            Action<ICharacter> stopMovement,
            Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus,
            Action<Dynel, Coordinate, AORebirth.Core.Vector.Quaternion, int> teleportToPlayfield)
        {
            this.statelTransitions.CheckStatelCollision(
                dynel,
                playfieldIdentity,
                collisionStatels,
                resolvePrivateCityDestinationPlayfield,
                resolveCharacterOrganizationInstance,
                stopMovement,
                sendCapturedPrivateCityEntrySocialStatus,
                teleportToPlayfield);
        }

        internal bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            return this.interaction.TryHandleGenericCmdUse(client, message, target);
        }

        internal void ExecuteFunction(
            IMExecuteFunction imExecuteFunction,
            Func<Identity, INamedEntity> findNamedEntity,
            Action<Character, string> sendNoValidTargetMessage)
        {
            this.environmentFunctions.ExecuteFunction(
                imExecuteFunction,
                findNamedEntity,
                sendNoValidTargetMessage);
        }

        internal void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
        {
            this.inventoryContainer.EnsureWeaponVisualMeshes(character, announceAppearanceUpdate);
        }

        internal bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
        {
            return this.inventoryContainer.CharacterHasUniqueItemAlready(character, item);
        }

        internal CorpseLootInventoryTransferResult TryAddCorpseLootItem(
            ICharacter looter,
            IItem item,
            int targetPlacement)
        {
            return this.inventoryContainer.TryAddCorpseLootItem(looter, item, targetPlacement);
        }

        internal bool TryUseCorpse<TCorpseState>(
            ICharacter looter,
            Identity corpseIdentity,
            IDictionary<int, TCorpseState> corpses,
            TimeSpan itemLootLifetime,
            TimeSpan emptyCleanupDelay,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Func<TCorpseState, DateTime> expiresAtUtc,
            Func<TCorpseState, bool> hasUnlootedItems,
            Action<TCorpseState, bool> setOpened,
            Func<TCorpseState, object> lootClass,
            Action<int> despawnCorpse,
            Action<TCorpseState, TimeSpan, string> extendCorpseLifetime,
            Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate,
            Action<ICharacter, TCorpseState> scheduleCorpseCreditAward,
            Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn)
            where TCorpseState : class
        {
            return this.corpseAccess.TryUseCorpse(
                looter,
                corpseIdentity,
                corpses,
                itemLootLifetime,
                emptyCleanupDelay,
                deadNpcIdentity,
                expiresAtUtc,
                hasUnlootedItems,
                setOpened,
                lootClass,
                despawnCorpse,
                extendCorpseLifetime,
                sendCorpseInventoryUpdate,
                scheduleCorpseCreditAward,
                scheduleCorpseDespawn);
        }

        internal bool TryUseDeadNpcCorpse<TCorpseState>(
            ICharacter looter,
            Identity deadNpcIdentity,
            IEnumerable<TCorpseState> corpses,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, Identity> corpseDeadNpcIdentity,
            Func<TCorpseState, DateTime> createdAtUtc,
            Func<ICharacter, Identity, bool> tryUseCorpse,
            out Identity routedCorpseIdentity)
            where TCorpseState : class
        {
            return this.corpseAccess.TryUseDeadNpcCorpse(
                looter,
                deadNpcIdentity,
                corpses,
                corpseIdentity,
                corpseDeadNpcIdentity,
                createdAtUtc,
                tryUseCorpse,
                out routedCorpseIdentity);
        }

        internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(
            ICharacter looter,
            Identity sourceContainer,
            Identity target,
            int targetPlacement,
            IEnumerable<TCorpseState> corpses,
            Func<TCorpseState, int> corpseInventoryHandle,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, DateTime> expiresAtUtc,
            Func<TCorpseState, bool> hasUnlootedItems,
            Func<TCorpseState, int> remainingUnlootedItems,
            Func<TCorpseState, TCorpseLootItem> findCorpseLootItem,
            Func<TCorpseLootItem, Item> lootItem,
            Func<TCorpseLootItem, int> lootItemSlot,
            Action<TCorpseLootItem, bool> setLooted,
            Action<TCorpseState, bool> setOpened,
            Func<ICharacter, Item, bool> characterHasUniqueItemAlready,
            Action<ICharacter, string> sendChatText,
            Action<ICharacter> sendUseActionFinished,
            Func<ICharacter, Item, int, CorpseLootInventoryTransferResult> tryAddCorpseLootItem,
            Action<ICharacter, Identity, int> sendCorpseContainerAddItem,
            Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn,
            Action<TCorpseState, TimeSpan, string> extendCorpseLifetime,
            Action<int> despawnCorpse,
            TimeSpan itemLootLifetime,
            TimeSpan emptyCleanupDelay)
            where TCorpseState : class
            where TCorpseLootItem : class
        {
            return this.corpseAccess.TryLootCorpseItem(
                looter,
                sourceContainer,
                target,
                targetPlacement,
                corpses,
                corpseInventoryHandle,
                corpseIdentity,
                expiresAtUtc,
                hasUnlootedItems,
                remainingUnlootedItems,
                findCorpseLootItem,
                lootItem,
                lootItemSlot,
                setLooted,
                setOpened,
                characterHasUniqueItemAlready,
                sendChatText,
                sendUseActionFinished,
                tryAddCorpseLootItem,
                sendCorpseContainerAddItem,
                scheduleCorpseDespawn,
                extendCorpseLifetime,
                despawnCorpse,
                itemLootLifetime,
                emptyCleanupDelay);
        }

        internal void ProcessPendingCorpseCreditAwards<TAward, TCorpseState>(
            IDictionary<int, TAward> pendingCorpseCreditAwards,
            IDictionary<int, TCorpseState> corpses,
            Func<TAward, DateTime> dueAtUtc,
            Func<TAward, int> corpseInstance,
            Func<TAward, Identity> looterIdentity,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<Identity, ICharacter> findLooter,
            Func<ICharacter, bool> looterInPlayfield,
            Action<ICharacter, TCorpseState> awardCorpseCredits)
            where TAward : class
            where TCorpseState : class
        {
            this.corpseAccess.ProcessPendingCorpseCreditAwards(
                pendingCorpseCreditAwards,
                corpses,
                dueAtUtc,
                corpseInstance,
                looterIdentity,
                corpseIdentity,
                findLooter,
                looterInPlayfield,
                awardCorpseCredits);
        }

        internal bool HasPendingDeadNpcDespawn(Identity identity)
        {
            return this.npcRuntime.HasPendingDeadNpcDespawn(identity);
        }

        internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)
        {
            this.npcRuntime.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);
        }

        internal void ClearNpcCorpseDespawn(int corpseInstance)
        {
            this.npcRuntime.ClearNpcCorpseDespawn(corpseInstance);
        }

        internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)
        {
            this.npcRuntime.ProcessDueNpcCorpseDespawns(utcNow, despawnCorpse);
        }

        internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
        {
            this.npcRuntime.BeginNpcDeath(attacker, target);
        }

        internal bool ProcessDeadNpcDespawn(ICharacter character)
        {
            return this.npcRuntime.ProcessDeadNpcDespawn(character);
        }

        internal void FinalizeNpcDespawn(ICharacter target)
        {
            this.npcRuntime.FinalizeNpcDespawn(target);
        }

        internal void ResetNpcCombatTick(ICharacter attacker)
        {
            this.npcRuntime.ResetCombatTick(attacker);
        }

        internal void ProcessNpcCombatTick(ICharacter attacker)
        {
            this.npcRuntime.ProcessCombatTick(attacker);
        }

        internal void ClearInvalidNpcCombatTarget(ICharacter attacker)
        {
            this.npcRuntime.ClearInvalidCombatTarget(attacker);
        }

        internal void ClearNpcFightingTarget(ICharacter character)
        {
            this.npcRuntime.ClearFightingTarget(character);
        }

        internal void StopDyingNpcCombatState(ICharacter target)
        {
            this.npcRuntime.StopDyingNpcCombatState(target);
        }

        internal void AcquireNpcAggro(ICharacter attacker, ICharacter target)
        {
            this.npcRuntime.AcquireAggro(attacker, target);
        }

        internal void ProcessNpcPatrolTick(ICharacter character)
        {
            this.npcRuntime.ProcessPatrolTick(character);
        }

        internal void ClearNpcCombatTracking(Identity identity)
        {
            this.npcRuntime.ClearCombatTracking(identity);
        }

        internal void StartPlayerAttack(
            ICharacter character,
            Identity target,
            Action<Identity> resetCombatTick)
        {
            this.playerCombat.StartAttack(character, target, resetCombatTick);
        }

        internal void CancelPlayerAttack(ICharacter character, Action<Identity> resetCombatTick)
        {
            this.playerCombat.CancelAttack(character, resetCombatTick);
        }

        internal void ResetPlayerCombatTick(Identity attacker, Action<Identity> resetCombatTick)
        {
            this.playerCombat.ResetCombatTick(attacker, resetCombatTick);
        }

        internal void ProcessPlayerCombatTick(
            ICharacter attacker,
            Action<Identity> clearCombatTracking,
            Func<Identity, ICharacter> findTarget,
            Func<ICharacter, bool> isValidTarget,
            Action<ICharacter, ICharacter> logInvalidTarget,
            Action<ICharacter, ICharacter> processValidatedCombatTick)
        {
            this.playerCombat.ProcessCombatTick(
                attacker,
                clearCombatTracking,
                findTarget,
                isValidTarget,
                logInvalidTarget,
                processValidatedCombatTick);
        }

        internal void ClearPlayerFightingTarget(ICharacter character, Action<Identity> clearCombatTracking)
        {
            this.playerCombat.ClearFightingTarget(character, clearCombatTracking);
        }

        internal void BeginPlayerDeath(ICharacter target, Action<ICharacter> beginDeath)
        {
            this.playerCombat.BeginDeath(target, beginDeath);
        }

        internal void CleanupPlayerDeathCombat(
            ICharacter target,
            Action<Identity> clearCombatTracking,
            Action<Identity> stopFightingDeadTarget,
            Action<ICharacter> sendCombatStop)
        {
            this.playerCombat.CleanupDeathCombat(
                target,
                clearCombatTracking,
                stopFightingDeadTarget,
                sendCombatStop);
        }
    }
}
