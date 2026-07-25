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

            var entry = new AcceptedMission
                        {
                            QuestIdentity = offer.QuestIdentity,
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
                            MarkerX = mx,
                            MarkerY = my,
                            MarkerZ = mz
                        };

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
                        return new List<AcceptedMission>();
                    }
                }

                PruneExpired_NoLock(list);
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
                    if (entry == null)
                    {
                        continue;
                    }

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

                File.WriteAllText(SidecarPath(characterInstance), sb.ToString());
            }
            catch
            {
            }
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
