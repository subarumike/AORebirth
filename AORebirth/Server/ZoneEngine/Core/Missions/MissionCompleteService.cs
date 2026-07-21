namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Completes one accepted RK mission. Capture <c>20260718-062936</c> finish order:
    /// FormatFeedback (XP/credits) → TemplateAction side-token (Clan/Omni; skip Neutral) →
    /// CharacterAction(59) on Mission → Quest Delete → CharacterAction(0x2F)+Despawn MissionKey.
    /// </summary>
    internal static class MissionCompleteService
    {
        private const int MissionIdentityType = 0x0000DAC3;

        // Capture finish CharacterAction before Quest Delete (Action printed as 59).
        private const int MissionCompleteAction = 59;

        // Overflow next-free slot used for mission key + token grants in live captures.
        private const int OverflowNextFreeSlot = 111;

        private const int TemplateActionUnknown1 = 1;

        private const int TemplateActionUnknown2 = 87;

        // Clan token pair from finish TemplateAction (capture 20260718-062936).
        private const int ClanTokenLowId = 103910;

        private const int ClanTokenHighId = 103911;

        // Adjacent pair in itemrelations.txt (Omni); Clan pair confirmed by capture.
        private const int OmniTokenLowId = 103908;

        private const int OmniTokenHighId = 103909;

        private static readonly object Gate = new object();

        private static readonly HashSet<string> InFlight = new HashSet<string>();

        /// <summary>
        /// Completes the most recently accepted non-expired mission for the character (if any).
        /// </summary>
        public static bool TryCompleteLatest(IZoneClient client, ICharacter character, string reason)
        {
            if (character == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all.Count == 0)
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission entry = all[all.Count - 1];
            return TryComplete(client, character, entry, reason);
        }

        /// <summary>
        /// Completes when the player is inside a mission instance (LookAt / loot triggers).
        /// </summary>
        public static bool TryCompleteIfInInstance(IZoneClient client, ICharacter character, string reason)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            return TryCompleteLatest(client, character, reason);
        }

        /// <summary>
        /// Completes when the designated Kill-target NPC dies inside the instance.
        /// </summary>
        public static bool TryCompleteIfMissionTargetKilled(
            ICharacter attacker,
            ICharacter victim,
            string reason)
        {
            if (attacker == null || victim == null || !MissionTargetTracker.IsMissionTarget(victim.Identity))
            {
                return false;
            }

            if (victim.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(victim.Playfield.Identity.Instance))
            {
                return false;
            }

            ICharacter player = attacker;
            if (attacker.Controller == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            var client = attacker.Controller.Client as ZoneClient;
            if (client == null)
            {
                return false;
            }

            MissionTargetTracker.Unregister(victim.Identity);
            return TryCompleteLatest(client, player, reason ?? "KillTarget");
        }

        public static bool TryComplete(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission entry,
            string reason)
        {
            if (client == null || character == null || entry == null)
            {
                return false;
            }

            string flightKey = character.Identity.Instance.ToString("X") + ":"
                               + entry.QuestIdentity.Instance.ToString("X");
            lock (Gate)
            {
                if (!InFlight.Add(flightKey))
                {
                    return false;
                }
            }

            try
            {
                int cashReward = ResolveCashReward(entry);
                int xpReward = ResolveXpReward(entry);
                GrantCredits(character, cashReward);
                SendRewardFeedback(character, xpReward, cashReward);

                int tokenQl = entry.Quality > 0 ? entry.Quality : 1;
                TryGrantSideToken(character, tokenQl);

                SendMissionCompleteAction(character, entry.QuestIdentity);
                SendQuestDelete(character, entry.QuestIdentity);

                int keyInstance;
                bool keyRemoved = false;
                if (MissionKeyStore.TryTakeLatest(character.Identity.Instance, out keyInstance))
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
                }

                bool storeRemoved = MissionAcceptedStore.Remove(
                    character.Identity.Instance,
                    entry.QuestIdentity);

                MissionDiagnostics.Log(
                    "COMPLETE char={0} mission={1:X8} reason={2} cash={3} xp={4} keyRemoved={5} storeRemoved={6}",
                    character.Identity.Instance,
                    entry.QuestIdentity.Instance,
                    reason ?? string.Empty,
                    cashReward,
                    xpReward,
                    keyRemoved,
                    storeRemoved);

                return true;
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "COMPLETE-FAIL char={0} err={1}",
                    character.Identity.Instance,
                    ex.Message);
                return false;
            }
            finally
            {
                lock (Gate)
                {
                    InFlight.Remove(flightKey);
                }
            }
        }

        private static int ResolveCashReward(MissionAcceptedStore.AcceptedMission entry)
        {
            if (entry.Offer != null && entry.Offer.CashReward > 0)
            {
                return entry.Offer.CashReward;
            }

            // Fallback when offer shell had no cash field — capture finish paid 3748 at QL~42.
            int ql = entry.Quality > 0 ? entry.Quality : 1;
            return Math.Max(100, ql * 90);
        }

        private static int ResolveXpReward(MissionAcceptedStore.AcceptedMission entry)
        {
            if (entry.Offer != null && entry.Offer.ExperienceReward > 0)
            {
                return entry.Offer.ExperienceReward;
            }

            return 0;
        }

        private static void GrantCredits(ICharacter character, int cashReward)
        {
            if (cashReward <= 0 || character == null)
            {
                return;
            }

            long before = character.Stats[StatIds.cash].BaseValue;
            if (before < 0)
            {
                before = 0;
            }

            long after = before + cashReward;
            if (after > int.MaxValue)
            {
                after = int.MaxValue;
            }

            character.Stats[StatIds.cash].Set((uint)after);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)after);
        }

        private static void SendRewardFeedback(ICharacter character, int xp, int cash)
        {
            character.Send(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    FormattedMessage = string.Format(
                        "Received reward: {0} XP, {1} credits.",
                        xp,
                        cash)
                });
        }

        private static void TryGrantSideToken(ICharacter character, int quality)
        {
            Side side = (Side)character.Stats[StatIds.side].Value;
            int lowId;
            int highId;
            if (side == Side.Clan)
            {
                lowId = ClanTokenLowId;
                highId = ClanTokenHighId;
            }
            else if (side == Side.Omni)
            {
                lowId = OmniTokenLowId;
                highId = OmniTokenHighId;
            }
            else
            {
                MissionDiagnostics.Log(
                    "TOKEN-SKIP char={0} side={1} (neutral/other → no token)",
                    character.Identity.Instance,
                    side);
                return;
            }

            int count = MissionLevelTable.GetTokenReward(character.Stats[StatIds.level].Value);
            if (count <= 0)
            {
                count = 1;
            }

            bool inventoryOk = false;
            try
            {
                if (ItemLoader.ItemList.ContainsKey(lowId) && ItemLoader.ItemList.ContainsKey(highId)
                    && character.BaseInventory != null)
                {
                    IInventoryPage page;
                    if (character.BaseInventory.Pages.TryGetValue(
                        character.BaseInventory.StandardPage,
                        out page))
                    {
                        int slot = page.FindFreeSlot();
                        if (slot >= 0)
                        {
                            var item = new Item(quality, lowId, highId) { MultipleCount = count, Flags = 1 };
                            if (page.Add(slot, item) == InventoryError.OK)
                            {
                                character.BaseInventory.Write();
                                inventoryOk = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "TOKEN-INV-FAIL char={0} side={1} err={2}",
                    character.Identity.Instance,
                    side,
                    ex.Message);
            }

            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = lowId,
                    ItemHighId = highId,
                    Quality = quality,
                    Unknown1 = TemplateActionUnknown1,
                    Unknown2 = TemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = character.Identity.Instance
                             },
                    TargetPlacement = OverflowNextFreeSlot
                });

            MissionDiagnostics.Log(
                "TOKEN-GRANT char={0} side={1} low={2} high={3} ql={4} count={5} invOk={6}",
                character.Identity.Instance,
                side,
                lowId,
                highId,
                quality,
                count,
                inventoryOk);
        }

        private static void SendMissionCompleteAction(ICharacter character, Identity mission)
        {
            var missionId = new Identity
                            {
                                Type = mission.Type != 0
                                           ? mission.Type
                                           : (IdentityType)MissionIdentityType,
                                Instance = mission.Instance
                            };

            character.Send(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = (CharacterActionType)MissionCompleteAction,
                    Unknown1 = 0,
                    Target = missionId,
                    Parameter1 = MissionIdentityType,
                    Parameter2 = unchecked((int)missionId.Instance),
                    Unknown2 = 0
                });
        }

        private static void SendQuestDelete(ICharacter character, Identity mission)
        {
            character.Send(
                new QuestMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = QuestAction.Delete,
                    Unknown1 = 0,
                    Mission = new Identity
                              {
                                  Type = mission.Type != 0
                                             ? mission.Type
                                             : (IdentityType)MissionIdentityType,
                                  Instance = mission.Instance
                              },
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }
    }
}
