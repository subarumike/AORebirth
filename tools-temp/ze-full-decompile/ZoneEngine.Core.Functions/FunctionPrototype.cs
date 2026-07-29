using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions;

public abstract class FunctionPrototype
{
	public abstract FunctionType FunctionId { get; }

	public string FunctionName
	{
		get
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			FunctionType functionId = FunctionId;
			return ((object)(FunctionType)(ref functionId)).ToString();
		}
	}

	public int FunctionNumber => (int)FunctionId;

	public abstract bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments);

	public virtual string ReturnName()
	{
		return FunctionName;
	}

	public virtual int ReturnNumber()
	{
		return FunctionNumber;
	}
}
