using System.Collections.Generic;

namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilityDiagnosticConfiguration
{
	internal static readonly SubwayVisibilityDiagnosticConfiguration Disabled = new SubwayVisibilityDiagnosticConfiguration(enabled: false, string.Empty, "NONE", string.Empty, 0, new int[0]);

	internal bool Enabled { get; private set; }

	internal string SessionId { get; private set; }

	internal string Slice { get; private set; }

	internal string ArtifactDirectory { get; private set; }

	internal int ExpectedQuarantinedRowCount { get; private set; }

	internal HashSet<int> SelectedSourceInstances { get; private set; }

	internal SubwayVisibilityDiagnosticConfiguration(bool enabled, string sessionId, string slice, string artifactDirectory, int expectedQuarantinedRowCount, IEnumerable<int> selectedSourceInstances)
	{
		Enabled = enabled;
		SessionId = sessionId ?? string.Empty;
		Slice = slice ?? "NONE";
		ArtifactDirectory = artifactDirectory ?? string.Empty;
		ExpectedQuarantinedRowCount = expectedQuarantinedRowCount;
		SelectedSourceInstances = new HashSet<int>(selectedSourceInstances ?? new int[0]);
	}
}
