#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.PacketHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    #endregion

    /// <summary>
    /// </summary>
    public static class TradeSkillReceiver
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        private static readonly List<TradeSkillInfo> TradeSkillInfos = new List<TradeSkillInfo>();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <param name="newItem">
        /// </param>
        /// <returns>
        /// </returns>
        public static string SuccessMessage(Item sourceItem, Item targetItem, Item newItem)
        {
            return string.Format(
                "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".",
                TradeSkill.Instance.GetItemName(sourceItem.LowID, sourceItem.HighID, sourceItem.Quality),
                TradeSkill.Instance.GetItemName(targetItem.LowID, targetItem.HighID, targetItem.Quality),
                newItem.Quality,
                TradeSkill.Instance.GetItemName(newItem.LowID, newItem.HighID, newItem.Quality));
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="quality">
        /// </param>
        public static void TradeSkillBuildPressed(IZoneClient client, int quality)
        {
            TradeSkillInfo source = client.Controller.Character.TradeSkillSource;
            TradeSkillInfo target = client.Controller.Character.TradeSkillTarget;

            Item sourceItem =
                InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, source);
            Item targetItem =
                InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, target);

            if (sourceItem == null || targetItem == null)
            {
                ChatTextMessageHandler.Default.Send(
                    client.Controller.Character,
                    "It is not possible to assemble those two items. Maybe the order was wrong?");
                return;
            }

            TradeSkillMatch match = TradeSkill.Instance.ResolveTradeSkill(
                sourceItem.LowID,
                sourceItem.HighID,
                targetItem.LowID,
                targetItem.HighID);
            TradeSkillEntry ts = match != null ? match.Entry : null;

            if (ts != null)
            {
                Item implantItem = match.ImplantOrTargetItem(sourceItem, targetItem);

                NormalizeResultTemplateOrder(ts);

                int resultMaxQl = 1;
                int resultHighForQl = ts.ResultHighId;
                if (!ItemLoader.ItemList.ContainsKey(resultHighForQl)
                    && ItemLoader.ItemList.ContainsKey(ts.ResultLowId))
                {
                    resultHighForQl = ts.ResultLowId;
                    ts.ResultHighId = ts.ResultLowId;
                }

                if (ItemLoader.ItemList.ContainsKey(resultHighForQl))
                {
                    resultMaxQl = ItemLoader.ItemList[resultHighForQl].Quality;
                }

                // UseItemOnItem passes quality < 0 → build at implant QL (+ NanoProg bump).
                if (quality < 0)
                {
                    quality = DeriveResultQuality(client, match, implantItem, resultMaxQl);
                }
                else
                {
                    quality = Math.Min(quality, resultMaxQl);
                }

                string failReason;
                if (!WindowBuild(client, quality, match, sourceItem, targetItem, out failReason))
                {
                    ChatTextMessageHandler.Default.Send(
                        client.Controller.Character,
                        string.IsNullOrEmpty(failReason)
                            ? "It is not possible to assemble those two items. Maybe the order was wrong?"
                            : failReason);
                    return;
                }

                Item newItem;
                try
                {
                    newItem = new Item(quality, ts.ResultLowId, ts.ResultHighId);
                }
                catch (ArgumentOutOfRangeException)
                {
                    try
                    {
                        // Capture 20260721-001538 QL1 grants use low==high (156026/156026).
                        newItem = new Item(quality, ts.ResultLowId, ts.ResultLowId);
                        ts.ResultHighId = ts.ResultLowId;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            "Combine failed: result item template is missing on the server.");
                        return;
                    }
                }

                bool vernonLibraryHack =
                    ZoneEngine.Core.Arete.Quests.VernonGodfrayCombineRules
                        .IsHackedTechnicalLibrary(newItem.LowID, newItem.HighID);
                bool masonAssemble =
                    ZoneEngine.Core.Arete.Quests.DoctorMasonCombineRules
                        .IsAssembleResult(newItem.LowID, newItem.HighID);
                // Capture: Overflow grants do not need a free inventory slot first, but our
                // server TryAdd does. Consume inputs before add when both are deleted (Mason)
                // or when Vernon consumes the library.
                if (vernonLibraryHack || masonAssemble)
                {
                    // Capture Mason results are always QL1 Overflow (even with QL5 clusters).
                    if (masonAssemble && newItem.Quality != 1)
                    {
                        try
                        {
                            newItem = new Item(1, newItem.LowID, newItem.HighID);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // Keep prior quality if template clamp rejects QL1.
                        }
                    }

                    ConsumeInputsByDeleteFlag(client, match, source, target);
                }

                InventoryError inventoryError =
                    InventoryContainerRuntimeService.Default.AddTradeSkillResultItem(
                        client.Controller.Character,
                        newItem);
                if (inventoryError != InventoryError.OK)
                {
                    ChatTextMessageHandler.Default.Send(
                        client.Controller.Character,
                        "Your inventory is full. Free a slot and try the combine again.");
                    return;
                }

                if (vernonLibraryHack)
                {
                    // Capture 20260721-Vernon-Godfray: FormatFeedback + TemplateAction
                    // Overflow + ContainerAddItem — never AddTemplate (client crash).
                    ZoneEngine.Core.Arete.Quests.VernonGodfrayQuestRuntime
                        .SendCombineResultClientPackets(
                            client.Controller.Character,
                            sourceItem,
                            targetItem,
                            newItem);
                }
                else if (masonAssemble)
                {
                    // Capture 20260721-Mason: FormatFeedback + Overflow TemplateAction.
                    ZoneEngine.Core.Arete.Quests.DoctorMasonQuestRuntime
                        .SendCombineResultClientPackets(
                            client.Controller.Character,
                            sourceItem,
                            targetItem,
                            newItem);
                }
                else
                {
                    AddTemplateMessageHandler.Default.Send(client.Controller.Character, newItem);

                    ConsumeInputsByDeleteFlag(client, match, source, target);

                    ChatTextMessageHandler.Default.Send(
                        client.Controller.Character,
                        SuccessMessage(sourceItem, targetItem, newItem));

                    client.Controller.Character.Stats[StatIds.xp].Value += CalculateXP(quality, ts);
                }

                ZoneEngine.Core.Arete.Quests.PersonalizedRobotBrainQuestRuntime.OnCombineSucceeded(
                    client.Controller.Character,
                    newItem.LowID,
                    newItem.HighID);
                ZoneEngine.Core.Arete.Quests.VernonGodfrayQuestRuntime.OnCombineSucceeded(
                    client.Controller.Character,
                    newItem.LowID,
                    newItem.HighID);
                ZoneEngine.Core.Arete.Quests.DoctorMasonQuestRuntime.OnCombineSucceeded(
                    client.Controller.Character,
                    newItem.LowID,
                    newItem.HighID);
                ZoneEngine.Core.Arete.Quests.LoreleiQuestRuntime.OnCombineSucceeded(
                    client.Controller.Character,
                    newItem.LowID,
                    newItem.HighID);
            }
            else
            {
                ChatTextMessageHandler.Default.Send(
                    client.Controller.Character,
                    "No tradeskill recipe for those two items (check cluster slot matches the implant).");
                ChatTextMessageHandler.Default.Send(
                    client.Controller.Character,
                    "Tried "
                    + sourceItem.LowID
                    + "/"
                    + sourceItem.HighID
                    + " QL"
                    + sourceItem.Quality
                    + " + "
                    + targetItem.LowID
                    + "/"
                    + targetItem.HighID
                    + " QL"
                    + targetItem.Quality);

            }
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        public static void TradeSkillSourceChanged(IZoneClient client, int container, int placement)
        {
            // Clear signal is container=0 (placement may also be 0). Placement 0 is a valid inventory slot.
            if (container != 0)
            {
                Item item = InventoryContainerRuntimeService.Default.SetTradeSkillSource(
                    client.Controller.Character,
                    container,
                    placement);
                if (item == null)
                {
                    InventoryContainerRuntimeService.Default.ClearTradeSkillSource(client.Controller.Character);
                    return;
                }

                TradeSkillPacket.SendSource(
                    client.Controller.Character,
                    TradeSkill.Instance.SourceProcessesCount(item.LowID, item.HighID));

                TradeSkillChanged(client);
            }
            else
            {
                InventoryContainerRuntimeService.Default.ClearTradeSkillSource(client.Controller.Character);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        public static void TradeSkillTargetChanged(IZoneClient client, int container, int placement)
        {
            if (container != 0)
            {
                Item item = InventoryContainerRuntimeService.Default.SetTradeSkillTarget(
                    client.Controller.Character,
                    container,
                    placement);
                if (item == null)
                {
                    InventoryContainerRuntimeService.Default.ClearTradeSkillTarget(client.Controller.Character);
                    return;
                }

                TradeSkillPacket.SendTarget(
                    client.Controller.Character,
                    TradeSkill.Instance.TargetProcessesCount(item.LowID, item.HighID));

                TradeSkillChanged(client);
            }
            else
            {
                InventoryContainerRuntimeService.Default.ClearTradeSkillTarget(client.Controller.Character);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="quality">
        /// </param>
        /// <param name="ts">
        /// </param>
        /// <returns>
        /// </returns>
        private static int CalculateXP(int quality, TradeSkillEntry ts)
        {
            int absMinQL = ItemLoader.ItemList[ts.ResultLowId].Quality;
            int absMaxQL = ItemLoader.ItemList[ts.ResultHighId].Quality;
            if (absMinQL > absMaxQL)
            {
                int swap = absMinQL;
                absMinQL = absMaxQL;
                absMaxQL = swap;
            }

            if (absMaxQL == absMinQL)
            {
                return ts.MaxXP;
            }

            return
                (int)
                    Math.Floor(
                        (double)((ts.MaxXP - ts.MinXP) / (absMaxQL - absMinQL)) * (quality - absMinQL) + ts.MinXP);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        private static void TradeSkillChanged(IZoneClient client)
        {
            TradeSkillInfo source = client.Controller.Character.TradeSkillSource;
            TradeSkillInfo target = client.Controller.Character.TradeSkillTarget;

            if ((source != null) && (target != null))
            {
                Item sourceItem =
                    InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, source);
                Item targetItem =
                    InventoryContainerRuntimeService.Default.GetTradeSkillItem(client.Controller.Character, target);
                if (sourceItem == null || targetItem == null)
                {
                    TradeSkillPacket.SendNotTradeskill(client.Controller.Character);
                    return;
                }

                TradeSkillMatch match = TradeSkill.Instance.ResolveTradeSkill(
                    sourceItem.LowID,
                    sourceItem.HighID,
                    targetItem.LowID,
                    targetItem.HighID);
                if (match != null)
                {
                    TradeSkillEntry ts = match.Entry;
                    NormalizeResultTemplateOrder(ts);
                    Item clusterItem = match.ClusterItem(sourceItem, targetItem);
                    Item implantItem = match.ImplantOrTargetItem(sourceItem, targetItem);

                    if (ts.ValidateRange(clusterItem.Quality, implantItem.Quality))
                    {
                        foreach (TradeSkillSkill tsi in ts.Skills)
                        {
                            int skillReq = (int)Math.Ceiling(tsi.Percent / 100M * implantItem.Quality);
                            if (skillReq > client.Controller.Character.Stats[tsi.StatId].Value)
                            {
                                TradeSkillPacket.SendRequirement(client.Controller.Character, tsi.StatId, skillReq);
                            }
                        }

                        int leastbump = 0;
                        int maxbump = GetImplantMaxBump(ts, implantItem.Quality);

                        foreach (TradeSkillSkill tsSkill in ts.Skills)
                        {
                            if (tsSkill.SkillPerBump != 0)
                            {
                                leastbump =
                                    Math.Min(
                                        Convert.ToInt32(
                                            (client.Controller.Character.Stats[tsSkill.StatId].Value
                                             - (tsSkill.Percent / 100M * implantItem.Quality)) / tsSkill.SkillPerBump),
                                        maxbump);
                            }
                        }

                        int resultHighId = ts.ResultHighId;
                        int resultLowId = ts.ResultLowId;
                        if (!ItemLoader.ItemList.ContainsKey(resultHighId)
                            && ItemLoader.ItemList.ContainsKey(resultLowId))
                        {
                            resultHighId = resultLowId;
                        }

                        int resultMaxQl = ItemLoader.ItemList.ContainsKey(resultHighId)
                                              ? ItemLoader.ItemList[resultHighId].Quality
                                              : implantItem.Quality;
                        TradeSkillPacket.SendResult(
                            client.Controller.Character,
                            implantItem.Quality,
                            Math.Min(implantItem.Quality + leastbump, resultMaxQl),
                            resultLowId,
                            resultHighId);
                    }
                    else
                    {
                        TradeSkillPacket.SendOutOfRange(
                            client.Controller.Character,
                            Convert.ToInt32(
                                Math.Round(
                                    (double)implantItem.Quality
                                    - ts.QLRangePercent * implantItem.Quality / 100)));
                    }
                }
                else
                {
                    TradeSkillPacket.SendNotTradeskill(client.Controller.Character);
                }
            }
        }

        /// <summary>
        /// items.dat Low/High AOIDs are not always ascending by AOID (e.g. Carbonrich Ore
        /// 144770=QL1, 144768=QL255). Ensure ResultLowId is the lower-QL template.
        /// </summary>
        private static void NormalizeResultTemplateOrder(TradeSkillEntry ts)
        {
            if (ts == null)
            {
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(ts.ResultLowId)
                || !ItemLoader.ItemList.ContainsKey(ts.ResultHighId))
            {
                return;
            }

            int lowQl = ItemLoader.ItemList[ts.ResultLowId].Quality;
            int highQl = ItemLoader.ItemList[ts.ResultHighId].Quality;
            if (lowQl <= highQl)
            {
                return;
            }

            int swapId = ts.ResultLowId;
            ts.ResultLowId = ts.ResultHighId;
            ts.ResultHighId = swapId;
        }

        /// <summary>
        /// AO-Universe / UseItemOnItem: result QL = implant QL + excess NanoProg bumps.
        /// </summary>
        private static int DeriveResultQuality(
            IZoneClient client,
            TradeSkillMatch match,
            Item implantItem,
            int resultMaxQl)
        {
            TradeSkillEntry ts = match.Entry;
            int bump = 0;
            int maxbump = GetImplantMaxBump(ts, implantItem.Quality);
            foreach (TradeSkillSkill tsSkill in ts.Skills)
            {
                if (tsSkill.SkillPerBump == 0)
                {
                    continue;
                }

                int req = (int)Math.Ceiling(tsSkill.Percent / 100M * implantItem.Quality);
                int have = client.Controller.Character.Stats[tsSkill.StatId].Value;
                if (have > req)
                {
                    bump = Math.Min((have - req) / tsSkill.SkillPerBump, maxbump);
                }
            }

            return Math.Min(implantItem.Quality + Math.Max(0, bump), resultMaxQl);
        }

        /// <summary>
        /// AO-Universe implant QL bump caps; DB MaxBump used when lower and &gt; 0.
        /// </summary>
        private static int GetImplantMaxBump(TradeSkillEntry ts, int implantQuality)
        {
            if (ts == null || !ts.IsImplant)
            {
                return 0;
            }

            int tierCap = 0;
            if (implantQuality >= 250)
            {
                tierCap = 5;
            }
            else if (implantQuality >= 201)
            {
                tierCap = 4;
            }
            else if (implantQuality >= 150)
            {
                tierCap = 3;
            }
            else if (implantQuality >= 100)
            {
                tierCap = 2;
            }
            else if (implantQuality >= 50)
            {
                tierCap = 1;
            }

            if (ts.MaxBump > 0 && ts.MaxBump < tierCap)
            {
                return ts.MaxBump;
            }

            return tierCap;
        }

        /// <summary>
        /// DeleteFlag bit0 = DB Id1 (cluster), bit1 = DB Id2 (implant). Map to UI slots when swapped.
        /// </summary>
        private static void ConsumeInputsByDeleteFlag(
            IZoneClient client,
            TradeSkillMatch match,
            TradeSkillInfo source,
            TradeSkillInfo target)
        {
            TradeSkillEntry ts = match.Entry;
            TradeSkillInfo id1Slot = match.Swapped ? target : source;
            TradeSkillInfo id2Slot = match.Swapped ? source : target;

            if ((ts.DeleteFlag & 1) == 1)
            {
                InventoryContainerRuntimeService.Default.RemoveTradeSkillItem(client.Controller.Character, id1Slot);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    client.Controller.Character,
                    id1Slot.Container,
                    id1Slot.Placement);
            }

            if ((ts.DeleteFlag & 2) == 2)
            {
                InventoryContainerRuntimeService.Default.RemoveTradeSkillItem(client.Controller.Character, id2Slot);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    client.Controller.Character,
                    id2Slot.Container,
                    id2Slot.Placement);
            }
        }

        private static bool WindowBuild(
            IZoneClient client,
            int desiredQuality,
            TradeSkillMatch match,
            Item sourceItem,
            Item targetItem,
            out string failReason)
        {
            failReason = null;
            TradeSkillEntry ts = match.Entry;
            Item clusterItem = match.ClusterItem(sourceItem, targetItem);
            Item implantItem = match.ImplantOrTargetItem(sourceItem, targetItem);

            if (!((ts.MinTargetQL >= implantItem.Quality) || (ts.MinTargetQL == 0)))
            {
                failReason = "Target implant QL is too low for this recipe.";
                return false;
            }

            if (!ts.ValidateRange(clusterItem.Quality, implantItem.Quality))
            {
                int minClusterQl = Convert.ToInt32(
                    Math.Round(
                        (double)implantItem.Quality - ts.QLRangePercent * implantItem.Quality / 100));
                failReason =
                    "Cluster QL too low for that implant (need about QL "
                    + Math.Max(1, minClusterQl)
                    + "+ for implant QL "
                    + implantItem.Quality
                    + ").";
                return false;
            }

            foreach (TradeSkillSkill tss in ts.Skills)
            {
                int need = (int)Math.Ceiling(tss.Percent / 100M * implantItem.Quality);
                int have = client.Controller.Character.Stats[tss.StatId].Value;
                if (have < need)
                {
                    failReason =
                        "Need "
                        + need
                        + " in skill "
                        + tss.StatId
                        + " (you have "
                        + have
                        + ") for implant QL "
                        + implantItem.Quality
                        + ".";
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
