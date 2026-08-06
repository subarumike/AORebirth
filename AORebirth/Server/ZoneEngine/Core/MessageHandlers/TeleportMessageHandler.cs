#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class TeleportMessageHandler : BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>
    {
        private const IdentityType LivePlayfieldProxyType = (IdentityType)0x0000C79E;

        /// <summary>Capture 20260718-062936 mission-enter N3Teleport Playfield identity type (ACGBuildingGeneratorData).</summary>
        private const IdentityType LiveMissionBuildingType = (IdentityType)0x0000C79F;

        /// <summary>Capture 20260718-062936 mission-enter building instance on the wire.</summary>
        private const int CapturedMissionBuildingInstance = unchecked((int)0x00D6D5C0);

        private const int CapturedMissionTeleportPlayfield2Type = 0x000186A2;

        private const int CapturedMissionTeleportPlayfield2Instance = 1;

        private const int CapturedPrivateCityBuildingInstance = 0x0000177A;

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        private const int CapturedPrivateCityTeleportPlayfield2Type = 0x000186A9;

        private const int CapturedPrivateCityTeleportPlayfield2Instance = unchecked((int)0xC000177A);

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="destination">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <param name="playfield">
        /// </param>
        public void Send(ICharacter character, Vector3 destination, Quaternion heading, Identity playfield)
        {
            this.Send(character, this.NormalTeleport(character, destination, heading, playfield), false);
        }

        public void SendLocal(ICharacter character, Vector3 destination, Quaternion heading)
        {
            this.Send(character, this.LocalTeleport(character, destination, heading), false);
        }

        public void SendCapturedGatewayTransfer(
            ICharacter character,
            Vector3 envelopeDestination,
            Vector3 landingDestination,
            Quaternion heading,
            int destinationPlayfieldId)
        {
            this.Send(
                character,
                this.CapturedGatewayTransfer(
                    character,
                    envelopeDestination,
                    landingDestination,
                    heading,
                    destinationPlayfieldId),
                false);
        }

        internal void SendOfficialDungeonProxyTransfer(
            ICharacter character,
            Vector3 envelopeDestination,
            Quaternion heading,
            int destinationPlayfieldId,
            Identity sourceDoor)
        {
            this.SendOfficialDungeonProxyTransition(
                character,
                envelopeDestination,
                heading,
                destinationPlayfieldId,
                (IdentityType)51102,
                1,
                sourceDoor.Instance,
                new Identity { Type = (IdentityType)100002, Instance = 1 },
                new byte[0]);
        }

        internal void SendOfficialDungeonProxyExit(
            ICharacter character,
            Vector3 envelopeDestination,
            Quaternion heading,
            int destinationPlayfieldId,
            Identity sourceDoor)
        {
            this.SendOfficialDungeonProxyTransition(
                character,
                envelopeDestination,
                heading,
                destinationPlayfieldId,
                (IdentityType)51100,
                0,
                0,
                new Identity { Type = (IdentityType)100003, Instance = sourceDoor.Instance },
                new byte[] { 0, 0, 0, 1 });
        }

        private void SendOfficialDungeonProxyTransition(
            ICharacter character,
            Vector3 envelopeDestination,
            Quaternion heading,
            int destinationPlayfieldId,
            IdentityType categoricalPlayfieldType,
            int gameServerId,
            int sgId,
            Identity secondaryPlayfield,
            byte[] payload)
        {
            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                                    {
                                        X = (float)envelopeDestination.x,
                                        Y = (float)envelopeDestination.y,
                                        Z = (float)envelopeDestination.z
                                    };
                    x.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion
                                {
                                    X = (float)heading.x,
                                    Y = (float)heading.y,
                                    Z = (float)heading.z,
                                    W = (float)heading.w
                                };
                    x.Unknown1 = 0x61;
                    x.Playfield = new Identity
                                  {
                                      Type = categoricalPlayfieldType,
                                      Instance = destinationPlayfieldId
                                  };
                    x.GameServerId = gameServerId;
                    x.SgId = sgId;
                    x.ChangePlayfield = new Identity
                                        {
                                            Type = IdentityType.Playfield2,
                                            Instance = destinationPlayfieldId
                                        };
                    x.Unknown4 = 0;
                    x.Unknown5 = 0;
                    x.Playfield2 = secondaryPlayfield;
                    x.Payload = payload;
                },
                false);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="destination">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <param name="playfield">
        /// </param>
        /// <param name="playfieldInstance">
        /// </param>
        /// <param name="GS">
        /// </param>
        /// <param name="SG">
        /// </param>
        /// <param name="destinationidentity">
        /// </param>
        public void SendTeleportProxy(
            ICharacter character,
            Vector3 destination,
            Quaternion heading,
            int playfield,
            Identity playfieldInstance,
            int GS,
            int SG,
            Identity destinationidentity)
        {
            this.Send(
                character,
                this.ProxyTeleport(
                    character,
                    destination,
                    heading,
                    playfield,
                    playfieldInstance,
                    GS,
                    SG,
                    destinationidentity),
                false);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="destination">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <param name="playfield">
        /// </param>
        /// <param name="playfieldInstance">
        /// </param>
        /// <param name="GS">
        /// </param>
        /// <param name="SG">
        /// </param>
        /// <param name="destinationidentity">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller ProxyTeleport(
            ICharacter character,
            Vector3 destination,
            Quaternion heading,
            int playfield,
            Identity playfieldInstance,
            int GS,
            int SG,
            Identity destinationidentity)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0;
                x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3()
                                {
                                    X = (float)destination.x,
                                    Y = (float)destination.y,
                                    Z = (float)destination.z
                                };
                x.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion()
                            {
                                X = (float)heading.x,
                                Y = (float)heading.y,
                                Z = (float)heading.z,
                                W = (float)heading.w
                            };
                x.Unknown1 = 0x61;
                x.Playfield = playfieldInstance;
                x.GameServerId = GS;
                x.SgId = SG;
                x.ChangePlayfield = ((playfield != character.Playfield.Identity.Instance)
                                     || (IdentityType.Playfield != character.Playfield.Identity.Type))
                    ? new Identity { Type = IdentityType.Playfield2, Instance = playfield }
                    : Identity.None;
                x.Playfield2 = destinationidentity;
                x.Payload = BuildDestinationPayload(destination);
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="destination">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <param name="playfield">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller NormalTeleport(
            ICharacter character,
            Vector3 destination,
            Quaternion heading,
            Identity playfield)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0;
                x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3()
                                {
                                    X = (float)destination.x,
                                    Y = (float)destination.y,
                                    Z = (float)destination.z
                                };
                x.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion()
                            {
                                X = (float)heading.x,
                                Y = (float)heading.y,
                                Z = (float)heading.z,
                                W = (float)heading.w
                            };
                x.Unknown1 = 0x61;
                // Match Teleport/PAF: stamped shape building (never a foreign fog-only building).
                if (IsMissionInstanceDestination(playfield))
                {
                    bool generatedMission =
                        MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                            playfield.Instance);
                    // Match PlayfieldAnarchyF: stamped shape ACG building for this enter.
                    int buildingInstance =
                        ZoneEngine.Core.Missions.MissionInstanceService.GetLiveBuildingInstance(
                            playfield.Instance);
                    byte[] generatorPayload = generatedMission
                        ? MissionInstanceService.GetLiveGeneratorPayload(playfield.Instance)
                        : null;
                    if (generatedMission
                        && (buildingInstance == 0
                            || generatorPayload == null
                            || generatorPayload.Length == 0))
                    {
                        throw new InvalidOperationException(
                            "Generated ACG mission has no exact building identity or generator payload.");
                    }

                    if (buildingInstance == 0)
                    {
                        buildingInstance = CapturedMissionBuildingInstance;
                    }

                    // Gold 20260725-184103: N3Teleport Destination is ACG entry
                    // (545.43, 8.51, 350.52). Interior spawn / PAF coords are (~1.8, 5, 95).
                    // Outdoor marker XYZ (capture 20260725-202953) made ACG load wrong → grey map.
                    float destX = 545.43f;
                    float destY = 8.51f;
                    float destZ = 350.52f;

                    x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3()
                                    {
                                        X = destX,
                                        Y = destY,
                                        Z = destZ
                                    };

                    x.Playfield = new Identity
                                  {
                                      Type = LiveMissionBuildingType,
                                      Instance = buildingInstance
                                  };
                    x.GameServerId = 0;
                    x.SgId = 0;
                    x.ChangePlayfield = new Identity { Type = IdentityType.Playfield2, Instance = playfield.Instance };
                    x.Unknown4 = 0;
                    x.Unknown5 = 0;
                    x.Playfield2 = new Identity
                                   {
                                       Type = (IdentityType)CapturedMissionTeleportPlayfield2Type,
                                       Instance =
                                           generatedMission
                                               ? playfield.Instance
                                               : CapturedMissionTeleportPlayfield2Instance
                                   };
                    x.Payload = new byte[0];
                    return;
                }

                x.Playfield = IsPrivateCityDestination(playfield)
                                  ? new Identity { Type = LivePlayfieldProxyType, Instance = CapturedPrivateCityBuildingInstance }
                                  : new Identity() { Type = LivePlayfieldProxyType, Instance = playfield.Instance };
                x.GameServerId = IsPrivateCityDestination(playfield) ? 0 : 1;
                x.SgId = IsPrivateCityDestination(playfield)
                             ? ResolvePrivateCityOrganizationInstance(character)
                             : 0;
                x.ChangePlayfield = new Identity { Type = IdentityType.Playfield2, Instance = playfield.Instance };
                x.Unknown4 = 0;
                x.Unknown5 = 0;
                x.Playfield2 = IsPrivateCityDestination(playfield)
                                   ? new Identity
                                     {
                                         Type = (IdentityType)CapturedPrivateCityTeleportPlayfield2Type,
                                         Instance = CapturedPrivateCityTeleportPlayfield2Instance
                                     }
                                   : Identity.None;
                x.Payload = IsPrivateCityDestination(playfield)
                                ? new byte[0]
                                : BuildDestinationPayload(destination);
            };
        }

        private MessageDataFiller LocalTeleport(ICharacter character, Vector3 destination, Quaternion heading)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0;
                x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3()
                                {
                                    X = (float)destination.x,
                                    Y = (float)destination.y,
                                    Z = (float)destination.z
                                };
                x.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion()
                            {
                                X = (float)heading.x,
                                Y = (float)heading.y,
                                Z = (float)heading.z,
                                W = (float)heading.w
                            };
                x.Unknown1 = 0x61;
                x.Playfield =
                    new Identity() { Type = LivePlayfieldProxyType, Instance = character.Playfield.Identity.Instance };
                x.GameServerId = 1;
                x.SgId = 0;
                x.ChangePlayfield = Identity.None;
                x.Unknown4 = 0;
                x.Unknown5 = 0;
                x.Playfield2 = Identity.None;
                x.Payload = BuildDestinationPayload(destination);
            };
        }

        private MessageDataFiller CapturedGatewayTransfer(
            ICharacter character,
            Vector3 envelopeDestination,
            Vector3 landingDestination,
            Quaternion heading,
            int destinationPlayfieldId)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0;
                x.Destination = new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                {
                    X = (float)envelopeDestination.x,
                    Y = (float)envelopeDestination.y,
                    Z = (float)envelopeDestination.z
                };
                x.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion
                {
                    X = (float)heading.x,
                    Y = (float)heading.y,
                    Z = (float)heading.z,
                    W = (float)heading.w
                };
                x.Unknown1 = 97;
                x.Playfield = new Identity { Type = (IdentityType)51100, Instance = destinationPlayfieldId };
                x.GameServerId = 0;
                x.SgId = 0;
                x.ChangePlayfield = new Identity
                {
                    Type = (IdentityType)40016,
                    Instance = destinationPlayfieldId
                };
                x.Unknown4 = 0;
                x.Unknown5 = 0;
                x.Playfield2 = Identity.None;
                x.Payload = BuildDestinationPayload(landingDestination);
            };
        }

        private static byte[] BuildDestinationPayload(Vector3 destination)
        {
            var payload = new byte[12];
            WriteSingle(payload, 0, (float)destination.x);
            WriteSingle(payload, 4, (float)destination.y);
            WriteSingle(payload, 8, (float)destination.z);
            return payload;
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static bool IsPrivateCityDestination(Identity playfield)
        {
            return AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(
                new Identity { Type = IdentityType.Playfield2, Instance = playfield.Instance });
        }

        private static bool IsMissionInstanceDestination(Identity playfield)
        {
            if (playfield.Type != IdentityType.Playfield && playfield.Type != IdentityType.Playfield2)
            {
                return false;
            }

            return MissionInstanceService.IsMissionInstancePlayfield(playfield.Instance);
        }

        private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
        {
            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : CapturedPrivateCityOrganizationInstance;
        }
    }
}
