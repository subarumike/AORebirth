namespace ZoneEngine.Core.Thrak.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Thrak garden key quest runtime (20260718-185306).
    /// </summary>
    internal static class ThrakGardenKeyQuestRuntime
    {
        internal static bool IsMissionActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static bool HasProphetDeviceInspected(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestVeronica,
                       ThrakGardenKeyInteractionRules.ProphetDeviceInspectedFlag) != null
                   || MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestInsignia,
                       ThrakGardenKeyInteractionRules.ProphetDeviceInspectedFlag) != null;
        }

        internal static void MarkProphetDeviceInspected(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            string questId = IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestInsignia)
                                 ? ThrakGardenKeyInteractionRules.QuestInsignia
                                 : ThrakGardenKeyInteractionRules.QuestVeronica;
            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                ThrakGardenKeyInteractionRules.ProphetDeviceInspectedFlag,
                "1");
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "thrak-quest-runtime-unavailable"
                       };
            }

            int characterId = source.Identity.Instance;

            // Already Active: re-sync client journal.
            if (IsMissionActive(source, questId))
            {
                if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyInsigniaCommitmentHandoff(source);
                }
                else if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestGarden, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyGardenHandoff(source);
                }
                else
                {
                    ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, questId);
                }

                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "thrak-quest-already-active"
                       };
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (!IsClientEmitSuccess(offer) && IsPersistenceFailure(offer))
            {
                return offer;
            }

            MissionOperationResult accepted = MissionRuntime.Service.AcceptMission(characterId, questId);
            if (IsClientEmitSuccess(accepted))
            {
                if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyInsigniaCommitmentHandoff(source);
                }
                else if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestGarden, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyGardenHandoff(source);
                }
                else
                {
                    ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, questId);
                }
            }

            return accepted;
        }

        /// <summary>
        /// Capture 20260718-230923 @21:10:16 — Prophet "I am prepared...":
        /// QFU Insignia → Action59+Delete Veronica → QFU VeronicaUpdated ("You agreed...").
        /// Veronica stays Active in DB / journal (updated text) until Insignia of Thrak trade.
        /// </summary>
        internal static void ApplyInsigniaCommitmentHandoff(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            // Do not ForceClose Veronica here — she leaves only on Insignia trade / Garden handoff.
            ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                source,
                ThrakGardenKeyInteractionRules.QuestInsignia);
            ThrakGardenKeyPacketSender.TrySendQuestDelete(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronica);
            ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
        }

        /// <summary>
        /// Capture: Insignia trade / statue garden advance — delete Veronica(+Updated) + Insignia, QFU Garden.
        /// </summary>
        private static void ApplyGardenHandoff(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronica,
                "mission_5556893A_find",
                "garden-handoff");
            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestInsignia,
                "mission_55563C16_insignia",
                "garden-handoff");
            ThrakGardenKeyPacketSender.TrySendQuestDelete(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);

            ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                source,
                ThrakGardenKeyInteractionRules.QuestGarden);
        }

        /// <summary>
        /// Statue entry with Insignia of Thrak: advance to Garden journal stage if still on Insignia.
        /// </summary>
        internal static void TryAdvanceToGardenOnStatueEntry(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestGarden)
                || IsMissionCompleted(source, ThrakGardenKeyInteractionRules.QuestGarden))
            {
                return;
            }

            if (!IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestInsignia)
                && !IsMissionCompleted(source, ThrakGardenKeyInteractionRules.QuestInsignia))
            {
                return;
            }

            AcceptQuest(source, ThrakGardenKeyInteractionRules.QuestGarden);
        }

        /// <summary>
        /// Client mission journal clears on every zone/relog. Re-emit capture-backed QuestFullUpdate
        /// for each Active Thrak garden-key mission (MissionRuntime state is durable; journal is not).
        /// </summary>
        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Finished chain: purge any leftover journal rows (VeronicaUpdated / Garden share the
            // "You agreed to find information..." short text) and do not re-emit.
            if (HasCompletedGardenKeyQuest(source))
            {
                ClearFinishedThrakChainJournal(source);
                return false;
            }

            System.Collections.Generic.IList<MissionStateRecord> missions =
                MissionRuntime.Service.GetMissions(source.Identity.Instance);
            if (missions == null || missions.Count == 0)
            {
                return false;
            }

            bool sent = false;
            bool insigniaActive = false;
            bool veronicaActive = false;
            bool gardenActive = false;
            bool soulsActive = false;
            bool returnActive = false;

            for (int i = 0; i < missions.Count; i++)
            {
                MissionStateRecord mission = missions[i];
                if (mission == null || mission.State != MissionLifecycleState.Active)
                {
                    continue;
                }

                if (string.Equals(mission.QuestId, ThrakGardenKeyInteractionRules.QuestVeronica, StringComparison.OrdinalIgnoreCase))
                {
                    veronicaActive = true;
                }
                else if (string.Equals(mission.QuestId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase))
                {
                    insigniaActive = true;
                }
                else if (string.Equals(mission.QuestId, ThrakGardenKeyInteractionRules.QuestGarden, StringComparison.OrdinalIgnoreCase))
                {
                    gardenActive = true;
                }
                else if (string.Equals(mission.QuestId, ThrakGardenKeyInteractionRules.QuestSouls, StringComparison.OrdinalIgnoreCase))
                {
                    soulsActive = true;
                }
                else if (string.Equals(mission.QuestId, ThrakGardenKeyInteractionRules.QuestReturn, StringComparison.OrdinalIgnoreCase))
                {
                    returnActive = true;
                }
            }

            // Later chain stages supersede earlier journal entries.
            if (returnActive)
            {
                ClearEarlierThrakJournalBefore(source, ThrakGardenKeyInteractionRules.QuestReturn);
                sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestReturn);
            }
            else if (soulsActive)
            {
                ClearEarlierThrakJournalBefore(source, ThrakGardenKeyInteractionRules.QuestSouls);
                int souls = GetSoulCount(source);
                string clientQuestId = ThrakGardenKeyInteractionRules.QuestSouls;
                if (souls >= 2)
                {
                    clientQuestId = ThrakGardenKeyInteractionRules.QuestSouls2;
                }
                else if (souls >= 1)
                {
                    clientQuestId = ThrakGardenKeyInteractionRules.QuestSouls1;
                }

                sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, clientQuestId);
            }
            else if (gardenActive)
            {
                // Garden short title matches Veronica — clear earlier client lines first.
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestInsignia);
                sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestGarden);
            }
            else if (insigniaActive)
            {
                // Capture: Insignia + VeronicaUpdated coexist until Insignia of Thrak trade.
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
                sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestInsignia);
                if (veronicaActive)
                {
                    sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                        source,
                        ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
                }
            }
            else if (veronicaActive)
            {
                sent |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
            }

            return sent;
        }

        /// <summary>
        /// After sacred key is earned: close any leftover Active DB missions and delete every
        /// capture-backed Thrak journal id from the client (fixes stuck VeronicaUpdated / Garden).
        /// </summary>
        internal static void ClearFinishedThrakChainJournal(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronica,
                "mission_5556893A_find",
                "finished-chain-cleanup");
            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestInsignia,
                "mission_55563C16_insignia",
                "finished-chain-cleanup");
            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestGarden,
                "mission_55563C18_garden",
                "finished-chain-cleanup");
            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestSouls,
                "mission_5556591A_souls",
                "finished-chain-cleanup");
            ForceCloseQuest(
                source,
                ThrakGardenKeyInteractionRules.QuestReturn,
                "mission_5556893D_return",
                "finished-chain-cleanup");

            // Client-only journal ids (not always in MissionRuntime).
            ThrakGardenKeyPacketSender.TrySendQuestDelete(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
            ThrakGardenKeyPacketSender.TrySendQuestDelete(
                source,
                ThrakGardenKeyInteractionRules.QuestSouls1);
            ThrakGardenKeyPacketSender.TrySendQuestDelete(
                source,
                ThrakGardenKeyInteractionRules.QuestSouls2);
        }

        /// <summary>
        /// Delete superseded earlier Thrak journal entries before re-emitting a later stage.
        /// </summary>
        private static void ClearEarlierThrakJournalBefore(ICharacter source, string keepQuestId)
        {
            if (source == null)
            {
                return;
            }

            // Always clear client-only VeronicaUpdated when past insignia commitment.
            if (!string.Equals(keepQuestId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(keepQuestId, ThrakGardenKeyInteractionRules.QuestVeronica, StringComparison.OrdinalIgnoreCase))
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestInsignia);
            }

            if (string.Equals(keepQuestId, ThrakGardenKeyInteractionRules.QuestReturn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(keepQuestId, ThrakGardenKeyInteractionRules.QuestSouls, StringComparison.OrdinalIgnoreCase))
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestGarden);
            }

            if (string.Equals(keepQuestId, ThrakGardenKeyInteractionRules.QuestReturn, StringComparison.OrdinalIgnoreCase))
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls1);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls2);
            }
        }

        internal static MissionOperationResult CompleteQuest(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "thrak-quest-runtime-unavailable"
                       };
            }

            string objectiveId = ResolveObjectiveId(questId);
            MissionOperationResult closed = ForceCloseQuest(source, questId, objectiveId, "complete-quest");

            // Garden stage replaces VeronicaUpdated ("You agreed...") — capture deletes 55563C17 here.
            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestGarden, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase))
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronicaUpdated);
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestVeronica);
            }

            // Final return: wipe the entire Thrak journal chain.
            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestReturn, StringComparison.OrdinalIgnoreCase))
            {
                ClearFinishedThrakChainJournal(source);
            }

            return closed;
        }

        /// <summary>
        /// Observe objective when possible, CompleteMission, else Abandon — always emit Quest/Delete.
        /// Thrak handoffs advance by capture packets, not by objective trackers.
        /// </summary>
        private static MissionOperationResult ForceCloseQuest(
            ICharacter source,
            string questId,
            string objectiveId,
            string reason)
        {
            if (source == null || string.IsNullOrWhiteSpace(questId) || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "thrak-force-close-unavailable"
                       };
            }

            int characterId = source.Identity.Instance;
            if (IsMissionActive(source, questId) && !string.IsNullOrWhiteSpace(objectiveId))
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = questId,
                        ObjectiveId = objectiveId,
                        ObservationKey = "thrak:" + reason + ":" + questId,
                        Amount = 1,
                        EventType = "ThrakGardenKey:ForceClose",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = questId
                    });
            }

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (IsPersistenceFailure(completed) && IsMissionActive(source, questId))
            {
                completed = MissionRuntime.Service.AbandonMission(characterId, questId);
            }

            ThrakGardenKeyPacketSender.TrySendQuestDelete(source, questId);
            return completed;
        }

        private static string ResolveObjectiveId(string questId)
        {
            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestVeronica, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_5556893A_find";
            }

            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestInsignia, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_55563C16_insignia";
            }

            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestGarden, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_55563C18_garden";
            }

            if (string.Equals(questId, ThrakGardenKeyInteractionRules.QuestSouls, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_5556591A_souls";
            }

            return null;
        }

        /// <summary>Only Applied/AlreadyApplied may drive client journal packets.</summary>
        private static bool IsClientEmitSuccess(MissionOperationResult result)
        {
            return result != null
                   && (result.Status == MissionOperationStatus.Applied
                       || result.Status == MissionOperationStatus.AlreadyApplied);
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result != null
                   && result.Status != MissionOperationStatus.Applied
                   && result.Status != MissionOperationStatus.AlreadyApplied
                   && result.Status != MissionOperationStatus.Unresolved;
        }

        internal static bool TryGrantAnalyzer(ICharacter source)
        {
            return TryGrantItem(
                source,
                ThrakGardenKeyInteractionRules.QuestVeronica,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId,
                ThrakGardenKeyInteractionRules.AnalyzerGrantedFlag);
        }

        internal static bool TryGrantInsignia(ICharacter source)
        {
            return TryGrantItem(
                source,
                ThrakGardenKeyInteractionRules.QuestSouls,
                ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId,
                ThrakGardenKeyInteractionRules.InsigniaGrantedFlag);
        }

        /// <summary>
        /// Capture 20260718-185306 Hyp Accept: TemplateAction 214783 after RejectedItems Unknown2=0.
        /// </summary>
        internal static bool TryGrantInspectedAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasInspectedAnalyzer(source) || HasFavoredAnalyzer(source))
            {
                return true;
            }

            // Replace raw Veronica analyzer (214998) with the capture return item so the client
            // receives TemplateAction/ContainerAddItem (Unknown2=0 already dropped the offer UI-side).
            TryConsumeCarriedItem(source, ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId);

            if (TryRestoreItem(
                    source,
                    ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId,
                    1))
            {
                if (MissionRuntime.IsInitialized)
                {
                    MissionRuntime.Service.SetFlag(
                        source.Identity.Instance,
                        ThrakGardenKeyInteractionRules.QuestSouls,
                        ThrakGardenKeyInteractionRules.InspectedAnalyzerGrantedFlag,
                        "item:" + ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId);
                }

                return true;
            }

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId,
                1);
        }

        /// <summary>
        /// Capture Hyp return trade: TemplateAction 214785 after RejectedItems Unknown2=0.
        /// Consumes empty/inspected analyzer first so the client gets a full favored device.
        /// </summary>
        internal static bool TryGrantFavoredAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasFavoredAnalyzer(source))
            {
                return true;
            }

            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId);

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId,
                1);
        }

        internal static bool TryGrantGardenKey(ICharacter source)
        {
            if (!TryGrantItem(
                    source,
                    ThrakGardenKeyInteractionRules.QuestReturn,
                    ThrakGardenKeyInteractionRules.SacredGardenKeyItemId,
                    ThrakGardenKeyInteractionRules.KeyGrantedFlag))
            {
                // Fallback: grant against souls quest if return not yet active.
                if (!TryGrantItem(
                        source,
                        ThrakGardenKeyInteractionRules.QuestSouls,
                        ThrakGardenKeyInteractionRules.SacredGardenKeyItemId,
                        ThrakGardenKeyInteractionRules.KeyGrantedFlag))
                {
                    return false;
                }
            }

            SetAccountKeyFlag(source);
            return true;
        }

        internal static bool HasGardenKey(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    ThrakGardenKeyInteractionRules.SacredGardenKeyItemId))
            {
                return true;
            }

            AORebirth.Core.Inventory.IInventoryPage weaponPage;
            if (!source.BaseInventory.Pages.TryGetValue(
                    (int)IdentityType.WeaponPage,
                    out weaponPage)
                || weaponPage == null)
            {
                return false;
            }

            for (int slot = weaponPage.FirstSlotNumber;
                 slot < weaponPage.FirstSlotNumber + weaponPage.MaxSlots;
                 slot++)
            {
                AORebirth.Core.Items.IItem item = weaponPage[slot];
                if (item != null
                    && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(item.LowID, item.HighID))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sacred garden key is permanent (not consumed on garden entry). Restore if missing
        /// when the account/character already earned the thrak-garden-key flag.
        /// </summary>
        internal static bool TryRestoreGardenKeyIfMissing(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasGardenKey(source))
            {
                return true;
            }

            if (!HasAccountGardenKeyFlag(source)
                && MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestReturn,
                       ThrakGardenKeyInteractionRules.KeyGrantedFlag) == null
                && MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestSouls,
                       ThrakGardenKeyInteractionRules.KeyGrantedFlag) == null)
            {
                return false;
            }

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.SacredGardenKeyItemId,
                1);
        }

        /// <summary>
        /// Move sacred key from HUD/WeaponPage slots into carried inventory (fixes stuck equip).
        /// </summary>
        internal static bool TryMoveSacredGardenKeyFromHudToInventory(ICharacter source)
        {
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            AORebirth.Core.Inventory.IInventoryPage weaponPage;
            if (!source.BaseInventory.Pages.TryGetValue(
                    (int)IdentityType.WeaponPage,
                    out weaponPage)
                || weaponPage == null)
            {
                return false;
            }

            AORebirth.Core.Inventory.IInventoryPage inventoryPage;
            if (!source.BaseInventory.Pages.TryGetValue(
                    (int)IdentityType.Inventory,
                    out inventoryPage)
                || inventoryPage == null)
            {
                return false;
            }

            bool moved = false;
            for (int slot = weaponPage.FirstSlotNumber;
                 slot < weaponPage.FirstSlotNumber + weaponPage.MaxSlots;
                 slot++)
            {
                AORebirth.Core.Items.IItem item = weaponPage[slot];
                if (item == null
                    || !ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(item.LowID, item.HighID))
                {
                    continue;
                }

                int free = inventoryPage.FindFreeSlot();
                if (free < 0)
                {
                    break;
                }

                try
                {
                    var slotHandler = weaponPage as AORebirth.Core.Inventory.IItemSlotHandler;
                    if (slotHandler == null)
                    {
                        break;
                    }

                    slotHandler.Unequip(slot, inventoryPage, free);
                    if (source.Controller != null && source.Controller.Client != null)
                    {
                        ZoneEngine.Core.Packets.UnEquip.Send(
                            source.Controller.Client,
                            weaponPage,
                            slot);
                    }

                    moved = true;
                }
                catch (Exception)
                {
                }
            }

            if (moved)
            {
                try
                {
                    source.BaseInventory.Write();
                }
                catch (Exception)
                {
                }
            }

            return moved;
        }

        /// <summary>
        /// After a client-side consumable DeleteItem on the sacred key, put it back immediately.
        /// </summary>
        internal static bool TryForceReturnGardenKey(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasGardenKey(source))
            {
                return true;
            }

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.SacredGardenKeyItemId,
                1);
        }

        /// <summary>
        /// Hypnagogic Urga-Lum Thrak only speaks during the garden Ancient-Device stage
        /// (QuestGarden / souls / active return). After the garden key quest is finished,
        /// dialog stays closed (Mike 20260821).
        /// </summary>
        internal static bool CanTalkToHypnagogic(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasCompletedGardenKeyQuest(source))
            {
                return false;
            }

            if (IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestGarden))
            {
                return true;
            }

            if (IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestSouls)
                || IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestSouls1)
                || IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestSouls2))
            {
                return true;
            }

            // Mid-chain: garden/souls completed but return trade not finished yet.
            if (IsMissionActive(source, ThrakGardenKeyInteractionRules.QuestReturn))
            {
                return true;
            }

            if (IsMissionCompleted(source, ThrakGardenKeyInteractionRules.QuestGarden)
                || IsMissionCompleted(source, ThrakGardenKeyInteractionRules.QuestSouls))
            {
                return true;
            }

            return GetSoulCount(source) > 0;
        }

        /// <summary>
        /// Finished Thrak garden key quest (account / mission completion flags only).
        /// Gates Son-Len shop/nano access (capture 20260718-210135).
        /// Inventory key alone is NOT completion — lost key still allows repurchase;
        /// holding a key without finishing the quest does not unlock Son-Len.
        /// </summary>
        internal static bool HasCompletedGardenKeyQuest(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasAccountGardenKeyFlag(source))
            {
                return true;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsMissionCompleted(source, ThrakGardenKeyInteractionRules.QuestReturn))
            {
                return true;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestReturn,
                       ThrakGardenKeyInteractionRules.KeyGrantedFlag) != null
                   || MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ThrakGardenKeyInteractionRules.QuestSouls,
                       ThrakGardenKeyInteractionRules.KeyGrantedFlag) != null;
        }

        private static bool HasAccountGardenKeyFlag(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                accountKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            }

            MissionAccountFlagRecord flag = MissionRuntime.Service.GetAccountFlag(
                accountKey,
                ThrakGardenKeyInteractionRules.AccountKeyFlag);
            return flag != null;
        }

        internal static int GetSoulCount(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                source.Identity.Instance,
                ThrakGardenKeyInteractionRules.QuestSouls,
                ThrakGardenKeyInteractionRules.SoulCountFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return 0;
            }

            int count;
            return int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                       ? Math.Max(0, count)
                       : 0;
        }

        /// <summary>
        /// Capture 20260718-185306: soul count advances on Silvertail trade finish.
        /// 1 → delete 5556591A + QFU 5556893B; 2 → delete 5556893B + QFU 5556893C;
        /// 3 → delete 5556893C + accept return 5556893D.
        /// </summary>
        internal static int IncrementSoulCount(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            int previous = GetSoulCount(source);
            if (previous >= 3)
            {
                return 3;
            }

            int next = previous + 1;
            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                ThrakGardenKeyInteractionRules.QuestSouls,
                ThrakGardenKeyInteractionRules.SoulCountFlag,
                next.ToString(CultureInfo.InvariantCulture));

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = source.Identity.Instance,
                    QuestId = ThrakGardenKeyInteractionRules.QuestSouls,
                    ObjectiveId = "mission_5556591A_souls",
                    ObservationKey = "cursed-silvertail-soul:" + next.ToString(CultureInfo.InvariantCulture),
                    Amount = 1,
                    EventType = "ThrakGardenKey:SoulClaimed",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = ThrakGardenKeyInteractionRules.CursedSilvertailName
                });

            if (next == 1)
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls);
                ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls1);
            }
            else if (next == 2)
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls1);
                ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls2);
            }
            else
            {
                ThrakGardenKeyPacketSender.TrySendQuestDelete(
                    source,
                    ThrakGardenKeyInteractionRules.QuestSouls2);
                // Persistence only — client already deleted 5556893C above.
                MissionRuntime.Service.CompleteMission(
                    source.Identity.Instance,
                    ThrakGardenKeyInteractionRules.QuestSouls);
                AcceptQuest(source, ThrakGardenKeyInteractionRules.QuestReturn);
            }

            return next;
        }

        /// <summary>
        /// Capture Silvertail trade: favored analyzer must stay with the player (Returned via
        /// RejectedItems Unknown2=1 for souls 1–2). Always re-materialize with TemplateAction.
        /// </summary>
        internal static bool TryForceReturnFavoredAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId);

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId,
                1);
        }

        /// <summary>
        /// Capture 20260821-225658 3rd Silvertail soul: RejectedItems Unknown2=0 then
        /// TemplateAction 214783 (empty/inspected Ancient Pattern Analyzer).
        /// </summary>
        internal static bool TryForceReturnInspectedAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId);

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId,
                1);
        }

        internal static bool HasFavoredAnalyzer(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId);
        }

        internal static bool HasInspectedAnalyzer(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId);
        }

        internal static bool HasAnalyzer(ICharacter source)
        {
            return source != null
                   && (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                           source,
                           ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId)
                       || HasInspectedAnalyzer(source)
                       || HasFavoredAnalyzer(source));
        }

        internal static bool HasInsignia(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId);
        }

        /// <summary>
        /// Always put Ancient Device (214998) back in inventory with client TemplateAction packets.
        /// Hyp trade UI can drop the offer client-side even when the server still holds it.
        /// </summary>
        internal static bool TryForceReturnAncientDevice(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasFavoredAnalyzer(source))
            {
                return true;
            }

            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(
                source,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId);

            return TryRestoreItem(
                source,
                ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId,
                1);
        }

        /// <summary>
        /// Ensure Ancient Device (214998) is present after Hyp inspect so it can be combined with Insignia.
        /// </summary>
        internal static bool TryRestoreAncientDeviceIfMissing(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasFavoredAnalyzer(source)
                || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId))
            {
                return true;
            }

            return TryForceReturnAncientDevice(source);
        }

        /// <summary>
        /// Recovery when Hyp trade left the player without Ancient Device.
        /// </summary>
        internal static bool TryRestoreAnalyzerIfMissing(ICharacter source)
        {
            return TryRestoreAncientDeviceIfMissing(source);
        }

        internal static bool TryRestoreItem(ICharacter source, int itemId, int quality)
        {
            if (source == null || itemId <= 0 || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                return true;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

            Item item;
            try
            {
                item = new Item(quality > 0 ? quality : 1, itemId, itemId);
            }
            catch (Exception)
            {
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                return false;
            }

            SendItemNotifications(source, item);
            return true;
        }

        private static void TryConsumeCarriedItem(ICharacter source, int itemId)
        {
            if (source == null || itemId <= 0 || source.BaseInventory == null)
            {
                return;
            }

            foreach (var pageEntry in source.BaseInventory.Pages)
            {
                var page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (var entry in page.List())
                {
                    var item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID != itemId && item.HighID != itemId)
                    {
                        continue;
                    }

                    page.Remove(entry.Key);
                    try
                    {
                        if (source.BaseInventory.Write())
                        {
                            try
                            {
                                ZoneEngine.Core.MessageHandlers.CharacterActionMessageHandler.Default.SendDeleteItem(
                                    source,
                                    pageEntry.Key,
                                    entry.Key);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            page.Add(entry.Key, item);
                        }
                    }
                    catch (Exception)
                    {
                        try
                        {
                            page.Add(entry.Key, item);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    return;
                }
            }
        }

        private static void SetAccountKeyFlag(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                accountKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            }

            MissionRuntime.Service.SetAccountFlag(
                characterId,
                accountKey,
                ThrakGardenKeyInteractionRules.QuestReturn,
                ThrakGardenKeyInteractionRules.AccountKeyFlag,
                "item:" + ThrakGardenKeyInteractionRules.SacredGardenKeyItemId);
        }

        private static bool TryGrantItem(ICharacter source, string questId, int itemId, string flagKey)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            bool alreadyFlagged = MissionRuntime.Service.GetFlag(characterId, questId, flagKey) != null;
            bool hasItem = InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                           && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                               source,
                               itemId);

            // Flag can survive quest delete / journal wipe while the item is gone — still restore.
            if (alreadyFlagged && hasItem)
            {
                return true;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

            if (!hasItem)
            {
                Item item;
                try
                {
                    item = new Item(1, itemId, itemId);
                }
                catch (Exception)
                {
                    return false;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    return false;
                }

                SendItemNotifications(source, item);
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                flagKey,
                "item:" + itemId);
            return flag.Status == MissionOperationStatus.Applied
                   || flag.Status == MissionOperationStatus.AlreadyApplied;
        }

        private static void SendItemNotifications(ICharacter source, Item item)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowID,
                    ItemHighId = item.HighID,
                    Quality = item.Quality,
                    Unknown1 = 1,
                    Unknown2 = 87,
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
                    TargetPlacement = 0x6F
                });
        }
    }
}
