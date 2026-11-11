using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class SpawnPointPicker : Window
{
    private readonly EditorApi api;
    private int playfield;
    private SpawnPointPage? page;
    private bool busy;

    internal SpawnPointPicker(EditorApi api, int? initialPlayfield)
    {
        InitializeComponent();
        this.api = api;
        PlayfieldId.Text = initialPlayfield?.ToString(CultureInfo.InvariantCulture) ?? "";
        RefreshActions();
    }

    internal SpawnPointAddress? Selected { get; private set; }

    private async void FindPoints_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(PlayfieldId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0)
            await LoadPageAsync(id, 0);
        else
            PointStatus.Text = "Enter the loaded runtime playfield ID.";
    }

    private async void PreviousPoints_Click(object sender, RoutedEventArgs e)
    {
        if (page is { } current)
            await LoadPageAsync(playfield, Math.Max(0, current.Offset - current.Limit));
    }

    private async void NextPoints_Click(object sender, RoutedEventArgs e)
    {
        if (page is { } current)
            await LoadPageAsync(playfield, current.Offset + current.Limit);
    }

    private async Task LoadPageAsync(int id, int offset)
    {
        if (busy)
            return;

        busy = true;
        PointStatus.Text = "Loading authored spawn points…";
        RefreshActions();
        try
        {
            SpawnPointPage next = await api.GetAsync<SpawnPointPage>(
                FormattableString.Invariant($"playfields/{id}/spawns?offset={offset}&limit=50"));
            playfield = id;
            page = next;
            Points.ItemsSource = next.Points;
            PointStatus.Text = next.Points.Length == 0 ? "No authored points in this playfield."
                : $"Playfield {id} · Showing {next.Offset + 1}–{next.Offset + next.Points.Length} of {next.Total}";
        }
        catch (Exception ex)
        {
            page = null;
            Points.ItemsSource = null;
            PointStatus.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshActions();
        }
    }

    private void Points_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            RefreshActions();
    }

    private void ChoosePoint_Click(object sender, RoutedEventArgs e)
    {
        if (!busy && Points.SelectedItem is SpawnView { AuthoredIndex: int index } row)
        {
            Selected = new SpawnPointAddress(row.PlayfieldId, index);
            DialogResult = true;
        }
    }

    private void RefreshActions()
    {
        FindPoints.IsEnabled = !busy;
        ChoosePoint.IsEnabled = !busy && Points.SelectedItem is SpawnView { AuthoredIndex: not null };
        PreviousPoints.IsEnabled = !busy && page?.Offset > 0;
        NextPoints.IsEnabled = !busy && page is { } current && current.Offset + current.Limit < current.Total;
    }
}

internal sealed record SpawnPointPage(int Offset, int Limit, int Total, SpawnView[] Points);
