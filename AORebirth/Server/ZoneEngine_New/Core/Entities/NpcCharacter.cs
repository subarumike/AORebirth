namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Trade;

    /// <summary>
    /// Monster / NPC character. Holds template-backed spawn data not shared with players.
    /// </summary>
    public class NpcCharacter : Character, IUsableDynel
    {
        public const int EquipmentCapacity = 50;

        readonly IItemBuilder _items;
        readonly Dictionary<int, string> _equipmentSawHashes = new();
        readonly HashSet<int> _combatWeaponSlots = new();

        public NpcCharacter(Identity identity, IItemBuilder items)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(items);
            _items = items;
            Equipment = new Container(IdentityType.WeaponPage, offset: 0, capacity: EquipmentCapacity, instanceId: identity.Instance);
        }

        /// <summary>Source mob template when this NPC was spawned from GameData mob templates.</summary>
        public MobTemplate? MobTemplate { get; set; }

        /// <summary>Granted only by an accepted runtime combat adapter, never by a template or name.</summary>
        public virtual bool AcceptsPlayerCombatNanos => false;
        /// <summary>Interpolated worn items and expanded monster weapons. Capacity 50.</summary>
        public Container Equipment { get; }

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

            if (GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
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

        protected override void RemoveFromWorldAfterDeath()
        {
            if (Playfield == null)
                return;

            Playfield.GetRequiredService<SpawnService>().DespawnNpc(this);
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
        /// Announces a WeaponInstance after SCFU for the visual right-hand weapon when it has a
        /// non-zero WeaponMesh. Monster weapons are never announced; SAW and AttackInfo carry them.
        /// </summary>
        public override List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
        {
            var messages = new List<WeaponItemFullUpdateMessage>();
            if (VisualRightHand == null)
                return messages;

            WeaponItemFullUpdateMessage? visible = TryBuildWeaponItemFullUpdate(
                VisualRightHand,
                (int)WeaponSlots.Righthand,
                visibleHand: true);
            if (visible != null)
                messages.Add(visible);
            return messages;
        }

        public override InfoPacketMessage BuildInfoPacket()
        {
            return BuildCharacterInfoPacket(1, InfoPacketType.Monster, string.Empty, string.Empty);
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

        public override bool IsEvading => Brain?.IsEvading == true;

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

        /// <summary>
        /// Mints template equipment at this NPC's level and expands EquipMonsterWeapon hashes.
        /// Call after stats are applied and before <see cref="Rebase"/>.
        /// </summary>
        public void FillEquipment(IGameData gameData, IZoneLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(gameData);

            MobTemplate? template = MobTemplate;
            if (template == null)
                return;

            _equipmentSawHashes.Clear();
            _combatWeaponSlots.Clear();
            int quality = Stats.GetOrOne(CharacterStat.Level);
            List<List<int>> pairs = template.Equipment;
            for (int i = 0; i < pairs.Count; i++)
            {
                List<int> pair = pairs[i];
                if (pair == null || pair.Count < 1 || pair[0] <= 0)
                    continue;

                int lowId = pair[0];
                int highId = pair.Count >= 2 && pair[1] > 0 ? pair[1] : lowId;
                Item item = _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Other);
                if (!TryAddEquipment(item, logger))
                    return;

                TryExpandMonsterWeapon(gameData, item, quality, logger);
            }
        }

        void TryExpandMonsterWeapon(IGameData gameData, Item item, int quality, IZoneLogger? logger)
        {
            if ((ItemClass)item.GetStat(CharacterStat.ItemClass) != ItemClass.Npc)
                return;
            if (!TryFindEquipMonsterWeaponHash(item, out string weaponHash))
                return;
            if (!gameData.TryGetMonsterWeapon(weaponHash, out int[] ids))
            {
                logger?.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Monster weapon hash '{0}' not found for NPC id={1}",
                        weaponHash,
                        Identity.Instance));
                return;
            }

            int lowId = ids[0];
            int highId = ids.Length >= 2 && ids[1] > 0 ? ids[1] : lowId;
            if (lowId <= 0)
                return;

            TryAddEquipment(
                _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Other),
                logger,
                combatWeapon: true,
                sawHash: weaponHash);
        }

        /// <summary>
        /// Adds a server-assigned combat weapon for NPCs that have no monster weapon equipper
        /// (generated mission actors). Call <see cref="RebaseWeapons"/> afterwards.
        /// </summary>
        internal bool EquipCombatWeapon(Item weapon, IZoneLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(weapon);
            return TryAddEquipment(weapon, logger, combatWeapon: true);
        }

        bool TryAddEquipment(Item item, IZoneLogger? logger, bool combatWeapon = false, string? sawHash = null)
        {
            int slot = Equipment.FindFreeSlot();
            if (slot < 0)
            {
                logger?.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NPC equipment full id={0} capacity={1}",
                        Identity.Instance,
                        Equipment.Capacity));
                return false;
            }

            if (!Equipment.Add(slot, item))
                return false;

            if (combatWeapon)
                _combatWeaponSlots.Add(slot);
            if (!string.IsNullOrEmpty(sawHash))
                _equipmentSawHashes[slot] = sawHash;

            return true;
        }

        internal static bool TryFindEquipMonsterWeaponHash(Item item, out string hash)
        {
            hash = string.Empty;
            foreach (KeyValuePair<EventType, List<ItemSpell>> pair in item.SpellList)
            {
                List<ItemSpell>? spells = pair.Value;
                if (spells == null)
                    continue;

                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell spell = spells[i];
                    if (!spell.Is(FunctionType.EquipMonsterWeapon))
                        continue;
                    if (TryReadAoHash(spell, out hash))
                        return true;
                }
            }

            return false;
        }

        static bool TryReadAoHash(ItemSpell spell, out string hash)
        {
            hash = string.Empty;
            for (int i = 0; i < spell.ArgumentCount; i++)
            {
                if (spell.TryReadString(i, out string text) && text.Length > 0)
                {
                    hash = text;
                    return true;
                }

                if (!spell.TryReadInt(i, out int packed))
                    continue;
                if (!TryDecodeAoHash(packed, out hash))
                    continue;
                return true;
            }

            return false;
        }

        static bool TryDecodeAoHash(int packed, out string hash)
        {
            char a = (char)((packed >> 24) & 0xFF);
            char b = (char)((packed >> 16) & 0xFF);
            char c = (char)((packed >> 8) & 0xFF);
            char d = (char)(packed & 0xFF);
            if (!IsHashChar(a) || !IsHashChar(b) || !IsHashChar(c) || !IsHashChar(d))
            {
                hash = string.Empty;
                return false;
            }

            hash = string.Concat(a, b, c, d);
            return true;
        }

        static bool IsHashChar(char value)
            => (value >= 'A' && value <= 'Z')
                || (value >= 'a' && value <= 'z')
                || (value >= '0' && value <= '9');

        public override void Rebase()
        {
            RebaseStats();
            RebaseWeapons();
        }

        /// <summary>
        /// NPC stats come from the mob template, buffs, and "lootgiver" items only; ordinary
        /// equipment never modifies them.
        /// </summary>
        public override void RebaseStats()
        {
            Stats.ClearBonuses(dirty: true);
            foreach (KeyValuePair<int, Item> slot in Equipment.EnumerateSlots())
            {
                if (slot.Value.Definition?.Name?.Contains("lootgiver", StringComparison.OrdinalIgnoreCase) == true)
                    WearBonusApplier.ApplyItem(slot.Value, includeWield: false, Stats);
            }

            ApplyBuffBonuses();
            RebaseWearAppearance();
        }

        /// <summary>
        /// NPCs keep one inventory rather than player wear pages, so everything they carry feeds
        /// their look.
        /// </summary>
        protected override IEnumerable<Container> AppearanceWearPages => [Equipment];

        /// <summary>
        /// First melee/ranged equipment item; later ones are ignored. Drawn in the right hand and
        /// overrides monster weapon damage.
        /// </summary>
        public Item? VisualRightHand { get; private set; }

        /// <summary>
        /// Visual weapons fill the hands; monster weapons (equipper-expanded or assigned through
        /// <see cref="EquipCombatWeapon"/>) are the only attack cycles. No monster weapon means
        /// no auto-attack.
        /// </summary>
        public override void RebaseWeapons()
        {
            ClearWeapons();
            AssignVisualHands();
            ArmMonsterWeapons();
            ResetAllWeaponAttacks();
        }

        void AssignVisualHands()
        {
            VisualRightHand = null;
            ClearHandMesh(RightHandMeshPosition);
            ClearHandMesh(LeftHandMeshPosition);

            foreach (KeyValuePair<int, Item> slot in Equipment.EnumerateSlots())
            {
                if (_combatWeaponSlots.Contains(slot.Key) || !IsVisualWeapon(slot.Value))
                    continue;

                VisualRightHand = slot.Value;
                int meshId = StatCollection.Normalize(slot.Value.GetStat(CharacterStat.WeaponMesh));
                if (meshId > 0)
                    SetHandMesh(RightHandMeshPosition, meshId, overrideTextureId: 0);
                return;
            }
        }

        void ArmMonsterWeapons()
        {
            int armed = 0;
            Item? rangeSource = VisualRightHand;
            foreach (KeyValuePair<int, Item> slot in Equipment.EnumerateSlots())
            {
                if (armed >= MaxNpcCombatWeapons)
                    return;
                if (!_combatWeaponSlots.Contains(slot.Key))
                    continue;

                _equipmentSawHashes.TryGetValue(slot.Key, out string? sawHash);
                WeaponSlot logical = (WeaponSlot)((int)WeaponSlot.Npc0 + armed);
                ArmFromItem(logical, slot.Value, wireSlot: armed, sawHash: sawHash);
                rangeSource ??= slot.Value;
                CharacterWeapon weapon = Weapons[logical];
                weapon.DamageOverride = VisualRightHand;
                weapon.RangeSource = rangeSource;
                armed++;
            }
        }

        static bool IsVisualWeapon(Item item)
            => (item.GetWeaponFlags() & (WeaponFlags.Melee | WeaponFlags.Ranged)) != 0;

        const int RightHandMeshPosition = 1;
        const int LeftHandMeshPosition = 2;
        const int MaxNpcCombatWeapons = 8;
    }
}
