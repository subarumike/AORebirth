namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Metrics;
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

        /// <summary>False for vendors and other non-combat NPCs.</summary>
        public bool Attackable { get; set; } = true;

        /// <summary>
        /// Shop backing this NPC when its equipment includes a vending machine item. The machine is
        /// not registered as a world dynel: it exists only as the pane behind this character.
        /// </summary>
        public VendingMachine? Shop { get; private set; }

        public NpcBrain? Brain { get; private set; }

        public bool IsAiBusy => Brain?.IsBusy == true;

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

        public void AttachBrain(NpcBrain brain)
        {
            ArgumentNullException.ThrowIfNull(brain);
            Brain = brain;
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

            Brain?.OnOwnerDied();

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

        /// <summary>
        /// Announces equipped combat weapons after SCFU only when they need owner-linked WIFU.
        /// Tag-backed NPC natural weapons stay SAW/AttackInfo-only (no fist/WIFU).
        /// </summary>
        public override List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
        {
            var messages = new List<WeaponItemFullUpdateMessage>();
            foreach (CharacterWeapon? armed in Weapons.Values)
            {
                if (armed?.Item == null || armed.WireSlot < 0)
                    continue;

                WeaponItemFullUpdateMessage? message = TryBuildWeaponItemFullUpdate(armed.Item, armed.WireSlot, armed);
                if (message != null)
                    messages.Add(message);
            }

            return messages;
        }

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

        public override void Tick(double deltaTime)
        {
            if (!IsDead)
                Brain?.Tick(deltaTime);

            TickStallWatch.Stage("npc.base", Identity.Instance);
            base.Tick(deltaTime);
        }

        protected override void OnDamaged(Character attacker, int hpRemoved, HitType hitType)
        {
            if (attacker.IsPlayer)
                Brain?.AddThreat(attacker.Identity, hpRemoved);
        }

        /// <summary>
        /// Fight ended without a kill. Restores HP, clears fight state, and drops kill credit.
        /// </summary>
        public void OnReset()
        {
            ClearKillRewards();
            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            if (maxHealth > 0)
                Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base, dirty: true);
            SetFightingTarget(Identity.None);
            // Path settle is announced by NpcBrain.StopPathing before OnReset.
            Motor.ClearPath();
        }

        public override void Rebase()
        {
            RebaseStats();
            RebaseWeapons();
        }

        public override void RebaseStats()
        {
            // NPCs carry no equipment bonuses, so buffs own the whole bonus layer.
            Stats.ClearBonuses(dirty: true);
            ApplyBuffBonuses();
        }

        public override void RebaseWeapons()
        {
            ClearWeapons();

            int quality = Stats.GetOrOne(CharacterStat.Level);
            int armed = 0;
            bool maCombined = false;

            // Prefer template Weapons (LowId/HighId/Hash). Fall back to Equipment (no SAW hash).
            if (!TryArmNpcWeapons(MobTemplate?.Weapons, quality, ref armed, ref maCombined))
                TryArmNpcEquipment(MobTemplate?.Equipment, quality, ref armed, ref maCombined);

            if (armed == 0)
            {
                FinishWeaponRebase(_items, armedMain: false, armedOff: false, maCombined: false);
                return;
            }

            if (maCombined)
                ArmMartialArtsFist(_items, WeaponSlot.CombinedMA);

            ResetAllWeaponAttacks();
        }

        bool TryArmNpcWeapons(
            List<MobWeaponEntry>? source,
            int quality,
            ref int armed,
            ref bool maCombined)
        {
            if (source == null || source.Count == 0)
                return false;

            bool armedAny = false;
            for (int i = 0; i < source.Count && armed < MaxNpcCombatWeapons; i++)
            {
                MobWeaponEntry? entry = source[i];
                if (entry == null)
                    continue;

                int lowId = entry.LowId;
                int highId = entry.HighId > 0 ? entry.HighId : lowId;
                if (lowId <= 0)
                    continue;

                Item item = _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Other);
                if (!item.IsWieldableCombatWeapon())
                    continue;

                WeaponSlot slot = (WeaponSlot)((int)WeaponSlot.Npc0 + armed);
                ArmFromItem(slot, item, wireSlot: armed, sawHash: entry.Hash);
                armed++;
                armedAny = true;
                if (item.IsMaCombinedWeapon())
                    maCombined = true;
            }

            return armedAny;
        }

        bool TryArmNpcEquipment(
            List<List<int>>? source,
            int quality,
            ref int armed,
            ref bool maCombined)
        {
            if (source == null || source.Count == 0)
                return false;

            bool armedAny = false;
            for (int i = 0; i < source.Count && armed < MaxNpcCombatWeapons; i++)
            {
                List<int> pair = source[i];
                if (pair == null || pair.Count < 1)
                    continue;

                int lowId = pair[0];
                int highId = pair.Count >= 2 ? pair[1] : lowId;
                if (lowId <= 0)
                    continue;

                Item item = _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Other);
                if (!item.IsWieldableCombatWeapon())
                    continue;

                WeaponSlot slot = (WeaponSlot)((int)WeaponSlot.Npc0 + armed);
                ArmFromItem(slot, item, wireSlot: armed);
                armed++;
                armedAny = true;
                if (item.IsMaCombinedWeapon())
                    maCombined = true;
            }

            return armedAny;
        }

        const int MaxNpcCombatWeapons = 8;
    }
}
