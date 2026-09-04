namespace ZoneEngine_New.Core.Entities
{
    using SmokeLounge.AOtomation.Messaging.Messages;

    public interface ISpawnable
    {
        MessageBody BuildSpawnMessage();
    }
}
