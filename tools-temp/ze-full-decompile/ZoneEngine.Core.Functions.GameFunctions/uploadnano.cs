using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Functions.GameFunctions;

public class uploadnano : FunctionPrototype
{
	private FunctionType functionId = (FunctionType)53019;

	public override FunctionType FunctionId => functionId;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		UploadedNano val = new UploadedNano
		{
			NanoId = ((MessagePackObject)(ref arguments[0])).AsInt32()
		};
		((Character)self).UploadedNanos.Add((IUploadedNanos)(object)val);
		UploadedNanosDao instance = Dao<DBUploadedNano, UploadedNanosDao>.Instance;
		Identity identity = ((PooledObject)(Character)self).Identity;
		instance.WriteNano(((Identity)(ref identity)).Instance, (IUploadedNanos)(object)val);
		if (((Dynel)(Character)self).Controller.Client != null)
		{
			CharacterActionMessage val2 = new CharacterActionMessage
			{
				Identity = ((IEntity)self).Identity,
				Action = (CharacterActionType)204,
				Target = ((IEntity)self).Identity,
				Parameter1 = 53019,
				Parameter2 = val.NanoId,
				Unknown = 0
			};
			((Dynel)(Character)self).Controller.Client.SendCompressed((MessageBody)(object)val2);
		}
		return true;
	}
}
