namespace ZoneEngine.Core.Arete.Quests
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    // Exact captured journal builders shared with the replacement runtime. No world or client adapter.
    public static partial class SafeQuestFullUpdateSender
    {
        private const int MissionIdentityType = 0x0000DAC3;

        private const int TalkToStanInstance = unchecked((int)0x555B4366);

        private const int BuyLockpickInstance = unchecked((int)0x555BD124);

        private const int StrongboxContentsInstance = unchecked((int)0x555BE9C5);

        private const int DeliverAntonioFactoryInstance = unchecked((int)0x555BE9F2);

        private const int TalkToSarahGreeneInstance = unchecked((int)0x555BE9F3);

        private const int BuyNanoProgramsInstance = unchecked((int)0x555BE9F4);

        private const string TalkToStanShortInfo = "Talk to Stan Goodman";

        private const string TalkToStanLongInfo =
            "Talk to Stan Goodman<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission "
            + "is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Alex told you to go talk to Stan Goodman, a local 'purveyer of recently used merchandise'. He should "
            + "be able to help with aquiring more parts for your ID card.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Stan Goodman.</font>";

        private const string BuyLockpickShortInfo = "Buy a Lockpick";

        private const string BuyLockpickLongInfo =
            "Buy a Lockpick<BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetected, but in order "
            + "to do so you need to buy a <a href='itemref://95577/95577/1'>Lock Pick</a>.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Find the <a href='itemref://297290/297290/3'>ICC Tech Supplies</a> vending machine and buy a "
            + "<a href='itemref://95577/95577/1'>Lock Pick</a>.</font>";

        private const string StrongboxContentsShortInfo = "Take the contents of the Str...";

        private const string StrongboxContentsLongInfo =
            "Take the contents of the Strongbox <BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetetected. "
            + "Now that you have bought a <a href='itemref://95577/95577/1'>Lock Pick</a>, this should be an easy task."
            + "<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Pick up (Left Click) your <a href='itemref://95577/95577/1'>Lock Pick</a> from your inventory and "
            + "drop it (Left Click) on the <a href='itemref://295604/295604/1'>Merchant's Strongbox</a>.</font>";

        private const string DeliverAntonioFactoryShortInfo = "Deliver Antonio's Adaptatio...";

        private const string DeliverAntonioFactoryLongInfo =
            "Deliver Antonio's Adaptation Factory to Stan Goodman.<BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetetected. "
            + "Now that you have found <a href='itemref://248306/248306/1'>Antonio's Adaptation Factory</a>, "
            + "bring it back to Stan.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Bring <a href='itemref://248306/248306/1'>Antonio's Adaptation Factory</a> to Stan Goodman.</font>";

        private const string TalkToSarahGreeneShortInfo = "Talk to Sarah Greene";

        private const string TalkToSarahGreeneLongInfo =
            "Talk to Sarah Greene<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Stab told you that Sarah Greene, a local armorsmith, should be able to help you with aquiring more "
            + "parts needed for your ID card.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Sarah Greene.</font>";

        private const string BuyNanoProgramsShortInfo = "Buy some Nano Programs";

        private const string BuyNanoProgramsLongInfo =
            "Buy some Nano Programs<BR><BR>"
            + "Stanley Goodman told you to go talk to Marco Spida to buy a Nanoprogram Container.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective: Talk to Marco Spida and buy a Nanoprogram Container "
            + "for your profession. Open the Container to complete your mission.</font>";

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        internal static QuestFullUpdateMessage CreateTalkToStanPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, TalkToStanInstance);
            // Capture 20260720-171317 Talk-to-Stan QFU UnknownId1 = SimpleChar:78E0FC63.
            Identity stanIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC63)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

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
                                   ShortInfo = TalkToStanShortInfo,
                                   LongInfo = TalkToStanLongInfo,
                                   UnknownId1 = stanIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
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
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
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
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateBuyLockpickPreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                BuyLockpickInstance,
                BuyLockpickShortInfo,
                BuyLockpickLongInfo);
        }

        internal static QuestFullUpdateMessage CreateStrongboxContentsPreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                StrongboxContentsInstance,
                StrongboxContentsShortInfo,
                StrongboxContentsLongInfo);
        }

        internal static QuestFullUpdateMessage CreateDeliverAntonioFactoryPreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                DeliverAntonioFactoryInstance,
                DeliverAntonioFactoryShortInfo,
                DeliverAntonioFactoryLongInfo);
        }

        internal static QuestFullUpdateMessage CreateTalkToSarahGreenePreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                TalkToSarahGreeneInstance,
                TalkToSarahGreeneShortInfo,
                TalkToSarahGreeneLongInfo);
        }

        internal static QuestFullUpdateMessage CreateBuyNanoProgramsPreviewMessage(Identity characterIdentity)
        {
            // Capture 20260721-afgter dog lockpick goodman: UnknownId1 = CanbeAffected:78E0FC65 (Stan).
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, BuyNanoProgramsInstance);
            Identity tipNpcIdentity = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = unchecked((int)0x78E0FC65)
                                       };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

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
                                   ShortInfo = BuyNanoProgramsShortInfo,
                                   LongInfo = BuyNanoProgramsLongInfo,
                                   UnknownId1 = tipNpcIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
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
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
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
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        private static QuestFullUpdateMessage CreateStanChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                missionInstance,
                shortInfo,
                longInfo,
                244818,
                unchecked((int)0x78E0FC63));
        }

        private static QuestFullUpdateMessage CreateSarahChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo,
            int missionIconId,
            int tipNpcInstance)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                missionInstance,
                shortInfo,
                longInfo,
                missionIconId,
                tipNpcInstance,
                unknown6: 0,
                unknown8: 0);
        }

        private static QuestFullUpdateMessage CreateSarahChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo,
            int missionIconId,
            int tipNpcInstance,
            int unknown6,
            int unknown8)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, missionInstance);
            Identity tipNpcIdentity = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = tipNpcInstance
                                       };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

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
                                   ShortInfo = shortInfo,
                                   LongInfo = longInfo,
                                   UnknownId1 = tipNpcIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = unknown6,
                                   Unknown7 = 0,
                                   Unknown8 = unknown8,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = missionIconId,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
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
                                   Unknown27 = 0,
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
