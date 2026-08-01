namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgLegacyFallthroughContractTests
    {
        [TestMethod]
        public void GeneratedOwnershipUsesExactBindingOrReservationNotNumericRange()
        {
            string runtime =
                ReadMissionSource("MissionAcgBindingRuntime.cs");
            string claims =
                ReadMember(
                    runtime,
                    "internal static bool ClaimsGeneratedLivePlayfield(");

            StringAssert.Contains(claims, "ByLivePlayfield.ContainsKey(");
            StringAssert.Contains(claims, "allocator.IsReserved(");
            Assert.IsFalse(
                claims.IndexOf(
                    "IsAllocatableRange(",
                    StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void GeneratedSpawnNeverFallsThroughToLegacySpawn()
        {
            string npcRuntime =
                ReadZoneSource("Core/Playfields/NPCRuntimeService.cs");
            string initialize =
                ReadMember(
                    npcRuntime,
                    "internal void SpawnCapturedNpcContent(");
            int generated =
                initialize.IndexOf(
                    "MissionAcgOperationalRuntime.TrySpawnForPlayfield(",
                    StringComparison.Ordinal);
            int legacy =
                initialize.IndexOf(
                    "MissionInstanceSpawn.SpawnForPlayfield(",
                    StringComparison.Ordinal);

            Assert.IsTrue(generated >= 0 && legacy > generated);
            int generatedCatch =
                initialize.IndexOf(
                    "catch (Exception",
                    generated,
                    StringComparison.Ordinal);
            string catchBlock =
                initialize.Substring(generatedCatch);
            AssertOrdered(
                catchBlock,
                "ClaimsGeneratedLivePlayfield(",
                "MissionInstanceSpawn.SpawnForPlayfield(");

            string legacySpawn =
                ReadZoneSource("Core/Playfields/MissionInstanceSpawn.cs");
            AssertOrdered(
                ReadMember(legacySpawn, "public static void SpawnForPlayfield("),
                "ClaimsGeneratedLivePlayfield(",
                "return;");

            string content =
                ReadZoneSource(
                    "Core/Playfields/Content/MissionInstanceContentModule.cs");
            StringAssert.Contains(
                ReadMember(content, "public bool ShouldSuppressDbMobSpawn("),
                "ClaimsGeneratedLivePlayfield(");
        }

        [TestMethod]
        public void UnknownGeneratedUseAndGetRuntimeIdentitiesAreConsumed()
        {
            string runtime =
                ReadMissionSource("MissionAcgRuntimeInteractionService.cs");
            string use =
                ReadMember(runtime, "internal static bool TryHandleUse(");
            string get =
                ReadMember(runtime, "internal static bool TryHandleGet(");

            AssertOrdered(
                use,
                "ClaimsGeneratedLivePlayfield(",
                "IsRuntimeIdentityCandidate(",
                "AcknowledgeDenied(",
                "return true;");
            AssertOrdered(
                get,
                "ClaimsGeneratedLivePlayfield(",
                "TryResolveObject(",
                "AcknowledgeDenied(",
                "return true;");

            string handler =
                ReadZoneSource("Core/MessageHandlers/GenericCmdMessageHandler.cs");
            string getCase =
                ReadMember(
                    handler,
                    "protected override void Read(");
            AssertOrdered(
                getCase,
                "MissionAcgRuntimeInteractionService.TryHandleGet(",
                "MissionFindItemService.TryHandleWorldPickUp(");

            string playfieldInteractions =
                ReadZoneSource(
                    "Core/Playfields/PlayfieldInteractionRuntimeService.cs");
            AssertOrdered(
                ReadMember(
                    playfieldInteractions,
                    "internal bool TryHandleGenericCmdUse("),
                "ClaimsCurrentGeneratedPlayfield(",
                "CorpseInteractionHandler.Default.TryHandleUse(",
                "MissionAcgRuntimeInteractionService.TryHandleUse(");
        }

        [TestMethod]
        public void GeneratedUseItemOnItemClaimPrecedesEveryLegacyHandler()
        {
            string objective =
                ReadMissionSource("MissionAcgObjectiveInteractionService.cs");
            string claims =
                ReadMember(
                    objective,
                    "internal static bool ClaimsGeneratedUseItemOnItem(");
            StringAssert.Contains(claims, "ClaimsCurrentGeneratedPlayfield(");
            StringAssert.Contains(claims, "ClaimsGeneratedRuntimeIdentity(");
            StringAssert.Contains(claims, "IsGeneratedMissionItem(");
            StringAssert.Contains(claims, "IsGeneratedMissionKey(");

            string handler =
                ReadZoneSource(
                    "Core/MessageHandlers/UseItemOnItemInteractionHandler.cs");
            string use =
                ReadMember(handler, "public bool TryHandle(");
            AssertOrdered(
                use,
                "MissionAcgObjectiveInteractionService.TryHandleUseItemOnItem(",
                "MissionAcgObjectiveInteractionService.ClaimsGeneratedUseItemOnItem(",
                "MissionRepairService.TryHandleUseItemOnItem(",
                "MissionFindItemService.TryHandleReturnToTerminal(");
            StringAssert.Contains(use, "AcknowledgeDenied(");
        }

        [TestMethod]
        public void GeneratedFindPersonNeverReachesGlobalTargetLookup()
        {
            string service =
                ReadMissionSource("MissionFindPersonService.cs");
            string info =
                ReadMember(service, "public static bool TryHandleInfoRequest(");
            int claimed =
                info.IndexOf(
                    "ClaimsCurrentGeneratedPlayfield(",
                    StringComparison.Ordinal);
            int exact =
                info.IndexOf(
                    "MissionAcgObjectiveInteractionService.TryHandleInfoRequest(",
                    StringComparison.Ordinal);
            int legacy =
                info.IndexOf(
                    "IsFindPersonTarget(",
                    StringComparison.Ordinal);

            Assert.IsTrue(claimed >= 0 && exact > claimed && legacy > exact);
            StringAssert.Contains(
                info.Substring(claimed, legacy - claimed),
                "return");
        }

        [TestMethod]
        public void GeneratedCombatUnknownIdentityFailsClosed()
        {
            string runtime =
                ReadMissionSource("MissionAcgOperationalRuntime.cs");
            string validate =
                ReadMember(
                    runtime,
                    "internal static bool TryValidateCombatTarget(");

            AssertOrdered(
                validate,
                "generatedPlayfieldClaimed",
                "ClaimsGeneratedLivePlayfield(",
                "ByPlayfield.TryGetValue(",
                "Generated mission PF2 has no operational combat state.",
                "return false;");
            StringAssert.Contains(
                validate,
                "Runtime identity is not an operational NPC in this instance.");

            string death =
                ReadMember(
                    runtime,
                    "internal static bool TryPrepareNpcDeath(");
            AssertOrdered(
                death,
                "ClaimsGeneratedLivePlayfield(",
                "lock (Sync)");
            Assert.IsFalse(
                death.IndexOf(
                    "IsAllocatableRange(",
                    StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void GeneratedCompletionNeverInvokesLegacyCompletion()
        {
            string completion =
                ReadMissionSource("MissionCompleteService.cs");
            string complete =
                ReadMember(completion, "public static bool TryComplete(");

            AssertOrdered(
                complete,
                "IsGeneratedAcceptedMission(",
                "return false;",
                "string flightKey");
            Assert.IsFalse(
                complete.Substring(
                        0,
                        complete.IndexOf(
                            "string flightKey",
                            StringComparison.Ordinal))
                    .IndexOf(
                        "MissionAcgCompletionJournalService.TryCompleteVerified(",
                        StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void GeneratedKillRejectionCannotReachGlobalTargetTracker()
        {
            string completion =
                ReadMissionSource("MissionCompleteService.cs");
            string killed =
                ReadMember(
                    completion,
                    "public static bool TryCompleteIfMissionTargetKilled(");

            AssertOrdered(
                killed,
                "MissionAcgObjectiveInteractionService.TryHandleTargetDeath(",
                "IsInClaimedGeneratedPlayfield(",
                "ClaimsGeneratedRuntimeIdentity(",
                "return false;",
                "MissionTargetTracker.IsMissionTarget(");
        }

        [TestMethod]
        public void LegacyMissionSelectorsExcludeGeneratedAcceptedMissions()
        {
            string completion =
                ReadMissionSource("MissionCompleteService.cs");
            StringAssert.Contains(
                ReadMember(completion, "public static bool TryCompleteLatest("),
                "IsGeneratedAcceptedMission(");
            StringAssert.Contains(
                ReadMember(
                    completion,
                    "public static bool TryCompleteFindPerson("),
                "IsGeneratedAcceptedMission(");

            string findItem =
                ReadMissionSource("MissionFindItemService.cs");
            StringAssert.Contains(
                ReadMember(
                    findItem,
                    "private static MissionAcceptedStore.AcceptedMission ResolveActiveFindItemMission("),
                "IsGeneratedAcceptedMission(");

            string repair =
                ReadMissionSource("MissionRepairService.cs");
            StringAssert.Contains(
                ReadMember(
                    repair,
                    "private static MissionAcceptedStore.AcceptedMission FindRepairMission("),
                "IsGeneratedAcceptedMission(");

            string instance =
                ReadMissionSource("MissionInstanceService.cs");
            StringAssert.Contains(
                ReadMember(
                    instance,
                    "private static MissionAcceptedStore.AcceptedMission ResolveLatestLegacyAcceptedMission("),
                "!MissionCompleteService.IsGeneratedAcceptedMission(entry)");
            StringAssert.Contains(
                ReadMember(instance, "internal static MissionRollType ResolveCharacterObjective("),
                "ResolveLatestLegacyAcceptedMission(");
            StringAssert.Contains(
                ReadMember(instance, "internal static int ResolveCharacterMissionQuality("),
                "ResolveLatestLegacyAcceptedMission(");
            StringAssert.Contains(
                ReadMember(instance, "internal static bool TryEnterMissionInstance("),
                "HasNonGeneratedMissionKey(");
            StringAssert.Contains(
                ReadMember(instance, "internal static void ResolveOutdoorExitDestination("),
                "!MissionCompleteService.IsGeneratedAcceptedMission(entry)");
        }

        [TestMethod]
        public void LegacyTemplateScansCannotConsumeGeneratedArtifacts()
        {
            string findItem =
                ReadMissionSource("MissionFindItemService.cs");
            StringAssert.Contains(
                findItem,
                "MissionAcgObjectiveRuntime.IsGeneratedMissionItem(");

            string repair =
                ReadMissionSource("MissionRepairService.cs");
            StringAssert.Contains(
                repair,
                "MissionAcgObjectiveRuntime.IsGeneratedMissionItem(");

            string keys =
                ReadMissionSource("MissionKeyGrantService.cs");
            StringAssert.Contains(
                ReadMember(keys, "public static bool HasNonGeneratedMissionKey("),
                "IsGeneratedMissionKeyInstance(");
            StringAssert.Contains(
                ReadMember(keys, "public static bool TryRemoveAnyMissionKey("),
                "IsGeneratedMissionKeyInstance(");

            string keyStore =
                ReadMissionSource("MissionKeyStore.cs");
            StringAssert.Contains(
                ReadMember(keyStore, "public static bool TryTakeExactNonGenerated("),
                "isGeneratedKey(mapped)");
            StringAssert.Contains(
                ReadMember(keyStore, "public static bool TryTakeLatestNonGenerated("),
                "isGeneratedKey(candidate)");

            string corpse =
                ReadZoneSource(
                    "Core/MessageHandlers/ContainerAddItemMessageHandler.cs");
            string corpseRead =
                ReadMember(corpse, "protected override void Read(");
            AssertOrdered(
                corpseRead,
                "ClaimsGeneratedLivePlayfield(",
                "MissionFindItemService.TryHandleAfterLoot(");
            AssertOrdered(
                corpseRead,
                "TryLootCorpseItem(",
                "ClaimsGeneratedMissionCorpseContainer(",
                "InventoryContainerRuntimeService.Default.HandleContainerAddItem(");
            StringAssert.Contains(
                corpseRead,
                "message.SourceContainer.Type == IdentityType.Corpse");

            string move =
                ReadZoneSource(
                    "Core/MessageHandlers/ClientMoveItemToInventoryMessageHandler.cs");
            AssertOrdered(
                ReadMember(move, "protected override void Read("),
                "TryLootCorpseItem(",
                "ClaimsGeneratedMissionCorpseContainer(",
                "InventoryContainerRuntimeService.Default.HandleClientMoveItemToInventory(");
            StringAssert.Contains(
                move,
                "message.SourceContainer.Type == IdentityType.Corpse");

            string action =
                ReadZoneSource(
                    "Core/MessageHandlers/CharacterActionMessageHandler.cs");
            AssertOrdered(
                ReadMember(action, "protected override void Read("),
                "TryDeleteCorpseLootItem(",
                "ClaimsGeneratedMissionCorpseContainer(",
                "InventoryContainerRuntimeService.Default.DeleteInventoryItemAction(");
            StringAssert.Contains(
                action,
                "message.Target.Type == IdentityType.Corpse");

            string playfield =
                ReadZoneSource("Core/Playfields/Playfield.cs");
            string classifier =
                ReadMember(
                    playfield,
                    "public bool ClaimsGeneratedMissionCorpseContainer(");
            StringAssert.Contains(classifier, "candidate.InventoryHandle == handle");
            StringAssert.Contains(classifier, "corpse.IsGeneratedMissionCorpse");
        }

        [TestMethod]
        public void GeneratedArtifactsAreClaimedIndependentOfCurrentOwner()
        {
            string objective =
                ReadMissionSource("MissionAcgObjectiveInteractionService.cs");
            string claims =
                ReadMember(
                    objective,
                    "internal static bool ClaimsGeneratedUseItemOnItem(");
            StringAssert.Contains(
                claims,
                "MissionAcgObjectiveRuntime.IsGeneratedMissionItem(");
            StringAssert.Contains(
                claims,
                "MissionAcgBindingRuntime.IsGeneratedMissionKey(");
            Assert.IsFalse(
                claims.IndexOf("IsOwnedMissionItem(", StringComparison.Ordinal) >= 0);
            Assert.IsFalse(
                claims.IndexOf("IsOwnedMissionKey(", StringComparison.Ordinal) >= 0);
        }

        [TestMethod]
        public void MissingGeneratedStateCannotReplayLegacyQfuOrAbandonment()
        {
            string accept =
                ReadMissionSource("MissionAcceptService.cs");
            string resend =
                ReadMember(accept, "public static bool TryResendForLogin(");
            StringAssert.Contains(
                resend,
                "!MissionCompleteService.IsGeneratedAcceptedMission(entry)");

            string oneWindow =
                ReadMember(accept, "private static bool SendOneMissionWindow(");
            AssertOrdered(
                oneWindow,
                "TryGetOwnedByAcceptedQuest(",
                "IsGeneratedAcceptedMission(stored)",
                "reason=missing-generated-binding",
                "return false;");

            string quest =
                ReadZoneSource("Core/MessageHandlers/QuestMessageHandler.cs");
            string delete =
                ReadMember(quest, "protected override void Read(");
            AssertOrdered(
                delete,
                "TryGetOwnedByAcceptedQuest(",
                "IsGeneratedAcceptedMission(stored)",
                "reason=missing-generated-binding",
                "return;");
        }

        [TestMethod]
        public void GeneratedExteriorRejectionIsConsumedBeforeOtherHandlers()
        {
            string entry =
                ReadZoneSource(
                    "Core/MessageHandlers/MissionEntranceInteractionHandler.cs");
            string handle =
                ReadMember(entry, "public bool TryHandleUse(");

            AssertOrdered(
                handle,
                "HasOwnedExteriorMarker(",
                "HasGeneratedAcceptedExteriorClaim(",
                "MissionInstanceService.TryEnterMissionInstance(client, target)",
                "AcknowledgeDenied(",
                "return true;");

            string service =
                ReadMissionSource("MissionInstanceService.cs");
            string enter =
                ReadMember(
                    service,
                    "internal static bool TryEnterMissionInstance(");
            StringAssert.Contains(
                enter,
                "generatedAcceptedExteriorClaim");
            StringAssert.Contains(
                enter,
                "entranceTarget");
            StringAssert.Contains(
                enter,
                "HasOwnedExteriorMarker(");
            AssertOrdered(
                enter,
                "HasGeneratedAcceptedExteriorClaim(",
                "HasOwnedExteriorMarker(",
                "if (generatedAcceptedExteriorClaim)",
                "return false;",
                "MissionKeyGrantService.HasNonGeneratedMissionKey(");

            string exteriorClaim =
                ReadMember(
                    service,
                    "internal static bool HasGeneratedAcceptedExteriorClaim(");
            AssertOrdered(
                exteriorClaim,
                "entry.MarkerPlayfield == playfield",
                "target.Type == IdentityType.Door",
                "entry.EntranceLow");
            StringAssert.Contains(
                ReadMember(
                    service,
                    "internal static bool IsAcceptedMissionEntranceUse("),
                "entry.MarkerPlayfield != currentPlayfield");
        }

        [TestMethod]
        public void ClaimedGeneratedPfNeverUsesLegacyExitPayloadOrBuilding()
        {
            string service =
                ReadMissionSource("MissionInstanceService.cs");
            StringAssert.Contains(
                ReadMember(
                    service,
                    "internal static bool TryExitMissionInstance("),
                "ClaimsGeneratedLivePlayfield(");
            StringAssert.Contains(
                ReadMember(
                    service,
                    "internal static byte[] GetLiveGeneratorPayload("),
                "ClaimsGeneratedLivePlayfield(");
            StringAssert.Contains(
                ReadMember(
                    service,
                    "internal static int GetLiveBuildingInstance("),
                "ClaimsGeneratedLivePlayfield(");
            AssertOrdered(
                ReadMember(
                    service,
                    "internal static bool ResolveInteriorExitDoor("),
                "TryResolveByPlayfield(",
                "ClaimsGeneratedLivePlayfield(",
                "return false;",
                "MissionInstanceShapeCatalog.PickShape(");

            string paf =
                ReadZoneSource(
                    "Core/MessageHandlers/PlayfieldAnarchyFMessageHandler.cs");
            StringAssert.Contains(paf, "ClaimsGeneratedLivePlayfield(");

            string teleport =
                ReadZoneSource("Core/MessageHandlers/TeleportMessageHandler.cs");
            StringAssert.Contains(teleport, "ClaimsGeneratedLivePlayfield(");
        }

        [TestMethod]
        public void ClaimedGeneratedReconnectAndSpatialStateFailClosed()
        {
            string charInPlay =
                ReadZoneSource("Core/MessageHandlers/CharInPlayMessageHandler.cs");
            AssertOrdered(
                ReadMember(charInPlay, "protected override void Read("),
                "ClaimsGeneratedLivePlayfield(",
                "MissionAcgRuntimeManager.SendForCharacter(",
                "MissionInstanceDoorReplay.SendForCharacter(");

            string spatial =
                ReadMissionSource("MissionAcgSpatialRuntime.cs");
            StringAssert.Contains(
                ReadMember(
                    spatial,
                    "internal static bool TryValidatePlayerMove("),
                "ClaimsGeneratedLivePlayfield(");

            string combatPair =
                ReadMember(
                    spatial,
                    "internal static bool TryValidateCombatPair(");
            StringAssert.Contains(
                combatPair,
                "ClaimsGeneratedLivePlayfield(");
            Assert.IsFalse(
                combatPair.IndexOf(
                    "IsAllocatableRange(",
                    StringComparison.Ordinal) >= 0);

            string instanceService =
                ReadMissionSource("MissionInstanceService.cs");
            string restamp =
                ReadMember(
                    instanceService,
                    "internal static void TryRestampOutdoorReturnFromAccepted(");
            AssertOrdered(
                restamp,
                "TryResolveByLivePlayfield(",
                "ClaimsGeneratedLivePlayfield(",
                "MissionAcceptedStore.GetAll(");
            StringAssert.Contains(
                restamp,
                "!MissionCompleteService.IsGeneratedAcceptedMission(entry)");

            string statels =
                ReadZoneSource(
                    "Core/Playfields/PlayfieldStatelTransitionRuntimeService.cs");
            AssertOrdered(
                ReadMember(statels, "internal void CheckStatelCollision("),
                "TryHandleMissionInstanceExit(",
                "ClaimsGeneratedLivePlayfield(",
                "TryHandleCapturedSubwayProxyEntry(");
            string walkIn =
                ReadMember(
                    statels,
                    "private bool TryHandleMissionInstanceEntry(");
            AssertOrdered(
                walkIn,
                "HasGeneratedAcceptedExteriorClaim(",
                "MissionKeyGrantService.HasMissionKey(",
                "return generatedExteriorClaim;");
        }

        [TestMethod]
        public void TrueLegacyAndAuthoredQuestPathsRemainPresent()
        {
            string npcRuntime =
                ReadZoneSource("Core/Playfields/NPCRuntimeService.cs");
            StringAssert.Contains(
                npcRuntime,
                "MissionInstanceSpawn.SpawnForPlayfield(");

            string itemUse =
                ReadZoneSource(
                    "Core/MessageHandlers/UseItemOnItemInteractionHandler.cs");
            StringAssert.Contains(
                itemUse,
                "MissionRepairService.TryHandleUseItemOnItem(");
            StringAssert.Contains(
                itemUse,
                "MissionFindItemService.TryHandleReturnToTerminal(");
            StringAssert.Contains(
                itemUse,
                "MarcusB194GasFireProgressTracker.TryHandleUseItemOnItem(");

            string authoredTests =
                ReadRepositoryFile(
                    "AORebirth/Libraries/Source/AOtomation/"
                    + "AOtomation.Messaging/src/"
                    + "SmokeLounge.AOtomation.Messaging.Tests/"
                    + "PersistentMissionFoundationTests.cs");
            StringAssert.Contains(
                authoredTests,
                "class PersistentMissionFoundationTests");
        }

        private static void AssertOrdered(
            string source,
            params string[] fragments)
        {
            int cursor = -1;
            for (int i = 0; i < fragments.Length; i++)
            {
                int next =
                    source.IndexOf(
                        fragments[i],
                        cursor + 1,
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    next > cursor,
                    "Expected fragment in order: " + fragments[i]);
                cursor = next;
            }
        }

        private static string ReadMember(string source, string signature)
        {
            int signatureIndex =
                source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
            {
                return string.Empty;
            }

            int openingBrace = source.IndexOf('{', signatureIndex);
            if (openingBrace < 0)
            {
                return string.Empty;
            }

            int depth = 0;
            bool inString = false;
            bool inCharacter = false;
            bool escaped = false;
            bool inLineComment = false;
            bool inBlockComment = false;
            for (int i = openingBrace; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        inLineComment = false;
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (inString || inCharacter)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (inString && current == '"')
                    {
                        inString = false;
                    }
                    else if (inCharacter && current == '\'')
                    {
                        inCharacter = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '\'')
                {
                    inCharacter = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(
                            signatureIndex,
                            i - signatureIndex + 1);
                    }
                }
            }

            return string.Empty;
        }

        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            return ReadRepositoryFile(
                "AORebirth/Server/ZoneEngine/"
                + relativePath.Replace('\\', '/'));
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            DirectoryInfo current =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return File.ReadAllText(
                        Path.Combine(
                            current.FullName,
                            relativePath.Replace(
                                '/',
                                Path.DirectorySeparatorChar)));
                }

                current = current.Parent;
            }

            Assert.Fail("Repository root could not be located.");
            return string.Empty;
        }
    }
}
