namespace ZoneEngine_New.Core.Network;

using AORebirth.Database.Dao;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

public interface IZoneAdmissionGate
{
    ZoneHandoffClaim Claim(ZoneLoginMessage message);
}

/// <summary>Reuses the existing read-only account resolver; never hydrates an owned player.</summary>
public sealed class ZoneAdmissionGate(IMissionDao accounts) : IZoneAdmissionGate
{
    public ZoneHandoffClaim Claim(ZoneLoginMessage message) => ZoneHandoffStore.Configured().Claim(
        message.CharacterId, message.Cookie1, message.Cookie2, accounts.ResolveCharacterAccountKey);
}
