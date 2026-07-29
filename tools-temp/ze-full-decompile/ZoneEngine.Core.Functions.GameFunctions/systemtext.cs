using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class systemtext : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53044;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		string formattedMessage = ((MessagePackObject)(ref arguments[0])).AsString();
		FormatFeedbackMessage val = new FormatFeedbackMessage
		{
			Identity = ((IEntity)self).Identity,
			FormattedMessage = formattedMessage,
			Unknown1 = 0,
			Unknown2 = 0
		};
		((IDynel)(ICharacter)self).Send((MessageBody)(object)val, false);
		return true;
	}
}
