using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private SpawnView? spawn;
    private SpawnPointAddress? standaloneSpawn;
    private int? spawnDraftRevision;
    private string? spawnDraftSignature;
    private string[]? originalSpawnDraft;

    private bool CanSaveSpawn => session?.CanSave == true && spawn?.CanSave == true
        && spawnDraftRevision == spawn.Revision && spawnDraftSignature == spawn.AuthoredSignature
        && (standaloneSpawn != null ? !busy && !BatchSelectionActive && spawn.AuthoredSignature != null : CanWrite);
    private bool CanMoveSpawnLive => CanWrite && spawn?.LivePreview?.CanApply == true;

    private async Task LoadSpawnAsync(NpcIdentity identity)
    {
        try
        {
            RenderSpawn(await Api.GetAsync<SpawnView>(identity.Path + "/spawn"), preserveDraft: true);
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            RenderSpawn(null);
            SpawnDescription.Text = DescribeError(ex);
        }
    }

    private void ClearSpawn() => RenderSpawn(null);

    private async void BrowseSpawnPoints_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SpawnPointPicker(Api, standaloneSpawn?.PlayfieldId ?? detail?.Npc.Identity.PlayfieldId) { Owner = this };
        pickerOpen = true;
        try
        {
            if (dialog.ShowDialog() != true || dialog.Selected is not { } point)
                return;

            await RunAsync(async () =>
            {
                ClearDetail();
                settingNpcSelection = true;
                try
                {
                    FollowTarget.IsChecked = false;
                    NpcGrid.UnselectAll();
                }
                finally
                {
                    settingNpcSelection = false;
                }
                standaloneSpawn = point;
                Details.SelectedItem = SpawnTab;
                await LoadStandaloneSpawnAsync(point);
            });
        }
        finally
        {
            pickerOpen = false;
        }
    }

    private async Task LoadStandaloneSpawnAsync(SpawnPointAddress point)
    {
        try
        {
            SpawnView next = await Api.GetAsync<SpawnView>(point.Path);
            if (next.PlayfieldId != point.PlayfieldId || next.AuthoredIndex != point.AuthoredIndex)
                throw new InvalidOperationException("ZoneEngine returned an unexpected spawn point.");

            DetailHeading.Text = next.SpawnPointKey ?? "Authored spawn point";
            DetailIdentity.Text = next.Definition ?? $"Playfield {point.PlayfieldId} · Authored index {point.AuthoredIndex}";
            RenderSpawn(next, preserveDraft: true);
            Status.Text = "Loaded an authored spawn point.";
        }
        catch
        {
            RenderSpawn(null);
            throw;
        }
    }

    private void RenderSpawn(SpawnView? next, bool preserveDraft = false)
    {
        bool keepDraft = preserveDraft && originalSpawnDraft != null && !SpawnDraftText().SequenceEqual(originalSpawnDraft);
        spawn = next;
        SpawnHistory.ItemsSource = next?.History;
        SpawnHistoryScope.Text = next?.Definition ?? "";
        SpawnDescription.Text = next == null ? "" : string.Join(Environment.NewLine,
            next.Definition ?? "No editable authored spawn definition",
            $"Spawn key  {next.SpawnPointKey ?? "Unavailable"}",
            $"Source  {next.Source ?? "Unavailable"}",
            $"Playfield  {next.PlayfieldId} · resource {next.ResourcePlayfieldId}",
            $"District  {next.DistrictIndex?.ToString() ?? "—"} · authored index {next.AuthoredIndex?.ToString() ?? "—"}",
            $"Grouped sites  {next.SiteCount}",
            $"Current state  {next.State}",
            $"Next scheduled respawn  {(next.NextRespawnUtc?.ToString("u") ?? "—")}",
            $"Activation  {string.Join(", ", next.ActivationConditions ?? [])}");
        SpawnValues.Text = next == null ? "" : string.Join(Environment.NewLine,
            "Original GameData", FormatSpawnSettings(next.Original), "",
            "Saved configuration", FormatSpawnSettings(next.Configured), "",
            "Loaded placement", FormatSpawnSettings(next.Runtime));
        if (!keepDraft)
            ResetSpawnDraft();
        RefreshControls();
    }

    private string[] SpawnDraftText() =>
        [SpawnX.Text, SpawnY.Text, SpawnZ.Text, SpawnFacing.Text, SpawnFacingWidth.Text, SpawnHash.Text,
            SpawnMinLevel.Text, SpawnMaxLevel.Text, SpawnRespawn.Text, SpawnChance.Text];

    private void ResetSpawnDraft()
    {
        SpawnSettings? placement = spawn?.Configured;
        SpawnLivePreview? live = spawn?.LivePreview;
        SpawnX.Text = FormatNumber(placement?.X ?? live?.X);
        SpawnY.Text = FormatNumber(placement?.Y ?? live?.Y);
        SpawnZ.Text = FormatNumber(placement?.Z ?? live?.Z);
        SpawnFacing.Text = FormatStatValue(placement?.FacingDegrees);
        SpawnFacingWidth.Text = FormatStatValue(placement?.FacingWidthDegrees);
        SpawnHash.Text = placement?.Hash ?? "";
        SpawnMinLevel.Text = FormatStatValue(placement?.MinLevel);
        SpawnMaxLevel.Text = FormatStatValue(placement?.MaxLevel);
        SpawnRespawn.Text = FormatStatValue(placement?.RespawnSeconds);
        SpawnChance.Text = FormatStatValue(placement?.RespawnChance);
        spawnDraftRevision = spawn?.Revision;
        spawnDraftSignature = spawn?.AuthoredSignature;
        originalSpawnDraft = SpawnDraftText();
    }

    private static string FormatNumber(double? value) => value?.ToString("G", CultureInfo.InvariantCulture) ?? "";

    private static string FormatSpawnSettings(SpawnSettings? settings) => settings == null ? "Unavailable" :
        string.Join(Environment.NewLine, $"{settings.Hash} · level {settings.MinLevel}–{settings.MaxLevel}",
            FormattableString.Invariant($"X {settings.X:G9} · Y {settings.Y:G9} · Z {settings.Z:G9}"),
            FormattableString.Invariant($"Facing {settings.FacingDegrees:G9}° · width {settings.FacingWidthDegrees:G9}°"),
            $"Respawn {settings.RespawnSeconds}s · chance {settings.RespawnChance}%");

    private static bool TryCoordinate(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= float.MaxValue;

    private bool TrySpawnSettings(out SpawnSettings? result)
    {
        result = null;
        string hash = SpawnHash.Text.Trim();
        if (!TryCoordinate(SpawnX.Text, out double x) || !TryCoordinate(SpawnY.Text, out double y)
            || !TryCoordinate(SpawnZ.Text, out double z)
            || !int.TryParse(SpawnFacing.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int facing) || facing is < -360 or > 360
            || !TryNumber(SpawnFacingWidth.Text, 0, 360, out int width)
            || !TryNumber(SpawnMinLevel.Text, 1, ushort.MaxValue, out int minLevel)
            || !TryNumber(SpawnMaxLevel.Text, minLevel, ushort.MaxValue, out int maxLevel)
            || !TryNumber(SpawnRespawn.Text, 0, int.MaxValue, out int respawn)
            || !TryNumber(SpawnChance.Text, 0, 100, out int chance) || hash.Length == 0)
        {
            Status.Text = "Enter finite coordinates, valid facing, a template hash, ordered levels, and a respawn chance from 0–100.";
            return false;
        }
        result = new SpawnSettings(x, y, z, facing, width, minLevel, maxLevel, respawn, chance, hash);
        return true;
    }

    private void RevertSpawnDraft_Click(object sender, RoutedEventArgs e)
    {
        ResetSpawnDraft();
        RefreshControls();
    }

    private async void PreviewSpawnSave_Click(object sender, RoutedEventArgs e) => await EditSpawnAsync(save: false);
    private async void SaveSpawn_Click(object sender, RoutedEventArgs e) => await EditSpawnAsync(save: true);

    private async Task EditSpawnAsync(bool save)
    {
        if (!CanSaveSpawn || spawn is not { } state || !TrySpawnSettings(out var settings))
            return;

        string change = $"Before{Environment.NewLine}{FormatSpawnSettings(state.Configured)}"
            + $"{Environment.NewLine}{Environment.NewLine}After{Environment.NewLine}{FormatSpawnSettings(settings)}";
        if (ReviewSpawnConfiguration(state, change, save) && save)
            await WriteSpawnAsync("save", new { expectedRevision = state.Revision, state.AuthoredSignature, settings }, preserveDraft: false);
    }

    private bool ReviewSpawnConfiguration(SpawnView state, string change, bool confirm) =>
        ReviewConfiguration("Review specific spawn point", state.Definition ?? state.SpawnPointKey ?? "Spawn point",
            state.LoadedNpcs, true, "The owning playfield must rebuild before new placements use these values.",
            state.Overlaps, change, confirm);

    private async void PreviewSpawnLive_Click(object sender, RoutedEventArgs e)
    {
        if (!CanMoveSpawnLive || spawn?.LivePreview is not { } live)
            return;
        if (!TryCoordinate(SpawnX.Text, out double x) || !TryCoordinate(SpawnY.Text, out double y)
            || !TryCoordinate(SpawnZ.Text, out double z))
        {
            Status.Text = "Enter finite X, Y, and Z coordinates for the live move.";
            return;
        }
        string change = FormattableString.Invariant($"X {live.X:G9} / Y {live.Y:G9} / Z {live.Z:G9} → X {x:G9} / Y {y:G9} / Z {z:G9}");
        if (ReviewConfiguration("Review live position preview", detail!.Npc.Identity.ToString(), 1, false, "Takes effect immediately.",
                null, change, confirm: true))
            await WriteSpawnAsync("preview", new { x, y, z }, preserveDraft: true);
    }

    private async void UndoSpawnLive_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && spawn?.LivePreview?.ChangeId is Guid changeId)
            await WriteSpawnAsync("preview/revert", new { changeId }, preserveDraft: true);
    }

    private async Task WriteSpawnAsync(string route, object payload, bool preserveDraft)
    {
        string path = standaloneSpawn?.Path ?? detail!.Npc.Identity.Path + "/spawn";
        await RunAsync(async () =>
        {
            SpawnView next = await Api.PostAsync<SpawnView>($"{path}/{route}", payload);
            RenderSpawn(next, preserveDraft);
            Status.Text = preserveDraft ? "Updated the selected NPC's live position." : $"Saved spawn revision {next.Revision}.";
        });
    }

    private void SpawnHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var row = SpawnHistory.SelectedItem as SpawnRevision;
        SpawnHistoryDetails.Text = row == null ? "" : string.Join(Environment.NewLine,
            "Before", FormatSpawnSettings(row.Before), "", "After", FormatSpawnSettings(row.After));
        RefreshControls();
    }

    private async void RestoreSpawnRevision_Click(object sender, RoutedEventArgs e)
    {
        if (SpawnHistory.SelectedItem is SpawnRevision revision)
            await RestoreSpawnAsync(revision.Revision);
    }

    private async void RestorePreviousSpawn_Click(object sender, RoutedEventArgs e)
    {
        if (spawn is { } state)
            await RestoreSpawnAsync(state.History.Where(row => row.Revision < state.Revision).Select(row => row.Revision).DefaultIfEmpty(0).Max());
    }

    private async Task RestoreSpawnAsync(int revision)
    {
        if (CanSaveSpawn && spawn is { } state && revision < state.Revision
            && ReviewSpawnConfiguration(state, $"Restore revision {revision} of this spawn point.", true))
            await WriteSpawnAsync("restore", new { expectedRevision = state.Revision, state.AuthoredSignature, revision }, preserveDraft: false);
    }

    private void RefreshSpawnControls()
    {
        bool canEdit = CanSaveSpawn;
        SpawnX.IsEnabled = SpawnY.IsEnabled = SpawnZ.IsEnabled = canEdit || CanMoveSpawnLive;
        SpawnFacing.IsEnabled = SpawnFacingWidth.IsEnabled = SpawnHash.IsEnabled = SpawnMinLevel.IsEnabled =
            SpawnMaxLevel.IsEnabled = SpawnRespawn.IsEnabled = SpawnChance.IsEnabled = canEdit;
        PreviewSpawnSave.IsEnabled = SaveSpawn.IsEnabled = canEdit;
        RevertSpawnDraft.IsEnabled = spawn != null && !busy;
        PreviewSpawnLive.IsEnabled = CanMoveSpawnLive;
        UndoSpawnLive.IsEnabled = CanWrite && spawn?.LivePreview?.ChangeId != null;
        RestorePreviousSpawn.IsEnabled = canEdit && spawn!.History.Length > 0;
        RestoreSpawnRevision.IsEnabled = canEdit && SpawnHistory.SelectedItem is SpawnRevision revision
            && revision.Revision < spawn!.Revision;
        SpawnStatus.Text = spawn switch
        {
            null => "",
            { UnavailableReason: string reason } => reason,
            _ when spawnDraftRevision != spawn.Revision || spawnDraftSignature != spawn.AuthoredSignature =>
                "The placement or revision changed. Revert the draft before editing.",
            _ => $"Revision {spawn.Revision} · {FormatAffected(spawn.LoadedNpcs)}"
        };
        SpawnLiveStatus.Text = spawn?.LivePreview switch
        {
            null => "Live position preview unavailable.",
            { CanApply: false } live => live.UnavailableReason ?? "The NPC cannot be moved now.",
            { } live => FormattableString.Invariant($"Current X {live.X:G9} · Y {live.Y:G9} · Z {live.Z:G9}")
        };
    }
}

