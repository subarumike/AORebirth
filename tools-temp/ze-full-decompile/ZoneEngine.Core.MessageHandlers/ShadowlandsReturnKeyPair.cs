namespace ZoneEngine.Core.MessageHandlers;

public sealed class ShadowlandsReturnKeyPair
{
	public int StatueTemplateId { get; private set; }

	public int InsigniaTemplateId { get; private set; }

	public ShadowlandsReturnKeyPair(int statueTemplateId, int insigniaTemplateId)
	{
		StatueTemplateId = statueTemplateId;
		InsigniaTemplateId = insigniaTemplateId;
	}
}
