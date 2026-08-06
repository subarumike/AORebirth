namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class KneecappingQuestRuntime
    {
        public const string AlexReportRootNodeId = "alex_171317_001";

        public const string AlexTradeskillOfferNodeId = "alex_171317_002";

        // Capture 20260722-Alex-dialog: mid-quest reopen during Surveillance→Kneecapping.
        public const string AlexFavorRootNodeId = "alex_favor_001";

        private const string KillTargetName = "Kneebreaker Alfonzo Rizzolo";

        // Capture 20260720-171317: 2560-Bit Encryption Compiler ql1
        private const int EncryptionCompilerItemId = 296571;

        // Capture 20260720-171317: Nano Crystal (Composite Tradeskill Expertise) ql25
        private const int CompositeTradeskillExpertiseNanoCrystalItemId = 287041;

        private const int CompositeTradeskillExpertiseNanoCrystalQuality = 25;

        private const int ReportTurnInXpReward = 2581;

        private const int ReportTurnInCreditReward = 1200;

        // Capture 20260720-171317 FormatFeedback wire (2581 XP, 1200 credits).
        private const string ReportRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!/+~";

        private const string ReportRewardFlag = "report-alex-rewards-granted";

        private const int AreteLandingPlayfieldId = 6553;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        public static string ResolveAlexStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            // Capture 20260720-190432: Tip 4/4 + Talk to Stan coexist. Dialogue shows
            // "I built a Personalized Robot Brain!" — not Calitri Report.
            string brainNode = PersonalizedRobotBrainQuestRuntime.ResolveAlexStartNodeId(source);
            if (!string.IsNullOrEmpty(brainNode))
            {
                return brainNode;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return null;
            }

            // Capture 20260720-171317: Report→Stan only while Report is still the tip.
            if (IsMissionActive(source, "Mission:555B4365")
                && !IsMissionActive(source, "Mission:555B4366")
                && !IsMissionCompleted(source, "Mission:555B4366"))
            {
                return AlexReportRootNodeId;
            }

            // Tradeskill teach offer before Tip 1 starts (Talk to Stan still open).
            if (IsMissionActive(source, "Mission:555B4366")
                && !IsMissionActive(source, "Mission:555B4367")
                && !IsMissionCompleted(source, "Mission:555B4367")
                && !IsMissionActive(source, PersonalizedRobotBrainQuestRuntime.Tip4QuestId)
                && !IsMissionCompleted(source, PersonalizedRobotBrainQuestRuntime.Tip4QuestId))
            {
                return AlexTradeskillOfferNodeId;
            }

            // Capture 20260722-Alex-dialog: "How is that...favor..." while Alex favor chain
            // is active (Surveillance Uplink → Plant → Deliver Bill → Kneecapping).
            if (IsMissionActive(source, "Mission:5514B19D")
                || IsMissionActive(source, "Mission:5514B19E")
                || IsMissionActive(source, "Mission:5514B19F")
                || IsMissionActive(source, "Mission:5514B1A0"))
            {
                return AlexFavorRootNodeId;
            }

            return null;
        }

        public static bool TryHandleAlexDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            if (string.Equals(previousNodeId, AlexReportRootNodeId, StringComparison.OrdinalIgnoreCase))
            {
                CompleteReportToAlexAndOfferTalkToStan(source);
                return true;
            }

            if (PersonalizedRobotBrainQuestRuntime.TryHandleAlexDialogueAnswer(source, previousNodeId, answerIndex))
            {
                return true;
            }

            if (string.Equals(previousNodeId, AlexTradeskillOfferNodeId, StringComparison.OrdinalIgnoreCase))
            {
                OfferTradeskillNanoSensorTip(source);
                return true;
            }

            return false;
        }

        public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            if (!IsInAreteLanding(attacker) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (!string.Equals(EffectiveName(target), KillTargetName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(attacker.Identity.Instance, "Mission:5514B1A0");
            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return false;
            }

            CompleteKneecappingAndOfferReportToAlex(attacker);
            return true;
        }

        private static void CompleteKneecappingAndOfferReportToAlex(ICharacter source)
        {
            int instance = source.Identity.Instance;
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B1A0",
                "Mission:555B4365");
            if (result.Status != MissionOperationStatus.Applied && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B1A0");
                MissionRuntime.Service.OfferMission(instance, "Mission:555B4365");
                MissionRuntime.Service.AcceptMission(instance, "Mission:555B4365");
            }

            SafeQuestFullUpdateSender.TrySendKneecappingToReportAlexHandoff(source);
            Log("kneecapping-complete→report-alex character=" + source.Identity.ToString(true));
        }

        private static void CompleteReportToAlexAndOfferTalkToStan(ICharacter source)
        {
            int instance = source.Identity.Instance;
            ApplyReportTurnInXpCredits(source);
            TrySendReportRewardFeedback(source);
            TryGrantReportRewardItems(source);
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:555B4365",
                "Mission:555B4366");
            if (result.Status != MissionOperationStatus.Applied && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:555B4365");
                MissionRuntime.Service.OfferMission(instance, "Mission:555B4366");
                MissionRuntime.Service.AcceptMission(instance, "Mission:555B4366");
            }

            SafeQuestFullUpdateSender.TrySendReportAlexToTalkStanHandoff(source);
            Log("report-alex-complete→talk-stan character=" + source.Identity.ToString(true));
        }

        private static void OfferTradeskillNanoSensorTip(ICharacter source)
        {
            int instance = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(instance, "Mission:555B4367");
            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                MissionRuntime.Service.OfferMission(instance, "Mission:555B4367");
                MissionRuntime.Service.AcceptMission(instance, "Mission:555B4367");
            }

            SafeQuestFullUpdateSender.TrySendTradeskillNanoSensorTip(source);
            Log("tradeskill-nano-sensor tip character=" + source.Identity.ToString(true));
        }

        private static void ApplyReportTurnInXpCredits(ICharacter source)
        {
            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-alex-report-calitri-xp-credits",
                                                    RewardType = "character-stats",
                                                    IsResolved = true,
                                                    StatMutations =
                                                        new[]
                                                        {
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.cash,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = ReportTurnInCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = ReportTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = ReportTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.lastxp,
                                                                Kind = MissionStatMutationKind.Set,
                                                                Value = ReportTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                "Mission:555B4365",
                definition,
                "capture:20260720-171317:alex-report-xp-credits");
            if (!result.Succeeded || result.StatValues == null)
            {
                return;
            }

            foreach (MissionCharacterStatValue statValue in result.StatValues)
            {
                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                source.Stats[(StatIds)statValue.StatId].Set(value);
            }

            StatMessageHandler.Default.SendChanged(source);
        }

        private static void TrySendReportRewardFeedback(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            try
            {
                source.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = ReportRewardFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception ex)
            {
                Log("report reward feedback failed: " + ex.Message);
            }
        }

        private static void TryGrantReportRewardItems(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (MissionRuntime.Service.GetFlag(
                    source.Identity.Instance,
                    "Mission:555B4365",
                    ReportRewardFlag) != null)
            {
                return;
            }

            bool compilerGranted = TryGrantQuestRewardWithOverflowPackets(
                source,
                EncryptionCompilerItemId,
                1);
            bool nanoCrystalGranted = TryGrantQuestRewardWithOverflowPackets(
                source,
                CompositeTradeskillExpertiseNanoCrystalItemId,
                CompositeTradeskillExpertiseNanoCrystalQuality);

            if (compilerGranted || nanoCrystalGranted)
            {
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    "Mission:555B4365",
                    ReportRewardFlag,
                    "items:" + EncryptionCompilerItemId + "," + CompositeTradeskillExpertiseNanoCrystalItemId);
            }

            Log(
                "report rewards compiler="
                + compilerGranted
                + " nano-crystal="
                + nanoCrystalGranted
                + " character="
                + source.Identity.ToString(true));
        }

        private static bool TryGrantQuestRewardWithOverflowPackets(ICharacter source, int itemId, int quality)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("grant skipped item=" + itemId + " reason=no-inventory-or-client");
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("grant skipped item=" + itemId + " reason=missing-ItemLoader-template");
                return false;
            }

            Item item;
            try
            {
                item = new Item(quality, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("grant create failed item=" + itemId + " err=" + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant == null || grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "grant failed item="
                    + itemId
                    + " status="
                    + (grant == null ? "null" : grant.Status.ToString()));
                return false;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = quality,
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
            return true;
        }

        private static bool IsMissionActive(ICharacter source, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        private static bool IsMissionCompleted(ICharacter source, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source != null
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static string EffectiveName(ICharacter character)
        {
            return character == null ? string.Empty : (character.Name ?? string.Empty).Trim();
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Error, "KneecappingQuestRuntime " + message);
        }
    }
}
