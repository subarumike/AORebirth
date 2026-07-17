namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Pf127GeometryOnlySafeModeTests
    {
        [TestMethod]
        public void SafeModeBranchesBeforeFullCaptureAndSubscribesOnlyOneCallback()
        {
            string repositoryRoot = FindRepositoryRoot();
            string mainText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Main.cs"));
            string run = ExtractMethodBlock(mainText, "public override void Run(string pluginDir)");
            string initialize = ExtractMethodBlock(mainText, "private void Initialize(string pluginDir)");
            string safeStart = ExtractMethodBlock(
                mainText,
                "private void StartMinimalPf127CaptureNoThrow(string pluginDir)");
            string safeTeardown = ExtractMethodBlock(
                mainText,
                "private void TeardownMinimalPf127CaptureNoThrow()");

            int requestBranch = run.IndexOf(
                "MinimalPf127Capture.ConsumeRequestNoThrow(pluginDir)",
                StringComparison.Ordinal);
            Assert.IsTrue(requestBranch >= 0, "Run must recognize the explicit safe-mode request.");
            Assert.IsTrue(
                requestBranch < run.IndexOf("this.Initialize(pluginDir)", StringComparison.Ordinal)
                && initialize.Contains("this.OpenFreshCaptureSession(")
                && initialize.Contains("Network.PacketReceived +="),
                "Safe mode must branch before full session creation and every legacy callback subscription.");
            Assert.IsTrue(
                safeStart.Contains("MinimalPf127Capture.TryCreate(")
                && safeStart.Contains("Game.OnUpdate += this.OnMinimalPf127CaptureUpdate")
                && safeStart.Contains("return;")
                && !safeStart.Contains("Network.")
                && !safeStart.Contains("DynelManager.")
                && !safeStart.Contains("Game.PlayfieldInit")
                && !safeStart.Contains("Game.Teleport")
                && !safeStart.Contains("Chat.RegisterCommand")
                && !safeStart.Contains("CombatLootSmoke")
                && !safeStart.Contains("OpenFreshCaptureSession"),
                "Safe startup must be isolated to its one no-throw update callback and fail closed without falling through.");
            Assert.IsTrue(
                safeTeardown.Contains("Game.OnUpdate -= this.OnMinimalPf127CaptureUpdate")
                && safeTeardown.Contains("DisposeNoThrow")
                && !safeTeardown.Contains("FinalizeCapture"),
                "Safe teardown must unsubscribe only its callback and must not enter comprehensive finalization.");
        }

        [TestMethod]
        public void SafeCollectorWaitsForStablePf127AndNeverLoadsSurfacesAutomatically()
        {
            string repositoryRoot = FindRepositoryRoot();
            string safeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\MinimalPf127Capture.cs"));
            string geometryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"));
            string update = ExtractMethodBlock(safeText, "private void UpdateCore(DateTime capturedUtc)");
            string geometryWriter = ExtractMethodBlock(
                geometryText,
                "private void TryWriteCanonicalGeometry()");

            Assert.IsTrue(
                safeText.Contains("RequiredStableDuration = TimeSpan.FromSeconds(5)")
                && safeText.Contains("RequiredStableTicks = 20")
                && update.IndexOf("if (Game.IsZoning)", StringComparison.Ordinal)
                   < update.IndexOf("TryCaptureStableSignal", StringComparison.Ordinal)
                && update.IndexOf("this.stableTickCount < RequiredStableTicks", StringComparison.Ordinal)
                   < update.IndexOf("this.geometryCapture.ExecuteUpdateBoundary(", StringComparison.Ordinal),
                "No native PF collection may be touched before the attach-inside zoning and identity stability gate opens.");
            Assert.IsTrue(
                safeText.Contains("new Pf127GeometryCapture(")
                && safeText.Contains("true);")
                && !safeText.Contains("DevExtras.LoadAllSurfaces()"),
                "The minimal collector must select resident-surface mode and must not call the native surface loader itself.");
            Assert.IsTrue(
                geometryText.Contains("private readonly bool residentSurfacesOnly;")
                && geometryWriter.Contains("if (this.residentSurfacesOnly)")
                && geometryWriter.Contains("DevExtras.LoadAllSurfaces is disabled in geometry-only safe mode")
                && geometryWriter.IndexOf("if (this.residentSurfacesOnly)", StringComparison.Ordinal)
                   < geometryWriter.IndexOf("DevExtras.LoadAllSurfaces()", StringComparison.Ordinal)
                && geometryWriter.Contains("WriteCanonicalGeometryAttempt(attemptPath)")
                && geometryWriter.Contains("stableGeometryCandidateSha256"),
                "Safe mode must use two stable topology-complete resident snapshots without auto-loading native surfaces.");
        }

        [TestMethod]
        public void SafeCollectorFailsClosedAndCapturesPromotionCoverage()
        {
            string repositoryRoot = FindRepositoryRoot();
            string safeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\MinimalPf127Capture.cs"));
            string geometryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"));
            string launcherText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\start-aosharp-live-capture.cmd"));

            Assert.IsTrue(
                safeText.Contains("\\\"requested\\\": true")
                && safeText.Contains("\\\"armed\\\": ")
                && safeText.Contains("\\\"complete\\\": ")
                && safeText.Contains("\\\"recaptureRequired\\\": ")
                && safeText.Contains("explicitly requested but never armed")
                && safeText.Contains("Current playfield model resource is "),
                "A requested safe capture must never report success when it did not arm inside PF127.");
            Assert.IsTrue(
                safeText.Contains("this.geometryCapture.VergilSameIdentityClearAndBlockedObserved")
                && safeText.Contains("a combat trigger is not required")
                && !safeText.Contains("localPlayer.FightingTarget")
                && !safeText.Contains("TryObserveVergilCombatContext")
                && !safeText.Contains("this.geometryCapture.RequestCombatSample()")
                && !safeText.Contains("this.geometryCapture.Pf127CombatObserved")
                && geometryText.Contains("VergilSameIdentityClearAndBlockedObserved")
                && geometryText.Contains("vergilClearIdentityKeys")
                && geometryText.Contains("vergilBlockedIdentityKeys")
                && geometryText.Contains("&& doorState.Usable)"),
                "Safe-mode acceptance must use same-identity clear and blocked raw/plus-one evidence with matching usable door batches, without relying on local FightingTarget state.");
            Assert.IsTrue(
                geometryText.Contains("DoorLinkUnavailableForClientSafety")
                && geometryText.Contains("unavailable_not_read_for_client_safety")
                && geometryText.Contains("doorLinkCapturePolicy")
                && geometryText.Contains("RawLink1Index,Link1Resolution")
                && !geometryText.Contains("door.RoomLink1")
                && !geometryText.Contains("door.RoomLink2")
                && !geometryText.Contains("room.Doors")
                && !geometryText.Contains("PropertyInfo")
                && !geometryText.Contains("BindingFlags")
                && !geometryText.Contains("GetProperty(")
                && !geometryText.Contains("GetValue(door")
                && !geometryText.Contains("room.NumDoors"),
                "Safe mode must explicitly leave door links unavailable and perform no private, reflected, or native link reads.");
            Assert.IsTrue(
                launcherText.Contains("--pf127-geometry-only")
                && launcherText.Contains("PF127_GEOMETRY_ONLY_REQUEST")
                && launcherText.Contains("pf127-geometry-only.request")
                && launcherText.Contains("already inside and stable in Subway"),
                "The approved launcher must own the explicit attach-inside safe-mode request.");
        }

        [TestMethod]
        public void SafeCollectorRetriesMissingResidentSurfaceBeforeDereference()
        {
            string repositoryRoot = FindRepositoryRoot();
            string geometryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"));
            string roomProjection = ExtractMethodBlock(
                geometryText,
                "private static RoomGeometrySourceSnapshot CaptureRoomGeometrySourceSnapshot(Room room)");
            string geometryWriter = ExtractMethodBlock(
                geometryText,
                "private void TryWriteCanonicalGeometry()");

            int residentGuard = roomProjection.IndexOf(
                "N3Zone_t.GetSurface(room.Pointer)",
                StringComparison.Ordinal);
            int surfaceDereference = roomProjection.IndexOf(
                "SurfaceResource surface = room.SurfaceResource",
                StringComparison.Ordinal);
            int retryCatch = geometryWriter.IndexOf(
                "catch (ResidentSurfaceIncompleteException ex)",
                StringComparison.Ordinal);
            int circuitCatch = geometryWriter.IndexOf(
                "catch (Exception ex)",
                retryCatch,
                StringComparison.Ordinal);

            Assert.IsTrue(
                residentGuard >= 0
                && surfaceDereference > residentGuard
                && roomProjection.Contains("residentSurfacePointer == IntPtr.Zero")
                && roomProjection.Contains("throw new ResidentSurfaceIncompleteException(instance)")
                && retryCatch >= 0
                && circuitCatch > retryCatch,
                "A nonresident room surface must be recorded as retryable before Room.SurfaceResource can dereference it.");
            string retryBlock = geometryWriter.Substring(retryCatch, circuitCatch - retryCatch);
            Assert.IsTrue(
                retryBlock.Contains("residentSurfaceIncompleteRetryCount")
                && retryBlock.Contains("resident-surface-incomplete-retryable")
                && !retryBlock.Contains("GeometryStageCircuitBroken"),
                "Resident-surface incompleteness must not circuit-break the safe collector generation.");
        }

        [TestMethod]
        public void PromotionRequiresExplicitClientSafeDoorLinkUnavailability()
        {
            string repositoryRoot = FindRepositoryRoot();
            string validatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"tools-temp\AOSharpCaptureAnalyzer\Pf127LineOfSightPromotionValidator.cs"));

            Assert.IsTrue(
                validatorText.Contains("doorLinkSchemaVersion")
                && validatorText.Contains("doorLinkCapturePolicy")
                && validatorText.Contains("unavailable_not_read_for_client_safety")
                && validatorText.Contains("RawLink1Index")
                && validatorText.Contains("Link1Resolution")
                && validatorText.Contains("RawLink2Index")
                && validatorText.Contains("Link2Resolution")
                && validatorText.Contains("expectedDoor.RequireSameLinkEvidence(observedDoor")
                && validatorText.Contains("SelfTestScenario.DoorLinkMismatch")
                && validatorText.Contains("client-safe unavailable link evidence")
                && !validatorText.Contains("doorLinkRoomSnapshot")
                && !validatorText.Contains("roomInstancesByIndex"),
                "Promotion must require explicit unavailable link evidence and never infer door-room topology.");
        }

        [TestMethod]
        public void PromotionAcceptsIdentityProvenPeriodicEvidenceWithoutCombatGate()
        {
            string repositoryRoot = FindRepositoryRoot();
            string validatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"tools-temp\AOSharpCaptureAnalyzer\Pf127LineOfSightPromotionValidator.cs"));
            string coverage = ExtractMethodBlock(
                validatorText,
                "private static void ValidateEvidenceCoverage(IList<LineOfSightPair> pairs)");

            Assert.IsTrue(
                coverage.Contains("GroupBy(pair => pair.TargetIdentityKey")
                && coverage.Contains("group.Any(pair => pair.NativeClear)")
                && coverage.Contains("group.Any(pair => !pair.NativeClear)")
                && coverage.Contains("same exact Vergil identity")
                && !coverage.Contains("pair.Key.Trigger")
                && !coverage.Contains("combat-triggered"),
                "Promotion must require clear and blocked evidence for one exact Vergil identity without requiring a combat-labeled sample.");
            Assert.IsTrue(
                validatorText.Contains("SelfTestScenario.PeriodicOnlySuccess")
                && validatorText.Contains("periodic-only accepted pair count")
                && validatorText.Contains("SelfTestScenario.SplitStateAcrossIdentities")
                && validatorText.Contains("client-safe unavailable link evidence")
                && validatorText.Contains("no complete matching door-state"),
                "Promotion self-tests must accept periodic-only proof while retaining same-identity and matching-door negative cases.");
        }

        [TestMethod]
        public void PromotionAnalyzerRunsAnyCpuAndFailsClearlyInA32BitProcess()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj"));
            string programText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"tools-temp\AOSharpCaptureAnalyzer\Program.cs"));
            string promotion = ExtractMethodBlock(
                programText,
                "private static int PromotePf127LineOfSight(string captureFolder, string outputPath)");

            Assert.IsTrue(
                projectText.Contains("<PlatformTarget>AnyCPU</PlatformTarget>")
                && projectText.Contains("<Prefer32Bit>false</Prefer32Bit>")
                && !projectText.Contains("<PlatformTarget>x86</PlatformTarget>"),
                "The offline analyzer must run as a 64-bit-capable AnyCPU executable while the injected capture plugin remains independently x86.");
            Assert.IsTrue(
                programText.Contains("AOSharpCaptureAnalyzer process bitness:")
                && programText.Contains("Environment.Is64BitProcess")
                && promotion.Contains("if (!Environment.Is64BitProcess)")
                && promotion.Contains("requires a 64-bit AOSharpCaptureAnalyzer process")
                && promotion.IndexOf("if (!Environment.Is64BitProcess)", StringComparison.Ordinal)
                   < promotion.IndexOf("Pf127LineOfSightPromotionValidator.Promote(", StringComparison.Ordinal),
                "Promotion must print process bitness and reject 32-bit execution before reading the full PF127 geometry.");
        }

        [TestMethod]
        public void LauncherRemovesSafeModeRequestOnSuccessFailureAndStaleEntry()
        {
            string repositoryRoot = FindRepositoryRoot();
            string launcherText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\start-aosharp-live-capture.cmd"));
            int initialCleanup = launcherText.IndexOf(
                "call :cleanup_pf127_geometry_request",
                StringComparison.Ordinal);
            int captureScan = launcherText.IndexOf(
                "if defined PREVIOUS_CAPTURE (",
                StringComparison.Ordinal);
            int requestArmed = launcherText.IndexOf(
                "set \"PF127_GEOMETRY_REQUEST_ARMED=1\"",
                StringComparison.Ordinal);
            int commonExit = launcherText.IndexOf(":post_arm_exit", StringComparison.Ordinal);
            int cleanupLabel = launcherText.LastIndexOf(
                ":cleanup_pf127_geometry_request",
                StringComparison.Ordinal);

            Assert.IsTrue(
                initialCleanup >= 0
                && initialCleanup < captureScan
                && requestArmed > captureScan
                && commonExit > requestArmed
                && cleanupLabel > commonExit,
                "The launcher must remove a stale request before active-capture detection and centralize every post-arm terminal path.");

            string postArmPaths = launcherText.Substring(requestArmed, commonExit - requestArmed);
            Assert.IsFalse(
                postArmPaths.Contains("exit /b"),
                "No success, helper failure, injection failure, missing-log failure, or timeout path may exit directly after the safe request is armed.");
            Assert.IsTrue(
                postArmPaths.Contains("set \"POST_ARM_EXIT_CODE=0\"")
                && postArmPaths.Contains("SUCCESS: PF127 geometry-only safe capture injected.")
                && postArmPaths.Contains("set \"POST_ARM_EXIT_CODE=1\"")
                && postArmPaths.Contains("injector launch helper failed")
                && postArmPaths.Contains("request was not confirmed consumed")
                && CountOccurrences(postArmPaths, "goto post_arm_exit") >= 7,
                "Confirmed safe-mode success and every modeled failure must converge on the common cleanup exit.");

            string commonExitBlock = launcherText.Substring(
                commonExit,
                launcherText.IndexOf(":usage", commonExit, StringComparison.Ordinal) - commonExit);
            string cleanupBlock = launcherText.Substring(cleanupLabel);
            Assert.IsTrue(
                commonExitBlock.Contains("call :cleanup_pf127_geometry_request")
                && commonExitBlock.IndexOf("call :cleanup_pf127_geometry_request", StringComparison.Ordinal)
                   < commonExitBlock.IndexOf("echo %POST_ARM_SUMMARY%", StringComparison.Ordinal)
                && cleanupBlock.Contains("del /q \"%PF127_GEOMETRY_ONLY_REQUEST%\"")
                && cleanupBlock.Contains("if exist \"%PF127_GEOMETRY_ONLY_REQUEST%\"")
                && cleanupBlock.Contains("refusing to continue because a stale marker could activate safe mode later"),
                "The one cleanup routine must delete and verify the request marker before reporting any terminal result.");
        }

        [TestMethod]
        public void LauncherKeepsSafeRequestArmedUntilInjectorCompletion()
        {
            string repositoryRoot = FindRepositoryRoot();
            string launcherText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\start-aosharp-live-capture.cmd"));
            int requestArmed = launcherText.IndexOf(
                "set \"PF127_GEOMETRY_REQUEST_ARMED=1\"",
                StringComparison.Ordinal);
            int safeWaitBranch = launcherText.IndexOf(
                "if defined PF127_GEOMETRY_ONLY_MODE (",
                requestArmed,
                StringComparison.Ordinal);
            int injectorLaunch = launcherText.IndexOf(
                "wscript.exe \"%LAUNCHER_VBS%\"",
                safeWaitBranch,
                StringComparison.Ordinal);
            int injectorExitCaptured = launcherText.IndexOf(
                "set \"LAUNCH_EXIT=%ERRORLEVEL%\"",
                injectorLaunch,
                StringComparison.Ordinal);
            int cleanupAfterArm = launcherText.IndexOf(
                "call :cleanup_pf127_geometry_request",
                requestArmed,
                StringComparison.Ordinal);

            Assert.IsTrue(
                requestArmed >= 0
                && safeWaitBranch > requestArmed
                && injectorLaunch > safeWaitBranch
                && injectorExitCaptured > injectorLaunch
                && cleanupAfterArm > injectorExitCaptured,
                "The safe request must remain present from arming through authoritative injector process completion.");

            string waitBranch = launcherText.Substring(
                safeWaitBranch,
                injectorLaunch - safeWaitBranch);
            Assert.IsTrue(
                waitBranch.Contains("WScript.Quit shell.Run(command, 2, True^)")
                && waitBranch.Contains(") else (")
                && waitBranch.Contains("WScript.Quit shell.Run(command, 2, False^)"),
                "Safe mode must synchronously wait for the injector while comprehensive mode may retain its existing asynchronous launch behavior.");

            string armedInjectorLifetime = launcherText.Substring(
                requestArmed,
                injectorExitCaptured - requestArmed);
            Assert.IsFalse(
                armedInjectorLifetime.Contains("call :cleanup_pf127_geometry_request")
                || armedInjectorLifetime.Contains("del /q \"%PF127_GEOMETRY_ONLY_REQUEST%\""),
                "No cleanup may race a late safe-mode injector before its process has exited and its exit code is captured.");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the AORebirth repository root.");
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, "Missing method: " + signature);
            int openingBrace = source.IndexOf('{', signatureIndex);
            Assert.IsTrue(openingBrace >= 0, "Missing method body: " + signature);
            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(signatureIndex, index - signatureIndex + 1);
                    }
                }
            }

            Assert.Fail("Unterminated method: " + signature);
            return string.Empty;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
