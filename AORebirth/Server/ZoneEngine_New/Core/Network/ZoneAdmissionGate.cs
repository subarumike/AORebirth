namespace ZoneEngine_New.Core.Network;

using AORebirth.Database.Dao;
using AORebirth.Interfaces.Persistence.Characters;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

public interface IZoneAdmissionGate
{
    ZoneHandoffClaim Claim(ZoneLoginMessage message);
}

/// <summary>Reuses the existing read-only account resolver; never hydrates an owned player.</summary>
public sealed class ZoneAdmissionGate(ICharacterDao characters) : IZoneAdmissionGate
{
    public ZoneHandoffClaim Claim(ZoneLoginMessage message)
    {
        ZoneRedirectEndpoint target = ZoneRedirectEndpoint.Configured();
        return ZoneHandoffStore.Configured().Claim(message.CharacterId, message.Cookie1, message.Cookie2,
            id => characters.LoadById(id)?.AccountUsername, target.Address.ToString(), target.Port);
    }
}
