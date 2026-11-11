using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AORebirth.NpcEditor;

public partial class NamedLootTargetPicker : Window
{
    private readonly EditorApi api;
    private readonly string scope;
    private bool busy;

    internal NamedLootTargetPicker(EditorApi api, string scope, string playfield)
    {
        InitializeComponent();
        this.api = api;
        this.scope = scope;
        Playfield.Text = playfield;
        bool authored = scope == "authoredPoint";
        Title = authored ? "Find authored spawn point" : "Find NPC loot assignment target";
        Templates.Visibility = authored ? Visibility.Collapsed : Visibility.Visible;
        Spawns.Visibility = authored ? Visibility.Visible : Visibility.Collapsed;
        PlayfieldField.Visibility = authored ? Visibility.Visible : Visibility.Collapsed;
        FamilyField.Visibility = scope == "family" ? Visibility.Visible : Visibility.Collapsed;
        TargetStatus.Text = authored ? "Enter a resource playfield to find its authored points." : "Search by NPC template name or hash.";
        Loaded += (_, _) => (authored ? Playfield : Query).Focus();
        RefreshActions();
    }

    internal NamedLootTemplateTarget? SelectedTemplate { get; private set; }
    internal NamedLootSpawnTarget? Spawn { get; private set; }
    internal string? Family { get; private set; }

    private async void Find_Click(object sender, RoutedEventArgs e) => await FindAsync();

    private async void Query_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await FindAsync();
    }

    private async Task FindAsync()
    {
        if (busy)
            return;
        string filters = "q=" + Uri.EscapeDataString(Query.Text.Trim());
        if (scope == "authoredPoint")
        {
            if (!int.TryParse(Playfield.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
            {
                TargetStatus.Text = "Enter a valid resource playfield ID.";
                return;
            }
            filters += FormattableString.Invariant($"&playfieldId={id}");
        }
        busy = true;
        TargetStatus.Text = "Searching…";
        RefreshActions();
        try
        {
            NamedLootTargets found = await api.GetAsync<NamedLootTargets>("loot-tables/targets?" + filters);
            if (scope == "authoredPoint")
            {
                Spawns.ItemsSource = found.Spawns;
                TargetStatus.Text = $"{found.Spawns.Length} authored points returned.";
            }
            else
            {
                var rows = found.Templates.Where(row => (scope != "namedBoss" || row.IsBoss)
                    && (scope != "family" || row.FamilyHashes.Length > 0)).ToArray();
                Templates.ItemsSource = rows;
                TargetStatus.Text = $"{rows.Length} template bands returned.";
            }
        }
        catch (Exception ex)
        {
            Templates.ItemsSource = Spawns.ItemsSource = null;
            TargetStatus.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshActions();
        }
    }

    private void Row_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var hashes = (Templates.SelectedItem as NamedLootTemplateTarget)?.FamilyHashes;
        Families.ItemsSource = hashes;
        Families.SelectedIndex = hashes?.Length == 1 ? 0 : -1;
        RefreshActions();
    }

    private void Family_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            RefreshActions();
    }

    private void RefreshActions()
    {
        SearchButton.IsEnabled = !busy;
        Choose.IsEnabled = !busy && (scope == "authoredPoint" ? Spawns.SelectedItem is NamedLootSpawnTarget
            : Templates.SelectedItem is NamedLootTemplateTarget && (scope != "family" || Families.SelectedItem is string));
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (!Choose.IsEnabled)
            return;
        SelectedTemplate = Templates.SelectedItem as NamedLootTemplateTarget;
        Spawn = Spawns.SelectedItem as NamedLootSpawnTarget;
        Family = Families.SelectedItem as string;
        DialogResult = true;
    }
}
