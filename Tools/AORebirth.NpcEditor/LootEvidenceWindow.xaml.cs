using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AORebirth.NpcEditor;

public partial class LootEvidenceWindow : Window
{
    private const int PageSize = 100;
    private const int MaxImportBytes = 256 * 1024;
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(50);
    private readonly EditorApi api;
    private readonly bool canSave;
    private readonly NpcIdentity? selectedNpc;
    private bool busy;
    private EvidencePage? evidence;
    private CoveragePage? coverage;
    private int evidenceOffset;
    private int coverageOffset;
    private string evidenceFilter = "";
    private string coverageFilter = "";

    internal bool Unauthorized { get; private set; }

    internal LootEvidenceWindow(EditorApi api, bool canSave, NpcIdentity? selectedNpc)
    {
        InitializeComponent();
        this.api = api;
        this.canSave = canSave;
        this.selectedNpc = selectedNpc;
        ComparisonTab.IsEnabled = selectedNpc != null;
        LoadOptions(EvidenceClass, "All classifications", "OBSERVED_AVAILABLE_LOOT", "COMMUNITY_DOCUMENTED", "INFERRED", "UNRESOLVED");
        LoadOptions(EvidenceDecision, "All reviews", "pending", "approved", "rejected");
        LoadOptions(CoverageBoss, "All NPC types", "Named boss", "Ordinary");
        LoadOptions(CoverageCategory, "All categories", "ordinaryNpc", "family", "namedBoss", "dynaBoss", "dungeon", "mission",
            "raid", "quest", "event", "global");
        LoadOptions(CoverageStatus, "All coverage statuses", "CONFIGURED", "UNCONFIGURED", "INTENTIONALLY_EMPTY",
            "UNRESOLVED", "EVIDENCE_INCOMPLETE", "OBSERVATION_NOT_CONFIGURED", "OBSERVATION_REPRESENTED",
            "UNUSED_TABLE", "DUPLICATE_DEFINITION");
        Loaded += async (_, _) => await RunAsync(() => ReadEvidenceAsync(0, ""));
        Closing += (_, args) => args.Cancel = busy;
        RefreshActions();
    }

    private static void LoadOptions(ComboBox control, string first, params string[] values)
    {
        control.ItemsSource = new[] { new FilterOption(first, null) }.Concat(values.Select(value => new FilterOption(value, value)));
        control.SelectedIndex = 0;
    }

    private static string? Value(ComboBox control) => (control.SelectedItem as FilterOption)?.Value;

