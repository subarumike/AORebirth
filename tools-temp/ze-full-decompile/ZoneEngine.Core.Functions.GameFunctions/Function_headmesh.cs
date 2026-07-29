using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_headmesh : FunctionPrototype
{
	private const FunctionType functionId = 53035;

	public override FunctionType FunctionId => (FunctionType)53035;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if (Arguments.Length == 2)
		{
			((Dynel)(Character)Self).Stats[(StatIds)64].Value = ((MessagePackObject)(ref Arguments[1])).AsInt32();
			((Dynel)(Character)Self).MeshLayer.AddMesh(0, ((MessagePackObject)(ref Arguments[1])).AsInt32(), ((MessagePackObject)(ref Arguments[0])).AsInt32(), 4);
		}
		else
		{
			int num = (int)Arguments[Arguments.Length - 1];
			if (num >= 49)
			{
				((Character)Self).SocialMeshLayer.AddMesh(0, ((MessagePackObject)(ref Arguments[1])).AsInt32(), ((MessagePackObject)(ref Arguments[0])).AsInt32(), 4);
			}
			else
			{
				((Dynel)(Character)Self).Stats[(StatIds)64].Value = ((MessagePackObject)(ref Arguments[0])).AsInt32();
				((Dynel)(Character)Self).MeshLayer.AddMesh(0, ((MessagePackObject)(ref Arguments[1])).AsInt32(), ((MessagePackObject)(ref Arguments[0])).AsInt32(), 4);
			}
		}
		((Dynel)(Character)Self).ChangedAppearance = true;
		return true;
	}
}
