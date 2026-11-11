using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private NamedLootEffectiveView? namedEffective;

    private async void BrowseNamedLoot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NamedLootWindow(Api, session?.CanSave == true) { Owner = this };
        pickerOpen = true;
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            pickerOpen = false;
        }

        if (dialog.Unauthorized)
        {
            ResetConnection();
            Status.Text = "Sign in again to use the NPC Editor.";
        }
        else if (dialog.Changed && detail?.Npc.Identity is { } selected)
        {
            await RunAsync(() => LoadLootAsync(selected));
            ClearLootSimulation("Named loot changed. Run a new simulation for the current inputs.");
        }
    }

    private async Task LoadNamedEffectiveAsync(NpcIdentity identity)
    {
        try
        {
            RenderNamedEffective(await Api.GetAsync<NamedLootEffectiveView>(identity.Path + "/loot/effective"));
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            RenderNamedEffective(null);
            NamedEffectiveStatus.Text = DescribeError(ex);
        }
    }

    private void RenderNamedEffective(NamedLootEffectiveView? view)
    {
        namedEffective = view;
        EffectiveNamedGroups.ItemsSource = view?.Resolution.Groups;
        EffectiveNamedGroups.SelectedIndex = view?.Resolution.Groups.Length > 0 ? 0 : -1;
        NamedLootTrace.ItemsSource = view?.Resolution.Trace;
        ShowEffectiveCandidates();
        NamedEffectiveStatus.Text = view == null ? "" : $"{view.NativeRows.Length} native rows on Selected NPC · "
            + $"{view.Resolution.Groups.Length} named groups. Initial % is conditional on group activation.";
    }

    private void EffectiveNamedGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ShowEffectiveCandidates();
    }

    private void ShowEffectiveCandidates()
    {
        var group = EffectiveNamedGroups.SelectedItem as NamedLootResolvedGroup;
        EffectiveNamedCandidates.ItemsSource = group == null ? namedEffective?.Candidates
            : namedEffective?.Candidates.Where(row => row.QualifiedGroupId == group.QualifiedId).ToArray();
        ShowNamedLootDetails();
    }

    private void EffectiveNamedCandidate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ShowNamedLootDetails();
    }

    private void ShowNamedLootDetails()
    {
        var candidate = EffectiveNamedCandidates.SelectedItem as NamedLootCandidate;
        var group = candidate == null ? EffectiveNamedGroups.SelectedItem as NamedLootResolvedGroup
            : namedEffective?.Resolution.Groups.FirstOrDefault(row => row.QualifiedId == candidate.QualifiedGroupId);
        if (group == null)
        {
            EffectiveNamedDetails.Text = "";
            return;
        }

        var entry = group.Group.Entries.FirstOrDefault(row => row.Id == candidate?.EntryId);
        var lines = new List<string>
        {
            $"Group {group.QualifiedId} · assignment {group.AssignmentId} · empty weight {group.Group.EmptyWeight}",
            $"Group conditions  {DescribeNamedLootCondition(group.Group.Condition)}"
        };
        if (entry != null)
        {
            lines.Add($"Entry conditions  {DescribeNamedLootCondition(entry.Condition)}");
            lines.Add($"Quantity {entry.MinQuantity}–{entry.MaxQuantity} · {(entry.LevelQuality ? "NPC-level QL" : "Configured QL range")}"
                + (entry.UniquePerCorpse ? " · Unique per corpse" : ""));
            lines.Add($"Evidence  {entry.EvidenceClassification ?? "No linked classification"}  {entry.EvidenceReference}".TrimEnd());
        }
        EffectiveNamedDetails.Text = string.Join(Environment.NewLine, lines);
    }

    private static string DescribeNamedLootCondition(NamedLootCondition? condition)
    {
        if (condition == null)
            return "None";
        var parts = new List<string>();
        if (condition.MinimumLevel is int minimum)
            parts.Add($"level ≥ {minimum}");
        if (condition.MaximumLevel is int maximum)
            parts.Add($"level ≤ {maximum}");
        if (condition.PlayfieldId is int playfield)
            parts.Add($"resource playfield {playfield}");
        if (condition.BossOnly)
            parts.Add("boss only");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }
}
