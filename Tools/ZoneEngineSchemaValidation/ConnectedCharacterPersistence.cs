using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

static partial class ConnectedAcceptanceSmoke
{
    static int expectedCash = 1234;
    static void ZonePersistenceRoundTrip(DisposableSchemaDatabase fixture, string password, MySqlConnection connection)
    {
        var handoff = Authorize(fixture, password);
        using (var source = EnterWithHandoff(fixture, handoff))
        {
            Command(source, ".set Cash 1432");
            source.Wait<ChatTextMessage>(m => m.Text.Contains("Set Cash", StringComparison.Ordinal));
            expectedCash = 1432;
            persistenceEvidence.ExpectBaseStat(CharacterStat.Cash, 1234, expectedCash);
            Command(source, ".tp 100 0 100 800");
            source.Wait<N3TeleportMessage>();
            source.Wait<ZoneRedirectionMessage>();
        }
        using (var destination = EnterWithHandoff(fixture, handoff))
        {
            var full = destination.Received.OfType<FullCharacterMessage>().Single(m => m.Identity == Character);
            Require(full.InventorySlots.Single(i => i.Identity.Instance == CharacterPersistenceGameplaySmoke.SecondItem).Placement == CharacterPersistenceGameplaySmoke.WearSlot,
                "zone-arrival-actual-equipment");
            Command(destination, ".tp 100 0 100 4582");
            destination.Wait<N3TeleportMessage>();
            destination.Wait<ZoneRedirectionMessage>();
        }
        using (var returned = EnterWithHandoff(fixture, handoff))
        {
            Logout(returned, connection);
            persistenceEvidence.Verify(connection, "ZONE_ROUND_TRIP_LOGOUT", 0);
        }
        Console.WriteLine("CONNECTED_ACTUAL_ZONE_TRANSFER=PASS ROUTE=4582-800-4582 REDIRECT_ADMISSION=PASS STATS_MUTATION=GM_SET_CASH FULL_DURABLE_STATE=PASS");
    }
    static void Command(ConnectedWireClient client, string text)
        => client.Send(new TextMessage { Message = new ChatMessage { Text = text } }, Owner);
}
