namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgTokenProgressRuntimeContractTests
    {
        [TestMethod]
        public void GeneratedTrackerRoutesBeforeLegacyGlobalClassification()
        {
            string tracker =
                ReadMissionSource("MissionTokenProgressTracker.cs");
            string notify =
                ReadMember(
                    tracker,
                    "public static void NotifyTrashKilled(");
            int generatedRange =
                notify.IndexOf(
                    "MissionAcgAllocationService.IsAllocatableRange(",
                    StringComparison.Ordinal);
            int generatedRuntime =
                notify.IndexOf(
                    "MissionAcgTokenProgressRuntime.",
                    StringComparison.Ordinal);
            int legacyObjective =
                notify.IndexOf(
                    "MissionFindPersonService.IsFindPersonTarget(",
                    StringComparison.Ordinal);
            int legacyAggressive =
                notify.IndexOf(
                    "MissionInstanceMobCombat.IsAggressive(",
                    StringComparison.Ordinal);
            int legacyGrey =
                notify.IndexOf(
                    "IsCountableTrash(",
                    StringComparison.Ordinal);

            Assert.IsTrue(
                generatedRange >= 0
                && generatedRuntime > generatedRange
                && legacyObjective > generatedRuntime
                && legacyAggressive > legacyObjective
                && legacyGrey > legacyAggressive);
        }

        [TestMethod]
        public void GeneratedRegistrationAndCleanupBypassLegacySessionDictionaries()
        {
            string tracker =
                ReadMissionSource("MissionTokenProgressTracker.cs");
            AssertGeneratedBranchPrecedesLegacyLock(
                ReadMember(tracker, "public static void BindCharacter("),
                "MissionAcgTokenProgressRuntime.");
            AssertGeneratedBranchPrecedesLegacyLock(
                ReadMember(tracker, "public static void ClearPlayfield("),
                "MissionAcgTokenProgressRuntime.");

            string hasPlayfield =
                ReadMember(tracker, "internal static bool HasPlayfield(");
            int generated =
                hasPlayfield.IndexOf(
                    "HasPlayfieldRegistration(",
                    StringComparison.Ordinal);
            int legacy =
                hasPlayfield.IndexOf(
                    "ByPlayfield.ContainsKey(",
                    StringComparison.Ordinal);
            Assert.IsTrue(
                generated >= 0 && legacy > generated);
        }

        [TestMethod]
        public void DeathObservationResolvesExactBindingObjectiveOwnerAndSourceBeforeClaim()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string observe =
                ReadMember(runtime, "internal static void TryObserveDeath(");
            if (observe.Length == 0)
            {
                observe =
                    ReadMember(runtime, "internal static bool TryObserveDeath(");
            }

            AssertOrdered(
                observe,
                "attacker.Playfield == null",
                "attacker.Playfield.Identity.Instance != livePlayfield",
                "MissionAcgBindingRuntime.TryResolveByLivePlayfield(",
                "TryValidateSoloBinding(",
                "binding.Binding.OwnerIdentity",
                "MissionAcgObjectiveRuntime.TryGetByAccepted(",
                "MissionInstanceMobCombat.IsAggressive(",
                "MissionAcgOperationalRuntime.TryResolveTokenProgressSource(",
                "MissionAcgExpiryRuntime.TryClaimTokenProgress(");
            StringAssert.Contains(
                observe,
                "int livePlayfield = victim.Playfield.Identity.Instance;");
            StringAssert.Contains(observe, "capturedSlot");
            StringAssert.Contains(observe, "spawnGeneration");
        }

        [TestMethod]
        public void ExactOperationalSourceIsDeadMaterializedAmbientOwnedByTheBinding()
        {
            string operational =
                ReadMissionSource("MissionAcgOperationalRuntime.cs");
            string resolve =
                ReadMember(
                    operational,
                    "internal static bool TryResolveTokenProgressSource(");

            AssertOrdered(
                resolve,
                "binding.Binding.AcceptedQuestIdentity.Instance",
                "MatchesTokenProgressBinding(",
                "state.CleanupState",
                "state.TryGetNpc(",
                "npc.RuntimeIdentity.Equals(",
                "npc.Role != MissionAcgNpcRole.Ambient",
                "!npc.IsMaterializable",
                "npc.LifeState != MissionAcgNpcLifeState.Dead",
                "npc.CleanupCompleted",
                "capturedSlot = npc.CapturedSlot",
                "spawnGeneration = npc.SpawnGeneration");
        }

        [TestMethod]
        public void TokenClaimArbitratesWithVerificationCompletionExpiryAndAbandonment()
        {
            string expiry =
                ReadMissionSource("MissionAcgExpiryRuntime.cs");
            string tokenClaim =
                ReadMember(
                    expiry,
                    "internal static bool TryClaimTokenProgress(");
            AssertOrdered(
                tokenClaim,
                "TryResolveExactCurrentObjective(",
                "lock (Gate)",
                "ExpiryClaims.Contains(",
                "CompletionClaims.Contains(",
                "AbandonmentClaims.Contains(",
                "TokenProgressClaims.Contains(",
                "ObjectiveVerificationClaims.Contains(",
                "MissionAcgExpiryPolicy.IsDue(",
                "TokenProgressClaims.Add(");

            StringAssert.Contains(
                ReadMember(
                    expiry,
                    "internal static bool TryClaimObjectiveVerification("),
                "TokenProgressClaims.Contains(");
            StringAssert.Contains(
                ReadMember(
                    expiry,
                    "internal static bool TryClaimCompletionTransition("),
                "TokenProgressClaims.Contains(");
            StringAssert.Contains(
                ReadMember(
                    expiry,
                    "internal static bool TryClaimAbandonment("),
                "TokenProgressClaims.Contains(");
            StringAssert.Contains(
                ReadMember(expiry, "private static bool TryClaimExpiry("),
                "TokenProgressClaims.Contains(");
        }

        [TestMethod]
        public void SidecarInitializationFollowsMissionRestorationAndPrecedesExpiry()
        {
            string binding =
                ReadMissionSource("MissionAcgBindingRuntime.cs");
            AssertOrdered(
                ReadMember(binding, "internal static void Initialize("),
                "MissionAcgObjectiveRuntime.Initialize(",
                "MissionAcgOperationalRuntime.Initialize(",
                "MissionAcgTokenProgressRuntime.Initialize(",
                "MissionAcgExpiryRuntime.Initialize(");
        }

        [TestMethod]
        public void StartupReconcilesTokenLifecycleWithoutRegressingCompletionSeal()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string initialize =
                ReadMember(runtime, "internal static void Initialize(");
            string ensure =
                ReadMember(
                    runtime,
                    "private static bool TryEnsureStateLocked(");
            string mirror =
                ReadMember(
                    runtime,
                    "private static bool TryMirrorBindingLifecycleLocked(");

            StringAssert.Contains(
                initialize,
                "TryMirrorBindingLifecycleLocked(");
            StringAssert.Contains(
                ensure,
                "TryMirrorBindingLifecycleLocked(");
            StringAssert.Contains(
                mirror,
                "MissionAcgTokenProgressState.CanTransition(");
            StringAssert.Contains(mirror, "WithLifecycle(");
            StringAssert.Contains(
                mirror,
                "MissionAcgLifecycleState.CompletionStarted");
            StringAssert.Contains(
                mirror,
                "Never regress that crash boundary.");
        }

        [TestMethod]
        public void MissingSidecarMigrationIsSafeOnlyBeforeAnyCountableDeath()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string ensure =
                ReadMember(
                    runtime,
                    "private static bool TryEnsureStateLocked(");

            AssertOrdered(
                ensure,
                "MissionAcgOperationalRuntime.TryGetTokenProgressSources(",
                "bool priorDeath",
                "MissionAcgTokenProgressState.CreateInvalid(",
                "MissionAcgTokenProgressState.Create(");
            StringAssert.Contains(ensure, "store.TryCreate(");
            StringAssert.Contains(
                ensure,
                "Legacy active token progress is ambiguous and was rejected.");
        }

        [TestMethod]
        public void EventPhasesArePersistedBeforeEachExternallyVisibleAdvance()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string apply =
                ReadMember(
                    runtime,
                    "private static bool TryApplyDeathLocked(");

            int validated =
                apply.IndexOf(
                    "AddValidatedDeath(",
                    StringComparison.Ordinal);
            int validatedWrite =
                apply.IndexOf(
                    "TryReplaceLocked(",
                    validated,
                    StringComparison.Ordinal);
            int resume =
                apply.IndexOf(
                    "TryResumeEventLocked(",
                    validatedWrite,
                    StringComparison.Ordinal);

            string resumeMember =
                ReadMember(
                    runtime,
                    "private static bool TryResumeEventLocked(");

            Assert.IsTrue(
                validated >= 0
                && validatedWrite > validated
                && resume > validatedWrite);
            AssertOrdered(
                resumeMember,
                "MissionAcgTokenProgressEventPhase.Validated",
                "DurablyApplied",
                "ClientUpdatePending",
                "AdvanceDeath(",
                "TryReplaceLocked(");
        }

        [TestMethod]
        public void DuplicateResolutionPrecedesAnyNewAppliedCount()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string apply =
                ReadMember(
                    runtime,
                    "private static bool TryApplyDeathLocked(");

            int existing =
                apply.IndexOf("TryGetEvent(", StringComparison.Ordinal);
            int add =
                apply.IndexOf("AddValidatedDeath(", StringComparison.Ordinal);
            int resume =
                apply.IndexOf(
                    "TryResumeEventLocked(",
                    StringComparison.Ordinal);
            Assert.IsTrue(
                existing >= 0 && add > existing && resume > add);

            string state =
                ReadMissionSource("MissionAcgTokenProgressState.cs");
            string addState =
                ReadMember(
                    state,
                    "internal MissionAcgTokenProgressState AddValidatedDeath(");
            StringAssert.Contains(addState, "TryGetEvent(");
            StringAssert.Contains(addState, "already");
        }

        [TestMethod]
        public void RestartReconciliationNeverReappliesAnAppliedEvent()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string reconcile =
                ReadMember(
                    runtime,
                    "private static bool TryReconcileStateLocked(");
            StringAssert.Contains(reconcile, "TryGetEvent(");
            StringAssert.Contains(reconcile, "TryApplyDeathLocked(");

            string resume =
                ReadMember(
                    runtime,
                    "private static bool TryResumeEventLocked(");
            AssertOrdered(
                resume,
                "MissionAcgTokenProgressEventPhase.Validated",
                "DurablyApplied",
                "ClientUpdatePending",
                "AdvanceDeath(",
                "TryReplaceLocked(");
            Assert.IsFalse(resume.Contains("AddValidatedDeath("));
        }

        [TestMethod]
        public void PendingFeedbackIsDurableBeforeSendAndSentMeansServerSend()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string send =
                ReadMember(
                    runtime,
                    "private static bool TrySendPendingFeedback(");
            AssertOrdered(
                send,
                "ClientUpdatePending",
                "character.Send(",
                "ClientUpdateSent",
                "TryReplaceLocked(");
            Assert.IsFalse(
                send.Contains("acknowledg"),
                "The client-update phase records a server send, not an acknowledgement.");
        }

        [TestMethod]
        public void ReconnectRetriesPendingFeedbackAfterAcceptedQfuResend()
        {
            AssertReconnectOrdering(
                ReadZoneSource("Core/PacketHandlers/ClientConnected.cs"));
            AssertReconnectOrdering(
                ReadZoneSource(
                    "Core/MessageHandlers/CharInPlayMessageHandler.cs"));
        }

        [TestMethod]
        public void CompletionSealsTokenProgressBeforeObjectiveVerified()
        {
            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            string verify =
                ReadMember(
                    completion,
                    "internal static bool TryPersistObjectiveVerification(");

            AssertOrdered(
                verify,
                "MissionAcgExpiryRuntime.TryClaimObjectiveVerification(",
                "MissionTokenProgressTracker.SealGeneratedProgress(",
                "MissionAcgObjectiveLifecycle.Verified",
                "MissionAcgCompletionPhase.ObjectiveVerified",
                "MissionAcgExpiryRuntime.ReleaseObjectiveVerificationClaim(");
        }

        [TestMethod]
        public void CompletionReadsOnlyExactDurablySealedGeneratedProgress()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string resolve =
                ReadMember(
                    runtime,
                    "internal static bool TryGetSealedProgress(");

            AssertOrdered(
                resolve,
                "TryValidateExactObjective(",
                "binding.Binding.AcceptedQuestIdentity.Instance",
                "InvalidAccepted.Contains(",
                "ByAccepted.TryGetValue(",
                "current.State.Matches(",
                "MissionAcgLifecycleState.CompletionStarted");
            Assert.IsFalse(
                resolve.Contains("MissionTokenProgressTracker."));
            Assert.IsFalse(resolve.Contains("MissionTypeCatalog"));
            Assert.IsFalse(resolve.Contains("GetAll("));
        }

        [TestMethod]
        public void CompletionTransitionResealsIdempotentlyBeforeCompletionStarted()
        {
            string completion =
                ReadMissionSource("MissionAcgCompletionJournalService.cs");
            string continuation =
                ReadMember(completion, "private static bool Continue(");
            int objectiveVerified =
                continuation.IndexOf(
                    "objective.State.Phase == MissionAcgCompletionPhase.ObjectiveVerified",
                    StringComparison.Ordinal);
            int seal =
                continuation.IndexOf(
                    "MissionTokenProgressTracker.SealGeneratedProgress(",
                    objectiveVerified,
                    StringComparison.Ordinal);
            int transition =
                continuation.IndexOf(
                    "MissionAcgBindingRuntime.TryTransition(",
                    seal,
                    StringComparison.Ordinal);
            Assert.IsTrue(
                objectiveVerified >= 0
                && seal > objectiveVerified
                && transition > seal);
        }

        [TestMethod]
        public void TerminalLifecycleRejectsNewEventsWhileRetainingAuditState()
        {
            string state =
                ReadMissionSource("MissionAcgTokenProgressState.cs");
            string canAccept =
                ReadMember(state, "internal bool CanAcceptDeaths");
            StringAssert.Contains(
                canAccept,
                "MissionAcgLifecycleState.Active");
            Assert.IsFalse(canAccept.Contains("Completed"));
            Assert.IsFalse(canAccept.Contains("Abandoned"));
            Assert.IsFalse(canAccept.Contains("Expired"));

            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string lifecycle =
                ReadMember(
                    runtime,
                    "internal static void OnBindingStateChanged(");
            StringAssert.Contains(lifecycle, "WithLifecycle(");
            StringAssert.Contains(lifecycle, "TryReplaceLocked(");
        }

        [TestMethod]
        public void CleanupClearsOnlyTransientRegistrationAndKeepsDurableReplayAudit()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string clear =
                ReadMember(
                    runtime,
                    "internal static void ClearPlayfieldRegistration(");
            StringAssert.Contains(clear, "RegisteredPlayfields.Remove(");
            Assert.IsFalse(clear.Contains("Store."));
            Assert.IsFalse(clear.Contains("File.Delete"));

            string tracker =
                ReadMissionSource("MissionTokenProgressTracker.cs");
            string trackerClear =
                ReadMember(tracker, "public static void ClearPlayfield(");
            StringAssert.Contains(
                trackerClear,
                "MissionAcgTokenProgressRuntime.ClearPlayfieldRegistration(");
        }

        [TestMethod]
        public void LegacyAuthoredMissionTokenPathRemainsIntact()
        {
            string tracker =
                ReadMissionSource("MissionTokenProgressTracker.cs");
            string notify =
                ReadMember(
                    tracker,
                    "public static void NotifyTrashKilled(");
            StringAssert.Contains(
                notify,
                "MissionFindPersonService.IsFindPersonTarget(");
            StringAssert.Contains(
                notify,
                "MissionInstanceMobCombat.IsAggressive(");
            StringAssert.Contains(notify, "IsCountableTrash(");
            StringAssert.Contains(notify, "ByPlayfield.TryGetValue(");
            StringAssert.Contains(notify, "new FormatFeedbackMessage");

            string eligibility =
                ReadMember(
                    tracker,
                    "public static bool HasFullTokenChance(");
            StringAssert.Contains(
                eligibility,
                "ByCharacter.TryGetValue(");
            Assert.IsFalse(
                eligibility.Contains("MissionAcgTokenProgressRuntime."));
        }

        [TestMethod]
        public void GeneratedProgressPolicyDoesNotGrantQfuSchemaOrTeamDistribution()
        {
            string runtime =
                ReadMissionSource("MissionAcgTokenProgressRuntime.cs");
            string state =
                ReadMissionSource("MissionAcgTokenProgressState.cs");
            string store =
                ReadMissionSource("MissionAcgTokenProgressStore.cs");
            string generated = runtime + state + store;

            Assert.IsFalse(generated.Contains("QuestFullUpdate"));
            Assert.IsFalse(generated.Contains("GrantMissionToken"));
            Assert.IsFalse(generated.Contains("MissionKeyGrantService"));
            Assert.IsFalse(generated.Contains("BaseInventory"));
            StringAssert.Contains(
                state,
                "MissionAcgTokenClaimDisposition.Eligible");
            StringAssert.Contains(
                state,
                "progress.Percent < 100");
            Assert.IsFalse(generated.Contains("ALTER TABLE"));
            Assert.IsFalse(generated.Contains("CREATE TABLE"));
            StringAssert.Contains(runtime, "TeamIdentity != null");
            StringAssert.Contains(
                runtime,
                "Generated token progress currently requires an exact solo binding.");
        }

        private static void AssertGeneratedBranchPrecedesLegacyLock(
            string member,
            string generatedFragment)
        {
            int generated =
                member.IndexOf(generatedFragment, StringComparison.Ordinal);
            int legacy =
                member.IndexOf("lock (Sync)", StringComparison.Ordinal);
            Assert.IsTrue(generated >= 0 && legacy > generated);
        }

        private static void AssertReconnectOrdering(string source)
        {
            int resend =
                source.IndexOf(
                    "MissionAcceptService.TryResendForLogin(",
                    StringComparison.Ordinal);
            int pending =
                source.IndexOf(
                    "TryResumePendingClientUpdates(",
                    StringComparison.Ordinal);
            Assert.IsTrue(resend >= 0 && pending > resend);
        }

        private static void AssertOrdered(
            string source,
            params string[] fragments)
        {
            int previous = -1;
            for (int i = 0; i < fragments.Length; i++)
            {
                int current =
                    source.IndexOf(
                        fragments[i],
                        previous + 1,
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    current > previous,
                    "Expected ordered fragment not found: " + fragments[i]);
                previous = current;
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
            DirectoryInfo current =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return File.ReadAllText(
                        Path.Combine(
                            current.FullName,
                            "AORebirth/Server/ZoneEngine",
                            relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }

                current = current.Parent;
            }

            Assert.Fail("Repository root could not be located.");
            return string.Empty;
        }
    }
}
