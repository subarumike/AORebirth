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
        List<Item>? _carriedLoot;

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

        /// <summary>
        /// False for NoCombat NPCs: vendors and other non-combat NPCs. A NoCombat NPC cannot be attacked, attack, cast or
        /// be cast on.
        /// </summary>
        public bool Attackable { get; set; } = true;

        /// <summary>
        /// Starts fights with players who come near (BreedHostility). False for a quest prop's guard, which goes for a
        /// player only when they tamper with the prop (see <see cref="Missions.QuestPropService"/>).
        /// </summary>
        public bool AttacksOnSight { get; set; } = true;

        /// <summary>
        /// The NPC's body cannot move: its MonsterData Features (stat 224, copied on at spawn) lack
        /// <see cref="GameData.MonsterBodyFeatures.CanMove"/>. It never paths, wanders or chases, and fights from where it
        /// stands. Unknown Features count as mobile.
        /// </summary>
        public bool IsImmobile
            => Stats.TryGetValue(CharacterStat.Features, out int features) && !StatCollection.IsUnset(features)
                && GameData.MonsterBodyFeatures.IsImmobile(features);

        /// <summary>
        /// Shop backing this NPC when its equipment includes a vending machine item. The machine is
        /// not registered as a world dynel: it exists only as the pane behind this character.
        /// </summary>
        public VendingMachine? Shop { get; private set; }

        public NpcBrain? Brain { get; private set; }

        /// <summary>The NpcScripts program this NPC runs; null for the (vast) majority that run none.</summary>
        public NpcScripts.NpcScriptRunner? Scripts { get; private set; }

        public void AttachScript(NpcScripts.NpcScriptRunner runner)
        {
            ArgumentNullException.ThrowIfNull(runner);
            Scripts?.OnDespawn();
            Scripts = runner;
        }

        /// <summary>The pet state when this NPC is someone's summoned pet; null for ordinary NPCs.</summary>
        public Pets.PetController? Pet { get; private set; }

        /// <summary>The character that summoned this pet; null for ordinary NPCs.</summary>
        public Character? PetOwner => Pet?.Owner;

        /// <summary>Makes this NPC a pet. Called before it is first sent, so observers get it as one.</summary>
        internal void BindPet(Pets.PetController controller)
        {
            ArgumentNullException.ThrowIfNull(controller);
            Pet = controller;
        }

        internal void UnbindPet() => Pet = null;

        /// <summary>
        /// The hash this NPC was spawned from (a family/META hash from Spawns.json or a command). The resolved
        /// <see cref="MobTemplate"/> carries the child leaf hash instead. Quests name NPCs by this one.
        /// </summary>
        public string? SpawnHash { get; init; }

        public bool IsAiBusy => Brain?.IsBusy == true;

        /// <summary>
        /// Holds an item SpawnItem gave this NPC until its corpse loots it alongside the item table roll. False once
        /// the NPC already carries a full corpse's worth.
        /// </summary>
        public bool TryCarryLoot(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _carriedLoot ??= new List<Item>();
            if (_carriedLoot.Count >= Corpse.LootCapacity)
                return false;

            _carriedLoot.Add(item);
            return true;
        }

        /// <summary>The carried items, handed to the corpse once; later calls return null.</summary>
        public IReadOnlyList<Item>? TakeCarriedLoot()
        {
            List<Item>? carried = _carriedLoot;
            _carriedLoot = null;
            return carried;
        }

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

            if (Scripts is { } scripts && !IsDead && player.Playfield == Playfield && GetEdgeDistanceTo(player) <= LootableDynel.OpenRange)
                scripts.Raise(NpcScripts.NpcScriptTrigger.Used);

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

            ReleaseForDeath("vendor died");
            Brain?.ScriptReleaseMovement();
            Scripts?.Raise(NpcScripts.NpcScriptTrigger.Death);
            base.OnDeath(killer);
        }

        /// <summary>
        /// GM <c>.despawn</c>: the same NPC cleanup as a death, then <see cref="Character.DespawnAsDeath"/>. The NPC always
        /// leaves the world, including kinds that keep their dead body around for a while.
        /// </summary>
        public override void DespawnAsDeath()
        {
            if (IsDead)
                return;

            ReleaseForDeath("vendor despawned");
            base.DespawnAsDeath();
            if (Playfield != null)
                Playfield.GetRequiredService<SpawnService>().DespawnNpc(this);
        }

        /// <summary>What an NPC lets go of when it dies or is despawned as dead: its brain, pet bookkeeping, pets and shop.</summary>
        void ReleaseForDeath(string shopCloseReason)
        {
            // A charmed NPC dies as itself, not as a pet: the charm ends first, so its body and rewards are its own.
            if (Pet is { IsCharmed: true })
                Playfield?.GetService<Pets.PetService>()?.EndCharm(this, "died", turnOnOwner: false);

            Brain?.OnOwnerDied();

            // A pet's death gives nobody a reward and leaves no body to loot.
            if (Pet != null)
            {
                ClearKillRewards();
                Playfield?.GetService<Pets.PetService>()?.OnPetDied(this);
            }

            // An NPC's pets fall with it.
            if (OwnedPets.Count > 0)
                Playfield?.GetService<Pets.PetService>()?.DismissAll(this, "owner died");

            VendingMachine? shop = Shop;
            if (shop != null)
            {
                SetCharacterFlag(CharacterFlags.HasItemsForSale, false);
                Playfield?.GetRequiredService<TradeService>().CloseMachine(shop, shopCloseReason);
            }
        }

        /// <summary>Pets leave no corpse.</summary>
        protected override void SpawnDeathCorpse()
        {
            if (Pet == null)
                base.SpawnDeathCorpse();
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

            if (BuildKnubotStatMessage() is { } knubot)
                yield return knubot;
        }

        /// <summary>
        /// HasKnubotData for an NPC with a Knubot script: without it the client's use action on the NPC
        /// is a trade, with it the client opens the KnuBot chat.
        /// </summary>
        protected StatMessage? BuildKnubotStatMessage()
        {
            if (IsDead || Playfield?.GetService<Knubot.KnubotCatalog>() is not { } knubot || !knubot.TryResolve(this, out _))
                return null;

            return BuildKnubotDataMessage(1);
        }

        /// <summary>HasKnubotData with an explicit value: 0 takes the Knubot chat away from a client that was told 1.</summary>
        internal StatMessage BuildKnubotDataMessage(uint value) => new()
        {
            Identity = Identity,
            Stats = [new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.HasKnubotData, Value2 = value }]
        };

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
            // Before the brain, so a walk the program asks for this tick is seen by this tick's think. A waiting
            // runner is one compare; a dead NPC still runs its Death handler until the corpse swap takes it.
            if (Scripts != null)
            {
                TickStallWatch.Stage("npc.script", Identity.Instance);
                Scripts.Tick(Environment.TickCount64);
            }

            if (!IsDead)
                Brain?.Tick(deltaTime);

            TickStallWatch.Stage("npc.base", Identity.Instance);
            base.Tick(deltaTime);
        }

        protected override void OnDamaged(Character attacker, int hpRemoved, HitType hitType)
        {
            // A player's pet draws threat like its owner does, so a mob turns on the pet that hits it, and its owner
            // takes a share (NpcBrain.AddThreatFrom). Another NPC's hit counts the same way: the victim fights back and
            // calls its kind for help (Gameplay.json socialAggro.npcVsNpcDefaultRadius) as it would against a player.
            if ((attacker.IsPlayer || attacker is NpcCharacter) && !ReferenceEquals(attacker, this))
                Brain?.AddThreatFrom(attacker, hpRemoved);

            // The percent is only computed when the program listens for health thresholds.
            if (Scripts is { } scripts && scripts.Program.Subscribes(NpcScripts.NpcScriptTrigger.HealthBelow))
            {
                int max = Stats.GetOrZero(CharacterStat.MaxHealth);
                if (max > 0)
                    scripts.Raise(NpcScripts.NpcScriptTrigger.HealthBelow, (int)Math.Clamp(Stats.GetOrZero(CharacterStat.Health) * 100L / max, 0, 100));
            }
        }

        public override bool IsEvading => Brain?.IsEvading == true;

        /// <summary>
        /// Fight ended without a kill. Restores HP, clears fight state, and drops kill credit.
        /// </summary>
        public void OnReset()
        {
            // An NPC that resets sends its pets away.
            if (OwnedPets.Count > 0)
                Playfield?.GetService<Pets.PetService>()?.DismissAll(this, "owner reset");

            ClearKillRewards();
            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            if (maxHealth > 0)
                Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base, dirty: true);
            SetFightingTarget(Identity.None);
            // Path settle is announced by NpcBrain.StopPathing before OnReset.
            Motor.ClearPath();

            if (Scripts is { } scripts)
            {
                scripts.Raise(NpcScripts.NpcScriptTrigger.CombatEnd);
                scripts.Raise(NpcScripts.NpcScriptTrigger.Reset);
            }
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
            TickStallWatch.Stage("equip.bossmod", Identity.Instance);
            (HashSet<int> bossModifiers, int keptBossModifier) = PickBossModifier(pairs, quality);
            for (int i = 0; i < pairs.Count; i++)
            {
                List<int> pair = pairs[i];
                if (pair == null || pair.Count < 1 || pair[0] <= 0)
                    continue;

                // Only the one boss modifier picked for this spawn is equipped; the rest are discarded.
                if (bossModifiers.Contains(i) && i != keptBossModifier)
                    continue;

                int lowId = pair[0];
                int highId = pair.Count >= 2 && pair[1] > 0 ? pair[1] : lowId;
                TickStallWatch.Stage("equip.create", lowId);
                Item item = _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Other);
                TickStallWatch.Stage("equip.add", lowId);
                if (!TryAddEquipment(item, logger))
                    return;

                TickStallWatch.Stage("equip.weapon", lowId);
                TryExpandMonsterWeapon(gameData, item, quality, logger);
                ApplyComboName(gameData, item);
            }
        }

        /// <summary>A ComboNameGen item (the boss "Name Giver") renames the NPC from ComboNames.json by its Clan stat.</summary>
        void ApplyComboName(IGameData gameData, Item item)
        {
            if (!HasFunction(item, FunctionType.ComboNameGen))
                return;
            if (gameData.ComboNames.TryGenerate(Stats.GetOrZero(CharacterStat.Clan), Random.Shared, out string name))
                Name = name;
        }

        static bool HasFunction(Item item, FunctionType function)
        {
            foreach (KeyValuePair<EventType, List<ItemSpell>> pair in item.SpellList)
            {
                List<ItemSpell>? spells = pair.Value;
                if (spells == null)
                    continue;

                for (int i = 0; i < spells.Count; i++)
                {
                    if (spells[i].Is(function))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Equipment items named "Boss Modifier Item (…)" are alternatives: each spawn keeps one at random.</summary>
        const string BossModifierPrefix = "Boss Modifier Item";

        /// <summary>
        /// The template equipment entries that are boss modifiers, and the one picked for this spawn (-1 when there
        /// are none). Names come from the item templates, so no instance is created for the discarded ones.
        /// </summary>
        (HashSet<int> Modifiers, int Kept) PickBossModifier(List<List<int>> pairs, int quality)
        {
            var modifiers = new HashSet<int>();
            for (int i = 0; i < pairs.Count; i++)
            {
                List<int> pair = pairs[i];
                if (pair == null || pair.Count < 1 || pair[0] <= 0)
                    continue;

                int lowId = pair[0];
                int highId = pair.Count >= 2 && pair[1] > 0 ? pair[1] : lowId;
                string? name = _items.CreateTemplate(lowId, highId, quality).Name;
                if (name != null && name.StartsWith(BossModifierPrefix, StringComparison.OrdinalIgnoreCase))
                    modifiers.Add(i);
            }

            if (modifiers.Count == 0)
                return (modifiers, -1);

            int pick = Random.Shared.Next(modifiers.Count);
            foreach (int index in modifiers)
            {
                if (pick-- == 0)
                    return (modifiers, index);
            }

            return (modifiers, -1);
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

            UploadNanoCrystal(item);
            return true;
        }

        /// <summary>
        /// Equipped nano crystals count as uploaded: every UploadNano function on the item adds its
        /// nano to this NPC's list. The crystal stays in equipment.
        /// </summary>
        void UploadNanoCrystal(Item item)
        {
            foreach (KeyValuePair<EventType, List<ItemSpell>> pair in item.SpellList)
            {
                List<ItemSpell>? spells = pair.Value;
                if (spells == null)
                    continue;

                for (int i = 0; i < spells.Count; i++)
                {
                    if (spells[i].Is(FunctionType.UploadNano) && spells[i].TryReadInt(0, out int nanoId))
                        TryAddUploadedNano(nanoId);
                }
            }
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

        internal static bool TryDecodeAoHash(int packed, out string hash)
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
            // A charmed NPC keeps pace with its owner through a bonus; the charm ending drops it with the next rebase.
            AppliedCharmRunSpeedBonus = Pets.PetService.CharmRunSpeedBonus(this);
            if (AppliedCharmRunSpeedBonus != 0)
                Stats.AddBonus(CharacterStat.RunSpeed, AppliedCharmRunSpeedBonus);
            RebaseWearAppearance();
            SyncPetRunSpeeds();
        }

        /// <summary>The run speed bonus the last rebase gave this NPC to keep up with its charmer (0 when not charmed).</summary>
        internal int AppliedCharmRunSpeedBonus { get; private set; }

        /// <summary>
        /// NPCs keep one inventory rather than player wear pages, so everything they carry feeds
        /// their look.
        /// </summary>
        protected override IEnumerable<Container> AppearanceWearPages => [Equipment];

        /// <summary>Armor page wear slots start after the 16 weapon page slots (ArmorSlots.Head 2 is wear slot 18).</summary>
        const int ArmorWearSlotBase = 16;

        /// <summary>
        /// NPC equipment slots are free-slot order, so an armor piece places its meshes by its own DefaultPos armor
        /// slot (ICC Peacekeeper Helmet 286563: 2, head; Shoulderpad 286450: 4, right shoulder).
        /// </summary>
        protected override int AppearanceWearSlot(int slot, Item item)
        {
            if ((ItemClass)item.GetStat(CharacterStat.ItemClass) != ItemClass.Armor)
                return slot;

            int armorSlot = StatCollection.Normalize(item.GetStat(CharacterStat.DefaultPos));
            return armorSlot is > 0 and < ArmorWearSlotBase ? ArmorWearSlotBase + armorSlot : slot;
        }

        /// <summary>
        /// First melee/ranged equipment item; later ones are ignored. Drawn in the right hand and shown as
        /// the swinging weapon; the monster weapon still rolls the damage.
        /// </summary>
        public Item? VisualRightHand { get; private set; }

        /// <summary>
        /// Rounds left in the visible ranged hand weapon's clip; -1 until the first shot fills it from the weapon's
        /// MaxEnergy. Kept across weapon rebases so a re-arm does not reload it.
        /// </summary>
        int _handClipRounds = -1;

        /// <summary>
        /// The swing bound to a ranged hand weapon spends a round: AttackInfo carries the rounds left after the shot
        /// (24 from a 25-round Ofab Shark Mk 5). An empty clip keeps firing at 0 and never reloads. Retail ICC Shuttle
        /// Guards, capture 2026-10-22T19:35Z pf 4582: 22, 21, ... 12 and 4, 3, 2, 1, 0, 0, 0.
        /// </summary>
        protected override int? ConsumeRound(CharacterWeapon weapon)
        {
            if (weapon.VisualHand is not Item hand || Helpers.AttackInfoRules.UsesMeleeAmmo(hand))
                return null;

            if (_handClipRounds < 0)
                _handClipRounds = Math.Max(0, StatCollection.Normalize(hand.GetStat(CharacterStat.MaxEnergy)));
            if (_handClipRounds > 0)
                _handClipRounds--;
            return _handClipRounds;
        }

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

            Item? prop = null;
            foreach (KeyValuePair<int, Item> slot in Equipment.EnumerateSlots())
            {
                if (_combatWeaponSlots.Contains(slot.Key))
                    continue;
                if (!IsVisualWeapon(slot.Value))
                {
                    prop ??= IsHandProp(slot.Value) ? slot.Value : null;
                    continue;
                }

                VisualRightHand = slot.Value;
                int meshId = StatCollection.Normalize(slot.Value.GetStat(CharacterStat.WeaponMesh));
                if (meshId > 0)
                    SetHandMesh(RightHandMeshPosition, meshId, overrideTextureId: 0);
                return;
            }

            // No real weapon to show: a hand-held prop with a weapon mesh but no attack (Carlo Pinnetti's The Briefcase,
            // no InitiativeType or AttackRange) is drawn in the hand. It is only a mesh: the monster weapons keep
            // their own swing and range.
            if (prop != null)
                SetHandMesh(RightHandMeshPosition, StatCollection.Normalize(prop.GetStat(CharacterStat.WeaponMesh)), overrideTextureId: 0);
        }

        /// <summary>A weapon-class item that only draws a mesh in the hand.</summary>
        static bool IsHandProp(Item item)
            => (ItemClass)item.GetStat(CharacterStat.ItemClass) == ItemClass.Weapon
                && StatCollection.Normalize(item.GetStat(CharacterStat.WeaponMesh)) > 0;

        /// <summary>
        /// Arms each monster weapon on its own wire slot (0, 1, 2, ...). With a visible hand weapon only the first one
        /// is bound to it: that swing shows as the hand weapon (slot 6, no tag, the hand weapon's range). Every other
        /// monster weapon keeps its own slot, SAW tag and range. Retail ICC Shuttle Guard (capture 2026-10-22T19:18Z,
        /// pf 4582: Ofab Shark Mk 5 in hand plus BKDD, PQEA, KLGZ, VQHW, HVJP) sends hand hits as slot 6 / ammo 0 /
        /// instance 0 and the martial arts hits as slots 1..4 with their tags, and lists only those four in its SAW.
        /// </summary>
        void ArmMonsterWeapons()
        {
            int armed = 0;
            foreach (KeyValuePair<int, Item> slot in Equipment.EnumerateSlots())
            {
                if (armed >= MaxNpcCombatWeapons)
                    return;
                if (!_combatWeaponSlots.Contains(slot.Key))
                    continue;

                _equipmentSawHashes.TryGetValue(slot.Key, out string? sawHash);
                WeaponSlot logical = (WeaponSlot)((int)WeaponSlot.Npc0 + armed);
                ArmFromItem(logical, slot.Value, wireSlot: armed, sawHash: sawHash);
                CharacterWeapon weapon = Weapons[logical];
                bool boundToHand = armed == 0 && VisualRightHand != null;
                weapon.VisualHand = boundToHand ? VisualRightHand : null;
                weapon.RangeSource = boundToHand ? VisualRightHand : slot.Value;
                weapon.RefreshEffectiveSpeeds();
                armed++;
            }
        }

        static bool IsVisualWeapon(Item item)
            => (item.GetWeaponFlags() & (WeaponFlags.Melee | WeaponFlags.Ranged)) != 0;

        const int RightHandMeshPosition = 1;
        const int LeftHandMeshPosition = 2;
        /// <summary>Monster weapons armed per NPC; <see cref="WeaponSlot.Npc0"/>..<see cref="WeaponSlot.Npc15"/>.</summary>
        const int MaxNpcCombatWeapons = 16;
    }
}
