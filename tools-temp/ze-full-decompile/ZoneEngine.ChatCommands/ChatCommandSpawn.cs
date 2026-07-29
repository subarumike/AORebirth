using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class ChatCommandSpawn : AOChatCommand
{
	private static readonly float[,] ZonePopulationOffsets = new float[8, 2]
	{
		{ 4f, 5f },
		{ -4f, 6.5f },
		{ 7f, 0f },
		{ -7f, 0f },
		{ 4f, -5f },
		{ -4f, -6.5f },
		{ 0f, 9f },
		{ 9f, 5f }
	};

	public override bool CheckCommandArguments(string[] args)
	{
		if (TryResolveCombatTestMob(args, out var _))
		{
			return true;
		}
		if (args.Length == 2 && string.Compare(args[1], "testmobs", ignoreCase: true) == 0)
		{
			return true;
		}
		if (args.Length == 2 && (string.Compare(args[1], "hints", ignoreCase: true) == 0 || string.Compare(args[1], "zone", ignoreCase: true) == 0 || string.Compare(args[1], "status", ignoreCase: true) == 0 || string.Compare(args[1], "lootstatus", ignoreCase: true) == 0 || string.Compare(args[1], "clear", ignoreCase: true) == 0))
		{
			return true;
		}
		if (args[0] == "spawnrandom")
		{
			return true;
		}
		List<Type> list = new List<Type>();
		list.Add(typeof(string));
		list.Add(typeof(uint));
		if (AOChatCommand.CheckArgumentHelper(list, args))
		{
			return true;
		}
		if (args.Length == 2)
		{
			return true;
		}
		if (args.Length > 1 && args[1].ToLower() != "list")
		{
			return false;
		}
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: /command Spawn hash level\r\nFor a list of available templates: /command spawn list [filter1,filter2...]\r\nSpawn the current combat test mob: /command spawnleet\r\nSpawn combat test mob aliases: /command spawn testmobs\r\nList supported population mobs for this playfield: /command spawn hints\r\nSpawn supported DB population mobs for this playfield: /command spawn zone\r\nShow live spawned mobs for this playfield: /command spawn status\r\nShow DB loot coverage for supported mobs: /command spawn lootstatus\r\nClear live spawned mobs/corpses for this playfield: /command spawn clear\r\nFilter will be applied to mob name", 0, 0));
	}

	public void SpawnRandomMob(ICharacter character)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Expected O, but got Unknown
		Coordinate val = ((IDynel)character).Coordinates();
		DBMobTemplate[] array = ((Dao<DBMobTemplate, MobTemplateDao>)(object)Dao<DBMobTemplate, MobTemplateDao>.Instance).GetAll((object)null).ToArray();
		Random random = new Random(Environment.TickCount);
		int num = random.Next(array.Length);
		DBMobTemplate val2 = array[num];
		NPCController nPCController = new NPCController();
		Character val3 = NonPlayerCharacterHandler.SpawnMobFromTemplate(val2.Hash, ((IEntity)((IInstancedEntity)character).Playfield).Identity, ((IDynel)character).Coordinates(), ((IDynel)character).RawHeading, (IController)(object)nPCController, -1);
		((Dynel)val3).Playfield = ((IInstancedEntity)character).Playfield;
		Playfield playfield = ((Dynel)val3).Playfield as Playfield;
		playfield?.RegisterNpcHome((ICharacter)(object)val3);
		int num2 = Math.Max(1, ((Dynel)val3).Stats[(StatIds)1].Value);
		((Dynel)val3).Stats[(StatIds)27].Value = num2;
		((Dynel)val3).Stats[(StatIds)27].BaseValue = (uint)num2;
		((Dynel)val3).DoNotDoTimers = false;
		playfield?.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val3, Identity.None);
		Vector3 val4 = new Vector3((double)val.x, (double)val.y, (double)(val.z + 5f));
		val3.AddWaypoint(val4, false);
		val4.x += (double)(10 - random.Next(20));
		val4.z -= (double)(10 - random.Next(20));
		val3.AddWaypoint(val4, false);
		val4.x += (double)(10 - random.Next(20));
		val4.z -= (double)(10 - random.Next(20));
		val3.AddWaypoint(val4, false);
		val4.x += (double)(10 - random.Next(20));
		val4.z -= (double)(10 - random.Next(20));
		val3.AddWaypoint(val4, false);
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		if (TryResolveCombatTestMob(args, out var combatTestMob))
		{
			SpawnCombatTestMob(character, combatTestMob);
			return;
		}
		if (string.Compare(args[0], "spawnrandom", ignoreCase: true) == 0)
		{
			SpawnRandomMob(character);
			return;
		}
		if (string.Compare(args[0], "spawncount", ignoreCase: true) == 0)
		{
			SpawnCount(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "hints", ignoreCase: true) == 0)
		{
			ListClientHintedMobs(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "status", ignoreCase: true) == 0)
		{
			ShowCombatTestMobStatus(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "lootstatus", ignoreCase: true) == 0)
		{
			ShowLootStatus(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "clear", ignoreCase: true) == 0)
		{
			ClearCombatTestMobs(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "zone", ignoreCase: true) == 0)
		{
			SpawnClientHintedMobs(character);
			return;
		}
		if (args.Length == 2 && string.Compare(args[1], "testmobs", ignoreCase: true) == 0)
		{
			ListCombatTestMobs(character);
			return;
		}
		if (args.Length > 1 && string.Compare(args[1], "list", ignoreCase: true) == 0)
		{
			IEnumerable<DBMobTemplate> mobTemplatesByName = Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplatesByName((args.Length > 2) ? args[2] : "%", false);
			StringBuilder stringBuilder = new StringBuilder("List of mobtemplates (Hash, Name): ");
			foreach (DBMobTemplate item in mobTemplatesByName)
			{
				stringBuilder.AppendLine($"{item.Hash},'{item.Name}'");
			}
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, stringBuilder.ToString(), 0, 0));
			return;
		}
		Character val = null;
		string text = ((args.Length > 1) ? args[1] : string.Empty);
		if (args.Length == 3)
		{
			NPCController nPCController = new NPCController();
			val = NonPlayerCharacterHandler.SpawnMobFromTemplate(args[1], ((IEntity)((IInstancedEntity)character).Playfield).Identity, ((IDynel)character).Coordinates(), ((IDynel)character).RawHeading, (IController)(object)nPCController, int.Parse(args[2]));
		}
		if (args.Length == 2)
		{
			NPCController nPCController2 = new NPCController();
			val = NonPlayerCharacterHandler.SpawnMobFromTemplate(text, ((IEntity)((IInstancedEntity)character).Playfield).Identity, ((IDynel)character).Coordinates(), ((IDynel)character).RawHeading, (IController)(object)nPCController2, -1);
		}
		if (val != null)
		{
			((Dynel)val).Playfield = ((IInstancedEntity)character).Playfield;
			Playfield playfield = ((Dynel)val).Playfield as Playfield;
			playfield?.RegisterNpcHome((ICharacter)(object)val);
			((Dynel)val).Stats[(StatIds)27].Value = ((Dynel)val).Stats[(StatIds)1].Value;
			((Dynel)val).Stats[(StatIds)27].BaseValue = (uint)((Dynel)val).Stats[(StatIds)1].Value;
			((Dynel)val).DoNotDoTimers = false;
			playfield?.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
			IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
			ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
			string name = ((Dynel)val).Name;
			Identity identity = ((PooledObject)val).Identity;
			playfield2.Publish((object)@default.CreateIM(character, $"Spawned {name} {((Identity)(ref identity)).ToString(true)}.", 0, 0));
			object[] obj = new object[8]
			{
				text,
				((Dynel)val).Name,
				null,
				null,
				null,
				null,
				null,
				null
			};
			identity = ((PooledObject)val).Identity;
			obj[2] = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			obj[3] = ((Identity)(ref identity)).ToString(true);
			obj[4] = ((Dynel)val).Stats[(StatIds)27].Value;
			obj[5] = ((Dynel)val).Stats[(StatIds)1].Value;
			obj[6] = ((Dynel)val).Stats[(StatIds)359].Value;
			obj[7] = ((Dynel)val).Stats[(StatIds)42].Value;
			LogUtil.Debug((DebugInfoDetail)512, string.Format("DB mob spawned template={0} name={1} identity={2} pf={3} hp={4}/{5} monsterData={6} catMesh={7}", obj));
		}
		else if (args.Length == 2 || args.Length == 3)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"No mob template found for hash '{text}'.", 0, 0));
			LogUtil.Debug((DebugInfoDetail)512, $"DB mob spawn failed: no template for hash {text}.");
		}
	}

	private void SpawnCount(ICharacter character)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Spawncount on this PF: " + Pool.Instance.GetAll<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity).Count((ICharacter x) => ((IDynel)x).Controller is NPCController), 0, 0));
	}

	private static List<ICharacter> GetLiveCombatTestMobs(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return Pool.Instance.GetAll<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, 50000).Where(CombatTestMobArchetype.IsCombatTestMob).ToList();
	}

	private static bool TryResolveCombatTestMob(string[] args, out CombatTestMobArchetype.Entry combatTestMob)
	{
		combatTestMob = null;
		if (string.Compare(args[0], "spawnleet", ignoreCase: true) == 0)
		{
			combatTestMob = CombatTestMobArchetype.Default;
			return true;
		}
		if (args.Length == 2)
		{
			return CombatTestMobArchetype.TryGetByAlias(args[1], out combatTestMob);
		}
		return false;
	}

	private void ListCombatTestMobs(ICharacter character)
	{
		StringBuilder stringBuilder = new StringBuilder("Combat test mobs:");
		CombatTestMobArchetype.Entry[] all = CombatTestMobArchetype.All;
		foreach (CombatTestMobArchetype.Entry entry in all)
		{
			stringBuilder.AppendLine(string.Format("{0}: /command spawn {1} ({2}, template {3})", entry.DisplayName, entry.Key, string.Join(", ", entry.Aliases), entry.TemplateHash));
		}
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, stringBuilder.ToString(), 0, 0));
	}

	private void ShowLootStatus(ICharacter character)
	{
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		DBMobTemplate[] array = ((Dao<DBMobTemplate, MobTemplateDao>)(object)Dao<DBMobTemplate, MobTemplateDao>.Instance).GetAll((object)null).ToArray();
		DBMobDroptable[] array2 = ((Dao<DBMobDroptable, MobDroptableDao>)(object)Dao<DBMobDroptable, MobDroptableDao>.Instance).GetAll((object)null).ToArray();
		CombatLootTableEntry[] array3 = CombatMobLootCatalog.BuildEntries(array, array2);
		Dictionary<string, DBMobTemplate> dictionary = array.Where((DBMobTemplate x) => !string.IsNullOrWhiteSpace(x.Hash)).GroupBy((DBMobTemplate x) => x.Hash, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, DBMobTemplate> x) => x.Key, (IGrouping<string, DBMobTemplate> x) => x.First(), StringComparer.OrdinalIgnoreCase);
		int num = array.Count(HasDropConfiguration);
		int num2 = (from x in array2
			select x.Hash into x
			where !string.IsNullOrWhiteSpace(x)
			select x).Distinct(StringComparer.OrdinalIgnoreCase).Count();
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		List<CombatTestMobArchetype.Entry> list = CombatTestMobArchetype.ForPlayfield(((Identity)(ref identity)).Instance).ToList();
		StringBuilder stringBuilder = new StringBuilder();
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		stringBuilder.AppendLine($"DB loot status for playfield {((Identity)(ref identity)).Instance}:");
		stringBuilder.AppendLine($"Mob templates: {array.Length}, configured: {num}, drop rows: {array2.Length}, distinct hashes: {num2}, parsed entries: {array3.Length}");
		if (list.Count == 0)
		{
			stringBuilder.AppendLine("Supported population mobs: none");
		}
		else
		{
			foreach (CombatTestMobArchetype.Entry item in list)
			{
				if (!dictionary.TryGetValue(item.TemplateHash, out var template))
				{
					stringBuilder.AppendLine($"- {item.RuntimeName} [{item.TemplateHash}]: missing mobtemplate");
					continue;
				}
				bool flag = HasDropConfiguration(template);
				int num3 = array3.Count((CombatLootTableEntry x) => x.Matches(template.Name, template.MonsterData, template.NPCFamily));
				stringBuilder.AppendLine(string.Format("- {0} [{1}] DB name='{2}': {3}, parsed entries={4}", item.RuntimeName, item.TemplateHash, template.Name, flag ? "configured" : "no DropHashes", num3));
			}
		}
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, stringBuilder.ToString(), 0, 0));
	}

	private static bool HasDropConfiguration(DBMobTemplate template)
	{
		return template != null && (!string.IsNullOrWhiteSpace(template.DropHashes) || !string.IsNullOrWhiteSpace(template.DropSlots) || !string.IsNullOrWhiteSpace(template.DropRates));
	}

	private void ShowCombatTestMobStatus(ICharacter character)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		List<ICharacter> liveCombatTestMobs = GetLiveCombatTestMobs(character);
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		List<CombatTestMobArchetype.Entry> list = CombatTestMobArchetype.ForPlayfield(((Identity)(ref identity)).Instance).ToList();
		StringBuilder stringBuilder = new StringBuilder();
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		stringBuilder.AppendLine($"Combat test mob status for playfield {((Identity)(ref identity)).Instance}:");
		stringBuilder.AppendLine($"Live test mobs: {liveCombatTestMobs.Count}");
		foreach (ICharacter item in liveCombatTestMobs)
		{
			string name = ((INamedEntity)item).Name;
			identity = ((IEntity)item).Identity;
			stringBuilder.AppendLine($"- {name} {((Identity)(ref identity)).ToString(true)}");
		}
		stringBuilder.AppendLine(string.Format("Supported population mobs: {0}", (list.Count == 0) ? "none" : string.Join(", ", list.Select((CombatTestMobArchetype.Entry x) => x.RuntimeName))));
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, stringBuilder.ToString(), 0, 0));
	}

	private void ClearCombatTestMobs(ICharacter character)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		List<ICharacter> liveCombatTestMobs = GetLiveCombatTestMobs(character);
		Playfield playfield = ((IInstancedEntity)character).Playfield as Playfield;
		foreach (ICharacter item in liveCombatTestMobs)
		{
			if (playfield != null)
			{
				playfield.DespawnNpcImmediately(item);
				continue;
			}
			((IInstancedEntity)character).Playfield.Despawn(((IEntity)item).Identity);
			Pool.Instance.RemoveObject<Character>((Character)item);
		}
		int num = 0;
		if (playfield != null)
		{
			num = playfield.DespawnCorpses((string name, Identity deadNpc) => CombatTestMobArchetype.IsCombatTestCorpseName(name));
		}
		IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
		ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
		object arg = liveCombatTestMobs.Count;
		object arg2 = num;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		playfield2.Publish((object)@default.CreateIM(character, $"Cleared {arg} live combat test mobs and {arg2} combat test corpses from playfield {((Identity)(ref identity)).Instance}.", 0, 0));
	}

	private void ListClientHintedMobs(ICharacter character)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		List<CombatTestMobArchetype.Entry> list = CombatTestMobArchetype.ForPlayfield(((Identity)(ref identity)).Instance).ToList();
		if (list.Count == 0)
		{
			IPlayfield playfield = ((IInstancedEntity)character).Playfield;
			ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			playfield.Publish((object)@default.CreateIM(character, $"No supported combat test mobs are mapped from client hints for playfield {((Identity)(ref identity)).Instance}.", 0, 0));
			return;
		}
		IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
		ChatTextMessageHandler default2 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		playfield2.Publish((object)default2.CreateIM(character, string.Format("Supported population mobs for playfield {0}: {1}.", ((Identity)(ref identity)).Instance, string.Join(", ", list.Select((CombatTestMobArchetype.Entry x) => x.RuntimeName + " [" + x.TemplateHash + "]"))), 0, 0));
	}

	private void SpawnClientHintedMobs(ICharacter character)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		List<CombatTestMobArchetype.Entry> list = CombatTestMobArchetype.ForPlayfield(((Identity)(ref identity)).Instance).ToList();
		if (list.Count == 0)
		{
			IPlayfield playfield = ((IInstancedEntity)character).Playfield;
			ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			playfield.Publish((object)@default.CreateIM(character, $"No supported population mobs are mapped for playfield {((Identity)(ref identity)).Instance}.", 0, 0));
			return;
		}
		List<string> list2 = new List<string>();
		for (int i = 0; i < list.Count; i++)
		{
			CombatTestMobArchetype.Entry entry = list[i];
			int num = i % ZonePopulationOffsets.GetLength(0);
			Character val = SpawnPopulationMob(character, entry, ZonePopulationOffsets[num, 0], ZonePopulationOffsets[num, 1]);
			if (val != null)
			{
				string name = ((Dynel)val).Name;
				identity = ((PooledObject)val).Identity;
				list2.Add(name + " " + ((Identity)(ref identity)).ToString(true));
			}
		}
		IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
		ChatTextMessageHandler default2 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
		object arg = list2.Count;
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		playfield2.Publish((object)default2.CreateIM(character, string.Format("Spawned {0} DB population mobs for playfield {1}: {2}.", arg, ((Identity)(ref identity)).Instance, string.Join(", ", list2)), 0, 0));
	}

	private Character SpawnPopulationMob(ICharacter character, CombatTestMobArchetype.Entry entry, float xOffset, float zOffset)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		Coordinate val = new Coordinate(((IDynel)character).Coordinates());
		val.x += xOffset;
		val.z += zOffset;
		NPCController nPCController = new NPCController();
		Character val2 = NonPlayerCharacterHandler.SpawnMobFromTemplate(entry.TemplateHash, ((IEntity)((IInstancedEntity)character).Playfield).Identity, val, ((IDynel)character).RawHeading, (IController)(object)nPCController, entry.Level);
		if (val2 == null)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Population mob spawn failed for {entry.RuntimeName} [{entry.TemplateHash}].", 0, 0));
			return null;
		}
		((Dynel)val2).Playfield = ((IInstancedEntity)character).Playfield;
		Playfield playfield = ((Dynel)val2).Playfield as Playfield;
		playfield?.RegisterNpcHome((ICharacter)(object)val2);
		CombatTestMobArchetype.Prepare((ICharacter)(object)val2, entry);
		((Dynel)val2).DoNotDoTimers = false;
		playfield?.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val2, Identity.None);
		object[] obj = new object[11]
		{
			entry.TemplateHash,
			((Dynel)val2).Name,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		Identity identity = ((PooledObject)val2).Identity;
		obj[2] = ((Identity)(ref identity)).ToString(true);
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = ((Dynel)val2).RawCoordinates.X;
		obj[5] = ((Dynel)val2).RawCoordinates.Y;
		obj[6] = ((Dynel)val2).RawCoordinates.Z;
		obj[7] = ((Dynel)val2).Stats[(StatIds)27].Value;
		obj[8] = ((Dynel)val2).Stats[(StatIds)1].Value;
		obj[9] = ((Dynel)val2).Stats[(StatIds)359].Value;
		obj[10] = ((Dynel)val2).Stats[(StatIds)42].Value;
		LogUtil.Debug((DebugInfoDetail)512, string.Format("DB population mob spawned template={0} name={1} identity={2} pf={3} pos={4:0.00},{5:0.00},{6:0.00} hp={7}/{8} monsterData={9} catMesh={10}", obj));
		return val2;
	}

	private void SpawnCombatTestMob(ICharacter character, CombatTestMobArchetype.Entry entry)
	{
		SpawnCombatTestMob(character, entry, 5f);
	}

	private Character SpawnCombatTestMob(ICharacter character, CombatTestMobArchetype.Entry entry, float zOffset)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		Character val = CombatTestMobArchetype.SpawnNear(character, entry, zOffset);
		if (val == null)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Combat test mob spawn failed for {entry.DisplayName}.", 0, 0));
			return null;
		}
		Playfield playfield = ((Dynel)val).Playfield as Playfield;
		playfield?.RegisterNpcHome((ICharacter)(object)val);
		playfield?.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
		ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
		string displayName = entry.DisplayName;
		Identity identity = ((PooledObject)val).Identity;
		playfield2.Publish((object)@default.CreateIM(character, $"Spawned {displayName} {((Identity)(ref identity)).ToString(true)}.", 0, 0));
		return val;
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "spawn", "spawnleet", "spawnrandom", "spawncount" };
	}
}
