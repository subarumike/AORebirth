using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AORebirth.NpcEditor;

public partial class ItemPicker : Window
{
    private readonly Func<string, Task<PickerRow[]>> search;

    internal ItemPicker(string title, Func<string, Task<PickerRow[]>> search)
    {
        InitializeComponent();
        Title = title;
        this.search = search;
        Loaded += (_, _) => Query.Focus();
    }

    internal object? Selected { get; private set; }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void Query_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (!SearchButton.IsEnabled || string.IsNullOrWhiteSpace(Query.Text))
            return;

        SearchButton.IsEnabled = false;
        Results.ItemsSource = null;
        ResultText.Text = "Searching…";
        try
        {
            PickerRow[] rows = await search(Query.Text.Trim());
            Results.ItemsSource = rows;
            ResultText.Text = rows.Length == 0 ? "No items matched the query." : "Select a row to preview its catalog identity.";
        }
        catch (Exception ex)
        {
            ResultText.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = Results.SelectedItem as PickerRow;
        Choose.IsEnabled = row != null;
        if (row != null)
            ResultText.Text = row.Preview;
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (Results.SelectedItem is PickerRow row)
        {
            Selected = row.Value;
            DialogResult = true;
        }
    }
}

internal sealed record PickerRow(string Name, string Identifier, string Preview, object Value);
