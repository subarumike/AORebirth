namespace ZoneEngine_New.Core.Missions;

using System;

// A delayed action belongs to one transport and one world, not merely a character id.
internal sealed class MissionCorpseInteractionLease(object session, object world, int ownerId, long expiresAtUtcTicks)
{
    public bool Matches(object? currentSession, object? currentWorld, int currentOwnerId, bool isDead,
        bool quarantined, double distance, long nowUtcTicks)
        => ReferenceEquals(session, currentSession) && ReferenceEquals(world, currentWorld) && ownerId == currentOwnerId
            && !isDead && !quarantined && double.IsFinite(distance) && distance >= 0 && distance <= 8.0
            && nowUtcTicks < expiresAtUtcTicks;
}
