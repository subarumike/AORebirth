using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

public sealed class PlayfieldLifecycleEvent
{
	public int Order { get; private set; }

	public string Flow { get; private set; }

	public string Stage { get; private set; }

	public string MessageType { get; private set; }

	public Identity Identity { get; private set; }

	public string Detail { get; private set; }

	public PlayfieldLifecycleEvent(int order, string flow, string stage, string messageType, Identity identity, string detail)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		Order = order;
		Flow = flow ?? string.Empty;
		Stage = stage ?? string.Empty;
		MessageType = messageType ?? string.Empty;
		Identity = identity;
		Detail = detail ?? string.Empty;
	}
}
