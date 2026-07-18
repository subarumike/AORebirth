namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    internal static class WindcallerKarrecPacketSender
    {
        internal const int MissionInstance = unchecked((int)0x55579381);

        private const int MissionIdentityType = 56003;
        private const int KarrecInstance = unchecked((int)0x796360BB);

        private const string ShortInfo = "The Windcaller's requests";

        private const string LongInfo =
            "The Windcaller's requests<BR><BR>"
            + "Windcaller Karrec told you to get him a hamburger from an annoying individual. "
            + "He also told you to get a woman named Maddy Cardile to donate money to his temple.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Give Windcaller Karrec a Bronto Burger and Maddy's Credit Card.</font>";

        internal static bool TrySendQuestFullUpdate(ICharacter character, Identity karrecIdentity)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                character.Controller.Client.SendCompressed(
                    CreateQuestFullUpdate(character.Identity, ResolveKarrecIdentity(karrecIdentity)));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SUBWAY_KARREC QuestFullUpdate send failed: " + exception.Message);
                return false;
            }
        }

        internal static bool TrySendCompletionAndDelete(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                character.Controller.Client.SendCompressed(CreateAction59(character.Identity));
                character.Controller.Client.SendCompressed(CreateQuestDelete(character.Identity));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SUBWAY_KARREC completion packet send failed: " + exception.Message);
                return false;
            }
        }

        internal static bool TrySendPersonalResearchFeedback(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                character.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 1107296284,
                        FormattedMessage = "~&!!!\":!)90Fi!!![g~",
                        Unknown2 = 0
                    });
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SUBWAY_KARREC_QUEST personal research feedback failed: " + exception.Message);
                return false;
            }
        }

        internal static bool TrySendSideTokenProjection(ICharacter character, long sideTokenValue)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                character.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = "Side tokens collected: " + sideTokenValue + ".",
                        Unknown2 = 0
                    });
                FeedbackMessageHandler.Default.Send(character, 110, 108871108);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SUBWAY_KARREC_QUEST side token projection failed: " + exception.Message);
                return false;
            }
        }

        internal static QuestFullUpdateMessage CreateQuestFullUpdate(
            Identity characterIdentity,
            Identity karrecIdentity)
        {
            Identity missionIdentity = RawIdentity(MissionIdentityType, MissionInstance);

            return new QuestFullUpdateMessage
            {
                Identity = characterIdentity,
                Unknown = 1,
                Quests = new[]
                {
                    new Quest
                    {
                        QuestId = missionIdentity,
                        Unknown1 = 15,
                        Unknown2 = 0,
                        Unknown3 = 0,
                        Unknown4 = 2,
                        ShortInfo = ShortInfo,
                        LongInfo = LongInfo,
                        UnknownId1 = karrecIdentity,
                        Unknown5 = 6,
                        Unknown6 = 0,
                        Unknown7 = 0,
                        Unknown8 = 0,
                        Unknown9 = 1009,
                        Unknown10 = 1009,
                        MissionItemData = new[]
                        {
                            new MissionItemReward
                            {
                                LowId = 285612,
                                HighId = 285612,
                                Ql = 1,
                                Unknown = 0
                            }
                        },
                        Unknown11 = 1110716998,
                        Unknown12 = 0,
                        Unknown13 = 0,
                        UnknownHash1 = "00000000",
                        Unknown14 = 0,
                        Unknown15 = 0,
                        Unknown16 = 0,
                        Unknown17 = 0,
                        Unknown18 = 0,
                        UnknownId2 = characterIdentity,
                        MissionIconId = 244818,
                        Unknown20 = 60,
                        Unknown21 = 60,
                        QuestActions = new[]
                        {
                            new QuestActionInfo
                            {
                                Version = 24,
                                Action = Identity.None,
                                UnknownId1 = Identity.None,
                                UnknownId2 = RawIdentity(70099, 105201),
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
                                UnknownHash1 = "6A5B02D9",
                                Unknown9 = 0,
                                UnknownId7 = RawIdentity(54001, 1297226293),
                                PlayfieldId = Identity.None,
                                Unknown10 = 0,
                                Unknown11 = 0,
                                Position = new Vector3(0, 0, 0)
                            }
                        },
                        PlayerIds = new[] { characterIdentity },
                        UnknownArray1 = new[] { 89266741 },
                        UnknownArray2 = new int[0],
                        CharacterInfos = new CharacterInfo[0],
                        Unknown22 = 6,
                        PlayerIds2 = new[] { characterIdentity },
                        Unknown23 = 0,
                        Unknown24 = 105201,
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

        private static Identity ResolveKarrecIdentity(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected && identity.Instance != 0
                       ? identity
                       : new Identity
                         {
                             Type = IdentityType.CanbeAffected,
                             Instance = KarrecInstance
                         };
        }

        internal static CharacterActionMessage CreateAction59(Identity characterIdentity)
        {
            return new CharacterActionMessage
            {
                Identity = characterIdentity,
                Unknown = 0,
                Action = (CharacterActionType)59,
                Unknown1 = 0,
                Target = RawIdentity(MissionIdentityType, MissionInstance),
                Parameter1 = MissionIdentityType,
                Parameter2 = MissionInstance,
                Unknown2 = 0
            };
        }

        internal static QuestMessage CreateQuestDelete(Identity characterIdentity)
        {
            return new QuestMessage
            {
                Identity = characterIdentity,
                Unknown = 0,
                Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                Unknown1 = 0,
                Mission = RawIdentity(MissionIdentityType, MissionInstance),
                Unknown2 = 0,
                Unknown3 = 0
            };
        }

        private static bool CanSend(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client != null
                   && character.Identity.Type == IdentityType.CanbeAffected
                   && character.Identity.Instance > 0;
        }

        private static Identity RawIdentity(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }
    }
}
