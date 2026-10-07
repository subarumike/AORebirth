namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Summons, commands and dismisses pets on one playfield. A pet is an ordinary NPC spawned from its mob hash
    /// with PetMaster (196) set to its owner before it is first sent, so observers get it as a pet
    /// (SimpleCharFullUpdate IsPet); its owner's client is told with AddPet and RemovePet, and the owner's Pets
    /// stat (251) carries one bit per occupied pet slot. Owners keep one pet per slot. Every call runs on this
    /// playfield's thread, where the owner and its pets live.
    /// </summary>
    public sealed class PetService
    {
        /// <summary>A player's pet farther than this from its owner rejoins the owner at once.</summary>
        public const double MaxPlayerPetDistance = 50.0;

        readonly Playfield _playfield;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;

        public PetService(Playfield playfield, IGameData gameData, IZoneLogger logger)
        {
            _playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        PetTypeCatalog Types => PetTypeCatalog.For(_gameData.RootPath);

        /// <summary>
        /// FunctionType.SummonPet: summons <paramref name="hash"/> at <paramref name="level"/> for
        /// <paramref name="owner"/>, lasting <paramref name="durationSeconds"/> (0 or less: until dismissed).
        /// A pet already in the same slot is dismissed first.
        /// </summary>
        public NpcCharacter? Summon(Character owner, string hash, int level, int durationSeconds,
            IReadOnlyList<ItemRequirement>? summonRequirements = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (string.IsNullOrWhiteSpace(hash) || owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return null;
            if (owner is NpcCharacter { PetOwner: not null })
                return null;

            DateTime? expires = durationSeconds > 0 ? DateTime.UtcNow.AddSeconds(durationSeconds) : null;
            int type = Types.TypeOf(hash.Trim());
            return Spawn(owner, hash.Trim(), type, level, expires, DefaultMode(type), healthPercent: 100,
                summonRequirements ?? []);
        }

        NpcCharacter? Spawn(Character owner, string hash, int type, int level, DateTime? expires, PetMode mode, int healthPercent,
            IReadOnlyList<ItemRequirement> summonRequirements)
        {
            if (!_gameData.TryResolveMobTemplate(hash, level > 0 ? level : null, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("SummonPet: mob hash " + hash + " does not resolve to a spawnable template");
                return null;
            }

            if (owner.OwnedPets.InSlotOf(type) is NpcCharacter previous)
                Dismiss(previous, "replaced");

            var controller = new PetController(owner, type, hash, level, expires, summonRequirements);
            if (owner is Player && mode is PetMode.Follow or PetMode.Guard)
                controller.Order(mode);

            int index = owner.OwnedPets.Count;
            Vector3 at = _playfield.SnapNpcSpawn(PetFormation.PointFor(owner, index));
            NpcCharacter pet = _playfield.GetRequiredService<SpawnService>().SpawnMob(
                template,
                at,
                owner.Rotation,
                level > 0 ? level : null,
                SpawnSource.Summoned,
                spawnHash: hash,
                configure: npc =>
                {
                    npc.Stats.Set(CharacterStat.PetMaster, owner.Identity.Instance);
                    npc.Stats.Set(CharacterStat.PetType, type);
                    npc.Stats.Set(CharacterStat.Side, owner.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base));
                    MatchOwnerRunSpeed(npc, owner);
                    npc.BindPet(controller);
                    if (healthPercent is > 0 and < 100)
                    {
                        int max = npc.Stats.GetOrZero(CharacterStat.MaxHealth);
                        if (max > 0)
                            npc.Stats.Set(CharacterStat.Health, Math.Max(1, (int)(max * (long)healthPercent / 100)));
                    }
                },
                attachDefaultBrain: false);
            NpcBrain.CreatePet(pet, controller);

            owner.OwnedPets.Add(pet);
            PublishPetsStat(owner);
            if (owner is Player player)
                SendAddPet(player, pet);
            RefreshOverEquip(pet);

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Summoned pet id={0} hash={1} type={2} level={3} owner={4} expires={5}",
                pet.Identity.Instance, hash, type, pet.Stats.GetOrZero(CharacterStat.Level), owner.Identity.Instance,
                expires?.ToString("o", CultureInfo.InvariantCulture) ?? "never"));
            return pet;
        }

        /// <summary>Removes a pet from the world and from its owner.</summary>
        public void Dismiss(NpcCharacter pet, string reason)
        {
            ArgumentNullException.ThrowIfNull(pet);
            PetController? controller = pet.Pet;
            if (controller == null)
                return;

            Character owner = controller.Owner;
            bool owned = owner.OwnedPets.Remove(pet);
            pet.Brain?.StopPathing();
            if (pet.Playfield != null)
                pet.Playfield.GetRequiredService<SpawnService>().DespawnNpc(pet);
            pet.UnbindPet();

            if (owned)
            {
                PublishPetsStat(owner);
                if (owner is Player player)
                    SendRemovePet(player, pet);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Dismissed pet id={0} hash={1} owner={2} reason={3}",
                pet.Identity.Instance, controller.Hash, owner.Identity.Instance, reason));
        }

        /// <summary>Removes every pet <paramref name="owner"/> has, and any it was carrying through a zone change.</summary>
        public void DismissAll(Character owner, string reason)
        {
            ArgumentNullException.ThrowIfNull(owner);
            owner.OwnedPets.Stash.Clear();
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
                Dismiss(pet, reason);
        }

        /// <summary>A pet died: it leaves its owner (its body is not kept; pets leave no corpse).</summary>
        public void OnPetDied(NpcCharacter pet)
        {
            ArgumentNullException.ThrowIfNull(pet);
            PetController? controller = pet.Pet;
            if (controller == null)
                return;

            Character owner = controller.Owner;
            if (owner.OwnedPets.Remove(pet))
            {
                PublishPetsStat(owner);
                if (owner is Player player)
                    SendRemovePet(player, pet);
            }
        }

        /// <summary>The owner is leaving this playfield for another: its pets go with it.</summary>
        public void StashForTransfer(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            owner.OwnedPets.Stash.Clear();
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            DateTime now = DateTime.UtcNow;
            foreach (NpcCharacter pet in pets)
            {
                PetController? controller = pet.Pet;
                if (controller == null || pet.IsDead || controller.HasExpired(now))
                    continue;

                // Pets arrive in the mode they left in. A waiting pet cannot keep its post on another playfield, so it
                // follows; an attack order's target stays behind, so the pet goes back to what it did before it.
                PetMode mode = controller.Mode switch
                {
                    PetMode.Wait => PetMode.Follow,
                    PetMode.Attack => controller.ModeBeforeAttack,
                    _ => controller.Mode
                };
                owner.OwnedPets.Stash.Add(new PetStash(controller.Hash, controller.Type, controller.Level,
                    controller.ExpiresUtc, mode, HealthPercent(pet), controller.SummonRequirements));
            }

            foreach (NpcCharacter pet in pets)
            {
                PetController? controller = pet.Pet;
                owner.OwnedPets.Remove(pet);
                pet.Brain?.StopPathing();
                if (pet.Playfield != null)
                    pet.Playfield.GetRequiredService<SpawnService>().DespawnNpc(pet);
                pet.UnbindPet();
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Pet id={0} hash={1} leaves with owner={2}", pet.Identity.Instance, controller?.Hash, owner.Identity.Instance));
            }
        }

        /// <summary>The owner arrived here from another playfield: the pets it brought are summoned next to it.</summary>
        public void RestoreAfterTransfer(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            List<PetStash> stash = owner.OwnedPets.Stash;
            if (stash.Count == 0)
                return;

            PetStash[] carried = [.. stash];
            stash.Clear();
            DateTime now = DateTime.UtcNow;
            foreach (PetStash pet in carried)
            {
                if (pet.ExpiresUtc is DateTime expires && now >= expires)
                    continue;
                Spawn(owner, pet.Hash, pet.Type, pet.Level, pet.ExpiresUtc, pet.Mode, pet.HealthPercent, pet.SummonRequirements);
            }
        }

        /// <summary>
        /// Tells the owner's client about every pet it has (after it finishes zoning in or reconnects), so its pet
        /// window lists them. The client ignores a pet it already lists.
        /// </summary>
        public void AnnounceOwnedPets(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            foreach (NpcCharacter pet in owner.OwnedPets.All)
                SendAddPet(owner, pet);
            PublishPetsStat(owner);
        }

        /// <summary>
        /// A PetCommand from the owner's client. Only the owner's own pets on this playfield obey; an attack order
        /// goes at the owner's current target.
        /// </summary>
        public void Command(Player owner, PetCommandCode command, IReadOnlyList<Identity> petIdentities)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(petIdentities);
            if (owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return;

            foreach (NpcCharacter pet in ResolveOwned(owner, petIdentities))
            {
                PetController controller = pet.Pet!;
                // An over-equipped pet ignores every command; its owner can still dismiss it or ask for a report.
                if (controller.IsOverEquipped && command is not (PetCommandCode.Terminate or PetCommandCode.Report))
                    continue;

                bool healPet = IsHealPet(controller.Type);
                switch (command)
                {
                    case PetCommandCode.Follow:
                    case PetCommandCode.Behind:
                        controller.Order(PetMode.Follow);
                        if (healPet)
                            controller.EndHeal();
                        StandDown(pet);
                        break;
                    case PetCommandCode.Guard:
                        // A heal pet's guard is a heal order on its owner; a mezz pet's guard is follow, so it
                        // only fights when sent at a target.
                        if (healPet)
                        {
                            controller.Order(PetMode.Guard);
                            controller.EndHeal();
                        }
                        else if (IsMezzPet(controller.Type))
                        {
                            controller.Order(PetMode.Follow);
                            StandDown(pet);
                        }
                        else
                        {
                            controller.Order(PetMode.Guard);
                        }
                        break;
                    case PetCommandCode.Wait:
                        controller.Order(PetMode.Wait, pet.Position);
                        if (healPet)
                            controller.EndHeal();
                        StandDown(pet);
                        break;
                    case PetCommandCode.Attack:
                        if (ResolveAttackTarget(owner, pet) is Character target)
                            controller.Attack(target.Identity);
                        break;
                    case PetCommandCode.Heal:
                        // A heal pet only heals out of follow and wait, so a heal order puts it back on duty.
                        if (healPet && controller.Mode is PetMode.Follow or PetMode.Wait)
                            controller.Order(PetMode.Guard);
                        controller.Heal(ResolveHealTarget(owner).Identity);
                        break;
                    case PetCommandCode.Terminate:
                        Dismiss(pet, "terminated by owner");
                        break;
                    case PetCommandCode.Report:
                        Report(owner, pet);
                        break;
                }
            }
        }

        /// <summary>
        /// /pet report: one plain-text feedback line per pet. Live (capture 2026-10-07T02:52:27Z) sends it as
        /// FormatFeedback 110/707 with one string, "Anger Manifestation: Health: 100% Nano: 100% NCU: 0/9 Position: 935,756".
        /// TBD: pet MaxNCU. Pet templates carry no MaxNCU (181), so this reports 0 where live reported 9; the live
        /// source of a pet's NCU is not known yet.
        /// </summary>
        static void Report(Player owner, NpcCharacter pet)
        {
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: Health: {1}% Nano: {2}% NCU: {3}/{4} Position: {5},{6}",
                pet.Name ?? string.Empty,
                Percent(pet.Stats.GetOrZero(CharacterStat.Health), pet.Stats.GetOrZero(CharacterStat.MaxHealth)),
                Percent(pet.Stats.GetOrZero(CharacterStat.CurrentNano), pet.Stats.GetOrZero(CharacterStat.MaxNanoEnergy)),
                pet.UsedNcu,
                pet.MaxNcu,
                (int)pet.Position.x,
                (int)pet.Position.z);
            Helpers.ClientFeedback.SendFormatted(owner, Helpers.ClientFeedback.PlainText, line);
        }

        static int Percent(int current, int max) => max <= 0 ? 100 : (int)(100L * Math.Max(0, current) / max);

        static bool IsHealPet(int type) => PetTypes.Slot(type) == PetTypes.Slot(PetTypes.Heal);

        /// <summary>The Metaphysicist's mezz pet: it only fights when sent at a target.</summary>
        static bool IsMezzPet(int type) => PetTypes.Slot(type) == PetTypes.Slot(PetTypes.Support);

        /// <summary>
        /// A newly summoned heal pet follows until told to heal or guard, and a mezz pet follows until sent at a
        /// target; other pets guard.
        /// </summary>
        static PetMode DefaultMode(int type) => IsHealPet(type) || IsMezzPet(type) ? PetMode.Follow : PetMode.Guard;

        IEnumerable<NpcCharacter> ResolveOwned(Player owner, IReadOnlyList<Identity> petIdentities)
        {
            // The command names the pets it is for; an empty list means all of them.
            NpcCharacter[] owned = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in owned)
            {
                if (pet.Pet == null || !ReferenceEquals(pet.Playfield, _playfield))
                    continue;
                if (petIdentities.Count == 0 || Contains(petIdentities, pet.Identity))
                    yield return pet;
            }
        }

        static bool Contains(IReadOnlyList<Identity> identities, Identity identity)
        {
            for (int i = 0; i < identities.Count; i++)
            {
                if (identities[i].Instance == identity.Instance && identities[i].Type == identity.Type)
                    return true;
            }

            return false;
        }

        Character? ResolveAttackTarget(Player owner, NpcCharacter pet)
        {
            Identity target = owner.Target.Instance != 0 ? owner.Target : owner.FightingTarget;
            if (target.Instance == 0)
                return null;
            if (!_playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                || dynel is not Character character || character.IsDead)
                return null;

            return Helpers.CombatRules.CanAttack(pet, character) ? character : null;
        }

        /// <summary>
        /// A heal order is for the owner's current target when that is someone friendly (a player, or a player's
        /// pet), otherwise for the owner.
        /// </summary>
        Character ResolveHealTarget(Player owner)
        {
            if (owner.Target.Instance != 0
                && _playfield.GetRequiredService<DynelRegistry>().TryGet(owner.Target, out Dynel? dynel)
                && dynel is Character target && !target.IsDead
                && (target is Player || target is NpcCharacter { PetOwner: Player }))
                return target;

            return owner;
        }

        /// <summary>
        /// Re-judges whether each of <paramref name="owner"/>'s pets is over-equipped (after the owner's stats were
        /// rebased). A pet that becomes OE drops what it was doing and follows.
        /// </summary>
        public void RefreshOverEquip(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (owner is not Player || owner.OwnedPets.Count == 0)
                return;

            foreach (NpcCharacter pet in owner.OwnedPets.All)
                RefreshOverEquip(pet);
        }

        void RefreshOverEquip(NpcCharacter pet)
        {
            PetController? controller = pet.Pet;
            if (controller == null || controller.Owner is not Player owner)
                return;

            bool overEquipped = OverEquip.ComputeLevel(controller.SummonRequirements, owner.Stats) > 0;
            if (overEquipped == controller.IsOverEquipped)
                return;

            controller.IsOverEquipped = overEquipped;
            if (overEquipped)
            {
                controller.EndHeal();
                controller.Order(PetMode.Follow);
                StandDown(pet);
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Pet id={0} hash={1} owner={2} over-equipped={3}",
                pet.Identity.Instance, controller.Hash, owner.Identity.Instance, overEquipped));
        }

        static void StandDown(NpcCharacter pet)
        {
            pet.Brain?.StopFighting();
            pet.Brain?.StopPathing();
        }

        /// <summary>
        /// A pet moves at its owner's run speed (buffs and all), so it keeps up instead of trailing behind.
        /// Returns true when the pet's speed changed.
        /// </summary>
        public static bool MatchOwnerRunSpeed(NpcCharacter pet, Character owner)
        {
            int speed = owner.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (speed <= 0 || pet.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Base) == speed)
                return false;

            pet.Stats.Set(CharacterStat.RunSpeed, speed, StatDetail.Base, dirty: true);
            return true;
        }

        static int HealthPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxHealth);
            return max <= 0 ? 100 : (int)Math.Clamp(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.Health)) / max, 1, 100);
        }

        static void PublishPetsStat(Character owner)
        {
            int flags = owner.OwnedPets.Flags();
            if (owner.Stats.GetOrZero(CharacterStat.Pets, StatDetail.Base) == flags)
                return;

            owner.Stats.Set(CharacterStat.Pets, flags, StatDetail.Base, dirty: true);
            if (owner is Player player)
                player.FlushDirtyStats();
        }

        static void SendAddPet(Player owner, NpcCharacter pet)
            => owner.Session?.Send(new AddPetMessage { Identity = owner.Identity, Unknown = 0, PetIdentity = pet.Identity });

        static void SendRemovePet(Player owner, NpcCharacter pet)
            => owner.Session?.Send(new RemovePetMessage { Identity = owner.Identity, Unknown = 0, PetIdentity = pet.Identity });
    }
}
