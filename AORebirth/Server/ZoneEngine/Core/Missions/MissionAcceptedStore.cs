namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Remembers every mission a character has accepted (AO allows multiple concurrent missions). Kept in
    /// memory for the process lifetime and mirrored to a sidecar file so zone/relog restores the full set
    /// for up to 48h each.
    /// </summary>
    internal static class MissionAcceptedStore
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, List<AcceptedMission>> ByCharacter =
            new Dictionary<int, List<AcceptedMission>>();

        internal sealed class AcceptedMission
        {
            public Identity QuestIdentity;

            public Identity OriginalOfferIdentity;

            public int MissionIconId;

            public int Quality;

            public string ShortInfo;

            /// <summary>Kill/Find person name shown in journal and used for interior spawn.</summary>
            public string TargetName;

            /// <summary>Map-dot side for the objective person (Clan/Omni/Neutral/Monster).</summary>
            public int TargetSide;

            /// <summary>Cash from mission description (money/XP slider) — paid on finish.</summary>
            public int CashReward;

            /// <summary>XP from mission description — paid on finish (may be 0 when slider is cash-heavy).</summary>
            public int ExperienceReward;

            public DateTime ExpiryUtc;

            public QuestInfo Offer;

            public MissionAcgAcceptedProjection Projection;

            public bool HasFrozenAcceptedRewards;

            public int FrozenItemRewardLowId;

            public int FrozenItemRewardHighId;

            public int FrozenItemRewardQuality;

            public int FrozenItemRewardCount;

            /// <summary>Outdoor map marker from the rolled offer (not the Rome capture shell).</summary>
            public int MarkerPlayfield;

            public int EntranceLow;

            public int EntranceHigh;

            public float MarkerX;

            public float MarkerY;

            public float MarkerZ;
        }

        /// <summary>
        /// Adds or replaces one accepted mission (matched by quest identity). Does not wipe other missions.
        /// </summary>
        public static void Register(
            int characterInstance,
            QuestInfo offer,
            DateTime expiryUtc,
            string targetName = null,
            int targetSide = 0)
        {
            if (characterInstance == 0 || offer == null)
            {
                return;
            }

            int markerPf = 0;
            int entranceLow = 0;
            int entranceHigh = 0;
            float mx = 0;
            float my = 0;
            float mz = 0;
            if (offer.QuestActions != null && offer.QuestActions.Length > 0 && offer.QuestActions[0] != null)
            {
                QuestActionList action = offer.QuestActions[0];
                markerPf = action.Playfield.Instance;
                entranceLow = action.Unknown18;
                entranceHigh = action.Unknown19;
                mx = action.X;
                my = action.Y;
                mz = action.Z;
            }

            var entry = BuildEntry(
                offer.QuestIdentity,
                offer.QuestIdentity,
                offer,
                expiryUtc,
                targetName,
                targetSide,
                markerPf,
                entranceLow,
                entranceHigh,
                mx,
                my,
                mz);

            lock (Sync)
            {
                List<AcceptedMission> list = GetOrCreateList_NoLock(characterInstance);
                int existing = FindIndex_NoLock(list, entry.QuestIdentity);
                if (existing >= 0)
                {
                    list[existing] = entry;
                }
                else
                {
                    list.Add(entry);
                }

                PruneExpired_NoLock(list);
                TryWriteSidecar(characterInstance, list);
            }
        }

        /// <summary>
        /// Atomically persists a generated mission under its distinct accepted identity.
        /// The rolled offer identity remains available for correlation and is never reused as the
        /// journal identity.
        /// </summary>
        public static bool TryRegisterGenerated(
            int characterInstance,
            Identity acceptedQuestIdentity,
            QuestInfo offer,
            DateTime expiryUtc,
            out string failure)
        {
            failure = string.Empty;
            if (characterInstance == 0
                || acceptedQuestIdentity == null
                || acceptedQuestIdentity.Instance == 0
                || offer == null
                || offer.QuestIdentity == null)
            {
                failure = "Generated accepted mission identity and offer are required.";
                return false;
            }

            QuestActionList action =
                offer.QuestActions != null
                && offer.QuestActions.Length > 0
                    ? offer.QuestActions[0]
                    : null;
            var entry = BuildEntry(
                acceptedQuestIdentity,
                offer.QuestIdentity,
                offer,
                expiryUtc,
                null,
                0,
                action == null ? 0 : action.Playfield.Instance,
                action == null ? 0 : action.Unknown18,
                action == null ? 0 : action.Unknown19,
                action == null ? 0 : action.X,
                action == null ? 0 : action.Y,
                action == null ? 0 : action.Z);

            lock (Sync)
            {
                List<AcceptedMission> list = GetOrCreateList_NoLock(characterInstance);
                if (FindIndex_NoLock(list, acceptedQuestIdentity) >= 0)
                {
                    failure = "Duplicate accepted mission identity.";
                    return false;
                }

                list.Add(entry);
                if (!TryWriteSidecarAtomic(characterInstance, list, out failure))
                {
                    list.Remove(entry);
                    return false;
                }

                return true;
            }
        }

        internal static bool TryRegisterGeneratedProjection(
            MissionAcgAcceptedProjection projection,
            out string failure)
        {
            failure = string.Empty;
            if (projection == null)
            {
                failure = "Accepted generated-mission projection is required.";
                return false;
            }

            AcceptedMission entry;
            try
            {
                entry = BuildProjectionEntry(projection);
            }
            catch (Exception ex)
            {
                failure = "Accepted projection could not reconstruct its exact QFU: " + ex.Message;
                return false;
            }

            int characterInstance = projection.Binding.OwnerIdentity.Instance;
            lock (Sync)
            {
                List<AcceptedMission> list = GetOrCreateList_NoLock(characterInstance);
                int existing = FindIndex_NoLock(list, entry.QuestIdentity);
                if (existing >= 0)
                {
                    AcceptedMission current = list[existing];
                    if (current.Projection == null
                        || current.OriginalOfferIdentity == null
                        || current.OriginalOfferIdentity.Instance
                            != projection.Binding.OriginalOfferIdentity.Instance)
                    {
                        failure = "Accepted quest identity is already owned by another mission.";
                        return false;
                    }

                    list[existing] = entry;
                }
                else
                {
                    list.Add(entry);
                }

                PruneExpired_NoLock(list);
                TryWriteSidecar(characterInstance, list);
                return true;
            }
        }

        /// <summary>
        /// Returns a snapshot of all non-expired accepted missions for the character.
        /// </summary>
        public static List<AcceptedMission> GetAll(int characterInstance)
        {
            lock (Sync)
            {
                List<AcceptedMission> list;
                if (!ByCharacter.TryGetValue(characterInstance, out list) || list == null || list.Count == 0)
                {
                    List<AcceptedMission> fromDisk;
                    if (TryReadSidecar(characterInstance, out fromDisk) && fromDisk.Count > 0)
                    {
                        ByCharacter[characterInstance] = fromDisk;
                        list = fromDisk;
                    }
                    else
                    {
                        list = new List<AcceptedMission>();
                        ByCharacter[characterInstance] = list;
                    }
                }

                PruneExpired_NoLock(list);
                MergeGeneratedProjections_NoLock(characterInstance, list);
                if (list.Count == 0)
                {
                    ByCharacter.Remove(characterInstance);
                    TryDeleteSidecar(characterInstance);
                    return new List<AcceptedMission>();
                }

                return new List<AcceptedMission>(list);
            }
        }

        public static bool TryGet(int characterInstance, out AcceptedMission entry)
        {
            List<AcceptedMission> all = GetAll(characterInstance);
            if (all.Count == 0)
            {
                entry = null;
                return false;
            }

            entry = all[all.Count - 1];
            return true;
        }

        /// <summary>
        /// Resolves the stored mission for a journal Delete (exact type+instance, else instance-only).
        /// </summary>
        public static bool TryResolve(int characterInstance, Identity questIdentity, out AcceptedMission entry)
        {
            entry = null;
            if (questIdentity == null || questIdentity.Instance == 0)
            {
                return false;
            }

            List<AcceptedMission> all = GetAll(characterInstance);
            int index = FindIndex_NoLock(all, questIdentity);
            if (index < 0 || index >= all.Count)
            {
                return false;
            }

            entry = all[index];
            return entry != null;
        }

        /// <summary>
        /// Removes one mission by quest identity (journal delete). Returns true if something was removed.
        /// </summary>
        public static bool Remove(int characterInstance, Identity questIdentity)
        {
            lock (Sync)
            {
                List<AcceptedMission> list;
                if (!ByCharacter.TryGetValue(characterInstance, out list) || list == null)
                {
                    List<AcceptedMission> fromDisk;
                    if (!TryReadSidecar(characterInstance, out fromDisk))
                    {
                        return false;
                    }

                    list = fromDisk;
                    ByCharacter[characterInstance] = list;
                }

                int index = FindIndex_NoLock(list, questIdentity);
                if (index < 0)
                {
                    return false;
                }

                list.RemoveAt(index);
                if (list.Count == 0)
                {
                    ByCharacter.Remove(characterInstance);
                    TryDeleteSidecar(characterInstance);
                }
                else
                {
                    TryWriteSidecar(characterInstance, list);
                }

                return true;
            }
        }

        internal static bool TryRemoveExactPersisted(
            int characterInstance,
            Identity questIdentity,
            out string failure)
        {
            failure = string.Empty;
            if (characterInstance <= 0
                || questIdentity == null
                || questIdentity.Instance <= 0)
            {
                failure = "Exact accepted mission identity is required.";
                return false;
            }

            lock (Sync)
            {
                List<AcceptedMission> list;
                bool sidecarExists;
                if (!TryReadSidecarForExactRemoval(
                    characterInstance,
                    out list,
                    out sidecarExists,
                    out failure))
                {
                    return false;
                }

                if (!sidecarExists)
                {
                    List<AcceptedMission> current;
                    if (ByCharacter.TryGetValue(characterInstance, out current)
                        && current != null)
                    {
                        list = new List<AcceptedMission>(current);
                    }
                }

                int index = FindExactIndex_NoLock(list, questIdentity);
                if (index == -2)
                {
                    failure =
                        "Duplicate exact accepted mission identities make removal ambiguous.";
                    return false;
                }

                if (index >= 0)
                {
                    list.RemoveAt(index);
                }

                if (!TryWriteSidecarAtomic(characterInstance, list, out failure))
                {
                    return false;
                }

                if (FindExactIndex_NoLock(list, questIdentity) != -1)
                {
                    failure = "Exact accepted mission remains after durable removal.";
                    return false;
                }

                if (list.Count == 0)
                {
                    ByCharacter.Remove(characterInstance);
                }
                else
                {
                    ByCharacter[characterInstance] = list;
                }

                return true;
            }
        }

        public static void Clear(int characterInstance)
        {
            lock (Sync)
            {
                ByCharacter.Remove(characterInstance);
            }

            TryDeleteSidecar(characterInstance);
        }

        private static List<AcceptedMission> GetOrCreateList_NoLock(int characterInstance)
        {
            List<AcceptedMission> list;
            if (!ByCharacter.TryGetValue(characterInstance, out list) || list == null)
            {
                List<AcceptedMission> fromDisk;
                if (TryReadSidecar(characterInstance, out fromDisk))
                {
                    list = fromDisk;
                }
                else
                {
                    list = new List<AcceptedMission>();
                }

                ByCharacter[characterInstance] = list;
            }

            return list;
        }

        private static int FindIndex_NoLock(List<AcceptedMission> list, Identity questIdentity)
        {
            if (list == null || questIdentity == null || questIdentity.Instance == 0)
            {
                return -1;
            }

            for (int i = 0; i < list.Count; i++)
            {
                AcceptedMission m = list[i];
                if (m == null || m.QuestIdentity == null)
                {
                    continue;
                }

                if (m.QuestIdentity.Type == questIdentity.Type
                    && m.QuestIdentity.Instance == questIdentity.Instance)
                {
                    return i;
                }
            }

            // Client journal Delete sometimes sends Mission.Type=0 (or a mismatched type) with the
            // same instance — still remove so LOGIN-RESYNC cannot resurrect the mission.
            for (int i = 0; i < list.Count; i++)
            {
                AcceptedMission m = list[i];
                if (m != null && m.QuestIdentity != null
                    && m.QuestIdentity.Instance == questIdentity.Instance)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindExactIndex_NoLock(
            List<AcceptedMission> list,
            Identity questIdentity)
        {
            if (list == null || questIdentity == null || questIdentity.Instance == 0)
            {
                return -1;
            }

            int found = -1;
            for (int i = 0; i < list.Count; i++)
            {
                AcceptedMission mission = list[i];
                if (mission == null
                    || mission.QuestIdentity == null
                    || mission.QuestIdentity.Type != questIdentity.Type
                    || mission.QuestIdentity.Instance != questIdentity.Instance)
                {
                    continue;
                }

                if (found >= 0)
                {
                    return -2;
                }

                found = i;
            }

            return found;
        }

        private static void PruneExpired_NoLock(List<AcceptedMission> list)
        {
            DateTime now = DateTime.UtcNow;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null || list[i].ExpiryUtc <= now)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private static void TryWriteSidecar(int characterInstance, List<AcceptedMission> list)
        {
            try
            {
                string dir = SidecarDirectory();
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                foreach (AcceptedMission entry in list)
                {
                    if (entry == null
                        || entry.Projection != null
                        || (entry.QuestIdentity != null
                            && MissionAcgAllocationService.IsGeneratedAcceptedQuestIdentity(
                                (int)entry.QuestIdentity.Type,
                                entry.QuestIdentity.Instance)))
                    {
                        continue;
                    }

                    AppendSidecarEntry(sb, characterInstance, entry);
                }

                File.WriteAllText(SidecarPath(characterInstance), sb.ToString());
            }
            catch
            {
            }
        }

        private static bool TryWriteSidecarAtomic(
            int characterInstance,
            List<AcceptedMission> list,
            out string failure)
        {
            failure = string.Empty;
            string temporary = string.Empty;
            try
            {
                string dir = SidecarDirectory();
                Directory.CreateDirectory(dir);
                string target = SidecarPath(characterInstance);
                temporary =
                    target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var sb = new StringBuilder();
                foreach (AcceptedMission entry in list)
                {
                    if (entry == null
                        || entry.Projection != null
                        || (entry.QuestIdentity != null
                            && MissionAcgAllocationService.IsGeneratedAcceptedQuestIdentity(
                                (int)entry.QuestIdentity.Type,
                                entry.QuestIdentity.Instance)))
                    {
                        continue;
                    }

                    if (entry != null)
                    {
                        AppendSidecarEntry(sb, characterInstance, entry);
                    }
                }

                byte[] bytes = new UTF8Encoding(false).GetBytes(sb.ToString());
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(target))
                {
                    string backup = target + ".bak";
                    File.Replace(temporary, target, backup, true);
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                }
                else
                {
                    File.Move(temporary, target);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporary) && File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void AppendSidecarEntry(
            StringBuilder sb,
            int characterInstance,
            AcceptedMission entry)
        {
            sb.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}",
                        characterInstance,
                        (int)entry.QuestIdentity.Type,
                        entry.QuestIdentity.Instance,
                        entry.MissionIconId,
                        entry.Quality,
                        entry.ExpiryUtc.Ticks,
                        entry.MarkerPlayfield,
                        entry.EntranceLow,
                        entry.EntranceHigh,
                        entry.MarkerX,
                        entry.MarkerY,
                        entry.MarkerZ,
                        (entry.ShortInfo ?? string.Empty).Replace('|', '/').Replace('\r', ' ').Replace('\n', ' '),
                        (entry.TargetName ?? string.Empty).Replace('|', '/').Replace('\r', ' ').Replace('\n', ' '),
                        entry.TargetSide,
                        entry.CashReward,
                        entry.ExperienceReward);
            sb.AppendLine();
        }

        private static AcceptedMission BuildEntry(
            Identity acceptedQuestIdentity,
            Identity originalOfferIdentity,
            QuestInfo offer,
            DateTime expiryUtc,
            string targetName,
            int targetSide,
            int markerPf,
            int entranceLow,
            int entranceHigh,
            float markerX,
            float markerY,
            float markerZ)
        {
            return new AcceptedMission
                   {
                       QuestIdentity = acceptedQuestIdentity,
                       OriginalOfferIdentity = originalOfferIdentity,
                       MissionIconId = offer.MissionIconId != 0
                                           ? offer.MissionIconId
                                           : MissionTypeCatalog.KillPersonIcon,
                       Quality = offer.Quality,
                       ShortInfo = offer.ShortInfo ?? string.Empty,
                       TargetName = targetName ?? string.Empty,
                       TargetSide = targetSide,
                       CashReward = offer.CashReward,
                       ExperienceReward = offer.ExperienceReward,
                       ExpiryUtc = expiryUtc,
                       Offer = offer,
                       MarkerPlayfield = markerPf,
                       EntranceLow = entranceLow,
                       EntranceHigh = entranceHigh,
                       MarkerX = markerX,
                       MarkerY = markerY,
                       MarkerZ = markerZ
                   };
        }

        private static AcceptedMission BuildProjectionEntry(
            MissionAcgAcceptedProjection projection)
        {
            QuestInfo offer = projection.ReconstructOffer();
            MissionAcgInstanceBinding binding = projection.Binding;
            var acceptedIdentity = new Identity
                                   {
                                       Type = (IdentityType)binding.AcceptedQuestIdentity.Type,
                                       Instance = binding.AcceptedQuestIdentity.Instance
                                   };
            var originalOfferIdentity = new Identity
                                        {
                                            Type = (IdentityType)binding.OriginalOfferIdentity.Type,
                                            Instance = binding.OriginalOfferIdentity.Instance
                                        };
            AcceptedMission entry = BuildEntry(
                acceptedIdentity,
                originalOfferIdentity,
                offer,
                binding.ExpiryUtc,
                string.Empty,
                0,
                binding.ExteriorEntranceIdentity.Instance,
                binding.ExteriorEntranceLow,
                binding.ExteriorEntranceHigh,
                binding.ExteriorX,
                binding.ExteriorY,
                binding.ExteriorZ);
            entry.Projection = projection;
            entry.HasFrozenAcceptedRewards = true;
            entry.CashReward = projection.FrozenCashReward;
            entry.ExperienceReward = projection.FrozenExperienceReward;
            entry.FrozenItemRewardLowId = projection.FrozenItemLowId;
            entry.FrozenItemRewardHighId = projection.FrozenItemHighId;
            entry.FrozenItemRewardQuality = projection.FrozenItemQuality;
            entry.FrozenItemRewardCount = projection.FrozenItemCount;
            return entry;
        }

        private static void MergeGeneratedProjections_NoLock(
            int characterInstance,
            List<AcceptedMission> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                AcceptedMission entry = list[i];
                if (entry != null
                    && entry.QuestIdentity != null
                    && MissionAcgAllocationService.IsGeneratedAcceptedQuestIdentity(
                        (int)entry.QuestIdentity.Type,
                        entry.QuestIdentity.Instance))
                {
                    list.RemoveAt(i);
                }
            }

            if (!MissionAcgAcceptedProjectionRuntime.IsInitialized)
            {
                return;
            }

            IList<MissionAcgAcceptedProjection> projections =
                MissionAcgAcceptedProjectionRuntime.GetOwned(characterInstance);
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < projections.Count; i++)
            {
                MissionAcgAcceptedProjection projection = projections[i];
                bool activeLifecycle =
                    projection.LifecycleState == MissionAcgLifecycleState.Accepted
                    || projection.LifecycleState == MissionAcgLifecycleState.Active;
                if ((int)projection.AcceptancePhase
                        < (int)MissionAcgAcceptancePhase.AcceptanceCommitted
                    || !activeLifecycle
                    || projection.CleanupState != MissionAcgCleanupState.None
                    || projection.Binding.ExpiryUtc <= now)
                {
                    continue;
                }

                list.Add(BuildProjectionEntry(projection));
            }
        }

        private static bool TryReadSidecarForExactRemoval(
            int characterInstance,
            out List<AcceptedMission> list,
            out bool sidecarExists,
            out string failure)
        {
            list = new List<AcceptedMission>();
            sidecarExists = false;
            failure = string.Empty;
            try
            {
                string path = SidecarPath(characterInstance);
                if (!File.Exists(path))
                {
                    return true;
                }

                sidecarExists = true;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line =
                        lines[i] == null ? string.Empty : lines[i].Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    AcceptedMission mission;
                    if (!TryParseSidecarLineForExactRemoval(
                        characterInstance,
                        line,
                        out mission,
                        out failure))
                    {
                        failure =
                            path + " line " + (i + 1) + ": " + failure;
                        list = new List<AcceptedMission>();
                        return false;
                    }

                    list.Add(mission);
                }

                return true;
            }
            catch (Exception ex)
            {
                list = new List<AcceptedMission>();
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryParseSidecarLineForExactRemoval(
            int characterInstance,
            string line,
            out AcceptedMission mission,
            out string failure)
        {
            mission = null;
            failure = string.Empty;
            string[] parts = line.Split('|');
            if (parts.Length != 6
                && parts.Length != 7
                && parts.Length != 13
                && parts.Length != 15
                && parts.Length != 17)
            {
                failure = "Unsupported or truncated accepted-mission field set.";
                return false;
            }

            int storedCharacter;
            int type;
            int instance;
            int icon;
            int quality;
            long ticks;
            if (!int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out storedCharacter)
                || storedCharacter != characterInstance
                || !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out type)
                || !int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out instance)
                || !int.TryParse(
                    parts[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out icon)
                || !int.TryParse(
                    parts[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out quality)
                || !long.TryParse(
                    parts[5],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks))
            {
                failure = "Malformed accepted-mission identity or required field.";
                return false;
            }

            int markerPf = 0;
            int entranceLow = 0;
            int entranceHigh = 0;
            float markerX = 0;
            float markerY = 0;
            float markerZ = 0;
            string shortInfo = parts.Length >= 7 ? parts[6] : string.Empty;
            string targetName = string.Empty;
            int targetSide = 0;
            int cashReward = 0;
            int experienceReward = 0;
            if (parts.Length >= 13)
            {
                if (!int.TryParse(
                        parts[6],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out markerPf)
                    || !int.TryParse(
                        parts[7],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out entranceLow)
                    || !int.TryParse(
                        parts[8],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out entranceHigh)
                    || !float.TryParse(
                        parts[9],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out markerX)
                    || !float.TryParse(
                        parts[10],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out markerY)
                    || !float.TryParse(
                        parts[11],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out markerZ)
                    || float.IsNaN(markerX)
                    || float.IsInfinity(markerX)
                    || float.IsNaN(markerY)
                    || float.IsInfinity(markerY)
                    || float.IsNaN(markerZ)
                    || float.IsInfinity(markerZ))
                {
                    failure = "Malformed accepted-mission exterior marker.";
                    return false;
                }

                shortInfo = parts[12];
            }

            if (parts.Length >= 15)
            {
                targetName = parts[13] ?? string.Empty;
                if (!int.TryParse(
                    parts[14],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out targetSide))
                {
                    failure = "Malformed accepted-mission target side.";
                    return false;
                }
            }

            if (parts.Length >= 17
                && (!int.TryParse(
                        parts[15],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out cashReward)
                    || !int.TryParse(
                        parts[16],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out experienceReward)))
            {
                failure = "Malformed accepted-mission reward fields.";
                return false;
            }

            mission =
                new AcceptedMission
                {
                    QuestIdentity =
                        new Identity
                        {
                            Type = (IdentityType)type,
                            Instance = instance
                        },
                    MissionIconId = icon,
                    Quality = quality,
                    ExpiryUtc = new DateTime(ticks, DateTimeKind.Utc),
                    ShortInfo = shortInfo,
                    TargetName = targetName,
                    TargetSide = targetSide,
                    CashReward = cashReward,
                    ExperienceReward = experienceReward,
                    Offer = null,
                    MarkerPlayfield = markerPf,
                    EntranceLow = entranceLow,
                    EntranceHigh = entranceHigh,
                    MarkerX = markerX,
                    MarkerY = markerY,
                    MarkerZ = markerZ
                };
            return true;
        }

        private static bool TryReadSidecar(int characterInstance, out List<AcceptedMission> list)
        {
            list = new List<AcceptedMission>();
            try
            {
                string path = SidecarPath(characterInstance);
                if (!File.Exists(path))
                {
                    return false;
                }

                string[] lines = File.ReadAllLines(path);
                DateTime now = DateTime.UtcNow;
                foreach (string raw in lines)
                {
                    string line = raw == null ? string.Empty : raw.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    string[] parts = line.Split('|');
                    if (parts.Length < 6)
                    {
                        continue;
                    }

                    int type;
                    int instance;
                    int icon;
                    int quality;
                    long ticks;
                    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out type)
                        || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out instance)
                        || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out icon)
                        || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out quality)
                        || !long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
                    {
                        continue;
                    }

                    if (MissionAcgAllocationService.IsGeneratedAcceptedQuestIdentity(
                        type,
                        instance))
                    {
                        MissionDiagnostics.Log(
                            "ACG-ACCEPTED-LEGACY-REJECT owner={0} accepted={1}:{2} reason=incomplete-unversioned-projection",
                            characterInstance,
                            type,
                            instance);
                        continue;
                    }

                    var expiry = new DateTime(ticks, DateTimeKind.Utc);
                    if (expiry <= now)
                    {
                        continue;
                    }

                    int markerPf = 0;
                    int entranceLow = 0;
                    int entranceHigh = 0;
                    float mx = 0;
                    float my = 0;
                    float mz = 0;
                    string shortInfo;
                    string targetName = string.Empty;
                    int targetSide = 0;
                    int cashReward = 0;
                    int experienceReward = 0;
                    if (parts.Length >= 13)
                    {
                        int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out markerPf);
                        int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out entranceLow);
                        int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out entranceHigh);
                        float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out mx);
                        float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out my);
                        float.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out mz);
                        shortInfo = parts[12];
                        if (parts.Length >= 15)
                        {
                            targetName = parts[13] ?? string.Empty;
                            int.TryParse(parts[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out targetSide);
                        }

                        if (parts.Length >= 17)
                        {
                            int.TryParse(parts[15], NumberStyles.Integer, CultureInfo.InvariantCulture, out cashReward);
                            int.TryParse(parts[16], NumberStyles.Integer, CultureInfo.InvariantCulture, out experienceReward);
                        }
                    }
                    else
                    {
                        // Legacy sidecar without marker fields.
                        shortInfo = parts.Length > 6 ? parts[6] : string.Empty;
                    }

                    list.Add(
                        new AcceptedMission
                        {
                            QuestIdentity = new Identity { Type = (IdentityType)type, Instance = instance },
                            MissionIconId = icon,
                            Quality = quality,
                            ExpiryUtc = expiry,
                            ShortInfo = shortInfo,
                            TargetName = targetName,
                            TargetSide = targetSide,
                            CashReward = cashReward,
                            ExperienceReward = experienceReward,
                            Offer = null,
                            MarkerPlayfield = markerPf,
                            EntranceLow = entranceLow,
                            EntranceHigh = entranceHigh,
                            MarkerX = mx,
                            MarkerY = my,
                            MarkerZ = mz
                        });
                }

                return list.Count > 0;
            }
            catch
            {
                list = new List<AcceptedMission>();
                return false;
            }
        }

        private static void TryDeleteSidecar(int characterInstance)
        {
            try
            {
                string path = SidecarPath(characterInstance);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string SidecarDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-state");
        }

        private static string SidecarPath(int characterInstance)
        {
            return Path.Combine(SidecarDirectory(), characterInstance.ToString(CultureInfo.InvariantCulture) + ".txt");
        }
    }
}
