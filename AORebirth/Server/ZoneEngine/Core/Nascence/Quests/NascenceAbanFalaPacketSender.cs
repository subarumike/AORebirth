namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Aban Fala QuestFullUpdate packets (20260822-224319 mission-flow.replay.log).
    /// Typed QFU built from decoded IN-MISSION-QUEST fields; expiry patched like Donna Red.
    /// </summary>
    internal static class NascenceAbanFalaPacketSender
    {
        private const int MissionIdentityType = 0x0000DAC3;
        private const int CapturedCharacterInstance = unchecked((int)0x7A1ADE69);
        private const int DonnaInstance = unchecked((int)0x7A18D4B1);

        private const int MissionDurationSeconds = 0xA8C0;

        private const long ClientClockBaseSeconds = 1_201_445_827L;

        private const float GameTimeUnknown1 = 30024.0f;
        private const int GameTimeUnknown3 = 185408;
        private const float GameTimeUnknown4 = 80183.3125f;

        private const int MissionInsigniaInstance = unchecked((int)0x55AAB052);
        private const int MissionDeviceInfoInstance = unchecked((int)0x55AAB053);
        private const int MissionGardenInstance = unchecked((int)0x55AAB054);
        private const int MissionSoulsInstance = unchecked((int)0x55ABF806);
        private const int MissionSoulsOneInstance = unchecked((int)0x55ABAD58);
        private const int MissionSoulsTwoInstance = unchecked((int)0x55ABAD5A);
        private const int MissionSoulsReturnInstance = unchecked((int)0x55ABAD60);
        private const int MissionDonnaInstance = unchecked((int)0x55ABAD4D);
        private const int LuxWeiInstance = unchecked((int)0x7A2013BC);

        private const int MobHashIsoa = unchecked((int)0x49534F41);
        private const int MobHashEcab = unchecked((int)0x45434142);
        private const int MobHashGarden = 18758;

        private const string InsigniaShortInfo = "You were assigned the task t...";

        private const string InsigniaLongInfo =
            "You were assigned the task to bring back something indicating the Divine presence of Aban in Nascence.<BR><BR>Bring an Insignia of Aban to Ecclesiast Aban Fala.";

        private const string DeviceInfoShortInfo = "You agreed to find informati...";

        private const string DeviceInfoLongInfo =
            "You agreed to find information about the ancient looking device given to you by Donna Red.<BR><BR>Ecclesiast Aban Fala may have some information, but you must prove your dedication to Aban by bringing the Ecclesiast proof of Abans existence before he helps you. <BR><BR><font color=\"#D60000\">Note: Proof of existance is often found in the form of so called 'Insignias'</font>";

        private const string GardenLongInfo =
            "You agreed to find information about the ancient looking device given to you by Donna Red.<BR><BR>Ecclesiast Aban Fala told you to continue your journey in the Garden of Aban.<BR>A 'Sipius', who should be well versed in the past, may be able to tell you more about the Ancient Device. <BR><BR><font color=\"#D60000\">Note: Use the Insignia on the Statue of Aban to enter his Garden.</font>";

        private const string SoulsShortInfo = "Release the power of Ancient...";

        private const string SoulsLongInfo =
            "Release the power of Ancient Xan technology.<BR><BR>Save three souls then return the Ancient Pattern Analyzer to Sipius Aban Lux-Wei.<BR><BR>You have saved 0 souls.<BR><BR>1. Use the Insignia of Aban on the Ancient Pattern Analyzer (Pick the Insignia up, and Shift+right-click on the Ancient Pattern Analyzer. You will then trigger a tradeskill process to make something new.)<BR>2. Apply the Ancient Pattern Analyzer favored by the Faithful on a creature. According to Sipius Aban Lux-Wei this will trigger its ancient abilities.<BR><BR>The Device looks to fit a Dreaming Silvertail's face or maybe covering its eyes. <BR>(Right-click a creature to attempt to place the device on it.)";

        private const string CapturedAction59DeleteHex =
            "0135000A0001003700000DB6765A690A5E4777700000C350765A690A000000003B000000000000DAC35556893A0000DAC35556893A0000";

        private const string CapturedQuestDeleteHex =
            "0136000A0001003500000DB6765A690A212C487A0000C350765A690A0000000001000000000000DAC35556893A0000000000000000";

        internal static bool TrySendInsigniaTaskQuestFullUpdate(ICharacter character)
        {
            return TrySend(character, BuildInsigniaTaskQuest(character.Identity));
        }

        internal static bool TrySendDeviceInfoQuestFullUpdate(ICharacter character)
        {
            return TrySend(character, BuildDeviceInfoQuest(character.Identity));
        }

        internal static bool TrySendGardenQuestFullUpdate(ICharacter character)
        {
            return TrySend(character, BuildGardenQuest(character.Identity));
        }

        internal static bool TrySendSoulsQuestFullUpdate(ICharacter character, int soulsSaved = 0)
        {
            if (soulsSaved >= 3)
            {
                return TrySendSoulsReturnQuestFullUpdate(character);
            }

            return TrySend(character, BuildSoulsQuest(character.Identity, soulsSaved));
        }

        internal static bool TrySendSoulsReturnQuestFullUpdate(ICharacter character)
        {
            return TrySend(character, BuildSoulsReturnQuest(character.Identity));
        }

        internal static bool TrySendQuestDelete(ICharacter character, string questId)
        {
            int instance;
            if (!TryResolveMissionInstance(questId, out instance) || !CanSend(character))
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;

                byte[] action59 = HexToBytes(CapturedAction59DeleteHex);
                ReplaceInstance(action59, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(action59, MissionInsigniaInstance, instance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(questDelete, MissionInsigniaInstance, instance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);

                client.SendCompressed(
                    new QuestMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = QuestAction.Delete,
                        Unknown1 = 0,
                        Mission = RawIdentity(MissionIdentityType, instance),
                        Unknown2 = 0,
                        Unknown3 = 0
                    });

                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_ABAN_FALA QuestDelete failed: " + exception.Message);
                return false;
            }
        }

        private static QuestFullUpdateMessage BuildInsigniaTaskQuest(Identity characterIdentity)
        {
            Identity fala = RawIdentity((int)IdentityType.CanbeAffected, NascenceAbanFalaInteractionRules.FalaInstance);
            Identity mission = RawIdentity(MissionIdentityType, MissionInsigniaInstance);

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = mission,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = InsigniaShortInfo,
                        LongInfo = InsigniaLongInfo,
                        UnknownId1 = fala,
                        Unknown5 = 6,
                        Unknown6 = 0,
                        Unknown7 = 0,
                        Unknown8 = 0,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new MissionItemReward[0],
                        Unknown11 = 1380276564,
                        Unknown12 = 0,
                        Unknown13 = 0,
                        UnknownHash1 = string.Empty,
                        Unknown14 = 0,
                        Unknown15 = 0,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 158429,
                        Unknown20 = MissionDurationSeconds,
                        Unknown21 = MissionDurationSeconds,
                        QuestActions = new[]
                        {
                            BuildVillageMarkerAction(MobHashIsoa, MobHashEcab, 0x4D882550, 92808528)
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 92808528 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 8,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 100044,
                        UnknownId3 = Identity.None,
                        Unknown25 = 0,
                        Unknown26 = 0,
                        QuestIdentities = new QuestIdentity[0],
                        Unknown27 = 7,
                        FactionInfos = new Identity[0],
                        Unknown28 = 1
                    }
                }
            };
        }

        private static QuestFullUpdateMessage BuildDeviceInfoQuest(Identity characterIdentity)
        {
            Identity donna = RawIdentity((int)IdentityType.CanbeAffected, DonnaInstance);
            Identity mission = RawIdentity(MissionIdentityType, MissionDeviceInfoInstance);

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = mission,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = DeviceInfoShortInfo,
                        LongInfo = DeviceInfoLongInfo,
                        UnknownId1 = donna,
                        Unknown5 = 6,
                        Unknown6 = 0,
                        Unknown7 = 0,
                        Unknown8 = 0,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new MissionItemReward[0],
                        Unknown11 = 1295533397,
                        Unknown12 = 0,
                        Unknown13 = 0,
                        UnknownHash1 = string.Empty,
                        Unknown14 = 0,
                        Unknown15 = 0,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 158429,
                        Unknown20 = MissionDurationSeconds,
                        Unknown21 = MissionDurationSeconds,
                        QuestActions = new[]
                        {
                            BuildVillageMarkerAction(MobHashIsoa, MobHashEcab, 0x4D882551, 92808529)
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 92808529 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 8,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 100142,
                        UnknownId3 = Identity.None,
                        Unknown25 = 0,
                        Unknown26 = 0,
                        QuestIdentities = new QuestIdentity[0],
                        Unknown27 = 7,
                        FactionInfos = new Identity[0],
                        Unknown28 = 1
                    }
                }
            };
        }

        private static QuestFullUpdateMessage BuildGardenQuest(Identity characterIdentity)
        {
            Identity donna = RawIdentity((int)IdentityType.CanbeAffected, DonnaInstance);
            Identity mission = RawIdentity(MissionIdentityType, MissionGardenInstance);

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = mission,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = DeviceInfoShortInfo,
                        LongInfo = GardenLongInfo,
                        UnknownId1 = donna,
                        Unknown5 = 6,
                        Unknown6 = 0,
                        Unknown7 = 0,
                        Unknown8 = 0,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new MissionItemReward[0],
                        Unknown11 = 1213484360,
                        Unknown12 = 0,
                        Unknown13 = 0,
                        UnknownHash1 = string.Empty,
                        Unknown14 = 0,
                        Unknown15 = 0,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 244818,
                        Unknown20 = MissionDurationSeconds,
                        Unknown21 = MissionDurationSeconds,
                        QuestActions = new[]
                        {
                            BuildGardenMarkerAction(0x4D882552, 92808530)
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 92808530 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 8,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 100184,
                        UnknownId3 = Identity.None,
                        Unknown25 = 0,
                        Unknown26 = 0,
                        QuestIdentities = new QuestIdentity[0],
                        Unknown27 = 7,
                        FactionInfos = new Identity[0],
                        Unknown28 = 1
                    }
                }
            };
        }

        private static QuestFullUpdateMessage BuildSoulsQuest(Identity characterIdentity, int soulsSaved)
        {
            Identity luxWei = RawIdentity((int)IdentityType.CanbeAffected, LuxWeiInstance);
            Identity mission = RawIdentity(MissionIdentityType, MissionSoulsInstance);
            string longInfo = BuildSoulsLongInfo(soulsSaved);

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = mission,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = SoulsShortInfo,
                        LongInfo = longInfo,
                        UnknownId1 = luxWei,
                        Unknown5 = 6,
                        Unknown6 = 0,
                        Unknown7 = 0,
                        Unknown8 = 0,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new MissionItemReward[0],
                        Unknown11 = 1131701847,
                        Unknown12 = 0,
                        Unknown13 = 0,
                        UnknownHash1 = string.Empty,
                        Unknown14 = 0,
                        Unknown15 = 0,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 158429,
                        Unknown20 = MissionDurationSeconds,
                        Unknown21 = MissionDurationSeconds,
                        QuestActions = new[]
                        {
                            BuildGardenMarkerAction(0x4D882553, 92808531)
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 92808531 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 8,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 100226,
                        UnknownId3 = Identity.None,
                        Unknown25 = 0,
                        Unknown26 = 0,
                        QuestIdentities = new QuestIdentity[0],
                        Unknown27 = 7,
                        FactionInfos = new Identity[0],
                        Unknown28 = 1
                    }
                }
            };
        }

        private static string BuildSoulsLongInfo(int soulsSaved)
        {
            if (soulsSaved < 0)
            {
                soulsSaved = 0;
            }

            if (soulsSaved > 3)
            {
                soulsSaved = 3;
            }

            return "Release the power of Ancient Xan technology.<BR><BR>Save three souls then return the Ancient Pattern Analyzer to Sipius Aban Lux-Wei.<BR><BR>You have saved "
                   + soulsSaved.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + " souls.<BR><BR>1. Use the Insignia of Aban on the Ancient Pattern Analyzer (Pick the Insignia up, and Shift+right-click on the Ancient Pattern Analyzer. You will then trigger a tradeskill process to make something new.)<BR>2. Apply the Ancient Pattern Analyzer favored by the Faithful on a creature. According to Sipius Aban Lux-Wei this will trigger its ancient abilities.<BR><BR>The Device looks to fit a Dreaming Silvertail's face or maybe covering its eyes. <BR>(Right-click a creature to attempt to place the device on it.)";
        }

        private static QuestFullUpdateMessage BuildSoulsReturnQuest(Identity characterIdentity)
        {
            Identity luxWei = RawIdentity((int)IdentityType.CanbeAffected, LuxWeiInstance);
            Identity mission = RawIdentity(MissionIdentityType, MissionSoulsReturnInstance);
            const string returnLongInfo =
                "Release the power of Ancient Xan technology.<BR><BR>Save three souls then return the Ancient Pattern Analyzer to Sipius Aban Lux-Wei.<BR><BR>You have saved 3 souls.<BR><BR>Now return the Ancient Pattern Analyzer to Sipius Aban Lux-Wei.";

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = mission,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = SoulsShortInfo,
                        LongInfo = returnLongInfo,
                        UnknownId1 = luxWei,
                        Unknown5 = 6,
                        Unknown6 = 1480,
                        Unknown7 = 0,
                        Unknown8 = 2323,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new[]
                        {
                            new MissionItemReward
                            {
                                LowId = NascenceAbanFalaInteractionRules.GardenKeyItemId,
                                HighId = NascenceAbanFalaInteractionRules.GardenKeyItemId,
                                Ql = 1,
                                Unknown = 0
                            },
                            new MissionItemReward
                            {
                                LowId = 295118,
                                HighId = 295118,
                                Ql = 1,
                                Unknown = 0
                            }
                        },
                        Unknown11 = 1414284110,
                        Unknown12 = 0,
                        Unknown13 = 12,
                        UnknownHash1 = string.Empty,
                        Unknown14 = 0,
                        Unknown15 = 911234897,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 158429,
                        Unknown20 = MissionDurationSeconds,
                        Unknown21 = MissionDurationSeconds,
                        QuestActions = new[]
                        {
                            BuildGardenMarkerAction(0x4D88C0D2, 92848338)
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 92848338 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 8,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 100137,
                        UnknownId3 = Identity.None,
                        Unknown25 = 0,
                        Unknown26 = 0,
                        QuestIdentities = new QuestIdentity[0],
                        Unknown27 = 7,
                        FactionInfos = new[]
                        {
                            RawIdentity(570, 0x1A9),
                            RawIdentity(571, 0x1A9),
                            RawIdentity(572, 0x1A9)
                        },
                        Unknown28 = 1
                    }
                }
            };
        }

        private static QuestActionInfo BuildVillageMarkerAction(
            int actionHash,
            int targetHash,
            int unknownId7Instance,
            int unknownArrayValue)
        {
            return new QuestActionInfo
            {
                Version = 6,
                Action = RawIdentity(actionHash, actionHash),
                UnknownId1 = Identity.None,
                UnknownId2 = RawIdentity(targetHash, targetHash),
                UnknownId3 = Identity.None,
                UnknownId4 = Identity.None,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0,
                Unknown4 = 0,
                UnknownId5 = Identity.None,
                Unknown5 = 0,
                Unknown6 = 0,
                Unknown7 = 0,
                Unknown8 = 0,
                UnknownId6 = Identity.None,
                UnknownHash1 = string.Empty,
                Unknown9 = 0,
                UnknownId7 = RawIdentity(54001, unknownId7Instance),
                PlayfieldId = new Identity
                              {
                                  Type = IdentityType.Playfield2,
                                  Instance = NascenceAbanFalaInteractionRules.RedeemedVillagePlayfieldId
                              },
                Unknown10 = 100000,
                Unknown11 = 100000,
                Position = new Vector3(1893, 69, 691)
            };
        }

        private static QuestActionInfo BuildGardenMarkerAction(int unknownId7Instance, int unknownArrayValue)
        {
            return new QuestActionInfo
            {
                Version = 24,
                Action = Identity.None,
                UnknownId1 = Identity.None,
                UnknownId2 = RawIdentity(MobHashGarden, MobHashGarden),
                UnknownId3 = Identity.None,
                UnknownId4 = Identity.None,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0,
                Unknown4 = 0,
                UnknownId5 = Identity.None,
                Unknown5 = 0,
                Unknown6 = 0,
                Unknown7 = 0,
                Unknown8 = 0,
                UnknownId6 = Identity.None,
                UnknownHash1 = string.Empty,
                Unknown9 = 0,
                UnknownId7 = RawIdentity(54001, unknownId7Instance),
                PlayfieldId = new Identity
                              {
                                  Type = IdentityType.Playfield2,
                                  Instance = NascenceAbanFalaInteractionRules.GardenPlayfieldId
                              },
                Unknown10 = 100000,
                Unknown11 = 100000,
                Position = new Vector3(468, 117, 495)
            };
        }

        private static bool TrySend(ICharacter character, QuestFullUpdateMessage message)
        {
            if (!CanSend(character) || message == null)
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;
                ReanchorGameTime(character, client);
                client.SendCompressed(message);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_ABAN_FALA QuestFullUpdate failed: " + exception.Message);
                return false;
            }
        }

        private static void ReanchorGameTime(ICharacter character, ZoneClient client)
        {
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

        private static bool TryResolveMissionInstance(string questId, out int instance)
        {
            instance = 0;
            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            string normalized = questId.Trim();
            int colon = normalized.LastIndexOf(':');
            string hex = colon >= 0 ? normalized.Substring(colon + 1) : normalized;
            return int.TryParse(
                hex,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out instance);
        }

        private static Identity RawIdentity(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }

        private static bool CanSend(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client != null
                   && character.Controller.Client is ZoneClient;
        }

        private static void ReplaceInstance(byte[] buffer, int fromInstance, int toInstance)
        {
            if (buffer == null || fromInstance == toInstance)
            {
                return;
            }

            for (int i = 0; i + 3 < buffer.Length; i++)
            {
                int value = (buffer[i] << 24) | (buffer[i + 1] << 16) | (buffer[i + 2] << 8) | buffer[i + 3];
                if (value != fromInstance)
                {
                    continue;
                }

                buffer[i] = (byte)((toInstance >> 24) & 0xFF);
                buffer[i + 1] = (byte)((toInstance >> 16) & 0xFF);
                buffer[i + 2] = (byte)((toInstance >> 8) & 0xFF);
                buffer[i + 3] = (byte)(toInstance & 0xFF);
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
