using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class shouldermesh : FunctionPrototype
{
	private const FunctionType functionId = 53038;

	public override FunctionType FunctionId => (FunctionType)53038;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		if (target == null)
		{
			return FunctionExecute(self, arguments);
		}
		lock (target)
		{
			return FunctionExecute(self, arguments);
		}
	}

	private bool FunctionExecute(INamedEntity self, MessagePackObject[] arguments)
	{
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length == 0)
		{
			return false;
		}
		int num;
		int num2;
		if (TryGetPlacement(arguments, out var placement))
		{
			if (arguments.Length >= 3)
			{
				num = ((MessagePackObject)(ref arguments[0])).AsInt32();
				num2 = ((MessagePackObject)(ref arguments[1])).AsInt32();
			}
			else
			{
				num = 0;
				num2 = ((MessagePackObject)(ref arguments[0])).AsInt32();
			}
		}
		else if (arguments.Length >= 2)
		{
			placement = 20;
			num = ((MessagePackObject)(ref arguments[0])).AsInt32();
			num2 = ((MessagePackObject)(ref arguments[1])).AsInt32();
		}
		else
		{
			placement = 20;
			num = 0;
			num2 = ((MessagePackObject)(ref arguments[0])).AsInt32();
		}
		int meshPositionFromPlacement = GetMeshPositionFromPlacement(placement);
		int layer = MeshLayers.GetLayer(placement);
		if (placement >= 49)
		{
			val.SocialMeshLayer.AddMesh(meshPositionFromPlacement, num2, num, layer);
		}
		else
		{
			switch (meshPositionFromPlacement)
			{
			case 3:
				((Dynel)val).Stats[(StatIds)1004].Value = num2;
				break;
			case 4:
				((Dynel)val).Stats[(StatIds)1005].Value = num2;
				break;
			}
			((Dynel)val).MeshLayer.AddMesh(meshPositionFromPlacement, num2, num, layer);
		}
		((Dynel)val).ChangedAppearance = true;
		return true;
	}

	private int GetMeshPositionFromPlacement(int placement)
	{
		if (placement == 22 || placement == 54)
		{
			return 4;
		}
		return 3;
	}

	private bool TryGetPlacement(MessagePackObject[] arguments, out int placement)
	{
		placement = 0;
		if (arguments.Length < 2)
		{
			return false;
		}
		int num;
		try
		{
			num = ((MessagePackObject)(ref arguments[arguments.Length - 1])).AsInt32();
		}
		catch
		{
			return false;
		}
		if (num >= 1 && num <= 100)
		{
			placement = num;
			return true;
		}
		return false;
	}
}
