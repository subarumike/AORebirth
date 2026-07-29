using System;
using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Playfields;

internal sealed class NascenceCoreHecklerSpawnOrchestrator
{
	private readonly Action<ICharacter> activateNpc;

	private readonly Dictionary<int, NascenceCoreHecklerSpawnDefinition> spawnBySource = new Dictionary<int, NascenceCoreHecklerSpawnDefinition>();

	private readonly Dictionary<int, int> runtimeToSource = new Dictionary<int, int>();

	private readonly Dictionary<int, DateTime> respawnDueBySource = new Dictionary<int, DateTime>();

	private readonly object sync = new object();

	private Playfield playfield;

	private Identity playfieldIdentity;

	internal NascenceCoreHecklerSpawnOrchestrator(Action<ICharacter> activateNpc)
	{
		this.activateNpc = activateNpc;
		NascenceCoreHecklerSpawnDefinition[] spawns = NascenceCoreHecklerContentProvider.GetSpawns();
		foreach (NascenceCoreHecklerSpawnDefinition nascenceCoreHecklerSpawnDefinition in spawns)
		{
			spawnBySource[nascenceCoreHecklerSpawnDefinition.SourceIdentity] = nascenceCoreHecklerSpawnDefinition;
		}
	}

	internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance != 4312)
		{
			return;
		}
		this.playfield = playfield;
		this.playfieldIdentity = playfieldIdentity;
		int num = 0;
		NascenceCoreHecklerSpawnDefinition[] spawns = NascenceCoreHecklerContentProvider.GetSpawns();
		foreach (NascenceCoreHecklerSpawnDefinition spawn in spawns)
		{
			if (TrySpawn(spawn))
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "NascenceCoreHeckler spawn complete pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + " capture=20260716-071407");
	}

	internal void NotifyDeath(ICharacter target, DateTime diedAtUtc)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			return;
		}
		Identity identity;
		int value;
		DateTime dateTime;
		lock (sync)
		{
			Dictionary<int, int> dictionary = runtimeToSource;
			identity = ((IEntity)target).Identity;
			if (!dictionary.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				return;
			}
			Dictionary<int, int> dictionary2 = runtimeToSource;
			identity = ((IEntity)target).Identity;
			dictionary2.Remove(((Identity)(ref identity)).Instance);
			dateTime = diedAtUtc.AddSeconds(600.0);
			respawnDueBySource[value] = dateTime;
		}
		identity = ((IEntity)target).Identity;
		CapturedEnemyCombatRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "NascenceCoreHeckler death scheduled respawn source=0x{0:X8} dueUtc={1:o}", value, dateTime));
	}

	internal void NotifyNpcDespawn(ICharacter target)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			return;
		}
		Identity identity;
		lock (sync)
		{
			Dictionary<int, int> dictionary = runtimeToSource;
			identity = ((IEntity)target).Identity;
			if (dictionary.TryGetValue(((Identity)(ref identity)).Instance, out var value))
			{
				Dictionary<int, int> dictionary2 = runtimeToSource;
				identity = ((IEntity)target).Identity;
				dictionary2.Remove(((Identity)(ref identity)).Instance);
				if (!respawnDueBySource.ContainsKey(value))
				{
					respawnDueBySource[value] = DateTime.UtcNow.AddSeconds(600.0);
				}
			}
		}
		identity = ((IEntity)target).Identity;
		CapturedEnemyCombatRuntimeRegistry.Remove(((Identity)(ref identity)).Instance);
	}

	internal void ProcessDue(DateTime utcNow)
	{
		if (playfield == null || ((Identity)(ref playfieldIdentity)).Instance != 4312)
		{
			return;
		}
		int[] array;
		lock (sync)
		{
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, DateTime> item in respawnDueBySource)
			{
				if (item.Value <= utcNow)
				{
					list.Add(item.Key);
				}
			}
			array = list.ToArray();
		}
		int[] array2 = array;
		foreach (int key in array2)
		{
			if (spawnBySource.TryGetValue(key, out var value) && TrySpawn(value))
			{
				lock (sync)
				{
					respawnDueBySource.Remove(key);
				}
			}
		}
	}

	private bool TrySpawn(NascenceCoreHecklerSpawnDefinition spawn)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		//IL_010a: Expected O, but got Unknown
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Expected O, but got Unknown
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || spawn == null)
		{
			return false;
		}
		lock (sync)
		{
			foreach (KeyValuePair<int, int> item in runtimeToSource)
			{
				if (item.Value == spawn.SourceIdentity)
				{
					return false;
				}
			}
		}
		NPCController nPCController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = spawn.X,
			y = spawn.Y,
			z = spawn.Z
		}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)nPCController, spawn.Level);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "NascenceCoreHeckler spawn FAILED source=0x{0:X8} name={1}", spawn.SourceIdentity, spawn.Name));
			return false;
		}
		((Dynel)val).Name = spawn.Name;
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		SetStat(val, (StatIds)54, spawn.Level);
		SetStat(val, (StatIds)1, spawn.Health);
		SetStat(val, (StatIds)27, spawn.Health);
		SetStat(val, (StatIds)156, spawn.RunSpeed);
		SetStat(val, (StatIds)359, 214982);
		SetStat(val, (StatIds)360, 100);
		SetStat(val, (StatIds)455, 171);
		SetStat(val, (StatIds)673, 31);
		SetStat(val, (StatIds)286, 106);
		SetStat(val, (StatIds)285, 320);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = spawn.X,
			y = spawn.Y,
			z = spawn.Z
		});
		CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttack("20260716-071407: Heckler of Earth fight 796C7244", 106, 320, 2.0, 3, 0, 1145132106);
		CapturedEnemyCombatRuntime.Prepare(val, nPCController, contract, out var _);
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		lock (sync)
		{
			Dictionary<int, int> dictionary = runtimeToSource;
			Identity identity = ((PooledObject)val).Identity;
			dictionary[((Identity)(ref identity)).Instance] = spawn.SourceIdentity;
		}
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "NascenceCoreHeckler spawned source=0x{0:X8} server={1} name={2} pos=({3},{4},{5})", spawn.SourceIdentity, ((PooledObject)val).Identity, spawn.Name, spawn.X, spawn.Y, spawn.Z));
		return true;
	}

	private static void SetStat(Character character, StatIds stat, int value)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected I4, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected I4, but got Unknown
		try
		{
			((Dynel)character).Stats[(int)stat].BaseValue = (uint)value;
			((Dynel)character).Stats[(int)stat].Value = value;
		}
		catch
		{
		}
	}
}
