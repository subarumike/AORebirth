using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal static class SubwayVisibilityDiagnosticSelection
{
	private const string ManifestRelativePath = "docs\\generated\\subway_pf127_visibility_diagnostic_manifest.csv";

	private const string ActiveConfigurationRelativePath = ".local\\subway-visibility\\active-session.cfg";

	private static readonly object Sync = new object();

	private static readonly object PopulationLedgerSync = new object();

	private static readonly Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> RuntimeEntries = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();

	private static readonly HashSet<string> PopulationEventKeys = new HashSet<string>();

	private static bool loaded;

	private static SubwayVisibilityDiagnosticConfiguration configuration = SubwayVisibilityDiagnosticConfiguration.Disabled;

	private static Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> manifestBySource = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();

	internal static SubwayVisibilityDiagnosticConfiguration Configuration
	{
		get
		{
			EnsureLoaded();
			return configuration;
		}
	}

	internal static bool ShouldIncludeQuarantined(int sourceInstance)
	{
		SubwayVisibilityDiagnosticConfiguration subwayVisibilityDiagnosticConfiguration = Configuration;
		bool flag = subwayVisibilityDiagnosticConfiguration.Enabled && subwayVisibilityDiagnosticConfiguration.SelectedSourceInstances.Contains(sourceInstance);
		if (flag)
		{
			RecordPopulationEventOnce(subwayVisibilityDiagnosticConfiguration, sourceInstance, "ELIGIBLE", null, "selected quarantine row enabled");
		}
		return flag;
	}

	internal static void RegisterRuntimeIdentity(int runtimeInstance, int sourceInstance)
	{
		EnsureLoaded();
		if (manifestBySource.TryGetValue(sourceInstance, out var value))
		{
			lock (Sync)
			{
				RuntimeEntries[runtimeInstance] = value;
			}
			RecordPopulationEventOnce(Configuration, sourceInstance, "MATERIALIZED", runtimeInstance, "runtime identity registered");
		}
	}

	internal static void RecordPopulationFailure(int sourceInstance, string detail)
	{
		SubwayVisibilityDiagnosticConfiguration subwayVisibilityDiagnosticConfiguration = Configuration;
		if (subwayVisibilityDiagnosticConfiguration.Enabled && subwayVisibilityDiagnosticConfiguration.SelectedSourceInstances.Contains(sourceInstance))
		{
			RecordPopulationEventOnce(subwayVisibilityDiagnosticConfiguration, sourceInstance, "FAILED", null, detail);
		}
	}

	internal static void RemoveRuntimeIdentity(int runtimeInstance)
	{
		lock (Sync)
		{
			RuntimeEntries.Remove(runtimeInstance);
		}
	}

	internal static bool TryGetRuntimeEntry(int runtimeInstance, out SubwayVisibilityDiagnosticManifestEntry entry)
	{
		lock (Sync)
		{
			return RuntimeEntries.TryGetValue(runtimeInstance, out entry);
		}
	}

	internal static SubwayVisibilityDiagnosticManifestEntry[] ManifestEntries()
	{
		EnsureLoaded();
		return manifestBySource.Values.OrderBy((SubwayVisibilityDiagnosticManifestEntry value) => value.Ordinal).ToArray();
	}

	private static void EnsureLoaded()
	{
		if (loaded)
		{
			return;
		}
		lock (Sync)
		{
			if (loaded)
			{
				return;
			}
			try
			{
				string text = FindRepositoryRoot();
				if (string.IsNullOrEmpty(text))
				{
					loaded = true;
					return;
				}
				manifestBySource = LoadManifest(Path.Combine(text, "docs\\generated\\subway_pf127_visibility_diagnostic_manifest.csv"));
				configuration = LoadConfiguration(text, Path.Combine(text, ".local\\subway-visibility\\active-session.cfg"), manifestBySource);
			}
			catch (Exception ex)
			{
				configuration = SubwayVisibilityDiagnosticConfiguration.Disabled;
				manifestBySource = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();
				LogUtil.Debug((DebugInfoDetail)512, "PF127 visibility diagnostics disabled: " + ex.Message);
			}
			finally
			{
				loaded = true;
			}
		}
	}

	private static string FindRepositoryRoot()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		if (string.IsNullOrEmpty(baseDirectory))
		{
			return string.Empty;
		}
		for (DirectoryInfo directoryInfo = new DirectoryInfo(baseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string path = Path.Combine(directoryInfo.FullName, "docs\\generated\\subway_pf127_visibility_diagnostic_manifest.csv");
			if (File.Exists(path))
			{
				return directoryInfo.FullName;
			}
		}
		return string.Empty;
	}

	private static Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> LoadManifest(string path)
	{
		Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> dictionary = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();
		string[] array = File.ReadAllLines(path);
		for (int i = 1; i < array.Length; i++)
		{
			if (!string.IsNullOrWhiteSpace(array[i]))
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length != 9)
				{
					throw new InvalidDataException("Invalid PF127 diagnostic manifest row " + (i + 1));
				}
				SubwayVisibilityDiagnosticManifestEntry subwayVisibilityDiagnosticManifestEntry = new SubwayVisibilityDiagnosticManifestEntry
				{
					Ordinal = int.Parse(array2[0], CultureInfo.InvariantCulture),
					SourceInstance = int.Parse(array2[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
					Name = array2[2],
					Family = array2[3],
					Classification = array2[4],
					X = float.Parse(array2[5], CultureInfo.InvariantCulture),
					Y = float.Parse(array2[6], CultureInfo.InvariantCulture),
					Z = float.Parse(array2[7], CultureInfo.InvariantCulture),
					SourceCapture = array2[8]
				};
				if (subwayVisibilityDiagnosticManifestEntry.Ordinal != dictionary.Count + 1 || dictionary.ContainsKey(subwayVisibilityDiagnosticManifestEntry.SourceInstance))
				{
					throw new InvalidDataException("PF127 diagnostic manifest ordering or identity uniqueness failed");
				}
				dictionary.Add(subwayVisibilityDiagnosticManifestEntry.SourceInstance, subwayVisibilityDiagnosticManifestEntry);
			}
		}
		int num = dictionary.Values.Count((SubwayVisibilityDiagnosticManifestEntry value) => value.Classification == "SUPPORTED_FAMILY_RESTORE");
		int num2 = dictionary.Values.Count((SubwayVisibilityDiagnosticManifestEntry value) => value.Classification == "ORDINARY_ENEMY_REGENERATE");
		if (dictionary.Count != 38 || num != 29 || num2 != 9)
		{
			throw new InvalidDataException("PF127 diagnostic manifest must contain 38 rows split 29/9");
		}
		return dictionary;
	}

	private static SubwayVisibilityDiagnosticConfiguration LoadConfiguration(string repositoryRoot, string path, IDictionary<int, SubwayVisibilityDiagnosticManifestEntry> manifest)
	{
		if (!File.Exists(path))
		{
			return SubwayVisibilityDiagnosticConfiguration.Disabled;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = File.ReadAllLines(path);
		foreach (string text in array)
		{
			int num = text.IndexOf('=');
			if (num > 0)
			{
				dictionary[text.Substring(0, num).Trim()] = text.Substring(num + 1).Trim();
			}
		}
		if (!dictionary.TryGetValue("enabled", out var value) || value != "1" || !dictionary.TryGetValue("session_id", out var value2) || !IsSafeSessionId(value2) || !dictionary.TryGetValue("slice", out var value3) || !IsKnownSlice(value3) || !dictionary.TryGetValue("artifact_directory", out var value4) || !dictionary.TryGetValue("expected_quarantined_row_count", out var value5) || !dictionary.TryGetValue("selected_source_instances", out var value6))
		{
			throw new InvalidDataException("PF127 active diagnostic configuration is incomplete or invalid");
		}
		string value7 = Path.GetFullPath(Path.Combine(repositoryRoot, ".local\\subway-visibility")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string text2 = Path.GetFullPath(value4).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!text2.StartsWith(value7, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("PF127 diagnostic artifact directory is outside the ignored session root");
		}
		int num2 = int.Parse(value5, CultureInfo.InvariantCulture);
		HashSet<int> hashSet = new HashSet<int>();
		if (!string.IsNullOrWhiteSpace(value6))
		{
			string[] array2 = value6.Split(',');
			foreach (string text3 in array2)
			{
				int num3 = int.Parse(text3.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
				if (!manifest.ContainsKey(num3))
				{
					throw new InvalidDataException("PF127 diagnostic configuration contains an unknown identity");
				}
				hashSet.Add(num3);
			}
		}
		if (num2 != hashSet.Count || num2 < 0 || num2 > 38)
		{
			throw new InvalidDataException("PF127 diagnostic selected count does not match configuration");
		}
		if (value3 == "ALL_38" && hashSet.Count != 38)
		{
			throw new InvalidDataException("ALL_38 requires all 38 explicit manifest identities");
		}
		return new SubwayVisibilityDiagnosticConfiguration(enabled: true, value2, value3, text2.TrimEnd(Path.DirectorySeparatorChar), num2, hashSet);
	}

	private static bool IsSafeSessionId(string value)
	{
		if (string.IsNullOrEmpty(value) || value.Length > 64)
		{
			return false;
		}
		foreach (char c in value)
		{
			if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsKnownSlice(string value)
	{
		int result;
		switch (value)
		{
		default:
			result = ((value == "FAMILY") ? 1 : 0);
			break;
		case "NONE":
		case "ALL_38":
		case "SUPPORTED_29":
		case "ORDINARY_9":
		case "FIRST_N":
		case "ORDINAL_RANGE":
		case "IDENTITY_LIST":
			result = 1;
			break;
		}
		return (byte)result != 0;
	}

	private static void RecordPopulationEventOnce(SubwayVisibilityDiagnosticConfiguration current, int sourceInstance, string phase, int? runtimeInstance, string detail)
	{
		if (current == null || !current.Enabled || !current.SelectedSourceInstances.Contains(sourceInstance) || !manifestBySource.TryGetValue(sourceInstance, out var value))
		{
			return;
		}
		string item = phase + ":" + sourceInstance.ToString("X8", CultureInfo.InvariantCulture);
		lock (PopulationLedgerSync)
		{
			if (PopulationEventKeys.Contains(item))
			{
				return;
			}
			try
			{
				Directory.CreateDirectory(current.ArtifactDirectory);
				string path = Path.Combine(current.ArtifactDirectory, "population-activation-ledger.csv");
				bool flag = !File.Exists(path);
				using (StreamWriter streamWriter = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
				{
					if (flag)
					{
						streamWriter.WriteLine("TimestampUtc,SessionId,Slice,ProcessId,Phase,SourceInstanceHex,RuntimeInstance,ManifestOrdinal,Name,Family,Detail");
					}
					streamWriter.WriteLine(string.Join(",", Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)), Csv(current.SessionId), Csv(current.Slice), Csv(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)), Csv(phase), Csv(sourceInstance.ToString("X8", CultureInfo.InvariantCulture)), Csv(runtimeInstance.HasValue ? runtimeInstance.Value.ToString(CultureInfo.InvariantCulture) : string.Empty), Csv(value.Ordinal.ToString(CultureInfo.InvariantCulture)), Csv(value.Name), Csv(value.Family), Csv(detail)));
				}
				PopulationEventKeys.Add(item);
			}
			catch (Exception ex)
			{
				LogUtil.Debug((DebugInfoDetail)512, "PF127 population activation diagnostic write failed: " + ex.Message);
			}
		}
	}

	private static string Csv(string value)
	{
		string text = value ?? string.Empty;
		return "\"" + text.Replace("\"", "\"\"") + "\"";
	}
}
