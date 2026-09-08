namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Trade;

    /// <summary>
    /// Monster / NPC character. Holds template-backed spawn data not shared with players.
    /// </summary>
    public class NpcCharacter : Character, IUsableDynel
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

        /// <summary>
        /// Shop backing this NPC when its equipment includes a vending machine item. The machine is
        /// not registered as a world dynel: it exists only as the pane behind this character.
        /// </summary>
        public VendingMachine? Shop { get; private set; }

        /// <summary>
        /// Binds a shop to this NPC and flags the character so the client draws the vendor cart.
        /// </summary>
        public void AttachShop(VendingMachine machine)
        {
            ArgumentNullException.ThrowIfNull(machine);

            Shop = machine;
            machine.OwnerNpc = this;
            SetCharacterFlag(CharacterFlags.HasItemsForSale, true);
        }

        /// <summary>Using a vendor NPC opens its shop; other NPCs have no use action yet.</summary>
        public bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            VendingMachine? shop = Shop;
            if (shop == null || IsDead || Playfield == null || player.Playfield == null)
                return false;

            if (player.Playfield.Identity.Instance != Playfield.Identity.Instance)
                return false;

            if (Distance3D(player) > LootableDynel.OpenRange)
                return false;

            return Playfield.GetRequiredService<TradeService>().TryOpenShop(player, shop);
        }

        /// <summary>
        /// Shops close the moment the vendor dies, not when the corpse swap lands: the flag has to be
        /// gone before <see cref="Character.OnDeath"/> snapshots stats onto the corpse, and shoppers
        /// must not be able to keep trading with a dead vendor during the swap delay.
        /// </summary>
        public override void OnDeath(Character? killer = null)
        {
            if (IsDead)
                return;

            VendingMachine? shop = Shop;
            if (shop != null)
            {
                SetCharacterFlag(CharacterFlags.HasItemsForSale, false);
                Playfield?.GetRequiredService<TradeService>().CloseMachine(shop, "vendor died");
            }

            base.OnDeath(killer);
        }

        /// <summary>
        /// Extra spawn packets a client needs when this NPC enters visibility. A vendor's shop pane
        /// is bound by a VendingMachineFullUpdate carrying this character as its NpcIdentity.
        /// </summary>
        public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
        {
            VendingMachine? shop = Shop;
            if (shop != null && !IsDead)
                yield return shop.BuildSpawnMessage();
        }

        void SetCharacterFlag(CharacterFlags flag, bool set)
        {
            int flags = Stats.GetOrZero(CharacterStat.Flags);
            int updated = set ? flags | (int)flag : flags & ~(int)flag;
            if (updated == flags)
                return;

            Stats.Set(CharacterStat.Flags, updated, StatDetail.Base, dirty: true);
        }

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
