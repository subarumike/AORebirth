using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_applytexture : FunctionPrototype
{
	private const FunctionType functionId = 53039;

	public override FunctionType FunctionId => (FunctionType)53039;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Expected O, but got Unknown
		if (Self is Character)
		{
			Character val = (Character)Self;
			bool flag = false;
			int num = ((Arguments.Length != 2) ? ((int)Arguments[Arguments.Length - 1]) : 0);
			if (num >= 49)
			{
				if (val.SocialTab.ContainsKey((int)Arguments[1]))
				{
					val.SocialTab[(int)Arguments[1]] = (int)Arguments[0];
				}
				else
				{
					val.SocialTab.Add((int)Arguments[1], (int)Arguments[0]);
				}
			}
			else
			{
				foreach (AOTextures texture in ((Dynel)val).Textures)
				{
					if (texture.place == (int)Arguments[1])
					{
						flag = true;
						texture.Texture = (int)Arguments[0];
					}
				}
				if (!flag)
				{
					((Dynel)val).Textures.Add(new AOTextures((int)Arguments[1], (int)Arguments[0]));
				}
			}
		}
		((Dynel)(Character)Self).ChangedAppearance = true;
		return true;
	}
}
