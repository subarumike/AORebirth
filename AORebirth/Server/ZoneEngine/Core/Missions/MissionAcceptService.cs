namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Fills / restores the client's mission journal. Each accepted mission is a patched copy of the
    /// captured QuestFullUpdate (capture 20260717-pull-mish-doit). Multiple missions are kept by sending
    /// one FullUpdate per mission (add) and only deleting the quest id being replaced/removed.
    /// </summary>
    internal static class MissionAcceptService
    {
        /// <summary>Client mission-clock anchor set by our constant GameTimeMessage (see PerkResetMissionSender).</summary>
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        /// <summary>Mission time window mirrored in the "Remain" countdown (48 hours).</summary>
        private const int MissionDurationSeconds = 48 * 60 * 60;

        /// <summary>QuestId type in the captured packet (Quest[0].QuestId.Type).</summary>
        private const int MissionIdentityType = 0x0000DAC3;

        /// <summary>QuestId instance in the captured packet (Quest[0].QuestId.Instance).</summary>
        private const int MissionInstance = 0x55509493;

        /// <summary>Byte offset of QuestId.Instance in the full transport packet.</summary>
        private const int QuestIdInstanceOffset = 37;

        /// <summary>Byte offset of MissionIconId in the full transport packet (captured Kill = 0x2C42).</summary>
        private const int MissionIconIdOffset = 563;

        private const float GameTimeUnknown1 = 30024.0f;

        private const int GameTimeUnknown3 = 185408;

        private const float GameTimeUnknown4 = 80183.3125f;

        /// <summary>
        /// Re-sends every accepted mission after login/zone (ClientConnected FullCharacter path).
        /// </summary>
        public static bool TryResendForLogin(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(character.Identity.Instance);
            bool hasKey = MissionKeyGrantService.HasMissionKey(character);
            if (all.Count == 0 && !hasKey)
            {
                MissionDiagnostics.Log(
                    "LOGIN-RESYNC skip char={0} hasKey=false count=0",
                    character.Identity.Instance);
                return false;
            }

            if (all.Count == 0)
            {
                // Key without store — nothing typed to restore.
                MissionDiagnostics.Log(
                    "LOGIN-RESYNC skip char={0} hasKey=true count=0",
                    character.Identity.Instance);
                return false;
            }

            ReanchorGameTime(character);

            int sent = 0;
            foreach (MissionAcceptedStore.AcceptedMission entry in all)
            {
                if (SendOneMissionWindow(character, entry.Offer, entry, register: false))
                {
                    sent++;
                }
            }

            MissionDiagnostics.Log(
                "LOGIN-RESYNC char={0} hasKey={1} count={2} sent={3}",
                character.Identity.Instance,
                hasKey,
                all.Count,
                sent);
            return sent > 0;
        }

        /// <summary>
        /// Adds one newly accepted mission to the journal (does not remove other active missions).
        /// </summary>
        public static bool SendAcceptedMission(ICharacter character, QuestInfo offer)
        {
            if (offer == null)
            {
                return false;
            }

            ReanchorGameTime(character);
            var fresh = new MissionAcceptedStore.AcceptedMission
                        {
                            QuestIdentity = offer.QuestIdentity,
                            MissionIconId = offer.MissionIconId,
                            Quality = offer.Quality,
                            ShortInfo = offer.ShortInfo,
                            ExpiryUtc = DateTime.UtcNow.AddSeconds(MissionDurationSeconds),
                            Offer = offer
                        };
            if (offer.QuestActions != null && offer.QuestActions.Length > 0 && offer.QuestActions[0] != null)
            {
                QuestActionList action = offer.QuestActions[0];
                fresh.MarkerPlayfield = action.Playfield.Instance;
                fresh.EntranceLow = action.Unknown18;
                fresh.EntranceHigh = action.Unknown19;
                fresh.MarkerX = action.X;
                fresh.MarkerY = action.Y;
                fresh.MarkerZ = action.Z;
            }

            // Live accept (20260718-062936): CreateQuest → MissionKey → QuestFullUpdate only.
            // Do not Delete+re-broadcast every stored mission here — that storm cleared the journal UI
            // while the key still arrived (RefreshAllMissionTimers after accept).
            return SendOneMissionWindow(character, offer, fresh, register: true);
        }

        /// <summary>
        /// Re-sends all stored missions with current Remain values (zone/login resync only).
        /// </summary>
        public static void RefreshAllMissionTimers(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all.Count == 0)
            {
                return;
            }

            ReanchorGameTime(character);

            int sent = 0;
            foreach (MissionAcceptedStore.AcceptedMission entry in all)
            {
                // Resync: push FullUpdate only (no Delete). Delete+single-quest FU was wiping the window.
                if (SendOneMissionWindow(character, entry.Offer, entry, register: false, deleteBeforeSend: false))
                {
                    sent++;
                }
            }

            MissionDiagnostics.Log(
                "TIMER-REFRESH char={0} count={1} sent={2}",
                character.Identity.Instance,
                all.Count,
                sent);
        }

        private static void ReanchorGameTime(ICharacter character)
        {
            var client = character != null && character.Controller != null
                             ? character.Controller.Client as ZoneClient
                             : null;
            if (client == null || character == null)
            {
                return;
            }

            // Must match ClientConnected: IdentityType.CanbeAffected + character instance. Using SimpleChar
            // here made the client ignore the mid-session GameTime, so Remain stayed wrong until a full zone
            // re-anchored the clock.
            client.SendCompressed(
                new GameTimeMessage
                {
                    Identity =
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = character.Identity.Instance
                        },
                    Unknown1 = GameTimeUnknown1,
                    Unknown3 = GameTimeUnknown3,
                    Unknown4 = GameTimeUnknown4
                });
            client.LastGameTimeSyncUtc = DateTime.UtcNow;
        }

        private static bool SendOneMissionWindow(
            ICharacter character,
            QuestInfo offer,
            MissionAcceptedStore.AcceptedMission stored,
            bool register,
            bool deleteBeforeSend = false)
        {
            if (character == null || character.Controller == null)
            {
                return false;
            }

            var client = character.Controller.Client as ZoneClient;
            if (client == null)
            {
                return false;
            }

            try
            {
                int iconId = offer != null
                                 ? offer.MissionIconId
                                 : (stored != null ? stored.MissionIconId : MissionTypeCatalog.KillPersonIcon);
                if (iconId == 0)
                {
                    iconId = MissionTypeCatalog.KillPersonIcon;
                }

                Identity questId = offer != null
                                       ? offer.QuestIdentity
                                       : (stored != null
                                              ? stored.QuestIdentity
                                              : new Identity
                                                {
                                                    Type = (IdentityType)MissionIdentityType,
                                                    Instance = MissionInstance
                                                });
                if ((int)questId.Type == 0 || questId.Instance == 0)
                {
                    questId = new Identity { Type = (IdentityType)MissionIdentityType, Instance = MissionInstance };
                }

                int remainingSeconds = MissionDurationSeconds;
                if (stored != null)
                {
                    double left = (stored.ExpiryUtc - DateTime.UtcNow).TotalSeconds;
                    if (left <= 0)
                    {
                        MissionAcceptedStore.Remove(character.Identity.Instance, questId);
                        MissionDiagnostics.Log(
                            "ACCEPT-WINDOW expired char={0} quest={1:X8}",
                            character.Identity.Instance,
                            questId.Instance);
                        return false;
                    }

                    remainingSeconds = (int)left;
                    if (remainingSeconds > MissionDurationSeconds)
                    {
                        remainingSeconds = MissionDurationSeconds;
                    }

                    if (remainingSeconds < 1)
                    {
                        remainingSeconds = 1;
                    }
                }

                byte[] packet = HexToBytes(MissionAcceptCaptureTemplate.CapturedPacketHex);
                ReplaceInstance(packet, MissionAcceptCaptureTemplate.CapturedCharacterInstance, character.Identity.Instance);

                double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
                if (secondsSinceSync < 0)
                {
                    secondsSinceSync = 0;
                }

                if (secondsSinceSync > MissionDurationSeconds)
                {
                    secondsSinceSync = 0;
                    client.LastGameTimeSyncUtc = DateTime.UtcNow;
                }

                long clientClockNow = ClientClockBaseSeconds + (long)secondsSinceSync;
                long expiry = clientClockNow + remainingSeconds;
                WriteInt32BigEndian(packet, MissionAcceptCaptureTemplate.ExpiryOffset, (int)expiry);
                WriteInt32BigEndian(packet, QuestIdInstanceOffset, questId.Instance);
                WriteInt32BigEndian(packet, MissionIconIdOffset, iconId);
                ApplyMarkerLocation(packet, offer, stored);

                DateTime expiryUtc = stored != null
                                         ? stored.ExpiryUtc
                                         : DateTime.UtcNow.AddSeconds(MissionDurationSeconds);
                if (register && offer != null)
                {
                    MissionAcceptedStore.Register(character.Identity.Instance, offer, expiryUtc);
                }

                // Live accept does not Delete before the first FullUpdate. Only delete when explicitly
                // replacing an already-shown quest id (optional).
                if (deleteBeforeSend)
                {
                    client.SendCompressed(
                        new QuestMessage
                        {
                            Identity = character.Identity,
                            Unknown = 0,
                            Action = QuestAction.Delete,
                            Unknown1 = 0,
                            Mission = questId,
                            Unknown2 = 0,
                            Unknown3 = 0
                        });
                }

                // Prefer SendCompressed (same path as GameTime) so FullUpdate stays ordered with the clock.
                client.SendCompressed(packet);

                int markerPf = 0;
                float markerX = 0;
                float markerZ = 0;
                if (stored != null && stored.MarkerPlayfield != 0)
                {
                    markerPf = stored.MarkerPlayfield;
                    markerX = stored.MarkerX;
                    markerZ = stored.MarkerZ;
                }
                else if (offer != null && offer.QuestActions != null && offer.QuestActions.Length > 0
                         && offer.QuestActions[0] != null)
                {
                    markerPf = offer.QuestActions[0].Playfield.Instance;
                    markerX = offer.QuestActions[0].X;
                    markerZ = offer.QuestActions[0].Z;
                }

                MissionDiagnostics.Log(
                    "ACCEPT-WINDOW char={0} quest={1:X8} icon={2} ql={3} remainSec={4} expiry={5} sinceSync={6} register={7} deleteFirst={8} markerPf={9} xz=({10:0.###},{11:0.###})",
                    character.Identity.Instance,
                    questId.Instance,
                    iconId,
                    offer != null ? offer.Quality : (stored != null ? stored.Quality : 0),
                    remainingSeconds,
                    expiry,
                    (long)secondsSinceSync,
                    register,
                    deleteBeforeSend,
                    markerPf,
                    markerX,
                    markerZ);

                return true;
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
                return false;
            }
        }

        /// <summary>
        /// Overwrites the capture shell's Rome Blue marker with the rolled offer outdoor location.
        /// </summary>
        private static void ApplyMarkerLocation(
            byte[] packet,
            QuestInfo offer,
            MissionAcceptedStore.AcceptedMission stored)
        {
            int pf = 0;
            int entranceLow = 0;
            int entranceHigh = 0;
            float x = 0;
            float y = 0;
            float z = 0;

            if (stored != null && stored.MarkerPlayfield != 0)
            {
                pf = stored.MarkerPlayfield;
                entranceLow = stored.EntranceLow;
                entranceHigh = stored.EntranceHigh;
                x = stored.MarkerX;
                y = stored.MarkerY;
                z = stored.MarkerZ;
            }
            else if (offer != null && offer.QuestActions != null && offer.QuestActions.Length > 0
                     && offer.QuestActions[0] != null)
            {
                QuestActionList action = offer.QuestActions[0];
                pf = action.Playfield.Instance;
                entranceLow = action.Unknown18;
                entranceHigh = action.Unknown19;
                x = action.X;
                y = action.Y;
                z = action.Z;
            }

            if (pf == 0)
            {
                return;
            }

            // Keep Playfield2 type (0x9C50) from the capture; only replace instance + entrance + XYZ.
            WriteInt32BigEndian(packet, MissionAcceptCaptureTemplate.LocationPlayfieldInstanceOffset, pf);
            WriteInt32BigEndian(packet, MissionAcceptCaptureTemplate.LocationEntranceLowOffset, entranceLow);
            WriteInt32BigEndian(packet, MissionAcceptCaptureTemplate.LocationEntranceHighOffset, entranceHigh);
            WriteFloatBigEndian(packet, MissionAcceptCaptureTemplate.LocationXOffset, x);
            WriteFloatBigEndian(packet, MissionAcceptCaptureTemplate.LocationYOffset, y);
            WriteFloatBigEndian(packet, MissionAcceptCaptureTemplate.LocationZOffset, z);
        }

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            byte f0 = (byte)(from >> 24);
            byte f1 = (byte)(from >> 16);
            byte f2 = (byte)(from >> 8);
            byte f3 = (byte)from;

            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == f0 && packet[i + 1] == f1 && packet[i + 2] == f2 && packet[i + 3] == f3)
                {
                    WriteInt32BigEndian(packet, i, to);
                    i += 3;
                }
            }
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static void WriteFloatBigEndian(byte[] buffer, int offset, float value)
        {
            byte[] bits = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer[offset] = bits[3];
                buffer[offset + 1] = bits[2];
                buffer[offset + 2] = bits[1];
                buffer[offset + 3] = bits[0];
            }
            else
            {
                buffer[offset] = bits[0];
                buffer[offset + 1] = bits[1];
                buffer[offset + 2] = bits[2];
                buffer[offset + 3] = bits[3];
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
