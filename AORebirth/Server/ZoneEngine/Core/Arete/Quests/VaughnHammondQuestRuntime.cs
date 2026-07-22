namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Capture 20260721-finish: Vaughn Hammond ID trade → leave FAQ → pad to ICC HQ.
    /// </summary>
    public static class VaughnHammondQuestRuntime
    {
        public const string IdOfferNodeId = "vaughn_001";

        public const string TradeHoldNodeId = "vaughn_trade";

        public const string TalkToVaughnQuestId = LoreleiQuestRuntime.TalkToVaughnQuestId;

        private const int AreteLandingPlayfieldId = 6553;

        private const int VaughnInstance = unchecked((int)0x78E0FC73);

        // Lorelei deliver reward — Identification Card (itemnames 296692) turned in at Vaughn.
        private const int IdCardItemId = 296692;

        // Older Arete chip ids that may still be in inventory on long-running chars.
        private const int LegacyUnprogrammedIdChipItemId = 296572;

        // Capture 20260722-233205 FormatFeedback: Received reward: 2581 XP, 1040 credits.
        private const int TalkToVaughnXpReward = 2581;

        private const int TalkToVaughnCreditReward = 1040;

        private const string TalkToVaughnRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!-5~";

        private const string TalkToVaughnXpCreditsFlag = "vaughn-talk-xp-credits-2581-1040";

        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Vaughn Hammond into one of the slots available and press \"accept\"";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, VaughnTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, VaughnTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private static readonly HashSet<int> ClearedForIccHqExitByCharacter = new HashSet<int>();

        // Capture 20260721-finish Use target Terminal:574187C3 (live AO StaticDynel).
        public const int ExitAreteLandingTerminalInstance = unchecked((int)0x574187C3);

        // playfields.dat Arete Terminal:C0001999 tpl=297303 — correct mesh heading (0,0.707,0,0.707).
        public const int ExitAreteLandingPlayfieldStatelInstance = unchecked((int)0xC0001999);

        private const int ExitAreteLandingTemplateId = 297303;

        private const int AndromedaIccHqPlayfieldId = 655;

        // Capture 20260721-finish N3Teleport envelope then CHAR-IN-PLAY plaza.
        private const float ExitEnvelopeX = 3364f;

        private const float ExitEnvelopeY = 18f;

        private const float ExitEnvelopeZ = 835f;

        private const float IccHqLandingX = 3337f;

        private const float IccHqLandingY = 36.1005f;

        private const float IccHqLandingZ = 866f;

        private const float IccHqLandingHeadingY = -0.5919532f;

        private const float IccHqLandingHeadingW = 0.8059723f;

        // Live tip wire before LoreleiTipSender remaps captured 555D68D8 → 555BEA06.
        private const int TalkToVaughnTipWireInstance = unchecked((int)0x555BEA06);

        private const int TalkToVaughnCapturedTipInstance = unchecked((int)0x555D68D8);

        private sealed class VaughnTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static bool IsExitAreteLandingTerminal(Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == ExitAreteLandingTerminalInstance
                || target.Instance == ExitAreteLandingPlayfieldStatelInstance)
            {
                return true;
            }

            // Any Arete Exit Arete Landing terminal (template 297303).
            PlayfieldData playfieldData;
            if (!PlayfieldLoader.PFData.TryGetValue(AreteLandingPlayfieldId, out playfieldData)
                || playfieldData == null
                || playfieldData.Statels == null)
            {
                return false;
            }

            for (int i = 0; i < playfieldData.Statels.Count; i++)
            {
                StatelData statel = playfieldData.Statels[i];
                if (statel != null
                    && statel.Identity.Type == IdentityType.Terminal
                    && statel.Identity.Instance == target.Instance
                    && statel.TemplateId == ExitAreteLandingTemplateId)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryHandleExitAreteLandingUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            if (client == null || message == null || !IsExitAreteLandingTerminal(target))
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            if (!IsClearedForIccHqExit(character) && !IsTalkToVaughnCompleted(character))
            {
                if (!TryHealExitClearanceAfterPriorTurnIn(character))
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    Log("exit-arete DENIED — talk/ID turn-in not complete char=" + character.Identity.ToString(true));
                    return true;
                }
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            var landing = new Coordinate(IccHqLandingX, IccHqLandingY, IccHqLandingZ);
            var heading = new Quaternion(0f, IccHqLandingHeadingY, 0f, IccHqLandingHeadingW);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            // Bind to ICC HQ before transfer so terminate cannot return to Arete.
            AndromedaIccHqArrivalSaveRuntime.ForceBindAtIccHq(character, "ExitAreteLanding");

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = AndromedaIccHqPlayfieldId },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedGatewayTransfer(
                    transferCharacter,
                    new Vector3(ExitEnvelopeX, ExitEnvelopeY, ExitEnvelopeZ),
                    new Vector3(IccHqLandingX, IccHqLandingY, IccHqLandingZ),
                    heading,
                    AndromedaIccHqPlayfieldId));
            Log("exit-arete→ICC HQ char=" + character.Identity.ToString(true));
            return true;
        }

        private static void MarkClearedForIccHqExit(ICharacter source)
        {
            if (source != null)
            {
                ClearedForIccHqExitByCharacter.Add(source.Identity.Instance);
            }
        }

        private static bool IsClearedForIccHqExit(ICharacter source)
        {
            return source != null && ClearedForIccHqExitByCharacter.Contains(source.Identity.Instance);
        }

        private static bool IsTalkToVaughnCompleted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, TalkToVaughnQuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        /// <summary>
        /// Heal chars that already gave Vaughn the ID but tip/mission did not persist
        /// (engine restart cleared in-memory exit flag).
        /// </summary>
        private static bool TryHealExitClearanceAfterPriorTurnIn(ICharacter character)
        {
            if (character == null || HasIdCard(character) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = character.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord vaughn =
                MissionRuntime.Service.GetMission(characterId, TalkToVaughnQuestId);

            if (vaughn != null
                && (vaughn.State == MissionLifecycleState.Active
                    || vaughn.State == MissionLifecycleState.Offered
                    || vaughn.State == MissionLifecycleState.Completed))
            {
                CompleteTalkToVaughn(character);
                MarkClearedForIccHqExit(character);
                Log("exit-arete HEAL from TalkToVaughn state=" + vaughn.State);
                return true;
            }

            // Tip was client-only / mission row missing, but Lorelei deliver finished (ID earned).
            ZoneEngine.Core.Missions.MissionStateRecord deliver =
                MissionRuntime.Service.GetMission(characterId, LoreleiQuestRuntime.DeliverQuestId);
            if (deliver != null && deliver.State == MissionLifecycleState.Completed)
            {
                CompleteTalkToVaughn(character);
                MarkClearedForIccHqExit(character);
                Log("exit-arete HEAL from Deliver completed (ID already gone)");
                return true;
            }

            return false;
        }

        public static bool TryBeginVaughnTrade(ICharacter source, Identity vaughnIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (vaughnIdentity.Type != IdentityType.CanbeAffected || vaughnIdentity.Instance == 0)
            {
                vaughnIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = VaughnInstance
                                };
            }

            BeginTrade(source, vaughnIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(source, vaughnIdentity, TradePrompt, 1);
            Log("vaughn-start-trade character=" + source.Identity.ToString(true));
            return true;
        }

        public static bool TryStageVaughnTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsVaughnNpc(character, message.Target))
            {
                return false;
            }

            if (!HasIdCard(character)
                && !IsTalkToVaughnActive(character)
                && GetTradeSession(character) == null)
            {
                return false;
            }

            BeginTrade(character, message.Target);
            VaughnTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "vaughn-trade-staged character="
                    + character.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true));
            }

            return true;
        }

        public static bool ShouldSuppressGenericVaughnTradeRemove(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsVaughnNpc(character, message.Target))
            {
                return false;
            }

            return HasIdCard(character) || IsTalkToVaughnActive(character) || GetTradeSession(character) != null;
        }

        public static bool TryFinishVaughnTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsVaughnNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            VaughnTradeSession session = GetTradeSession(source);
            Identity staged = session != null ? session.StagedContainer : Identity.None;
            ApplyIdTurnIn(source, message.Target, staged);
            return true;
        }

        private static void ApplyIdTurnIn(ICharacter source, Identity vaughnTarget, Identity staged)
        {
            int instance = source.Identity.Instance;
            lock (TradeSyncRoot)
            {
                if (!TurnInInFlightByCharacter.Add(instance))
                {
                    return;
                }
            }

            try
            {
                // Capture 20260721-finish: empty Accept still RejectedItems=0 → leave options.
                // Consume Lorelei Identification Card 296692 (legacy chip 296572 fallback).
                if (!TryConsumeInventoryItem(source, staged, IdCardItemId)
                    && !TryConsumeInventoryItem(source, Identity.None, IdCardItemId)
                    && !TryConsumeInventoryItem(source, staged, LegacyUnprogrammedIdChipItemId))
                {
                    TryConsumeInventoryItem(source, Identity.None, LegacyUnprogrammedIdChipItemId);
                }

                KnuBotRejectedItemsMessageHandler.Default.Send(source, vaughnTarget, new Item[0], 0);
                CompleteTalkToVaughn(source);
                MarkClearedForIccHqExit(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, vaughnTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, vaughnTarget);
                    }
                }
                catch
                {
                }

                Log("vaughn-id-turnin done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void CompleteTalkToVaughn(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            // Capture 20260721-finish: Quest Delete on Accept (client tip must clear).
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, TalkToVaughnTipWireInstance);
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, TalkToVaughnCapturedTipInstance);

            // Stuck Remain 00:00 Arete leftovers (Talk to Sarah / Buy Nano) — clear on turn-in,
            // not only after ICC arrival (player may still be blocked from leaving).
            AndromedaIccHqArrivalSaveRuntime.ClearStuckAreteTips(source);

            if (!MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, TalkToVaughnQuestId);

            // Tip-only / missing mission row: still force completed so exit survives restart.
            if (mission == null)
            {
                MissionRuntime.Service.OfferMission(characterId, TalkToVaughnQuestId);
                MissionRuntime.Service.AcceptMission(characterId, TalkToVaughnQuestId);
                mission = MissionRuntime.Service.GetMission(characterId, TalkToVaughnQuestId);
            }

            if (mission == null || mission.State == MissionLifecycleState.Completed)
            {
                return;
            }

            if (mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, TalkToVaughnQuestId);
                mission = MissionRuntime.Service.GetMission(characterId, TalkToVaughnQuestId);
            }

            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return;
            }

            ApplyTalkToVaughnXpCredits(source);
            TrySendTalkToVaughnRewardFeedback(source);

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = TalkToVaughnQuestId,
                    ObjectiveId = "mission_555BEA06_talk_vaughn",
                    ObservationKey = "vaughn-force-complete",
                    Amount = 1,
                    EventType = "VaughnHammondQuestRuntime",
                    SourceIdentity = string.Empty,
                    TargetIdentity = string.Empty
                });
            MissionRuntime.Service.CompleteMission(characterId, TalkToVaughnQuestId);
        }

        private static void ApplyTalkToVaughnXpCredits(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, TalkToVaughnQuestId, TalkToVaughnXpCreditsFlag)
                != null)
            {
                return;
            }

            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-vaughn-talk-xp-credits",
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
                                                                Value = TalkToVaughnCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = TalkToVaughnXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = TalkToVaughnXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                characterId,
                TalkToVaughnQuestId,
                definition,
                "capture:20260722-233205:vaughn-talk-xp-credits");
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
            MissionRuntime.Service.SetFlag(
                characterId,
                TalkToVaughnQuestId,
                TalkToVaughnXpCreditsFlag,
                "xp:" + TalkToVaughnXpReward + "+credits:" + TalkToVaughnCreditReward);
        }

        private static void TrySendTalkToVaughnRewardFeedback(ICharacter source)
        {
            if (source?.Controller?.Client == null)
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
                        FormattedMessage = TalkToVaughnRewardFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception ex)
            {
                Log("vaughn reward feedback failed: " + ex.Message);
            }
        }

        private static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new VaughnTradeSession
                                                                    {
                                                                        NpcIdentity = npcIdentity,
                                                                        StagedContainer = Identity.None
                                                                    };
            }
        }

        private static VaughnTradeSession GetTradeSession(ICharacter source)
        {
            lock (TradeSyncRoot)
            {
                VaughnTradeSession session;
                return TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        private static void ForgetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool IsVaughnNpc(ICharacter source, Identity target)
        {
            if (source == null
                || source.Playfield == null
                || source.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(source.Controller is PlayerController))
            {
                return false;
            }

            if (target.Type == IdentityType.CanbeAffected && target.Instance == VaughnInstance)
            {
                return true;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(target);
            INamedEntity named = npc as INamedEntity;
            return named != null
                   && string.Equals(named.Name, "Vaughn Hammond", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTalkToVaughnActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, TalkToVaughnQuestId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool HasIdCard(ICharacter source)
        {
            return HasInventoryItem(source, IdCardItemId)
                   || HasInventoryItem(source, LegacyUnprogrammedIdChipItemId);
        }

        private static bool HasInventoryItem(ICharacter source, int itemId)
        {
            Identity found;
            return TryFindItemContainer(source, itemId, out found);
        }

        private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance >= 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null && (staged.LowID == itemId || staged.HighID == itemId))
                    {
                        source.BaseInventory.RemoveItem((int)stagedContainer.Type, stagedContainer.Instance);
                        PersistInventoryAfterConsume(source);
                        CharacterActionMessageHandler.Default.SendDeleteItem(
                            source,
                            (int)stagedContainer.Type,
                            stagedContainer.Instance);
                        return true;
                    }
                }
            }

            Identity found;
            if (!TryFindItemContainer(source, itemId, out found))
            {
                return false;
            }

            source.BaseInventory.RemoveItem((int)found.Type, found.Instance);
            PersistInventoryAfterConsume(source);
            CharacterActionMessageHandler.Default.SendDeleteItem(source, (int)found.Type, found.Instance);
            return true;
        }

        private static void PersistInventoryAfterConsume(ICharacter source)
        {
            try
            {
                if (source != null && source.BaseInventory != null)
                {
                    source.BaseInventory.Write();
                }
            }
            catch (Exception ex)
            {
                Log("id-consume inventory write failed: " + ex.Message);
            }
        }

        private static bool TryFindItemContainer(ICharacter source, int itemId, out Identity container)
        {
            container = Identity.None;
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in page.List())
                {
                    IItem item = slot.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID == itemId || item.HighID == itemId)
                    {
                        container = new Identity { Type = (IdentityType)pageEntry.Key, Instance = slot.Key };
                        return true;
                    }
                }
            }

            return false;
        }

        private static void Log(string message)
        {
            try
            {
                LogUtil.Debug(DebugInfoDetail.Engine, "VaughnHammondQuestRuntime " + message);
            }
            catch
            {
            }
        }
    }
}
