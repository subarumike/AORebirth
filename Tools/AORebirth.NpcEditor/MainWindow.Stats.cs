using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private static readonly ConfiguredStatScope TemplateStatScope = new("Exact template level band", "templateBand", "");
    private static readonly ConfiguredStatScope PlayfieldStatScope = new("Resource playfield · exact template level band",
        "playfield", "?scope=playfield");
    private SavedStatsState? savedStats;
    private ConfiguredStatScope? loadedStatScope;
    private int? configuredStatDraftRevision;
    private bool updatingConfiguredStats;
    private bool settingStatScopes;

    private ConfiguredStatScope SelectedStatScope =>
        ConfiguredStatScopePicker.SelectedItem as ConfiguredStatScope ?? TemplateStatScope;

    private bool CanSaveStats => CanWrite && session?.CanSave == true && loadedStatScope == SelectedStatScope
        && savedStats is { UnavailableReason: null };

    private async Task LoadConfiguredStatsAsync(NpcIdentity identity)
    {
        ConfiguredStatScope scope = SelectedStatScope;
        try
        {
            SavedStatsState next = await Api.GetAsync<SavedStatsState>(ConfiguredStatPath(identity, "saved", scope));
            if (detail?.Npc.Identity != identity || SelectedStatScope != scope)
                return;
            EnsureStatScope(next, scope);

            bool preserveDraft = loadedStatScope == scope;
            loadedStatScope = scope;
            RenderConfiguredStats(next, preserveDraft);
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            loadedStatScope = null;
            RenderConfiguredStats(null);
            ConfiguredStatsStatus.Text = DescribeError(ex);
            StatHistoryScope.Text = DescribeError(ex);
        }
    }

    private void ClearConfiguredStats()
    {
        SetConfiguredStatScopes(null);
        loadedStatScope = null;
        ConfiguredStatQuery.Text = "";
        RenderConfiguredStats(null);
    }

    private void SetConfiguredStatScopes(NpcSummary? npc)
    {
        settingStatScopes = true;
        try
        {
            var scopes = new List<ConfiguredStatScope>();
            if (npc != null)
            {
                scopes.Add(TemplateStatScope);
                scopes.Add(PlayfieldStatScope);
                foreach (string family in (npc.FamilyHashes ?? []).Where(hash => !string.IsNullOrWhiteSpace(hash))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
                {
                    scopes.Add(new ConfiguredStatScope($"NPC family · {family}", "family",
                        "?scope=family&familyHash=" + Uri.EscapeDataString(family)));
                }
            }
            ConfiguredStatScopePicker.ItemsSource = scopes;
            ConfiguredStatScopePicker.SelectedItem = npc == null ? null : TemplateStatScope;
        }
        finally
        {
            settingStatScopes = false;
        }
    }

    private async void ConfiguredStatScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || settingStatScopes || detail == null)
            return;

        loadedStatScope = null;
        RenderConfiguredStats(null);
        ConfiguredStatsStatus.Text = $"Loading {SelectedStatScope.Label}…";
        await RunAsync(() => LoadConfiguredStatsAsync(detail.Npc.Identity));
    }

    private static string ConfiguredStatPath(NpcIdentity identity, string verb, ConfiguredStatScope scope) =>
        $"{identity.Path}/stats/{verb}{scope.Query}";

    private static void EnsureStatScope(SavedStatsState state, ConfiguredStatScope scope)
    {
        if (state.Scope != scope.Code)
            throw new InvalidOperationException($"ZoneEngine returned a different stat scope than {scope.Label}.");
    }

    private void RenderConfiguredStats(SavedStatsState? next, bool preserveDraft = false)
    {
        savedStats = next;
        StatHistory.ItemsSource = next?.History;
        ConfiguredStatsStatus.Text = next switch
        {
            null => "",
            { UnavailableReason: string reason } => reason,
            _ => $"{next.Definition} · revision {next.Revision} · {FormatAffected(next.EstimatedAffectedNpcs)}"
        };
        StatHistoryScope.Text = next == null ? "" : $"{SelectedStatScope.Label} · {next.Definition}";
        RefreshConfiguredStatsGrid(preserveDraft);
    }

    private void ConfiguredStatQuery_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            RefreshConfiguredStatsGrid(preserveDraft: true);
    }

    private void RefreshConfiguredStatsGrid(bool preserveDraft)
    {
        string term = ConfiguredStatQuery.Text.Trim();
        var prior = ConfiguredStatsGrid.SelectedItem as ConfiguredStatRow;
        ConfiguredStatRow[]? rows = savedStats?.Rows.Where(row => row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.StatId.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)).ToArray();
        var selected = rows?.FirstOrDefault(row => row.StatId == prior?.StatId);
        bool keep = preserveDraft && selected != null && NewConfiguredStat.Text != FormatStatValue(prior?.ConfiguredValue);
        updatingConfiguredStats = true;
        try
        {
            ConfiguredStatsGrid.ItemsSource = rows;
            ConfiguredStatsGrid.SelectedItem = selected;
        }
        finally
        {
            updatingConfiguredStats = false;
        }
        if (!keep)
            SetConfiguredStatDraft(selected);
        RefreshControls();
    }

    private void ConfiguredStat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !updatingConfiguredStats)
        {
            SetConfiguredStatDraft(ConfiguredStatsGrid.SelectedItem as ConfiguredStatRow);
            RefreshControls();
        }
    }

    private void SetConfiguredStatDraft(ConfiguredStatRow? row)
    {
        NewConfiguredStat.Text = FormatStatValue(row?.ConfiguredValue);
        configuredStatDraftRevision = savedStats?.Revision;
    }

    private void RevertConfiguredStatDraft_Click(object sender, RoutedEventArgs e)
    {
        SetConfiguredStatDraft(ConfiguredStatsGrid.SelectedItem as ConfiguredStatRow);
        RefreshControls();
    }

    private async void PreviewConfiguredStat_Click(object sender, RoutedEventArgs e) => await EditConfiguredStatAsync(false, false);
    private async void SaveConfiguredStat_Click(object sender, RoutedEventArgs e) => await EditConfiguredStatAsync(false, true);
    private async void ResetConfiguredStat_Click(object sender, RoutedEventArgs e) => await EditConfiguredStatAsync(true, true);

    private async Task EditConfiguredStatAsync(bool removeOverride, bool save)
    {
        if (!CanSaveStats || ConfiguredStatsGrid.SelectedItem is not ConfiguredStatRow { Editable: true } row
            || savedStats is not { } state || configuredStatDraftRevision != state.Revision)
            return;

        int? value = null;
        if (!removeOverride)
        {
            if (!int.TryParse(NewConfiguredStat.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                Status.Text = "Enter a whole-number configured base value.";
                return;
            }
            value = parsed;
        }
        string previousOverride = row.OverrideValue.HasValue ? FormatStatValue(row.OverrideValue) : "—";
        string change = removeOverride
            ? $"Remove the {row.Name} ({row.StatId}) override in this scope. Other applicable definitions determine the effective value."
            : $"{row.Name} ({row.StatId}) · scoped override {previousOverride} → {FormatStatValue(value)}"
                + $"{Environment.NewLine}Current configured value for the selected NPC · {FormatStatValue(row.ConfiguredValue)}";
        if (!ReviewStatConfiguration(state, change, save) || !save)
            return;

        await WriteConfiguredStatsAsync("save", new { expectedRevision = state.Revision, statId = row.StatId, value });
    }

    private bool ReviewStatConfiguration(SavedStatsState state, string change, bool confirm) =>
        ReviewConfiguration("Review saved stat change", $"{SelectedStatScope.Label}{Environment.NewLine}{state.Definition}", state.EstimatedAffectedNpcs,
            state.FutureSpawnsInherit, state.RequiresRespawn ? "Loaded NPCs must respawn to pick up the saved configuration." : "",
            state.Overlaps, change, confirm);

    private async Task WriteConfiguredStatsAsync(string verb, object request)
    {
        ConfiguredStatScope scope = SelectedStatScope;
        string path = ConfiguredStatPath(detail!.Npc.Identity, verb, scope);
        await RunAsync(async () =>
        {
            var next = await Api.PostAsync<SavedStatsState>(path, request);
            EnsureStatScope(next, scope);
            if (next.EstimatedAffectedNpcs == null && savedStats?.Definition == next.Definition)
                next = next with { EstimatedAffectedNpcs = savedStats.EstimatedAffectedNpcs };
            loadedStatScope = scope;
            RenderConfiguredStats(next);
            Status.Text = $"Saved stat revision {next.Revision} for future spawns.";
        });
    }

    private void StatHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var row = StatHistory.SelectedItem as StatRevision;
        StatHistoryDetails.Text = row == null ? "" : string.Join(Environment.NewLine,
            $"Before  {FormatStatChanges(row.Before)}", $"After  {FormatStatChanges(row.After)}");
        RefreshControls();
    }

    private string FormatStatChanges(IEnumerable<StatOverrideValue> entries) => string.Join(", ", entries.Select(entry =>
        $"{savedStats?.Rows.FirstOrDefault(row => row.StatId == entry.StatId)?.Name ?? entry.StatId.ToString(CultureInfo.InvariantCulture)} = {entry.Value}"));

    private async void RestoreStatRevision_Click(object sender, RoutedEventArgs e)
    {
        if (StatHistory.SelectedItem is StatRevision revision)
            await RestoreStatsAsync(revision.Revision);
    }

    private async void RestorePreviousStat_Click(object sender, RoutedEventArgs e)
    {
        if (savedStats is { } state)
            await RestoreStatsAsync(state.History.Where(row => row.Revision < state.Revision).Select(row => row.Revision).DefaultIfEmpty(0).Max());
    }

    private async Task RestoreStatsAsync(int revision)
    {
        if (CanSaveStats && savedStats is { } state && revision < state.Revision
            && ReviewStatConfiguration(state, $"Restore revision {revision} of this stat configuration.", true))
            await WriteConfiguredStatsAsync("restore", new { expectedRevision = state.Revision, revision });
    }

    private void RefreshConfiguredStatControls()
    {
        var row = ConfiguredStatsGrid.SelectedItem as ConfiguredStatRow;
        bool current = configuredStatDraftRevision == savedStats?.Revision;
        bool editable = CanSaveStats && current && row?.Editable == true;
        ConfiguredStatScopePicker.IsEnabled = detail != null && !busy;
        NewConfiguredStat.IsEnabled = PreviewConfiguredStat.IsEnabled = SaveConfiguredStat.IsEnabled = editable;
        ResetConfiguredStat.IsEnabled = editable && row?.OverrideValue != null;
        RevertConfiguredStatDraft.IsEnabled = row != null && !busy;
        RestorePreviousStat.IsEnabled = CanSaveStats && savedStats!.History.Length > 0;
        RestoreStatRevision.IsEnabled = CanSaveStats && StatHistory.SelectedItem is StatRevision revision
            && revision.Revision < savedStats!.Revision;
        ConfiguredStatLimits.Text = !current ? "The revision changed. Revert the draft before editing."
            : row == null ? "Select a stat and lock the NPC to edit its future base value."
            : row.Editable ? $"Value source · {row.Source}" : "This stat is read-only in GameData.";
    }

    private sealed record ConfiguredStatScope(string Label, string Code, string Query);
}

internal sealed record SavedStatsState(string Scope, string Definition, string TemplateHash, int BandIndex, int? BandLevel,
    int? EstimatedAffectedNpcs, bool FutureSpawnsInherit, bool RequiresRespawn, string[] Overlaps, string? UnavailableReason,
    int Revision, ConfiguredStatRow[] Rows, StatRevision[] History);

internal sealed record ConfiguredStatRow(int StatId, string Name, int? AuthoredValue, int? ConfiguredValue,
    int? OverrideValue, string Source, bool Editable);

internal sealed record StatOverrideValue(int StatId, int Value);

internal sealed record StatRevision(int Revision, DateTimeOffset AtUtc, string Actor, string Action,
    StatOverrideValue[] Before, StatOverrideValue[] After);
