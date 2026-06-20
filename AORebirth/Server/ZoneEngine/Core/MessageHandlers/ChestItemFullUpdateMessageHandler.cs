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
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class ChestItemFullUpdateMessageHandler :
        BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>
    {
        private const int MissingItemStatValue = 1234567890;

        public void Send(ICharacter character, Item item, Identity sourceInventorySlot, Identity containerIdentity)
        {
            this.Send(character, this.FillData(character, item, sourceInventorySlot, containerIdentity));
        }

        private MessageDataFiller FillData(
            ICharacter character,
            Item item,
            Identity sourceInventorySlot,
            Identity containerIdentity)
        {
            return x =>
            {
                Character concreteCharacter = character as Character;
                x.Identity = containerIdentity;
                x.Unknown = 0;
                x.Unknown1 = 0x0b;
                x.Owner = character.Identity;
                x.PlayfieldId = (concreteCharacter != null) && (concreteCharacter.Playfield != null)
                                    ? concreteCharacter.Playfield.Identity.Instance
                                    : 0;
                x.StateMachine = new Identity() { Type = (IdentityType)1000015, Instance = 0 };
                x.Unknown5 = (short)(0x0100 | (sourceInventorySlot.Instance & 0xff));
                x.Stats = new[]
                          {
                              this.StatTuple(CharacterStat.Flags, this.ItemStat(item, StatIds.flags, 0)),
                              this.StatTuple(CharacterStat.StaticInstance, (uint)item.HighID),
                              this.StatTuple(CharacterStat.ACGItemLevel, (uint)item.Quality),
                              this.StatTuple(CharacterStat.ACGItemTemplateID, (uint)item.LowID),
                              this.StatTuple(CharacterStat.ACGItemTemplateID2, (uint)item.HighID),
                              this.StatTuple(CharacterStat.MultipleCount, (uint)Math.Max(1, item.MultipleCount))
                          };
                x.Unknown6 = 0;
                x.Unknown7 = 2;
                x.Unknown8 = 50;
                x.UnknownArray = new int[0];
                x.Unknown9 = 3;
            };
        }

        private GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint>() { Value1 = stat, Value2 = value };
        }

        private uint ItemStat(Item item, StatIds stat, int fallback)
        {
            int value = item.GetAttribute((int)stat);
            if (value == MissingItemStatValue)
            {
                value = fallback;
            }

            return (uint)value;
        }
    }
}
