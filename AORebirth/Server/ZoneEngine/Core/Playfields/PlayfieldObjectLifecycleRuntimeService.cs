namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    internal sealed class PlayfieldObjectLifecycleRuntimeService
    {
        internal void RemoveInstancedEntity(IInstancedEntity entity)
        {
            Pool.Instance.RemoveObject(entity);
        }

        internal void DespawnCorpse(
            int corpseInstance,
            Action<Identity> sendDespawn,
            Action<int> clearNpcCorpseDespawn,
            Action<int> removeCorpseState,
            Action<int> removePendingCorpseCreditAward)
        {
            Identity corpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = corpseInstance };
            sendDespawn(corpseIdentity);
            clearNpcCorpseDespawn(corpseInstance);
            removeCorpseState(corpseInstance);
            removePendingCorpseCreditAward(corpseInstance);

            LogUtil.Debug(DebugInfoDetail.Engine, string.Format("Corpse despawned corpse={0}", corpseIdentity));
        }
    }
}
