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

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int B18CUnknownActionIdType = 0x00001999;

        private const int B18CUnknownActionIdInstance = 0x4D4C4345;

        private const int B18CUnknownActionId7Type = 0x0000D2FC;

        private const int B18CUnknownActionId7Instance = 0x1C50D8CE;

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

        private static Identity IdentityFromRaw(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }
    }
}
