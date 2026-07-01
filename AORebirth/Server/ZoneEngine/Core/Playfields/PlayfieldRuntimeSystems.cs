namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields.Content;

    #endregion

    internal sealed class PlayfieldRuntimeSystems
    {
        private readonly Playfield playfield;

        private readonly PlayfieldContentCoordinator content;

        private readonly NpcCorpseLifecycleCoordinator npcCorpseLifecycle;

        private readonly NpcCombatTickCoordinator npcCombatTick;

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
            this.npcCorpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield);
            this.npcCombatTick = new NpcCombatTickCoordinator(playfield);
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

        internal bool ShouldSuppressDbMobSpawn(DBMobSpawn mob)
        {
            if (mob == null)
            {
                return false;
            }

            return this.content.ShouldSuppressDbMobSpawn(mob.Playfield, mob.Id);
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
