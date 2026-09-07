namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// Monster / NPC character. Holds template-backed spawn data not shared with players.
    /// </summary>
    public class NpcCharacter : Character
    {
        readonly IItemBuilder _items;

        public NpcCharacter(Identity identity, IItemBuilder items)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(items);
            _items = items;
        }

        /// <summary>Source mob template when this NPC was spawned from GameData mob templates.</summary>
        public MobTemplate? MobTemplate { get; set; }

        /// <summary>NPC WIFUs not implemented yet.</summary>
        public override List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
            => new();

        public override InfoPacketMessage BuildInfoPacket()
        {
            return new InfoPacketMessage
            {
                Identity = Identity,
                Unknown = 1,
                Type = InfoPacketType.Monster,
                Info = new MonsterInfoPacket
                {
                    Unknown1 = 1,
                    Profession = ClampToByte(Stats.GetOrZero(CharacterStat.Profession)),
                    Level = ClampToByte(Stats.GetOrOne(CharacterStat.Level)),
                    TitleLevel = ClampToByte(Stats.GetOrOne(CharacterStat.TitleLevel)),
                    VisualProfession = ClampToByte(Stats.GetOrZero(CharacterStat.VisualProfession)),
                    Unknown2 = 0,
                    CurrentHealth = Stats.GetOrZero(CharacterStat.Health),
                    MaxHealth = Stats.GetOrZero(CharacterStat.MaxHealth),
                    Unknown3 = 0,
                    OrganizationId = 0,
                    Unknown8 = 1234567890,
                    Unknown9 = 1234567890,
                    Unknown10 = 1234567890
                }
            };
        }

        /// <summary>
        /// Fight ended without a kill. Clears kill credit.
        /// Later: restore HP, leash home, clear FightingTarget.
        /// </summary>
        public void OnReset()
            => ClearKillRewards();

        public override void Rebase() => RebaseWeapons();

        public override void RebaseWeapons()
        {
            ClearWeapons();

            bool armedMain = false;
            bool armedOff = false;
            bool maCombined = false;

            List<List<int>>? equipment = MobTemplate?.Equipment;
            if (equipment != null && equipment.Count > 0)
            {
                int quality = Stats.GetOrOne(CharacterStat.Level);

                for (int i = 0; i < equipment.Count; i++)
                {
                    if (armedMain && armedOff)
                        break;

                    List<int> pair = equipment[i];
                    if (pair == null || pair.Count < 1)
                        continue;

                    int lowId = pair[0];
                    int highId = pair.Count >= 2 ? pair[1] : lowId;
                    if (lowId <= 0)
                        continue;

                    Item item = _items.Create(lowId, highId, quality, ItemSource.Other);
                    if (!item.IsWieldableCombatWeapon())
                        continue;

                    WeaponSlot slot = ResolveHandSlot(i, equipment.Count, armedMain, armedOff);
                    if (slot == WeaponSlot.None)
                        continue;

                    ArmFromItem(slot, item);
                    if (slot == WeaponSlot.MainHand)
                        armedMain = true;
                    else
                        armedOff = true;

                    if (item.IsMaCombinedWeapon())
                        maCombined = true;
                }
            }

            FinishWeaponRebase(_items, armedMain, armedOff, maCombined);
        }

        static WeaponSlot ResolveHandSlot(int equipmentSlot, int equipmentCount, bool armedMain, bool armedOff)
        {
            if (equipmentSlot == (int)WeaponSlots.Righthand)
                return armedMain ? WeaponSlot.None : WeaponSlot.MainHand;
            if (equipmentSlot == (int)WeaponSlots.LeftHand)
                return armedOff ? WeaponSlot.None : WeaponSlot.OffHand;
            if (equipmentCount > (int)WeaponSlots.LeftHand)
                return WeaponSlot.None;
            if (!armedMain)
                return WeaponSlot.MainHand;
            if (!armedOff)
                return WeaponSlot.OffHand;
            return WeaponSlot.None;
        }
    }
}
