using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Statels;
using AORebirth.Database.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldObjectMaterializationRuntimeService
{
	internal delegate bool TryResolveVendorStatelsDelegate(Identity playfieldIdentity, IEnumerable<StatelData> statels, out StatelData[] vendorStatels);

	internal void MaterializeStartupObjects(Identity playfieldIdentity, IEnumerable<StatelData> statels, Func<Identity, IEnumerable<DBMobSpawn>> loadMobSpawns, Func<DBMobSpawn, bool> shouldSuppressDbMobSpawn, Func<DBMobSpawn, IEnumerable<DBMobSpawnStat>> loadMobSpawnStats, Func<DBMobSpawn, DBMobSpawnStat[], ICharacter> instantiateDbMobSpawn, Action<ICharacter> activateNpc, Action<DBMobSpawn, ICharacter> attachMobSpawnScript, Action<Identity> registerContent, TryResolveVendorStatelsDelegate tryResolveVendorStatels, Action<StatelData[]> spawnVendors, Func<Identity, IEnumerable<PlayfieldStaticDynelDefinition>> resolveStaticDynels, Func<PlayfieldStaticDynelDefinition, IEntity> instantiateStaticDynel, Action<IEntity> registerDynel, Action refreshDynelRegistry)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		Require(loadMobSpawns, "loadMobSpawns");
		Require(shouldSuppressDbMobSpawn, "shouldSuppressDbMobSpawn");
		Require(loadMobSpawnStats, "loadMobSpawnStats");
		Require(instantiateDbMobSpawn, "instantiateDbMobSpawn");
		Require(activateNpc, "activateNpc");
		Require(attachMobSpawnScript, "attachMobSpawnScript");
		Require(registerContent, "registerContent");
		Require(tryResolveVendorStatels, "tryResolveVendorStatels");
		Require(spawnVendors, "spawnVendors");
		Require(resolveStaticDynels, "resolveStaticDynels");
		Require(instantiateStaticDynel, "instantiateStaticDynel");
		Require(registerDynel, "registerDynel");
		Require(refreshDynelRegistry, "refreshDynelRegistry");
		MaterializeDbMobSpawns(playfieldIdentity, loadMobSpawns, shouldSuppressDbMobSpawn, loadMobSpawnStats, instantiateDbMobSpawn, activateNpc, attachMobSpawnScript);
		registerContent(playfieldIdentity);
		MaterializeVendors(playfieldIdentity, statels, tryResolveVendorStatels, spawnVendors);
		MaterializeStaticDynels(playfieldIdentity, resolveStaticDynels, instantiateStaticDynel, registerDynel);
		refreshDynelRegistry();
	}

	private void MaterializeDbMobSpawns(Identity playfieldIdentity, Func<Identity, IEnumerable<DBMobSpawn>> loadMobSpawns, Func<DBMobSpawn, bool> shouldSuppressDbMobSpawn, Func<DBMobSpawn, IEnumerable<DBMobSpawnStat>> loadMobSpawnStats, Func<DBMobSpawn, DBMobSpawnStat[], ICharacter> instantiateDbMobSpawn, Action<ICharacter> activateNpc, Action<DBMobSpawn, ICharacter> attachMobSpawnScript)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		foreach (DBMobSpawn item in loadMobSpawns(playfieldIdentity))
		{
			if (!shouldSuppressDbMobSpawn(item))
			{
				ICharacter val = instantiateDbMobSpawn(item, loadMobSpawnStats(item).ToArray());
				activateNpc(val);
				attachMobSpawnScript(item, val);
			}
		}
	}

	private void MaterializeVendors(Identity playfieldIdentity, IEnumerable<StatelData> statels, TryResolveVendorStatelsDelegate tryResolveVendorStatels, Action<StatelData[]> spawnVendors)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		if (tryResolveVendorStatels(playfieldIdentity, statels, out var vendorStatels))
		{
			spawnVendors(vendorStatels);
		}
	}

	private void MaterializeStaticDynels(Identity playfieldIdentity, Func<Identity, IEnumerable<PlayfieldStaticDynelDefinition>> resolveStaticDynels, Func<PlayfieldStaticDynelDefinition, IEntity> instantiateStaticDynel, Action<IEntity> registerDynel)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		foreach (PlayfieldStaticDynelDefinition item in resolveStaticDynels(playfieldIdentity))
		{
			registerDynel(instantiateStaticDynel(item));
			num++;
		}
		LogUtil.Debug((DebugInfoDetail)8, "MaterializeStaticDynels pf=" + ((Identity)(ref playfieldIdentity)).Instance + " loaded=" + num);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
