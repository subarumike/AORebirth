using System;
using System.IO;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class ProductionDeploymentWorkflowContractTests
    {
        public static void Run(string repositoryRoot)
        {
            string deploymentRoot = Path.Combine(repositoryRoot, "LinuxBuild", "deployment", "production-release");
            string upgrader = File.ReadAllText(Path.Combine(deploymentRoot, "upgrade-active-services.sh"));
            string manifest = File.ReadAllText(Path.Combine(deploymentRoot, "create-release-manifest.sh"));
            string tests = File.ReadAllText(Path.Combine(deploymentRoot, "tests", "test-upgrade-active-services.sh"));
            string acceptance = File.ReadAllText(Path.Combine(repositoryRoot, "LinuxBuild", "accept-linux-sha.sh"));
            string placementProvenance = File.ReadAllText(Path.Combine(repositoryRoot, "LinuxBuild", "placement-provenance.sh"));
            string zoneArtifactGate = File.ReadAllText(Path.Combine(repositoryRoot, "LinuxBuild", "deployment", "zone-stage9", "upgrade-live-service.sh"));
            string zoneArtifactTests = File.ReadAllText(Path.Combine(repositoryRoot, "LinuxBuild", "deployment", "zone-stage9", "test-artifact-provenance.sh"));
            string zoneUnit = File.ReadAllText(Path.Combine(repositoryRoot, "LinuxBuild", "deployment", "systemd", "ao-rebirth-zoneengine.service"));
            string zoneProgram = File.ReadAllText(Path.Combine(repositoryRoot, "AORebirth", "Server", "ZoneEngine", "Program.cs"));

            Require(upgrader.Contains("--dry-run"), "production upgrader lost dry-run mode");
            Require(upgrader.Contains("manifest source SHA mismatch"), "production upgrader lost source SHA gating");
            Require(upgrader.Contains("artifact hash mismatch"), "production upgrader lost artifact hash gating");
            Require(upgrader.Contains("unit hash mismatch"), "production upgrader lost unit hash gating");
            Require(upgrader.Contains("ownership directories differ"), "production upgrader lost shared ownership validation");
            Require(upgrader.Contains("ownership directory must not be under /tmp"), "production upgrader permits /tmp ownership locks");
            Require(upgrader.Contains("PrivateTmp contract failed"), "production upgrader lost PrivateTmp validation");
            Require(upgrader.Contains("ZoneEngine ExecStartPre ordering contract failed"), "production upgrader lost recovery ordering validation");
            Require(upgrader.Contains("online characters present; deployment policy is fail closed"), "production upgrader lost online-player fail-closed policy");
            Require(upgrader.Contains("ROLLBACK_BOTH_SERVICES=PASS"), "production upgrader lost paired rollback evidence");
            Require(upgrader.Contains("ROLLBACK_EXACT_PRIOR_TARGETS=PASS"), "production upgrader lost exact prior symlink restoration evidence");
            Require(upgrader.Contains("ROLLBACK_PRIOR_ARTIFACTS_AND_UNITS=PASS"), "production upgrader lost prior artifact/unit restoration evidence");
            Require(upgrader.Contains("ROLLBACK_NO_MIXED_STATE=PASS"), "production upgrader lost no-mixed-state rollback evidence");
            Require(upgrader.Contains("IDEMPOTENT_REDEPLOY=PASS"), "production upgrader lost idempotent no-op evidence");
            Require(upgrader.Contains("READINESS_TIMEOUT_SECONDS=30"), "production readiness timeout does not safely exceed observed startup time");
            Require(upgrader.Contains("READINESS_POLL_INTERVAL_SECONDS=1"), "production readiness polling interval changed");
            Require(upgrader.Contains("wait_for_readiness login 7500"), "LoginEngine bounded readiness wait is missing");
            Require(upgrader.Contains("wait_for_readiness zone 7501"), "ZoneEngine bounded readiness wait is missing");
            Require(upgrader.Contains("READINESS_JOURNAL_BEGIN"), "readiness timeout journal diagnostics are missing");
            Require(upgrader.Contains("state=${state_value} restarts=${restart_value}"), "readiness timeout service diagnostics are missing");
            Require(upgrader.Contains("ZoneEngine production executable contract failed"), "production upgrader does not require the headless ZoneEngine runtime");
            Require(upgrader.Contains("ZoneEngine validation lifecycle cannot be production ExecStart"), "production upgrader does not reject the listener-free ZoneEngine validation runtime");
            Require(upgrader.Contains("--recover-zone-outage"), "production upgrader lacks explicit stopped-Zone recovery mode");
            Require(upgrader.Contains("--resume-stopped-recovery"), "production upgrader cannot resume after its fail-closed outage rollback");
            Require(upgrader.Contains("STOPPED_PAIR_RECOVERY_PRECONDITION=PASS"), "stopped-pair recovery lacks an exact stopped-state precondition");
            Require(upgrader.Contains("STOPPED_PAIR_ROLLBACK_PROVENANCE=PASS"), "stopped-pair recovery lacks coherent prior-release provenance");
            Require(upgrader.Contains("STOPPED_PAIR_RECOVERY_FROZEN=PASS"), "stopped-pair recovery does not freeze both engines before mutation");
            Require(upgrader.Contains("CANDIDATE_DATABASE_COMPATIBILITY=PASS"), "production upgrader does not validate candidate binaries against the live schema");
            Require(CountOccurrences(upgrader, "AO_REBIRTH_CONFIG_PATH=\"${login_config_path}\"") == 2, "candidate LoginEngine validation does not use the governed production config exactly twice");
            Require(CountOccurrences(upgrader, "AO_REBIRTH_CONFIG_PATH=\"${zone_config_path}\"") == 2, "candidate ZoneEngine validation does not use the governed production config exactly twice");
            Require(!upgrader.Contains("AO_REBIRTH_CONFIG_PATH=\"${LOGIN_ARTIFACT_DIR}/Config.xml\""), "candidate LoginEngine validation reverted to the portable loopback artifact config");
            Require(!upgrader.Contains("AO_REBIRTH_CONFIG_PATH=\"${ZONE_ARTIFACT_DIR}/Config.xml\""), "candidate ZoneEngine validation reverted to a config that may differ from production");
            Require(upgrader.Contains("is missing or duplicated in ${environment_file}"), "production environment parsing does not reject duplicate assignments");
            Require(upgrader.Contains("^[[:space:]]*${key}[[:space:]]*="), "production environment parsing misses whitespace-prefixed systemd assignments");
            Require(upgrader.Contains("must use canonical KEY=value formatting"), "production environment parsing does not require canonical assignments");
            Require(upgrader.Contains("configuration path diverges from the governed production path"), "candidate validation does not bind runtime config paths to governed production paths");
            Require(upgrader.Contains("ZONEENGINE_OUTAGE_FROZEN=PASS"), "production upgrader does not prove the outage remains frozen");
            Require(upgrader.Contains("PRESTOP_BOUNDARY=PASS onlineCharacters=0"), "production upgrader does not recheck zero online characters before closing admission");
            Require(upgrader.Contains("LOGIN_ADMISSION_CLOSED_BOUNDARY=PASS onlineCharacters=0"), "production upgrader does not recheck zero online characters after LoginEngine closes admission");
            Require(upgrader.Contains("ZONE_PRESTOP_INVARIANT=PASS"), "production upgrader does not preserve ZoneEngine state around the closed-admission Online check");
            Require(upgrader.Contains("CLOSED_ENGINE_MUTATION_BOUNDARY=PASS onlineCharacters=0"), "production upgrader does not recheck zero online characters after both engines stop");
            Require(upgrader.Contains("ZONEENGINE_RESTART_COUNTER_RESET=PASS"), "production upgrader does not establish a zero restart baseline for controlled recovery");
            Require(upgrader.Contains("POST_START_STABILITY=PASS"), "production upgrader lacks bounded post-start restart stability");
            Require(upgrader.Contains("ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS"), "outage recovery can restart a known-incompatible rollback pair");
            Require(upgrader.Contains("2d1ebd0ffd7534c6357830891a35d2343428b56c8093b05223abe7635f67b55f"), "production upgrader does not pin the exact stale ZoneEngine readiness override");
            Require(upgrader.Contains("4ea8e3ba780f564a17ba454fa46121a6618985da3ef449d792016a41f8ac0e29"), "production upgrader does not preserve the governed daily-login drop-in");
            Require(upgrader.Contains("remove_zone_stale_notify_dropin"), "production upgrader does not remove the governed stale readiness override");
            Require(upgrader.Contains("restore_zone_notify_dropin"), "production rollback does not restore the prior readiness override exactly");
            Require(upgrader.Contains("DropInPaths"), "production upgrader does not validate all effective systemd drop-ins");
            Require(upgrader.Contains("ZONEENGINE_EFFECTIVE_READINESS_CONTRACT=PASS"), "production upgrader does not prove the effective Type=notify contract");
            Require(upgrader.Contains("[[ \"${FORMAT}\" == \"2\" ]]"), "production upgrader does not require the placement-aware manifest format");
            Require(upgrader.Contains("require_zone_placement_artifact"), "production upgrader does not fail closed on placement provenance");
            Require(upgrader.Contains("PLACEMENT_BUILD_MANIFEST_SHA256"), "production upgrader does not pin the placement build manifest");

            Require(zoneUnit.Contains("Type=notify"), "production ZoneEngine unit does not use readiness notification");
            Require(zoneUnit.Contains("NotifyAccess=main"), "production ZoneEngine unit does not authorize main-process readiness notification");
            Require(zoneUnit.Contains("ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --headless --shutdown-file /run/ao-rebirth-zoneengine/shutdown"), "production ZoneEngine unit does not start the headless listener runtime");
            Require(!zoneUnit.Contains("ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-lifecycle"), "production ZoneEngine unit incorrectly starts the listener-free lifecycle validator");

            int headlessDatabaseGate = zoneProgram.IndexOf("int headlessDatabaseValidation = ValidateDatabase();", StringComparison.Ordinal);
            int databaseValidatedState = zoneProgram.IndexOf("runtimeDatabaseValidated = true;", StringComparison.Ordinal);
            int runtimeInitialize = zoneProgram.LastIndexOf("if (!Initialize(runtimeDatabaseValidated))", StringComparison.Ordinal);
            int databasePolicyStart = zoneProgram.IndexOf("private static bool CheckRuntimeDatabase(bool databaseAlreadyValidated)", StringComparison.Ordinal);
            int databasePolicyEnd = databasePolicyStart < 0
                ? -1
                : zoneProgram.IndexOf("        /// <summary>", databasePolicyStart, StringComparison.Ordinal);
            string databasePolicy = databasePolicyStart >= 0 && databasePolicyEnd > databasePolicyStart
                ? zoneProgram.Substring(databasePolicyStart, databasePolicyEnd - databasePolicyStart)
                : string.Empty;
            int headlessRuntime = zoneProgram.IndexOf("private static void RunHeadless", StringComparison.Ordinal);
            int headlessListenerStart = zoneProgram.IndexOf("StartTheServer();", headlessRuntime, StringComparison.Ordinal);
            int headlessListenerReadyCheck = zoneProgram.IndexOf("zoneServer == null || !zoneServer.IsRunning || !zoneServer.TCPEnabled", headlessRuntime, StringComparison.Ordinal);
            int headlessReady = zoneProgram.IndexOf("NotifySystemd(\"READY=1\\nSTATUS=ZoneEngine listener is ready\")", headlessRuntime, StringComparison.Ordinal);
            int headlessInitializationFailure = zoneProgram.IndexOf("ZONEENGINE_HEADLESS_INITIALIZATION_FAILED", StringComparison.Ordinal);
            int headlessInitializationExit = zoneProgram.IndexOf("Environment.ExitCode = 1;", headlessInitializationFailure, StringComparison.Ordinal);
            int headlessInitializationFlush = zoneProgram.IndexOf("FlushHeadlessConsoleLogging();", headlessInitializationFailure, StringComparison.Ordinal);
            int headlessInitializationReturn = zoneProgram.IndexOf("return;", headlessInitializationFailure, StringComparison.Ordinal);
            int legacyLogOffAll = zoneProgram.IndexOf("Misc.LogOffAll();", StringComparison.Ordinal);
            int legacyLogOffGuard = zoneProgram.LastIndexOf("#if !AOREBIRTH_LINUX", legacyLogOffAll, StringComparison.Ordinal);
            int legacyLogOffEnd = legacyLogOffGuard < 0
                ? -1
                : zoneProgram.IndexOf("#endif", legacyLogOffGuard, StringComparison.Ordinal);
            Require(CountOccurrences(zoneProgram, "int headlessDatabaseValidation = ValidateDatabase();") == 1, "Linux ZoneEngine does not have exactly one governed in-process database gate");
            Require(headlessDatabaseGate >= 0 && databaseValidatedState > headlessDatabaseGate && runtimeInitialize > databaseValidatedState, "Linux ZoneEngine does not pass a successful governed database validation into initialization");
            Require(zoneProgram.Contains("private static bool Initialize(bool databaseAlreadyValidated)"), "ZoneEngine initialization lacks explicit database validation state");
            Require(zoneProgram.Contains("if (!CheckRuntimeDatabase(databaseAlreadyValidated))"), "ZoneEngine initialization bypasses the platform-scoped database policy");
            Require(databasePolicy.Contains("#if AOREBIRTH_LINUX") && databasePolicy.Contains("return databaseAlreadyValidated;"), "Linux ZoneEngine initialization can bypass the proven database state");
            Require(databasePolicy.Contains("#else") && databasePolicy.Contains("return Misc.CheckDatabase();"), "non-Linux interactive database bootstrap contract changed unexpectedly");
            Require(!databasePolicy.Contains("ValidateDatabase()"), "Linux ZoneEngine repeats the governed database validation during initialization");
            Require(zoneProgram.Contains("ZONEENGINE_HEADLESS_INITIALIZATION_FAILED"), "headless ZoneEngine initialization failure lacks a noninteractive error boundary");
            Require(headlessInitializationFailure >= 0 && headlessInitializationExit > headlessInitializationFailure && headlessInitializationFlush > headlessInitializationExit && headlessInitializationReturn > headlessInitializationFlush, "headless ZoneEngine initialization failure can prompt or exit successfully");
            Require(headlessListenerStart >= 0 && headlessListenerReadyCheck > headlessListenerStart && headlessReady > headlessListenerReadyCheck, "ZoneEngine reports systemd readiness before proving its TCP listener");
            Require(zoneProgram.Contains("zoneServer == null || !zoneServer.IsRunning || !zoneServer.TCPEnabled"), "ZoneEngine readiness does not require a running TCP listener");
            Require(zoneProgram.Contains("ZONEENGINE_HEADLESS_READY listener="), "ZoneEngine lacks deterministic headless readiness evidence");
            Require(zoneProgram.Contains("Environment.GetEnvironmentVariable(\"NOTIFY_SOCKET\")"), "ZoneEngine cannot notify systemd readiness");
            Require(zoneProgram.Contains("ZoneEngine could not notify systemd readiness."), "ZoneEngine does not fail closed when configured systemd notification fails");
            Require(zoneProgram.Contains("NotifySystemd(\"STOPPING=1\\nSTATUS=ZoneEngine is stopping\")"), "ZoneEngine does not notify systemd before governed shutdown");
            Require(legacyLogOffGuard >= 0 && legacyLogOffGuard < legacyLogOffAll && legacyLogOffAll < legacyLogOffEnd, "Linux ZoneEngine can still perform legacy global Online mutation during initialization");

            int stopLogin = upgrader.IndexOf("service_stop login", StringComparison.Ordinal);
            int stopZone = upgrader.IndexOf("service_stop zone", StringComparison.Ordinal);
            int startLogin = upgrader.LastIndexOf("service_start login", StringComparison.Ordinal);
            int startZone = upgrader.LastIndexOf("service_start zone", StringComparison.Ordinal);
            int rollbackFunction = upgrader.IndexOf("rollback()", StringComparison.Ordinal);
            int rollbackStartLogin = rollbackFunction < 0
                ? -1
                : upgrader.IndexOf("rollback_operation START_LOGIN", rollbackFunction, StringComparison.Ordinal);
            int rollbackVerifiedBeforeRestart = rollbackStartLogin < 0
                ? -1
                : upgrader.LastIndexOf("[[ \"${failed}\" == false ]]", rollbackStartLogin, StringComparison.Ordinal);
            Require(stopLogin >= 0 && stopLogin < stopZone, "production stop order is not LoginEngine then ZoneEngine");
            Require(startLogin >= 0 && startLogin < startZone, "production start order is not LoginEngine then ZoneEngine");
            Require(rollbackFunction >= 0 && rollbackVerifiedBeforeRestart > rollbackFunction && rollbackStartLogin > rollbackVerifiedBeforeRestart, "general rollback can restart services before exact prior-state restoration passes");

            Require(manifest.Contains("LOGINENGINE_ARTIFACT_SHA256="), "release manifest lacks LoginEngine artifact hash");
            Require(manifest.Contains("ZONEENGINE_ARTIFACT_SHA256="), "release manifest lacks ZoneEngine artifact hash");
            Require(manifest.Contains("PLACEMENT_CORPUS_MANIFEST_SHA256="), "release manifest lacks placement corpus manifest provenance");
            Require(manifest.Contains("PLACEMENT_BUILD_MANIFEST_SHA256="), "release manifest lacks placement build manifest provenance");
            Require(manifest.Contains("PLACEMENT_RECORD_COUNT="), "release manifest lacks placement count provenance");
            Require(manifest.Contains("LOGINENGINE_UNIT_SHA256="), "release manifest lacks LoginEngine unit hash");
            Require(manifest.Contains("ZONEENGINE_UNIT_SHA256="), "release manifest lacks ZoneEngine unit hash");
            Require(manifest.Contains("repository HEAD does not match expected source SHA"), "manifest generator lost immutable SHA gate");

            Require(tests.Contains("production deployment workflow tests (56/56)"), "deployment fixture suite count changed");
            Require(tests.Contains("candidate LoginEngine configuration path diverges from the governed production path"), "deployment fixtures do not reject a divergent LoginEngine config path");
            Require(tests.Contains("AO_REBIRTH_CONFIG_PATH is missing or duplicated"), "deployment fixtures do not reject duplicate config-path assignments");
            Require(tests.Contains("outage recovery accepted an active ZoneEngine"), "deployment fixtures do not reject misuse of outage recovery");
            Require(tests.Contains("outage recovery accepted a non-stopped ZoneEngine state"), "deployment fixtures do not require an exact stopped ZoneEngine state");
            Require(tests.Contains("outage recovery accepted an occupied ZoneEngine port"), "deployment fixtures do not reject a stale ZoneEngine listener");
            Require(tests.Contains("outage recovery accepted a failed ZoneEngine port inspection"), "deployment fixtures allow a failed listener inspection to pass open");
            Require(tests.Contains("outage recovery accepted an incompatible candidate database contract"), "deployment fixtures do not fail closed on candidate/schema mismatch");
            Require(tests.Contains("outage recovery accepted a changing frozen restart count"), "deployment fixtures do not prove that outage recovery remains frozen");
            Require(tests.Contains("outage recovery left an already-deployed ZoneEngine stopped"), "deployment fixtures allow recovery to take the stopped idempotent no-op path");
            Require(tests.Contains("outage recovery accepted a ZoneEngine auto-restart"), "deployment fixtures do not require post-start restart stability");
            Require(tests.Contains("stopped recovery modifier was accepted without outage recovery mode"), "deployment fixtures do not constrain stopped-pair recovery authority");
            Require(tests.Contains("stopped recovery accepted an occupied LoginEngine port"), "deployment fixtures do not reject an occupied LoginEngine port during retry");
            Require(tests.Contains("stopped recovery accepted failed LoginEngine port inspection"), "deployment fixtures allow a failed stopped-pair listener inspection to pass open");
            Require(tests.Contains("stopped recovery accepted LoginEngine state drift"), "deployment fixtures do not freeze the stopped LoginEngine state");
            Require(tests.Contains("stopped recovery accepted LoginEngine restart drift"), "deployment fixtures do not freeze the LoginEngine restart count");
            Require(tests.Contains("stopped recovery accepted Online drift before mutation"), "deployment fixtures do not recheck Online state before stopped-pair mutation");
            Require(tests.Contains("stopped-pair recovery took an idempotent no-op path"), "deployment fixtures permit a stopped candidate to remain stopped");
            Require(tests.Contains("deployment accepted an unmanaged ZoneEngine systemd drop-in"), "deployment fixtures do not reject unmanaged effective unit overrides");
            Require(tests.Contains("stopped recovery accepted an ineffective ZoneEngine readiness unit"), "deployment fixtures do not prove effective Type=notify after installation");
            Require(tests.Contains("stopped recovery accepted concurrent daily-login drop-in drift"), "deployment fixtures do not fail closed on governed drop-in byte drift");
            Require(tests.Contains("outage recovery accepted an online character after admission closed"), "deployment fixtures do not close the zero-online race before mutation");
            Require(tests.Contains("outage recovery accepted a ZoneEngine state change before mutation"), "deployment fixtures do not preserve ZoneEngine state around the Online boundary");
            Require(tests.Contains("artifact_install"), "deployment fixture suite lacks artifact rollback failure");
            Require(tests.Contains("unit_install"), "deployment fixture suite lacks unit rollback failure");
            Require(tests.Contains("login_start"), "deployment fixture suite lacks first-service startup failure");
            Require(tests.Contains("zone_start"), "deployment fixture suite lacks second-service startup failure");
            Require(tests.Contains("listener"), "deployment fixture suite lacks listener failure");
            Require(tests.Contains("READINESS_WAIT=PASS engine=login elapsedSeconds=7"), "deployment fixture does not prove delayed LoginEngine readiness");
            Require(tests.Contains("READINESS_WAIT=PASS engine=zone elapsedSeconds=7"), "deployment fixture does not prove delayed ZoneEngine readiness");
            Require(tests.Contains("READINESS_WAIT=TIMEOUT engine=login elapsedSeconds=30"), "deployment fixture does not prove bounded readiness timeout");
            Require(tests.Contains("ZoneEngine --validate-lifecycle --shutdown-file"), "deployment fixture does not reject the listener-free ZoneEngine validation runtime");
            Require(tests.Contains("prior_login_link_target=\"releases/old-login\""), "deployment fixture suite does not preserve an exact relative LoginEngine symlink target");
            Require(tests.Contains("prior_zone_link_target=\"releases/old-zone\""), "deployment fixture suite does not preserve an exact relative ZoneEngine symlink target");
            Require(tests.Contains("official-placement-build-manifest.json\"; expect_preflight_failure"), "deployment fixture does not reject a missing placement build manifest");
            Require(tests.Contains("official-placement-summary.json\"; expect_preflight_failure"), "deployment fixture does not reject changed placement data");
            Require(tests.Contains("placements/pf_630.json\"; expect_preflight_failure"), "deployment fixture does not reject a missing placement shard");
            Require(tests.Contains("placements/pf_1.json\"; expect_preflight_failure"), "deployment fixture does not reject changed placement shard content");

            Require(acceptance.Contains("publish-loginengine.sh"), "Linux exact-SHA acceptance does not publish LoginEngine");
            Require(acceptance.Contains("publish-zoneengine.sh"), "Linux exact-SHA acceptance does not publish ZoneEngine");
            Require(acceptance.Contains("test-upgrade-active-services.sh"), "Linux exact-SHA acceptance does not run deployment tests");
            Require(acceptance.Contains("test-artifact-provenance.sh"), "Linux exact-SHA acceptance does not run non-production provenance tests");
            Require(acceptance.Contains("create-release-manifest.sh"), "Linux exact-SHA acceptance does not generate the release manifest");
            Require(acceptance.Contains("--expected-placement-manifest-sha"), "Linux exact-SHA acceptance does not require the Windows placement manifest digest");
            Require(acceptance.Contains("PLACEMENT_VALIDATION=PASS"), "Linux exact-SHA acceptance does not record placement validation");
            Require(placementProvenance.Contains("official placement shard count is"), "shared placement provenance gate does not require all shards");
            Require(placementProvenance.Contains("PLACEMENT_CORPUS_SUMMARY_SHA256"), "shared placement provenance gate does not verify the summary digest");
            Require(zoneArtifactGate.Contains("placement_provenance_load"), "non-production artifact provenance validation omits placement evidence");
            Require(zoneArtifactGate.Contains("EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256"), "non-production artifact provenance validation omits accepted Windows parity");
            Require(zoneArtifactTests.Contains("ZoneEngine placement artifact provenance tests (8/8)"), "non-production provenance fixture suite count changed");
            Require(zoneArtifactTests.Contains("PLACEMENT_CORPUS_INDEX_SHA256"), "non-production provenance fixtures omit corpus digest validation");
            Console.WriteLine("PASS: governed transactional production deployment contract");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) { throw new InvalidOperationException(message); }
        }

        private static int CountOccurrences(string source, string value)
        {
            return source.Split(new[] { value }, StringSplitOptions.None).Length - 1;
        }
    }
}
