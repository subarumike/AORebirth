using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core;

public static class CombatTestMobArchetype
{
	public class Entry
	{
		public string Key { get; private set; }

		public string[] Aliases { get; private set; }

		public string TemplateHash { get; private set; }

		public string DisplayName { get; private set; }

		public string RuntimeName
		{
			get
			{
				if (DisplayName.StartsWith("Codex Test ", StringComparison.OrdinalIgnoreCase))
				{
					return DisplayName.Substring("Codex Test ".Length);
				}
				return DisplayName;
			}
		}

		public int Level { get; private set; }

		public int Health { get; private set; }

		public int MonsterData { get; private set; }

		public int CorpseCatMesh { get; private set; }

		public int MonsterScale { get; private set; }

		public int NpcFamily { get; private set; }

		public int Breed { get; private set; }

		public int Sex { get; private set; }

		public int[] ClientHintPlayfieldIds { get; private set; }

		public int VisualFlags { get; private set; }

		public int RunSpeedBase { get; private set; }

		public int DeathAnimationKey { get; private set; }

		public int XpReward { get; private set; }

		public NpcAiProfile AiProfile { get; private set; }

		public Entry(string key, string[] aliases, string templateHash, string displayName, int level, int health, int monsterData, int corpseCatMesh, int monsterScale, int npcFamily, int breed, int sex, int[] clientHintPlayfieldIds, NpcAiProfile aiProfile)
			: this(key, aliases, templateHash, displayName, level, health, monsterData, corpseCatMesh, monsterScale, npcFamily, breed, sex, clientHintPlayfieldIds, 31, 400, 503, 0, aiProfile)
		{
		}

		public Entry(string key, string[] aliases, string templateHash, string displayName, int level, int health, int monsterData, int corpseCatMesh, int monsterScale, int npcFamily, int breed, int sex, int[] clientHintPlayfieldIds, int visualFlags, int runSpeedBase, int deathAnimationKey, int xpReward, NpcAiProfile aiProfile)
		{
			Key = key;
			Aliases = aliases;
			TemplateHash = templateHash;
			DisplayName = displayName;
			Level = level;
			Health = health;
			MonsterData = monsterData;
			CorpseCatMesh = corpseCatMesh;
			MonsterScale = monsterScale;
			NpcFamily = npcFamily;
			Breed = breed;
			Sex = sex;
			ClientHintPlayfieldIds = clientHintPlayfieldIds ?? new int[0];
			VisualFlags = visualFlags;
			RunSpeedBase = runSpeedBase;
			DeathAnimationKey = deathAnimationKey;
			XpReward = xpReward;
			AiProfile = aiProfile;
		}

