namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    internal static class CapturedSubwayHandbagMissionRuntime
    {
        internal const int MontroyalPlayfieldInstance = 655;
        internal const int SubwayPlayfieldInstance = 127;
        internal const int NataliaMonsterData = 26076;
        internal const int HandbagItemId = 297055;

        private const int MissionIdentityType = 56003;
        private const int MissionInstance = 1431120071;
        private const int DailyMissionXpRewardItemId = 285612;
        private static readonly bool EmitCapturedQuestUiPackets = false;
        private const string MissionShortInfo = "The stolen handbag";
        private const string MissionLongInfo =
            "The stolen handbag<BR><BR>Natalia Akcora has her handbag stolen, the thief ran in to the Subway entrance. Find him and give Natalia back her purse.<BR>";
        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Natalia Akcora into one of the slots available and press \"accept\"";

        private static readonly object Sync = new object();
        private static readonly Dictionary<int, HandbagMissionSession> Sessions =
            new Dictionary<int, HandbagMissionSession>();

        internal static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
        {
            if (!IsNatalia(npc))
            {
                return false;
            }

            ICharacter source = Pool.Instance.GetObject<ICharacter>(npc.Playfield.Identity, sourceIdentity);
            if (source == null)
            {
                return false;
            }

            HandbagMissionSession session = GetSession(source.Identity.Instance);
            session.NataliaIdentity = npc.Identity;
            KnuBotOpenChatWindowMessageHandler.Default.Send(source, npc.Identity);

            if (session.State == HandbagMissionState.Active
                && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, HandbagItemId))
            {
                session.Dialogue = HandbagDialogueStage.TurnInChoice;
                KnuBotAnswerListMessageHandler.Default.Send(
                    source,
                    npc.Identity,
                    new[] { "I found your handbag.", "Goodbye" });
                return true;
            }

            if (session.State == HandbagMissionState.None)
            {
                session.Dialogue = HandbagDialogueStage.OfferChoice;
                KnuBotAnswerListMessageHandler.Default.Send(
                    source,
                    npc.Identity,
                    new[] { "What do you want me to help you with?", "Goodbye" });
                return true;
            }

            session.Dialogue = HandbagDialogueStage.Goodbye;
            KnuBotAnswerListMessageHandler.Default.Send(source, npc.Identity, new[] { "Goodbye" });
            return true;
        }

        internal static bool TryHandleAnswer(ICharacter source, Identity target, int answer)
        {
            HandbagMissionSession session;
            if (!TryGetSession(source, target, out session))
            {
                return false;
            }

            if (answer != 0)
            {
                CloseDialogue(source, session);
                return true;
            }

            switch (session.Dialogue)
            {
                case HandbagDialogueStage.OfferChoice:
                    session.Dialogue = HandbagDialogueStage.AcceptChoice;
                    KnuBotAnswerListMessageHandler.Default.Send(
                        source,
                        target,
                        new[] { "I'll help you out.", "Goodbye" });
                    return true;

                case HandbagDialogueStage.AcceptChoice:
                    session.State = HandbagMissionState.Active;
                    session.Dialogue = HandbagDialogueStage.Goodbye;
                    if (EmitCapturedQuestUiPackets)
                    {
                        source.Controller.Client.SendCompressed(
                            CreateQuestFullUpdateMessage(source.Identity, target, MissionEmission.Accept));
                    }
                    KnuBotAnswerListMessageHandler.Default.Send(source, target, new[] { "Goodbye" });
                    return true;

                case HandbagDialogueStage.TurnInChoice:
                    session.Dialogue = HandbagDialogueStage.Trading;
                    KnuBotStartTradeMessageHandler.Default.Send(source, target, TradePrompt, 1);
                    return true;

                case HandbagDialogueStage.CompletionThanks:
                    session.Dialogue = HandbagDialogueStage.Goodbye;
                    KnuBotAnswerListMessageHandler.Default.Send(source, target, new[] { "Goodbye" });
                    return true;

                default:
                    CloseDialogue(source, session);
                    return true;
            }
        }

        internal static bool TryHandleClose(ICharacter source, Identity target)
        {
            HandbagMissionSession session;
            if (!TryGetSession(source, target, out session))
            {
                return false;
            }

            session.Dialogue = HandbagDialogueStage.None;
            session.TradeContainer = Identity.None;
            return true;
        }

        internal static bool TryHandleTradeOffer(ICharacter source, KnuBotTradeMessage message)
        {
            HandbagMissionSession session;
            if (!TryGetSession(source, message.Target, out session)
                || session.Dialogue != HandbagDialogueStage.Trading)
            {
                return false;
            }

            IItem item = InventoryContainerRuntimeService.Default.GetKnuBotTradeItem(
                source,
                message.Container.Type,
                message.Container.Instance);
            session.TradeContainer = item != null && (item.LowID == HandbagItemId || item.HighID == HandbagItemId)
                                         ? message.Container
                                         : Identity.None;
            return true;
        }

        internal static bool TryHandleFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            HandbagMissionSession session;
            if (!TryGetSession(source, message.Target, out session)
                || session.Dialogue != HandbagDialogueStage.Trading)
            {
                return false;
            }

            if (message.Decline != 0 || session.TradeContainer.Equals(Identity.None))
            {
                session.TradeContainer = Identity.None;
                return true;
            }

            IInventoryPage page;
            if (!source.BaseInventory.Pages.TryGetValue((int)session.TradeContainer.Type, out page))
            {
                return true;
            }

            IItem item = page[session.TradeContainer.Instance];
            if (item == null || (item.LowID != HandbagItemId && item.HighID != HandbagItemId))
            {
                session.TradeContainer = Identity.None;
                return true;
            }

            page.Remove(session.TradeContainer.Instance);
            page.Write();
            CharacterActionMessageHandler.Default.SendDeleteItem(
                source,
                (int)session.TradeContainer.Type,
                session.TradeContainer.Instance);
            KnuBotRejectedItemsMessageHandler.Default.Send(source, message.Target, new Item[0]);

            ApplyCapturedReward(source);
            if (EmitCapturedQuestUiPackets)
            {
                source.Controller.Client.SendCompressed(CreateCompletionAction(source.Identity));
                source.Controller.Client.SendCompressed(CreateQuestDelete(source.Identity));
            }

            session.State = HandbagMissionState.Completed;
            session.Dialogue = HandbagDialogueStage.CompletionThanks;
            session.TradeContainer = Identity.None;
            KnuBotAnswerListMessageHandler.Default.Send(
                source,
                message.Target,
                new[] { "I'm glad I could help.", "Goodbye" });
            return true;
        }

        internal static bool ShouldSuppressCombat(ICharacter target)
        {
            return IsNatalia(target);
        }

        internal static bool HasActiveMissionPlayerInPlayfield(ICharacter target)
        {
            if (target == null || target.Playfield == null
                || target.Playfield.Identity.Instance != SubwayPlayfieldInstance)
            {
                return false;
            }

            int[] activePlayers;
            lock (Sync)
            {
                activePlayers = Sessions.Where(x => x.Value.State == HandbagMissionState.Active)
                    .Select(x => x.Key).ToArray();
            }

            return activePlayers.Any(
                instance =>
                {
                    ICharacter player = Pool.Instance.GetObject<ICharacter>(
                        target.Playfield.Identity,
                        new Identity { Type = IdentityType.CanbeAffected, Instance = instance });
                    return player != null;
                });
        }

        internal static void TryResendActiveMission(ICharacter source)
        {
            if (!EmitCapturedQuestUiPackets)
            {
                return;
            }

            if (source == null || source.Playfield == null || source.Controller == null
                || source.Controller.Client == null)
            {
                return;
            }

            HandbagMissionSession session = GetSession(source.Identity.Instance);
            if (session.State != HandbagMissionState.Active)
            {
                return;
            }

            MissionEmission emission = source.Playfield.Identity.Instance == SubwayPlayfieldInstance
                                           ? MissionEmission.SubwayResend
                                           : MissionEmission.MontroyalResend;
            source.Controller.Client.SendCompressed(
                CreateQuestFullUpdateMessage(source.Identity, session.NataliaIdentity, emission));
        }

        internal static QuestFullUpdateMessage CreateQuestFullUpdateMessage(
            Identity characterIdentity,
            Identity nataliaIdentity,
            MissionEmission emission)
        {
            int actionId7 = emission == MissionEmission.SubwayResend ? 1296350711
                                : emission == MissionEmission.MontroyalResend ? 1296457004
                                      : 1296456999;
            int unknownArray1 = emission == MissionEmission.SubwayResend ? 88391159
                                    : emission == MissionEmission.MontroyalResend ? 88497452
                                          : 88497447;

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests = new[]
                                    {
                                        new Quest
                                        {
                                            QuestId = IdentityFromRaw(MissionIdentityType, MissionInstance),
                                            Unknown1 = 15,
                                            Unknown2 = 0,
                                            Unknown3 = 0,
                                            Unknown4 = 2,
                                            ShortInfo = MissionShortInfo,
                                            LongInfo = MissionLongInfo,
                                            UnknownId1 = nataliaIdentity,
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
                                                                          LowId = DailyMissionXpRewardItemId,
                                                                          HighId = DailyMissionXpRewardItemId,
                                                                          Ql = 1,
                                                                          Unknown = 0
                                                                      }
                                                                  },
                                            Unknown11 = 808933429,
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
                                            Unknown20 = 60,
                                            Unknown21 = 60,
                                            QuestActions = new[]
                                                               {
                                                                   new QuestActionInfo
                                                                   {
                                                                       Version = 6,
                                                                       Action = IdentityFromRaw(70099, 1213481281),
                                                                       UnknownId1 = Identity.None,
                                                                       UnknownId2 = IdentityFromRaw(70099, 1346715979),
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
                                                                       UnknownHash1 = "\u006A\u0051\u00B7\u00A3",
                                                                       Unknown9 = 0,
                                                                       UnknownId7 = IdentityFromRaw(54001, actionId7),
                                                                       PlayfieldId = new Identity
                                                                                     {
                                                                                         Type = IdentityType.Playfield2,
                                                                                         Instance = MontroyalPlayfieldInstance
                                                                                     },
                                                                       Unknown10 = 37182,
                                                                       Unknown11 = 31311,
                                                                       Position = new Vector3(3306, 0, 837)
                                                                   }
                                                               },
                                            PlayerIds = new[] { characterIdentity },
                                            UnknownArray1 = new[] { unknownArray1 },
                                            UnknownArray2 = new int[0],
                                            CharacterInfos = new CharacterInfo[0],
                                            Unknown22 = 6,
                                            PlayerIds2 = new[] { characterIdentity },
                                            Unknown23 = 0,
                                            Unknown24 = 104999,
                                            UnknownId3 = Identity.None,
                                            Unknown25 = 0,
                                            Unknown26 = 0,
                                            QuestIdentities = new QuestIdentity[0],
                                            Unknown27 = 7,
                                            FactionInfos = new Identity[0],
                                            Unknown28 = (byte)(emission == MissionEmission.Accept ? 1 : 0)
                                        }
                                    }
                   };
        }

        private static void ApplyCapturedReward(ICharacter source)
        {
            try
            {
                var reward = new Item(1, DailyMissionXpRewardItemId, DailyMissionXpRewardItemId);
                reward.PerformAction(source, EventType.OnUse, 0);
            }
            catch (Exception e)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Handbag Daily Mission XP reward failed: " + e.Message);
            }
        }

        private static CharacterActionMessage CreateCompletionAction(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, MissionInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = MissionInstance,
                       Unknown2 = 0
                   };
        }

        private static QuestMessage CreateQuestDelete(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, MissionInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        private static bool IsNatalia(ICharacter npc)
        {
            return npc != null && npc.Playfield != null
                   && npc.Playfield.Identity.Instance == MontroyalPlayfieldInstance
                   && string.Equals(npc.Name, "Natalia Akcora", StringComparison.Ordinal)
                   && npc.Stats[StatIds.monsterdata].Value == NataliaMonsterData;
        }

        private static HandbagMissionSession GetSession(int playerInstance)
        {
            lock (Sync)
            {
                HandbagMissionSession session;
                if (!Sessions.TryGetValue(playerInstance, out session))
                {
                    session = new HandbagMissionSession();
                    Sessions[playerInstance] = session;
                }

                return session;
            }
        }

        private static bool TryGetSession(
            ICharacter source,
            Identity target,
            out HandbagMissionSession session)
        {
            session = GetSession(source.Identity.Instance);
            return !session.NataliaIdentity.Equals(Identity.None) && session.NataliaIdentity.Equals(target);
        }

        private static void CloseDialogue(ICharacter source, HandbagMissionSession session)
        {
            KnuBotCloseChatWindowMessageHandler.Default.Send(source, session.NataliaIdentity);
            session.Dialogue = HandbagDialogueStage.None;
            session.TradeContainer = Identity.None;
        }

        private static Identity IdentityFromRaw(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }

        internal enum MissionEmission
        {
            Accept,
            SubwayResend,
            MontroyalResend
        }

        private enum HandbagMissionState
        {
            None,
            Active,
            Completed
        }

        private enum HandbagDialogueStage
        {
            None,
            OfferChoice,
            AcceptChoice,
            TurnInChoice,
            Trading,
            CompletionThanks,
            Goodbye
        }

        private sealed class HandbagMissionSession
        {
            internal HandbagMissionState State;
            internal HandbagDialogueStage Dialogue;
            internal Identity NataliaIdentity = Identity.None;
            internal Identity TradeContainer = Identity.None;
        }
    }
}
