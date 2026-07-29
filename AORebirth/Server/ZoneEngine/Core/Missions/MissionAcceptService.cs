namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Text;

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
                if (entry == null)
                {
                    continue;
                }

                // Sidecar-only Repair (Offer null) was crashing clients: Kill-template QFU + Repair icon.
                // Drop those until a Repair capture template exists.
                MissionRollType type = MissionTypeCatalog.TypeFromIcon(entry.MissionIconId);
                if (entry.Offer == null && type == MissionRollType.RepairMachine)
                {
                    MissionAcceptedStore.Remove(character.Identity.Instance, entry.QuestIdentity);
                    MissionDiagnostics.Log(
                        "LOGIN-RESYNC drop-sidecar-repair char={0} quest={1:X8}",
                        character.Identity.Instance,
                        entry.QuestIdentity.Instance);
                    continue;
                }

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

        public static bool SendAcceptedGeneratedMission(
            ICharacter character,
            QuestInfo offer,
            MissionAcgBindingRecord bindingRecord)
        {
            if (character == null || offer == null || bindingRecord == null)
            {
                return false;
            }

            MissionAcgInstanceBinding binding = bindingRecord.Binding;
            ReanchorGameTime(character);
            var stored = new MissionAcceptedStore.AcceptedMission
                         {
                             QuestIdentity =
                                 new Identity
                                 {
                                     Type = (IdentityType)binding.AcceptedQuestIdentity.Type,
                                     Instance = binding.AcceptedQuestIdentity.Instance
                                 },
                             OriginalOfferIdentity = offer.QuestIdentity,
                             MissionIconId = offer.MissionIconId,
                             Quality = offer.Quality,
                             ShortInfo = offer.ShortInfo,
                             ExpiryUtc = binding.ExpiryUtc,
                             Offer = offer,
                             MarkerPlayfield = binding.ExteriorEntranceIdentity.Instance,
                             EntranceLow = binding.ExteriorEntranceLow,
                             EntranceHigh = binding.ExteriorEntranceHigh,
                             MarkerX = binding.ExteriorX,
                             MarkerY = binding.ExteriorY,
                             MarkerZ = binding.ExteriorZ
                         };
            return SendStructuredGeneratedMission(
                character,
                offer,
                stored,
                binding);
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
            bool deleteBeforeSend = false,
            MissionAcgInstanceBinding acgBinding = null)
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

                Identity questId = stored != null && stored.QuestIdentity != null
                                       ? stored.QuestIdentity
                                       : (offer != null
                                              ? offer.QuestIdentity
                                              : new Identity
                                                {
                                                    Type = (IdentityType)MissionIdentityType,
                                                    Instance = MissionInstance
                                                });
                if ((int)questId.Type == 0 || questId.Instance == 0)
                {
                    questId = new Identity { Type = (IdentityType)MissionIdentityType, Instance = MissionInstance };
                }

                if (acgBinding == null)
                {
                    MissionAcgBindingRecord restoredBinding;
                    if (MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                        character.Identity.Instance,
                        questId.Instance,
                        out restoredBinding))
                    {
                        acgBinding = restoredBinding.Binding;
                    }
                }

                if (acgBinding != null)
                {
                    return SendStructuredGeneratedMission(
                        character,
                        offer ?? (stored == null ? null : stored.Offer),
                        stored,
                        acgBinding);
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
                if (acgBinding != null)
                {
                    WriteInt32BigEndian(
                        packet,
                        MissionAcceptCaptureTemplate.BuildingIdentityTypeOffset,
                        acgBinding.AcgBuildingIdentity.Type);
                    WriteInt32BigEndian(
                        packet,
                        MissionAcceptCaptureTemplate.BuildingIdentityInstanceOffset,
                        acgBinding.AcgBuildingIdentity.Instance);
                    ReplaceInstance(
                        packet,
                        MissionAcceptCaptureTemplate.CapturedMissionKeyInstance,
                        acgBinding.MissionKeyIdentity.Instance);
                    ReplaceInstance(
                        packet,
                        MissionAcceptCaptureTemplate.CapturedIssuingTerminalInstance,
                        acgBinding.IssuingTerminalIdentity.Instance);
                }

                string targetName = stored != null ? stored.TargetName : null;
                int targetSide = stored != null ? stored.TargetSide : 0;
                MissionRollType missionType = MissionTypeCatalog.TypeFromIcon(iconId);

                // Find Person: journal must keep Find text from the roll offer (not Kill "stronghold").
                // Same Kill QFU shell lengths — fit offer ShortInfo/Info over the capture ASCII blobs.
                if (missionType == MissionRollType.FindPerson
                    && offer != null
                    && !string.IsNullOrEmpty(offer.Info))
                {
                    if (string.IsNullOrEmpty(targetName))
                    {
                        ResolveFindPersonNameAndSide(character, offer, out targetName, out targetSide);
                    }
                    else if (targetSide != (int)Side.Omni && targetSide != (int)Side.Clan)
                    {
                        targetSide = MissionInstanceService.ResolveFindPersonSideForCharacter(character);
                    }

                    string shortInfo = string.IsNullOrEmpty(offer.ShortInfo)
                                           ? MissionAcceptCaptureTemplate.TemplateKillShortInfo
                                           : offer.ShortInfo;
                    string info = InjectDisplayNameIntoFindInfo(offer.Info, targetName);
                    PatchAsciiBlob(
                        packet,
                        MissionAcceptCaptureTemplate.TemplateKillShortInfo,
                        FitAsciiName(shortInfo, MissionAcceptCaptureTemplate.TemplateKillShortInfo.Length));
                    PatchAsciiBlob(
                        packet,
                        MissionAcceptCaptureTemplate.TemplateKillInfo,
                        FitAsciiName(info, MissionAcceptCaptureTemplate.TemplateKillInfo.Length));
                }
                else if (missionType == MissionRollType.FindPerson
                         && stored != null
                         && stored.Offer != null
                         && !string.IsNullOrEmpty(stored.Offer.Info))
                {
                    // Login resync: offer may be null on the call but stored.Offer still has Find text.
                    if (string.IsNullOrEmpty(targetName))
                    {
                        ResolveFindPersonNameAndSide(character, stored.Offer, out targetName, out targetSide);
                    }
                    else if (targetSide != (int)Side.Omni && targetSide != (int)Side.Clan)
                    {
                        targetSide = MissionInstanceService.ResolveFindPersonSideForCharacter(character);
                    }

                    string shortInfo = string.IsNullOrEmpty(stored.Offer.ShortInfo)
                                           ? MissionAcceptCaptureTemplate.TemplateKillShortInfo
                                           : stored.Offer.ShortInfo;
                    string info = InjectDisplayNameIntoFindInfo(stored.Offer.Info, targetName);
                    PatchAsciiBlob(
                        packet,
                        MissionAcceptCaptureTemplate.TemplateKillShortInfo,
                        FitAsciiName(shortInfo, MissionAcceptCaptureTemplate.TemplateKillShortInfo.Length));
                    PatchAsciiBlob(
                        packet,
                        MissionAcceptCaptureTemplate.TemplateKillInfo,
                        FitAsciiName(info, MissionAcceptCaptureTemplate.TemplateKillInfo.Length));
                }
                else
                {
                    bool patchKillName = missionType == MissionRollType.KillPerson;
                    if (string.IsNullOrEmpty(targetName))
                    {
                        if (patchKillName)
                        {
                            ResolveAndPatchObjectiveName(packet, missionType, offer, out targetName, out targetSide);
                        }
                    }
                    else if (patchKillName)
                    {
                        PatchTemplateKillName(packet, targetName);
                    }
                }

                if (stored != null)
                {
                    stored.TargetName = targetName ?? string.Empty;
                    stored.TargetSide = targetSide;
                }

                DateTime expiryUtc = stored != null
                                         ? stored.ExpiryUtc
                                         : DateTime.UtcNow.AddSeconds(MissionDurationSeconds);
                if (register && offer != null)
                {
                    MissionAcceptedStore.Register(
                        character.Identity.Instance,
                        offer,
                        expiryUtc,
                        targetName,
                        targetSide);
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

        private static bool SendStructuredGeneratedMission(
            ICharacter character,
            QuestInfo offer,
            MissionAcceptedStore.AcceptedMission stored,
            MissionAcgInstanceBinding binding)
        {
            if (character == null
                || character.Controller == null
                || offer == null
                || binding == null)
            {
                return false;
            }

            var client = character.Controller.Client as ZoneClient;
            if (client == null)
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                character.Identity.Instance,
                binding.AcceptedQuestIdentity.Instance,
                out objective))
            {
                return false;
            }

            try
            {
                MissionAcgAcceptedQfuContract contract =
                    MissionAcgAcceptedQfuBuilder.Build(
                        character,
                        offer,
                        binding,
                        objective);
                client.SendCompressed(contract.Message);
                if (stored != null)
                {
                    stored.TargetName = objective.Binding.ObjectiveName;
                }

                MissionDiagnostics.Log(
                    "ACCEPT-QFU-STRUCTURED char={0} accepted={1}:{2} type={3} version={4} flag={5} building={6}:{7} objective={8}:{9} livePf2={10}",
                    character.Identity.Instance,
                    binding.AcceptedQuestIdentity.Type,
                    binding.AcceptedQuestIdentity.Instance,
                    binding.MissionType,
                    contract.QuestActionVersion,
                    contract.QuestIdentityFlag,
                    binding.AcgBuildingIdentity.Type,
                    binding.AcgBuildingIdentity.Instance,
                    objective.Binding.RuntimeObjectiveIdentity.Type,
                    objective.Binding.RuntimeObjectiveIdentity.Instance,
                    binding.AllocatedLivePlayfield2);
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

        private static void ResolveFindPersonNameAndSide(
            ICharacter character,
            QuestInfo offer,
            out string targetName,
            out int targetSide)
        {
            targetSide = MissionInstanceService.ResolveFindPersonSideForCharacter(character);
            if (offer != null && !string.IsNullOrEmpty(offer.Info))
            {
                if (offer.Info.IndexOf("omni side", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetSide = (int)Side.Omni;
                }
                else if (offer.Info.IndexOf("clan side", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetSide = (int)Side.Clan;
                }
            }

            var rng = new Random(unchecked(Environment.TickCount * 911)
                                 ^ (offer != null ? offer.QuestIdentity.Instance : 0));
            if (!TryExtractFindPersonName(offer != null ? offer.Info : null, out targetName)
                || string.IsNullOrEmpty(targetName))
            {
                targetName = MissionTargetNameCatalog.PickFindName(rng);
            }
        }

        private static bool TryExtractFindPersonName(string info, out string name)
        {
            name = null;
            if (string.IsNullOrEmpty(info))
            {
                return false;
            }

            // Capture Find texts: "quickly find Jeanne Messamore," / "find an enemy agent"
            int idx = info.IndexOf("find ", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            int start = idx + 5;
            while (start < info.Length && info[start] == ' ')
            {
                start++;
            }

            if (start >= info.Length || !char.IsUpper(info[start]))
            {
                return false;
            }

            int end = start;
            while (end < info.Length
                   && (char.IsLetter(info[end]) || info[end] == ' ' || info[end] == '-' || info[end] == '\''))
            {
                end++;
            }

            string candidate = info.Substring(start, end - start).Trim();
            if (candidate.Length < 5 || candidate.IndexOf(' ') < 0)
            {
                return false;
            }

            // Reject "him / her" style.
            if (candidate.IndexOf("him", StringComparison.OrdinalIgnoreCase) >= 0
                || candidate.IndexOf("her", StringComparison.OrdinalIgnoreCase) >= 0
                || candidate.IndexOf("enemy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            name = candidate;
            return true;
        }

        private static string InjectDisplayNameIntoFindInfo(string info, string displayName)
        {
            if (string.IsNullOrEmpty(info) || string.IsNullOrEmpty(displayName))
            {
                return info ?? string.Empty;
            }

            string existing;
            if (!TryExtractFindPersonName(info, out existing) || string.IsNullOrEmpty(existing))
            {
                return info;
            }

            string fitted = FitAsciiName(displayName, existing.Length);
            int idx = info.IndexOf(existing, StringComparison.Ordinal);
            if (idx < 0)
            {
                return info;
            }

            return info.Substring(0, idx) + fitted + info.Substring(idx + existing.Length);
        }

        private static void PatchAsciiBlob(byte[] packet, string fromText, string toText)
        {
            if (packet == null || string.IsNullOrEmpty(fromText) || string.IsNullOrEmpty(toText))
            {
                return;
            }

            if (fromText.Length != toText.Length)
            {
                return;
            }

            byte[] from = Encoding.ASCII.GetBytes(fromText);
            byte[] to = Encoding.ASCII.GetBytes(toText);
            for (int i = 0; i + from.Length <= packet.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < from.Length; j++)
                {
                    if (packet[i + j] != from[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                for (int j = 0; j < to.Length; j++)
                {
                    packet[i + j] = to[j];
                }

                return;
            }
        }

        private static void ResolveAndPatchObjectiveName(
            byte[] packet,
            MissionRollType type,
            QuestInfo offer,
            out string targetName,
            out int targetSide)
        {
            targetName = MissionAcceptCaptureTemplate.TemplateKillTargetName;
            targetSide = (int)Side.Clan;

            if (offer != null && !string.IsNullOrEmpty(offer.Info))
            {
                if (offer.Info.IndexOf("omni side", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetSide = (int)Side.Omni;
                }
                else if (offer.Info.IndexOf("clan side", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetSide = (int)Side.Clan;
                }
                else
                {
                    targetSide = (int)Side.Neutral;
                }
            }

            var rng = new Random(unchecked(Environment.TickCount * 911)
                                 ^ (offer != null ? offer.QuestIdentity.Instance : 0));
            if (type == MissionRollType.FindPerson)
            {
                targetName = MissionTargetNameCatalog.PickFindName(rng);
            }
            else if (type == MissionRollType.KillPerson)
            {
                targetName = MissionTargetNameCatalog.PickKillName(rng);
            }
            else
            {
                // Non-person objectives keep template text; no spawn rename.
                targetName = MissionAcceptCaptureTemplate.TemplateKillTargetName;
                return;
            }

            PatchTemplateKillName(packet, targetName);
        }

        /// <summary>
        /// Replace the fixed capture name in the accept QFU Info (same ASCII length — wire-safe).
        /// </summary>
        private static void PatchTemplateKillName(byte[] packet, string displayName)
        {
            if (packet == null || string.IsNullOrEmpty(displayName))
            {
                return;
            }

            string template = MissionAcceptCaptureTemplate.TemplateKillTargetName;
            byte[] from = Encoding.ASCII.GetBytes(template);
            string fitted = FitAsciiName(displayName, template.Length);
            byte[] to = Encoding.ASCII.GetBytes(fitted);
            if (from.Length != to.Length)
            {
                return;
            }

            for (int i = 0; i + from.Length <= packet.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < from.Length; j++)
                {
                    if (packet[i + j] != from[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    for (int j = 0; j < to.Length; j++)
                    {
                        packet[i + j] = to[j];
                    }

                    return;
                }
            }
        }

        private static string FitAsciiName(string name, int length)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == length)
            {
                return trimmed;
            }

            if (trimmed.Length > length)
            {
                return trimmed.Substring(0, length);
            }

            return trimmed.PadRight(length, ' ');
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
