namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    /// <summary>
    /// Capture 20260726-Antonio-Stacklund: recipe tip upload on dialogue choices.
    /// Capture 20260726-Antonio-1 / antonio-2: Overflow combine grants + tip Action59/Delete
    /// when the final upgraded weapon/gadget is created.
    /// </summary>
    internal static class AntonioStacklundQuestRuntime
    {
        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private static readonly string[] UpgradeMenuTipNodes =
            {
                "antonio_upgrade_assault",
                "antonio_upgrade_bat",
                "antonio_upgrade_blade",
                "antonio_upgrade_bow",
                "antonio_upgrade_pistol",
                "antonio_upgrade_dagger",
                "antonio_upgrade_energy",
                "antonio_upgrade_grenade",
                "antonio_upgrade_hammer",
                "antonio_upgrade_rifle",
                "antonio_upgrade_shotgun",
                "antonio_upgrade_smg",
                "antonio_upgrade_sword",
                "antonio_upgrade_oakbo",
                "antonio_upgrade_bat_energy",
                "antonio_upgrade_naja"
            };

        private static readonly string[] OtherMenuTipNodes =
            {
                "antonio_craft_bracer",
                "antonio_craft_vest",
                "antonio_craft_hud"
            };

        /// <summary>
        /// Final tip-objective item → Mission tip instance (Action59 + Quest Delete).
        /// Captured completions include Antonio-1/2 + 20260726-220219 finals
        /// (Shaolin Bow, Polished Eliminator, Injector Dagger, Poison Bracelet, Wailing Bat,
        /// Strong Oak Bo, Grip Blade, Leather Vest, …).
        /// </summary>
        private static readonly Dictionary<int, int> TipCompletionByResultId =
            new Dictionary<int, int>
            {
                { 248347, AntonioStacklundTipSender.AssaultRifleInstance },
                { 248341, AntonioStacklundTipSender.WailingBatInstance },
                { 248352, AntonioStacklundTipSender.GripBladeInstance },
                { 248354, AntonioStacklundTipSender.ShaolinBowInstance },
                { 248343, AntonioStacklundTipSender.ElectricalPistolInstance },
                { 248350, AntonioStacklundTipSender.InjectorDaggerInstance },
                { 248349, AntonioStacklundTipSender.WavePlasmaGunInstance },
                { 248346, AntonioStacklundTipSender.NiznoBombThrowerInstance },
                { 248353, AntonioStacklundTipSender.WarHammerInstance },
                { 248348, AntonioStacklundTipSender.CersetRifleInstance },
                { 248345, AntonioStacklundTipSender.PolishedEliminatorInstance },
                { 248344, AntonioStacklundTipSender.SilentSpitterInstance },
                { 248351, AntonioStacklundTipSender.SpineSwordInstance },
                { 301071, AntonioStacklundTipSender.StrongOakBoInstance },
                { 248355, AntonioStacklundTipSender.SurgeBatInstance },
                { 302602, AntonioStacklundTipSender.HandStaffNajaInstance },
                { 248374, AntonioStacklundTipSender.RangeMeterInstance },
                { 248375, AntonioStacklundTipSender.PoisonBracerInstance },
                { 248373, AntonioStacklundTipSender.LeatherVestInstance }
            };

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || string.IsNullOrEmpty(previousNodeId))
            {
                return false;
            }

            string tipNode = null;
            if (string.Equals(previousNodeId, "antonio_upgrade_menu", StringComparison.OrdinalIgnoreCase)
                && answerIndex >= 0
                && answerIndex < UpgradeMenuTipNodes.Length)
            {
                tipNode = UpgradeMenuTipNodes[answerIndex];
            }
            else if (string.Equals(previousNodeId, "antonio_other", StringComparison.OrdinalIgnoreCase)
                     && answerIndex >= 0
                     && answerIndex < OtherMenuTipNodes.Length)
            {
                tipNode = OtherMenuTipNodes[answerIndex];
            }

            if (tipNode == null)
            {
                return false;
            }

            if (AntonioStacklundTipSender.TrySendTipForNode(source, tipNode))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Antonio Stacklund recipe tip sent node="
                    + tipNode
                    + " from="
                    + previousNodeId
                    + " answer="
                    + answerIndex
                    + " character="
                    + source.Identity);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Capture 20260726-Antonio-1 / antonio-2: FormatFeedback + TemplateAction Overflow
        /// + ContainerAddItem — never AddTemplate for Adaptation Factory results.
        /// </summary>
        public static void SendCombineResultClientPackets(
            ICharacter source,
            Item sourceItem,
            Item targetItem,
            Item resultItem)
        {
            if (source?.Controller?.Client == null || resultItem == null)
            {
                return;
            }

            string feedback = string.Format(
                "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".",
                ResolveCombineItemName(sourceItem),
                ResolveCombineItemName(targetItem),
                resultItem.Quality,
                ResolveCombineItemName(resultItem));

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = feedback,
                    Unknown2 = 0
                });

            int itemId = resultItem.LowID > 0 ? resultItem.LowID : resultItem.HighID;
            SendOverflowGrantPackets(source, itemId, resultItem.Quality > 0 ? resultItem.Quality : 1);
        }

        public static void OnCombineSucceeded(ICharacter source, int resultLowId, int resultHighId)
        {
            if (source == null)
            {
                return;
            }

            int tipInstance;
            if (!TryResolveTipInstance(resultLowId, out tipInstance)
                && !TryResolveTipInstance(resultHighId, out tipInstance))
            {
                return;
            }

            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, tipInstance);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Antonio Stacklund tip complete result="
                + resultLowId
                + "/"
                + resultHighId
                + " mission=Mission:"
                + tipInstance.ToString("X8")
                + " character="
                + source.Identity);
        }

        private static bool TryResolveTipInstance(int resultId, out int tipInstance)
        {
            return TipCompletionByResultId.TryGetValue(resultId, out tipInstance);
        }

        private static string ResolveCombineItemName(Item item)
        {
            if (item == null)
            {
                return "item";
            }

            string name = TradeSkill.Instance.GetItemName(item.LowID, item.HighID, item.Quality);
            return string.IsNullOrEmpty(name) ? "item" : name;
        }

        private static void SendOverflowGrantPackets(ICharacter source, int itemId, int quality)
        {
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
        }
    }
}
