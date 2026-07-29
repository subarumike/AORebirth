using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class opensystemdialog : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53168;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		string text = "(no args)";
		if (arguments != null && arguments.Length != 0)
		{
			try
			{
				text = ((MessagePackObject)(ref arguments[0])).AsString();
			}
			catch
			{
				text = ((object)(MessagePackObject)(ref arguments[0])).ToString();
			}
		}
		object obj2;
		if (self == null)
		{
			obj2 = "null";
		}
		else
		{
			Identity identity = ((IEntity)self).Identity;
			obj2 = ((object)(Identity)(ref identity)).ToString();
		}
		LogUtil.Debug((DebugInfoDetail)256, "OpenSystemDialog self=" + (string)obj2 + " " + text);
		return true;
	}
}
