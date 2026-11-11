using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private BehaviorView? behavior;
    private string? behaviorDraftVersion;
    private string? originalWaypoints;
    private string? originalHostility;
    private bool originalPatrolEnabled;
    private bool originalPatrolRun;

    private bool CanEditBehavior => CanWrite && behavior is { } state && behaviorDraftVersion == state.Version
        && (state.CanEditAggression || state.CanEditPatrol);

    private async Task LoadBehaviorAsync(NpcIdentity identity)
    {
        try
        {
            RenderBehavior(await Api.GetAsync<BehaviorView>(identity.Path + "/behavior"), preserveDraft: true);
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            RenderBehavior(null);
            BehaviorStatus.Text = DescribeError(ex);
        }
    }

    private void ClearBehavior() => RenderBehavior(null);

    private void RenderBehavior(BehaviorView? next, bool preserveDraft = false)
    {
        bool keepDraft = preserveDraft && HasBehaviorDraft();
        behavior = next;
        BehaviorGrid.ItemsSource = next?.Settings;
        BehaviorHistory.ItemsSource = next?.History;
        if (!keepDraft)
            ResetBehaviorDraft();
        RefreshControls();
    }

    private bool HasBehaviorDraft() => behaviorDraftVersion != null
        && (BreedHostility.Text != originalHostility || PatrolWaypoints.Text != originalWaypoints
            || PatrolEnabled.IsChecked != originalPatrolEnabled || PatrolRun.IsChecked != originalPatrolRun);

    private void ResetBehaviorDraft()
    {
        BreedHostility.Text = originalHostility = FormatStatValue(behavior?.BreedHostility);
        PatrolEnabled.IsChecked = originalPatrolEnabled = behavior?.PatrolEnabled == true;
        PatrolRun.IsChecked = originalPatrolRun = behavior?.PatrolRun == true;
        PatrolWaypoints.Text = originalWaypoints = string.Join(Environment.NewLine,
            (behavior?.Waypoints ?? []).Select(point => FormattableString.Invariant($"{point.X:G} {point.Y:G} {point.Z:G}")));
        behaviorDraftVersion = behavior?.Version;
    }

    private void RevertBehaviorDraft_Click(object sender, RoutedEventArgs e)
    {
        ResetBehaviorDraft();
        RefreshControls();
    }

    private async void PreviewBehavior_Click(object sender, RoutedEventArgs e) => await EditBehaviorAsync(apply: false);
    private async void ApplyBehavior_Click(object sender, RoutedEventArgs e) => await EditBehaviorAsync(apply: true);

    private async Task EditBehaviorAsync(bool apply)
    {
        if (!CanEditBehavior || behavior is not { } state)
            return;

        int? breedHostility = null;
        bool? patrolEnabled = null;
        bool? patrolRun = null;
        BehaviorPoint[]? waypoints = null;
        var changes = new List<string>();
        if (state.CanEditAggression && BreedHostility.Text != originalHostility)
        {
            if (!TryNumber(BreedHostility.Text, 0, 100, out int value))
            {
                Status.Text = "Enter a breed hostility value from 0–100.";
                return;
            }
            breedHostility = value;
            changes.Add($"Breed hostility  {state.BreedHostility} → {breedHostility}");
        }
        if (state.CanEditPatrol)
        {
            if (PatrolEnabled.IsChecked != originalPatrolEnabled)
            {
                patrolEnabled = PatrolEnabled.IsChecked == true;
                changes.Add($"Patrol enabled  {state.PatrolEnabled} → {patrolEnabled}");
            }
            if (PatrolRun.IsChecked != originalPatrolRun)
            {
                patrolRun = PatrolRun.IsChecked == true;
                changes.Add($"Patrol run  {state.PatrolRun} → {patrolRun}");
            }
            if (PatrolWaypoints.Text != originalWaypoints)
            {
                if (!TryReadWaypoints(PatrolWaypoints.Text, out waypoints))
                {
                    Status.Text = "Enter three finite coordinates per waypoint, separated by spaces.";
                    return;
                }
                changes.Add($"Patrol waypoints  {state.Waypoints?.Length ?? 0} → {waypoints.Length}");
            }
        }
        if (changes.Count == 0)
        {
            Status.Text = "Edit an available behavior setting first.";
            return;
        }

        if (ReviewConfiguration("Review live behavior", detail!.Npc.Identity.ToString(), 1, false,
                "Takes effect when the selected NPC is eligible for the edit.", null,
                string.Join(Environment.NewLine, changes), confirm: apply) && apply)
        {
            await WriteBehaviorAsync("behavior", new
            {
                expectedVersion = state.Version,
                breedHostility,
                patrolEnabled,
                patrolRun,
                waypoints
            }, "Updated behavior on the selected NPC.");
        }
    }

    private static bool TryReadWaypoints(string text, out BehaviorPoint[] result)
    {
        result = [];
        var points = new List<BehaviorPoint>();
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (points.Count == 64 || fields.Length != 3 || !TryCoordinate(fields[0], out double x) || !TryCoordinate(fields[1], out double y)
                || !TryCoordinate(fields[2], out double z))
                return false;
            points.Add(new BehaviorPoint(x, y, z));
        }
        result = points.ToArray();
        return true;
    }

    private async void UndoBehavior_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && behavior is { } state && BehaviorHistory.SelectedItem is BehaviorChange { Revertible: true } change)
            await WriteBehaviorAsync("behavior/undo", new { expectedVersion = state.Version, changeId = change.ChangeId },
                "Undid the selected live behavior change.");
    }

    private async Task WriteBehaviorAsync(string route, object payload, string success)
    {
        string path = detail!.Npc.Identity.Path;
        await RunAsync(async () =>
        {
            RenderBehavior(await Api.PostAsync<BehaviorView>($"{path}/{route}", payload));
            Status.Text = success;
        });
    }

    private void BehaviorHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var row = BehaviorHistory.SelectedItem as BehaviorChange;
        BehaviorHistoryDetails.Text = row == null ? "" : string.Join(Environment.NewLine,
            $"Before  {row.Before}", $"After  {row.After}");
        RefreshControls();
    }

    private void RefreshBehaviorControls()
    {
        BreedHostility.IsEnabled = CanEditBehavior && behavior?.CanEditAggression == true;
        PatrolEnabled.IsEnabled = PatrolRun.IsEnabled = PatrolWaypoints.IsEnabled = CanEditBehavior && behavior?.CanEditPatrol == true;
        ApplyBehavior.IsEnabled = PreviewBehavior.IsEnabled = CanEditBehavior;
        RevertBehaviorDraft.IsEnabled = behavior != null && !busy;
        UndoBehavior.IsEnabled = CanWrite && BehaviorHistory.SelectedItem is BehaviorChange { Revertible: true };
        BehaviorStatus.Text = behavior switch
        {
            null => "",
            _ when behaviorDraftVersion != behavior.Version => "The behavior changed. Revert the draft before editing.",
            { UnavailableReason: string reason } => reason,
            _ => "Live changes affect only the selected NPC. Editing requires a locked selection and an eligible NPC."
        };
    }
}

internal sealed record BehaviorView(string Version, BehaviorSetting[] Settings, int? BreedHostility, bool PatrolEnabled,
    bool PatrolRun, BehaviorPoint[]? Waypoints, bool CanEditAggression, bool CanEditPatrol,
    string? UnavailableReason, BehaviorChange[] History);

internal sealed record BehaviorSetting(string Name, string? Runtime, string? Configured, string Source, bool Editable)
{
    public string RuntimeText => Runtime ?? "—";
    public string ConfiguredText => Configured ?? "—";
}

internal sealed record BehaviorPoint(double X, double Y, double Z);

internal sealed record BehaviorChange(Guid ChangeId, string Actor, DateTimeOffset AtUtc, string Before, string After,
    bool Reverted, bool Revertible);
