namespace ZoneEngine_New.Tests;
using System;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;

// Historical content identities live only in test expectations and editable runtime data.
internal static class AuthoredQuestFixture
{
    internal const string TalkStan="Mission:555B4366", BuyLockpick="Mission:555BD124", Strongbox="Mission:555BE9C5";
    internal const string DeliverFactory="Mission:555BE9F2", TalkSarah="Mission:555BE9F3", BuyNano="Mission:555BE9F4";
    internal const string FindThief="Mission:555BE9F5", DeliverArmor="Mission:555BE9F6", TalkVernon="Mission:555BE9F7";
    internal static bool TryUseLockpickOnStrongbox(this AuthoredQuestService service, Player player, Identity slot, Item item, Action? publish = null)
        => service.TryExecuteAction(player, "unlock-strongbox", slot, item, publish);
    internal static bool TryUseShopThiefRemains(this AuthoredQuestService service, Player player, Action publish)
        => service.TryExecuteAction(player, "recover-stolen-armor", acknowledge: publish);
    internal static bool TryTurnInFactory(this AuthoredQuestService service, Player player, Identity slot, Item item, Action publish)
        => service.TryExecuteAction(player, "deliver-factory", slot, item, publish);
    internal static bool TryTurnInDoja(this AuthoredQuestService service, Player player, Identity slot, Item item, Action publish)
        => service.TryExecuteAction(player, "doja-nascense", slot, item, publish);
    internal static bool TryGrantTailorMeasurement(this AuthoredQuestService service, Player player, int answer)
        => service.TryExecuteAction(player, "tailor-measurement-" + answer);
}
internal static class DialogueFixture
{
    internal const string Stan="SimpleChar:78E0FC65", Scarlett="SimpleChar:7A18B924", Tailor="SimpleChar:79135F51";
    internal const string Zyvania="SimpleChar:7976BCF3", Sarah="SimpleChar:78E0FC69", Marco="SimpleChar:78E0FC81";
}
internal static class QuestPropFixture
{
    internal const int StrongboxInstance=0x574187CE, RemainsInstance=0x574187CF;
}
