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

    using System.Collections.Generic;
    using System.Threading;

    using CellAO.Core.Inventory;
    using CellAO.Core.Items;
    using CellAO.Core.Network;
    using CellAO.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// </summary>
    internal static class WeaponItemIdentity
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<IItem, int> WeaponInstances = new Dictionary<IItem, int>();
        private static int nextWeaponInstance = 0x25000000;

        public static Identity GetOrCreate(IItem item)
        {
            int instance = GetOrCreateInstance(item);
            return new Identity { Type = IdentityType.WeaponInstance, Instance = instance };
        }

        public static int GetOrCreateInstance(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item.Identity.Type == IdentityType.WeaponInstance && item.Identity.Instance != 0)
            {
                return item.Identity.Instance;
            }

            lock (SyncRoot)
            {
                int existing;
                if (WeaponInstances.TryGetValue(item, out existing))
                {
                    return existing;
                }

                int created = Interlocked.Increment(ref nextWeaponInstance);
                WeaponInstances[item] = created;
                return created;
            }
        }
    }

    /// <summary>
    /// </summary>
    public static class Equip
    {
        #region Public Methods and Operators

        private static bool IsWeaponHandSlot(IInventoryPage page, int slotNumber)
        {
            return page is WeaponInventoryPage
                   && (slotNumber == (int)WeaponSlots.Righthand || slotNumber == (int)WeaponSlots.LeftHand);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="page">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        public static void Send(IZoneClient client, IInventoryPage page, int slotNumber)
        {
            if (IsWeaponHandSlot(page, slotNumber))
            {
                IItem weapon = page[slotNumber];
                var action167Message = new CharacterActionMessage
                                       {
                                           Identity = client.Controller.Character.Identity,
                                           Action = CharacterActionType.ChangeAnimationAndStance,
                                           Unknown = 0
                                       };
                client.Controller.Character.Send(action167Message);

                var equipMessage = new CharacterActionMessage
                                   {
                                       Identity = client.Controller.Character.Identity,
                                       Action = CharacterActionType.Equip,
                                       Target = WeaponItemIdentity.GetOrCreate(weapon),
                                       Parameter1 = 0,
                                       Parameter2 = slotNumber,
                                       Unknown = 0
                                   };
                client.Controller.Character.Send(equipMessage);

                client.Controller.Character.Playfield.AnnounceOthers(
                    action167Message,
                    client.Controller.Character.Identity);
                client.Controller.Character.Playfield.AnnounceOthers(
                    equipMessage,
                    client.Controller.Character.Identity);
                return;
            }

            switch (slotNumber)
            {
                case 6:
                    IItem rightHandItem = page[slotNumber];
                    if (rightHandItem != null)
                    {
                        var rightHandTemplateActionMessage = new TemplateActionMessage()
                                                             {
                                                                 Identity = client.Controller.Character.Identity,
                                                                 ItemHighId = rightHandItem.HighID,
                                                                 ItemLowId = rightHandItem.LowID,
                                                                 Quality = rightHandItem.Quality,
                                                                 Unknown1 = 1,
                                                                 Unknown2 = 6,
                                                                 Placement =
                                                                     new Identity()
                                                                     {
                                                                         Type = page.Identity.Type,
                                                                         Instance = slotNumber
                                                                     },
                                                                 Unknown = 0,
                                                             };
                        client.Controller.Character.Send(rightHandTemplateActionMessage);
                    }
                    break;
                default:
                    IItem item = page[slotNumber];
                    var templateActionMessage = new TemplateActionMessage()
                                                {
                                                    Identity =
                                                        client.Controller.Character.Identity,
                                                    ItemHighId = item.HighID,
                                                    ItemLowId = item.LowID,
                                                    Quality = item.Quality,
                                                    Unknown1 = 1,
                                                    Unknown2 =
                                                        page is SocialArmorInventoryPage
                                                            ? 7
                                                            : 6,
                                                    Placement =
                                                        new Identity()
                                                        {
                                                            Type =
                                                                page.Identity
                                                                .Type,
                                                            Instance =
                                                                slotNumber
                                                        },
                                                    Unknown = 0,
                                                };
                    client.Controller.Character.Send(templateActionMessage);
                    break;
            }
        }

        #endregion
    }
}
