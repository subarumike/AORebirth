using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_mesh : FunctionPrototype
{
	private const FunctionType functionId = 53004;

	public override FunctionType FunctionId => (FunctionType)53004;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	private bool FunctionExecute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length == 0)
		{
			return false;
		}
		int placement = 0;
		int num;
		int num2;
		if (TryGetPlacement(arguments, out placement))
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
		else
		{
			num = ((MessagePackObject)(ref arguments[0])).AsInt32();
			num2 = ((arguments.Length >= 2) ? ((MessagePackObject)(ref arguments[1])).AsInt32() : ((MessagePackObject)(ref arguments[0])).AsInt32());
		}
		int meshPositionFromPlacement = GetMeshPositionFromPlacement(placement);
		int layer = MeshLayers.GetLayer(placement);
		bool flag = placement >= 49;
		if (flag)
		{
			val.SocialMeshLayer.AddMesh(meshPositionFromPlacement, num2, num, layer);
		}
		else
		{
			((Dynel)val).MeshLayer.AddMesh(meshPositionFromPlacement, num2, num, layer);
			UpdateMeshStats(val, meshPositionFromPlacement, num2);
		}
		LogUtil.Debug((DebugInfoDetail)512, $"Function_mesh char={((PooledObject)val).Identity} placement={placement} position={meshPositionFromPlacement} layer={layer} social={(flag ? 1 : 0)} mesh={num2} override={num} args={FormatArguments(arguments)}");
		((Dynel)val).ChangedAppearance = true;
		return true;
	}

	private int GetMeshPositionFromPlacement(int placement)
	{
		switch (placement)
		{
		case 6:
		case 56:
			return 1;
		case 8:
		case 58:
			return 2;
		case 20:
		case 52:
			return 3;
		case 22:
		case 54:
			return 4;
		case 19:
		case 51:
			return 5;
		case 18:
		case 50:
			return 0;
		default:
			return 0;
		}
	}

	private bool TryGetPlacement(MessagePackObject[] arguments, out int placement)
	{
		placement = 0;
		if (arguments == null || arguments.Length < 2)
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
		if (arguments == null)
		{
			return "<null>";
		}
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
				if (((MessagePackObject)(ref arguments[i])).IsTypeOf<int>() == true)
				{
					stringBuilder.Append(((MessagePackObject)(ref arguments[i])).AsInt32());
				}
				else if (((MessagePackObject)(ref arguments[i])).IsTypeOf<uint>() == true)
				{
					stringBuilder.Append(((MessagePackObject)(ref arguments[i])).AsUInt32());
				}
				else if (((MessagePackObject)(ref arguments[i])).IsTypeOf<string>() == true)
				{
					stringBuilder.Append('"').Append(((MessagePackObject)(ref arguments[i])).AsString()).Append('"');
				}
				else
				{
					stringBuilder.Append(((object)(MessagePackObject)(ref arguments[i])).ToString());
				}
			}
			catch
			{
				stringBuilder.Append(((object)(MessagePackObject)(ref arguments[i])).ToString());
			}
		}
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}

	private void UpdateMeshStats(Character character, int position, int meshId)
	{
		switch (position)
		{
		case 1:
			((Dynel)character).Stats[(StatIds)1006].Value = meshId;
			break;
		case 2:
			((Dynel)character).Stats[(StatIds)1007].Value = meshId;
			break;
		case 3:
			((Dynel)character).Stats[(StatIds)1004].Value = meshId;
			break;
		case 4:
			((Dynel)character).Stats[(StatIds)1005].Value = meshId;
			break;
		case 5:
			((Dynel)character).Stats[(StatIds)38].Value = meshId;
			break;
		}
	}
}
