#region License

// Copyright (c) 2013 OpenSharp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#endregion

namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    internal sealed class PrivateCityReadyInitCoordinator
    {
        private readonly Identity playfieldIdentity;

        private readonly Func<Identity, bool> isPrivateCityPlayfieldCandidate;

        private readonly Func<int, bool> isCapturedMontroyalPrivateCityInstance;

        private readonly Func<ICharacter, int> resolveCharacterOrganizationInstance;

        private readonly Func<int, string> resolveOrganizationName;

        private readonly Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue;

        public PrivateCityReadyInitCoordinator(
            Identity playfieldIdentity,
            Func<Identity, bool> isPrivateCityPlayfieldCandidate,
            Func<int, bool> isCapturedMontroyalPrivateCityInstance,
            Func<ICharacter, int> resolveCharacterOrganizationInstance,
            Func<int, string> resolveOrganizationName,
            Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue)
        {
            this.playfieldIdentity = playfieldIdentity;
            this.isPrivateCityPlayfieldCandidate = isPrivateCityPlayfieldCandidate;
            this.isCapturedMontroyalPrivateCityInstance = isCapturedMontroyalPrivateCityInstance;
            this.resolveCharacterOrganizationInstance = resolveCharacterOrganizationInstance;
            this.resolveOrganizationName = resolveOrganizationName;
            this.resolveCharacterStatWireValue = resolveCharacterStatWireValue;
        }

        public void SendPlayfieldReadyBlock(ZoneClient client, ICharacter character)
        {
            if (client == null || character == null || !this.isPrivateCityPlayfieldCandidate(this.playfieldIdentity))
            {
                return;
            }

            if (this.isCapturedMontroyalPrivateCityInstance(this.playfieldIdentity.Instance))
            {
                this.SendEmptyPlayfieldTowersAndCities(client);
            }
            else
            {
                this.SendPlayfieldTowersAndCities(client, 1, CreateCapturedPrivateCityAllCitiesPayload());
            }

            client.Server.Info(
                client,
                "Private city ready block sent character={0} playfield={1} evidence=live_capture_20260622-092054 live_capture_20260622-093540 live_capture_20260622-101935 live_capture_20260623-021643",
                character.Identity,
                this.playfieldIdentity);
        }

        public void SendPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
        {
            if (client == null || character == null || !this.isPrivateCityPlayfieldCandidate(this.playfieldIdentity))
            {
                return;
            }

            int organizationInstance = this.resolveCharacterOrganizationInstance(character);
            if (organizationInstance <= 0)
            {
                return;
            }

            string organizationName = this.resolveOrganizationName(organizationInstance);
            client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(
                () =>
                {
                    if (!string.IsNullOrEmpty(organizationName))
                    {
                        PlayfieldLifecycleTrace.Record(
                            PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                            PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                            PlayfieldLifecycleTrace.MessageOrgInfoPacket,
                            character.Identity,
                            organizationName);
                        client.SendCompressed(
                            new OrgInfoPacketMessage
                            {
                                Identity = character.Identity,
                                Name = organizationName
                            });
                    }
                },
                () => this.SendPrivateCityStatValue(client, character, StatIds.socialstatus, 4, 1),
                () => this.SendPrivateCityStat(client, character, StatIds.clan, 0),
                () => this.SendPrivateCityStat(client, character, StatIds.clanlevel, 0),
                () => this.SendPrivateCityStatValue(client, character, StatIds.socialstatus, 4, 1),
                () => this.SendPrivateCityStatValue(client, character, StatIds.socialstatus, 4, 1),
                () => this.SendPrivateCityStatValue(client, character, StatIds.socialstatus, 4, 1),
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent,
                    character.Identity,
                    "org=" + organizationInstance + " orgName=" + organizationName + " socialStatus=4 repeats=4"));

            client.Server.Info(
                client,
                "Private city owned org init sent character={0} playfield={1} org={2} orgInfoSent={3} socialStatus=4 repeats=4 evidence=live_capture_20260623-021643 live_capture_20260623-042326",
                character.Identity,
                this.playfieldIdentity,
                organizationInstance,
                !string.IsNullOrEmpty(organizationName));
        }

        private void SendEmptyPlayfieldTowersAndCities(ZoneClient client)
        {
            this.SendPlayfieldTowersAndCities(client, 0, new byte[0]);
        }

        private void SendPlayfieldTowersAndCities(ZoneClient client, byte cityUnknown, byte[] cityPayload)
        {
            var playfieldIdentity = new Identity
                                    {
                                            Type = IdentityType.Playfield2,
                                            Instance = this.playfieldIdentity.Instance
                                        };

            client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence(
                () => client.SendCompressed(
                    new PlayfieldAllTowersMessage
                    {
                        Identity = playfieldIdentity,
                        Unknown1 = new TowerProxyBase[0]
                    }),
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllTowers,
                    playfieldIdentity),
                () => client.SendCompressed(
                    new PlayfieldAllCitiesMessage
                    {
                        Identity = playfieldIdentity,
                        Unknown = cityUnknown,
                        Payload = cityPayload ?? new byte[0]
                    }),
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllCities,
                    playfieldIdentity),
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent,
                    playfieldIdentity,
                    "cityUnknown=" + cityUnknown + " cityPayloadBytes=" + (cityPayload == null ? 0 : cityPayload.Length)));
        }

        private void SendPrivateCityStat(ZoneClient client, ICharacter character, StatIds statId, byte unknown)
        {
            this.SendPrivateCityStatValue(
                client,
                character,
                statId,
                this.resolveCharacterStatWireValue(character, statId),
                unknown);
        }

        private void SendPrivateCityStatValue(
            ZoneClient client,
            ICharacter character,
            StatIds statId,
            uint value,
            byte unknown)
        {
            string stage = PlayfieldLifecycleTrace.StagePrivateCitySocialStatus;
            if (statId == StatIds.clan)
            {
                stage = PlayfieldLifecycleTrace.StagePrivateCityClan;
            }
            else if (statId == StatIds.clanlevel)
            {
                stage = PlayfieldLifecycleTrace.StagePrivateCityClanLevel;
            }

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                stage,
                PlayfieldLifecycleTrace.MessageStat,
                character.Identity,
                statId + "=" + value);
            client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Unknown = unknown,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)statId,
                                Value2 = value
                            }
                        }
                });
        }

        private static byte[] CreateCapturedPrivateCityAllCitiesPayload()
        {
            return new byte[]
                   {
                       0x00, 0x00, 0x00, 0x05, 0x44, 0x5C, 0x00, 0x00,
                       0x40, 0xA0, 0x00, 0x00, 0x44, 0xB5, 0x80, 0x00,
                       0x00, 0x00, 0x00, 0xB4, 0x00, 0x00, 0x0F, 0x42,
                       0x68, 0x00, 0x6A, 0x00, 0x6D, 0x44, 0x62, 0x00,
                       0x00, 0x40, 0xA0, 0x00, 0x00, 0x44, 0xB0, 0x80,
                       0x00, 0x00, 0x00, 0x00, 0xB4, 0x00, 0x00, 0x0F,
                       0x42, 0x68, 0x00, 0x6A, 0x00, 0x6E, 0x44, 0x80,
                       0x80, 0x00, 0x40, 0xA0, 0x00, 0x00, 0x44, 0xA9,
                       0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                       0x0F, 0x42, 0x68, 0x00, 0x6A, 0x00, 0x68, 0x44,
                       0x85, 0x80, 0x00, 0x40, 0xA0, 0x00, 0x00, 0x44,
                       0xB0, 0x80, 0x00, 0x00, 0x00, 0x00, 0x5A, 0x00,
                       0x00, 0x0F, 0x42, 0x68, 0x00, 0x6A, 0x00, 0x66,
                       0x44, 0x87, 0x80, 0x00, 0x40, 0xA0, 0x00, 0x00,
                       0x44, 0xA9, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x0F, 0x42, 0x68, 0x00, 0x6A, 0x00,
                       0x75
                   };
        }
    }
}
