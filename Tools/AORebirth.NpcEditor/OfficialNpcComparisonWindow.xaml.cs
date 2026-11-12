using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

using AORebirth.NpcEditor.Official;

namespace AORebirth.NpcEditor;

public partial class OfficialNpcComparisonWindow : Window
{
    private readonly OfficialSnapshot snapshot;
    private CancellationTokenSource? operation;
    private string? catalog;

    internal OfficialNpcComparisonWindow(OfficialSnapshot snapshot)
    {
        InitializeComponent();
        this.snapshot = snapshot;
        Observation.Text = $"{snapshot.Name ?? "UNKNOWN"} · PF {snapshot.PlayfieldId} · "
            + $"{new DateTimeOffset(snapshot.CapturedUtcTicks, TimeSpan.Zero):u}. Candidate matches do not establish original-server template identity.";
        CompareCatalog.IsEnabled = false;
        Closed += (_, _) => operation?.Cancel();
    }

    private async void ChooseCatalog_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose the AORebirth NPC template catalog",
            FileName = "NpcTemplates.json",
            Filter = "AORebirth NPC catalog (NpcTemplates.json)|NpcTemplates.json",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true)
            return;
        catalog = picker.FileName;
        CatalogLabel.Text = catalog;
        await CompareAsync();
    }

    private async void CompareCatalog_Click(object sender, RoutedEventArgs e) => await CompareAsync();

    private async Task CompareAsync()
    {
        if (catalog is not { } path || operation != null)
            return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        operation = deadline;
        ChooseCatalog.IsEnabled = CompareCatalog.IsEnabled = false;
        Candidates.ItemsSource = null;
        ComparisonRows.ItemsSource = null;
        string hash = TemplateHash.Text;
        Status.Text = "Reading catalog candidates…";
        try
        {
            OfficialComparisonResult result = await Task.Run(
                () => OfficialNpcComparison.ReadAsync(path, snapshot, hash, deadline.Token), deadline.Token);
            Candidates.ItemsSource = result.Candidates;
            Status.Text = result.Total == 0 ? "No matching catalog bands." :
                $"{result.Candidates.Length} of {result.Total} candidate bands. Select one to compare.";
        }
        catch (Exception ex)
        {
            Status.Text = ex is OperationCanceledException ? "The catalog scan was canceled or reached its time limit."
                : ex.Message;
        }
        finally
        {
            operation = null;
            ChooseCatalog.IsEnabled = CompareCatalog.IsEnabled = true;
        }
    }

    private void Candidate_Changed(object sender, SelectionChangedEventArgs e)
        => ComparisonRows.ItemsSource = (Candidates.SelectedItem as OfficialComparisonCandidate)?.Rows;
}
