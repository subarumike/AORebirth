namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class CityControllerInteractionHandler
    {
        public static readonly CityControllerInteractionHandler Default =
            new CityControllerInteractionHandler();

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

        private const int CapturedCityControllerInfoIdentityType = 0x0000C419;

        private const int CapturedCityControllerInfoIdentityInstance = 0x0000C000;

        private const int CapturedCityControllerBuildingType = 0x0000C79E;

        private const int CapturedCityControllerBuildingInstance = 0x0000138A;

        private const int CapturedNonOrgCityControllerBuildingInstance = 0x0000177A;

        private const int CapturedMontroyalPrivateCityInstance = 1196045;

        private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

        private const string CapturedCityControllerOwnedOrganizationText = "Est. 2024";

        private const string CapturedCityControllerNoOrganizationText = "no organization";

        private const string CapturedCityControllerNonOrgText = "Identifies As Clan";

        private const int CapturedCityControllerCloseWindowInstance = 0x0000C000;

        private CityControllerInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (!GenericCmdUseRouteClassifier.IsPrivateCityControllerTarget(target))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null)
            {
                client.Server.Info(
                    client,
                    "CityController use consumed without character target={0} count={1} temp4={2} evidence=live_capture_20260623-015602",
                    target,
                    message.Count,
                    message.Temp4);

                return true;
            }

            if (character.Playfield == null
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return false;
            }

            int organizationId = ResolveCharacterOrganizationInstance(character);
            int owningOrganizationId = ResolveCurrentPrivateCityOwningOrganizationInstance(character, organizationId);
            CityControllerMenuMode menuMode =
                CityControllerInteractionRules.ResolveMenuMode(organizationId, owningOrganizationId);
            if (menuMode == CityControllerMenuMode.OwnerMember)
            {
                SendCapturedCityControllerOpenSignals(client, character, organizationId, true);
            }
            else
            {
                SendCapturedCityControllerNonOrgOpenSignals(client, character, owningOrganizationId);
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "CityController use handled character={0} target={1} org={2} owningOrg={3} menu={4} count={5} temp4={6} feedbackSent=False aoTransportSignalSent=5 noCityAdvantages=1 noOwnershipChange=1 evidence=private_city_owned_entry_capture_20260623_021643/city_controller_non_org_capture_20260623_081344 runtime_target=009C6010",
                character.Identity,
                target,
                organizationId,
                owningOrganizationId,
                menuMode == CityControllerMenuMode.OwnerMember ? "owner_member" : "non_org_limited",
                message.Count,
                message.Temp4);

            return true;
        }

        public bool TryHandleWindowClose(MessageWrapper<CityControllerWindowCloseMessage> messageWrapper)
        {
            ICharacter character = messageWrapper.Client.Controller.Character;
            CityControllerWindowCloseMessage message = messageWrapper.MessageBody;
            if (character == null
                || character.Playfield == null
                || message.WindowInstance != CapturedCityControllerCloseWindowInstance
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return false;
            }

            messageWrapper.Client.SendCompressed(
                new AOTransportSignalMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Signal = 7,
                    Payload = CreateCapturedCityControllerClosePayload(character)
                });

            messageWrapper.Client.Server.Info(
                messageWrapper.Client,
                "CityController window close handled character={0} windowInstance={1} signal=7 evidence=private_city_owned_entry_capture_20260623_021643",
                character.Identity,
                message.WindowInstance);

            return true;
        }

        private static void SendCapturedCityControllerOpenSignals(
            IZoneClient client,
            ICharacter character,
            int organizationId,
            bool hasOrganization)
        {
            SendCapturedCityControllerSignal(
                client,
                character,
                5,
                CreateCapturedCityControllerInfoPayload(character, organizationId, hasOrganization));
            SendCapturedCityControllerSignal(client, character, 10, new byte[] { 0x00, 0xE4, 0xE1, 0xC0 });
            SendCapturedCityControllerSignal(
                client,
                character,
                13,
                hasOrganization
                    ? new byte[] { 0x00, 0x27, 0x88, 0x05 }
                    : new byte[] { 0x95, 0xC5, 0xD6, 0xBD });
            SendCapturedCityControllerSignal(
                client,
                character,
                14,
                hasOrganization
                    ? new byte[] { 0x00, 0x00, 0x00, 0x00, 0x95, 0xC5, 0xCC, 0xD7 }
                    : new byte[] { 0x00, 0x00, 0x00, 0x00, 0x95, 0xC5, 0xD6, 0xBD });
            SendCapturedCityControllerSignal(client, character, 15, new byte[] { 0x3F, 0x80, 0x00, 0x00 });
        }

        private static void SendCapturedCityControllerNonOrgOpenSignals(
            IZoneClient client,
            ICharacter character,
            int owningOrganizationId)
        {
            SendCapturedCityControllerSignal(
                client,
                character,
                5,
                CreateCapturedCityControllerNonOrgInfoPayload(character, owningOrganizationId));
            SendCapturedCityControllerSignal(client, character, 10, new byte[] { 0x03, 0x93, 0x87, 0x00 });
            SendCapturedCityControllerSignal(client, character, 13, new byte[] { 0x00, 0x26, 0x32, 0xBF });
            SendCapturedCityControllerSignal(
                client,
                character,
                14,
                new byte[] { 0x00, 0x00, 0x00, 0x01, 0x95, 0xC5, 0x79, 0x53 });
            SendCapturedCityControllerSignal(client, character, 15, new byte[] { 0x3F, 0x80, 0x00, 0x00 });
        }

        private static void SendCapturedCityControllerSignal(
            IZoneClient client,
            ICharacter character,
            int signal,
            byte[] payload)
        {
            client.SendCompressed(
                new AOTransportSignalMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Signal = signal,
                    Payload = payload
                });
        }

        private static byte[] CreateCapturedCityControllerInfoPayload(
            ICharacter character,
            int organizationId,
            bool hasOrganization)
        {
            string text = hasOrganization
                ? CapturedCityControllerOwnedOrganizationText
                : CapturedCityControllerNoOrganizationText;
            byte[] textBytes = Encoding.ASCII.GetBytes(text);
            var payload = new List<byte>(58 + textBytes.Length);

            AppendInt32(payload, CapturedCityControllerInfoIdentityType);
            AppendInt32(payload, CapturedCityControllerInfoIdentityInstance);
            AppendInt32(
                payload,
                hasOrganization ? organizationId : CapturedOwnedPrivateCityOrganizationInstance);
            AppendInt32(payload, CapturedCityControllerBuildingType);
            AppendInt32(payload, CapturedCityControllerBuildingInstance);
            AppendInt32(payload, (int)character.Identity.Type);
            AppendInt32(payload, character.Identity.Instance);
            AppendInt32(payload, hasOrganization ? 2 : 1);
            AppendInt32(payload, hasOrganization ? 3 : 2);
            AppendInt32(payload, hasOrganization ? 1 : -1);
            AppendInt16(payload, textBytes.Length);
            payload.AddRange(textBytes);

            return payload.ToArray();
        }

        private static byte[] CreateCapturedCityControllerNonOrgInfoPayload(
            ICharacter character,
            int owningOrganizationId)
        {
            byte[] textBytes = Encoding.ASCII.GetBytes(CapturedCityControllerNonOrgText);
            var payload = new List<byte>(58 + textBytes.Length);

            AppendInt32(payload, CapturedCityControllerInfoIdentityType);
            AppendInt32(payload, CapturedCityControllerInfoIdentityInstance);
            AppendInt32(
                payload,
                owningOrganizationId > 0 ? owningOrganizationId : CapturedPrivateCityOrganizationInstance);
            AppendInt32(payload, CapturedCityControllerBuildingType);
            AppendInt32(payload, ResolvePrivateCityControllerBuildingInstance(character));
            AppendInt32(payload, (int)character.Identity.Type);
            AppendInt32(payload, character.Identity.Instance);
            AppendInt32(payload, 2);
            AppendInt32(payload, 1);
            AppendInt32(payload, -1);
            AppendInt16(payload, textBytes.Length);
            payload.AddRange(textBytes);

            return payload.ToArray();
        }

        private static byte[] CreateCapturedCityControllerClosePayload(ICharacter character)
        {
            int organizationId = ResolveCloseOrganizationInstance(character);
            var payload = new List<byte>(20);

            AppendInt32(payload, CapturedCityControllerInfoIdentityType);
            AppendInt32(payload, CapturedCityControllerInfoIdentityInstance);
            AppendInt32(payload, organizationId);
            AppendInt32(payload, CapturedCityControllerBuildingType);
            AppendInt32(payload, CapturedCityControllerBuildingInstance);

            return payload.ToArray();
        }

        private static int ResolvePrivateCityControllerBuildingInstance(ICharacter character)
        {
            if (character == null || character.Playfield == null)
            {
                return CapturedNonOrgCityControllerBuildingInstance;
            }

            int playfieldInstance = character.Playfield.Identity.Instance;
            return playfieldInstance == CapturedMontroyalPrivateCityInstance
                   || playfieldInstance == CapturedOwnedMontroyalPrivateCityInstance
                       ? CapturedCityControllerBuildingInstance
                       : CapturedNonOrgCityControllerBuildingInstance;
        }

        private static int ResolveCurrentPrivateCityOwningOrganizationInstance(ICharacter character, int characterOrganizationId)
        {
            if (character == null || character.Playfield == null)
            {
                return 0;
            }

            int currentPlayfieldInstance = character.Playfield.Identity.Instance;
            if (characterOrganizationId > 0
                && ResolveOrganizationCityId(characterOrganizationId) == currentPlayfieldInstance)
            {
                return characterOrganizationId;
            }

            try
            {
                DBOrganization organization =
                    OrganizationDao.Instance.GetAll(new { CityId = currentPlayfieldInstance }).FirstOrDefault();
                if (organization != null)
                {
                    return organization.Id;
                }
            }
            catch
            {
            }

            return currentPlayfieldInstance == CapturedOwnedMontroyalPrivateCityInstance
                       ? CapturedOwnedPrivateCityOrganizationInstance
                       : 0;
        }

        private static int ResolveOrganizationCityId(int organizationInstance)
        {
            if (organizationInstance <= 0)
            {
                return 0;
            }

            try
            {
                DBOrganization organization = OrganizationDao.Instance.Get(organizationInstance);
                return organization == null ? 0 : organization.CityId;
            }
            catch
            {
                return 0;
            }
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            uint baseOrganizationInstance = character.Stats[StatIds.clan].BaseValue;
            if (baseOrganizationInstance > 0 && baseOrganizationInstance <= int.MaxValue)
            {
                return (int)baseOrganizationInstance;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : 0;
        }

        private static int ResolveCloseOrganizationInstance(ICharacter character)
        {
            uint baseOrganizationInstance = character.Stats[StatIds.clan].BaseValue;
            if (baseOrganizationInstance > 0 && baseOrganizationInstance <= int.MaxValue)
            {
                return (int)baseOrganizationInstance;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : CapturedOwnedPrivateCityOrganizationInstance;
        }

        private static void AppendInt32(ICollection<byte> bytes, int value)
        {
            bytes.Add((byte)((value >> 24) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)(value & 0xFF));
        }

        private static void AppendInt16(ICollection<byte> bytes, int value)
        {
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)(value & 0xFF));
        }
    }

    [MessageHandler(MessageHandlerDirection.All)]
    public class CityControllerWindowCloseMessageHandler :
        BaseMessageHandler<CityControllerWindowCloseMessage, CityControllerWindowCloseMessageHandler>
    {
        public override void Receive(MessageWrapper<CityControllerWindowCloseMessage> messageWrapper)
        {
            CityControllerInteractionHandler.Default.TryHandleWindowClose(messageWrapper);
        }
    }
}
