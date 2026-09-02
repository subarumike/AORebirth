namespace ZoneEngine.Core.Thrak.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260718-185306: after favored-analyzer trade, Dreaming Silvertail despawns and a new
    /// Cursed Silvertail (lvl 8, HP 720, monsterData 208922) spawns at the same position and attacks.
    /// Soul progress is applied on trade finish (not on death).
    /// </summary>
    internal static class ThrakGardenKeySilvertailTransform
    {
        private const string TemplateHash = "BART";
        private const int CursedLevel = 8;
        private const int CursedHealth = 720;
        private const int CursedMonsterData = 208922;
        private const int CursedScale = 141;
        private const int CursedVisualFlags = 31;

        internal static bool TryCurseAndAggro(ICharacter source, Identity silvertailIdentity)
        {
            if (source == null || source.Playfield == null || silvertailIdentity == Identity.None)
            {
                return false;
            }

            var playfield = source.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            ICharacter dreaming = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, silvertailIdentity);
            if (dreaming == null
                || !string.Equals(
                        dreaming.Name,
                        ThrakGardenKeyInteractionRules.DreamingSilvertailName,
                        StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Coordinate position = dreaming.CalculatePredictedPosition();
            Quaternion heading = dreaming.Rotation ?? new Quaternion(0f, 0f, 0f, 1f);

            // Capture order: spawn Cursed first, then Despawn Dreaming.
            Character cursed = SpawnCursed(playfield, source.Playfield.Identity, position, heading);
            if (cursed == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ThrakGardenKeySilvertailTransform cursed spawn failed at dreaming="
                    + silvertailIdentity.ToString(true));
                return false;
            }

            try
            {
                playfield.DespawnNpcImmediately(dreaming);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ThrakGardenKeySilvertailTransform dreaming despawn failed: " + ex.Message);
            }

            try
            {
                cursed.SetFightingTarget(source.Identity);
                source.SetFightingTarget(cursed.Identity);
                playfield.AcquireNpcAggro(source, cursed);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ThrakGardenKeySilvertailTransform aggro failed: " + ex.Message);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ThrakGardenKeySilvertailTransform cursed npc=" + cursed.Identity.ToString(true)
                + " from dreaming=" + silvertailIdentity.ToString(true)
                + " by=" + source.Identity.ToString(true));
            return true;
        }

        /// <summary>
        /// Capture advances souls on trade; death is combat only (do not double-count).
        /// </summary>
        internal static void TryObserveCursedDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            if (!string.Equals(
                    target.Name,
                    ThrakGardenKeyInteractionRules.CursedSilvertailName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ThrakGardenKey cursed silvertail killed by=" + attacker.Identity.ToString(true)
                + " (soul count already advanced on trade)");
        }

        private static Character SpawnCursed(
            Playfield playfield,
            Identity playfieldIdentity,
            Coordinate position,
            Quaternion heading)
        {
            if (playfield == null || position == null)
            {
                return null;
            }

            var npcController = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                position,
                heading ?? new Quaternion(0f, 0f, 0f, 1f),
                npcController,
                CursedLevel);

            if (mob == null)
            {
                return null;
            }

            mob.Name = ThrakGardenKeyInteractionRules.CursedSilvertailName;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)CursedMonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)CursedHealth);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)CursedHealth);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)CursedLevel);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)CursedVisualFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)CursedScale);
            mob.Position = (position).coordinate;
            string combatFailure;
            CapturedEnemyCombatRuntime.Prepare(
                mob,
                npcController,
                CapturedEnemyCombatContract.Unresolved(
                    "20260718-185306 Cursed Silvertail has no source-local WIFU/attack-start/AttackInfo contract mapped",
                    true),
                out combatFailure);
            mob.DoNotDoTimers = false;

            playfield.ActivateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }
    }
}