    private async void FindEvidence_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadEvidenceAsync(0, Filter(("q", EvidenceQuery.Text), ("classification", Value(EvidenceClass)),
            ("review", Value(EvidenceDecision)))));

    private async void EvidencePrevious_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadEvidenceAsync(Math.Max(0, evidenceOffset - PageSize), evidenceFilter));

    private async void EvidenceNext_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadEvidenceAsync(evidenceOffset + PageSize, evidenceFilter));

    private async Task ReadEvidenceAsync(int offset, string filter)
    {
        evidence = await api.GetAsync<EvidencePage>($"loot-evidence?offset={offset}&limit={PageSize}{filter}");
        evidenceOffset = offset;
        evidenceFilter = filter;
        EvidenceGrid.ItemsSource = evidence.Rows;
        EvidenceCount.Text = PageLabel(offset, evidence.Rows.Length, evidence.Total);
        Status.Text = evidence.UnavailableReason ?? "Evidence review is separate from the active loot registry.";
    }

    private async void ImportEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (evidence is not { UnavailableReason: null } current || !canSave)
            return;
        string capture = CaptureSource.Text.Trim();
        if (capture.Length == 0)
        {
            Status.Text = "Enter the capture source label before importing.";
            return;
        }
        var picker = new OpenFileDialog
        {
            Title = "Open a decoded loot observation file",
            Filter = "Loot observation files (*.csv;*.json)|*.csv;*.json",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true)
            return;
        await RunAsync(async () =>
        {
            await using var file = new FileStream(picker.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            if (file.Length is < 1 or > MaxImportBytes)
                throw new InvalidDataException("Choose a nonempty observation file of at most 256 KiB.");
            byte[] bytes = new byte[file.Length];
            await file.ReadExactlyAsync(bytes);
            string content = new UTF8Encoding(false, true).GetString(bytes);
            string header = content.Split('\n', 2)[0];
            string format = Path.GetExtension(picker.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json"
                : header.Contains("InitialSnapshot", StringComparison.Ordinal) ? "aosharp"
                : header.Contains("loot_kind", StringComparison.Ordinal) ? "decoded"
                : throw new InvalidDataException("Choose an AOSharp corpse observation CSV, decoded drop CSV, or observation JSON.");
            var result = await api.PostAsync<EvidenceImportResult>("loot-evidence/import",
                new { expectedRevision = current.Revision, captureSource = capture, fileName = Path.GetFileName(picker.FileName), format, content });
            EvidenceQuery.Clear();
            EvidenceClass.SelectedIndex = EvidenceDecision.SelectedIndex = 0;
            await ReadEvidenceAsync(0, "");
            Status.Text = $"Staged {result.Imported} observations. {result.Duplicates} duplicates and {result.Skipped} non-item records skipped.";
        });
    }

    private void EvidenceSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var row = EvidenceGrid.SelectedItem as EvidenceObservation;
        EvidenceDetail.Text = row == null ? "" : string.Join(Environment.NewLine,
            $"{row.CaptureSource} / {row.FileName} / {row.SourceRow}",
            $"NPC {row.EnemyIdentity ?? "unlinked"}, template {row.TemplateHash ?? "unlinked"}, MonsterData {row.MonsterData}, level {row.Level}",
            $"Captured {row.CapturedUtc:u} · patch {row.Patch ?? "unattributed"} · observations {row.ObservationCount}",
            $"{row.Confidence} · reported classification {row.ClaimedClassification ?? "unavailable"}",
            $"Imported by {row.ImportedBy} at {row.ImportedUtc:u} · reviewed by {row.ReviewedBy ?? "pending"} at {row.ReviewedUtc:u}",
            $"Source fingerprint {row.FileFingerprint}");
        RefreshActions();
    }

    private async void ApproveEvidence_Click(object sender, RoutedEventArgs e) => await ReviewAsync("approved");
    private async void RejectEvidence_Click(object sender, RoutedEventArgs e) => await ReviewAsync("rejected");

    private async Task ReviewAsync(string decision)
    {
        if (!canSave || evidence is not { UnavailableReason: null } current || EvidenceGrid.SelectedItem is not EvidenceObservation row)
            return;
        await RunAsync(async () =>
        {
            await api.PostAsync<EvidenceObservation>("loot-evidence/review", new { expectedRevision = current.Revision, row.Id, decision });
            await ReadEvidenceAsync(evidenceOffset, evidenceFilter);
            Status.Text = $"Observation marked {decision}.";
        });
    }

    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (selectedNpc == null)
            return;
        await RunAsync(async () =>
        {
            var result = await api.GetAsync<ComparisonPage>(selectedNpc.Path + "/loot/comparison");
            ComparisonGrid.ItemsSource = result.Rows;
            ComparisonNote.Text = $"{result.TemplateHash} · showing {result.Rows.Length} of {result.Total}. {result.Note}";
            Status.Text = "Comparison loaded.";
        });
    }

    private async void FindCoverage_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadCoverageAsync(0, ReadCoverageFilter()));

    private async void CoveragePrevious_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadCoverageAsync(Math.Max(0, coverageOffset - PageSize), coverageFilter));

    private async void CoverageNext_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ReadCoverageAsync(coverageOffset + PageSize, coverageFilter));

    private string ReadCoverageFilter()
    {
        string? playfield = Positive(CoveragePlayfield.Text, "Resource playfield ID");
        string? min = Positive(CoverageMinLevel.Text, "Minimum level");
        string? max = Positive(CoverageMaxLevel.Text, "Maximum level");
        return Filter(("q", CoverageQuery.Text), ("playfieldId", playfield), ("family", CoverageFamily.Text),
            ("minLevel", min), ("maxLevel", max), ("boss", Value(CoverageBoss)), ("category", Value(CoverageCategory)),
            ("status", Value(CoverageStatus)));
    }

    private async Task ReadCoverageAsync(int offset, string filter)
    {
        Status.Text = "Reading loot coverage…";
        coverage = await api.GetAsync<CoveragePage>($"loot-coverage?offset={offset}&limit={PageSize}{filter}", timeout: ScanTimeout);
        coverageOffset = offset;
        coverageFilter = filter;
        CoverageGrid.ItemsSource = coverage.Rows;
        CoverageCount.Text = PageLabel(offset, coverage.Rows.Length, coverage.Total);
        CoverageNote.Text = coverage.Note;
        Status.Text = "Coverage loaded.";
    }

    private async void ExportCoverage_Click(object sender, RoutedEventArgs e)
    {
        if (coverage == null)
            return;
        var picker = new SaveFileDialog
        {
            Title = "Export filtered loot coverage",
            Filter = "CSV file (*.csv)|*.csv|JSON file (*.json)|*.json",
            FileName = "loot-coverage",
            AddExtension = true
        };
        if (picker.ShowDialog(this) != true)
            return;
        await RunAsync(async () =>
        {
            CoveragePage result = await api.GetAsync<CoveragePage>("loot-coverage?export=true" + coverageFilter, timeout: ScanTimeout);
            string pending = Path.Combine(Path.GetDirectoryName(picker.FileName)!, Path.GetRandomFileName());
            try
            {
                if (Path.GetExtension(picker.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    await using var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    await JsonSerializer.SerializeAsync(file, result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
                }
                else
                {
                    await using var file = new StreamWriter(pending, append: false, Encoding.UTF8);
                    await file.WriteLineAsync("Kind,Key,Name,PlayfieldId,Level,FamilyHashes,Boss,Category,Status,Detail");
                    foreach (CoverageRow row in result.Rows)
                        await file.WriteLineAsync(string.Join(",", new[] { row.Kind, row.Key, row.Name,
                            row.PlayfieldId?.ToString(CultureInfo.InvariantCulture), row.Level?.ToString(CultureInfo.InvariantCulture),
                            string.Join(" ", row.FamilyHashes), row.Boss, row.Category, row.Status, row.Detail }.Select(Csv)));
                }
                File.Move(pending, picker.FileName, overwrite: true);
            }
            finally
            {
                File.Delete(pending);
            }
            Status.Text = $"Exported {result.Rows.Length} coverage rows.";
        });
    }

    private static string Csv(string? field)
    {
        string value = field ?? "";
        if (value.TrimStart() is { Length: > 0 } trimmed && trimmed[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string? Positive(string text, string label) => string.IsNullOrWhiteSpace(text) ? null
        : int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException(label + " must be a positive whole number.");

    private static string Filter(params (string Key, string? Value)[] fields) =>
        string.Concat(fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => "&" + field.Key + "=" + Uri.EscapeDataString(field.Value!.Trim())));

    private static string PageLabel(int offset, int count, int total) => count == 0 ? $"{total} matches"
        : $"{offset + 1}–{offset + count} of {total}";

    private async Task RunAsync(Func<Task> work)
    {
        if (busy)
            return;
        busy = true;
        RefreshActions();
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Unauthorized = ex is EditorApiException { Status: HttpStatusCode.Unauthorized };
            Status.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshActions();
        }
    }

    private void RefreshActions()
    {
        Views.IsEnabled = !busy && !Unauthorized;
        bool edits = !busy && canSave && evidence is { UnavailableReason: null };
        ImportEvidence.IsEnabled = edits;
        ApproveEvidence.IsEnabled = RejectEvidence.IsEnabled = edits && EvidenceGrid.SelectedItem is EvidenceObservation;
        EvidencePrevious.IsEnabled = !busy && evidenceOffset > 0;
        EvidenceNext.IsEnabled = !busy && evidence != null && evidenceOffset + PageSize < evidence.Total;
        CoveragePrevious.IsEnabled = !busy && coverageOffset > 0;
        CoverageNext.IsEnabled = !busy && coverage != null && coverageOffset + PageSize < coverage.Total;
        ExportCoverage.IsEnabled = !busy && coverage?.Total > 0;
    }

    private sealed record FilterOption(string Label, string? Value);
    private sealed record EvidenceImportResult(int Revision, int Imported, int Duplicates, int Skipped);
    private sealed record EvidencePage(int Revision, int Total, EvidenceObservation[] Rows, string? UnavailableReason);
    private sealed record EvidenceObservation(string Id, string CaptureSource, string FileName, string FileFingerprint, string SourceRow,
        string? EnemyName, string? EnemyIdentity, int? PlayfieldId, int? MonsterData, string? TemplateHash, int? Level, int LowId,
        int HighId, int Quality, int ObservationCount, DateTimeOffset? CapturedUtc, string? Patch, string Classification,
        string Confidence, string? ClaimedClassification, string ImportedBy, DateTimeOffset ImportedUtc,
        string Review, string? ReviewedBy, DateTimeOffset? ReviewedUtc);
    private sealed record CoveragePage(int Total, CoverageRow[] Rows, string Note);
    private sealed record CoverageRow(string Kind, string Key, string Name, int? PlayfieldId, int? Level, string[] FamilyHashes,
        string Boss, string? Category, string Status, string Detail);
    private sealed record ComparisonPage(string TemplateHash, int Total, ComparisonRow[] Rows, string Note);
    private sealed record ComparisonRow(string Status, string Item, int LowId, int? HighId, int? Quality, string? CaptureSource,
        string? Classification, string Detail);
}
