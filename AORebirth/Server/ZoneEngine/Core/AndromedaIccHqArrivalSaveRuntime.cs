namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Concurrent;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using Utility;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture 20260721-finish: after leaving Arete via Vaughn, arrival at ICC HQ Andromeda (PF 655)
    /// at (3337, 36.10, 866) becomes the character's bind/save point (cannot return to Arete).
    /// No credit fee / no SocialStatus chat flood — same bind pattern as garden pad save.
    /// </summary>
    public static class AndromedaIccHqArrivalSaveRuntime
    {
        private const int AndromedaPlayfieldId = 655;

        // Capture 20260721-finish DYNEL-SPAWNED / CHAR-IN-PLAY after PLAYFIELD-INIT 655.
        private const float BindX = 3337f;

        private const float BindY = 36.1005f;

        private const float BindZ = 866f;

        private static readonly ConcurrentDictionary<int, bool> BoundCharacterIds =
            new ConcurrentDictionary<int, bool>();

        public static bool IsAndromedaPlayfield(int playfieldId)
        {
            return playfieldId == AndromedaPlayfieldId;
        }

        public static void GetBindSpot(out float x, out float y, out float z)
        {
            x = BindX;
            y = BindY;
            z = BindZ;
        }

        /// <summary>
        /// Bind immediately on Exit Arete Landing teleport (before CharDCMove).
        /// Ensures terminate/death cannot return to Arete.
        /// </summary>
        public static void ForceBindAtIccHq(ICharacter character, string reason)
        {
            try
            {
                if (character == null)
                {
                    return;
                }

                SaveRespawnPoint(character, AndromedaPlayfieldId);
                BoundCharacterIds[character.Identity.Instance] = true;
                ClearStuckAreteTips(character);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AndromedaIccHqArrivalSave FORCE-bound char="
                    + character.Identity
                    + " pf="
                    + AndromedaPlayfieldId
                    + " pos=("
                    + BindX
                    + ","
                    + BindY
                    + ","
                    + BindZ
                    + ") reason="
                    + (reason ?? string.Empty)
                    + " source=20260721-finish");
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AndromedaIccHqArrivalSave FORCE FAILED: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>Call from CharDCMove when the character is in PF 655.</summary>
        public static void TryApplyOnArrival(ICharacter character, string reason)
        {
            try
            {
                if (character == null || character.Playfield == null)
                {
                    return;
                }

                int playfieldId = character.Playfield.Identity.Instance;
                if (!IsAndromedaPlayfield(playfieldId))
                {
                    return;
                }

                int characterId = character.Identity.Instance;

                // Already bound to Andromeda with valid coords — still clear leftover tips once.
                if (character.Stats[StatIds.tempsaveplayfield].Value == AndromedaPlayfieldId
                    && character.Stats[StatIds.tempsavex].Value > 0
                    && character.Stats[StatIds.tempsavey].Value > 0)
                {
                    if (BoundCharacterIds.TryAdd(characterId, true))
                    {
                        ClearStuckAreteTips(character);
                    }

                    return;
                }

                SaveRespawnPoint(character, playfieldId);
                BoundCharacterIds[characterId] = true;
                ClearStuckAreteTips(character);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AndromedaIccHqArrivalSave bound char="
                    + character.Identity
                    + " pf="
                    + playfieldId
                    + " pos=("
                    + BindX
                    + ","
                    + BindY
                    + ","
                    + BindZ
                    + ") reason="
                    + (reason ?? string.Empty)
                    + " source=20260721-finish");
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AndromedaIccHqArrivalSave FAILED: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Stuck Remain 00:00 tips in Missions window after Arete leave
        /// (Talk to Sarah Greene / Buy some Nano Programs).
        /// </summary>
        public static void ClearStuckAreteTips(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            // Wire delete (Int16 Action59 + Quest/Delete) — typed Action59 leaves Remain 00:00.
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, unchecked((int)0x555BE9F3));
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, unchecked((int)0x555BE9F4));
            // Live finish capture Buy Nano instance (if client still holds it).
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, unchecked((int)0x555CF539));

            if (!MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            CompleteIfPresent(characterId, "Mission:555BE9F3");
            CompleteIfPresent(characterId, "Mission:555BE9F4");
        }

        private static void CompleteIfPresent(int characterId, string questId)
        {
            try
            {
                ZoneEngine.Core.Missions.MissionStateRecord mission =
                    MissionRuntime.Service.GetMission(characterId, questId);
                if (mission == null || mission.State == MissionLifecycleState.Completed)
                {
                    return;
                }

                if (mission.State == MissionLifecycleState.Offered)
                {
                    MissionRuntime.Service.AcceptMission(characterId, questId);
                }

                MissionRuntime.Service.CompleteMission(characterId, questId);
            }
            catch
            {
            }
        }

        private static void SaveRespawnPoint(ICharacter character, int playfieldId)
        {
            // ResolvePlayerRespawnLocation reads TempSaveX as X and TempSaveY as world Z (same as savechar).
            int saveX = (int)Math.Round(BindX);
            int saveZ = (int)Math.Round(BindZ);

            character.Stats[StatIds.tempsaveplayfield].Set((uint)Math.Max(0, playfieldId));
            character.Stats[StatIds.tempsavex].Set((uint)Math.Max(0, saveX));
            character.Stats[StatIds.tempsavey].Set((uint)Math.Max(0, saveZ));
            character.Stats[StatIds.insurancepercentage].Set(100);
            character.Stats[StatIds.insurancetime].Set((uint)Math.Max(0, Environment.TickCount));
            character.Stats.Write();
        }
    }
}
