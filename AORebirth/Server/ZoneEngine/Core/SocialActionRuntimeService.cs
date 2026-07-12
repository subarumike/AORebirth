namespace ZoneEngine.Core
{
    #region Usings

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.InternalMessages;

    #endregion

    internal static class SocialActionRuntimeService
    {
        internal static void BroadcastAthleteBackflip(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            SocialActionCmdMessage message = CreateAthleteBackflipMessage(character);

            IZoneClient client = character.Controller == null ? null : character.Controller.Client;
            if (client != null)
            {
                client.SendCompressed(message);
            }

            IPlayfield playfield = character.Playfield;
            if (playfield != null)
            {
                playfield.Publish(new IMSendAOtomationMessageToPlayfield { Body = message });
            }
        }

        internal static void TriggerLevelUpBackflip(ICharacter character)
        {
            BroadcastAthleteBackflip(character);
        }

        private static SocialActionCmdMessage CreateAthleteBackflipMessage(ICharacter character)
        {
            return new SocialActionCmdMessage
            {
                Identity = character.Identity,
                Unknown = 0,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0,
                Unknown4 = 1,
                Unknown5 = 0,
                Action = SocialAction.AthleteBackflip
            };
        }
    }
}
