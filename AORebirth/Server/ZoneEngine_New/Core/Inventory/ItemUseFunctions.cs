namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>OnUse FunctionType implementations that ZoneEngine_New can run today.</summary>
    internal static class ItemUseFunctions
    {
        public static bool TryExecute(
            int templateId,
            Character target,
            Character? source,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            SpellCriteria? criteria = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.OpenBank:
                    return target is Player bankPlayer
                        && OpenBank(bankPlayer, inventoryRepository, items);
                case FunctionType.Hit:
                    return Hit(target, source, spell);
                case FunctionType.Set:
                    return Set(target, spell);
                case FunctionType.SetFlag:
                    return SetFlag(target, spell);
                case FunctionType.ClearFlag:
                    return ClearFlag(target, spell);
                case FunctionType.SystemText:
                case FunctionType.Text:
                    return target is Player textPlayer && SystemText(textPlayer, spell);
                case FunctionType.SaveChar:
                    return true;
                case FunctionType.LockSkill:
                    return LockSkill(target, source, spell);
                case FunctionType.UploadNano:
                    return target is Player uploadPlayer && UploadNano(uploadPlayer, spell);
                case FunctionType.CastNano:
                case FunctionType.AreaCastNano:
                case FunctionType.TeamCastNano:
                case FunctionType.PlayfieldNano:
                    return NanoCastFunctions.TryExecute(target, source, spell, items, inventoryRepository);
                case FunctionType.Teleport:
                    return target is Player teleportPlayer && Teleport(teleportPlayer, spell);
                case FunctionType.LineTeleport:
                    return target is Player linePlayer && LineTeleport(linePlayer, spell);
                case FunctionType.TeleportProxy:
                    return target is Player proxyDoorPlayer && TeleportProxy(proxyDoorPlayer, spell, criteria);
                case FunctionType.TeleportProxy2:
                    return target is Player proxyPlayer && TeleportProxy2(proxyPlayer, spell);
                case FunctionType.SpawnMonster2:
                    return SpawnMonster2(target, spell);
                case FunctionType.DestroyItem:
                    return DestroySubject(target, criteria);
                case FunctionType.ToggleFlag:
                    return ToggleFlag(target, spell);
                case FunctionType.CastNanoIfPossible:
                case FunctionType.NpcCastNanoIfPossible:
                case FunctionType.CastNanoIfPossibleOnFightTarget:
                case FunctionType.NpcCastNanoIfPossibleOnFightTarget:
                    return NanoCastFunctions.TryExecute(target, source, spell, items, inventoryRepository);
                case FunctionType.CastChance:
                    return CastChance(target, source, spell, items, inventoryRepository, criteria);
                case FunctionType.RemoveNano:
                    return RemoveNano(target, spell);
                case FunctionType.RemoveNanoStrain:
                    return RemoveNanoStrain(target, spell);
                case FunctionType.RemoveBuffs:
                    return NanoRuntime.StripAllBuffs(target);
                case FunctionType.SpawnItem:
                    return SpawnItem(target, spell);
                case FunctionType.NpcSocialAnim:
                    return NpcSocialAnim(target, spell);
                case FunctionType.NpcWipeHateList:
                    return NpcWipeHateList(target);
                case FunctionType.NpcStopMoving:
                    return NpcStopMoving(target);
                case FunctionType.NpcTeleportToSpawnPoint:
                    return NpcTeleportToSpawnPoint(target);
                case FunctionType.NpcFightSelected:
                    return NpcFightSelected(target, source);
                default:
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unhandled OnUse FunctionType={0} template={1} character={2}",
                            spell.FunctionType,
                            templateId,
                            target.Identity.Instance));
                    return false;
            }
        }

        static bool OpenBank(Player player, IInventoryRepository inventoryRepository, IItemBuilder items)
        {
            if (player.Session == null)
                return false;

            int characterId = player.Identity.Instance;
            player.Inventory.EnsureBankHydrated(characterId, inventoryRepository, items);
            player.Session.Send(player.Inventory.BuildBankMessage(player.Identity));
            return true;
        }

        static bool Hit(Character target, Character? source, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int minHit))
                return false;

            // Hit args: Stat, Min, Max [, AC]. Collapsed Stat, Amount, ACType when Amount<0 and third>0.
            int maxHit = minHit;
            int acStat = 0;
            if (spell.TryReadInt(2, out int third))
            {
                maxHit = third;
                if (spell.ArgumentCount == 3 && minHit < 0 && third > 0)
                {
                    acStat = third;
                    maxHit = minHit;
                }
                else if (spell.ArgumentCount >= 4)
                {
                    if (spell.TryReadInt(3, out int fourth))
                        acStat = fourth;

                    if (minHit < 0 && maxHit > 0)
                        maxHit = minHit;
                }
            }

            if (minHit > maxHit)
            {
                int swap = minHit;
                minHit = maxHit;
                maxHit = swap;
            }

            int delta = minHit == maxHit
                ? minHit
                : Random.Shared.Next(minHit, maxHit + 1);

            var stat = (CharacterStat)statId;
            if (stat == CharacterStat.Health)
                return ApplyHealthDelta(target, source, delta, acStat);

            if (stat == CharacterStat.CurrentNano || stat == CharacterStat.NanoPool)
                return ApplyNanoDelta(target, delta);

            target.Stats.Set(stat, target.Stats.GetOrZero(stat, StatDetail.Base) + delta, StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>
        /// Weapon OnHit proc (item 205012): nano id, then chance percent. A roll of 1–100
        /// lands the nano when it is less than or equal to the chance. ApplyOn selects
        /// the recipient; <see cref="ItemTarget.Target"/> is the character who was hit.
        /// </summary>
        static bool CastChance(
            Character target,
            Character? source,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            SpellCriteria? criteria)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;
            if (!spell.TryReadInt(1, out int chance))
                return false;
            if (chance <= 0)
                return true;

            if (chance > SpellCriteria.RollMaxInclusive)
                chance = SpellCriteria.RollMaxInclusive;

            criteria ??= new SpellCriteria();
            int roll = criteria.Resolve(CharacterStat.Rnd, static _ => 0);
            if (roll > chance)
                return true;

            Character caster = source ?? target;
            Character recipient = ResolveApplyOn(target, source, spell);
            return NanoRuntime.TryApplyImmediate(
                caster,
                recipient,
                nanoId,
                items,
                inventory,
                DateTime.UtcNow);
        }

        /// <summary>
        /// Character a function's ApplyOn names: User / Wearer / Self is whoever used the item or cast
        /// the nano, Target is the event target.
        /// </summary>
        internal static Character ResolveApplyOn(Character eventTarget, Character? source, ItemSpell spell)
        {
            switch ((ItemTarget)spell.Target)
            {
                case ItemTarget.User:
                case ItemTarget.Wearer:
                case ItemTarget.Self:
                    return source ?? eventTarget;
                case ItemTarget.Fightingtarget:
                    Character? fighting = source?.TryResolveFightingTarget();
                    return fighting ?? eventTarget;
                default:
                    return eventTarget;
            }
        }

        static bool ApplyHealthDelta(Character target, Character? source, int delta, int acStat)
        {
            Character caster = source ?? target;

            if (delta < 0)
            {
                int before = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                target.ApplyDamage(caster, -delta, HitType.Normal);

                int after = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                int actual = before - after;
                // HealthDamage carries post-hit HP; clear dirty Health so Tick's Stat flush
                // does not also print a second "unknown damage" line.
                target.Stats.ClearDirty(CharacterStat.Health);
                if (actual > 0)
                    target.AnnounceHealthDamage(caster, after, -actual, acStat);

                return true;
            }

            int maxHealth = Math.Max(1, target.Stats.GetOrZero(CharacterStat.MaxHealth));
            int current = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
            int applied = Math.Min(delta, Math.Max(0, maxHealth - current));
            if (applied <= 0)
                return true;

            int healed = current + applied;
            target.Stats.Set(CharacterStat.Health, healed, StatDetail.Base, dirty: true);
            target.Stats.ClearDirty(CharacterStat.Health);
            target.AnnounceHealthDamage(caster, healed, applied, damageTypeStat: 0);
            return true;
        }

        static bool ApplyNanoDelta(Character target, int delta)
        {
            int maxNano = Math.Max(0, target.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
            int current = Math.Max(0, target.Stats.GetOrZero(CharacterStat.CurrentNano));
            int next = delta >= 0
                ? current + Math.Min(delta, Math.Max(0, maxNano - current))
                : Math.Max(0, current + delta);

            if (next == current)
                return true;

            target.Stats.Set(CharacterStat.CurrentNano, next, StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>LockSkill args: Stat, Value (seconds). The leading Action word is not stored in items.dat.</summary>
        internal static bool TryReadSkillLock(ItemSpell spell, out int statId, out int durationSeconds)
        {
            durationSeconds = 0;
            return spell.TryReadInt(0, out statId) && spell.TryReadInt(1, out durationSeconds);
        }

        static bool LockSkill(Character target, Character? source, ItemSpell spell)
        {
            if (!TryReadSkillLock(spell, out int statId, out int durationSeconds))
                return false;

            ResolveApplyOn(target, source, spell).LockSkill(statId, durationSeconds, DateTime.UtcNow);
            return true;
        }

        static bool Set(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int value))
                return false;

            target.Stats.Set((CharacterStat)statId, value, StatDetail.Base, dirty: true);
            return true;
        }

        static bool SetFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current | (1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool ClearFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current & ~(1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool ToggleFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current ^ (1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool RemoveNano(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int nanoId))
                return false;

            return NanoRuntime.TryStripNano(target, nanoId);
        }

        static bool RemoveNanoStrain(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int strain))
                return false;

            return NanoRuntime.TryStripStrain(target, strain);
        }

        /// <summary>
        /// Catalog shape: hash, quality, and a 0/1 third integer. Both third values grant one item
        /// into inventory. Any other third value is refused.
        /// </summary>
        static bool SpawnItem(Character target, ItemSpell spell)
        {
            if (target is not Player player || player.Playfield == null || player.Session == null)
                return false;
            if (!spell.TryReadString(0, out string hash) || hash.Length == 0)
                return false;
            if (!spell.TryReadInt(1, out int quality) || quality <= 0)
                return false;
            if (spell.TryReadInt(2, out int mode) && mode is not 0 and not 1)
                return false;

            if (!player.Playfield.GetRequiredService<HashItemMinter>().TryMint(hash, quality, ItemSource.Other, out Item item))
                return false;
            if (!player.Inventory.TryPlace(item, out Container page, out int slot))
                return false;

            player.Inventory.MarkDirty(item, page, slot);
            player.Playfield.GetService<InventoryFlushService>()?.NotifyDirty(player);
            player.Session.Send(
                new AddTemplateMessage
                {
                    Identity = player.Identity,
                    HighId = item.HighId,
                    LowId = item.LowId,
                    Quality = item.Quality,
                    Count = item.StackCount
                });
            return true;
        }

        /// <summary>
        /// Catalog shape is one animation id. <see cref="CharacterActionType.NpcSocialAnim"/>
        /// carries that id in <c>Target.Instance</c>.
        /// </summary>
        static bool NpcSocialAnim(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int animId) || animId <= 0)
                return false;

            var message = new CharacterActionMessage
            {
                Identity = target.Identity,
                Unknown = 0,
                Action = CharacterActionType.NpcSocialAnim,
                Target = new Identity { Type = IdentityType.None, Instance = animId }
            };

            if (target.Cell != null)
                target.Cell.Announce(message);
            else if (target is Player player)
                player.Session?.Send(message);

            return true;
        }

        static bool NpcWipeHateList(Character target)
        {
            if (target is not NpcCharacter npc || npc.Brain == null)
                return false;

            npc.Brain.StopFighting();
            npc.Brain.ClearHate();
            return true;
        }

        static bool NpcStopMoving(Character target)
        {
            if (target is not NpcCharacter npc)
                return false;

            if (npc.Brain != null)
                npc.Brain.StopPathing();
            else
                npc.Motor.ClearPath();
            return true;
        }

        static bool NpcTeleportToSpawnPoint(Character target)
        {
            if (target is not NpcCharacter npc || npc.Brain is not NpcBrain brain || brain.Home is not Vector3 home)
                return false;

            brain.StopPathing();
            npc.Motor.Warp(home);
            return true;
        }

        static bool NpcFightSelected(Character target, Character? source)
        {
            NpcCharacter? npc = target as NpcCharacter ?? source as NpcCharacter;
            if (npc?.Brain == null)
                return false;

            Character? opponent = ReferenceEquals(npc, target) ? source : target;
            if (opponent == null || ReferenceEquals(opponent, npc) || opponent.IsDead)
                return false;
            if (!ReferenceEquals(opponent.Playfield, npc.Playfield))
                return false;

            npc.Brain.AddThreat(opponent.Identity, NpcAiRules.ProximityHate);
            npc.StartFighting(opponent.Identity, 0);
            return true;
        }

        static bool SystemText(Player player, ItemSpell spell)
        {
            if (player.Session == null || !spell.TryReadString(0, out string text) || text.Length == 0)
                return false;

            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
            return true;
        }

        /// <summary>
        /// Absolute OnUse teleport. Positional args are RelX, RelY, RelZ, destination playfield.
        /// One-way: it clears any stored proxy return door.
        /// </summary>
        static bool Teleport(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!spell.TryReadInt(0, out int x)
                || !spell.TryReadInt(1, out int y)
                || !spell.TryReadInt(2, out int z)
                || !spell.TryReadInt(3, out int playfieldId)
                || playfieldId <= 0
                || playfieldId >= ZoneEngine.Core.Missions.GeneratedMissionIdentitySpace.MinimumLivePlayfield2)
                return false;

            Playfield source = player.Playfield;
            IGameData gameData = source.GetRequiredService<IGameData>();
            string playfieldDir = Path.Combine(
                gameData.RootPath,
                GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
            if (!Directory.Exists(playfieldDir))
                return false;

            bool samePlayfield = playfieldId == source.Identity.Instance;
            Playfield destination = samePlayfield
                ? source
                : source.GetRequiredService<PlayfieldManager>().GetOrCreate(playfieldId);
            Vector3 landing = destination.SnapFeetToFloor(new Vector3(x, y, z));

            ClearProxyReturn(player);
            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);

            if (samePlayfield)
            {
                FinishSamePlayfieldMove(player, source, landing, heading: null);
                return true;
            }

            player.Session.TransferToPlayfield(destination, landing);
            return true;
        }

        /// <summary>
        /// LineTeleport names a Destinations.dat line: <c>{Playfield3, packed, destPlayfield}</c>.
        /// A destination playfield of 0 stays on the character's current playfield. One-way.
        /// </summary>
        static bool LineTeleport(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseLineDestination(spell.Arguments, out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            int playfieldId = destination.PlayfieldId == 0
                ? source.Identity.Instance
                : destination.PlayfieldId;
            if (playfieldId <= 0
                || playfieldId >= ZoneEngine.Core.Missions.GeneratedMissionIdentitySpace.MinimumLivePlayfield2)
                return false;

            IGameData gameData = source.GetRequiredService<IGameData>();
            if (playfieldId != source.Identity.Instance)
            {
                string playfieldDir = Path.Combine(
                    gameData.RootPath,
                    GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
                if (!Directory.Exists(playfieldDir))
                    return false;
            }

            if (!PortalDoorLandingResolver.TryResolveLineLanding(
                    DestinationsCatalog.Instance,
                    playfieldId,
                    destination.DestinationIndex,
                    out Vector3 landing,
                    out Quaternion heading))
                return false;

            ClearProxyReturn(player);
            // Drops the pad overlap and holds triggers off briefly; afterwards the landing's
            // overlaps are adopted, so a return beam beside the landing only fires once walked off.
            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);

            if (destination.PlayfieldId == 0)
            {
                FinishIntrazoneLineTeleport(player, source, landing, destination.DestinationKey);
                return true;
            }

            if (playfieldId == source.Identity.Instance)
            {
                FinishSamePlayfieldMove(player, source, landing, heading);
                return true;
            }

            player.Rotation = heading;
            Playfield destPlayfield = source.GetRequiredService<PlayfieldManager>().GetOrCreate(playfieldId);
            player.Session.TransferToPlayfield(destPlayfield, landing, heading);
            return true;
        }

        static void ClearProxyReturn(Player player)
        {
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
        }

        static void FinishSamePlayfieldMove(Player player, Playfield source, Vector3 landing, Quaternion? heading)
        {
            if (heading != null)
                player.Motor.Warp(landing, heading);
            else
                player.Motor.Warp(landing);

            player.Session!.SendSamePlayfieldRespawnTeleport(landing);
            AnnounceArrival(player, source, landing);
        }

        /// <summary>
        /// Same-playfield lift (LineTeleport with destination playfield 0). The client snaps in place
        /// from a soft N3Teleport and keeps its heading; no zone transfer and no redirect.
        /// </summary>
        static void FinishIntrazoneLineTeleport(Player player, Playfield source, Vector3 landing, int destinationKey)
        {
            player.Motor.Warp(landing);
            player.Session!.SendIntrazoneTeleport(landing, player.Rotation, destinationKey);
            AnnounceArrival(player, source, landing);
        }

        /// <summary>Other clients only see the N3Teleport's effect through a normal move.</summary>
        static void AnnounceArrival(Player player, Playfield source, Vector3 landing)
        {
            PlayfieldLocality? locality = source.GetService<PlayfieldLocality>();
            if (locality == null)
                return;

            locality.Announce(
                player,
                new CharDCMoveMessage
                {
                    Identity = player.Identity,
                    Unknown = 0x00,
                    MoveType = (byte)MovementAction.FullStop,
                    Heading = new MsgQuaternion
                    {
                        X = player.Rotation.xf,
                        Y = player.Rotation.yf,
                        Z = player.Rotation.zf,
                        W = player.Rotation.wf
                    },
                    Coordinates = new MsgVector3
                    {
                        X = landing.xf,
                        Y = landing.yf,
                        Z = landing.zf
                    },
                    Unknown1 = 0,
                    AuxA = 0,
                    AuxB = 0
                },
                includeSelf: false);
        }

        /// <summary>
        /// Walk-in proxy. Args are <c>{PlayfieldDoor, destPlayfield, doorIndex, sourceDoor, ...}</c>.
        /// A source door of 0 uses <see cref="SpellCriteria.SourceDoorInstance"/>. The destination
        /// door becomes the way back, unless that door already has its own vicinity teleport.
        /// Jobe Platform's return rings instead name a destination line and land on its midpoint.
        /// </summary>
        static bool TeleportProxy(Player player, ItemSpell spell, SpellCriteria? criteria)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseProxyDestination(
                    spell.Arguments,
                    PortalDoorLandingResolver.ProxyEntryDoorClearance,
                    recordsReturn: true,
                    out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            IGameData gameData = source.GetRequiredService<IGameData>();
            int sourceDoor = criteria?.SourceDoorInstance ?? 0;
            if (spell.TryReadInt(3, out int namedDoor) && namedDoor != 0)
                sourceDoor = namedDoor;

            int destPlayfield = destination.PlayfieldId;
            int destDoor = destination.DoorInstance;
            bool landsOnLine = destination.Kind == PortalLandingKind.DestinationMidpoint;
            if (sourceDoor != 0
                && gameData.TryGetTeleportRoute(
                    source.Identity.Instance,
                    (int)IdentityType.Door,
                    unchecked((uint)sourceDoor),
                    out int routedPlayfield,
                    out int routedType,
                    out uint routedInstance))
            {
                // A route replaces the raw door. Door 0 on Jobe Platform is a teleporting ring,
                // which is where every unrouted proxy was landing.
                if (routedType != (int)IdentityType.Door
                    || routedPlayfield <= 0
                    || routedPlayfield > 0xFFFF
                    || (routedInstance & 0xFF000000u) != 0xC0000000u
                    || (routedInstance & 0xFFFFu) != (uint)routedPlayfield)
                    return false;

                destPlayfield = routedPlayfield;
                destDoor = unchecked((int)routedInstance);
                landsOnLine = false;
            }

            if (destPlayfield == source.Identity.Instance)
                return false;

            Vector3 landing;
            Quaternion heading;
            if (landsOnLine)
            {
                if (!PortalDoorLandingResolver.TryResolveMidpointLanding(
                        DestinationsCatalog.Instance,
                        destPlayfield,
                        destination.DestinationIndex,
                        out landing,
                        out heading))
                    return false;
                ClearProxyReturn(player);
            }
            else
            {
                if (!PortalDoorLandingResolver.TryResolveDoorLanding(
                        gameData.GetPlayfieldGeometry(destPlayfield),
                        destDoor,
                        destination.DoorClearance,
                        out landing,
                        out heading))
                    return false;
                player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, source.Identity.Instance, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.ExternalDoorInstance, sourceDoor, StatDetail.Base, dirty: true);
            }

            player.Rotation = heading;

            Playfield arrival = source.GetRequiredService<PlayfieldManager>().GetOrCreate(destPlayfield);
            if (!landsOnLine && arrival is ACGPlayfield acg)
                acg.World?.RegisterExitProxyDoor(destDoor);

            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);
            player.Session.TransferToPlayfield(arrival, landing, heading);
            return true;
        }

        /// <summary>
        /// One-way proxy teleport used by Grid enter terminals and similar OnUse machines. A full
        /// argument list names a Destinations.dat line and lands on its midpoint; otherwise the
        /// destination is packed as PlayfieldDoor (playfield + door index) and the landing dynel
        /// may be a Door or a Terminal with that packed instance.
        /// </summary>
        static bool TeleportProxy2(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseProxyDestination(
                    spell.Arguments,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            if (destination.PlayfieldId == source.Identity.Instance)
                return false;

            Vector3 landing;
            Quaternion heading;
            if (destination.Kind == PortalLandingKind.DestinationMidpoint)
            {
                if (!PortalDoorLandingResolver.TryResolveMidpointLanding(
                        DestinationsCatalog.Instance,
                        destination.PlayfieldId,
                        destination.DestinationIndex,
                        out landing,
                        out heading))
                    return false;
            }
            else if (!PortalDoorLandingResolver.TryResolveProxyLanding(
                    source.GetRequiredService<IGameData>().GetPlayfieldGeometry(destination.PlayfieldId),
                    destination.DoorInstance,
                    destination.DoorClearance,
                    out landing,
                    out heading))
                return false;

            // TeleportProxy2 is one-way: clear any stale return door so exit proxies cannot pull
            // the character back to an unrelated entry.
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            player.Rotation = heading;

            Playfield destPlayfield = source.GetRequiredService<PlayfieldManager>()
                .GetOrCreate(destination.PlayfieldId);
            player.Session.TransferToPlayfield(destPlayfield, landing, heading);
            return true;
        }

        /// <summary>
        /// Catalog shape from nano 205606: SpawnMonster2("KHAL", 73, 3600) — hash, level, lifetime.
        /// Lifetime is stored on the function; one-shot summons do not hash-respawn.
        /// </summary>
        static bool SpawnMonster2(Character target, ItemSpell spell)
        {
            Playfield? playfield = target.Playfield;
            if (playfield == null)
                return false;

            if (!spell.TryReadString(0, out string hash) || hash.Length == 0)
                return false;
            if (!spell.TryReadInt(1, out int level) || level <= 0)
                return false;

            IGameData gameData = playfield.GetRequiredService<IGameData>();
            if (!gameData.CanResolveMobHash(hash))
                return false;

            try
            {
                IReadOnlyList<NpcCharacter> spawned = playfield.GetRequiredService<SpawnService>().SpawnBranches(
                    hash,
                    target.Position,
                    target.Rotation,
                    level,
                    SpawnSource.Summoned);
                return spawned.Count > 0;
            }
            catch (Exception exception) when (exception is KeyNotFoundException
                or InvalidOperationException
                or ArgumentException)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SpawnMonster2 failed hash={0} level={1} character={2}: {3}",
                        hash,
                        level,
                        target.Identity.Instance,
                        exception.Message));
                return false;
            }
        }

        /// <summary>
        /// Removes the item this event was aimed at: the item used on a target, or the item
        /// whose own OnUse is running. Criteria on the function decide when it fires.
        /// </summary>
        static bool DestroySubject(Character target, SpellCriteria? criteria)
        {
            if (target is not Player player || criteria?.Subject == null)
                return false;

            return criteria.Subject.DestroyOne(player, criteria.SubjectSlot);
        }

        static bool UploadNano(Player player, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            // A nano already in the list is not uploaded again, and the crystal is not spent.
            if (!player.TryAddUploadedNano(nanoId))
            {
                AlreadyUploaded(player);
                return false;
            }

            player.MarkUploadedNanoDirty(nanoId);
            player.Playfield?.GetRequiredService<InventoryFlushService>().NotifyDirty(player);

            if (player.Session == null)
                return true;

            player.Session.Send(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.UploadNano,
                    Unknown1 = 0,
                    Target = player.Identity,
                    Parameter1 = (int)IdentityType.NanoProgram,
                    Parameter2 = nanoId,
                    Unknown2 = 0
                });
            return true;
        }

        static void AlreadyUploaded(Player player)
        {
            if (player.Session == null)
                return;

            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = "You already know that nano program.",
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }

    }
}
