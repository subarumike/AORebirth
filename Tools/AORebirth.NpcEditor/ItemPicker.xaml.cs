using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AORebirth.NpcEditor;

public partial class ItemPicker : Window
{
    private readonly EditorApi api;
    private readonly bool families;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Stack<int> previousPages = new();
    private CancellationTokenSource? previewRequest;
    private ItemQuery? query;
    private int offset;
    private int? nextOffset;

    internal ItemPicker(EditorApi api, string title, bool families = false, string? category = null, bool browseOnly = false)
    {
        InitializeComponent();
        this.api = api;
        this.families = families;
        Title = title;
        Category.ItemsSource = new[] { "All categories", "Weapon", "Armor", "Nano", "Misc", "Implant", "Npc", "Spirit", "Utility", "Tower" };
        Category.SelectedItem = category;
        if (Category.SelectedIndex < 0)
            Category.SelectedIndex = 0;
        if (browseOnly)
        {
            Choose.Visibility = Visibility.Collapsed;
            ClosePicker.Content = "Close";
        }
        Loaded += (_, _) => Query.Focus();
        Closed += (_, _) =>
        {
            lifetime.Cancel();
            lifetime.Dispose();
        };
    }

    internal ItemRow? Selected { get; private set; }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void Query_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (!SearchButton.IsEnabled)
            return;

        if (!TryQuality(MinQuality.Text, out int? min) || !TryQuality(MaxQuality.Text, out int? max) || min > max)
        {
            ResultText.Text = "Enter a valid QL range.";
            return;
        }

        string category = Category.SelectedIndex > 0 ? (string)Category.SelectedItem : "";
        var nextQuery = new ItemQuery(Query.Text.Trim(), category, min, max);
        if (await LoadPageAsync(nextQuery, 0))
        {
            query = nextQuery;
            previousPages.Clear();
            RefreshPageButtons();
        }
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (query != null && previousPages.TryPeek(out int previous) && await LoadPageAsync(query, previous))
        {
            previousPages.Pop();
            RefreshPageButtons();
        }
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        int previous = offset;
        if (query != null && nextOffset is int next && await LoadPageAsync(query, next))
        {
            previousPages.Push(previous);
            RefreshPageButtons();
        }
    }

    private async Task<bool> LoadPageAsync(ItemQuery search, int start)
    {
        SearchButton.IsEnabled = Previous.IsEnabled = Next.IsEnabled = Choose.IsEnabled = false;
        ResultText.Text = "Searching…";
        try
        {
            string filters = FormattableString.Invariant(
                $"q={Uri.EscapeDataString(search.Text)}&category={Uri.EscapeDataString(search.Category)}&offset={start}&limit=100&families={families.ToString().ToLowerInvariant()}");
            if (search.Min is int min)
                filters += FormattableString.Invariant($"&minQl={min}");
            if (search.Max is int max)
                filters += FormattableString.Invariant($"&maxQl={max}");
            ItemPage page = await api.GetAsync<ItemPage>("item-catalog?" + filters, lifetime.Token);
            Results.ItemsSource = page.Rows;
            offset = start;
            nextOffset = page.HasMore && page.NextOffset > start ? page.NextOffset : null;
            ResultText.Text = page.Rows.Length == 0 ? "No matching items." : $"Results {start + 1}–{start + page.Rows.Length}";
            return true;
        }
        catch (Exception ex)
        {
            if (IsLoaded)
                ResultText.Text = MainWindow.DescribeError(ex);
            return false;
        }
        finally
        {
            SearchButton.IsEnabled = true;
            Choose.IsEnabled = Results.SelectedItem is ItemRow;
            RefreshPageButtons();
        }
    }

    private void RefreshPageButtons()
    {
        Previous.IsEnabled = previousPages.Count > 0;
        Next.IsEnabled = nextOffset.HasValue;
    }

    private static bool TryQuality(string text, out int? quality)
    {
        quality = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
            return false;
        quality = value;
        return true;
    }

    private async void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        previewRequest?.Cancel();
        var row = Results.SelectedItem as ItemRow;
        Choose.IsEnabled = row != null && SearchButton.IsEnabled;
        PreviewText.Text = row?.Preview ?? "";
        if (row == null)
            return;

        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        previewRequest = request;
        try
        {
            ItemDetails item = await api.GetAsync<ItemDetails>(FormattableString.Invariant($"item-catalog/{row.Id}"), request.Token);
            if (ReferenceEquals(previewRequest, request) && !request.IsCancellationRequested)
            {
                var description = string.IsNullOrWhiteSpace(item.Description) ? "No description in the loaded catalog." : item.Description;
                PreviewText.Text = string.Join(Environment.NewLine, new[] { row.Preview, description }
                    .Concat(item.Stats.Select(stat => $"{stat.Name} ({stat.Id})  {stat.Value}")));
            }
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(previewRequest, request) && !request.IsCancellationRequested)
                PreviewText.Text = $"{row.Preview}{Environment.NewLine}{MainWindow.DescribeError(ex)}";
        }
        finally
        {
            if (ReferenceEquals(previewRequest, request))
                previewRequest = null;
        }
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (Results.SelectedItem is ItemRow row)
        {
            Selected = row;
            DialogResult = true;
        }
    }

    private sealed record ItemQuery(string Text, string Category, int? Min, int? Max);
}
