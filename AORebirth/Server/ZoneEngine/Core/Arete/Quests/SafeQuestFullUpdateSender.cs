namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    public static class SafeQuestFullUpdateSender
    {
        private const int MissionIdentityType = 0x0000DAC3;

        private const int B18CInstance = unchecked((int)0x5514B18C);

        private const int B18DInstance = unchecked((int)0x5514B18D);

        private const int B18EInstance = unchecked((int)0x5514B18E);

        private const int B18FInstance = unchecked((int)0x5514B18F);

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int B18CUnknownActionIdType = 0x00001999;

        private const int B18CUnknownActionIdInstance = 0x4D4C4345;

        private const int B18CUnknownActionId7Type = 0x0000D2FC;

        private const int B18CUnknownActionId7Instance = 0x1C50D8CE;

        private const int B18DUnknownActionId2Type = 0x000111D3;

        private const int B18DUnknownActionId2Instance = 0x00019A8F;

        private const int B18DUnknownActionId7Type = 0x0000D2F1;

        private const int B18DUnknownActionId7Instance = 0x4D167F39;

        private const int B18EUnknownActionId2Type = 0x000111D3;

        private const int B18EUnknownActionId2Instance = 0x52454C53;

        private const int B18EUnknownActionId7Type = 0x0000D2F1;

        private const int B18EUnknownActionId7Instance = 0x4D167F3A;

        private const int B18FUnknownActionId2Type = 0x000111D3;

        private const int B18FUnknownActionId2Instance = 0x00019A50;

        private const int B18FUnknownActionId7Type = 0x0000D2F1;

        private const int B18FUnknownActionId7Instance = 0x4D167F3B;

        private const string B18CShortInfo = "Terminate 5 Malfunctioning C...";

        private const string B18CLongInfo =
            "Terminate 5 Malfunctioning Cleaning Robots<BR><br>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<br><BR>"
            + "Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, "
            + "he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then "
            + "open the package with brand new cleaning robots and set them to work.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Kill 5 Malfunctining Cleaning Robots.</font>";

        private const string B18DShortInfo = "Open the Cargo Box";

        private const string B18DLongInfo =
            "Open the Cargo Box<BR><BR>"
            + "Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, "
            + "he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then "
            + "open the Cargo Box with brand new cleaning robots and set them to work.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Use (Right Click) the Cargo Box to open it.</font>";

        private const string B18EShortInfo = "Return to Rex Larsson";

        private const string B18ELongInfo =
            "Return to Rex Larsson<BR><BR>"
            + "Return to Rex Larsson to inform him of the great cleaning success.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Rex Larsson.</font>";

        private const string B18FShortInfo = "Talk to Marcus Stone";

        private const string B18FLongInfo =
            "Talk to Marcus Stone<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Rex Larsson told you to spreak with Marcus Stone, an overseer for arriving cargo in the area, "
            + "might be able to aid in getting your license issue settled.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Marcus Stone.</font>";

        public static RexQuestPreviewEmissionResult TrySendB18CPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18CPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18C QuestFullUpdate DTO preview sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18C rawReplay=false noPersistence=true noRewards=true "
                    + "noQuestDelete=true noCompletion=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18C QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18C "
                    + "rawReplay=false noPersistence=true noRewards=true noInventory=true noXpCredits=true "
                    + "noQuestDelete=true noCompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static bool TrySendB18CCompletionHandoff(ICharacter source)
        {
            if (source == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source character missing.");
                return false;
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source client missing.");
                return false;
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source identity is invalid.");
                return false;
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB18CAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18CQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18DPreviewMessage(source.Identity));

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18C completion handoff sent character="
                    + source.Identity.ToString(true)
                    + " action59=Mission:5514B18C questDelete=Mission:5514B18C "
                    + "nextQuestFullUpdate=Mission:5514B18D capture=20260614-194454/events.log:5919-5926 "
                    + "packetHandoffOnly=true noRewards=true noInventory=true noXpCredits=true "
                    + "noDbWrites=true noPersistence=true noCargoBox=true");
                return true;
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff failed: " + e.Message);
                return false;
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18DQuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18D Quest Delete DTO cleanup sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18D source=20260614-194454/packets.hex.log:5765 "
                    + "rawReplay=false noAction59=true b18dWindowCleanupOnly=true noCompletionSemantics=true "
                    + "noPersistence=true noRewards=true noInventory=true noXpCredits=true noB18ECompletion=true");

                source.Controller.Client.SendCompressed(CreateB18DQuestDeleteMessage(source.Identity));

                return RexQuestPreviewEmissionResult.Sent(
                    "B18D Quest Delete sent using DTO serializer. mission=Mission:5514B18D "
                    + "source=20260614-194454/packets.hex.log:5765 rawReplay=false noAction59=true "
                    + "b18dWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true "
                    + "noInventory=true noXpCredits=true noB18ECompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18D Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18D Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18EPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18EPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18E QuestFullUpdate DTO preview sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5767 "
                    + "rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noRewards=true "
                    + "noInventory=true noXpCredits=true noCompletion=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18E QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18E "
                    + "source=20260614-194454/packets.hex.log:5767 rawReplay=false noAction59=true "
                    + "noQuestDelete=true noPersistence=true noRewards=true noInventory=true noXpCredits=true "
                    + "noCompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18E QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18EQuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18E Quest Delete DTO cleanup sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5947 "
                    + "rawReplay=false noAction59=true b18eWindowCleanupOnly=true noCompletionSemantics=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true noDbWrites=true");

                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));

                return RexQuestPreviewEmissionResult.Sent(
                    "B18E Quest Delete sent using DTO serializer. mission=Mission:5514B18E "
                    + "source=20260614-194454/packets.hex.log:5947 rawReplay=false noAction59=true "
                    + "b18eWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noCredits=true "
                    + "noItems=true noInventory=true noDbWrites=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18E Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18FPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18FPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18F QuestFullUpdate DTO handoff sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18F source=20260614-194454/packets.hex.log:5949 "
                    + "nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true noMarcusStoneImplementation=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18F QuestFullUpdate sent using DTO serializer. mission=Mission:5514B18F "
                    + "source=20260614-194454/packets.hex.log:5949 title=\"Talk to Marcus Stone\" "
                    + "nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true "
                    + "noMarcusStoneImplementation=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18F QuestFullUpdate DTO handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate failed during DTO serialization/send: " + e.Message);
            }
        }

        internal static QuestFullUpdateMessage CreateB18CPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18CInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18CShortInfo,
                                   LongInfo = B18CLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1112496696,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 11330,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 20,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = Identity.None,
                                               UnknownId3 = IdentityFromRaw(
                                                   B18CUnknownActionIdType,
                                                   B18CUnknownActionIdInstance),
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
                                               UnknownId7 = IdentityFromRaw(
                                                   B18CUnknownActionId7Type,
                                                   B18CUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3614, 0, 779)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 72407246 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 5,
                                   Unknown24 = 105102,
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

        internal static QuestFullUpdateMessage CreateB18DPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18DInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18DShortInfo,
                                   LongInfo = B18DLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1145587534,
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
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18DUnknownActionId2Type,
                                                   B18DUnknownActionId2Instance),
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
                                               UnknownId7 = IdentityFromRaw(
                                                   B18DUnknownActionId7Type,
                                                   B18DUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3621, 0, 782)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360441 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105103,
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

        internal static QuestFullUpdateMessage CreateB18EPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18EInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18EShortInfo,
                                   LongInfo = B18ELongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1040,
                                   Unknown7 = 0,
                                   Unknown8 = 1281,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 861490233,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = "5UFZ",
                                   Unknown14 = 1,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 23,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18EUnknownActionId2Type,
                                                   B18EUnknownActionId2Instance),
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
                                               UnknownId7 = IdentityFromRaw(
                                                   B18EUnknownActionId7Type,
                                                   B18EUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3621, 0, 790)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360442 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105104,
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

        internal static QuestFullUpdateMessage CreateB18FPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18FInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18FShortInfo,
                                   LongInfo = B18FLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1212436295,
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
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18FUnknownActionId2Type,
                                                   B18FUnknownActionId2Instance),
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
                                               UnknownId7 = IdentityFromRaw(
                                                   B18FUnknownActionId7Type,
                                                   B18FUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3638, 0, 830)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360443 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
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

        internal static CharacterActionMessage CreateB18CAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B18CInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B18CInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB18CQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18CInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestMessage CreateB18DQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18DInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestMessage CreateB18EQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18EInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        private static Identity IdentityFromRaw(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }
    }
}
