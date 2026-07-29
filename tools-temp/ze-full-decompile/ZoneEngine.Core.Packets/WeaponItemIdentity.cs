using System.Collections.Generic;
using System.Threading;
using AORebirth.Core.Items;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Packets;

internal static class WeaponItemIdentity
{
	private static readonly object SyncRoot = new object();

	private static readonly Dictionary<IItem, int> WeaponInstances = new Dictionary<IItem, int>();

	private static int nextWeaponInstance = 620756992;

	public static Identity GetOrCreate(IItem item)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		int orCreateInstance = GetOrCreateInstance(item);
		Identity result = default(Identity);
		((Identity)(ref result)).Type = (IdentityType)51018;
		((Identity)(ref result)).Instance = orCreateInstance;
		return result;
	}

	public static int GetOrCreateInstance(IItem item)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (item == null)
		{
			return 0;
		}
		Identity identity = item.Identity;
		if ((int)((Identity)(ref identity)).Type == 51018)
		{
			identity = item.Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				identity = item.Identity;
				return ((Identity)(ref identity)).Instance;
			}
		}
		lock (SyncRoot)
		{
			if (WeaponInstances.TryGetValue(item, out var value))
			{
				return value;
			}
			int num = Interlocked.Increment(ref nextWeaponInstance);
			WeaponInstances[item] = num;
			return num;
		}
	}
}
