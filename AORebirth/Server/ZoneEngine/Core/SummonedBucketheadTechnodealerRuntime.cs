namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    /// <summary>
    /// Nano 300439 Summon Buckethead Technodealer — Capture 20260723-061619.
    /// SpawnMonster2("BKTH", 220, 600): SCFU NPC + VendingMachineFullUpdate (template 99566),
    /// Use NPC → ShopUpdate (46 slots). Lifetime arg = 600 seconds.
    /// </summary>
    public static class SummonedBucketheadTechnodealerRuntime
    {
        private static readonly object TimerGate = new object();

        private static readonly Dictionary<int, Timer> DespawnTimers = new Dictionary<int, Timer>();

        /// <summary>
        /// Crystal item 300440 uploads nano 300439. Remap mistaken uploaded/cast ids.
        /// </summary>
        public static int NormalizeNanoId(int nanoId)
        {
            if (nanoId == CapturedBucketheadTechnodealerContentProvider.PremiumCrystalItemId)
            {
                return CapturedBucketheadTechnodealerContentProvider.SummonNanoId;
            }

            return nanoId;
        }

        public static bool IsSummonNano(int nanoId)
        {
            return NormalizeNanoId(nanoId) == CapturedBucketheadTechnodealerContentProvider.SummonNanoId;
        }

        public static bool HasUploadedSummonNano(ICharacter character, int nanoId)
        {
            if (character == null || character.UploadedNanos == null)
            {
                return false;
            }

            int normalized = NormalizeNanoId(nanoId);
            return character.UploadedNanos.Any(
                x => x.NanoId == normalized
                     || x.NanoId == CapturedBucketheadTechnodealerContentProvider.PremiumCrystalItemId
                     || x.NanoId == CapturedBucketheadTechnodealerContentProvider.SummonNanoId);
        }

        /// <summary>
        /// After CastNano OnUse: ensure spawn ran (SpawnMonster2 may be missing from FunctionCollection).
        /// </summary>
        public static void EnsureSpawnedAfterCast(ICharacter owner, int nanoId)
        {
            if (owner == null || NormalizeNanoId(nanoId) != CapturedBucketheadTechnodealerContentProvider.SummonNanoId)
            {
                return;
            }

            CapturedBucketheadTechnodealerRuntimeDefinition existing;
            if (CapturedBucketheadTechnodealerRuntimeRegistry.TryGetByOwner(
                    owner.Identity.Instance,
                    out existing)
                && existing != null
                && existing.VendorIdentity.Instance != 0
                && TryGetVendor(existing.VendorIdentity) != null)
            {
                return;
            }

            if (existing != null)
            {
                Despawn(existing.NpcIdentity.Instance);
            }

            TrySpawn(
                owner,
                CapturedBucketheadTechnodealerContentProvider.MobHash,
                CapturedBucketheadTechnodealerContentProvider.Level,
                600);
        }

        internal static Vendor TryGetVendor(Identity vendorIdentity)
        {
            if (vendorIdentity.Instance == 0)
            {
                return null;
            }

            try
            {
                Vendor byIdentity = Pool.Instance.GetObject<Vendor>(vendorIdentity);
                if (byIdentity != null)
                {
                    return byIdentity;
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool TrySpawn(ICharacter owner, string hash, int level, int lifetimeSeconds)
        {
            if (owner == null
                || owner.Playfield == null
                || !string.Equals(
                    hash,
                    CapturedBucketheadTechnodealerContentProvider.MobHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            CapturedBucketheadTechnodealerRuntimeDefinition existing;
            if (CapturedBucketheadTechnodealerRuntimeRegistry.TryGetByOwner(
                    owner.Identity.Instance,
                    out existing))
            {
                Despawn(existing.NpcIdentity.Instance);
            }

            int spawnLevel = level > 0 ? level : CapturedBucketheadTechnodealerContentProvider.Level;
            int lifetime = lifetimeSeconds > 0 ? lifetimeSeconds : 600;

            Character npc = CreateNpc(playfield, owner, spawnLevel);
            if (npc == null)
            {
                return false;
            }

            Vendor vendor = TryCreateVendor(playfield, npc);
            Identity vendorIdentity = vendor == null ? Identity.None : vendor.Identity;
            if (vendor != null)
            {
                playfield.RegisterDynel(vendor);
            }

            playfield.ActivateNpc(npc);
            playfield.AnnounceSpawnedCharacterVisibility(npc, Identity.None);

            // Capture order: SCFU then VendingMachineFullUpdate to caster.
            ZoneClient ownerClient = owner.Controller != null
                                         ? owner.Controller.Client as ZoneClient
                                         : null;
            if (ownerClient != null && vendor != null)
            {
                // Capture 20260723-114826 VMFU: empty name, Unk6=64, exact 8 stats.
                VendingMachineFullUpdateMessageHandler.Default.SendBucketheadTechnodealer(owner, vendor);
            }

            CapturedBucketheadTechnodealerRuntimeRegistry.Register(
                new CapturedBucketheadTechnodealerRuntimeDefinition(
                    playfield.Identity,
                    owner.Identity,
                    npc.Identity,
                    vendorIdentity,
                    lifetime,
                    vendor));

            ScheduleDespawn(npc.Identity.Instance, lifetime);

            if (vendor == null && ownerClient != null)
            {
                ChatTextMessageHandler.Default.Send(
                    owner,
                    "Buckethead Technodealer spawned without shop (vendor create failed).");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Buckethead Technodealer summoned owner="
                + owner.Identity
                + " npc="
                + npc.Identity
                + " vendor="
                + vendorIdentity
                + " lifetimeSec="
                + lifetime
                + " evidence="
                + CapturedBucketheadTechnodealerContentProvider.Evidence);

            return true;
        }

        public static void Despawn(int npcInstance)
        {
            CapturedBucketheadTechnodealerRuntimeDefinition runtime;
            if (!CapturedBucketheadTechnodealerRuntimeRegistry.Remove(npcInstance, out runtime)
                || runtime == null)
            {
                CancelTimer(npcInstance);
                return;
            }

            CancelTimer(npcInstance);

            Playfield playfield = Pool.Instance.GetObject<Playfield>(
                Identity.None,
                runtime.PlayfieldIdentity);
            if (playfield == null)
            {
                return;
            }

            ICharacter npc = playfield.FindByIdentity<ICharacter>(runtime.NpcIdentity);
            if (npc != null)
            {
                playfield.DespawnNpcImmediately(npc);
            }

            if (runtime.VendorIdentity.Instance != 0)
            {
                Vendor vendor = Pool.Instance.GetObject<Vendor>(
                    runtime.PlayfieldIdentity,
                    runtime.VendorIdentity);
                if (vendor != null)
                {
                    playfield.Despawn(runtime.VendorIdentity);
                    playfield.UnregisterDynel(runtime.VendorIdentity);
                    Pool.Instance.RemoveObject(vendor);
                }
            }
        }

        private static void ScheduleDespawn(int npcInstance, int lifetimeSeconds)
        {
            CancelTimer(npcInstance);
            Timer timer = new Timer(
                _ =>
                    {
                        try
                        {
                            Despawn(npcInstance);
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Debug(
                                DebugInfoDetail.Error,
                                "Buckethead Technodealer despawn failed: " + ex.Message);
                        }
                    },
                null,
                Math.Max(1, lifetimeSeconds) * 1000,
                Timeout.Infinite);

            lock (TimerGate)
            {
                DespawnTimers[npcInstance] = timer;
            }
        }

        private static void CancelTimer(int npcInstance)
        {
            Timer timer;
            lock (TimerGate)
            {
                if (!DespawnTimers.TryGetValue(npcInstance, out timer))
                {
                    return;
                }

                DespawnTimers.Remove(npcInstance);
            }

            timer.Dispose();
        }

        private static Character CreateNpc(Playfield playfield, ICharacter owner, int level)
        {
            Character character = null;
            try
            {
                int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
                var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
                var controller = new NPCController();
                character = new Character(playfield.Identity, identity, controller);
                character.Read();
                controller.Character = character;
                character.Playfield = playfield;
                character.Name = CapturedBucketheadTechnodealerContentProvider.DisplayName;
                character.FirstName = string.Empty;
                character.LastName = string.Empty;

                Coordinate spawn = SpawnInFrontOf(owner, 1.0f);
                character.Coordinates(spawn);
                character.RawHeading = new Quaternion(
                    owner.RawHeading.xf,
                    owner.RawHeading.yf,
                    owner.RawHeading.zf,
                    owner.RawHeading.wf);

                SetStat(character, StatIds.side, (int)Side.Neutral);
                SetStat(character, StatIds.fatness, (int)Fatness.Normal);
                SetStat(character, StatIds.breed, (int)Breed.Monster);
                SetStat(character, StatIds.sex, (int)Gender.None);
                SetStat(character, StatIds.race, 1);
                SetStat(character, StatIds.flags, CapturedBucketheadTechnodealerContentProvider.CharacterFlags);
                SetStat(character, StatIds.accountflags, 0);
                SetStat(character, StatIds.expansion, 0);
                // Capture 20260723-114826 SCFU: NpcFamily=0 (HasSmallNpcFamily).
                SetStat(character, StatIds.npcfamily, 0);
                SetStat(character, StatIds.losheight, 0);
                SetStat(character, StatIds.monsterdata, CapturedBucketheadTechnodealerContentProvider.MonsterData);
                SetStat(character, StatIds.monsterscale, CapturedBucketheadTechnodealerContentProvider.MonsterScale);
                SetStat(character, StatIds.headmesh, 0);
                SetStat(character, StatIds.visualflags, CapturedBucketheadTechnodealerContentProvider.VisualFlags);
                SetStat(character, StatIds.currentmovementmode, 3);
                SetStat(character, StatIds.prevmovementmode, 3);
                SetStat(character, StatIds.runspeed, CapturedBucketheadTechnodealerContentProvider.RunSpeed);
                SetStat(character, StatIds.level, level);
                SetStat(character, StatIds.life, CapturedBucketheadTechnodealerContentProvider.Health);
                SetStat(character, StatIds.health, CapturedBucketheadTechnodealerContentProvider.Health);

                character.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    character.Textures.Add(new AOTextures(i, 0));
                }

                character.MeshLayer.Clear();
                character.SocialMeshLayer.Clear();
                character.DoNotDoTimers = true;
                controller.AiProfile = NpcAiProfile.Passive;
                return character;
            }
            catch (Exception exception)
            {
                if (character != null)
                {
                    Pool.Instance.RemoveObject(character);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Buckethead Technodealer NPC refused: " + exception.Message);
                return null;
            }
        }

        private static Vendor TryCreateVendor(Playfield playfield, Character character)
        {
            Vendor vendor = null;
            try
            {
                // Avoid Vendor(int) — it requires ItemNamesDao row and NRE's when missing.
                // Hash ctor with unknown hash falls back to template 46522 without ItemNames.
                vendor = CreateVendorEntity(playfield, character);
                return vendor;
            }
            catch (Exception exception)
            {
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Buckethead Technodealer vendor refused: " + exception.Message);
                return null;
            }
        }

        private static Vendor CreateVendorEntity(Playfield playfield, Character character)
        {
            int captureTemplateId = CapturedBucketheadTechnodealerContentProvider.VendorTemplateId;
            var items = new List<KeyValuePair<int, Item>>();
            foreach (CapturedBucketheadTechnodealerStockDefinition stock in
                CapturedBucketheadTechnodealerContentProvider.Stock)
            {
                if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                    || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                {
                    continue;
                }

                items.Add(
                    new KeyValuePair<int, Item>(
                        stock.Slot,
                        new Item(stock.Quality, stock.LowId, stock.HighId)));
            }

            if (items.Count == 0)
            {
                throw new InvalidOperationException("no stock items resolved");
            }

            var identity =
                new Identity
                {
                    Type = IdentityType.VendingMachine,
                    Instance = Pool.Instance.GetFreeInstance<Vendor>(0x70000000, IdentityType.VendingMachine)
                };

            // Unknown hash → Vendor uses fallback template without ItemNamesDao.
            Vendor vendor = new Vendor(playfield.Identity, identity, "__buckethead_runtime__");
            try
            {
                vendor.Name = CapturedBucketheadTechnodealerContentProvider.DisplayName;
                vendor.NpcIdentity = character.Identity;
                Coordinate coords = character.Coordinates();
                vendor.RawCoordinates = new AORebirth.Core.Vector.Vector3(coords.x, coords.y, coords.z);
                vendor.Heading = new Quaternion(
                    character.RawHeading.xf,
                    character.RawHeading.yf,
                    character.RawHeading.zf,
                    character.RawHeading.wf);
                vendor.Playfield = playfield;

                // Prefer capture template stats when present (VMFU still uses capture filler).
                if (ItemLoader.ItemList.ContainsKey(captureTemplateId))
                {
                    foreach (KeyValuePair<int, int> stat in ItemLoader.ItemList[captureTemplateId].Stats)
                    {
                        vendor.Stats[stat.Key].Value = stat.Value;
                    }
                }

                vendor.Stats[(int)StatIds.staticinstance].Value = captureTemplateId;

                int page = vendor.BaseInventory.StandardPage;
                vendor.BaseInventory[page].List().Clear();
                foreach (KeyValuePair<int, Item> item in items)
                {
                    vendor.BaseInventory.AddToPage(page, item.Key, item.Value);
                }

                return vendor;
            }
            catch
            {
                Pool.Instance.RemoveObject(vendor);
                throw;
            }
        }

        private static Coordinate SpawnInFrontOf(ICharacter owner, float distance)
        {
            Coordinate coords = owner.Coordinates();
            // AO heading: yaw from quaternion Y/W — forward on XZ.
            float y = owner.RawHeading.yf;
            float w = owner.RawHeading.wf;
            float sin = 2f * y * w;
            float cos = 1f - (2f * y * y);
            // Capture 20260723-061619: caster→vendor ΔX≈+1 with heading Y=-0.411 W=0.912.
            float dx = cos * distance;
            float dz = sin * distance;
            return new Coordinate { x = coords.x + dx, y = coords.y, z = coords.z + dz };
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
            character.Stats[stat].Value = value;
        }
    }
}
