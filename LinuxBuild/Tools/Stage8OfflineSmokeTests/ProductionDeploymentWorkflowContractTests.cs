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

            int stopLogin = upgrader.IndexOf("service_stop login", StringComparison.Ordinal);
            int stopZone = upgrader.IndexOf("service_stop zone", StringComparison.Ordinal);
            int startLogin = upgrader.LastIndexOf("service_start login", StringComparison.Ordinal);
            int startZone = upgrader.LastIndexOf("service_start zone", StringComparison.Ordinal);
            Require(stopLogin >= 0 && stopLogin < stopZone, "production stop order is not LoginEngine then ZoneEngine");
            Require(startLogin >= 0 && startLogin < startZone, "production start order is not LoginEngine then ZoneEngine");

            Require(manifest.Contains("LOGINENGINE_ARTIFACT_SHA256="), "release manifest lacks LoginEngine artifact hash");
            Require(manifest.Contains("ZONEENGINE_ARTIFACT_SHA256="), "release manifest lacks ZoneEngine artifact hash");
            Require(manifest.Contains("LOGINENGINE_UNIT_SHA256="), "release manifest lacks LoginEngine unit hash");
            Require(manifest.Contains("ZONEENGINE_UNIT_SHA256="), "release manifest lacks ZoneEngine unit hash");
            Require(manifest.Contains("repository HEAD does not match expected source SHA"), "manifest generator lost immutable SHA gate");

            Require(tests.Contains("production deployment workflow tests (17/17)"), "deployment fixture suite count changed");
            Require(tests.Contains("artifact_install"), "deployment fixture suite lacks artifact rollback failure");
            Require(tests.Contains("unit_install"), "deployment fixture suite lacks unit rollback failure");
            Require(tests.Contains("login_start"), "deployment fixture suite lacks first-service startup failure");
            Require(tests.Contains("zone_start"), "deployment fixture suite lacks second-service startup failure");
            Require(tests.Contains("listener"), "deployment fixture suite lacks listener failure");
            Require(tests.Contains("READINESS_WAIT=PASS engine=login elapsedSeconds=7"), "deployment fixture does not prove delayed LoginEngine readiness");
            Require(tests.Contains("READINESS_WAIT=PASS engine=zone elapsedSeconds=7"), "deployment fixture does not prove delayed ZoneEngine readiness");
            Require(tests.Contains("READINESS_WAIT=TIMEOUT engine=login elapsedSeconds=30"), "deployment fixture does not prove bounded readiness timeout");
            Require(tests.Contains("prior_login_link_target=\"releases/old-login\""), "deployment fixture suite does not preserve an exact relative LoginEngine symlink target");
            Require(tests.Contains("prior_zone_link_target=\"releases/old-zone\""), "deployment fixture suite does not preserve an exact relative ZoneEngine symlink target");

            Require(acceptance.Contains("publish-loginengine.sh"), "Linux exact-SHA acceptance does not publish LoginEngine");
            Require(acceptance.Contains("publish-zoneengine.sh"), "Linux exact-SHA acceptance does not publish ZoneEngine");
            Require(acceptance.Contains("test-upgrade-active-services.sh"), "Linux exact-SHA acceptance does not run deployment tests");
            Require(acceptance.Contains("create-release-manifest.sh"), "Linux exact-SHA acceptance does not generate the release manifest");
            Console.WriteLine("PASS: governed transactional production deployment contract");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) { throw new InvalidOperationException(message); }
        }
    }
}
