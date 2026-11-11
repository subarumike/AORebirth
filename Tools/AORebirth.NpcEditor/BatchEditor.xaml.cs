using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class BatchEditor : Window
{
    private static readonly TimeSpan BatchRequestTimeout = TimeSpan.FromSeconds(65);
    private readonly EditorApi api;
    private readonly NpcIdentity[] targets;
    private readonly bool allowSave;
    private bool busy;
    private ItemRow? armor;
    private BatchPreview? preview;
    private Guid? lastBatch;

    internal BatchEditor(EditorApi api, NpcSummary[] selection, bool allowSave)
    {
        InitializeComponent();
        this.api = api;
        targets = selection.Select(row => row.Identity).ToArray();
        this.allowSave = allowSave;
        ScopeText.Text = $"{selection.Length} selected NPC(s) · playfield {targets[0].PlayfieldId}";
        Rows.ItemsSource = selection.Select(row => new BatchRow(row.Identity, row.Name, "Selected NPC", "", "", null, null)).ToArray();
        var operations = new List<BatchOperation>
        {
            new("stat", "Live stat"),
            new("aggression", "Live breed hostility"),
            new("armor", "Add live armor"),
            new("loot", "Add live loot")
        };
        if (allowSave)
            operations.Add(new("respawn", "Save spawn respawn settings"));
        Operation.ItemsSource = operations;
        Operation.SelectedIndex = 0;
        Loaded += LoadStats;
        Closing += (_, args) => args.Cancel = busy;
        RefreshActions();
    }

    internal string? LastOutcome { get; private set; }
    internal bool Unauthorized { get; private set; }

    private string? OperationCode => (Operation.SelectedItem as BatchOperation)?.Code;

    private async void LoadStats(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            StatRow[] rows = await api.GetAsync<StatRow[]>(targets[0].Path + "/stats");
            Stat.ItemsSource = rows.Where(row => row.Editable && row.RuntimeBase.HasValue)
                .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .Select(row => new StatChoice(row.StatId, $"{row.Name} ({row.StatId})")).ToArray();
            BatchStatus.Text = "Preview the operation to validate all selected targets.";
        });
    }

    private void Operation_Changed(object sender, SelectionChangedEventArgs e)
    {
        StatFields.Visibility = VisibilityFor("stat");
        AggressionFields.Visibility = VisibilityFor("aggression");
        ArmorFields.Visibility = VisibilityFor("armor");
        LootFields.Visibility = VisibilityFor("loot");
        RespawnFields.Visibility = VisibilityFor("respawn");
        Draft_Changed(sender, e);
    }

    private Visibility VisibilityFor(string operation) => OperationCode == operation ? Visibility.Visible : Visibility.Collapsed;

    private void Draft_Changed(object sender, RoutedEventArgs e)
    {
        if (preview == null)
            return;

        preview = null;
        BatchStatus.Text = "The draft changed. Preview it again before applying.";
        RefreshActions();
    }

    private void PickArmor_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ItemPicker(api, "Choose live armor", category: "Armor") { Owner = this };
        if (picker.ShowDialog() == true && picker.Selected is { } item)
        {
            armor = item;
            ArmorChoice.Text = item.Preview;
            Draft_Changed(sender, e);
        }
    }

    private void PickLoot_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ItemPicker(api, "Choose loot item family", families: true) { Owner = this };
        if (picker.ShowDialog() == true && picker.Selected is { Hash: string hash })
            LootHash.Text = hash;
    }

    private async void PreviewBatch_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRequest(out BatchRequest? request))
            return;

        await RunAsync(async () =>
        {
            preview = null;
            BatchStatus.Text = "Checking all targets…";
            preview = await api.PostAsync<BatchPreview>("batch/preview", request!, BatchRequestTimeout);
            Rows.ItemsSource = preview.Rows.Select(row => row with { Applied = null }).ToArray();
            BatchStatus.Text = preview.PlanId.HasValue
                ? $"{preview.Scope} · Validated {preview.Rows.Length} targets · Expires {preview.ExpiresAt?.ToLocalTime():T}"
                : "The batch cannot be applied. Check the per-target results.";
        });
    }

    private bool TryBuildRequest(out BatchRequest? request)
    {
        request = OperationCode switch
        {
            "stat" when Stat.SelectedItem is StatChoice stat
                && int.TryParse(StatValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) =>
                new BatchRequest("stat", targets, StatId: stat.Id, Value: value),
            "aggression" when TryRange(Hostility.Text, 0, 100, out int hostility) =>
                new BatchRequest("aggression", targets, Value: hostility),
            "armor" when armor != null =>
                new BatchRequest("armor", targets, ItemId: armor.Id, Quality: armor.Quality),
            "loot" when !string.IsNullOrWhiteSpace(LootHash.Text)
                && TryRange(DropChance.Text, 0, 100, out int chance)
                && TryRange(DropRepeats.Text, 1, 20, out int repeats)
                && TryRange(DropVariance.Text, 0, 100, out int variance) =>
                new BatchRequest("loot", targets, LootEntry: new LootEntry(LootHash.Text.Trim(), chance, repeats, variance)),
            "respawn" when allowSave && TryRange(RespawnDelay.Text, 0, int.MaxValue, out int seconds)
                && TryRange(RespawnChance.Text, 0, 100, out int probability) =>
                new BatchRequest("respawn", targets, RespawnSeconds: seconds, RespawnChance: probability),
            _ => null
        };
        if (request == null)
            BatchStatus.Text = "Select the setting or item and enter values within its permitted range.";
        return request != null;
    }

    private static bool TryRange(string text, int min, int max, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

    private async void ApplyBatch_Click(object sender, RoutedEventArgs e)
    {
        if (preview is not { PlanId: Guid planId } ready || ready.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            preview = null;
            BatchStatus.Text = "Preview expired. Run a new preview before applying.";
            RefreshActions();
            return;
        }

        var answer = MessageBox.Show(this,
            $"Apply the reviewed changes to {ready.Rows.Length} targets?\n{ready.Scope}\nEach NPC or spawn has its own transaction. Review the result for each target.",
            "Confirm NPC batch", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK)
            return;

        await RunAsync(async () =>
        {
            BatchStatus.Text = "Applying the reviewed batch…";
            BatchResult result = await api.PostAsync<BatchResult>("batch/apply", new { planId }, BatchRequestTimeout);
            preview = null;
            ShowResult(result, undo: false);
            lastBatch = result.CanUndo ? result.BatchId : null;
        });
    }

    private async void UndoBatch_Click(object sender, RoutedEventArgs e)
    {
        if (lastBatch is not Guid batchId)
            return;

        await RunAsync(async () =>
        {
            preview = null;
            BatchStatus.Text = "Undoing the applied batch…";
            BatchResult result = await api.PostAsync<BatchResult>("batch/undo", new { batchId }, BatchRequestTimeout);
            ShowResult(result, undo: true);
            if (!result.CanUndo)
                lastBatch = null;
        });
    }

    private void ShowResult(BatchResult result, bool undo)
    {
        Rows.ItemsSource = result.Rows;
        int succeeded = result.Rows.Count(row => row.Applied == true);
        string action = undo ? "undone" : "applied";
        LastOutcome = BatchStatus.Text = $"{succeeded} of {result.Rows.Length} {action}. {result.TransactionBoundary}";
    }

    private void Row_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var row = Rows.SelectedItem as BatchRow;
        RowDetails.Text = row == null ? "" : string.Join(Environment.NewLine, row.Identity.ToString(),
            row.Definition, $"Before  {row.Before}", $"After  {row.After}", row.Result);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy)
            return;

        busy = true;
        RefreshActions();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Unauthorized = ex is EditorApiException { Status: HttpStatusCode.Unauthorized };
            BatchStatus.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshActions();
        }
    }

    private void RefreshActions()
    {
        DraftFields.IsEnabled = PreviewBatch.IsEnabled = !busy && !Unauthorized;
        ApplyBatch.IsEnabled = !busy && !Unauthorized && preview?.PlanId != null;
        UndoBatch.IsEnabled = !busy && !Unauthorized && lastBatch.HasValue;
        CloseBatch.IsEnabled = !busy;
    }

    private sealed record BatchOperation(string Code, string Label);
    private sealed record StatChoice(int Id, string Label);
}

internal sealed record BatchRequest(string Operation, NpcIdentity[] Targets, int? StatId = null, int? Value = null,
    int? ItemId = null, int? Quality = null, LootEntry? LootEntry = null, int? RespawnSeconds = null, int? RespawnChance = null);

internal sealed record BatchPreview(Guid? PlanId, string Scope, BatchRow[] Rows, DateTimeOffset? ExpiresAt);

internal sealed record BatchResult(Guid? BatchId, BatchRow[] Rows, string TransactionBoundary, bool CanUndo);

internal sealed record BatchRow(NpcIdentity Identity, string Name, string Definition, string Before, string After,
    string? Error, bool? Applied)
{
    public string Result => Error ?? (Applied is bool applied ? applied ? "Completed" : "Unchanged"
        : Before.Length == 0 && After.Length == 0 ? "Pending preview" : "Validated");
}
