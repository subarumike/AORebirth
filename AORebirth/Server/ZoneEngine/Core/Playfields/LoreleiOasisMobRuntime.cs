namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260721-loralei: desert oasis Reets + Rollerrats + quest Lolly (7985CAEC).
    /// Reets are passive until attacked; nearby reets assist (social aggro).
    /// Rollerrats are attack-on-sight (capture 20260722-233205: Attack before player Attack).
    /// Lorelei bartender spawn is handled by AreteLandingSpawn.
    /// </summary>
    internal static class LoreleiOasisMobRuntime
    {
        private sealed class MobSlot
        {
            public string Name { get; private set; }

            public int Level { get; private set; }

            public int Health { get; private set; }

            public int Scale { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }

            public MobSlot(string name, int level, int health, int scale, float x, float y, float z)
            {
                this.Name = name;
                this.Level = level;
                this.Health = health;
                this.Scale = scale;
                this.X = x;
                this.Y = y;
                this.Z = z;
            }
        }

        private const int AreteLandingPlayfieldId = 6553;

        private const int LollyFixedInstance = unchecked((int)0x7985CAEC);

        private const int ReetMonsterData = 30365;

        private const int RollerratMonsterData = 17687;

        private const float SocialAggroRadiusMeters = 12f;

        // Capture 20260722-233205: first Rollerrat AOS FollowTarget path ~12–18m to player.
        private const float RollerratAutomaticAggroRadiusMeters = 15f;

        private const double RespawnSeconds = 30.0;

        private const float LollySpawnX = 3360.186f;

        private const float LollySpawnY = 3.527f;

        private const float LollySpawnZ = 620.348f;

        // Capture 20260721-loralei scfu RawBodyHex ExtTex (48 bytes) — Lolly material 95855.
        private static readonly byte[] LollyExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x63, 0x75, 0x74, 0x65, 0x5F, 0x62, 0x69, 0x72, 0x64, 0x79, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0x6F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture Desert Reet material 95858.
        private static readonly byte[] DesertReetExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x63, 0x75, 0x74, 0x65, 0x5F, 0x62, 0x69, 0x72, 0x64, 0x79, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture Greedy material 95859.
        private static readonly byte[] GreedyDesertReetExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x63, 0x75, 0x74, 0x65, 0x5F, 0x62, 0x69, 0x72, 0x64, 0x79, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture Rollerrat Material #1 / 39966.
        private static readonly byte[] RollerratExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x31, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9C, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture Gnarl Material #1 / 95949.
        private static readonly byte[] GnarlExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x31, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0xCD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        // Capture 20260721-loralei ScfuUnknown1Hex for Desert Reet / Rollerrat.
        private static readonly byte[] OasisMobCapturedScfuUnknown1 =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00
            };


        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextReetRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        private static readonly Dictionary<int, DateTime[]> NextRollerRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        private static readonly HashSet<int> LollyDespawnedPlayfields = new HashSet<int>();

        private static readonly object OasisGate = new object();

        private static readonly HashSet<int> OasisReetInstances = new HashSet<int>();

        private static readonly HashSet<int> OasisRollerratInstances = new HashSet<int>();

        private static readonly MobSlot[] DesertReetSlots =
            {
                new MobSlot("Desert Reet", 5, 58, 93, 3285.676760f, 4.930795f, 689.824500f),
                new MobSlot("Desert Reet", 6, 69, 93, 3365.803220f, 2.110000f, 594.795500f),
                new MobSlot("Desert Reet", 5, 58, 93, 3356.303470f, 2.955723f, 604.348633f),
                new MobSlot("Desert Reet", 6, 69, 93, 3351.431400f, 3.442523f, 599.837200f),
                new MobSlot("Desert Reet", 5, 58, 93, 3344.144780f, 3.937722f, 644.245500f),
                new MobSlot("Desert Reet", 6, 69, 93, 3321.169680f, 0.010000f, 650.742400f),
                new MobSlot("Desert Reet", 6, 69, 93, 3321.499760f, 0.010000f, 676.924561f),
                new MobSlot("Desert Reet", 5, 58, 93, 3301.305660f, 0.290518f, 694.266663f),
                new MobSlot("Desert Reet", 6, 69, 93, 3313.068850f, 0.010000f, 665.691400f),
                new MobSlot("Desert Reet", 5, 58, 93, 3314.008540f, 0.010000f, 708.599854f),
                new MobSlot("Desert Reet", 5, 58, 93, 3362.621000f, 5.934419f, 565.113400f),
                new MobSlot("Desert Reet", 5, 58, 93, 3375.664310f, 2.110000f, 585.995000f),
                new MobSlot("Desert Reet", 6, 69, 93, 3364.041750f, 2.110000f, 610.830900f),
                new MobSlot("Desert Reet", 6, 69, 93, 3350.786870f, 4.210948f, 626.358800f),
                new MobSlot("Desert Reet", 5, 58, 93, 3399.074220f, 2.110000f, 560.702300f),
                new MobSlot("Desert Reet", 5, 58, 93, 3397.174000f, 2.110000f, 575.071800f),
                new MobSlot("Desert Reet", 5, 58, 93, 3369.473140f, 2.407519f, 672.061340f),
                new MobSlot("Desert Reet", 5, 58, 93, 3360.018310f, 3.011502f, 699.918640f),
                new MobSlot("Desert Reet", 6, 69, 93, 3348.060790f, 2.251925f, 694.462769f),
                new MobSlot("Desert Reet", 6, 69, 93, 3351.919680f, 4.242593f, 658.161200f),
                new MobSlot("Desert Reet", 6, 69, 93, 3341.605000f, 3.798910f, 664.030640f),
                new MobSlot("Desert Reet", 5, 58, 93, 3389.780760f, 2.110000f, 593.955933f),
                new MobSlot("Greedy Desert Reet", 7, 80, 130, 3377.233150f, 2.110000f, 570.398600f),
            };

        // Capture 20260721-loralei enemy-dossier: Rollerrats on the path past the oasis Reets.
        private static readonly MobSlot[] RollerratSlots =
            {
                new MobSlot("Rollerrat", 5, 58, 125, 3423.55225f, 2.110000f, 691.272949f),
                new MobSlot("Rollerrat", 5, 58, 125, 3392.27124f, 3.010000f, 755.309631f),
                new MobSlot("Rollerrat", 6, 69, 125, 3392.08887f, 2.110000f, 680.059800f),
                new MobSlot("Rollerrat", 5, 58, 125, 3417.22949f, 2.110000f, 718.296631f),
                new MobSlot("Rollerrat", 6, 69, 125, 3379.18262f, 2.173428f, 662.932200f),
                new MobSlot("Rollerrat", 6, 69, 125, 3431.68200f, 2.110000f, 664.818054f),
                new MobSlot("Rollerrat", 6, 69, 125, 3384.95166f, 1.795237f, 639.372200f),
                new MobSlot("Rollerrat", 6, 69, 125, 3457.39575f, 4.289871f, 746.963300f),
                new MobSlot("Rollerrat", 5, 58, 125, 3387.48000f, 3.010000f, 744.358000f),
                new MobSlot("Gnarl the Roller", 7, 674, 200, 3396.06787f, 2.460354f, 721.528900f),
            };

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            if (npc.FightingTarget.Instance != 0)
            {
                return null;
            }

            lock (OasisGate)
            {
                if (!OasisRollerratInstances.Contains(npc.Identity.Instance))
                {
                    // Capture behavior: Reets are not attack-on-sight.
                    return null;
                }
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null || npc.RawCoordinates == null)
            {
                return null;
            }

            Coordinate npcCoord = npc.Coordinates();
            ICharacter best = null;
            double bestDistance = RollerratAutomaticAggroRadiusMeters;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, RollerratAutomaticAggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.RawCoordinates == null)
                {
                    continue;
                }

                double distance = candidate.Coordinates().Distance3D(npcCoord);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        public static ICharacter[] FindSocialAggroAllies(ICharacter npc, ICharacter target)
        {
            if (npc == null || target == null || npc.Playfield == null)
            {
                return new ICharacter[0];
            }

            lock (OasisGate)
            {
                if (!OasisReetInstances.Contains(npc.Identity.Instance))
                {
                    return new ICharacter[0];
                }
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return new ICharacter[0];
            }

            var allies = new List<ICharacter>();
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, SocialAggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is NPCController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance != 0)
                {
                    continue;
                }

                lock (OasisGate)
                {
                    if (!OasisReetInstances.Contains(candidate.Identity.Instance))
                    {
                        continue;
                    }
                }

                allies.Add(candidate);
            }

            return allies.ToArray();
        }

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Lolly the Reet", StringComparison.OrdinalIgnoreCase)
                || (name != null
                    && name.IndexOf("Lolly", StringComparison.OrdinalIgnoreCase) >= 0
                    && name.IndexOf("Reet", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                data = (byte[])LollyExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Greedy Desert Reet", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])GreedyDesertReetExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Desert Reet", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])DesertReetExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Gnarl the Roller", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])GnarlExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Rollerrat", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])RollerratExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        internal static bool TryGetCapturedScfuUnknown1(string name, out byte[] data)
        {
            if (TryGetExtendedTextureOverride(name, out _))
            {
                data = (byte[])OasisMobCapturedScfuUnknown1.Clone();
                return true;
            }

            data = null;
            return false;
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            // Same proven path as AlexArea / SurveillanceDroid: A004 + MonsterData + SCFU ExtTex.
            // (AreteLandingSpawn BART cannot show reet/rollerrat bodies.)
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            LollyDespawnedPlayfields.Remove(playfieldIdentity.Instance);
            NextReetRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[DesertReetSlots.Length];
            NextRollerRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[RollerratSlots.Length];
            DateTime[] reetTimers = NextReetRespawnUtcBySlot[playfieldIdentity.Instance];
            DateTime[] rollerTimers = NextRollerRespawnUtcBySlot[playfieldIdentity.Instance];
            int spawned = 0;
            for (int i = 0; i < DesertReetSlots.Length; i++)
            {
                try
                {
                    if (SpawnDesertReetSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                    {
                        reetTimers[i] = DateTime.MaxValue;
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "LoreleiOasisMobRuntime reet slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            for (int i = 0; i < RollerratSlots.Length; i++)
            {
                try
                {
                    if (SpawnRollerratSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                    {
                        rollerTimers[i] = DateTime.MaxValue;
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "LoreleiOasisMobRuntime rollerrat slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            try
            {
                if (SpawnLolly(playfield, playfieldIdentity, activateNpc) != null)
                {
                    spawned++;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LoreleiOasisMobRuntime Lolly spawn failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LoreleiOasisMobRuntime spawned="
                + spawned
                + "/"
                + (DesertReetSlots.Length + RollerratSlots.Length + 1)
                + " pf="
                + playfieldIdentity.Instance
                + " template=A004 source=20260721-loralei");
            if (spawned == 0)
            {
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
                NextReetRespawnUtcBySlot.Remove(playfieldIdentity.Instance);
                NextRollerRespawnUtcBySlot.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextReetRespawnUtcBySlot.Remove(playfieldInstance);
            NextRollerRespawnUtcBySlot.Remove(playfieldInstance);
            LollyDespawnedPlayfields.Remove(playfieldInstance);
            lock (OasisGate)
            {
                OasisReetInstances.Clear();
                OasisRollerratInstances.Clear();
            }
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            DateTime[] reetTimers;
            if (!NextReetRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out reetTimers)
                || reetTimers == null
                || reetTimers.Length != DesertReetSlots.Length)
            {
                reetTimers = new DateTime[DesertReetSlots.Length];
                NextReetRespawnUtcBySlot[playfieldIdentity.Instance] = reetTimers;
            }

            for (int i = 0; i < DesertReetSlots.Length; i++)
            {
                if (HasLivingMobNear(playfield, DesertReetSlots[i]))
                {
                    reetTimers[i] = DateTime.MaxValue;
                }
                else if (reetTimers[i] == DateTime.MaxValue)
                {
                    reetTimers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(reetTimers[i] > DateTime.UtcNow)
                         && SpawnDesertReetSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    reetTimers[i] = DateTime.MaxValue;
                }
            }

            DateTime[] rollerTimers;
            if (!NextRollerRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out rollerTimers)
                || rollerTimers == null
                || rollerTimers.Length != RollerratSlots.Length)
            {
                rollerTimers = new DateTime[RollerratSlots.Length];
                NextRollerRespawnUtcBySlot[playfieldIdentity.Instance] = rollerTimers;
            }

            for (int i = 0; i < RollerratSlots.Length; i++)
            {
                if (HasLivingMobNear(playfield, RollerratSlots[i]))
                {
                    rollerTimers[i] = DateTime.MaxValue;
                }
                else if (rollerTimers[i] == DateTime.MaxValue)
                {
                    rollerTimers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(rollerTimers[i] > DateTime.UtcNow)
                         && SpawnRollerratSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    rollerTimers[i] = DateTime.MaxValue;
                }
            }

            if (!LollyDespawnedPlayfields.Contains(playfieldIdentity.Instance)
                && !HasLivingLolly(playfield)
                && SpawnLolly(playfield, playfieldIdentity, activateNpc) != null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LoreleiOasisMobRuntime respawned Lolly pf=" + playfieldIdentity.Instance);
            }
        }

        public static void DespawnLolly(ICharacter source)
        {
            Playfield playfield = source == null ? null : source.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            DespawnLolly(playfield);
        }

        public static void DespawnLolly(Playfield playfield)
        {
            if (playfield == null)
            {
                return;
            }

            LollyDespawnedPlayfields.Add(playfield.Identity.Instance);
            ICharacter lolly = FindLolly(playfield);
            if (lolly == null)
            {
                return;
            }

            playfield.DespawnNpcImmediately(lolly);
        }

        private static Character SpawnDesertReetSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = DesertReetSlots[slotIndex];
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character mob = SpawnMobWithTemplateFallback(
                playfieldIdentity,
                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },
                controller,
                slot.Level,
                CombatTestMobArchetype.TemplateHash,
                CombatTestMobArchetype.TemplateHash,
                slot.Name);
            if (mob == null)
            {
                return null;
            }

            mob.Name = slot.Name;
            mob.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.IslandReet);
            mob.Name = slot.Name;
            ApplyReetStats(mob, slot);
            controller.AiProfile = NpcAiProfile.Passive;

            int minDamage = 6;
            int maxDamage = slot.Level >= 7 ? 10 : 8;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "lorelei-oasis-20260721-loralei",
                minDamage,
                maxDamage,
                2.0,
                0,
                0,
                1279612721,
                0,
                0,
                0,
                0,
                0,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            // Capture: not attack-on-sight; retaliate + nearby assist only.
            controller.AiProfile = NpcAiProfile.Passive;
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            RegisterOasisReet(mob.Identity.Instance);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static Character SpawnRollerratSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = RollerratSlots[slotIndex];
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Aggressive };
            Character mob = SpawnMobWithTemplateFallback(
                playfieldIdentity,
                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },
                controller,
                slot.Level,
                CombatTestMobArchetype.TemplateHash,
                CombatTestMobArchetype.TemplateHash,
                slot.Name);
            if (mob == null)
            {
                return null;
            }

            mob.Name = slot.Name;
            mob.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.StowawayRollerrat);
            mob.Name = slot.Name;
            ApplyRollerratStats(mob, slot);
            controller.AiProfile = NpcAiProfile.Aggressive;

            int minDamage = 5;
            int maxDamage = slot.Level >= 7 ? 9 : 7;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "lorelei-rollerrat-20260722-233205",
                minDamage,
                maxDamage,
                2.0,
                0,
                0,
                1279612721,
                0,
                0,
                0,
                0,
                0,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            controller.AiProfile = NpcAiProfile.Aggressive;
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            RegisterOasisRollerrat(mob.Identity.Instance);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static Character SpawnLolly(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (HasLivingLolly(playfield))
            {
                return null;
            }

            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character mob = SpawnMobWithTemplateFallback(
                playfieldIdentity,
                new Coordinate { x = LollySpawnX, y = LollySpawnY, z = LollySpawnZ },
                controller,
                10,
                CombatTestMobArchetype.TemplateHash,
                CombatTestMobArchetype.TemplateHash,
                "Lolly the Reet");
            if (mob == null)
            {
                return null;
            }

            mob.Name = "Lolly the Reet";
            mob.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.IslandReet);
            mob.Name = "Lolly the Reet";
            ApplyReetStats(
                mob,
                new MobSlot("Lolly the Reet", 10, 114, 95, LollySpawnX, LollySpawnY, LollySpawnZ),
                isLolly: true);
            controller.AiProfile = NpcAiProfile.Passive;

            controller.SetCapturedPatrolReplaySegments(
                new[]
                {
                    new NpcPatrolReplaySegment(
                        0.0,
                        LollySpawnX,
                        LollySpawnY,
                        LollySpawnZ,
                        3359.94f,
                        3.57f,
                        620.67f),
                    new NpcPatrolReplaySegment(
                        2.0,
                        3359.94f,
                        3.57f,
                        620.67f,
                        3358.18f,
                        3.61f,
                        640.52f),
                    new NpcPatrolReplaySegment(
                        0.0,
                        3358.18f,
                        3.61f,
                        640.52f,
                        LollySpawnX,
                        LollySpawnY,
                        LollySpawnZ),
                },
                false,
                true,
                true);
            controller.State = CharacterState.Patrolling;
            mob.Coordinates(new Coordinate { x = LollySpawnX, y = LollySpawnY, z = LollySpawnZ });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static Character SpawnMobWithTemplateFallback(
            Identity playfieldIdentity,
            Coordinate coord,
            NPCController controller,
            int level,
            string preferredHash,
            string fallbackHash,
            string debugName)
        {
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                preferredHash,
                playfieldIdentity,
                coord,
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                level);
            if (mob != null)
            {
                return mob;
            }

            // A001/A012 may be absent from some local DBs; A004 is proven on Arete (Alex/Junkyard).
            if (!string.Equals(preferredHash, fallbackHash, StringComparison.OrdinalIgnoreCase))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LoreleiOasisMobRuntime template=" + preferredHash
                    + " missing for " + debugName + "; fallback=" + fallbackHash);
                mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    fallbackHash,
                    playfieldIdentity,
                    coord,
                    new Quaternion(0.0, 0.0, 0.0, 1.0),
                    controller,
                    level);
            }

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LoreleiOasisMobRuntime spawn FAILED name=" + debugName
                    + " templates=" + preferredHash + "/" + fallbackHash);
            }

            return mob;
        }

        private static void ApplyReetStats(Character mob, MobSlot slot, bool isLolly = false)
        {
            SetStat(mob, StatIds.monsterdata, ReetMonsterData);
            SetStat(mob, StatIds.life, slot.Health);
            SetStat(mob, StatIds.health, slot.Health);
            SetStat(mob, StatIds.level, slot.Level);
            SetStat(mob, StatIds.npcfamily, CombatTestMobArchetype.IslandReet.NpcFamily);
            SetStat(mob, StatIds.monsterscale, slot.Scale);
            // Capture 20260721-loralei SCFU RunSpeedBase ~17-22.
            SetStat(mob, StatIds.runspeed, isLolly ? 17 : (slot.Level >= 6 ? 22 : 17));
            // Capture SCFU CharacterFlags: Desert/Rollerrat 268964353; Lolly/Greedy 277352961.
            bool lollyOrGreedy = isLolly
                || string.Equals(slot.Name, "Greedy Desert Reet", StringComparison.OrdinalIgnoreCase);
            SetStat(mob, StatIds.flags, lollyOrGreedy ? 277352961 : 268964353);
            // Capture VisualFlags=31 (not 0x5f — that hid reets).
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            SetStat(mob, StatIds.headmesh, 0);
            SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.IslandReet.CorpseCatMesh);
            SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.IslandReet.CorpseCatMesh);
            SetStat(mob, StatIds.xp, slot.Level >= 7 ? 400 : 316);

            if (mob.Textures != null)
            {
                mob.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    mob.Textures.Add(new AORebirth.Core.Textures.AOTextures(i, 0));
                }
            }

            if (mob.MeshLayer != null)
            {
                mob.MeshLayer.Clear();
            }

            if (mob.SocialMeshLayer != null)
            {
                mob.SocialMeshLayer.Clear();
            }
        }

        private static void ApplyRollerratStats(Character mob, MobSlot slot)
        {
            SetStat(mob, StatIds.monsterdata, RollerratMonsterData);
            SetStat(mob, StatIds.life, slot.Health);
            SetStat(mob, StatIds.health, slot.Health);
            SetStat(mob, StatIds.level, slot.Level);
            SetStat(mob, StatIds.npcfamily, CombatTestMobArchetype.StowawayRollerrat.NpcFamily);
            SetStat(mob, StatIds.monsterscale, slot.Scale);
            SetStat(mob, StatIds.runspeed, 17);
            SetStat(mob, StatIds.flags, 268964353);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            SetStat(mob, StatIds.headmesh, 0);
            SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
            SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
            SetStat(mob, StatIds.xp, slot.Level >= 7 ? 400 : 316);

            if (mob.Textures != null)
            {
                mob.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    mob.Textures.Add(new AORebirth.Core.Textures.AOTextures(i, 0));
                }
            }

            if (mob.MeshLayer != null)
            {
                mob.MeshLayer.Clear();
            }

            if (mob.SocialMeshLayer != null)
            {
                mob.SocialMeshLayer.Clear();
            }
        }

        private static ICharacter FindLolly(Playfield playfield)
        {
            ICharacter byInstance = playfield.FindByIdentity<ICharacter>(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = LollyFixedInstance
                });
            if (byInstance != null && byInstance.Stats[StatIds.health].Value > 0)
            {
                return byInstance;
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                if (string.Equals(candidate.Name, "Lolly the Reet", StringComparison.OrdinalIgnoreCase)
                    || (candidate.Name != null
                        && candidate.Name.IndexOf("Lolly", StringComparison.OrdinalIgnoreCase) >= 0
                        && candidate.Name.IndexOf("Reet", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    float dx = candidate.Coordinates().x - LollySpawnX;
                    float dz = candidate.Coordinates().z - LollySpawnZ;
                    if ((dx * dx) + (dz * dz) <= 400f)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static bool HasLivingLolly(Playfield playfield)
        {
            ICharacter lolly = FindLolly(playfield);
            return lolly != null && lolly.Stats[StatIds.health].Value > 0;
        }

        private static void SetStat(ICharacter mob, StatIds stat, int value)
        {
            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }

        private static void RegisterOasisReet(int npcInstance)
        {
            if (npcInstance == 0)
            {
                return;
            }

            lock (OasisGate)
            {
                OasisReetInstances.Add(npcInstance);
            }
        }

        private static void RegisterOasisRollerrat(int npcInstance)
        {
            if (npcInstance == 0)
            {
                return;
            }

            lock (OasisGate)
            {
                OasisRollerratInstances.Add(npcInstance);
            }
        }

        private static bool HasLivingMobNear(Playfield playfield, MobSlot slot)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, slot.Name, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - slot.X;
                float dz = candidate.Coordinates().z - slot.Z;
                if ((dx * dx) + (dz * dz) <= 6.25f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
