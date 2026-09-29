namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Pets;

/// <summary>
/// PetCommand from the pet window or /pet (Gamecode.dll PetCommandIIR_c 0x100763de): Unknown2 is the command
/// code, Unknown3 its argument, Identities the pets it is for. The owner's own pets on its playfield obey
/// follow, wait, guard, attack (at the owner's target) and terminate; everything else is ignored.
/// </summary>
public sealed class PetCommandMessageHandler : IMessageHandler<PetCommandMessage>
{
    public Type MessageBodyType => typeof(PetCommandMessage);

    public void Handle(MessageBody body, IZoneSession session) => Handle((PetCommandMessage)body, session);

    public void Handle(PetCommandMessage message, IZoneSession session)
    {
        var player = session.Player;
        if (session.State != SessionState.InPlay || player == null || player.IsDead || player.Playfield == null
            || player.IsPersistenceQuarantined || !message.Identity.Equals(player.Identity))
            return;
        if (!Enum.IsDefined(typeof(PetCommandCode), message.Unknown2))
            return;

        Identity[] pets = message.Identities ?? [];
        player.Playfield.GetRequiredService<PetService>().Command(player, (PetCommandCode)message.Unknown2, pets);
    }
}
