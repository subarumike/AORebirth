namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldVisibilityLifecycleIntegrationTests
    {
        [TestMethod]
        public void InitialSnapshotUsesBoundedSelectionInsteadOfAllCharacterFanout()
        {
            string visibilityText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs");
            string method = ExtractBlock(
                visibilityText,
                "internal void SendExistingCharacterVisibilityToClient(");

            AssertBefore(
                method,
                "this.visibilityInterest.Synchronize(characterSnapshot);",
                "this.visibilityInterest.SelectInitialCharacters(recipient);",
                "Initial visibility must synchronize the index before selecting candidates.");
            AssertBefore(
                method,
                "IList<ICharacter> selectedCharacters",
                "recipient,\n                selectedCharacters,",
                "The bounded selection must be the collection passed to visibility fanout.");
            Assert.IsFalse(
                method.Contains("recipient,\n                characterSnapshot,"),
                "The complete playfield snapshot must never be passed to the visibility fanout.");
            Assert.IsFalse(
                method.Contains("recipient,\n                characters,"),
                "The unbounded input enumeration must never be passed to the visibility fanout.");
            AssertBefore(
                method,
                "this.visibilityInterest.SelectInitialCharacters(recipient);",
                "this.visibilityInterest.CompleteInitialRecipient(recipient);",
                "A recipient becomes initialized only after its bounded initial snapshot completes.");
        }

        [TestMethod]
        public void DynamicOrdinaryCapturedAndPetSpawnsUseSharedInterestHook()
        {
            string ordinaryText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs");
            string capturedText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs");
            string petText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\PetRuntimeService.cs");

            Assert.IsTrue(
                ordinaryText.Contains("playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);")
                && !ordinaryText.Contains("playfield.Announce(fullUpdate);")
                && !ordinaryText.Contains("WeaponItemFullUpdate.SendWeaponDefinitions(character, true);"),
                "Ordinary enemies must use only the shared dynamic visibility entry hook.");
            Assert.IsTrue(
                capturedText.Contains(
                    "playfield.AnnounceSpawnedCharacterVisibility(mobCharacter, Identity.None);")
                && !capturedText.Contains("playfield.Announce(SimpleCharFullUpdate.ConstructMessage(mobCharacter));"),
                "Captured Arete robots must use the shared dynamic visibility entry hook.");
            Assert.IsTrue(
                petText.Contains(
                    "concretePlayfield.AnnounceSpawnedCharacterVisibility(petCharacter, owner.Identity);")
                && !petText.Contains("owner.Playfield.AnnounceOthers(petSpawnUpdate, owner.Identity);"),
                "Pet observers must use the shared hook while excluding the owner already sent direct capture packets.");
            Assert.IsTrue(
                petText.Contains("ownerClient.SendCompressed(petSpawnUpdate);")
                && petText.Contains("PetSummonCaptureWireReplayer.SendHealingPetScfuToOwner("),
                "The pet owner's capture-backed direct packet paths must remain intact.");
            AssertBefore(
                petText,
                "concretePlayfield.ActivateNpc(petCharacter);",
                "concretePlayfield.AnnounceSpawnedCharacterVisibility(petCharacter, owner.Identity);",
                "Pet registration must precede observer visibility entry.");
        }

        [TestMethod]
        public void MovementRefreshPrecedesCharacterScopedFanoutForPlayerAndNpcMessages()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string playerMovementText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharDCMoveMessageHandler.cs");
            string npcMovementText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\FollowTargetMessageHandler.cs");
            string announceMethod = ExtractBlock(
                playfieldText,
                "public void Announce(MessageBody messageBody)");
            string movementClassifier = ExtractBlock(
                playfieldText,
                "private static bool IsVisibilityMovementMessage(MessageBody messageBody)");

            AssertBefore(
                announceMethod,
                "this.RefreshCharacterVisibility(source);",
                "this.runtimeSystems.TryAnnounceCharacterScopedMessage(",
                "Spatial membership and enter/leave transitions must refresh before movement fanout.");
            Assert.IsTrue(
                movementClassifier.Contains("messageBody is CharDCMoveMessage")
                && movementClassifier.Contains("messageBody is FollowTargetMessage")
                && movementClassifier.Contains("messageBody is SetPosMessage"),
                "Player movement, NPC follow movement, and explicit position updates must share refresh-before-fanout.");
            Assert.IsTrue(
                playerMovementText.Contains("client.Controller.Character.Playfield.Publish(")
                && playerMovementText.Contains("Body = reply"),
                "Player CharDCMove must continue through the playfield delivery path.");
            Assert.IsTrue(
                npcMovementText.Contains("this.SendToPlayfield(character")
                && npcMovementText.Contains("FollowCoordinateInfo"),
                "NPC coordinate-follow movement must continue through the playfield delivery path.");
        }

        [TestMethod]
        public void NpcDamageRequiresVisibilityEntryBeforeHealthMutation()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string runtimeSystemsText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string visibilityPacketsText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs");
            string coordinatorText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs");
            string characterCombatText = ReadRepositoryFile(
                @"AORebirth\Libraries\Source\AORebirth.Core\Entities\Character.Combat.cs");
            string visibilityGate = ExtractBlock(
                playfieldText,
                "internal bool EnsureNpcCombatVisibility(ICharacter attacker, ICharacter target)");
            string damageGate = ExtractBlock(
                coordinatorText,
                "private bool CanApplyNpcDamage(");
            string capturedCombatTick = ExtractBlock(
                coordinatorText,
                "internal void ProcessCombatTick(ICharacter attacker)");
            string ordinaryCombatHit = ExtractBlock(
                coordinatorText,
                "internal void ApplyCombatHit(ICharacter attacker)");
            string damageCore = ExtractBlock(
                coordinatorText,
                "private void ApplyNpcCombatHitCore(");
            string characterStrike = ExtractBlock(
                characterCombatText,
                "public CombatStrikeResult Strike(");
            string receiveStrike = ExtractBlock(
                characterCombatText,
                "internal void ReceiveStrike(");
            string playfieldForceVisibility = playfieldText;
            string runtimeForceVisibility = runtimeSystemsText;

            const string visibilityLookup =
                "this.runtimeSystems.VisibleRecipientsForSource(attacker.Identity)";
            int initialVisibilityLookup = visibilityGate.IndexOf(visibilityLookup, StringComparison.Ordinal);
            int refreshVisibility = visibilityGate.IndexOf(
                "this.runtimeSystems.RefreshCharacterVisibility(",
                StringComparison.Ordinal);
            int refreshedVisibilityLookup = initialVisibilityLookup < 0
                                                ? -1
                                                : visibilityGate.IndexOf(
                                                    visibilityLookup,
                                                    initialVisibilityLookup + visibilityLookup.Length,
                                                    StringComparison.Ordinal);
            int forceVisibility = visibilityGate.IndexOf(
                "visible = this.ForceCharacterVisibilityToRecipient(attacker, target);",
                StringComparison.Ordinal);

            Assert.IsTrue(
                initialVisibilityLookup >= 0
                && refreshVisibility > initialVisibilityLookup
                && refreshedVisibilityLookup > refreshVisibility
                && forceVisibility > refreshedVisibilityLookup
                && !visibilityGate.Contains("visible = true;"),
                "Existing visibility must be reused, then refresh/forced SCFU-WIFU-CharInPlay entry must return success before visibility is accepted.");
            Assert.IsTrue(
                playfieldForceVisibility.Contains(
                    "return this.ForceCharacterVisibilityToRecipient(")
                && playfieldForceVisibility.Contains("return false;")
                && playfieldForceVisibility.Contains(
                    "return this.runtimeSystems.ForceCharacterVisibilityToRecipient(")
                && runtimeForceVisibility.Contains(
                    "return this.ForceCharacterVisibilityToRecipient(")
                && runtimeForceVisibility.Contains("return false;")
                && runtimeForceVisibility.Contains(
                    "return this.visibilityPackets.SendCharacterVisibilityEntry(")
                && visibilityPacketsText.Contains("internal bool SendCharacterVisibilityEntry("),
                "Both force-visibility wrappers must propagate packet-entry failure instead of inventing success.");
            Assert.IsTrue(
                damageGate.Contains("this.playfield.EnsureNpcCombatVisibility(attacker, target)"),
                "NPC damage must fail closed when the target has not received the attacker visibility sequence.");
            AssertBefore(
                capturedCombatTick,
                "this.CanApplyNpcDamage(",
                "this.ApplyNpcCombatHitCore(",
                "Captured NPC combat must prove visibility before entering the shared damage core.");
            AssertBefore(
                ordinaryCombatHit,
                "this.CanApplyNpcDamage(",
                "this.ApplyNpcCombatHitCore(",
                "Ordinary weapon-clock combat must prove visibility before entering the shared damage core.");
            AssertBefore(
                damageCore,
                "this.BuildStrikeContext(attackerCharacter, attackSource)",
                "attackerCharacter.Strike(target, strikeContext)",
                "The shared NPC damage core must construct its governed context before entering Character combat.");
            AssertBefore(
                characterStrike,
                "CombatStrikeDamageCalculator.Calculate(",
                "targetCharacter.ReceiveStrike(",
                "Character combat must calculate damage before delivering the health mutation.");
            Assert.IsTrue(
                receiveStrike.Contains("this.Stats[StatIds.health].Value = newHealth;"),
                "Character combat must retain one explicit health mutation after the visibility-gated strike entry.");
        }

        [TestMethod]
        public void LocalTeleportRefreshesBothRecipientSnapshotAndReversePlayerVisibility()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string teleportMethod = ExtractBlock(
                playfieldText,
                "internal void Teleport(");
            string localTeleportMethod = ExtractBlock(
                playfieldText,
                "private bool TryCompleteLocalTeleportInCurrentPlayfield(");

            AssertBefore(
                teleportMethod,
                "if (this.TryCompleteLocalTeleportInCurrentPlayfield(dynel, destination, heading, playfield))",
                "this.runtimeSystems.TransferToPlayfield(",
                "Same-playfield teleports must complete locally instead of using the zoning/redirect path.");
            Assert.IsTrue(
                localTeleportMethod.Contains("playfield.Type != this.Identity.Type")
                && localTeleportMethod.Contains("playfield.Instance != this.Identity.Instance")
                && !localTeleportMethod.Contains("this.Identity.Instance != GridPlayfield"),
                "The local teleport path must apply to every current playfield, not only Grid.");
            AssertBefore(
                localTeleportMethod,
                "TeleportMessageHandler.Default.SendLocal(",
                "dynel.RawCoordinates = new AORebirth.Core.Vector.Vector3",
                "The local teleport packet must precede server coordinate mutation.");
            AssertBefore(
                localTeleportMethod,
                "dynel.RawCoordinates = new AORebirth.Core.Vector.Vector3",
                "this.SendSCFUsToClient(new IMSendPlayerSCFUs { toClient = client });",
                "The teleporting client must receive a fresh nearby-character snapshot after landing.");
            AssertBefore(
                localTeleportMethod,
                "this.SendSCFUsToClient(new IMSendPlayerSCFUs { toClient = client });",
                "this.RefreshCharacterVisibility(character);",
                "Reverse visibility must be refreshed after the recipient snapshot is rebuilt.");
        }

        [TestMethod]
        public void ReconnectDiscardsPooledPlayerWhenImmutableParentDoesNotMatchCurrentPlayfield()
        {
            string zoneClientText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs");
            string createCharacterMethod = ExtractBlock(
                zoneClientText,
                "public void CreateCharacter(int charId)");

            AssertBefore(
                createCharacterMethod,
                "if (pooledCharacter != null && !pooledCharacter.Parent.Equals(pf.Identity))",
                "if (pooledCharacter == null)",
                "A stale pooled player must be discarded before the create/reconnect branch is selected.");
            Assert.IsTrue(
                createCharacterMethod.Contains("Pool.Instance.RemoveObject(pooledCharacter);")
                && createCharacterMethod.Contains("pooledCharacter = null;")
                && createCharacterMethod.Contains("currentPlayfield="),
                "Discarding a stale pooled player must remove it from Pool and create a fresh parent-correct character.");
        }

        [TestMethod]
        public void KnownCharacterDespawnTargetsTrackedRecipientsAndCleansState()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string runtimeText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string interestText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityInterestState.cs");
            string despawnHandlerText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\DespawnMessageHandler.cs");
            string despawnMethod = ExtractBlock(playfieldText, "public void Despawn(Identity identity)");
            string scopedDespawn = ExtractBlock(runtimeText, "internal bool TryDespawnVisibleCharacter(");
            string unregister = ExtractBlock(interestText, "internal void Unregister(Identity identity)");

            AssertBefore(
                despawnMethod,
                "this.runtimeSystems.TryDespawnVisibleCharacter(identity, this.SendVisibilityMessage)",
                "this.Announce(DespawnMessageHandler.Default.Create(identity));",
                "Known character removal must attempt tracked delivery before the unknown-identity fallback.");
            Assert.IsTrue(
                scopedDespawn.Contains("if (source == null)")
                && scopedDespawn.Contains("return false;")
                && scopedDespawn.Contains("VisibleRecipientsForSource(sourceIdentity)"),
                "Only known indexed characters may use tracked Despawn delivery.");
            AssertBefore(
                scopedDespawn,
                "sendVisibilityMessage(recipient, despawn);",
                "this.visibilityInterest.Unregister(sourceIdentity);",
                "Despawn must reach the tracked recipients before source state is removed.");
            Assert.IsTrue(
                unregister.Contains("this.spatialIndex.Remove(identity);")
                && unregister.Contains("this.valuesByIdentity.Remove(identityKey);")
                && unregister.Contains("this.RemoveRecipientStateUnlocked(identityKey);")
                && unregister.Contains("this.RemoveSourceStateUnlocked(identityKey);"),
                "Character removal must clear index, recipient, and reverse-source state atomically.");
            Assert.IsTrue(
                despawnHandlerText.Contains("x.Unknown = 1;"),
                "The proven DespawnMessage Unknown=1 wire value must remain immutable.");
        }

        [TestMethod]
        public void CorpseVisibilityUsesTrackedRecipientsAndEnterLeaveHysteresis()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string sendCorpse = ExtractBlock(
                playfieldText,
                "private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)");
            string refreshCorpse = ExtractBlock(
                playfieldText,
                "private void RefreshCorpseVisibilityForRecipient(ICharacter recipient)");
            string despawnCorpse = ExtractBlock(
                playfieldText,
                "private void SendCorpseDespawn(Identity corpseIdentity)");
            string finalDespawn = ExtractBlock(
                playfieldText,
                "private void DespawnCorpse(int corpseInstance)");

            Assert.IsTrue(
                sendCorpse.Contains("this.runtimeSystems.VisibleRecipientsForSource(target.Identity)")
                && !sendCorpse.Contains("this.runtimeSystems.Characters()"),
                "CorpseFullUpdate must use the dead character's visible recipients, never the whole playfield.");
            Assert.IsTrue(
                refreshCorpse.Contains("!visible && distance <= this.runtimeSystems.VisibilityEnterRadius")
                && refreshCorpse.Contains("visible && distance > this.runtimeSystems.VisibilityLeaveRadius")
                && refreshCorpse.Contains("corpse.VisibleRecipients.Remove(recipient.Identity);"),
                "Corpse entry and leave must use distinct radii and update tracked recipient state.");
            Assert.IsTrue(
                despawnCorpse.Contains("corpse.VisibleRecipients")
                && despawnCorpse.Contains("this.SendVisibilityLeave(recipient, corpseIdentity);")
                && despawnCorpse.Contains("corpse.VisibleRecipients.Clear();"),
                "Corpse despawn must notify only recipients that were sent the corpse.");
            Assert.IsTrue(
                finalDespawn.Contains("this.SendCorpseDespawn"),
                "Timed and loot-complete corpse cleanup must use tracked-recipient despawn.");
        }

        [TestMethod]
        public void ZoningDisconnectAndPlayfieldResetReleaseVisibilityState()
        {
            string playfieldText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string runtimeText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string zoneClientText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs");
            string transferDespawn = ExtractBlock(
                playfieldText,
                "private void AnnouncePlayfieldTransferDespawn(Dynel dynel)");
            string disconnect = ExtractBlock(
                playfieldText,
                "public void DisconnectClient(IInstancedEntity entity)");
            string clearRuntime = ExtractBlock(runtimeText, "internal void ClearNpcRuntimeState()");
            string zoneClientDispose = ExtractBlock(zoneClientText, "protected override void Dispose(bool disposing)");

            Assert.IsTrue(
                transferDespawn.Contains("this.Despawn(dynel.Identity);"),
                "Playfield transfer must use tracked character removal.");
            AssertBefore(
                disconnect,
                "this.Despawn(character.Identity);",
                "this.ForgetVisibilityRecipient(character.Identity);",
                "Disconnect must announce removal before forgetting recipient state.");
            AssertBefore(
                disconnect,
                "this.ForgetVisibilityRecipient(character.Identity);",
                "this.runtimeSystems.UnregisterDynel(character.Identity);",
                "Disconnect must clear recipient state before unregistering the dynel.");
            Assert.IsTrue(
                zoneClientDispose.Contains(
                    "disconnectPlayfield.ForgetVisibilityRecipient(disconnectCharacter.Identity);"),
                "Socket/session disposal must release reverse visibility membership.");
            Assert.IsTrue(
                clearRuntime.Contains("this.npcRuntime.ClearRuntimeState();")
                && clearRuntime.Contains("this.visibilityInterest.Clear();"),
                "Playfield runtime reset must clear both NPC and spatial-interest state.");
        }

        [TestMethod]
        public void StaticDynelSnapshotBehaviorRemainsOutsideCharacterInterestSelection()
        {
            string charInPlayText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharInPlayMessageHandler.cs");
            string runtimeText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string registerDynel = ExtractBlock(runtimeText, "internal void RegisterDynel(IEntity entity)");

            Assert.IsTrue(
                charInPlayText.Contains(
                    "Pool.Instance.GetAll<StaticDynel>(client.Controller.Character.Playfield.Identity)")
                && charInPlayText.Contains(
                    "SimpleItemFullUpdateMessageHandler.Default.Send(client.Controller.Character, sd);"),
                "Static dynels must retain their established CharInPlay snapshot path.");
            Assert.IsFalse(
                charInPlayText.Contains("visibilityInterest")
                || charInPlayText.Contains("SelectInitialCharacters")
                || charInPlayText.Contains("AnnounceSpawnedCharacterVisibility"),
                "Character interest selection must not alter static dynel delivery.");
            Assert.IsTrue(
                registerDynel.Contains("ICharacter character = entity as ICharacter;")
                && registerDynel.Contains("if (character != null)")
                && registerDynel.Contains("this.visibilityInterest.Register(character);"),
                "The spatial interest registry must index only character entities from generic dynel registration.");
        }

        [TestMethod]
        public void SubwayPopulationQuarantineAndDiagnosticSelectionCannotBypassSpatialSelection()
        {
            string root = FindRepositoryRoot();
            string supportedText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs");
            string ordinaryText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs");
            string catalogText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs");
            string populationText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs");
            string visibilityText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs");
            string interestText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityInterestRuntimeService.cs");
            string[][] manifestRows = File.ReadAllLines(
                    Path.Combine(root, @"docs\generated\subway_20260710_population_restore_manifest.csv"))
                .Skip(1)
                .Select(line => line.Split(','))
                .ToArray();
            string[] manifestHeader = File.ReadAllLines(
                Path.Combine(root, @"docs\generated\subway_20260710_population_restore_manifest.csv"))[0]
                .Split(',');
            int classificationIndex = Array.IndexOf(manifestHeader, "Classification");

            CapturedSubwaySpawnDefinition[] supported =
                new CapturedSubwayContentProvider().GetAllSpawnDefinitions();
            Assert.AreEqual(124, supported.Length, "Supported-family evidence rows must remain unchanged.");
            Assert.AreEqual(
                0,
                supported.Count(row => CapturedSubwayContentProvider.IsRuntimeQuarantined(row.SourceInstance)),
                "All supported-family rows must now be active; the quarantine mechanism remains available for future evidence gaps.");
            Assert.AreEqual(6, supported.Select(row => row.MonsterData).Distinct().Count());
            Assert.AreEqual(
                198,
                CountOccurrences(ordinaryText, "new CapturedSubwayOrdinarySpawnDefinition("),
                "Ordinary captured spawn data must remain 198 rows.");
            Assert.AreEqual(
                20,
                CountOccurrences(ordinaryText, "new CapturedSubwayOrdinaryArchetypeDefinition("),
                "Ordinary profile data must remain 20 archetypes.");
            Assert.AreEqual(322, supported.Length + 198, "The normalized catalog must remain 322 spawn rows.");
            Assert.AreEqual(26, 6 + 20, "The normalized catalog must remain 26 profiles.");
            Assert.AreEqual(107, manifestRows.Length);
            Assert.AreEqual(
                29,
                manifestRows.Count(row => row[classificationIndex] == "SUPPORTED_FAMILY_RESTORE"));
            Assert.AreEqual(
                9,
                manifestRows.Count(row => row[classificationIndex] == "ORDINARY_ENEMY_REGENERATE"));

            Assert.IsTrue(
                supportedText.Contains("RuntimeQuarantinedSourceInstances")
                && !ordinaryText.Contains(
                    "!string.Equals(spawn.EvidenceCapture, \"20260710-202132\", StringComparison.Ordinal)")
                && catalogText.Contains("SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined("),
                "Accepted ordinary rows must be active while the supported quarantine mechanism stays opt-in.");
            Assert.IsTrue(
                CountOccurrences(
                    populationText,
                    "SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(") >= 2
                && populationText.Contains("Enabled = runtimeEnabled")
                && populationText.Contains("&& !runtimeEnabled"),
                "The world population owner must activate selected diagnostic rows and count them in the active group.");
            Assert.IsFalse(
                visibilityText.Contains("SubwayVisibilityDiagnosticSelection")
                || interestText.Contains("SubwayVisibilityDiagnosticSelection"),
                "Diagnostic ALL_38 selection may affect spawn eligibility only, never spatial visibility eligibility.");
            Assert.IsTrue(
                visibilityText.Contains("this.visibilityInterest.SelectInitialCharacters(recipient)")
                && visibilityText.Contains("this.visibilityFanout.FanoutExistingCharactersForScfu(")
                && visibilityText.Contains("selectedCharacters,"),
                "Every initial snapshot, including diagnostic populations, must still pass through bounded selection.");
        }

        [TestMethod]
        public void SharedVisibilityEntryPreservesScfuWeaponAndCharInPlayOrder()
        {
            string visibilityText = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs");
            string method = ExtractBlock(
                visibilityText,
                "private bool SendCharacterVisibilityEntry(");

            AssertBefore(
                method,
                "SimpleCharFullUpdate.ConstructMessage(temp)",
                "this.SendWeaponDefinitionsForVisibility(",
                "SCFU construction and delivery must remain before observer weapon definitions.");
            AssertBefore(
                method,
                "this.SendWeaponDefinitionsForVisibility(",
                "charInPlay = new CharInPlayMessage",
                "Weapon definitions must remain before CharInPlay preparation and delivery.");
            AssertBefore(
                method,
                "charInPlay = new CharInPlayMessage",
                "sendVisibilityMessage(charInPlay);",
                "CharInPlay must be prepared before delivery.");
            AssertBefore(
                method,
                "sendVisibilityMessage(charInPlay);",
                "this.visibilityInterest.MarkVisibleEntry(recipient, source);",
                "Visibility state must be committed only after the complete packet entry sequence.");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath))
                .Replace("\r\n", "\n");
        }

        private static string ExtractBlock(string text, string marker)
        {
            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.IsTrue(markerIndex >= 0, "Missing source marker: " + marker);
            int startIndex = text.IndexOf('{', markerIndex);
            Assert.IsTrue(startIndex >= 0, "Missing source block for marker: " + marker);

            int depth = 0;
            for (int index = startIndex; index < text.Length; index++)
            {
                if (text[index] == '{')
                {
                    depth++;
                }
                else if (text[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(startIndex, index - startIndex + 1);
                    }
                }
            }

            Assert.Fail("Unterminated source block for marker: " + marker);
            return string.Empty;
        }

        private static void AssertBefore(string text, string first, string second, string message)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing source fragment: " + first);
            Assert.IsTrue(secondIndex >= 0, "Missing source fragment: " + second);
            Assert.IsTrue(firstIndex < secondIndex, message);
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int start = 0;
            while ((start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
            {
                count++;
                start += value.Length;
            }

            return count;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(
                    current,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
                if (Directory.Exists(candidate))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Unable to find AORebirth repository root from " + sourcePath + ".");
            return string.Empty;
        }
    }
}
