namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Generic;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;

public enum DialogueActionOutcome { Rejected, Continue, StanTrade, DojaTrade, Vendor, SarahTrade }

/// <summary>
/// Explicit domain links, not a replacement quest engine. An unported required quest gate cannot
/// be converted into a text-only success. All mutations remain in the existing authored service.
/// </summary>
public sealed class DialogueActionRouter(AuthoredQuestService quests)
{
    public const string Stan = "SimpleChar:78E0FC65", Scarlett = "SimpleChar:7A18B924";
    public const string Tailor = "SimpleChar:79135F51", Zyvania = "SimpleChar:7976BCF3";
    public const string Sarah = "SimpleChar:78E0FC69";
    public const string Marco = "SimpleChar:78E0FC81";

    static readonly HashSet<string> CraigOr = new(StringComparer.Ordinal)
    { "SimpleChar:79758F3F", "SimpleChar:79758F3E", "SimpleChar:79758F3B", "SimpleChar:79758F3C", "SimpleChar:79758F3D" };
    static readonly HashSet<string> OrMada = new(StringComparer.Ordinal)
    { "SimpleChar:7A2013B7", "SimpleChar:7A2013B4", "SimpleChar:7A2013B5", "SimpleChar:7A2013B8", "SimpleChar:7A2013B6", "SimpleChar:7A2013B9" };

    public bool TryOpen(Player player, string contentIdentity, bool hasPriorOpen, out string? startNode)
    {
        startNode = null;
        if (contentIdentity == Stan) return quests.TryResolveStanDialogueStart(player, out startNode);
        if (contentIdentity == Sarah) return quests.TryResolveSarahDialogueStart(player, out startNode);
        if (contentIdentity == Tailor)
        { startNode = CapturedSubwayTailorDialogueContent.ResolveRootNodeId(hasPriorOpen); return true; }
        // Zyvania has an accepted transport effect; text-only continuation is not an implementation.
        return contentIdentity is Scarlett or Marco || CraigOr.Contains(contentIdentity) || OrMada.Contains(contentIdentity);
    }

    public DialogueActionOutcome ApplyAnswer(Player player, string contentIdentity, string previousNode, int answerIndex)
    {
        if (contentIdentity == Stan && previousNode == "stan_goldman_003" && answerIndex == 0)
            return quests.AcceptStanJob(player) ? DialogueActionOutcome.Continue : DialogueActionOutcome.Rejected;
        if (contentIdentity == Stan && previousNode == "stan_deliver_001" && answerIndex == 0)
            return DialogueActionOutcome.StanTrade;
        if (contentIdentity == Sarah && previousNode == "sarah_greene_001" && answerIndex == 0)
            return quests.AcceptSarahJob(player) ? DialogueActionOutcome.Continue : DialogueActionOutcome.Rejected;
        if (contentIdentity == Sarah && previousNode == "sarah_deliver_001" && answerIndex == 0)
            return DialogueActionOutcome.SarahTrade;
        if (contentIdentity == Scarlett && previousNode == "scarlett_001" && answerIndex == 0)
            return quests.CanOpenDojaTrade(player) ? DialogueActionOutcome.DojaTrade : DialogueActionOutcome.Rejected;
        if (contentIdentity == Tailor && previousNode == CapturedSubwayTailorDialogueContent.MeasurementNodeId)
            return quests.TryGrantTailorMeasurement(player, answerIndex) ? DialogueActionOutcome.Continue : DialogueActionOutcome.Rejected;
        if (answerIndex == 0 && ((CraigOr.Contains(contentIdentity) && previousNode == "craig_or_001")
            || (OrMada.Contains(contentIdentity) && previousNode == "or_mada_001"))) return DialogueActionOutcome.Vendor;
        return DialogueActionOutcome.Continue;
    }

    public bool CompleteTrade(Player player, DialogueActionOutcome trade, SmokeLounge.AOtomation.Messaging.GameData.Identity slot,
        ZoneEngine_New.Core.Inventory.Item item, Action publishAccepted)
        => trade switch
        {
            DialogueActionOutcome.DojaTrade => quests.TryTurnInDoja(player, slot, item, publishAccepted),
            DialogueActionOutcome.StanTrade => quests.TryTurnInFactory(player, slot, item, publishAccepted),
            DialogueActionOutcome.SarahTrade => quests.TryTurnInDnaArmor(player, slot, item, publishAccepted),
            _ => false
        };
}