		public bool MatchesAlias(string alias)
		{
			if (string.Equals(Key, alias, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			string[] aliases = Aliases;
			foreach (string a in aliases)
			{
				if (string.Equals(a, alias, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		public bool IsHintedForPlayfield(int playfieldId)
		{
			int[] clientHintPlayfieldIds = ClientHintPlayfieldIds;
			foreach (int num in clientHintPlayfieldIds)
			{
				if (num == playfieldId)
				{
					return true;
				}
			}
			return false;
		}
	}

	public const string TemplateHash = "A004";

	public const string DisplayName = "Codex Test Beach Leet";

	public const int MonsterData = 17655;

	public const int CorpseCatMesh = 15222;

	public static readonly Entry BeachLeet = new Entry("beachleet", new string[3] { "beachleet", "leet", "codexleet" }, "A004", "Codex Test Beach Leet", 1, 12, 17655, 15222, 90, 36, 6, 5, new int[10] { 540, 545, 565, 585, 600, 655, 716, 730, 800, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry MalfunctioningCleaningRobot = new Entry("robot", new string[4] { "robot", "cleaningrobot", "malfunctioningrobot", "mcr" }, "A004", "Malfunctioning Cleaning Robot", 1, 12, 297023, 1234567890, 200, 1019, 6, 5, new int[1] { 1044525 }, 31, 6, 503, 260, NpcAiProfile.Passive);

	public static readonly Entry IslandReet = new Entry("islandreet", new string[2] { "islandreet", "reet" }, "A001", "Codex Test Island Reet", 1, 12, 30365, 25733, 90, 53, 6, 5, new int[1] { 4582 }, NpcAiProfile.Passive);

	public static readonly Entry ShoreSnake = new Entry("shoresnake", new string[2] { "shoresnake", "snake" }, "A003", "Codex Test Shore Snake", 1, 25, 30252, 23353, 36, 27, 6, 5, new int[8] { 565, 585, 590, 605, 655, 790, 791, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry StowawayRollerrat = new Entry("rollerrat", new string[3] { "rollerrat", "stowawayrollerrat", "rat" }, "A012", "Codex Test Stowaway Rollerrat", 4, 58, 17687, 15272, 65, 55, 6, 5, new int[3] { 551, 585, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry DuneFlea = new Entry("duneflea", new string[2] { "duneflea", "flea" }, "A096", "Codex Test Dune Flea", 4, 58, 17657, 15231, 93, 25, 6, 5, new int[3] { 565, 585, 716 }, NpcAiProfile.Passive);

	public static readonly Entry SurfLizard = new Entry("surflizard", new string[2] { "surflizard", "lizard" }, "A000", "Codex Test Surf Lizard", 1, 25, 22794, 22773, 90, 37, 6, 5, new int[4] { 565, 600, 605, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry CliffMalle = new Entry("cliffmalle", new string[2] { "cliffmalle", "malle" }, "A035", "Codex Test Cliff Malle", 2, 70, 17660, 15239, 69, 38, 6, 5, new int[2] { 716, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry ReefSalamander = new Entry("reefsalamander", new string[2] { "reefsalamander", "salamander" }, "A034", "Codex Test Reef Salamander", 3, 70, 30354, 23344, 92, 57, 6, 5, new int[2] { 565, 4582 }, NpcAiProfile.Passive);

	public static readonly Entry AlienSpiderZix = new Entry("alienspider", new string[3] { "alienspider", "spider", "zix" }, "A026", "Codex Test Alien Spider - Zix", 7, 34, 247728, 31774, 119, 220, 6, 4, new int[7] { 346, 551, 590, 600, 655, 4542, 4544 }, NpcAiProfile.Passive);

	public static readonly Entry[] All = new Entry[10] { BeachLeet, MalfunctioningCleaningRobot, IslandReet, ShoreSnake, StowawayRollerrat, DuneFlea, SurfLizard, CliffMalle, ReefSalamander, AlienSpiderZix };

	private const int LiveObservedDeathActionKey = 503;

	private const int MissingVisualId = 1234567890;

	public static Entry Default => BeachLeet;

	public static Entry DefaultForPlayfield(int playfieldId)
	{
		using (IEnumerator<Entry> enumerator = ForPlayfield(playfieldId).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current;
			}
		}
		return Default;
	}

	public static bool TryGetByAlias(string alias, out Entry entry)
	{
		entry = null;
		if (string.IsNullOrWhiteSpace(alias))
		{
			return false;
		}
		Entry[] all = All;
		foreach (Entry entry2 in all)
		{
			if (entry2.MatchesAlias(alias))
			{
				entry = entry2;
				return true;
			}
		}
		return false;
	}

	public static bool TryGetByName(string name, out Entry entry)
	{
		entry = null;
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		Entry[] all = All;
		foreach (Entry entry2 in all)
		{
			if (string.Equals(entry2.DisplayName, name, StringComparison.OrdinalIgnoreCase))
			{
				entry = entry2;
				return true;
			}
			if (string.Equals(entry2.RuntimeName, name, StringComparison.OrdinalIgnoreCase))
			{
				entry = entry2;
				return true;
			}
		}
		return false;
	}

	public static bool IsCombatTestCorpseName(string corpseName)
	{
		if (string.IsNullOrWhiteSpace(corpseName))
		{
			return false;
		}
		if (!corpseName.StartsWith("Remains of ", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Entry entry;
		return TryGetByName(corpseName.Substring("Remains of ".Length), out entry);
	}

	public static IEnumerable<Entry> ForPlayfield(int playfieldId)
	{
		Entry[] all = All;
		foreach (Entry entry in all)
		{
			if (entry.IsHintedForPlayfield(playfieldId))
			{
				yield return entry;
			}
		}
	}

	public static bool IsCombatTestMob(ICharacter character)
	{
		Entry entry;
		return character != null && ((IDynel)character).Controller is NPCController && TryGetByName(((INamedEntity)character).Name, out entry);
	}

	public static IEnumerable<KeyValuePair<int, int>> CorpseVisualMappings()
	{
		Entry[] all = All;
		foreach (Entry entry in all)
		{
			if (CombatCorpseVisuals.IsUsableVisualId(entry.CorpseCatMesh))
			{
				yield return new KeyValuePair<int, int>(entry.MonsterData, entry.CorpseCatMesh);
			}
		}
	}

	public static Character SpawnNear(ICharacter character, float zOffset)
	{
		return SpawnNear(character, Default, zOffset);
	}

	public static Character SpawnNear(ICharacter character, Entry entry, float zOffset)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		Coordinate val = new Coordinate(((IDynel)character).Coordinates());
		val.z += zOffset;
		NPCController nPCController = new NPCController();
		Character val2 = NonPlayerCharacterHandler.SpawnMobFromTemplate(entry.TemplateHash, ((IEntity)((IInstancedEntity)character).Playfield).Identity, val, ((IDynel)character).Heading, (IController)(object)nPCController, entry.Level);
		if (val2 == null)
		{
			return null;
		}
		((Dynel)val2).Name = entry.DisplayName;
		((Dynel)val2).Playfield = ((IInstancedEntity)character).Playfield;
		Prepare((ICharacter)(object)val2, entry);
		((Dynel)val2).DoNotDoTimers = false;
		return val2;
	}

	public static void Prepare(ICharacter mobCharacter)
	{
		if (!TryGetByName(((INamedEntity)mobCharacter).Name, out var entry))
		{
			entry = Default;
		}
		Prepare(mobCharacter, entry);
	}

	public static void Prepare(ICharacter mobCharacter, Entry entry)
	{
		SetMobStat(mobCharacter, (StatIds)359, entry.MonsterData);
		SetMobStat(mobCharacter, (StatIds)42, entry.CorpseCatMesh);
		SetMobStat(mobCharacter, (StatIds)404, entry.CorpseCatMesh);
		SetMobStat(mobCharacter, (StatIds)360, entry.MonsterScale);
		SetMobStat(mobCharacter, (StatIds)673, entry.VisualFlags);
		SetMobStat(mobCharacter, (StatIds)33, 3);
		SetMobStat(mobCharacter, (StatIds)47, 1);
		SetMobStat(mobCharacter, (StatIds)173, 3);
		SetMobStat(mobCharacter, (StatIds)174, 3);
		SetMobStat(mobCharacter, (StatIds)156, entry.RunSpeedBase);
		SetMobStat(mobCharacter, (StatIds)4, entry.Breed);
		SetMobStat(mobCharacter, (StatIds)59, entry.Sex);
		SetMobStat(mobCharacter, (StatIds)89, 1);
		SetMobStat(mobCharacter, (StatIds)455, entry.NpcFamily);
		if (entry == AlienSpiderZix)
		{
			SetMobStat(mobCharacter, (StatIds)0, 268980737);
		}
		SetMobStat(mobCharacter, (StatIds)99, entry.DeathAnimationKey);
		SetMobStat(mobCharacter, (StatIds)417, entry.DeathAnimationKey);
		SetMobStat(mobCharacter, (StatIds)387, entry.DeathAnimationKey);
		SetMobStat(mobCharacter, (StatIds)1, entry.Health);
		SetMobStat(mobCharacter, (StatIds)27, entry.Health);
		SetMobStat(mobCharacter, (StatIds)343, 0);
		SetMobStat(mobCharacter, (StatIds)342, 600);
		SetMobStat(mobCharacter, (StatIds)286, 1);
		SetMobStat(mobCharacter, (StatIds)285, 3);
		SetMobStat(mobCharacter, (StatIds)284, 0);
		SetMobStat(mobCharacter, (StatIds)292, 0);
		SetMobStat(mobCharacter, (StatIds)339, 91);
		SetMobStat(mobCharacter, (StatIds)436, 91);
		SetMobStat(mobCharacter, (StatIds)1003, 0);
		SetMobStat(mobCharacter, (StatIds)274, 0);
		if (entry.XpReward > 0)
		{
			SetMobStat(mobCharacter, (StatIds)52, entry.XpReward);
		}
		if (((IDynel)mobCharacter).Controller is NPCController nPCController)
		{
			nPCController.AiProfile = entry.AiProfile;
		}
	}

	private static void SetMobStat(ICharacter mobCharacter, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		((IStats)mobCharacter).Stats[stat].Value = value;
		((IStats)mobCharacter).Stats[stat].BaseValue = (uint)value;
	}
}
