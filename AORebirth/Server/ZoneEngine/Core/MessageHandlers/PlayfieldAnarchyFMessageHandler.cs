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
    using System.Collections.Generic;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using AORebirth.Core.Playfields;

    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class PlayfieldAnarchyFMessageHandler :
        BaseMessageHandler<PlayfieldAnarchyFMessage, PlayfieldAnarchyFMessageHandler>
    {
        private const IdentityType CapturedPrivateCityPlayfieldProxyType = (IdentityType)0x0000C79E;

        /// <summary>Capture 20260718-062936 mission-enter PlayfieldAnarchyF PlayfieldId1 type.</summary>
        private const IdentityType CapturedMissionBuildingType = (IdentityType)0x0000C79F;

        private const int CapturedPrivateCityBuildingInstance = 0x0000177A;

        /// <summary>Capture 20260718-062936 mission-enter ACGBuildingGeneratorData instance.</summary>
        private const int CapturedMissionBuildingInstance = unchecked((int)0x00D6D5C0);

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        private const int CapturedMontroyalPrivateCityInstance = 1196045;

        private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

        private const int CapturedMontroyalPrivateCityBuildingInstance = 0x0000138A;

        #region Outbound

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        public void Send(ICharacter character)
        {
            this.Send(character, Filler(character));
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <returns>
        /// </returns>
        private static MessageDataFiller Filler(ICharacter character)
        {
            return x =>
            {
                x.Identity = new Identity
                             {
                                 Type = IdentityType.Playfield2,
                                 Instance = character.Playfield.Identity.Instance
                             };
                Coordinate temp = character.Coordinates();
                x.CharacterCoordinates = new Vector3 { X = temp.x, Y = temp.y, Z = temp.z, };
                x.PlayfieldId1 = new Identity
                                 {
                                     Type = IdentityType.Playfield1,
                                     Instance = character.Playfield.Identity.Instance
                                 };
                x.PlayfieldId2 = new Identity
                                 {
                                     Type = IdentityType.Playfield2,
                                     Instance = character.Playfield.Identity.Instance
                                 };
                x.PlayfieldX = Playfields.GetPlayfieldX(character.Playfield.Identity.Instance);
                x.PlayfieldZ = Playfields.GetPlayfieldZ(character.Playfield.Identity.Instance);

                // Capture 20260824-125154: ACGEntrance C7A1:C00010D6 + j222 generator payload.
                // D4/D3/D2 before D1: all share the live ACG dyn band; wrong order stamps D1 layout.
                if (NascenceDungeon4Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
                {
                    x.PlayfieldX = 0;
                    x.PlayfieldZ = 0;
                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = NascenceDungeon4Rules.BuildingGeneratorType,
                                         Instance = NascenceDungeon4Rules.BuildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = NascenceDungeon4AcgLayout.CreateGeneratorPayload();
                }
                else if (NascenceDungeon3Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
                {
                    x.PlayfieldX = 0;
                    x.PlayfieldZ = 0;
                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = NascenceDungeon3Rules.BuildingGeneratorType,
                                         Instance = NascenceDungeon3Rules.BuildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = NascenceDungeon3AcgLayout.CreateGeneratorPayload();
                }
                else if (NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
                {
                    x.PlayfieldX = 0;
                    x.PlayfieldZ = 0;
                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = NascenceDungeon2Rules.BuildingGeneratorType,
                                         Instance = NascenceDungeon2Rules.BuildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = NascenceDungeon2AcgLayout.CreateGeneratorPayload();
                }
                else if (NascenceDungeon1Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
                {
                    // Keep Identity/PlayfieldId2 = server dyn lease. Forcing reserved 1F900B
                    // mismatched ChangePlayfield and froze the client on character login.
                    // PlayfieldX/Z=0 keeps ACG fog mode (non-zero → full static grey floorplan).
                    x.PlayfieldX = 0;
                    x.PlayfieldZ = 0;
                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = NascenceDungeon1Rules.BuildingGeneratorType,
                                         Instance = NascenceDungeon1Rules.BuildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = NascenceDungeon1AcgLayout.CreateGeneratorPayload();
                }
                else if (MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
                {
                    // Remapped live PFs are not in Playfields.xml → GetPlayfieldX/Z returns 100000
                    // (unknown-size fallback). That makes the client treat the interior as a huge
                    // static PF and paint the full grey floorplan. Gold 080425 PAF uses 0 sizes.
                    x.PlayfieldX = 0;
                    x.PlayfieldZ = 0;

                    // Live zone-in: PlayfieldId1 = ACGBuildingGeneratorData + stamped shape payload.
                    // Payload MUST match ShapeSourceByPlayfield (doors + NPC XYZ). Foreign ACG piles mobs.
                    int pf = character.Playfield.Identity.Instance;
                    bool generatedMission =
                        MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(pf);
                    byte[] payload = MissionInstanceService.GetLiveGeneratorPayload(pf);
                    int buildingInstance =
                        MissionInstanceService.GetLiveBuildingInstance(pf);
                    if (generatedMission
                        && (payload == null
                            || payload.Length == 0
                            || buildingInstance == 0))
                    {
                        throw new InvalidOperationException(
                            "Generated ACG mission has no exact generator payload or building identity.");
                    }

                    if (payload == null || payload.Length == 0)
                    {
                        payload = CreateCapturedMissionGeneratorPayload();
                        buildingInstance = CapturedMissionBuildingInstance;
                    }

                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = CapturedMissionBuildingType,
                                         Instance = buildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = payload;
                }
                else if (LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                             character.Playfield.Identity.Instance))
                {
                    // Capture 20260806-220142: per-character apartment PF + building instance.
                    // Must run before private-city candidate checks on overlapping bands.
                    int buildingInstance;
                    if (!LuxuryApartmentInstanceRuntime.TryGetBuildingInstance(
                            character.Playfield.Identity.Instance,
                            out buildingInstance))
                    {
                        buildingInstance = LuxuryApartmentSunriseRules.LuxuryApartmentBuildingInstance;
                    }

                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = IdentityType.Playfield,
                                         Instance = buildingInstance
                                     };
                    x.Unknown3 = 0;
                    x.Unknown4 = 0;
                    x.GeneratorPayload = CreateCapturedLuxuryApartmentGeneratorPayload(buildingInstance);
                }
                else if (AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
                {
                    x.PlayfieldId1 = new Identity
                                     {
                                         Type = CapturedPrivateCityPlayfieldProxyType,
                                         Instance = ResolvePrivateCityBuildingInstance(character.Playfield.Identity.Instance)
                                     };
                    x.Unknown4 = ResolvePrivateCityOrganizationInstance(character);
                    x.GeneratorPayload = CreateCapturedPrivateCityGeneratorPayload(character.Playfield.Identity.Instance);
                }

                IEnumerable<Vendor> vendors = Pool.Instance.GetAll<Vendor>(
                    character.Playfield.Identity,
                    (int)IdentityType.VendingMachine);

                /*                if (vendors.Any())
                {
                    x.PlayfieldVendorInfo = new PlayfieldVendorInfo()
                                            {
                                                VendorCount = vendors.Count(),
                                                FirstVendorId =
                                                    vendors.ElementAt(0).Identity.Instance
                                            };
                }*/
            };

            // TODO: Add the VendorHandler again
            /* var vendorcount = VendorHandler.GetNumberofVendorsinPlayfield(client.Character.PlayField);
            if (vendorcount > 0)
            {
                var firstVendorId = VendorHandler.GetFirstVendor(client.Character.PlayField);
                message.PlayfieldVendorInfo = new PlayfieldVendorInfo
                                                  {
                                                      VendorCount = vendorcount, 
                                                      FirstVendorId = firstVendorId
                                                  };
            }
            */
        }

        private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
        {
            int organizationInstance = ResolveCharacterOrganizationInstance(character);
            return organizationInstance > 0 ? organizationInstance : CapturedPrivateCityOrganizationInstance;
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            if (character == null)
            {
                return 0;
            }

            uint baseValue = character.Stats[StatIds.clan].BaseValue;
            if (baseValue > 0 && baseValue <= int.MaxValue)
            {
                return (int)baseValue;
            }

            return character.Stats[StatIds.clan].Value;
        }

        private static int ResolvePrivateCityBuildingInstance(int playfieldInstance)
        {
            return IsCapturedMontroyalPrivateCityInstance(playfieldInstance)
                       ? CapturedMontroyalPrivateCityBuildingInstance
                       : CapturedPrivateCityBuildingInstance;
        }

        /// <summary>
        /// Capture 20260806-202421 / 20260806-213039 PlayfieldAnarchyF generator payload.
        /// Mail entry MUST be IdentityType.MailTerminal (0xC773), not Terminal (0xC73D) —
        /// wrong type left the Mail Terminal missing inside the apartment.
        /// Evidence 20260806-213039: MailTerminal:79A84D @ (512, 51.7, 482);
        /// first layout uses instance 79A08A with the same type/position.
        /// </summary>
        private static byte[] CreateCapturedLuxuryApartmentGeneratorPayload(int buildingInstance)
        {
            byte[] payload =
                {
                       0x00, 0x00, 0xC7, 0x7B, 0x00, 0x5E, 0x38, 0x20,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x04,
                       0x00, 0x00, 0x17, 0x71, 0x00, 0x00, 0xC7, 0x9C,
                       0x00, 0x00, 0x17, 0x72, 0xC0, 0x00, 0x17, 0x72,
                       0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x86, 0xA0,
                       0x00, 0x00, 0x00, 0x00, 0x17, 0xE7, 0x94, 0xA0,
                       0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1E,
                       0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0xC7, 0x3D,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x00, 0x03, 0x57, 0xC1, 0x2A, 0x71,
                       // Mail Terminal — capture 20260806-213039 type=C773 (was wrongly C73D).
                       0x00, 0x00, 0xC7, 0x73, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x79, 0xA0, 0x8A, 0x00, 0x00, 0xC7, 0x3D,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x04,
                       0x00, 0x00, 0x00, 0x01, 0x57, 0xC1, 0x2A, 0x74,
                       0x00, 0x00, 0xC7, 0x48, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01,
                       0x10, 0x9D, 0xE4, 0x93, 0x00, 0x00, 0xC7, 0x3D,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x06,
                       0x00, 0x00, 0x00, 0x02, 0x57, 0xC1, 0x2A, 0x75,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                   };

            // Patch C77B building instance (bytes 4..7 big-endian).
            uint building = unchecked((uint)buildingInstance);
            payload[4] = (byte)((building >> 24) & 0xFF);
            payload[5] = (byte)((building >> 16) & 0xFF);
            payload[6] = (byte)((building >> 8) & 0xFF);
            payload[7] = (byte)(building & 0xFF);
            return payload;
        }

        private static byte[] CreateCapturedPrivateCityGeneratorPayload(int playfieldId)
        {
            if (playfieldId == CapturedOwnedMontroyalPrivateCityInstance)
            {
                return CreateCapturedOwnedMontroyalPrivateCityGeneratorPayload();
            }

            return playfieldId == CapturedMontroyalPrivateCityInstance
                       ? CreateCapturedMontroyalPrivateCityGeneratorPayload()
                       : CreateCapturedPrivateCityGeneratorPayload();
        }

        private static bool IsCapturedMontroyalPrivateCityInstance(int playfieldInstance)
        {
            return playfieldInstance == CapturedMontroyalPrivateCityInstance
                   || playfieldInstance == CapturedOwnedMontroyalPrivateCityInstance;
        }

        private static byte[] CreateCapturedMissionGeneratorPayload()
        {
            // Capture 20260718-062936 PlayfieldAnarchyF for pf 1413198 — opaque layout after PlayfieldId2.
            return new byte[]
                   {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD6, 0xD5, 0xC0,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x44,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x13, 0x00,
                       0x2B, 0x00, 0x00, 0x15, 0x00, 0x00, 0x2F, 0x00,
                       0x01, 0x14, 0x01, 0x00, 0x00, 0x00, 0x06, 0x12,
                       0x01, 0x00, 0x1E, 0x00, 0x06, 0x16, 0x03, 0x00,
                       0x12, 0x00, 0x03, 0x12, 0x03, 0x00, 0x14, 0x00,
                       0x04, 0x16, 0x02, 0x00, 0x05, 0x00, 0x02, 0x16,
                       0x02, 0x00, 0x15, 0x00, 0x06, 0x17, 0x03, 0x00,
                       0x05, 0x00, 0x03, 0x17, 0x03, 0x00, 0x05, 0x00,
                       0x08, 0x11, 0x00, 0x00, 0x17, 0x00, 0x08, 0x15,
                       0x02, 0x00, 0x06, 0x00, 0x07, 0x11, 0x00, 0x00,
                       0x29, 0x00, 0x07, 0x15, 0x00, 0x00, 0x0D, 0x00,
                       0x06, 0x11, 0x00, 0x00, 0x0A, 0x00, 0x09, 0x12,
                       0x01, 0x00, 0x2A, 0x00, 0x05, 0x12, 0x01, 0x00,
                       0x17, 0x00, 0x05, 0x13, 0x03, 0x00, 0x2A, 0x00,
                       0x05, 0x14, 0x01, 0x00, 0x2A, 0x00, 0x03, 0x11,
                       0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                       0xFF
                   };
        }

        private static byte[] CreateCapturedPrivateCityGeneratorPayload()
        {
            return new byte[]
                   {
                       0x00, 0x00, 0xC7, 0x7D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0xC4, 0x18,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x9C, 0xA0, 0x0B,
                       0x00, 0x00, 0xC7, 0x3D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x57, 0x4D, 0xF8, 0xBB, 0x00, 0x00, 0xC7, 0x48,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02,
                       0x00, 0x00, 0x00, 0x01, 0x10, 0x8E, 0xBC, 0x21,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                   };
        }

        private static byte[] CreateCapturedMontroyalPrivateCityGeneratorPayload()
        {
            return new byte[]
                   {
                       0x00, 0x00, 0xC7, 0x7D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0xC4, 0x18,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x9C, 0x60, 0x10,
                       0x00, 0x00, 0xC7, 0x3D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x57, 0x4B, 0x84, 0xAB, 0x00, 0x00, 0xC7, 0x48,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02,
                       0x00, 0x00, 0x00, 0x01, 0x10, 0x8E, 0xCA, 0x90,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                   };
        }

        private static byte[] CreateCapturedOwnedMontroyalPrivateCityGeneratorPayload()
        {
            return new byte[]
                   {
                       0x00, 0x00, 0xC7, 0x7D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0xC4, 0x18,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x9C, 0x18, 0x2E,
                       0x00, 0x00, 0xC7, 0x3D, 0x00, 0x00, 0x00, 0x01,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                       0x57, 0x51, 0x53, 0x8B, 0x00, 0x00, 0xC7, 0x48,
                       0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02,
                       0x00, 0x00, 0x00, 0x01, 0x10, 0x8D, 0x96, 0xED,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                   };
        }

        #endregion
    }
}
