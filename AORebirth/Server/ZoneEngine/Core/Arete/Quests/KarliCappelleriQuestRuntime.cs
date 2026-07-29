namespace ZoneEngine.Core.Arete.Quests

{

    #region Usings ...



    using System;

    using System.Collections.Generic;



    using AORebirth.Core.Entities;

    using AORebirth.Core.Items;

    using AORebirth.Enums;



    using SmokeLounge.AOtomation.Messaging.GameData;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;



    using Utility;



    using ZoneEngine.Core;

    using ZoneEngine.Core.Arete.Dialogue;

    using ZoneEngine.Core.Controllers;

    using ZoneEngine.Core.MessageHandlers;

    using ZoneEngine.Core.Missions;

    using ZoneEngine.Core.Playfields;



    #endregion



    /// <summary>

    /// Capture 20260727-Alien- quest-ncu: Karli Cappelleri on PF 8009.

    /// Tips Mission:5565A09A (teamed) / Mission:5565A09B (solo + Friendly Buff 301250).

    /// Find-a-Friend finish: 2080 XP, 2520 credits, 3x NCU 36778/36786 ql38.

    /// </summary>

    public static class KarliCappelleriQuestRuntime

    {

        public const string RootNodeId = "karli_001";



        public const string DoingTeamedNodeId = "karli_doing_teamed";



        public const string DoingSoloNodeId = "karli_doing_solo";



        public const string FightNodeId = "karli_fight";



        public const int FriendlyBuffItemId = 301250;



        public const string CrashedShipQuestId = "Mission:5565A09A";



        public const string FindFriendQuestId = "Mission:5565A09B";



        private const int CrashedAlienShipPlayfieldId = 8009;



        private const int NcuLowId = 36778;



        private const int NcuHighId = 36786;



        private const int NcuQuality = 38;



        private const int NcuRewardCount = 3;



        private const int FinishXpReward = 2080;



        private const int FinishCreditReward = 2520;



        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!9Ii!!!>X~";



        // Capture FormatFeedback wire for "You have a friend." (x2 on use).

        private const string FriendFeedback = "~&!!!\":!!!)<sYou have a friend.";



        private const int CapturedTemplateActionUnknown1 = 1;



        private const int CapturedTemplateActionUnknown2 = 87;



        private const int CapturedOverflowNextFreeSlot = 0x6F;



        private const string RewardsGrantedFlag = "karli-find-friend-rewards-granted";



        public static bool HasTeamPartner(ICharacter source)

        {

            List<Identity> members;

            return source != null

                   && TeamRuntime.TryGetTeamMembers(source, out members)

                   && members != null

                   && members.Count > 1;

        }



        public static DialogueSessionResult ApplyDoingBranchOverride(

            DialogueSessionService service,

            DialogueSessionResult result,

            string previousNodeId,

            int answerIndex,

            ICharacter source)

        {

            if (service == null

                || result?.Session == null

                || source == null

                || answerIndex != 0

                || !string.Equals(previousNodeId, RootNodeId, StringComparison.OrdinalIgnoreCase))

            {

                return null;

            }



            string desiredNodeId = HasTeamPartner(source) ? DoingTeamedNodeId : DoingSoloNodeId;

            if (string.Equals(result.Session.CurrentNodeId, desiredNodeId, StringComparison.OrdinalIgnoreCase))

            {

                return null;

            }



            result.Session.CurrentNodeId = desiredNodeId;

            return service.RebuildResultForCurrentNode(result.Session);

        }



        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)

        {

            if (source == null || answerIndex != 0 || string.IsNullOrEmpty(previousNodeId))

            {

                return false;

            }



            if (string.Equals(previousNodeId, DoingTeamedNodeId, StringComparison.OrdinalIgnoreCase))

            {

                StartCrashedShipTip(source);

                return true;

            }



            if (string.Equals(previousNodeId, FightNodeId, StringComparison.OrdinalIgnoreCase))

            {

                GrantFriendlyBuffAndFindFriendTip(source);

                return true;

            }



            return false;

        }



        /// <summary>

        /// Capture: GenericCmd Use Friendly Buff Nano Can (301250) while teamed → rewards + consume.

        /// </summary>

        public static bool TryHandleFriendlyBuffUse(

            ICharacter character,

            Identity itemPosition,

            Item item)

        {

            if (character == null

                || item == null

                || (item.LowID != FriendlyBuffItemId && item.HighID != FriendlyBuffItemId))

            {

                return false;

            }



            if (!IsInCrashedAlienShip(character)

                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(character)

                || character.Controller == null

                || character.Controller.Client == null)

            {

                Log("friendly-buff use skipped: pf/inventory/client");

                return false;

            }



            if (!IsMissionActive(character, FindFriendQuestId))

            {

                Log("friendly-buff use ignored: find-friend tip inactive");

                return false;

            }



            if (HasRewardsGranted(character))

            {

                SafeQuestFullUpdateSender.SendTipAction59AndDelete(

                    character,

                    KarliCappelleriTipSender.FindFriendTipInstance);

                return true;

            }



            if (!HasTeamPartner(character))

            {

                Log("friendly-buff use blocked: not teamed");

                return false;

            }



            SendFriendFeedback(character);

            SendFriendFeedback(character);

            ApplyFinishXpCredits(character);

            TryGrantNcuRewards(character);

            TrySendFinishRewardFeedback(character);



            SafeQuestFullUpdateSender.SendTipAction59AndDelete(

                character,

                KarliCappelleriTipSender.FindFriendTipInstance);

            ForceCompleteTip(character.Identity.Instance, FindFriendQuestId, "mission_5565A09B_use_friendly_buff");

            if (MissionRuntime.IsInitialized)

            {

                MissionRuntime.Service.SetFlag(

                    character.Identity.Instance,

                    FindFriendQuestId,

                    RewardsGrantedFlag,

                    "1");

            }



            ConsumeFriendlyBuff(character, itemPosition, item);

            Log("find-friend complete character=" + character.Identity.ToString(true));

            return true;

        }



        public static void PausePatrolForDialogue(ICharacter npc)

        {

            AreteKarliCappelleriPatrolRuntime.PauseForDialogue(npc);

        }



        public static void ResumePatrolAfterDialogue(ICharacter npc)

        {

            AreteKarliCappelleriPatrolRuntime.ResumeAfterDialogue(npc);

        }



        private static void StartCrashedShipTip(ICharacter source)

        {

            if (!MissionRuntime.IsInitialized || !IsInCrashedAlienShip(source))

            {

                return;

            }



            int characterId = source.Identity.Instance;

            MissionRuntime.Service.OfferMission(characterId, CrashedShipQuestId);

            MissionRuntime.Service.AcceptMission(characterId, CrashedShipQuestId);

            KarliCappelleriTipSender.TrySendCrashedShipTipOnly(source);

            Log("crashed-ship tip started character=" + source.Identity.ToString(true));

        }



        private static void GrantFriendlyBuffAndFindFriendTip(ICharacter source)

        {

            if (!MissionRuntime.IsInitialized || !IsInCrashedAlienShip(source))

            {

                return;

            }



            if (!TryGrantItem(source, FriendlyBuffItemId))

            {

                Log("friendly-buff grant failed character=" + source.Identity.ToString(true));

                return;

            }



            SendOverflowGrantPackets(source, FriendlyBuffItemId);

            EnsureQuestActive(source, FindFriendQuestId);

            KarliCappelleriTipSender.TrySendFindFriendTipOnly(source);

            Log("find-friend tip + nano can character=" + source.Identity.ToString(true));

        }



        private static void ConsumeFriendlyBuff(ICharacter character, Identity itemPosition, Item item)

        {

            if (character?.BaseInventory == null || item == null)

            {

                return;

            }



            try

            {

                character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);

            }

            catch (Exception ex)

            {

                Log("friendly-buff remove failed err=" + ex.Message);

            }



            CharacterActionMessageHandler.Default.SendDeleteItem(

                character,

                (int)itemPosition.Type,

                itemPosition.Instance);

        }



        private static void TryGrantNcuRewards(ICharacter source)

        {

            if (!ItemLoader.ItemList.ContainsKey(NcuLowId) && !ItemLoader.ItemList.ContainsKey(NcuHighId))

            {

                Log("ncu grant skipped: missing ItemLoader template");

                return;

            }



            int createId = ItemLoader.ItemList.ContainsKey(NcuLowId) ? NcuLowId : NcuHighId;

            for (int i = 0; i < NcuRewardCount; i++)

            {

                Item ncu;

                try

                {

                    ncu = new Item(NcuQuality, createId, NcuHighId);

                }

                catch (Exception ex)

                {

                    Log("ncu create failed err=" + ex.Message);

                    return;

                }



                QuestRewardInventoryGrantResult grant =

                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, ncu);

                if (grant == null || grant.Status != QuestRewardInventoryGrantStatus.Success)

                {

                    Log("ncu grant failed status=" + (grant == null ? "null" : grant.Status.ToString()));

                    return;

                }



                source.Send(

                    new TemplateActionMessage

                    {

                        Identity = source.Identity,

                        Unknown = 0,

                        ItemLowId = NcuLowId,

                        ItemHighId = NcuHighId,

                        Quality = NcuQuality,

                        Unknown1 = CapturedTemplateActionUnknown1,

                        Unknown2 = CapturedTemplateActionUnknown2,

                        Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },

                        Unknown3 = 0,

                        Unknown4 = 0

                    });

                source.Send(

                    new ContainerAddItemMessage

                    {

                        Identity = source.Identity,

                        Unknown = 0,

                        SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },

                        Target = new Identity

                                 {

                                     Type = IdentityType.OverflowWindow,

                                     Instance = source.Identity.Instance

                                 },

                        TargetPlacement = CapturedOverflowNextFreeSlot

                    });

            }

        }



        private static void ApplyFinishXpCredits(ICharacter source)

        {

            bool cashApplied = false;

            if (MissionRuntime.IsInitialized)

            {

                MissionRewardDefinition cashDefinition = new MissionRewardDefinition

                                                        {

                                                            RewardKey = "captured-karli-find-friend-credits",

                                                            RewardType = "character-stats",

                                                            IsResolved = true,

                                                            StatMutations =

                                                                new[]

                                                                {

                                                                    new MissionCharacterStatMutation

                                                                    {

                                                                        StatIdentityType =

                                                                            (int)IdentityType.CanbeAffected,

                                                                        StatId = (int)StatIds.cash,

                                                                        Kind = MissionStatMutationKind.AddClamped,

                                                                        Value = FinishCreditReward,

                                                                        MinimumValue = 0,

                                                                        MaximumValue = uint.MaxValue

                                                                    }

                                                                }

                                                        };

                MissionRewardExecutionResult cashResult = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(

                    source.Identity.Instance,

                    FindFriendQuestId,

                    cashDefinition,

                    "capture:20260727-Alien- quest-ncu:karli-find-friend-credits");

                if (cashResult.Succeeded && cashResult.StatValues != null)

                {

                    foreach (MissionCharacterStatValue statValue in cashResult.StatValues)

                    {

                        if (statValue.StatId != (int)StatIds.cash)

                        {

                            continue;

                        }



                        uint value = statValue.Value <= 0

                                         ? 0

                                         : (uint)Math.Min(statValue.Value, uint.MaxValue);

                        source.Stats[StatIds.cash].Set(value);

                        cashApplied = true;

                    }



                    if (cashApplied)

                    {

                        StatMessageHandler.Default.SendChanged(source);

                    }

                }

            }



            if (!cashApplied)

            {

                long cashAfter = (long)source.Stats[StatIds.cash].Value + FinishCreditReward;

                if (cashAfter > uint.MaxValue)

                {

                    cashAfter = uint.MaxValue;

                }



                source.Stats[StatIds.cash].Set((uint)cashAfter);

                StatMessageHandler.Default.SendChanged(source);

            }



            CombatXpRuntimeService.AwardDirectXp(source, FinishXpReward, "karli-find-friend-2080xp");

        }



        private static void SendFriendFeedback(ICharacter source)

        {

            if (source?.Controller?.Client == null)

            {

                return;

            }



            source.Controller.Client.SendCompressed(

                new FormatFeedbackMessage

                {

                    Identity = source.Identity,

                    Unknown = 1,

                    Unknown1 = 0,

                    FormattedMessage = FriendFeedback,

                    Unknown2 = 0

                });

        }



        private static void TrySendFinishRewardFeedback(ICharacter source)

        {

            if (source?.Controller?.Client == null)

            {

                return;

            }



            source.Controller.Client.SendCompressed(

                new FormatFeedbackMessage

                {

                    Identity = source.Identity,

                    Unknown = 1,

                    Unknown1 = 0,

                    FormattedMessage = FinishRewardFeedback,

                    Unknown2 = 0

                });

        }



        private static bool TryGrantItem(ICharacter source, int itemId)

        {

            if (!ItemLoader.ItemList.ContainsKey(itemId))

            {

                return false;

            }



            Item item;

            try

            {

                item = new Item(1, itemId, itemId);

            }

            catch (Exception ex)

            {

                Log("item create failed id=" + itemId + " err=" + ex.Message);

                return false;

            }



            QuestRewardInventoryGrantResult grant =

                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);

            return grant != null && grant.Status == QuestRewardInventoryGrantStatus.Success;

        }



        private static void SendOverflowGrantPackets(ICharacter source, int itemId)

        {

            source.Send(

                new TemplateActionMessage

                {

                    Identity = source.Identity,

                    Unknown = 0,

                    ItemLowId = itemId,

                    ItemHighId = itemId,

                    Quality = 1,

                    Unknown1 = CapturedTemplateActionUnknown1,

                    Unknown2 = CapturedTemplateActionUnknown2,

                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },

                    Unknown3 = 0,

                    Unknown4 = 0

                });

            source.Send(

                new ContainerAddItemMessage

                {

                    Identity = source.Identity,

                    Unknown = 0,

                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },

                    Target = new Identity

                             {

                                 Type = IdentityType.OverflowWindow,

                                 Instance = source.Identity.Instance

                             },

                    TargetPlacement = CapturedOverflowNextFreeSlot

                });

        }



        private static bool HasRewardsGranted(ICharacter source)

        {

            return source != null

                   && MissionRuntime.IsInitialized

                   && MissionRuntime.Service.GetFlag(

                       source.Identity.Instance,

                       FindFriendQuestId,

                       RewardsGrantedFlag) != null;

        }



        private static void EnsureQuestActive(ICharacter source, string questId)

        {

            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))

            {

                return;

            }



            int characterId = source.Identity.Instance;

            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);

            if (mission != null && mission.State == MissionLifecycleState.Active)

            {

                return;

            }



            if (mission == null || mission.State == MissionLifecycleState.Offered)

            {

                MissionRuntime.Service.OfferMission(characterId, questId);

                MissionRuntime.Service.AcceptMission(characterId, questId);

            }

        }



        private static void ForceCompleteTip(int characterId, string questId, string objectiveId)

        {

            if (!MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))

            {

                return;

            }



            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);

            if (mission == null || mission.State == MissionLifecycleState.Completed)

            {

                return;

            }



            if (mission.State == MissionLifecycleState.Offered)

            {

                MissionRuntime.Service.AcceptMission(characterId, questId);

                mission = MissionRuntime.Service.GetMission(characterId, questId);

            }



            if (mission == null || mission.State != MissionLifecycleState.Active)

            {

                return;

            }



            if (!string.IsNullOrEmpty(objectiveId))

            {

                MissionRuntime.Service.ObserveObjective(

                    new MissionObjectiveObservation

                    {

                        CharacterId = characterId,

                        QuestId = questId,

                        ObjectiveId = objectiveId,

                        ObservationKey = "karli-find-friend-force-complete",

                        Amount = 1,

                        EventType = "KarliCappelleriQuestRuntime",

                        SourceIdentity = string.Empty,

                        TargetIdentity = string.Empty

                    });

            }



            MissionRuntime.Service.CompleteMission(characterId, questId);

        }



        private static bool IsMissionActive(ICharacter source, string questId)

        {

            if (!MissionRuntime.IsInitialized)

            {

                return false;

            }



            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);

            return mission != null && mission.State == MissionLifecycleState.Active;

        }



        private static bool IsInCrashedAlienShip(ICharacter source)

        {

            return source != null

                   && source.Playfield != null

                   && source.Playfield.Identity.Instance == CrashedAlienShipPlayfieldId;

        }



        private static void Log(string message)

        {

            LogUtil.Debug(DebugInfoDetail.Engine, "KarliCappelleriQuestRuntime " + message);

        }

    }

}


