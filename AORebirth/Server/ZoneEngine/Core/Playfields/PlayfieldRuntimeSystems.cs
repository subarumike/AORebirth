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

        private readonly NpcCorpseLifecycleCoordinator npcCorpseLifecycle;

        private readonly NpcCombatTickCoordinator npcCombatTick;

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
            this.npcCorpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield);
            this.npcCombatTick = new NpcCombatTickCoordinator(playfield);
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
            return this.npcCorpseLifecycle.HasPendingDeadNpcDespawn(identity);
        }

        internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
        {
            this.npcCorpseLifecycle.BeginNpcDeath(attacker, target);
        }

        internal bool ProcessDeadNpc(ICharacter character)
        {
            return this.npcCorpseLifecycle.ProcessDeadNpc(character);
        }

        internal void FinalizeNpcDespawn(ICharacter target)
        {
            this.npcCorpseLifecycle.FinalizeNpcDespawn(target);
        }

        internal void ResetNpcCombatTick(ICharacter attacker)
        {
            this.npcCombatTick.ResetCombatTick(attacker);
        }

        internal void ProcessNpcCombatTick(ICharacter attacker)
        {
            this.npcCombatTick.ProcessCombatTick(attacker);
        }

        internal void ClearNpcCombatTracking(Identity identity)
        {
            this.npcCombatTick.ClearTracking(identity);
        }
    }
}
