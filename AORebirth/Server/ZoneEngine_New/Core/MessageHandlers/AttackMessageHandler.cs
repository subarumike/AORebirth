namespace ZoneEngine_New.Core.MessageHandlers;

using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

// Rejected before acquiring a target, starting timers, consuming ammo or calculating damage.
public sealed class AttackMessageHandler() : UnavailableHandler<AttackMessage>("Weapon combat");