internal sealed record SpawnView(int PlayfieldId, int ResourcePlayfieldId, string? SpawnPointKey, string? Source,
    string? Definition, bool CanSave, string? UnavailableReason, int Revision, SpawnSettings? Original,
    SpawnSettings? Configured, SpawnSettings? Runtime, string State, DateTimeOffset? NextRespawnUtc, int? LoadedNpcs,
    bool RequiresPlayfieldReload, int SiteCount, int? DistrictIndex, int? AuthoredIndex, string[]? ActivationConditions,
    string[]? Overlaps, SpawnRevision[] History, SpawnLivePreview? LivePreview, string? AuthoredSignature)
{
    public string ConfiguredHash => Configured?.Hash ?? Runtime?.Hash ?? Original?.Hash ?? "";
}

internal sealed record SpawnSettings(double X, double Y, double Z, int FacingDegrees, int FacingWidthDegrees,
    int MinLevel, int MaxLevel, int RespawnSeconds, int RespawnChance, string Hash);

internal sealed record SpawnRevision(int Revision, string Actor, DateTimeOffset AtUtc, string Action,
    SpawnSettings? Before, SpawnSettings? After);

internal sealed record SpawnLivePreview(bool CanApply, string? UnavailableReason, double X, double Y, double Z, Guid? ChangeId);

internal sealed record SpawnPointAddress(int PlayfieldId, int AuthoredIndex)
{
    public string Path => FormattableString.Invariant($"playfields/{PlayfieldId}/spawns/{AuthoredIndex}");
}
