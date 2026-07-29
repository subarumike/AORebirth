using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class reducenanostrainduration : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53177;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 1)
		{
			return false;
		}
		int strain = ((MessagePackObject)(ref arguments[0])).AsInt32();
		ActiveNanoRuntimeService.Default.RemoveActiveNanoInStrain((ICharacter)(object)val, strain, notifyClient: true);
		Identity identity = ((PooledObject)val).Identity;
		LogUtil.Debug((DebugInfoDetail)256, "ReduceNanoStrainDuration char=" + ((object)(Identity)(ref identity)).ToString() + " strain=" + strain);
		return true;
	}
}
