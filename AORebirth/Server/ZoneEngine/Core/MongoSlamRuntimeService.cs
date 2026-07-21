namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    using ZoneEngine.Core.Functions.GameFunctions;

    /// <summary>
    /// Capture-backed Mongo Slam runtime (20260719-Rex-Markus-stone / nanos.dat).
    /// Uploaded program 287046 (strain 51, Attr8 duration, Modify skill buffs, radius 20).
    /// Slam effect 100198: Hit +12 HP + AreaCastNano(100194, 20) TauntNpc.
    /// AoE is taunt/aggro steal with 1 HP engage damage — not a nuke; skips unflagged players.
    /// </summary>
    internal static class MongoSlamRuntimeService
    {
        internal const int UploadedMongoSlamNanoId = 287046;

        internal const int MongoSlamEffectNanoId = 100198;

        internal const int MongoSlamNestedTauntNanoId = 100194;

        internal const int MongoSlamStrain = 51;

        internal const float SlamRadiusMeters = 20f;

        internal const int SelfHealAmount = 12;

        // HoT while 287046 program is active: +12 HP every 10s (matches 100198 Hit amount).
        private static readonly TimeSpan HotTickInterval = TimeSpan.FromSeconds(10);

        private static readonly ConcurrentDictionary<int, DateTime> NextHotTickUtc =
            new ConcurrentDictionary<int, DateTime>();

        internal static bool IsMongoSlamNano(int nanoId)
        {
            return nanoId == UploadedMongoSlamNanoId || nanoId == MongoSlamEffectNanoId;
        }

        /// <summary>
        /// After OnUse for uploaded 287046: apply slam effect (heal + 20m taunt AoE) and start HoT.
        /// </summary>
        internal static void ApplyCaptureBackedSlamEffects(Character caster, int castNanoId)
        {
            if (caster == null)
            {
                return;
            }

            if (castNanoId == UploadedMongoSlamNanoId)
            {
                ApplySlamEffectNano(caster);
                BeginHotWhileProgramActive(caster);
                return;
            }

            if (castNanoId == MongoSlamEffectNanoId)
            {
                // Direct cast of effect nano already ran OnUse; ensure HoT if buff program is up.
                BeginHotWhileProgramActive(caster);
            }
        }

        /// <summary>
        /// Heartbeat hook: heal caster while Mongo Slam program (strain 51) remains active.
        /// </summary>
        internal static bool ProcessHotTick(ICharacter character)
        {
            Character caster = character as Character;
            if (caster == null || caster.Controller == null || !(caster.Controller is Controllers.PlayerController))
            {
                return false;
            }

            if (!IsMongoSlamProgramActive(caster))
            {
                DateTime removed;
                NextHotTickUtc.TryRemove(caster.Identity.Instance, out removed);
                return false;
            }

            DateTime nextTick;
            DateTime now = DateTime.UtcNow;
            if (!NextHotTickUtc.TryGetValue(caster.Identity.Instance, out nextTick))
            {
                NextHotTickUtc[caster.Identity.Instance] = now + HotTickInterval;
                return false;
            }

            if (now < nextTick)
            {
                return false;
            }

            NextHotTickUtc[caster.Identity.Instance] = now + HotTickInterval;
            ApplySelfHeal(caster, SelfHealAmount);
            return true;
        }

        private static void ApplySlamEffectNano(Character caster)
        {
            NanoFormula effectNano;
            if (NanoLoader.NanoList.TryGetValue(MongoSlamEffectNanoId, out effectNano)
                && effectNano != null
                && effectNano.Events != null
                && effectNano.Events.Count > 0)
            {
                NanoEventRuntimeService.Default.ExecuteOnUseEvents(caster, effectNano);
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "MongoSlam applied dat effect nano=" + MongoSlamEffectNanoId);
                return;
            }

            ApplyFallbackHealAndTauntAoe(caster);
        }

        internal static void BeginHotWhileProgramActive(Character caster)
        {
            if (caster == null)
            {
                return;
            }

            NextHotTickUtc[caster.Identity.Instance] = DateTime.UtcNow + HotTickInterval;
        }

        private static bool IsMongoSlamProgramActive(ICharacter caster)
        {
            if (caster == null || caster.ActiveNanos == null)
            {
                return false;
            }

            IActiveNano state;
            if (caster.ActiveNanos.TryGetValue(MongoSlamStrain, out state) && state != null)
            {
                return state.ID == UploadedMongoSlamNanoId;
            }

            return ActiveNanoRuntimeService.Default.HasActiveNanoInStrain(
                caster,
                UploadedMongoSlamNanoId,
                MongoSlamStrain);
        }

        private static void ApplyFallbackHealAndTauntAoe(Character caster)
        {
            ApplySelfHeal(caster, SelfHealAmount);

            Playfield playfield = caster.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            IList<ICharacter> inRange = playfield.FindCharacterInRange(caster, SlamRadiusMeters);
            int hits = 0;
            foreach (ICharacter nearby in inRange)
            {
                Character other = nearby as Character;
                if (other == null || object.ReferenceEquals(other, caster))
                {
                    continue;
                }

                bool isNpc = other.Stats[StatIds.npcfamily].BaseValue != 0
                               || other.Stats[StatIds.monsterdata].BaseValue != 0
                               || other.Controller is Controllers.NPCController;
                if (!isNpc)
                {
                    if (!PlayerVersusPlayerCombatRules.IsProtectedPlayerVersusPlayerTarget(other)
                        || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(caster, other))
                    {
                        continue;
                    }
                }

                var tauntArgs = new MessagePackObject[] { 4000 };
                if (new tauntnpc().Execute(caster, caster, other, tauntArgs))
                {
                    hits++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                "MongoSlam fallback heal+tauntAoE caster=" + caster.Identity + " hits=" + hits);
        }

        private static void ApplySelfHeal(Character caster, int amount)
        {
            if (caster == null || amount <= 0)
            {
                return;
            }

            var healArgs = new MessagePackObject[] { 27, amount, amount, 0 };
            new hit().Execute(caster, caster, caster, healArgs);
        }
    }
}
