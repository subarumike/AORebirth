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
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Sends explicit equipped-weapon updates to the client for right/left hand slots.
    /// </summary>
    public static class WeaponItemFullUpdate
    {
        private const int MissingItemStatValue = 1234567890;

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

            WeaponItemFullUpdateMessage rightHand = CreateForSlot(character, weaponPage, (int)WeaponSlots.Righthand);
            if (rightHand != null)
            {
                character.Send(rightHand);
                LogWeaponDefinition("sent-slot", character, null, rightHand, false);
            }

            WeaponItemFullUpdateMessage leftHand = CreateForSlot(character, weaponPage, (int)WeaponSlots.LeftHand);
            if (leftHand != null)
            {
                character.Send(leftHand);
                LogWeaponDefinition("sent-slot", character, null, leftHand, false);
            }
        }

        public static void SendWeaponDefinitions(ICharacter character, bool announceToPlayfield = false)
        {
            foreach (WeaponItemFullUpdateMessage message in CreateWeaponDefinitionMessages(character))
            {
                character.Send(message, announceToPlayfield);
                LogWeaponDefinition("sent", character, null, message, announceToPlayfield);
            }
        }

        public static void SendWeaponDefinition(ICharacter character, IItem item)
        {
            if (character == null || item == null || character.BaseInventory == null)
            {
                return;
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return;
            }

            // Only hand slots — bag/HUD placements must never emit WIFU (hides Reload / wrong ammotype).
            int[] handSlots = { (int)WeaponSlots.Righthand, (int)WeaponSlots.LeftHand };
            for (int i = 0; i < handSlots.Length; i++)
            {
                int slot = handSlots[i];
                if (!object.ReferenceEquals(weaponPage[slot], item))
                {
                    continue;
                }

                WeaponItemFullUpdateMessage message = CreateForSlot(character, weaponPage, slot);
                if (message != null)
                {
                    character.Send(message);
                    LogWeaponDefinition("sent-single", character, null, message, false);
                }

                return;
            }
        }

        public static WeaponItemFullUpdateMessage[] CreateWeaponDefinitionMessages(ICharacter character)
        {
            if (character == null)
            {
                return new WeaponItemFullUpdateMessage[0];
            }

            WeaponItemFullUpdateMessage captured = CreateCapturedWeaponDefinition(character);
            if (captured != null)
            {
                return new[] { captured };
            }

            CapturedEnemyCombatContract registeredContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out registeredContract))
            {
                return new WeaponItemFullUpdateMessage[0];
            }

            // Hand slots only — bag/HUD WIFU hides Actions→Reload / causes Wrong ammotype.
            if (character.BaseInventory == null)
            {
                return new WeaponItemFullUpdateMessage[0];
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return new WeaponItemFullUpdateMessage[0];
            }

            var messages = new List<WeaponItemFullUpdateMessage>();
            int[] handSlots = { (int)WeaponSlots.Righthand, (int)WeaponSlots.LeftHand };
            for (int i = 0; i < handSlots.Length; i++)
            {
                WeaponItemFullUpdateMessage message = CreateForSlot(character, weaponPage, handSlots[i]);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            return messages.ToArray();
        }

        public static WeaponItemFullUpdateMessage CreateRightHandWeaponDefinitionMessage(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return null;
            }

            WeaponItemFullUpdateMessage captured = CreateCapturedWeaponDefinition(character);
            if (captured != null)
            {
                return captured;
            }

            CapturedEnemyCombatContract registeredContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out registeredContract))
            {
                return null;
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return null;
            }

            return CreateForSlot(character, weaponPage, (int)WeaponSlots.Righthand);
        }

        private static WeaponItemFullUpdateMessage CreateCapturedWeaponDefinition(ICharacter character)
        {
            CapturedEnemyCombatContract contract;
            if (character == null
                || character.BaseInventory == null
                || !CapturedEnemyCombatRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out contract)
                || contract == null
                || !contract.IsCombatReady
                || contract.WeaponDefinition == null)
            {
                return null;
            }

            IItem item;
            string weaponFailure;
            if (!CapturedEnemyCombatRuntime.TryValidateLiveCapturedWeapon(
                    character,
                    contract,
                    out item,
                    out weaponFailure))
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                    character,
                    weaponFailure + " during visibility");
                return null;
            }

            int currentEnergy;
            if (!CapturedEnemyCombatRuntimeRegistry.TryGetCapturedWeaponEnergy(
                    character.Identity.Instance,
                    out currentEnergy))
            {
                CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime(
                    character,
                    "captured weapon Energy state is unavailable during visibility");
                return null;
            }

            return CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = character.Identity.Instance
                },
                character.Playfield.Identity.Instance,
                WeaponItemIdentity.GetOrCreate(item),
                contract.WeaponDefinition,
                currentEnergy,
                item.MultipleCount);
        }

        public static void SendRightHandWeaponDefinition(ICharacter character, bool announceToPlayfield = false)
        {
            WeaponItemFullUpdateMessage message = CreateRightHandWeaponDefinitionMessage(character);
            if (message == null)
            {
                return;
            }

            character.Send(message, announceToPlayfield);
            LogWeaponDefinition("sent-right-hand", character, null, message, announceToPlayfield);
        }

        internal static void LogObserverWeaponDefinition(
            ICharacter owner,
            ICharacter recipient,
            WeaponItemFullUpdateMessage message)
        {
            LogWeaponDefinition("visibility-sync", owner, recipient, message, false);
        }

        private static WeaponItemFullUpdateMessage CreateForSlot(
            ICharacter character,
            IInventoryPage page,
            int slot)
        {
            IItem item = page[slot];
            if (item == null || !IsCombatWeaponItem(page, item, slot))
            {
                return null;
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

            return message;
        }

        private static GameTuple<CharacterStat, uint>[] BuildStats(
            IItem item,
            int quality,
            int lowId,
            int highId,
            int multipleCount)
        {
            // Energy must not become uint.MaxValue (-1) when the item attribute is missing —
            // that is the live "energy weapon / NoAmmo" marker and the client hides Actions→Reload.
            // BANKA (working Reload): always send Energy=0 on WIFU so the client enables Reload.
            var stats = new List<GameTuple<CharacterStat, uint>>
            {
                StatTuple(CharacterStat.Flags, (uint)NormalizeFlags(item.Flags)),
                StatTuple(CharacterStat.StaticInstance, (uint)lowId),
                StatTuple(CharacterStat.ACGItemLevel, (uint)quality),
                StatTuple(CharacterStat.ACGItemTemplateID, (uint)lowId),
                StatTuple(CharacterStat.ACGItemTemplateID2, (uint)highId),
                StatTuple(CharacterStat.MultipleCount, (uint)multipleCount),
                // BANKA working Reload: always Energy=0 (never -1 / uint.MaxValue).
                StatTuple(CharacterStat.Energy, 0)
            };
            AddStatIfPresent(stats, CharacterStat.AttackDelay, item.GetAttribute((int)StatIds.itemdelay));
            AddStatIfPresent(stats, CharacterStat.RechargeDelay, item.GetAttribute((int)StatIds.rechargedelay));
            return stats.ToArray();
        }

        private static void AddStatIfPresent(
            ICollection<GameTuple<CharacterStat, uint>> stats,
            CharacterStat stat,
            int value)
        {
            if (value == MissingItemStatValue)
            {
                return;
            }

            stats.Add(StatTuple(stat, unchecked((uint)value)));
        }

        private static GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint>
            {
                Value1 = stat,
                Value2 = value
            };
        }

        private static bool IsCombatWeaponItem(IInventoryPage page, IItem item, int slot)
        {
            // Only left/right hand are combat weapons. HUD/Util/NCU/Belt items on WeaponPage
            // must NOT get WeaponItemFullUpdate — client then treats them as guns ("Wrong ammotype").
            if (page is WeaponInventoryPage)
            {
                return slot == (int)WeaponSlots.Righthand || slot == (int)WeaponSlots.LeftHand;
            }

            // Vehicles (yalm/water/ground) have ToWield but are HUD items, not guns.
            int isVehicle = item.GetAttribute((int)StatIds.isvehicle);
            if (isVehicle != MissingItemStatValue && isVehicle != 0)
            {
                return false;
            }

            return item.ItemActions.Any(x => x.ActionType == ActionType.ToWield)
                   || HasWeaponStats(item);
        }

        private static bool IsWeaponItem(IInventoryPage page, IItem item)
        {
            return IsCombatWeaponItem(page, item, (int)WeaponSlots.Righthand);
        }

        private static bool HasWeaponStats(IItem item)
        {
            return NormalizeValue(item.GetAttribute((int)StatIds.mindamage)) > 0
                   || NormalizeValue(item.GetAttribute((int)StatIds.maxdamage)) > 0;
        }

        private static int NormalizeFlags(int flags)
        {
            return flags > 0 && flags != MissingItemStatValue ? flags : 0x403;
        }

        private static int NormalizeValue(int value)
        {
            if (value <= 0 || value == MissingItemStatValue)
            {
                return 0;
            }

            return value;
        }

        private static void LogWeaponDefinition(
            string phase,
            ICharacter owner,
            ICharacter recipient,
            WeaponItemFullUpdateMessage message,
            bool announceToPlayfield)
        {
            if (!ShouldLogWeaponDefinition(owner))
            {
                return;
            }

            int slot = message.Unknown2 & 0xff;
            string recipientText = recipient == null ? "none" : recipient.Identity.ToString();
            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "WeaponItemFullUpdate {0} owner={1} recipient={2} weapon={3} slot={4} ownerField={5} playfield={6} stats={7} announce={8}",
                    phase,
                    owner == null ? Identity.None : owner.Identity,
                    recipientText,
                    message.Identity,
                    slot,
                    message.Owner,
                    message.PlayfieldId,
                    message.Stats == null ? 0 : message.Stats.Length,
                    announceToPlayfield ? 1 : 0));
        }

        private static bool ShouldLogWeaponDefinition(ICharacter owner)
        {
            return owner != null
                   && owner.Playfield != null
                   && owner.Playfield.Identity.Instance == 127
                   && owner.Stats[StatIds.monsterdata].Value == 26092
                   && string.Equals(owner.Name, "Thief", System.StringComparison.Ordinal);
        }
    }
}
