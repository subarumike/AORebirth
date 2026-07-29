using AORebirth.Core.Functions;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.InternalMessages;

public class IMExecuteFunction
{
	public Function Function;

	public Identity User;

	public IMExecuteFunction(Function function, Identity user)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		User = user;
		Function = function;
	}
}
