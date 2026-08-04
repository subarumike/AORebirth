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

    using System.Collections.Generic;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class VendingMachineFullUpdateMessageHandler :
        BaseMessageHandler<VendingMachineFullUpdateMessage, VendingMachineFullUpdateMessageHandler>
    {
        public void Send(ICharacter character, Vendor vendor)
        {
            this.Send(character, this.Filler(vendor));
        }

        /// <summary>
        /// Capture 20260723-114826 portable Buckethead Technodealer VMFU:
        /// TypeId=11, NpcIdentity set, Unk4=0xF424F, Unk5=0, Unk6=64,
        /// 8 stats, empty name string, Unk8=2, Unk9=50, empty array, Unk11=3.
        /// </summary>
        public void SendBucketheadTechnodealer(ICharacter character, Vendor vendor)
        {
            this.Send(character, this.BucketheadFiller(vendor));
        }

        /// <summary>
        /// Capture 20260721-lockpick freestanding ICC Tech Supplies VMFU:
        /// NpcIdentity none + Position/Rotation, Unknown4=111, Unknown6=0,
        /// 8 compact stats (Flags/StaticInstance/ACG*), name, Unk8=2, Unk9=50, Unk11=3.
        /// </summary>
        public void SendAreteFreestanding(ICharacter character, Vendor vendor)
        {
            this.Send(character, this.AreteFreestandingFiller(vendor));
        }

        private MessageDataFiller BucketheadFiller(Vendor vendor)
        {
            return x =>
            {
                x.Identity = vendor.Identity;
                x.Unknown = 0;
                x.Coordinates = null;
                x.Heading = null;
                x.NpcIdentity = vendor.NpcIdentity;
                x.TypeIdentifier = 0x0b;
                x.PlayfieldId = vendor.Playfield.Identity.Instance;
                x.Unknown4 = 0xf424f;
                x.Unknown5 = 0;
                x.Unknown6 = 64;
                x.Stats = new[]
                          {
                              Tuple(CharacterStat.Flags, unchecked((uint)-2147338749)),
                              Tuple(CharacterStat.StaticInstance, 99566),
                              Tuple(CharacterStat.ACGItemLevel, 1),
                              Tuple(CharacterStat.ACGItemTemplateID, 99566),
                              Tuple(CharacterStat.ACGItemTemplateID2, 99566),
                              Tuple(CharacterStat.MultipleCount, 1),
                              Tuple((CharacterStat)501, 2),
                              Tuple((CharacterStat)500, 0),
                          };
                x.Unknown7 = string.Empty;
                x.Unknown8 = 2;
                x.Unknown9 = 50;
                x.Unknown10 = new Identity[0];
                x.Unknown11 = 3;
            };
        }

        private MessageDataFiller AreteFreestandingFiller(Vendor vendor)
        {
            uint templateId = (uint)vendor.Stats[0x17].Value;
            return x =>
            {
                x.Identity = vendor.Identity;
                x.Unknown = 0;
                x.NpcIdentity = Identity.None;
                x.Coordinates = new Vector3()
                                {
                                    X = vendor.Coordinates().x,
                                    Y = vendor.Coordinates().y,
                                    Z = vendor.Coordinates().z
                                };
                x.Heading = new Quaternion()
                            {
                                X = vendor.Heading.xf,
                                Y = vendor.Heading.yf,
                                Z = vendor.Heading.zf,
                                W = vendor.Heading.wf
                            };
                x.TypeIdentifier = 0x0b;
                x.PlayfieldId = vendor.Playfield.Identity.Instance;
                x.Unknown4 = 111;
                x.Unknown5 = 0;
                x.Unknown6 = 0;
                x.Stats = new[]
                          {
                              Tuple(CharacterStat.Flags, unchecked((uint)-2147338751)),
                              Tuple(CharacterStat.StaticInstance, templateId),
                              Tuple(CharacterStat.ACGItemLevel, 1),
                              Tuple(CharacterStat.ACGItemTemplateID, templateId),
                              Tuple(CharacterStat.ACGItemTemplateID2, templateId),
                              Tuple(CharacterStat.MultipleCount, 1),
                              Tuple((CharacterStat)501, 2),
                              Tuple((CharacterStat)500, 0),
                          };
                x.Unknown7 = string.IsNullOrEmpty(vendor.Name) ? string.Empty : vendor.Name + "\0";
                x.Unknown8 = 2;
                x.Unknown9 = 50;
                x.Unknown10 = new Identity[0];
                x.Unknown11 = 3;
            };
        }

        private static GameTuple<CharacterStat, uint> Tuple(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = value };
        }

        private MessageDataFiller Filler(Vendor vendor)
        {
            return x =>
            {
                x.Identity = vendor.Identity;
                x.Unknown = 0;
                if (vendor.NpcIdentity.Instance != 0)
                {
                    x.Coordinates = null;
                    x.Heading = null;
                    x.NpcIdentity = vendor.NpcIdentity;
                }
                else
                {
                    x.Coordinates = new Vector3()
                                    {
                                        X = vendor.Coordinates().x,
                                        Y = vendor.Coordinates().y,
                                        Z = vendor.Coordinates().z
                                    };

                    x.Heading = new Quaternion()
                                {
                                    X = vendor.Heading.xf,
                                    Y = vendor.Heading.yf,
                                    Z = vendor.Heading.zf,
                                    W = vendor.Heading.wf
                                };

                    x.NpcIdentity = Identity.None;
                }
                x.TypeIdentifier = 0x0b;
                x.PlayfieldId = vendor.Playfield.Identity.Instance;
                x.Unknown4 = 0xf424f;
                x.Unknown5 = 0;
                x.Unknown6 = 111;
                List<GameTuple<CharacterStat, uint>> temp = new List<GameTuple<CharacterStat, uint>>();
                Dictionary<int, uint> templist = vendor.Stats.GetStatValues();
                SortedDictionary<int, uint> templist2 = new SortedDictionary<int, uint>();
                foreach (KeyValuePair<int, uint> kv in templist)
                {
                    templist2.Add(kv.Key, kv.Value);
                }

                foreach (KeyValuePair<int, uint> stat in templist2)
                    /*foreach (IStat stat in vendor.Stats.All)
                {
                    if (stat.NotDefault())
                    {*/
                {
                    temp.Add(
                        new GameTuple<CharacterStat, uint>()
                        {
                            Value1 = (CharacterStat)stat.Key,
                            Value2 = (uint)stat.Value
                        });
                }
                /*}
                }*/
                x.Stats = temp.ToArray();
                x.Unknown7 = vendor.Name + "\0";
                x.Unknown8 = 2;
                x.Unknown9 = 50;
                x.Unknown10 = new Identity[0];
                x.Unknown11 = 3;
            };
        }
    }
}
