namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class SubwayVisibilitySpatialInterestMetricsTests
    {
        [TestMethod]
        public void InitialSpatialInterestMetricsDeriveBoundedVisibilityCounts()
        {
            SubwayVisibilitySpatialInterestMetrics metrics =
                SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    222,
                    221,
                    221,
                    73,
                    35);

            Assert.AreEqual(222, metrics.TotalPlayfieldCharacters);
            Assert.AreEqual(221, metrics.TotalPlayfieldNpcs);
            Assert.AreEqual(73, metrics.SpatialQueryInspectedCandidates);
            Assert.AreEqual(35, metrics.WithinEnterRadiusCount);
            Assert.AreEqual(0, metrics.AlreadyVisibleCount);
            Assert.AreEqual(35, metrics.NewlyVisibleCount);
            Assert.AreEqual(0, metrics.LeavingVisibleCount);
            Assert.AreEqual(186, metrics.FilteredOutCount);
        }

        [TestMethod]
        public void InitialSpatialInterestMetricsRejectInconsistentCounts()
        {
            ExpectException<ArgumentOutOfRangeException>(
                () => SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    -1,
                    0,
                    0,
                    0,
                    0));
            ExpectException<ArgumentOutOfRangeException>(
                () => SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    10,
                    11,
                    9,
                    5,
                    5));
            ExpectException<ArgumentOutOfRangeException>(
                () => SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    10,
                    9,
                    11,
                    5,
                    5));
            ExpectException<ArgumentOutOfRangeException>(
                () => SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    10,
                    9,
                    9,
                    4,
                    5));
            ExpectException<ArgumentOutOfRangeException>(
                () => SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    10,
                    9,
                    9,
                    3,
                    4));
        }

        [TestMethod]
        public void SpatialInterestMetricsEmitExactSnapshotJsonFieldsAndValues()
        {
            SubwayVisibilitySpatialInterestMetrics metrics =
                SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                    222,
                    221,
                    221,
                    73,
                    35);
            var builder = new StringBuilder("{");
            metrics.AppendJsonFields(builder, true);
            builder.Append('}');

            Assert.AreEqual(
                "{\"total_playfield_characters\":222,\"total_playfield_npcs\":221,"
                + "\"spatial_query_inspected_candidates\":73,\"within_enter_radius_count\":35,"
                + "\"already_visible_count\":0,\"newly_visible_count\":35,"
                + "\"leaving_visible_count\":0,\"filtered_out_count\":186}",
                builder.ToString());
        }

        [TestMethod]
        public void SpatialInterestMetricRecordingRemainsPf127OptIn()
        {
            string root = FindRepositoryRoot();
            string snapshotText = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\SubwayVisibilitySnapshotDiagnostics.cs"));
            string packetText = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityPackets.cs"));

            Assert.IsTrue(
                snapshotText.Contains("if (!configuration.Enabled")
                && snapshotText.Contains(
                    "recipient.Playfield.Identity.Instance != CapturedSubwayContentProvider.SubwayPlayfieldInstance")
                && snapshotText.Contains("return null;"),
                "Spatial snapshot diagnostics must remain disabled unless an explicit PF127 session is active.");
            Assert.IsTrue(
                packetText.Contains("if (diagnosticSnapshot != null)")
                && packetText.Contains("diagnosticSnapshot.RecordSpatialInterestSelection("),
                "Spatial metrics must only record through an active opt-in diagnostic snapshot.");
        }

        private static void ExpectException<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            Assert.Fail("Expected exception of type " + typeof(TException).Name + ".");
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
