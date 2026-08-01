namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;

    #endregion

    /// <summary>
    /// Arete Landing Gas Fire (template 295883) extinguish + respawn.
    /// Capture 20260731-macrus-stone-dialog-quests: UseItemOnItem → feedback → Despawn,
    /// then Gas Fire SIFU returns at the same pad (~30s live; Mike asked 20s).
    /// Positions: capture 20260731-162309 + Marcus pad extinguish/respawn +
    /// two LookAt-tagged pads from 20260731-163559 (57A9CDCB / 57A9CDCC).
    /// </summary>
    internal static class AreteGasFireRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int GasFireTemplateId = 295883;

        private const int GasFireFlags = unchecked((int)0x800A2221);

        // Mike: respawn after 20 seconds (live capture gap was ~30s despawn→respawn).
        private const double RespawnSeconds = 20.0;

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<int, Dictionary<int, PendingRespawn>> PendingByPlayfield =
            new Dictionary<int, Dictionary<int, PendingRespawn>>();

        private sealed class GasFireSlot
        {
            public int Instance;
            public float X;
            public float Y;
            public float Z;
            public float Hy;
            public float Hw;
            public string Evidence;
        }

        private sealed class PendingRespawn
        {
            public GasFireSlot Slot;
            public DateTime DueUtc;
        }

        // Stable private-server instances; coords from live captures (instances rotate on live).
        private static readonly GasFireSlot[] Slots =
            {
                // Capture 20260731-162309 Terminal:57A424A1
                new GasFireSlot
                {
                    Instance = unchecked((int)0x57A424A1),
                    X = 3584.342f,
                    Y = 41.18011f,
                    Z = 818.8473f,
                    Hy = 0.001027973f,
                    Hw = -0.9999995f,
                    Evidence = "20260731-162309 Terminal:57A424A1"
                },
                // Capture 20260731-162309 Terminal:57A9CD12 (was 57961EAA)
                new GasFireSlot
                {
                    Instance = unchecked((int)0x57961EAA),
                    X = 3599.269f,
                    Y = 42.75448f,
                    Z = 843.9785f,
                    Hy = 0.002724483f,
                    Hw = 0.9999963f,
                    Evidence = "20260731-162309 Terminal:57A9CD12"
                },
                // Capture 20260720-061810 pad east
                new GasFireSlot
                {
                    Instance = unchecked((int)0x579BA8B4),
                    X = 3636.222f,
                    Y = 43.58711f,
                    Z = 845.9906f,
                    Hy = 0.003037463f,
                    Hw = 0.9999954f,
                    Evidence = "20260720-061810 Terminal:579BA8B4"
                },
                new GasFireSlot
                {
                    Instance = unchecked((int)0x57961EAB),
                    X = 3602.093f,
                    Y = 42.86554f,
                    Z = 842.4749f,
                    Hy = 0.002859163f,
                    Hw = 0.9999959f,
                    Evidence = "20260719-Rex-Markus-stone Terminal:57961EAB"
                },
                new GasFireSlot
                {
                    Instance = unchecked((int)0x57967DD1),
                    X = 3607.675f,
                    Y = 42.24637f,
                    Z = 840.8735f,
                    Hy = 0.003388073f,
                    Hw = -0.9999943f,
                    Evidence = "20260719-Rex-Markus-stone Terminal:57967DD1"
                },
                // Capture 20260731-macrus extinguish respawn Terminal:57A9CE5F (same pad as 579ADB41)
                new GasFireSlot
                {
                    Instance = unchecked((int)0x579ADB41),
                    X = 3629.714f,
                    Y = 42.90778f,
                    Z = 832.0806f,
                    Hy = 0.002421766f,
                    Hw = -0.9999971f,
                    Evidence = "20260731-macrus-stone-dialog-quests Terminal:57A9CE5F"
                },
                // Capture 20260731-163559 LookAt Terminal:57A9CDCB (no SIFU; coords from 162309
                // player stand + 1.5m heading, CE5F LookAt error ~1.3m).
                new GasFireSlot
                {
                    Instance = unchecked((int)0x579ADB42),
                    X = 3633.922f,
                    Y = 42.90778f,
                    Z = 837.486f,
                    Hy = 0.002421766f,
                    Hw = -0.9999971f,
                    Evidence = "20260731-163559 LookAt Terminal:57A9CDCB (pos from 162309 walk)"
                },
                // Capture 20260731-163559 LookAt Terminal:57A9CDCC
                new GasFireSlot
                {
                    Instance = unchecked((int)0x579ADB43),
                    X = 3640.333f,
                    Y = 42.90778f,
                    Z = 833.333f,
                    Hy = 0.002421766f,
                    Hw = -0.9999971f,
                    Evidence = "20260731-163559 LookAt Terminal:57A9CDCC (pos from 162309 walk)"
                }
            };

        /// <summary>
        /// Prop injector reads the same capture-backed slots (single source of truth).
        /// </summary>
        internal static IEnumerable<PlayfieldStaticDynelDefinition> EnumerateDefinitions()
        {
            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(GasFireTemplateId, out template) || template == null)
            {
                yield break;
            }

            foreach (GasFireSlot slot in Slots)
            {
                yield return BuildDefinition(slot, template);
            }
        }

        public static bool TryExtinguish(Playfield playfield, StaticDynel fire)
        {
            if (playfield == null
                || fire == null
                || playfield.Identity.Instance != AreteLandingPlayfieldId)
            {
                return false;
            }

            GasFireSlot slot = FindSlot(fire);
            if (slot == null)
            {
                // Unknown instance (rotated live id) — still despawn and respawn at fire's current coord.
                slot = new GasFireSlot
                       {
                           Instance = fire.Identity.Instance,
                           X = (float)fire.Coordinate.x,
                           Y = (float)fire.Coordinate.y,
                           Z = (float)fire.Coordinate.z,
                           Hy = fire.Heading != null ? fire.Heading.Y : 0f,
                           Hw = fire.Heading != null ? fire.Heading.W : 1f,
                           Evidence = "runtime-extinguish-fallback"
                       };
            }

            Identity fireIdentity = fire.Identity;
            try
            {
                playfield.Despawn(fireIdentity);
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteGasFireRuntime despawn announce failed: " + e.Message);
            }

            try
            {
                playfield.UnregisterDynel(fireIdentity);
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteGasFireRuntime unregister failed: " + e.Message);
            }

            try
            {
                Pool.Instance.RemoveObject(fire);
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteGasFireRuntime pool remove failed: " + e.Message);
            }

            ScheduleRespawn(playfield.Identity.Instance, slot);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteGasFireRuntime extinguished instance="
                + unchecked((uint)slot.Instance).ToString("X8")
                + " respawnSeconds="
                + RespawnSeconds
                + " evidence="
                + slot.Evidence);
            return true;
        }

        public static void TickRespawn(Playfield playfield)
        {
            if (playfield == null || playfield.Identity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            int playfieldId = playfield.Identity.Instance;
            List<PendingRespawn> due = null;
            lock (SyncRoot)
            {
                Dictionary<int, PendingRespawn> pending;
                if (!PendingByPlayfield.TryGetValue(playfieldId, out pending) || pending.Count == 0)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                List<int> removeKeys = null;
                foreach (KeyValuePair<int, PendingRespawn> pair in pending)
                {
                    if (pair.Value == null || pair.Value.DueUtc > now)
                    {
                        continue;
                    }

                    if (due == null)
                    {
                        due = new List<PendingRespawn>();
                        removeKeys = new List<int>();
                    }

                    due.Add(pair.Value);
                    removeKeys.Add(pair.Key);
                }

                if (removeKeys != null)
                {
                    for (int i = 0; i < removeKeys.Count; i++)
                    {
                        pending.Remove(removeKeys[i]);
                    }
                }
            }

            if (due == null)
            {
                return;
            }

            for (int i = 0; i < due.Count; i++)
            {
                TrySpawnSlot(playfield, due[i].Slot, true);
            }
        }

        public static void ClearPlayfield(int playfieldId)
        {
            lock (SyncRoot)
            {
                PendingByPlayfield.Remove(playfieldId);
            }
        }

        private static void ScheduleRespawn(int playfieldId, GasFireSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                Dictionary<int, PendingRespawn> pending;
                if (!PendingByPlayfield.TryGetValue(playfieldId, out pending))
                {
                    pending = new Dictionary<int, PendingRespawn>();
                    PendingByPlayfield[playfieldId] = pending;
                }

                pending[slot.Instance] = new PendingRespawn
                                         {
                                             Slot = slot,
                                             DueUtc = DateTime.UtcNow.AddSeconds(RespawnSeconds)
                                         };
            }
        }

        private static GasFireSlot FindSlot(StaticDynel fire)
        {
            if (fire == null)
            {
                return null;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Instance == fire.Identity.Instance)
                {
                    return Slots[i];
                }
            }

            // Match by near position (live instance ids rotate).
            double fx = fire.Coordinate.x;
            double fy = fire.Coordinate.y;
            double fz = fire.Coordinate.z;
            GasFireSlot best = null;
            double bestDist = double.MaxValue;
            for (int i = 0; i < Slots.Length; i++)
            {
                GasFireSlot slot = Slots[i];
                double dx = slot.X - fx;
                double dy = slot.Y - fy;
                double dz = slot.Z - fz;
                double dist = (dx * dx) + (dy * dy) + (dz * dz);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = slot;
                }
            }

            // Within ~4m
            return bestDist <= 16.0 ? best : null;
        }

        private static bool TrySpawnSlot(Playfield playfield, GasFireSlot slot, bool announce)
        {
            if (playfield == null || slot == null)
            {
                return false;
            }

            Identity identity = new Identity
                                {
                                    Type = IdentityType.Terminal,
                                    Instance = slot.Instance
                                };

            StaticDynel existing = playfield.FindByIdentity<StaticDynel>(identity);
            if (existing != null)
            {
                return true;
            }

            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(GasFireTemplateId, out template) || template == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteGasFireRuntime missing template=" + GasFireTemplateId);
                return false;
            }

            StaticDynel fire;
            try
            {
                PlayfieldStaticDynelDefinition definition = BuildDefinition(slot, template);
                fire = new StaticDynel(playfield.Identity, identity, template);
                foreach (GameTuple<CharacterStat, uint> stat in definition.Stats)
                {
                    int key = (int)stat.Value1;
                    int value = (int)stat.Value2;
                    if (fire.Stats.ContainsKey(key))
                    {
                        fire.Stats[key] = value;
                    }
                    else
                    {
                        fire.Stats.Add(key, value);
                    }
                }

                fire.Coordinate = definition.Coordinate;
                fire.Heading = definition.Heading;
                playfield.RegisterDynel(fire);
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteGasFireRuntime spawn failed instance="
                    + unchecked((uint)slot.Instance).ToString("X8")
                    + " err="
                    + e.Message);
                return false;
            }

            if (announce)
            {
                AnnounceFire(playfield, fire);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteGasFireRuntime respawned instance="
                + unchecked((uint)slot.Instance).ToString("X8")
                + " evidence="
                + slot.Evidence);
            return true;
        }

        private static void AnnounceFire(Playfield playfield, StaticDynel fire)
        {
            if (playfield == null || fire == null)
            {
                return;
            }

            foreach (ICharacter character in playfield.EnumerateActiveCharacters())
            {
                if (character == null || !(character.Controller is PlayerController))
                {
                    continue;
                }

                try
                {
                    SimpleItemFullUpdateMessageHandler.Default.Send(character, fire);
                }
                catch (Exception e)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteGasFireRuntime SIFU send failed: " + e.Message);
                }
            }
        }

        private static PlayfieldStaticDynelDefinition BuildDefinition(GasFireSlot slot, ItemTemplate template)
        {
            var stats = new List<GameTuple<CharacterStat, uint>>
                        {
                            Stat(CharacterStat.Flags, unchecked((uint)GasFireFlags)),
                            Stat(CharacterStat.StaticInstance, (uint)GasFireTemplateId),
                            Stat(CharacterStat.ACGItemLevel, 1),
                            Stat(CharacterStat.ACGItemTemplateID, (uint)GasFireTemplateId),
                            Stat(CharacterStat.ACGItemTemplateID2, (uint)GasFireTemplateId),
                            Stat(CharacterStat.MultipleCount, 1),
                            Stat(CharacterStat.AnimPlay, 0),
                            Stat(CharacterStat.AnimPos, 0)
                        };

            return new PlayfieldStaticDynelDefinition(
                new Identity { Type = IdentityType.Terminal, Instance = slot.Instance },
                template,
                stats,
                new Coordinate(slot.X, slot.Y, slot.Z),
                new Quaternion { X = 0f, Y = slot.Hy, Z = 0f, W = slot.Hw });
        }

        private static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = id, Value2 = value };
        }
    }
}
