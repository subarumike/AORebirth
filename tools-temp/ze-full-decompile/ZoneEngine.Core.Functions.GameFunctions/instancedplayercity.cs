using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class instancedplayercity : FunctionPrototype
{
	private const int CapturedPrivateCityPlayfieldId = 1067112;

	private const int CapturedPrivateCityOrganizationInstance = 1370122;

	public override FunctionType FunctionId => (FunctionType)53233;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = (ICharacter)(object)((self is ICharacter) ? self : null);
		Dynel val2 = (Dynel)(object)((self is Dynel) ? self : null);
		if (val == null || val2 == null || ((IInstancedEntity)val).Playfield == null)
		{
			return false;
		}
		int value = ((IStats)val).Stats[(StatIds)5].Value;
		int num = ResolvePrivateCityPlayfieldId(value);
		Identity val3;
		if (num <= 0)
		{
			val3 = ((IEntity)val).Identity;
			LogUtil.Debug((DebugInfoDetail)64, $"InstancedPlayerCity skipped character={((Identity)(ref val3)).ToString(true)} org={value} reason=no_city");
			return false;
		}
		Coordinate val4 = new Coordinate(211.55756f, 3.775f, 186.51588f);
		Quaternion val5 = new Quaternion(0.0, -0.9575281143188477, 0.0, 0.2883400321006775);
		val.StopMovement();
		IPlayfield playfield = ((IInstancedEntity)val).Playfield;
		val3 = default(Identity);
		((Identity)(ref val3)).Type = (IdentityType)51101;
		((Identity)(ref val3)).Instance = num;
		playfield.Teleport(val2, val4, (IQuaternion)(object)val5, val3);
		object[] array = new object[6];
		val3 = ((IEntity)val).Identity;
		array[0] = ((Identity)(ref val3)).ToString(true);
		array[1] = value;
		array[2] = num;
		array[3] = val4.x;
		array[4] = val4.y;
		array[5] = val4.z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format("InstancedPlayerCity teleport character={0} org={1} destPf={2} dest=({3:F3},{4:F3},{5:F3}) evidence=live_capture_20260622-093540", array));
		return true;
	}

	private static int ResolvePrivateCityPlayfieldId(int organizationInstance)
	{
		if (organizationInstance <= 0)
		{
			return 0;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(organizationInstance);
			if (val != null && val.CityId > 0)
			{
				return val.CityId;
			}
		}
		catch
		{
		}
		return (organizationInstance == 1370122) ? 1067112 : 0;
	}
}
