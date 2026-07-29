using System;
using System.Collections.Generic;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using ZoneEngine.Core.Arete;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Missions;

public static class MissionRuntime
{
	public const string RexB18CQuestId = "Mission:5514B18C";

	public const string RexB18DQuestId = "Mission:5514B18D";

	public const string RexB18EQuestId = "Mission:5514B18E";

	public const string RexB18FQuestId = "Mission:5514B18F";

	public const string RexB194QuestId = "Mission:5514B194";

	public const string RexB196QuestId = "Mission:5514B196";

	public const string RexFlintQuestId = "Mission:5514B198";

	public const string RexB199QuestId = "Mission:5514B199";

	public const string RexB19AQuestId = "Mission:5514B19A";

	public const string RexFlintFindBioQuestId = "Mission:5514B19B";

	public const string RexFlintDeliverBioQuestId = "Mission:5514B19C";

	public const string RexFlintSurveillanceUplinkQuestId = "Mission:5514B19D";

	public const string RexFlintPlantBugQuestId = "Mission:5514B19E";

	public const string RexFlintDeliverHc12BillQuestId = "Mission:5514B19F";

	public const string RexFlintKneecappingQuestId = "Mission:5514B1A0";

	public const string RexFlintReportToAlexQuestId = "Mission:555B4365";

	public const string RexFlintTalkToStanQuestId = "Mission:555B4366";

	public const string RexFlintTradeskillNanoSensorQuestId = "Mission:555B4367";

	public const string WindcallerKarrecQuestId = "Mission:55579381";

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
		PersistentMissionService persistentMissionService = new PersistentMissionService(missionRepository, definitions);
		MissionRewardCoordinator missionRewardCoordinator = new MissionRewardCoordinator(missionRepository);
		lock (SyncRoot)
		{
			repository = missionRepository;
			service = persistentMissionService;
			rewards = missionRewardCoordinator;
		}
	}

	public static string ResolveAccountKey(int characterId)
	{
		if (characterId <= 0)
		{
			return null;
		}
		DBCharacter val = ((Dao<DBCharacter, CharacterDao>)(object)Dao<DBCharacter, CharacterDao>.Instance).Get(characterId);
		return (val == null || string.IsNullOrWhiteSpace(val.Username)) ? null : val.Username.Trim();
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
