namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    /// <summary>
    /// Process entry point for the durable mission repository. This class owns no player state; every read and
    /// mutation is delegated to the configured repository.
    /// </summary>
    public static class MissionRuntime
    {
        public const string RexB18CQuestId = MissionDefinitionCatalog.RexB18CQuestId;
        public const string RexB18DQuestId = MissionDefinitionCatalog.RexB18DQuestId;
        public const string RexB18EQuestId = MissionDefinitionCatalog.RexB18EQuestId;
        public const string RexB18FQuestId = MissionDefinitionCatalog.RexB18FQuestId;
        public const string RexB194QuestId = MissionDefinitionCatalog.RexB194QuestId;
        public const string WindcallerKarrecQuestId = MissionDefinitionCatalog.WindcallerKarrecQuestId;

        private static readonly object SyncRoot = new object();

        private static IMissionRepository repository;
        private static PersistentMissionService service;
        private static MissionRewardCoordinator rewards;

        public static bool IsInitialized
        {
            get
            {
                lock (SyncRoot)
                {
                    return service != null;
                }
            }
        }

        public static PersistentMissionService Service
        {
            get
            {
                lock (SyncRoot)
                {
                    if (service == null)
                    {
                        throw new InvalidOperationException("The persistent mission runtime has not been initialized.");
                    }

                    return service;
                }
            }
        }

        public static MissionRewardCoordinator Rewards
        {
            get
            {
                lock (SyncRoot)
                {
                    if (rewards == null)
                    {
                        throw new InvalidOperationException("The persistent mission runtime has not been initialized.");
                    }

                    return rewards;
                }
            }
        }

        public static void Initialize(AreteFrameworkRegistries registries)
        {
            Initialize(registries, new MySqlMissionRepository());
        }

        public static void Initialize(AreteFrameworkRegistries registries, IMissionRepository missionRepository)
        {
            if (registries == null || !registries.IsValid)
            {
                throw new InvalidOperationException("Persistent missions require a valid checked-in content registry.");
            }

            if (missionRepository == null)
            {
                throw new ArgumentNullException("missionRepository");
            }

            IList<MissionDefinition> definitions = BuildDefinitions(registries.QuestRegistry);
            var initializedService = new PersistentMissionService(missionRepository, definitions);
            var initializedRewards = new MissionRewardCoordinator(missionRepository);

            lock (SyncRoot)
            {
                repository = missionRepository;
                service = initializedService;
                rewards = initializedRewards;
            }
        }

        public static string ResolveAccountKey(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            DBCharacter character = CharacterDao.Instance.Get(characterId);
            return character == null || string.IsNullOrWhiteSpace(character.Username)
                       ? null
                       : character.Username.Trim();
        }

        public static MissionReloadResult ReloadForLogin(int characterId)
        {
            return Service.ReloadForLogin(characterId);
        }

        public static MissionReloadResult ReloadForReconnect(int characterId)
        {
            return Service.ReloadForReconnect(characterId);
        }

        public static MissionReloadResult ReloadForZoning(int characterId)
        {
            return Service.ReloadForZoning(characterId);
        }

        internal static IList<MissionDefinition> BuildDefinitions(QuestContentRegistry questRegistry)
        {
            return MissionDefinitionCatalog.Build(questRegistry);
        }
    }
}
