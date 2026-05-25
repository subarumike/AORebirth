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

namespace ZoneEngine.Core.Packets
{
    #region Usings ...

    using System.Linq;

    using CellAO.Core.Entities;
    using CellAO.Core.Inventory;
    using CellAO.Core.Items;
    using CellAO.Core.Network;
    using CellAO.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    /// <summary>
    /// Sends explicit equipped-weapon updates to the client for right/left hand slots.
    /// </summary>
    public static class WeaponItemFullUpdate
    {
        public static void Send(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            if (character == null)
            {
                return;
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return;
            }

            SendForSlot(character, weaponPage, (int)WeaponSlots.Righthand);
            SendForSlot(character, weaponPage, (int)WeaponSlots.LeftHand);
        }

        public static void SendWeaponDefinitions(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            foreach (IInventoryPage page in character.BaseInventory.Pages.Values)
            {
                for (int slot = page.FirstSlotNumber; slot < page.FirstSlotNumber + page.MaxSlots; slot++)
                {
                    IItem item = page[slot];
                    if (item == null || !IsWeaponItem(page, item))
                    {
                        continue;
                    }

                    SendForSlot(character, page, slot);
                }
            }
        }

        public static void SendWeaponDefinition(ICharacter character, IItem item)
        {
            if (character == null || item == null)
            {
                return;
            }

            foreach (IInventoryPage page in character.BaseInventory.Pages.Values)
            {
                for (int slot = page.FirstSlotNumber; slot < page.FirstSlotNumber + page.MaxSlots; slot++)
                {
                    if (object.ReferenceEquals(page[slot], item))
                    {
                        SendForSlot(character, page, slot);
                        return;
                    }
                }
            }
        }

        private static void SendForSlot(ICharacter character, IInventoryPage page, int slot)
        {
            IItem item = page[slot];
            if (item == null || !IsWeaponItem(page, item))
            {
                return;
            }

            int quality = NormalizeValue(item.Quality);
            int lowId = NormalizeValue(item.LowID);
            int highId = NormalizeValue(item.HighID);
            int multipleCount = item.MultipleCount > 0 ? item.MultipleCount : 1;
            Identity weaponIdentity = WeaponItemIdentity.GetOrCreate(item);

            var message = new WeaponItemFullUpdateMessage
            {
                Identity = weaponIdentity,
                Unknown = 0,
                Unknown1 = 0x0b,
                Owner = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = character.Identity.Instance
                },
                PlayfieldId = character.Playfield.Identity.Instance,
                StateMachine = new Identity
                {
                    Type = (IdentityType)0x000f424f,
                    Instance = 0
                },
                Unknown2 = (short)(0x0100 | (slot & 0xff)),
                Stats = BuildStats(item, quality, lowId, highId, multipleCount),
                Unknown3 = 0
            };

            character.Send(message);

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "WeaponItemFullUpdate sent char={0} slot={1} hasItem={2} item={3}/{4} ql={5} itemId={6}",
                    character.Identity,
                    slot,
                    1,
                    lowId,
                    highId,
                    quality,
                    weaponIdentity.Instance));
        }

        private static GameTuple<CharacterStat, uint>[] BuildStats(
            IItem item,
            int quality,
            int lowId,
            int highId,
            int multipleCount)
        {
            return new[]
            {
                StatTuple(CharacterStat.Flags, (uint)NormalizeFlags(item.Flags)),
                StatTuple(CharacterStat.StaticInstance, (uint)lowId),
                StatTuple(CharacterStat.ACGItemLevel, (uint)quality),
                StatTuple(CharacterStat.ACGItemTemplateID, (uint)lowId),
                StatTuple(CharacterStat.ACGItemTemplateID2, (uint)highId),
                StatTuple(CharacterStat.MultipleCount, (uint)multipleCount),
                StatTuple(CharacterStat.Energy, 0)
            };
        }

        private static GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint>
            {
                Value1 = stat,
                Value2 = value
            };
        }

        private static bool IsWeaponItem(IInventoryPage page, IItem item)
        {
            return page is WeaponInventoryPage || item.ItemActions.Any(x => x.ActionType == ActionType.ToWield);
        }

        private static int NormalizeFlags(int flags)
        {
            return flags > 0 && flags != 1234567890 ? flags : 0x403;
        }

        private static int NormalizeValue(int value)
        {
            if (value <= 0 || value == 1234567890)
            {
                return 0;
            }

            return value;
        }
    }
}
