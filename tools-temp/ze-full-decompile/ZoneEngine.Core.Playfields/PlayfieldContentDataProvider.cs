using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Items;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldContentDataProvider
{
	private readonly Func<Identity, bool> isPrivateCityPlayfieldCandidate;

	internal PlayfieldContentDataProvider(Func<Identity, bool> isPrivateCityPlayfieldCandidate)
	{
		if (isPrivateCityPlayfieldCandidate == null)
		{
			throw new ArgumentNullException("isPrivateCityPlayfieldCandidate");
		}
		this.isPrivateCityPlayfieldCandidate = isPrivateCityPlayfieldCandidate;
	}

	internal List<StatelData> ResolveStatels(Identity playfieldIdentity)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (PlayfieldLoader.PFData.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value))
		{
			return value.Statels;
		}
		if (isPrivateCityPlayfieldCandidate(playfieldIdentity))
		{
			LogUtil.Debug((DebugInfoDetail)64, string.Format(CultureInfo.InvariantCulture, "Dynamic private city instance created without PFData statels instance={0} evidence=live_capture_20260622-101935", playfieldIdentity));
			return new List<StatelData>();
		}
		if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref playfieldIdentity)).Instance))
		{
			LogUtil.Debug((DebugInfoDetail)64, string.Format(CultureInfo.InvariantCulture, "Dynamic mission instance created without PFData statels instance={0} evidence=live_capture_20260718-062936", ((Identity)(ref playfieldIdentity)).Instance));
			return new List<StatelData>();
		}
		if (((Identity)(ref playfieldIdentity)).Instance == 7001)
		{
			LogUtil.Debug((DebugInfoDetail)64, "HoloDeck PF 7001 created without PFData statels evidence=20260719-155043");
			return new List<StatelData>();
		}
		return PlayfieldLoader.PFData[((Identity)(ref playfieldIdentity)).Instance].Statels;
	}

	internal bool TryResolveVendorStatels(Identity playfieldIdentity, IEnumerable<StatelData> statels, out StatelData[] vendorStatels)
	{
		if (!PlayfieldLoader.PFData.ContainsKey(((Identity)(ref playfieldIdentity)).Instance))
		{
			vendorStatels = null;
			return false;
		}
		vendorStatels = ResolveVendorStatels(statels);
		return true;
	}

	internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)
	{
		if (statels == null)
		{
			return (StatelData[])(object)new StatelData[0];
		}
		return statels.Where(HandlesCollisionEvent).ToArray();
	}

	internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		List<PlayfieldStaticDynelDefinition> fromDb = new List<PlayfieldStaticDynelDefinition>();
		IEnumerable<DBStaticDynel> dynels = ((Dao<DBStaticDynel, StaticDynelDao>)(object)Dao<DBStaticDynel, StaticDynelDao>.Instance).GetWhere((object)new
		{
			Playfield = ((Identity)(ref playfieldIdentity)).Instance
		}, (IDbConnection)null, (IDbTransaction)null);
		int dbCount = 0;
		int skippedNoTemplateStat = 0;
		int skippedMissingItem = 0;
		int yielded = 0;
		foreach (DBStaticDynel staticDynel in dynels)
		{
			dbCount++;
			List<GameTuple<CharacterStat, uint>> stats = MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(staticDynel.stats.ToArray());
			if (!stats.Any((GameTuple<CharacterStat, uint> x) => (int)x.Value1 == 702))
			{
				skippedNoTemplateStat++;
				continue;
			}
			int templateId = (int)stats.First((GameTuple<CharacterStat, uint> x) => (int)x.Value1 == 702).Value2;
			if (!ItemLoader.ItemList.TryGetValue(templateId, out var template))
			{
				skippedMissingItem++;
				continue;
			}
			yielded++;
			Identity identity = default(Identity);
			((Identity)(ref identity)).Type = (IdentityType)staticDynel.Type;
			((Identity)(ref identity)).Instance = staticDynel.Instance;
			PlayfieldStaticDynelDefinition definition = new PlayfieldStaticDynelDefinition(identity, template, stats, new Coordinate(staticDynel.X, staticDynel.Y, staticDynel.Z), new Quaternion
			{
				X = staticDynel.HeadingX,
				Y = staticDynel.HeadingY,
				Z = staticDynel.HeadingZ,
				W = staticDynel.HeadingW
			});
			fromDb.Add(definition);
			yield return definition;
			template = null;
		}
		foreach (PlayfieldStaticDynelDefinition captureProp in AreteLandingQuestPropDefinitions.ResolveMissingProps(playfieldIdentity, fromDb))
		{
			yielded++;
			yield return captureProp;
		}
		LogUtil.Debug((DebugInfoDetail)8, "ResolveStaticDynels pf=" + ((Identity)(ref playfieldIdentity)).Instance + " db=" + dbCount + " yielded=" + yielded + " skipNoTplStat=" + skippedNoTemplateStat + " skipMissingItem=" + skippedMissingItem);
	}

	private StatelData[] ResolveVendorStatels(IEnumerable<StatelData> statels)
	{
		if (statels == null)
		{
			return (StatelData[])(object)new StatelData[0];
		}
		return statels.Where(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			Identity identity = x.Identity;
			return (int)((Identity)(ref identity)).Type == 51035;
		}).ToArray();
	}

	private static bool HandlesCollisionEvent(StatelData statel)
	{
		return statel != null && statel.Events.Any((Event x) => (int)x.EventType == 22 || (int)x.EventType == 16 || (int)x.EventType == 3);
	}
}
