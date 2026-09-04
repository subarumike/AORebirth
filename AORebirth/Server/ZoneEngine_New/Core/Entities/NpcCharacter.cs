namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

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

        public override void Rebase() => RebaseWeapons();

        public override void RebaseWeapons()
        {
            ClearWeapons();

            bool armedMain = false;
            bool armedOff = false;
            bool maCombined = false;

            List<List<int>>? weapons = MobTemplate?.Weapons;
            if (weapons != null && weapons.Count > 0)
            {
                int quality = Stats.Get(CharacterStat.Level);
                if (StatCollection.IsUnset(quality) || quality < 1)
                    quality = 1;

                for (int i = 0; i < weapons.Count && i < 2; i++)
                {
                    List<int> pair = weapons[i];
                    if (pair == null || pair.Count < 2)
                        continue;

                    int lowId = pair[0];
                    int highId = pair[1];
                    if (lowId <= 0)
                        continue;

                    Item item = _items.Create(lowId, highId, quality);
                    if (!item.IsWieldableCombatWeapon())
                        continue;

                    WeaponSlot slot = i == 0 ? WeaponSlot.MainHand : WeaponSlot.OffHand;
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

        protected override byte[] CreateMovementStatus(int movementMode) =>
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            (byte)movementMode, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
        ];
    }
}
