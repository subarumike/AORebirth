namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// Attack-on-sight for RK mission interior mobs.
    /// Must NOT call CapturedEnemyCombatRuntime.Prepare (subway quarantine).
    /// Gun mesh trash: EquippedWeapon + RH WIFU (short range). Melee: SIW1.
    /// Aggro matches pistol range (8m); 3m left gun trash idle until inside collision.
    /// Death Parameter2=501.
    /// </summary>
    internal static class MissionInstanceMobCombat
    {
        // Mike: pull only when nearly in melee/collision (2m). Gun attack range stays MissionTrashGunRange.
        private const float AggroRadius = 2.0f;

        private const int CapturedDeathAnimationKey = 501;

        private const int CapturedSiw1LowTemplate = 0x023566;

        private const int CapturedSiw1HighTemplate = 0x023567;

        private const int CapturedSiw1Tag = 0x53495731;

        private const int CapturedSawUnknown = 20;

        // Capture 20260725-185432 WIFU RH pistols on Fresh trash.
        private static readonly int[] MissionTrashPistolTemplates =
        {
            121564, 121567, 121568, 121570, 121571
        };

        private const double MissionTrashGunRange = 8.0d;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

        private static readonly HashSet<int> FindItemHosts = new HashSet<int>();

        public static void RegisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Add(identity.Instance);
            }
        }

        /// <summary>
        /// Mission trash reuses dynel ids 1000001+ every entry. Clear before respawn so
        /// leftover FightingTarget / AggressiveMobs membership cannot suppress aggro.
        /// </summary>
        public static void UnregisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Remove(identity.Instance);
                FindItemHosts.Remove(identity.Instance);
            }
        }

        public static void RegisterFindItemHost(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                FindItemHosts.Add(identity.Instance);
            }
        }

        public static bool IsAggressive(Identity identity)
        {
            lock (Gate)
            {
                return AggressiveMobs.Contains(identity.Instance);
            }
        }

        public static bool IsFindItemHost(Identity identity)
        {
            lock (Gate)
            {
                return FindItemHosts.Contains(identity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
        }

        public static bool HasGunMesh(Character mob)
        {
            if (mob == null || mob.MeshLayer == null)
            {
                return false;
            }

            try
            {
                List<AORebirth.Core.Textures.AOMeshs> meshes = mob.MeshLayer.GetMeshs();
                if (meshes == null)
                {
                    return false;
                }

                for (int i = 0; i < meshes.Count; i++)
                {
                    AORebirth.Core.Textures.AOMeshs mesh = meshes[i];
                    // Layer 2 = attached weapon only. Body pose meshes (~19900–21000 on layer 0)
                    // falsely marked melee trash as gun and equipped the wrong weapon.
                    if (mesh != null && mesh.Layer == 2 && mesh.Mesh > 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool TryPrepareCombat(Character mob, NPCController controller, int level)
        {
            if (mob == null || controller == null)
            {
                return false;
            }

            int lvl = level > 0 ? level : 1;
            int minDamage = Math.Max(2, lvl);
            int maxDamage = Math.Max(4, lvl + (lvl / 2) + 2);
            if (maxDamage < minDamage)
            {
                maxDamage = minDamage;
            }

            CapturedEnemyCombatRuntimeRegistry.Remove(mob.Identity.Instance);

            CapturedEnemyCombatContract contract;
            // Gun pose / attached weapon mesh → EquippedWeapon + WIFU for client gun anim.
            // Range stays short (8m) — 18m made trash snipe across the mish.
            int pistolTemplate = ResolveMissionPistolTemplate(mob.Identity.Instance);
            bool gunTrash = HasGunMesh(mob) && TryEquipMissionPistol(mob, lvl, pistolTemplate);
            if (gunTrash)
            {
                // Must match Item.Quality after template clamp — mission QL ≠ ACGItemLevel
                // quarantines WIFU and leaves Passive/Unresolved (no SAW/aggro).
                int ql = ResolveMissionPistolQuality(lvl, pistolTemplate);

                contract = CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                        "mission-instance-gun-equipped-20260725-185432",
                        mob.Identity.Instance,
                        pistolTemplate,
                        pistolTemplate,
                        ql,
                        (int)WeaponSlots.Righthand,
                        false,
                        minDamage,
                        maxDamage,
                        0,
                        MissionTrashGunRange,
                        0.25d,
                        0.0d,
                        0.5d,
                        2.0d,
                        true,
                        true,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoUnknown,
                        // Live 185432 Fresh Marksman SAW Unknown1–4 = 30 (not subway 32).
                        30,
                        30,
                        30,
                        30,
                        0,
                        3,
                        0,
                        0,
                        0,
                        0)
                    .WithCapturedWeapon(
                        BuildMissionPistolWeaponDefinition(mob.Identity.Instance, ql, pistolTemplate));
            }
            else
            {
                contract = CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                    "mission-instance-mob-siw1-20260725-185432",
                    mob.Identity.Instance,
                    NpcAiProfile.Aggressive,
                    minDamage,
                    maxDamage,
                    2.0d,
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            CapturedSiw1LowTemplate,
                            CapturedSiw1HighTemplate,
                            CapturedSiw1Tag,
                            "SIW1")
                    },
                    0,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    0,
                    0,
                    0,
                    NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                    0,
                    0,
                    3,
                    CapturedSiw1Tag,
                    0,
                    false,
                    new[] { minDamage, maxDamage },
                    new[] { 0.0d },
                    new[] { 0.25d },
                    new[] { 2.0d },
                    0,
                    false,
                    NpcCombatAttackRules.MaxMeleeCombatDistance,
                    true);
            }

            CapturedEnemyCombatRuntimeRegistry.Register(mob.Identity.Instance, contract);
            if (gunTrash && !contract.IsCombatReady)
            {
                gunTrash = false;
                contract = CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                    "mission-instance-mob-gun-fallback-20260725-185432",
                    mob.Identity.Instance,
                    NpcAiProfile.Aggressive,
                    minDamage,
                    maxDamage,
                    2.0d,
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            CapturedSiw1LowTemplate,
                            CapturedSiw1HighTemplate,
                            CapturedSiw1Tag,
                            "SIW1")
                    },
                    0,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    CapturedSawUnknown,
                    0,
                    0,
                    0,
                    NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                    0,
                    0,
                    3,
                    CapturedSiw1Tag,
                    0,
                    false,
                    new[] { minDamage, maxDamage },
                    new[] { 0.0d },
                    new[] { 0.25d },
                    new[] { 2.0d },
                    0,
                    false,
                    NpcCombatAttackRules.MaxMeleeCombatDistance,
                    true);
                CapturedEnemyCombatRuntimeRegistry.Register(mob.Identity.Instance, contract);
            }

            if (gunTrash && contract.IsCombatReady)
            {
                IInventoryPage weaponPage;
                IItem pistol = null;
                if (mob.BaseInventory != null
                    && mob.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage)
                    && weaponPage != null)
                {
                    pistol = weaponPage[(int)WeaponSlots.Righthand];
                }

                if (pistol != null)
                {
                    CapturedEnemyCombatRuntimeRegistry.Register(mob.Identity.Instance, contract, pistol);
                }

                WeaponItemFullUpdate.SendRightHandWeaponDefinition(mob, true);

                CapturedEnemyCombatContract live;
                if (!CapturedEnemyCombatRuntimeRegistry.TryGet(mob.Identity.Instance, out live)
                    || !live.IsCombatReady)
                {
                    // WIFU visibility quarantined the gun contract — fall back to SIW1 melee.
                    gunTrash = false;
                    contract = CapturedEnemyCombatContract.CapturedFixedPacketSequence(
                        "mission-instance-mob-gun-wifu-fallback-20260725-185432",
                        mob.Identity.Instance,
                        NpcAiProfile.Aggressive,
                        minDamage,
                        maxDamage,
                        2.0d,
                        new[]
                        {
                            new CapturedEnemySpecialAttackDefinition(
                                CapturedSiw1LowTemplate,
                                CapturedSiw1HighTemplate,
                                CapturedSiw1Tag,
                                "SIW1")
                        },
                        0,
                        CapturedSawUnknown,
                        CapturedSawUnknown,
                        CapturedSawUnknown,
                        CapturedSawUnknown,
                        0,
                        0,
                        0,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                        0,
                        0,
                        3,
                        CapturedSiw1Tag,
                        0,
                        false,
                        new[] { minDamage, maxDamage },
                        new[] { 0.0d },
                        new[] { 0.25d },
                        new[] { 2.0d },
                        0,
                        false,
                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                        true);
                    CapturedEnemyCombatRuntimeRegistry.Register(mob.Identity.Instance, contract);
                }
            }

            controller.AiProfile = NpcAiProfile.Aggressive;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.mindamage, (uint)minDamage);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.maxdamage, (uint)maxDamage);
            mob.Stats.SetBaseValueWithoutTriggering(436, 91u);
            mob.Stats.SetBaseValueWithoutTriggering(339, 91u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.corpseanimkey, (uint)CapturedDeathAnimationKey);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.dieanim, (uint)CapturedDeathAnimationKey);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.itemanim, (uint)CapturedDeathAnimationKey);

            CapturedEnemyCombatContract registryContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(mob.Identity.Instance, out registryContract))
            {
                return registryContract.IsCombatReady;
            }

            return contract.IsCombatReady;
        }

        private static int ResolveMissionPistolTemplate(int salt)
        {
            int[] templates = MissionTrashPistolTemplates;
            if (templates == null || templates.Length == 0)
            {
                return 121568;
            }

            for (int i = 0; i < templates.Length; i++)
            {
                int candidate = templates[Math.Abs(salt + i) % templates.Length];
                if (candidate > 0
                    && ItemLoader.ItemList.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return templates[Math.Abs(salt) % templates.Length];
        }

        private static CapturedEnemyWeaponDefinition BuildMissionPistolWeaponDefinition(
            int evidenceSourceIdentity,
            int quality,
            int pistolTemplate)
        {
            int slot = (int)WeaponSlots.Righthand;
            int ql = quality > 0 ? quality : 1;
            int template = pistolTemplate > 0 ? pistolTemplate : 121568;
            // Capture 185432: Flags 67110401 for 121570, else 67109889.
            uint flags = template == 121570 ? 67110401u : 67109889u;
            return new CapturedEnemyWeaponDefinition(
                "mission-trash-pistol-" + template,
                evidenceSourceIdentity,
                0,
                0x0b,
                slot,
                1000015,
                0,
                (short)(0x0100 | (slot & 0xff)),
                new[]
                {
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.Flags, flags),
                    new CapturedEnemyWeaponStatDefinition(
                        CharacterStat.StaticInstance,
                        (uint)template),
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.ACGItemLevel, (uint)ql),
                    new CapturedEnemyWeaponStatDefinition(
                        CharacterStat.ACGItemTemplateID,
                        (uint)template),
                    new CapturedEnemyWeaponStatDefinition(
                        CharacterStat.ACGItemTemplateID2,
                        (uint)template),
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.MultipleCount, 1u),
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.Energy, unchecked((uint)(-1))),
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.AttackDelay, 235u),
                    new CapturedEnemyWeaponStatDefinition(CharacterStat.RechargeDelay, 235u)
                },
                0);
        }

        /// <summary>
        /// Item(ql, same, same) clamps Quality to the template's own QL. Contract ACGItemLevel
        /// must use that clamped value or MatchesCapturedWeapon fails → WIFU quarantine.
        /// </summary>
        private static int ResolveMissionPistolQuality(int requestedQl, int pistolTemplate)
        {
            int ql = requestedQl > 0 ? requestedQl : 1;
            if (ql > 23)
            {
                ql = 23;
            }

            try
            {
                var probe = new Item(ql, pistolTemplate, pistolTemplate);
                return probe.Quality > 0 ? probe.Quality : 1;
            }
            catch
            {
                return 1;
            }
        }

        private static bool TryEquipMissionPistol(Character mob, int level, int pistolTemplate)
        {
            if (mob == null || mob.BaseInventory == null || pistolTemplate <= 0)
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(pistolTemplate))
            {
                return false;
            }

            IInventoryPage weaponPage;
            if (!mob.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage)
                || weaponPage == null)
            {
                return false;
            }

            int slot = (int)WeaponSlots.Righthand;
            if (!weaponPage.ValidSlot(slot))
            {
                return false;
            }

            int requestedQl = level > 0 ? level : 1;
            int ql = ResolveMissionPistolQuality(requestedQl, pistolTemplate);

            // Capture WIFU stats must be stamped or MatchesCapturedWeapon fails → quarantine
            // (Passive/Unresolved) and mobs never emit SAW/Attack.
            CapturedEnemyWeaponDefinition definition =
                BuildMissionPistolWeaponDefinition(mob.Identity.Instance, ql, pistolTemplate);

            try
            {
                IItem existing = weaponPage[slot];
                if (existing != null)
                {
                    ApplyMissionPistolStats(existing as Item, definition);
                    return true;
                }

                var weapon = new Item(ql, pistolTemplate, pistolTemplate) { MultipleCount = 1 };
                ApplyMissionPistolStats(weapon, definition);
                return weaponPage.Add(slot, weapon) == InventoryError.OK;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyMissionPistolStats(Item weapon, CapturedEnemyWeaponDefinition definition)
        {
            if (weapon == null || definition == null || definition.Stats == null)
            {
                return;
            }

            for (int i = 0; i < definition.Stats.Length; i++)
            {
                CapturedEnemyWeaponStatDefinition stat = definition.Stats[i];
                if (stat == null)
                {
                    continue;
                }

                int value = unchecked((int)stat.Value);
                if (stat.Stat == CharacterStat.Flags)
                {
                    weapon.Flags = value;
                }
                else if (stat.Stat == CharacterStat.MultipleCount)
                {
                    weapon.MultipleCount = value;
                }
                else if (stat.Stat == CharacterStat.Energy)
                {
                    weapon.SetAttribute((int)StatIds.energy, value);
                }
                else if (stat.Stat == CharacterStat.AttackDelay)
                {
                    weapon.SetAttribute((int)StatIds.itemdelay, value);
                }
                else if (stat.Stat == CharacterStat.RechargeDelay)
                {
                    weapon.SetAttribute((int)StatIds.rechargedelay, value);
                }
            }
        }

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            lock (Gate)
            {
                if (!AggressiveMobs.Contains(npc.Identity.Instance))
                {
                    return null;
                }
            }

            if (npc.FightingTarget.Instance != 0 || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            Coordinate npcPos = npc.Coordinates();
            ICharacter nearest = null;
            double nearestDist = AggroRadius;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, AggroRadius);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                double dist = candidate.Coordinates().coordinate.Distance2D(npcPos.coordinate);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
