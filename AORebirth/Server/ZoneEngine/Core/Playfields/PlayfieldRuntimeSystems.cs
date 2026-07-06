namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields.Content;

    #endregion

    internal sealed class PlayfieldRuntimeSystems
    {
        private readonly Playfield playfield;

        private readonly PlayfieldContentCoordinator content;

        private readonly PlayfieldContentDataProvider contentData;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly NPCRuntimeService npcRuntime;

        private readonly PlayerCombatRuntimeService playerCombat;

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
            this.dynelRegistry = new PlayfieldDynelRegistry(playfieldIdentity);
            this.npcRuntime = new NPCRuntimeService(playfield, this.dynelRegistry);
            this.playerCombat = new PlayerCombatRuntimeService();
            this.packetSequencing = new PacketSequencingCoordinator();
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

        internal void ActivateNpc(ICharacter character)
        {
            this.npcRuntime.ActivateNpc(character);
        }

        internal void RegisterNpcHome(ICharacter character)
        {
            this.npcRuntime.RegisterNpcHome(character);
        }

        internal void RemoveNpcImmediately(
            ICharacter target,
            Action<Identity> stopFightingDeadTarget,
            Action<Identity> cancelPendingCorpseSpawn)
        {
            this.npcRuntime.RemoveNpcImmediately(target, stopFightingDeadTarget, cancelPendingCorpseSpawn);
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

        internal ReadOnlyCollection<StaticDynel> StaticDynels()
        {
            return this.dynelRegistry.StaticDynels();
        }

        internal PacketSequencingCoordinator PacketSequencing
        {
            get
            {
                return this.packetSequencing;
            }
        }

        internal void SendPrivateCityPlayfieldReadyBlock(ZoneClient client, ICharacter character)
        {
            this.privateCityReadyInit.SendPlayfieldReadyBlock(client, character);
        }

        internal void SendPrivateCityPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
        {
            this.privateCityReadyInit.SendPreFullCharacterReadyBlock(client, character);
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

        internal void CancelPlayerAttack(ICharacter character, Action<ICharacter> cancelAttack)
        {
            this.playerCombat.CancelAttack(character, cancelAttack);
        }

        internal void ResetPlayerCombatTick(Identity attacker, Action<Identity> resetCombatTick)
        {
            this.playerCombat.ResetCombatTick(attacker, resetCombatTick);
        }

        internal void ProcessPlayerCombatTick(ICharacter attacker, Action<ICharacter> processCombatTick)
        {
            this.playerCombat.ProcessCombatTick(attacker, processCombatTick);
        }

        internal void ClearPlayerFightingTarget(ICharacter character, Action<ICharacter> clearFightingTarget)
        {
            this.playerCombat.ClearFightingTarget(character, clearFightingTarget);
        }

        internal void BeginPlayerDeath(ICharacter target, Action<ICharacter> beginDeath)
        {
            this.playerCombat.BeginDeath(target, beginDeath);
        }
    }
}
