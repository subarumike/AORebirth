using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private const int MaxBatchTargets = 20;

    private bool BatchSelectionActive => BatchMode.IsChecked == true;

    private void BatchMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        settingNpcSelection = true;
        try
        {
            NpcGrid.UnselectAll();
            NpcGrid.SelectionMode = BatchSelectionActive ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;
            BatchSelectionPanel.Visibility = BatchSelectionActive ? Visibility.Visible : Visibility.Collapsed;
            if (BatchSelectionActive)
            {
                FollowTarget.IsChecked = false;
                ClearDetail();
            }
        }
        finally
        {
            settingNpcSelection = false;
        }
        RefreshControls();
    }

    private bool TryGetBatchFilter(out string family, out int? min, out int? max)
    {
        family = "";
        min = max = null;
        if (!BatchSelectionActive)
            return true;

        family = BatchFamilyFilter.Text.Trim();
        if (TryLevelFilter(BatchMinLevel.Text, out min) && TryLevelFilter(BatchMaxLevel.Text, out max) && !(min > max))
            return true;
        Status.Text = "Enter an ordered level range or leave the level filters blank.";
        return false;
    }

    private static bool TryLevelFilter(string text, out int? level)
    {
        level = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
            return false;
        level = value;
        return true;
    }

    private void SelectBatchResults_Click(object sender, RoutedEventArgs e)
    {
        if (NpcGrid.ItemsSource is NpcSummary[] rows && IsBatchSelectionValid(rows))
            NpcGrid.SelectAll();
    }

    private async void ReviewBatch_Click(object sender, RoutedEventArgs e)
    {
        NpcSummary[] selected = NpcGrid.SelectedItems.Cast<NpcSummary>().ToArray();
        if (!BatchSelectionActive || session?.CanWrite != true || !IsBatchSelectionValid(selected))
            return;

        var dialog = new BatchEditor(Api, selected, session.CanSave) { Owner = this };
        pickerOpen = true;
        try
        {
            dialog.ShowDialog();
            if (dialog.LastOutcome != null)
                Status.Text = dialog.LastOutcome;
        }
        finally
        {
            pickerOpen = false;
        }
        if (dialog.Unauthorized)
            await RunAsync(() =>
            {
                ResetConnection();
                Status.Text = "Sign in again to use the NPC Editor.";
                return Task.CompletedTask;
            });
    }

    private static bool IsBatchSelectionValid(NpcSummary[] rows) => rows.Length is > 0 and <= MaxBatchTargets
        && rows.All(row => row.Identity.PlayfieldId == rows[0].Identity.PlayfieldId);

    private void RefreshBatchControls()
    {
        if (!IsLoaded)
            return;

        BatchMode.IsEnabled = !busy && LockNpc.IsChecked != true;
        FollowTarget.IsEnabled = !BatchSelectionActive;
        if (!BatchSelectionActive)
            return;

        NpcSummary[] selected = NpcGrid.SelectedItems.Cast<NpcSummary>().ToArray();
        SelectBatchResults.IsEnabled = NpcGrid.ItemsSource is NpcSummary[] rows && IsBatchSelectionValid(rows);
        ReviewBatch.IsEnabled = !busy && session?.CanWrite == true && IsBatchSelectionValid(selected);
        BatchSelectionStatus.Text = selected.Length switch
        {
            0 => $"Select up to {MaxBatchTargets} NPCs from one playfield.",
            _ when !IsBatchSelectionValid(selected) => $"The selection must contain at most {MaxBatchTargets} NPCs from one playfield.",
            _ => $"{selected.Length} selected · playfield {selected[0].Identity.PlayfieldId}"
        };
    }
}
