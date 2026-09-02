namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldVisibilityLifecycleIntegrationTests
    {
        [TestMethod]
        public void InitialSnapshotSynchronizesCellsSelectsNeighborsAndCompletesRecipient()
        {
            string packets = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityPackets.cs");
            string method = ExtractBlock(
                packets,
                "internal void SendExistingCharacterVisibilityToClient(");

            AssertBefore(method, "this.visibility.Synchronize(characterSnapshot);", "this.visibility.SelectInitialCharacters(recipient);");
            AssertBefore(method, "IList<ICharacter> selectedCharacters", "recipient,\n                selectedCharacters,");
            AssertBefore(method, "this.visibility.SelectInitialCharacters(recipient);", "this.visibility.CompleteInitialRecipient(recipient);");
            Assert.IsFalse(
                method.Contains("recipient,\n                characterSnapshot,"),
                "The complete playfield snapshot must not bypass cell-neighbor selection.");
        }

        [TestMethod]
        public void DynamicSpawnsRegisterBeforeSharedLocalityVisibility()
        {
            string ordinary = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs");
            string captured = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs");
            string pet = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\PetRuntimeService.cs");

            StringAssert.Contains(ordinary, "playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);");
            StringAssert.Contains(captured, "playfield.AnnounceSpawnedCharacterVisibility(mobCharacter, Identity.None);");
            StringAssert.Contains(pet, "concretePlayfield.AnnounceSpawnedCharacterVisibility(petCharacter, owner.Identity);");
            AssertBefore(
                pet,
                "concretePlayfield.ActivateNpc(petCharacter);",
                "concretePlayfield.AnnounceSpawnedCharacterVisibility(petCharacter, owner.Identity);");
        }

        [TestMethod]
        public void MovementRefreshesCellMembershipBeforeScopedFanout()
        {
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string announce = ExtractBlock(playfield, "public void Announce(MessageBody messageBody)");
            string refresh = ExtractBlock(
                playfield,
                "public void RefreshCharacterVisibility(ICharacter character, bool forceRefresh = false)");

            AssertBefore(announce, "this.RefreshCharacterVisibility(source);", "this.TryAnnounceCharacterScopedMessage(");
            AssertBefore(refresh, "this.locality.MoveCharacter(character);", "this.locality.RefreshCharacterVisibility(");
        }

        [TestMethod]
        public void CombatVisibilityRefreshesBeforeForcedEntryFallback()
        {
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string ensure = ExtractBlock(
                playfield,
                "internal bool EnsureNpcCombatVisibility(ICharacter attacker, ICharacter target)");

            AssertBefore(
                ensure,
                "this.RefreshCharacterVisibility(attacker, forceRefresh: true);",
                "this.ForceCharacterVisibilityToRecipient(attacker, target);");
            StringAssert.Contains(ensure, "this.locality.VisibleRecipientsForSource(attacker.Identity)");
        }

        [TestMethod]
        public void DespawnDeliversToTrackedRecipientsBeforeLocalityCleanup()
        {
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string despawn = ExtractBlock(playfield, "private bool TryDespawnVisibleCharacter(Identity sourceIdentity)");
            string visibility = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityVisibility.cs");
            string unregister = ExtractBlock(visibility, "internal void UnregisterSource(Identity sourceIdentity)");

            AssertBefore(
                despawn,
                "this.locality.VisibleRecipientsForSource(sourceIdentity)",
                "this.locality.UnregisterCharacter(sourceIdentity);");
            StringAssert.Contains(unregister, "this.RemoveSourceStateUnlocked(sourceKey);");
        }

        [TestMethod]
        public void DisconnectAndResetReleaseLocalityState()
        {
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string disconnect = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs");

            StringAssert.Contains(disconnect, "ForgetVisibilityRecipient");
            StringAssert.Contains(playfield, "this.locality.Clear();");
            StringAssert.Contains(playfield, "this.locality.UnregisterCharacter(identity);");
        }

        [TestMethod]
        public void VisibilityEntryPreservesScfuWeaponAndCharInPlaySequence()
        {
            string packets = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityPackets.cs");
            string method = ExtractBlock(
                packets,
                "private bool SendCharacterVisibilityEntry(");

            AssertBefore(method, "SimpleCharFullUpdate.ConstructMessage(temp);", "new CharInPlayMessage");
            AssertBefore(method, "new CharInPlayMessage", "this.visibility.MarkVisibleEntry(recipient, source);");
            StringAssert.Contains(method, "this.packetSequences.RunVisibilityPacketPairSequence(");
        }

        [TestMethod]
        public void SubwayDiagnosticsObserveLocalitySelectionWithoutBypassingIt()
        {
            string packets = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityPackets.cs");
            string snapshot = ExtractBlock(
                packets,
                "internal void SendExistingCharacterVisibilityToClient(");

            AssertBefore(
                snapshot,
                "IList<ICharacter> selectedCharacters = this.visibility.SelectInitialCharacters(recipient);",
                "SubwayVisibilitySnapshotDiagnostics.TryBeginSnapshot(recipient, 0);");
            StringAssert.Contains(snapshot, "this.visibility.LastCandidateCount");
            StringAssert.Contains(snapshot, "selectedCharacters.Count");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), relativePath));
        }

        private static string ExtractBlock(string text, string signature)
        {
            int start = text.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing source signature: " + signature);

            int open = text.IndexOf('{', start);
            Assert.IsTrue(open >= 0, "Missing opening brace for: " + signature);
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail("Unterminated source block: " + signature);
            return string.Empty;
        }

        private static void AssertBefore(string text, string first, string second)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing source fragment: " + first);
            Assert.IsTrue(secondIndex >= 0, "Missing source fragment: " + second);
            Assert.IsTrue(firstIndex < secondIndex, "Expected source ordering was not preserved.");
        }
    }
}
