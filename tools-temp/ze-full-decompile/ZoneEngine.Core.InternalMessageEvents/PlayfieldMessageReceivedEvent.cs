using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.InternalMessageEvents;

public class PlayfieldMessageReceivedEvent
{
	private readonly InternalMessage message;

	private readonly object sender;

	public InternalMessage Message => message;

	public object Sender => sender;

	public PlayfieldMessageReceivedEvent(object sender, InternalMessage message)
	{
		this.sender = sender;
		this.message = message;
	}
}
