using AORebirth.Core.Entities;

namespace ZoneEngine.Core.InternalMessages;

public class InternalMessage
{
	public InternalMessageBody MessageBody { get; set; }

	public IInstancedEntity Sender { get; set; }
}
