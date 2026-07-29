using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class backmesh : FunctionPrototype
{
	private const FunctionType functionId = 53037;

	public override FunctionType FunctionId => (FunctionType)53037;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((Self is Character) ? Self : null);
		if (val == null || Arguments == null || Arguments.Length == 0)
		{
			return false;
		}
		int num;
		int num2;
		if (TryGetPlacement(Arguments, out var placement))
		{
			if (Arguments.Length >= 3)
			{
				num = ((MessagePackObject)(ref Arguments[0])).AsInt32();
				num2 = ((MessagePackObject)(ref Arguments[1])).AsInt32();
			}
			else
			{
				num = 0;
				num2 = ((MessagePackObject)(ref Arguments[0])).AsInt32();
			}
		}
		else if (Arguments.Length >= 2)
		{
			placement = 19;
			num = ((MessagePackObject)(ref Arguments[0])).AsInt32();
			num2 = ((MessagePackObject)(ref Arguments[1])).AsInt32();
		}
		else
		{
			placement = 19;
			num = 0;
			num2 = ((MessagePackObject)(ref Arguments[0])).AsInt32();
		}
		bool flag = placement == 51;
		int layer = MeshLayers.GetLayer(placement);
		if (flag)
		{
			val.SocialMeshLayer.AddMesh(5, num2, num, layer);
		}
		else
		{
			if (placement == 19)
			{
				((Dynel)val).Stats[(StatIds)38].Value = num2;
			}
			((Dynel)val).MeshLayer.AddMesh(5, num2, num, layer);
		}
		LogUtil.Debug((DebugInfoDetail)512, $"Function_backmesh char={((PooledObject)val).Identity} placement={placement} position=5 layer={layer} social={(flag ? 1 : 0)} mesh={num2} override={num} args={FormatArguments(Arguments)}");
		((Dynel)val).ChangedAppearance = true;
		return true;
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

	private string FormatArguments(MessagePackObject[] arguments)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[");
		for (int i = 0; i < arguments.Length; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(", ");
			}
			try
			{
				stringBuilder.Append(((MessagePackObject)(ref arguments[i])).AsInt32());
			}
			catch
			{
				stringBuilder.Append(((object)(MessagePackObject)(ref arguments[i])).ToString());
			}
		}
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}
}
