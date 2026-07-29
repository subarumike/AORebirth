using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class tauntnpc : FunctionPrototype
{
	private const int EngageDamage = 1;

	public override FunctionType FunctionId => (FunctionType)53117;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		Character val2 = (Character)(object)((target is Character) ? target : null);
		if (val == null || val2 == null || val == val2)
		{
			return false;
		}
		int num = 0;
		if (arguments != null && arguments.Length >= 1)
		{
			num = ((MessagePackObject)(ref arguments[0])).AsInt32();
		}
		MessagePackObject[] arguments2 = (MessagePackObject[])(object)new MessagePackObject[4]
		{
			MessagePackObject.op_Implicit(27),
			MessagePackObject.op_Implicit(-1),
			MessagePackObject.op_Implicit(-1),
			MessagePackObject.op_Implicit(0)
		};
		new hit().Execute((INamedEntity)(object)val, (IEntity)(object)val, (IInstancedEntity)(object)val2, arguments2);
		if (((Dynel)val2).Playfield is Playfield playfield)
		{
			playfield.ForceNpcTauntAggro((ICharacter)(object)val, (ICharacter)(object)val2);
		}
		LogUtil.Debug((DebugInfoDetail)256, $"TauntNpc caster={((PooledObject)val).Identity} target={((PooledObject)val2).Identity} tauntAmount={num} engageDmg={1}");
		return true;
	}
}
