namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.World.Pathfinding;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

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

        /// <summary>Starts a clearance ray just above the supporting floor, avoiding a hit on the floor itself.</summary>
        const float SpawnFloorClearance = 0.05f;

        static readonly NavMeshBuildSettings DefaultCollisionAgent = NavMeshBuildSettings.CreateDefault();

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
        /// A pet already in the same slot is dismissed first, unless <paramref name="replaceSameSlot"/> is false (an NPC's
        /// combat pets, which keep several of one kind: MonsterCombatRules.json).
        /// </summary>
        public NpcCharacter? Summon(Character owner, string hash, int level, int durationSeconds,
            IReadOnlyList<ItemRequirement>? summonRequirements = null, bool replaceSameSlot = true)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (string.IsNullOrWhiteSpace(hash) || owner.IsDead || !ReferenceEquals(owner.Playfield, _playfield))
                return null;
            if (owner is NpcCharacter { PetOwner: not null })
                return null;

            DateTime? expires = durationSeconds > 0 ? DateTime.UtcNow.AddSeconds(durationSeconds) : null;
            int type = Types.TypeOf(hash.Trim());
            return Spawn(owner, hash.Trim(), type, level, expires, summonRequirements ?? [], replaceSameSlot);
        }

        NpcCharacter? Spawn(Character owner, string hash, int type, int level, DateTime? expires,
            IReadOnlyList<ItemRequirement> summonRequirements, bool replaceSameSlot = true)
        {
            if (!_gameData.TryResolveMobTemplate(hash, level > 0 ? level : null, out MobTemplate template)
                || !NpcTemplateValidation.CanSpawn(template))
            {
                _logger.Warn("SummonPet: mob hash " + hash + " does not resolve to a spawnable template");
                return null;
            }

            NpcCharacter? previous = replaceSameSlot ? owner.OwnedPets.InSlotOf(type) : null;
            if (!TryFindSpawnPosition(owner, previous, out Vector3 at, out int formationIndex))
                return null;

            if (previous != null)
                Dismiss(previous, "replaced");

            var controller = new PetController(owner, type, hash, level, expires, summonRequirements);
            if (owner is Player)
                controller.Order(DefaultMode(type));

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
                    // A pet of a type its owner named before takes that name again.
                    if (owner is Player namer && namer.PetNames.TryGet(type, out string savedName))
                        npc.Name = savedName;
                    npc.Stats.Set(CharacterStat.Side, owner.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base));
                    MatchOwnerRunSpeed(npc, owner);
                    npc.BindPet(controller);
                },
                attachDefaultBrain: false);
            NpcBrain.CreatePet(pet, controller);

            owner.OwnedPets.Add(pet, formationIndex);
            PublishPetsStat(owner);
            if (owner is Player player)
            {
                SendAddPet(player, pet);
                Answer(player, pet, PetReplyCatalog.Summoned);
            }

            RefreshOverEquip(pet);

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Summoned pet id={0} hash={1} type={2} level={3} owner={4} expires={5}",
                pet.Identity.Instance, hash, type, pet.Stats.GetOrZero(CharacterStat.Level), owner.Identity.Instance,
                expires?.ToString("o", CultureInfo.InvariantCulture) ?? "never"));
            return pet;
        }

        internal bool TryFindSpawnPosition(Character owner, NpcCharacter? previous, out Vector3 at, out int formationIndex)
        {
            at = owner.Position;
            formationIndex = 0;
            var finder = _playfield.Pathfinder;
            var world = _playfield.WorldAccess.Instance;
            if (world == null)
                return false;

            NavMeshBuildSettings agent = finder?.Settings ?? DefaultCollisionAgent;
            if (finder != null && TryFindSpawnPosition(owner, previous, world, finder, agent, out at, out formationIndex))
                return true;

            // Valid collision can extend beyond a baked polygon, and some shop interiors have no mesh at all.
            return TryFindSpawnPosition(owner, previous, world, null, agent, out at, out formationIndex);
        }

        bool TryFindSpawnPosition(Character owner, NpcCharacter? previous, PlayfieldWorldSimulation world,
            NavMeshPathfinder? finder, NavMeshBuildSettings agent, out Vector3 at, out int formationIndex)
        {
            at = owner.Position;
            formationIndex = 0;
            float radius = agent.AgentRadius;
            float height = agent.AgentHeight;
            int candidates = PetFormation.SlotCount + owner.OwnedPets.Count;
            for (int index = 0; index < candidates; index++)
            {
                if (owner.OwnedPets.IsFormationIndexOccupied(index, previous))
                    continue;

                Vector3 wanted = PetFormation.PointFor(owner, index);
                Vector3 candidate = wanted;
                if (finder != null)
                {
                    var point = new System.Numerics.Vector3(wanted.xf, wanted.yf, wanted.zf);
                    if (!finder.TrySnap(point, PetAi.FollowSnapMeters, out System.Numerics.Vector3 snapped))
                        continue;

                    double dx = snapped.X - wanted.x;
                    double dz = snapped.Z - wanted.z;
                    if ((dx * dx) + (dz * dz) > PetAi.FollowSnapMeters * PetAi.FollowSnapMeters
                        || Math.Abs(snapped.Y - wanted.y) > MovementConfig.GroundSnapTolerance)
                        continue;
                    candidate = new Vector3(snapped.X, snapped.Y, snapped.Z);
                }

                float maxDrop = finder == null ? MovementConfig.GroundSnapTolerance : MovementConfig.GroundProbeLift;
                if (!world.TrySnapToFloor(candidate, out Vector3 floor)
                    || floor.y - candidate.y > MovementConfig.GroundProbeLift || candidate.y - floor.y > maxDrop)
                    continue;

                var feet = new Vector3(floor.x, floor.y + SpawnFloorClearance, floor.z);
                var head = new Vector3(floor.x, floor.y + height, floor.z);
                Vector3 ownerAt = owner.Position;
                if (!world.HasLineOfSight(feet, head)
                    || (finder == null && (world.TrySphereCast(feet, head, radius, out _, out _)
                        || world.TrySphereCast(head, feet, radius, out _, out _)))
                    || !world.HasLineOfSight(ownerAt.xf, ownerAt.yf + MovementConfig.LineOfSightEyeHeight, ownerAt.zf,
                        floor.xf, floor.yf + MovementConfig.LineOfSightEyeHeight, floor.zf))
                    continue;

                bool occupied = false;
                foreach (NpcCharacter other in owner.OwnedPets.All)
                {
                    if (ReferenceEquals(other, previous) || other.IsDead || !ReferenceEquals(other.Playfield, _playfield))
                        continue;

                    double ox = other.Position.x - floor.x;
                    double oz = other.Position.z - floor.z;
                    if ((ox * ox) + (oz * oz) < 4 * radius * radius && Math.Abs(other.Position.y - floor.y) < height)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                    continue;

                at = floor;
                formationIndex = index;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a pet from the world and from its owner. A charmed NPC is not removed: its charm ends
        /// (<see cref="EndCharm"/>), and it turns on its owner unless <paramref name="charmTurnsOnOwner"/> is false
        /// (an owner leaving the world).
        /// </summary>
        public void Dismiss(NpcCharacter pet, string reason, bool charmTurnsOnOwner = true)
        {
            ArgumentNullException.ThrowIfNull(pet);
            PetController? controller = pet.Pet;
            if (controller == null)
                return;

            // A charmed NPC is not the owner's to remove from the world: dismissing it ends the charm.
            if (controller.IsCharmed)
            {
                EndCharm(pet, reason, charmTurnsOnOwner);
                return;
            }

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

        /// <summary>
        /// Removes every pet <paramref name="owner"/> has, including any in transit with it. Charmed NPCs go back to
        /// themselves (<see cref="Dismiss"/>); <paramref name="charmTurnsOnOwner"/> is false when the owner is leaving
        /// the world (logout, despawn), so nothing turns on a character that is no longer there.
        /// </summary>
        public void DismissAll(Character owner, string reason, bool charmTurnsOnOwner = true)
        {
            ArgumentNullException.ThrowIfNull(owner);
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
                Dismiss(pet, reason, charmTurnsOnOwner);
        }

        /// <summary>How long a logged-out player's pets wait for them before they go (retail: 30 seconds).</summary>
        public const int OwnerReturnSeconds = 30;

        /// <summary>Pets waiting for their logged-out owner, by the owner's character id.</summary>
        readonly Dictionary<int, List<NpcCharacter>> _awaitingOwner = new();

        /// <summary>
        /// <paramref name="owner"/> logs out: its pets stay where they are for <see cref="OwnerReturnSeconds"/>, out
        /// of fights, waiting for it to come back (<see cref="ReclaimAwaitingPets"/>); then they go
        /// (<see cref="DismissUnclaimed"/>). A charmed NPC is released at once, its own again, and does not turn on the
        /// owner. A pet in transit or dead goes at once.
        /// </summary>
        public void AwaitOwnerReturn(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            DateTime until = DateTime.UtcNow.AddSeconds(OwnerReturnSeconds);
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
            {
                if (pet.Pet is not PetController controller || controller.IsCharmed || pet.IsDead
                    || !ReferenceEquals(pet.Playfield, _playfield))
                {
                    Dismiss(pet, "owner logged out", charmTurnsOnOwner: false);
                    continue;
                }

                Hold(owner, pet, controller, until);
            }
        }

        /// <summary>
        /// <paramref name="owner"/> is leaving this playfield: its towers (<see cref="PetTypes.StaysBehind"/>) stay
        /// here and wait <see cref="OwnerReturnSeconds"/> for it to come back, as for a logout. Returns true when one
        /// stayed.
        /// </summary>
        public bool HoldStayBehindPets(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            bool any = false;
            DateTime until = DateTime.UtcNow.AddSeconds(OwnerReturnSeconds);
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
            {
                if (pet.Pet is not PetController controller || controller.IsCharmed || pet.IsDead
                    || !PetTypes.StaysBehind(controller.Type) || !ReferenceEquals(pet.Playfield, _playfield))
                    continue;

                Hold(owner, pet, controller, until);
                any = true;
            }

            if (any)
                PublishPetsStat(owner);
            return any;
        }

        void Hold(Player owner, NpcCharacter pet, PetController controller, DateTime until)
        {
            owner.OwnedPets.Remove(pet);
            controller.AwaitOwner(until);
            pet.Brain?.StopFighting();
            pet.Brain?.DropEngagement();
            pet.Brain?.StopPathing();
            if (!_awaitingOwner.TryGetValue(owner.Identity.Instance, out List<NpcCharacter>? held))
                _awaitingOwner[owner.Identity.Instance] = held = new List<NpcCharacter>();
            held.Add(pet);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Pet id={0} hash={1} waits for owner={2} until={3:o}",
                pet.Identity.Instance, controller.Hash, owner.Identity.Instance, until));
        }

        /// <summary>
        /// <paramref name="owner"/> is back on this playfield (logged in or reconnected) while its pets still wait: they
        /// serve it again. Call before <see cref="AnnounceOwnedPets"/> so its client lists them.
        /// </summary>
        public void ReclaimAwaitingPets(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (!_awaitingOwner.Remove(owner.Identity.Instance, out List<NpcCharacter>? pets))
                return;

            DateTime now = DateTime.UtcNow;
            foreach (NpcCharacter pet in pets)
            {
                if (pet.Pet is not PetController controller || pet.IsDead || !ReferenceEquals(pet.Playfield, _playfield))
                    continue;
                if (controller.HasExpired(now))
                {
                    Dismiss(pet, "expired while owner away");
                    continue;
                }

                controller.ReturnTo(owner);
                owner.OwnedPets.Add(pet);
                MatchOwnerRunSpeed(pet, owner);
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Pet id={0} hash={1} back with owner={2} mode={3}",
                    pet.Identity.Instance, controller.Hash, owner.Identity.Instance, controller.Mode));
            }
        }

        /// <summary>Sends away the pets whose owner did not come back in time.</summary>
        public void DismissUnclaimed(DateTime nowUtc)
        {
            if (_awaitingOwner.Count == 0)
                return;

            List<int>? done = null;
            foreach (KeyValuePair<int, List<NpcCharacter>> entry in _awaitingOwner)
            {
                // A pet that died or was removed some other way while waiting is no longer here to send away.
                entry.Value.RemoveAll(pet => pet.IsDead || pet.Pet is not { IsAwaitingOwner: true } || pet.Playfield == null);
                if (entry.Value.Count == 0 || entry.Value[0].Pet!.AwaitingOwnerUntilUtc <= nowUtc)
                    (done ??= new List<int>()).Add(entry.Key);
            }

            if (done == null)
                return;

            foreach (int ownerId in done)
            {
                _awaitingOwner.Remove(ownerId, out List<NpcCharacter>? pets);
                foreach (NpcCharacter pet in pets!)
                    Dismiss(pet, "owner did not return", charmTurnsOnOwner: false);
            }
        }

        /// <summary>
        /// CharmNpc: <paramref name="npc"/> serves <paramref name="owner"/> as a pet for as long as the charm nano
        /// <paramref name="nanoId"/> stays in its NCU. Its own brain is set aside (it goes back to it when the charm
        /// ends, <see cref="EndCharm"/>); it takes the owner's side and run speed, a pet slot of
        /// <paramref name="petType"/>, and <paramref name="requirements"/> for over-equip as a summoned pet does.
        /// Refused for a player, a pet, a dead or non-combat NPC, an NPC without the charm nano, or an owner elsewhere.
        /// </summary>
        public bool Charm(Player owner, NpcCharacter npc, int nanoId, int petType, IReadOnlyList<ItemRequirement> requirements)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(requirements);
            if (owner.IsDead || npc.IsDead || !npc.Attackable || npc.Pet != null || npc.Brain == null
                || !ReferenceEquals(owner.Playfield, _playfield) || !ReferenceEquals(npc.Playfield, _playfield)
                || !npc.TryGetBuff(nanoId, out Buff? charmBuff) || charmBuff == null)
                return false;

            // Charms share the mindcontrolled slot; the nano's TestNumPets already decided whether this one fits. Only
            // a charm in the very same place (an owner past that test some other way) gives way to the new one.
            foreach (NpcCharacter held in owner.OwnedPets.All)
            {
                if (held.Pet is { IsCharmed: true } heldCharm && heldCharm.Type == petType)
                {
                    Dismiss(held, "replaced");
                    break;
                }
            }

            // Whatever the NPC commanded is its own business no longer: its pets go, its fight ends.
            if (npc.OwnedPets.Count > 0)
                DismissAll(npc, "owner charmed");
            NpcBrain original = npc.Brain;
            original.StopFighting();
            original.DropEngagement();

            var charm = new PetCharm(nanoId, original, npc.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base));
            var controller = new PetController(owner, petType, npc.SpawnHash ?? npc.MobTemplate?.Hash ?? string.Empty,
                npc.Stats.GetOrZero(CharacterStat.Level), charmBuff.ExpiresAtUtc, requirements)
            {
                Charm = charm
            };
            // A charmed NPC guards: it follows its owner and fights whatever fights either of them.
            controller.Order(PetMode.Guard);

            // PetType (the client's pet kind) is the nano's ChangeVariable; the run speed follows the owner as a bonus
            // the next rebase adds (NpcCharacter.RebaseStats).
            npc.Stats.Set(CharacterStat.PetMaster, owner.Identity.Instance, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.Side, owner.Stats.GetOrZero(CharacterStat.Side, StatDetail.Base), StatDetail.Base, dirty: true);
            npc.BindPet(controller);
            NpcBrain.CreatePet(npc, controller);
            npc.MarkRebaseDirty();
            // A stroll leaves the NPC walking, and only its own brain's wandering switches back: a pet keeps pace.
            if (npc.Motor.State == Movement.MovementState.Walk)
                npc.Motor.ApplyAction(Movement.MovementAction.SwitchToRun);

            owner.OwnedPets.Add(npc);
            PublishPetsStat(owner);
            SendAddPet(owner, npc);
            // Observers learn a pet's master only from its spawn packet's owner field: everyone else who sees the NPC
            // gets it afresh. The owner keeps its copy, which AddPet has just made its pet.
            npc.Playfield?.GetRequiredService<PlayfieldLocality>().RespawnForObservers(npc, except: owner);
            RefreshOverEquip(npc);

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Charmed npc id={0} hash={1} type={2} owner={3} nano={4} until={5} mode={6} over-equipped={7}",
                npc.Identity.Instance, controller.Hash, petType, owner.Identity.Instance, nanoId,
                charmBuff.ExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture), controller.Mode,
                controller.IsOverEquipped));
            return true;
        }

        /// <summary>
        /// The charm on <paramref name="npc"/> is over: it leaves its owner and goes back to its own brain, side and
        /// run speed, where it stands, and the charm nano leaves its NCU. The nano's OnTerminate decides how it parts
        /// with its charmer (231008 hits it for the charmer, who it then fights); then it settles
        /// (<see cref="SettleAfterCharm"/>). <paramref name="turnOnOwner"/> is false for an owner who is leaving
        /// (zoning, logging out): the NPC does not go after them. <paramref name="settle"/> is false only when the
        /// nano's end is already running (it settles the NPC once its OnTerminate has run). Does nothing for an NPC
        /// that is not charmed, so every path that ends a charm (the nano running out or stripped, a dismiss, the
        /// owner zoning or logging out, the NPC dying or despawning) can call it. <paramref name="staysInWorld"/> is
        /// false when the NPC is dying or leaving the world: it is not sent afresh to its observers then.
        /// </summary>
        public void EndCharm(NpcCharacter npc, string reason, bool turnOnOwner = true, bool settle = true,
            bool staysInWorld = true)
        {
            ArgumentNullException.ThrowIfNull(npc);
            if (npc.Pet is not { Charm: PetCharm charm } controller)
                return;

            Character owner = controller.Owner;
            if (owner.OwnedPets.Remove(npc))
            {
                PublishPetsStat(owner);
                if (owner is Player player)
                    SendRemovePet(player, npc);
            }

            npc.Brain?.StopFighting();
            npc.Brain?.DropEngagement();
            npc.UnbindPet();
            npc.Stats.Set(CharacterStat.PetMaster, 0, StatDetail.Base, dirty: true);
            npc.Stats.Set(CharacterStat.Side, charm.OriginalSide, StatDetail.Base, dirty: true);
            // The next rebase drops the owner's run speed bonus.
            npc.MarkRebaseDirty();
            // The client writes PetMaster from a spawn's owner field only when it is non-zero, so a copy that already
            // has a master keeps it: everyone who sees the NPC, the former owner too, gets a fresh one without an owner.
            if (staysInWorld)
                npc.Playfield?.GetRequiredService<PlayfieldLocality>().RespawnForObservers(npc);

            NpcBrain brain = charm.OriginalBrain;
            npc.AttachBrain(brain);
            brain.DropEngagement();

            // The pet state is gone first, so the nano's own end (ExecuteBuffEnd) finds nothing more to end here; its
            // OnTerminate runs on the NPC as itself.
            NanoRuntime.StripBuff(npc, charm.NanoId);
            // Then nothing anyone ran on it while charmed stays: its NCU is emptied.
            NanoRuntime.ClearBuffsOnCharmEnd(npc);

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Charm ended npc id={0} owner={1} reason={2}", npc.Identity.Instance, owner.Identity.Instance, reason));

            if (settle)
                SettleAfterCharm(npc, turnOnOwner);
        }

        /// <summary>
        /// A released NPC picks up where its own brain left off. Without <paramref name="turnOnOwner"/> it forgets the
        /// charmer (who is leaving). With nobody left to fight, it resets as a leashing NPC does: heals, sheds what
        /// others cast on it and walks home. Otherwise it would stand wherever its charmer last led it.
        /// </summary>
        public void SettleAfterCharm(NpcCharacter npc, bool turnOnOwner)
        {
            ArgumentNullException.ThrowIfNull(npc);
            if (npc.IsDead || npc.Playfield == null || npc.Pet != null || npc.Brain is not NpcBrain brain)
                return;

            if (!turnOnOwner)
                brain.ClearHate();
            if (!brain.HasNearbyHate())
                brain.Evade();
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

        /// <summary>
        /// The owner is leaving this playfield for another: its pets go with it, as the same NPCs. Each drops what
        /// tied it to this playfield (its fight, route, hate, orders that only made sense here) and soft-leaves the
        /// way the owner does, keeping its health, nano, NCU and buffs. Only pets on this playfield are touched, so
        /// a destination undoing a partial arrival can call it too.
        /// </summary>
        public void LeaveWithOwner(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            // Towers stay put and wait for the owner instead.
            HoldStayBehindPets(owner);
            SpawnService spawn = _playfield.GetRequiredService<SpawnService>();
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
            {
                if (!ReferenceEquals(pet.Playfield, _playfield))
                    continue;

                // A charm holds only where it was cast: the NPC stays behind, its own again.
                if (pet.Pet is { IsCharmed: true })
                {
                    EndCharm(pet, "owner zoned", turnOnOwner: false);
                    continue;
                }

                pet.Pet?.LeavePlayfield();
                pet.Brain?.DropEngagement();
                spawn.LeaveForTransfer(pet);
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Pet id={0} hash={1} leaves with owner={2}", pet.Identity.Instance, pet.Pet?.Hash, owner.Identity.Instance));
            }
        }

        /// <summary>
        /// The owner arrived here from another playfield: the pets it brought take their formation places beside it.
        /// A pet whose summon ran out on the way, or that is dead, is dismissed instead. The owner's client learns of
        /// them once it has zoned in (<see cref="AnnounceOwnedPets"/>).
        /// </summary>
        public void ArriveWithOwner(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            // Towers it left waiting here (a zone change that came straight back) serve it again.
            ReclaimAwaitingPets(owner);
            if (owner.OwnedPets.Count == 0)
                return;

            owner.OwnedPets.ResetFormation();
            SpawnService spawn = _playfield.GetRequiredService<SpawnService>();
            DateTime now = DateTime.UtcNow;
            NpcCharacter[] pets = [.. owner.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
            {
                if (pet.Playfield != null)
                    continue;

                PetController? controller = pet.Pet;
                if (controller == null)
                {
                    owner.OwnedPets.Remove(pet);
                    continue;
                }

                if (pet.IsDead || controller.HasExpired(now))
                {
                    Dismiss(pet, "ended in transit");
                    continue;
                }

                Vector3 at = _playfield.SnapNpcSpawn(PetFormation.PointFor(owner, owner.OwnedPets.FormationIndex(pet)));
                spawn.ArriveFromTransfer(pet, at, owner.Rotation);
                MatchOwnerRunSpeed(pet, owner);
                _logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "Pet id={0} hash={1} arrives with owner={2} mode={3}",
                    pet.Identity.Instance, controller.Hash, owner.Identity.Instance, controller.Mode));
            }
        }

        /// <summary>
        /// Removes client pet entries before character reconstruction, keeping server ownership intact.
        /// Local Lost Eden inspection shows that a later removal can miss its already-reset ownership set.
        /// </summary>
        public void RemoveClientPetEntries(Player owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            foreach (NpcCharacter pet in owner.OwnedPets.All)
                SendRemovePet(owner, pet);
        }

        /// <summary>
        /// Tells the owner's client about every pet it currently has after zoning, reconnecting or reconstruction.
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
        public void Command(Player owner, PetCommandCode command, IReadOnlyList<Identity> petIdentities, string? name = null)
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
                        Answer(owner, pet, command.ToString());
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
                        Answer(owner, pet, command.ToString());
                        break;
                    case PetCommandCode.Attack:
                        // A heal pet never fights on command.
                        if (healPet)
                            break;
                        if (ResolveAttackTarget(owner, pet) is Character target)
                        {
                            controller.Attack(target.Identity);
                            Answer(owner, pet, command.ToString());
                        }
                        else
                        {
                            Answer(owner, pet, PetReplyCatalog.AttackRefused);
                        }

                        break;
                    case PetCommandCode.Heal:
                        // A heal pet only heals out of follow and wait, so a heal order puts it back on duty.
                        // A heal pet sent at what it cannot heal (an enemy, an unowned NPC) refuses; no target heals
                        // the owner.
                        if (healPet && !TryResolveHealTarget(owner, out _))
                        {
                            Answer(owner, pet, PetReplyCatalog.HealRefused);
                            break;
                        }

                        if (healPet && controller.Mode is PetMode.Follow or PetMode.Wait)
                            controller.Order(PetMode.Guard);
                        controller.Heal(ResolveHealTarget(owner).Identity);
                        if (healPet)
                            Answer(owner, pet, command.ToString());
                        break;
                    case PetCommandCode.Terminate:
                        Answer(owner, pet, command.ToString());
                        Dismiss(pet, "terminated by owner");
                        break;
                    case PetCommandCode.Report:
                        Report(owner, pet);
                        break;
                    case PetCommandCode.Rename:
                        Rename(owner, pet, name);
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

        /// <summary>
        /// The longest pet name every client shows: the client's SetName reader ignores a name of 32 bytes or more
        /// (Gamecode.dll 0x10076E6E), so a longer one would leave the pet named as before on every screen.
        /// </summary>
        public const int MaxPetNameLength = 31;

        /// <summary>
        /// /pet rename: the client sends the name in the PetCommand (font tags already escaped, "&lt;font ...&gt;", so
        /// they show as text); live renames the pet and sends everyone who sees it SetName with the owner's text
        /// (capture 2026-10-24: "Target-Dummy_99"). A name the client could not show (empty, too long, non-printable)
        /// is refused with a line to the owner; live echoed even an 86-byte name, which no client then applied.
        /// </summary>
        void Rename(Player owner, NpcCharacter pet, string? requested)
        {
            // A charmed NPC is borrowed, not the owner's pet: it keeps its own name.
            if (pet.Pet is { IsCharmed: true })
            {
                Helpers.ClientFeedback.SendFormatted(owner, Helpers.ClientFeedback.PlainText, "You cannot rename a charmed creature.");
                return;
            }

            string name = requested?.Trim() ?? string.Empty;
            if (!AORebirth.Core.Text.NameFilter.For(_playfield.GetRequiredService<IGameData>().RootPath).IsAllowed(name))
            {
                Helpers.ClientFeedback.SendFormatted(owner, Helpers.ClientFeedback.PlainText, "That pet name is not allowed.");
                return;
            }

            if (!IsShowablePetName(name))
            {
                Helpers.ClientFeedback.SendFormatted(owner, Helpers.ClientFeedback.PlainText, string.Format(
                    CultureInfo.InvariantCulture,
                    "A pet name must be 1 to {0} printable characters.",
                    MaxPetNameLength));
                return;
            }

            pet.Name = name;
            // Kept for the pet's type, so the next summon of that type takes the name too.
            if (pet.Pet is { } controller)
            {
                owner.PetNames.Set(controller.Type, name);
                _playfield.GetService<Inventory.InventoryFlushService>()?.NotifyDirty(owner);
            }

            _playfield.GetRequiredService<PlayfieldLocality>().Announce(pet, new SetNameMessage
            {
                Identity = pet.Identity,
                Unknown = 0,
                Kind = 3,
                Flag = 0,
                Name = name,
                Unknown1 = Identity.None,
                Unknown2 = 0,
                Unknown3 = 0
            });
        }

        /// <summary>1 to <see cref="MaxPetNameLength"/> printable ASCII characters.</summary>
        internal static bool IsShowablePetName(string name)
        {
            if (name.Length is 0 or > MaxPetNameLength)
                return false;
            foreach (char c in name)
            {
                if (c is < ' ' or > '~')
                    return false;
            }

            return true;
        }

        /// <summary>How long a pet stays quiet before giving the same answer again.</summary>
        const long RepeatAnswerCooldownMs = 1000;

        /// <summary>
        /// The pet's answer to a command its owner gave (GameData/PetReplies.json): "&lt;owner&gt;'s pet, &lt;pet&gt;:
        /// &lt;line&gt;" to the owner from the chat server, as live sends it. Nothing when the command has no lines or the
        /// chat server cannot be reached.
        /// </summary>
        void Answer(Player owner, NpcCharacter pet, string key)
        {
            // The same answer again within a second (the follow button clicked repeatedly) is not repeated.
            long now = Environment.TickCount64;
            if (pet.Pet is PetController controller)
            {
                (string lastKey, long lastAtMs) = controller.LastAnswer;
                if (string.Equals(lastKey, key, StringComparison.OrdinalIgnoreCase) && now - lastAtMs < RepeatAnswerCooldownMs)
                    return;
                controller.LastAnswer = (key, now);
            }

            string? line = PetReplyCatalog.For(_playfield.GetRequiredService<IGameData>().RootPath).Pick(key);
            if (line == null)
                return;
            string text = string.Format(CultureInfo.InvariantCulture, "{0}'s pet, {1}: {2}", owner.Name, pet.Name, line);
            _playfield.GetService<Chat.VicinityChatRelay>()?.Announce(text, [owner.Identity.Instance],
                Chat.VicinityChatRelay.PetAnswerBlob);
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
            => TryResolveHealTarget(owner, out Character target) ? target : owner;

        /// <summary>
        /// The owner's target when it is a living player or player's pet; the owner with no target. False for a target
        /// that cannot be healed.
        /// </summary>
        bool TryResolveHealTarget(Player owner, out Character target)
        {
            target = owner;
            if (owner.Target.Instance == 0 || owner.Target == owner.Identity)
                return true;

            if (_playfield.GetRequiredService<DynelRegistry>().TryGet(owner.Target, out Dynel? dynel)
                && dynel is Character character && !character.IsDead
                && (character is Player || character is NpcCharacter { PetOwner: Player }))
            {
                target = character;
                return true;
            }

            return false;
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
        /// Returns true when the pet's speed changed. A charmed NPC keeps its own run speed and catches up through a
        /// bonus (<see cref="CharmRunSpeedBonus"/>), which its next rebase recomputes.
        /// </summary>
        public static bool MatchOwnerRunSpeed(NpcCharacter pet, Character owner)
        {
            if (pet.Pet is { IsCharmed: true })
            {
                if (pet.AppliedCharmRunSpeedBonus == CharmRunSpeedBonus(pet))
                    return false;

                pet.MarkRebaseDirty();
                return true;
            }

            int speed = owner.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (speed <= 0 || pet.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Base) == speed)
                return false;

            pet.Stats.Set(CharacterStat.RunSpeed, speed, StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>
        /// The run speed bonus that brings a charmed NPC's own (base) run speed up to its owner's: what its rebase adds
        /// on top of its buffs, so a snare on it still slows it. 0 for any other NPC, and never negative.
        /// </summary>
        public static int CharmRunSpeedBonus(NpcCharacter npc)
        {
            ArgumentNullException.ThrowIfNull(npc);
            if (npc.Pet is not { IsCharmed: true } controller)
                return 0;

            int owner = controller.Owner.Stats.GetOrZero(CharacterStat.RunSpeed);
            return Math.Max(0, owner - npc.Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Base));
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
            => owner.Session?.Send(new AddPetMessage { Identity = owner.Identity, PetIdentity = pet.Identity });

        static void SendRemovePet(Player owner, NpcCharacter pet)
            => owner.Session?.Send(new RemovePetMessage { Identity = owner.Identity, PetIdentity = pet.Identity });
    }
}
