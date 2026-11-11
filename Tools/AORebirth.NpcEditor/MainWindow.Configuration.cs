using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private async void Details_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && e.Source == Details && detail != null)
            await RunAsync(() => LoadSelectedConfigurationAsync());
    }

    private async void StatsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && e.Source == StatsViews && detail != null && StatsViews.SelectedItem != LiveStatsTab)
            await RunAsync(() => LoadSelectedConfigurationAsync());
    }

    private Task LoadSelectedConfigurationAsync(bool refresh = false)
    {
        if (detail == null)
            return Task.CompletedTask;

        NpcIdentity identity = detail.Npc.Identity;
        if (Details.SelectedItem == StatsTab && StatsViews.SelectedItem != LiveStatsTab && (refresh || savedStats == null))
            return LoadConfiguredStatsAsync(identity);
        if (Details.SelectedItem == SpawnTab && (refresh || spawn == null))
            return LoadSpawnAsync(identity);
        if (Details.SelectedItem == BehaviorTab && (refresh || behavior == null))
            return LoadBehaviorAsync(identity);
        return Task.CompletedTask;
    }

    private bool ReviewConfiguration(string title, string definition, int? affected, bool futureSpawns,
        string activation, IEnumerable<string>? overlaps, string change, bool confirm)
    {
        string overlapText = overlaps == null ? "" : string.Join(Environment.NewLine, overlaps);
        string message = string.Join(Environment.NewLine, definition, FormatAffected(affected),
            futureSpawns ? "Future spawns inherit this configuration." : "Only the selected loaded NPC is affected.",
            activation, overlapText.Length > 0 ? "Overlapping overrides" + Environment.NewLine + overlapText : "",
            "", change);
        var buttons = confirm ? MessageBoxButton.OKCancel : MessageBoxButton.OK;
        return MessageBox.Show(this, message, title, buttons, MessageBoxImage.Information) == MessageBoxResult.OK;
    }
}
