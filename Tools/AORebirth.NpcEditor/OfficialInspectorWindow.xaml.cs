using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AORebirth.NpcEditor.Official;

namespace AORebirth.NpcEditor;

public partial class OfficialInspectorWindow : Window
{
    private const int MaxObservationSets = 32;
    private const int MaxLootPerSet = 128;
    private const string AllCategories = "All properties";
    private readonly OfficialSnapshotReader reader = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<ObservationKey, LootCapture> lootCaptures = new();
    private OfficialSessionChoice[] sessionChoices = [];
    private OfficialSessionKey? selectedSession;
    private OfficialSelectionLock? targetLock;
    private OfficialSnapshot? activeSnapshot;
    private OfficialSnapshot? lockedSnapshot;
    private OfficialPropertyRow[] propertyRows = [];
    private OfficialEventRow[] eventRows = [];
    private LootSetChoice[] lootChoices = [];
    private ObservationKey? explicitLootSet;
    private int readRevision;
    private bool updatingSessions;
    private bool updatingLootSets;
    private bool reading;
    private bool exporting;
    private bool closed;

    public OfficialInspectorWindow()
    {
        InitializeComponent();
        PropertyCategory.ItemsSource = new[] { AllCategories };
        PropertyCategory.SelectedIndex = 0;
        timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            timer.Start();
            await RefreshAsync();
        };
        Closing += (_, e) =>
        {
            e.Cancel = exporting || (lootCaptures.Values.Any(capture => capture.HasUnexported)
                && MessageBox.Show(this, "Close with unexported loot observations?", "Official AO observations",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK);
        };
        Closed += (_, _) =>
        {
            closed = true;
            timer.Stop();
        };
        RefreshActions();
    }

    private async void ReadSelected_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (reading || closed)
            return;

        reading = true;
        int revision = readRevision;
        OfficialSessionKey? session = selectedSession;
        OfficialSelectionLock? locked = targetLock;
        RefreshActions();
        try
        {
            OfficialReadResult result = await Task.Run(() => reader.Read(session, locked));
            if (closed || revision != readRevision)
                return;

            if (selectedSession == null)
                selectedSession = result.ActiveSession;
            UpdateSessions(result.Sessions);
            OfficialSnapshot? snapshot = result.LockInvalidated ? null : result.Snapshot;
            if (snapshot != null && targetLock != null)
                lockedSnapshot = snapshot;
            RenderSnapshot(snapshot);
            StatusText.Text = result.Message;
        }
        catch (Exception ex)
        {
            if (!closed && revision == readRevision)
            {
                RenderSnapshot(null);
                StatusText.Text = $"Could not read the local observation bridge. {ex.Message}";
            }
        }
        finally
        {
            reading = false;
            if (!closed)
                RefreshActions();
        }
    }

    private void UpdateSessions(OfficialSessionChoice[] next)
    {
        bool changed = sessionChoices.Length != next.Length || sessionChoices.Zip(next)
            .Any(pair => !Equals(pair.First.Key, pair.Second.Key) || pair.First.Label != pair.Second.Label);
        updatingSessions = true;
        try
        {
            if (changed)
            {
                sessionChoices = next;
                SessionSelector.ItemsSource = sessionChoices;
            }
            OfficialSessionChoice? selected = sessionChoices.FirstOrDefault(choice => Equals(choice.Key, selectedSession));
            if (!ReferenceEquals(SessionSelector.SelectedItem, selected))
                SessionSelector.SelectedItem = selected;
        }
        finally
        {
            updatingSessions = false;
        }
    }

    private async void Session_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingSessions || !IsLoaded || targetLock != null
            || SessionSelector.SelectedItem is not OfficialSessionChoice selected)
            return;

        selectedSession = selected.Key;
        readRevision++;
        explicitLootSet = null;
        RenderSnapshot(null);
        StatusText.Text = "Reading the selected local session…";
        await RefreshAsync();
    }

    private async void LockTarget_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (LockTarget.IsChecked == true)
        {
            if (activeSnapshot == null)
                return;
            targetLock = OfficialSelectionLock.From(activeSnapshot);
            lockedSnapshot = activeSnapshot;
        }
        else
        {
            targetLock = null;
            lockedSnapshot = null;
            RenderSnapshot(null);
        }
        readRevision++;
        RefreshActions();
        await RefreshAsync();
    }

    private void RenderSnapshot(OfficialSnapshot? snapshot)
    {
        activeSnapshot = snapshot;
        if (snapshot != null)
        {
            TargetHeading.Text = snapshot.Name ?? "UNKNOWN";
            TargetInfo.Text = $"{DescribeNpc(snapshot)} · Captured {Utc(snapshot.CapturedUtcTicks):u}";
        }
        else
        {
            TargetHeading.Text = targetLock == null ? "Waiting for a selected NPC" : "Locked target unavailable";
            TargetInfo.Text = lockedSnapshot == null ? "" : $"Last locked capture · {DescribeNpc(lockedSnapshot)}";
        }

        OfficialPropertyRow[] properties = snapshot == null ? [] : ReadProperties(snapshot);
        if (!propertyRows.SequenceEqual(properties))
        {
            propertyRows = properties;
            RefreshPropertyCategories();
        }
        OfficialEventRow[] events = snapshot?.Events.Select(row => new OfficialEventRow(
            Utc(row.CapturedUtcTicks), row.Kind, row.Detail, row.Source, row.Classification, row.Confidence ?? "UNKNOWN")).ToArray() ?? [];
        if (!eventRows.SequenceEqual(events))
        {
            eventRows = events;
            Events.ItemsSource = eventRows;
        }
        RefreshLootChoices();
        RefreshActions();
    }

    private static string DescribeNpc(OfficialSnapshot snapshot) =>
        $"PF {snapshot.PlayfieldId} · {snapshot.IdentityType}:{snapshot.InstanceId} · "
        + $"MonsterData {KnownNumber(snapshot.MonsterData)} · Level {KnownNumber(snapshot.Level)}";

    private static string KnownNumber(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? "UNKNOWN";

    private static DateTime Utc(long ticks) => new(ticks, DateTimeKind.Utc);

    private static OfficialPropertyRow[] ReadProperties(OfficialSnapshot snapshot)
    {
        var rows = snapshot.Fields.Select(field =>
        {
            string value = field.Value ?? (field.Classification == "NOT_AVAILABLE" ? "NOT EXPOSED" : "UNKNOWN");
            if (field.Value != null && !string.IsNullOrWhiteSpace(field.Unit))
                value += " " + field.Unit;
            return new OfficialPropertyRow(field.Key, PropertyCategoryName(field.Key), PropertyLabel(field.Key),
                value, field.Classification, field.Source, field.Confidence ?? "UNKNOWN");
        }).ToList();
        foreach (string key in ExpectedProperties)
        {
            if (!rows.Any(row => row.Key == key))
                rows.Add(new OfficialPropertyRow(key, PropertyCategoryName(key), PropertyLabel(key),
                    "UNKNOWN", "NOT_AVAILABLE", "No field in this snapshot", "UNKNOWN"));
        }
        return rows.OrderBy(row => CategoryOrder(row.Category)).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static readonly string[] ExpectedProperties =
    [
        "identity.name", "identity.type", "identity.instance", "identity.playfield", "identity.monster_data", "identity.level", "identity.family",
        "position.x", "position.y", "position.z", "movement.state", "movement.velocity", "movement.patrol",
        "health.current", "health.maximum", "health.percent", "nano.current", "nano.maximum", "nano.percent", "nano.buffs", "nano.casting",
        "stats.attack_rating", "stats.armor_classes", "stats.evades",
        "equipment.weapons", "equipment.armor", "equipment.appearance", "equipment.template_ids",
        "combat.is_alive", "combat.is_attacking", "combat.damage", "combat.animation", "combat.timing", "loot.contents"
    ];

    private static string PropertyCategoryName(string key) => key.Split('.', 2)[0] switch
    {
        "identity" => "Identity",
        "health" or "nano" => "Health and nano",
        "stats" or "stat" => "Character stats",
        "position" or "movement" => "Movement",
        "equipment" or "appearance" => "Equipment",
        "combat" => "Combat",
        "loot" => "Loot",
        _ => "Other"
    };

    private static int CategoryOrder(string category) => category switch
    {
        "Identity" => 0, "Health and nano" => 1, "Character stats" => 2, "Equipment" => 3,
        "Combat" => 4, "Movement" => 5, "Loot" => 6, _ => 7
    };

    private static string PropertyLabel(string key) => key switch
    {
        "identity.monster_data" => "MonsterData",
        "equipment.template_ids" => "Item template IDs",
        "nano.buffs" => "Active nano effects",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('.', ' ').Replace('_', ' '))
    };

    private void RefreshPropertyCategories()
    {
        string? old = PropertyCategory.SelectedItem as string;
        string[] categories = [AllCategories, .. propertyRows.Select(row => row.Category).Distinct()];
        if (PropertyCategory.ItemsSource is not string[] current || !current.SequenceEqual(categories))
        {
            PropertyCategory.ItemsSource = categories;
            PropertyCategory.SelectedItem = categories.Contains(old) ? old : AllCategories;
        }
        RefreshPropertyGrid();
    }

    private void PropertyFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            RefreshPropertyGrid();
    }

    private void RefreshPropertyGrid()
    {
        string? category = PropertyCategory.SelectedItem as string;
        string search = PropertySearch.Text.Trim();
        Properties.ItemsSource = propertyRows.Where(row => (category == null || category == AllCategories || row.Category == category)
            && (row.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || row.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Source.Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private OfficialSnapshot? CurrentLootSource => activeSnapshot ?? lockedSnapshot;

    private OfficialSnapshot? SelectedLootSource()
    {
        if (LootSets.SelectedItem is not LootSetChoice choice)
            return null;
        if (CurrentLootSource is { } snapshot && ObservationKey.From(snapshot) == choice.Key)
            return snapshot;
        return lootCaptures.GetValueOrDefault(choice.Key)?.Snapshot;
    }

    private void RefreshLootChoices()
    {
        var choices = new List<LootSetChoice>();
        if (CurrentLootSource is { } current)
        {
            string status = activeSnapshot == null ? "Historical locked capture" : "Selected NPC";
            choices.Add(new LootSetChoice(ObservationKey.From(current), $"{status} · {current.Name ?? "UNKNOWN"} · {DescribeNpc(current)}"));
        }
        foreach (var item in lootCaptures.OrderByDescending(pair => pair.Value.Snapshot.CapturedUtcTicks))
        {
            if (choices.Any(choice => choice.Key.Equals(item.Key)))
                continue;
            OfficialSnapshot capture = item.Value.Snapshot;
            choices.Add(new LootSetChoice(item.Key, $"Recorded · {capture.Name ?? "UNKNOWN"} · {DescribeNpc(capture)} · {Utc(capture.CapturedUtcTicks):u}"));
        }
        if (!lootChoices.SequenceEqual(choices))
        {
            lootChoices = choices.ToArray();
            updatingLootSets = true;
            try
            {
                LootSets.ItemsSource = lootChoices;
                LootSets.SelectedItem = lootChoices.FirstOrDefault(row => Equals(row.Key, explicitLootSet)) ?? lootChoices.FirstOrDefault();
            }
            finally
            {
                updatingLootSets = false;
            }
            RefreshLootRows();
        }
    }

    private void LootSet_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || updatingLootSets)
            return;
        explicitLootSet = (LootSets.SelectedItem as LootSetChoice)?.Key;
        RefreshLootRows();
    }

    private void RefreshLootRows()
    {
        var choice = LootSets.SelectedItem as LootSetChoice;
        LootCapture? capture = choice == null ? null : lootCaptures.GetValueOrDefault(choice.Key);
        LootObservations.ItemsSource = capture?.Rows.Select(row => new LootRowView(row)).ToArray();
        LootContext.Text = choice == null ? "Select an NPC before recording opened-corpse loot."
            : "Manually associated observations from opened corpses. Availability and drop rates remain unknown.";
        RefreshActions();
    }

    private void RecordLoot_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedLootSource() is not { } snapshot || CurrentLootSource is not { } current)
            return;
        ObservationKey key = ObservationKey.From(snapshot);
        if (key != ObservationKey.From(current) || exporting)
            return;
        if (!lootCaptures.TryGetValue(key, out LootCapture? capture) && lootCaptures.Count >= MaxObservationSets)
        {
            ShowAction("This session reached its local observation-set limit. Export and remove an existing set to record another.");
            return;
        }
        if (capture?.Rows.Count >= MaxLootPerSet)
        {
            ShowAction("This target reached its local observation limit. Export and remove existing rows to add another.");
            return;
        }

        string label = $"{(activeSnapshot == null ? "Historical locked capture" : "Observed NPC")} · "
            + $"{snapshot.Name ?? "UNKNOWN"} · {DescribeNpc(snapshot)} · {Utc(snapshot.CapturedUtcTicks):u}";
        var dialog = new OfficialLootRecordWindow(OfficialSelectionLock.From(snapshot), label) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Observation is not { } observation)
            return;
        if (capture == null)
        {
            capture = new LootCapture(snapshot);
            lootCaptures.Add(key, capture);
        }
        if (snapshot.Sequence > capture.Snapshot.Sequence)
            capture.Snapshot = snapshot;
        capture.Rows.Add(observation);
        capture.HasUnexported = true;
        explicitLootSet = key;
        RefreshLootChoices();
        RefreshLootRows();
        ShowAction("Added a manual opened-corpse observation to the local draft.");
    }

    private void RemoveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (exporting || LootSets.SelectedItem is not LootSetChoice choice
            || LootObservations.SelectedItem is not LootRowView row || !lootCaptures.TryGetValue(choice.Key, out LootCapture? capture))
            return;
        capture.Rows.Remove(row.Observation);
        if (capture.Rows.Count == 0)
        {
            lootCaptures.Remove(choice.Key);
            explicitLootSet = null;
        }
        else
        {
            capture.HasUnexported = true;
        }
        RefreshLootChoices();
        RefreshLootRows();
    }

    private void LootSelection_Changed(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void ObservationView_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == ObservationViews)
            RefreshActions();
    }

    private void CompareNpc_Click(object sender, RoutedEventArgs e)
    {
        if (activeSnapshot is { } snapshot)
            new OfficialNpcComparisonWindow(snapshot) { Owner = this }.ShowDialog();
    }

    private OfficialSnapshot? SnapshotToExport => ObservationViews.SelectedItem == LootView ? SelectedLootSource() : activeSnapshot;

    private async void ExportNpc_Click(object sender, RoutedEventArgs e)
    {
        if (exporting || SnapshotToExport is not { } snapshot)
            return;
        ObservationKey key = ObservationKey.From(snapshot);
        LootCapture? capture = lootCaptures.GetValueOrDefault(key);
        OfficialLootObservation[] observations = capture?.Rows.ToArray() ?? [];
        exporting = true;
        RefreshActions();
        try
        {
            OfficialExportPaths paths = await Task.Run(() => OfficialObservationExport.Write(snapshot, observations));
            if (capture != null)
                capture.HasUnexported = false;
            ShowAction($"JSON  {paths.JsonPath}{Environment.NewLine}CSV  {paths.CsvPath}");
        }
        catch (Exception ex)
        {
            ShowAction($"Could not export the local observations. {ex.Message}");
        }
        finally
        {
            exporting = false;
            RefreshActions();
        }
    }

    private void ShowAction(string text)
    {
        ExportStatus.Text = text;
        ExportStatus.Visibility = Visibility.Visible;
    }

    private void RefreshActions()
    {
        if (!IsLoaded)
            return;
        SessionSelector.IsEnabled = targetLock == null && sessionChoices.Length > 0;
        LockTarget.IsEnabled = activeSnapshot != null || targetLock != null;
        ReadSelected.IsEnabled = !reading;
        CompareNpc.IsEnabled = activeSnapshot != null;
        ExportNpc.IsEnabled = !exporting && SnapshotToExport != null;
        RecordLoot.IsEnabled = !exporting && CurrentLootSource is { } current
            && LootSets.SelectedItem is LootSetChoice choice && choice.Key == ObservationKey.From(current);
        RemoveLoot.IsEnabled = !exporting && LootObservations.SelectedItem is LootRowView;
    }

    private sealed record OfficialPropertyRow(string Key, string Category, string Name, string Value, string Classification,
        string Source, string Confidence);

    private sealed record OfficialEventRow(DateTime CapturedUtc, string Kind, string Detail, string Source, string Classification, string Confidence);

    private sealed record ObservationKey(int ProcessId, string SessionId, string? SelectionId, int PlayfieldId, int IdentityType, int InstanceId)
    {
        public static ObservationKey From(OfficialSnapshot snapshot) => new(snapshot.ProcessId, snapshot.SessionId, snapshot.SelectionId,
            snapshot.PlayfieldId, snapshot.IdentityType, snapshot.InstanceId);
    }

    private sealed record LootSetChoice(ObservationKey Key, string Label);

    private sealed class LootCapture(OfficialSnapshot snapshot)
    {
        public OfficialSnapshot Snapshot { get; set; } = snapshot;
        public List<OfficialLootObservation> Rows { get; } = [];
        public bool HasUnexported { get; set; }
    }

    private sealed record LootRowView(OfficialLootObservation Observation)
    {
        public DateTime CapturedUtc => Utc(Observation.CapturedUtcTicks);
        public string Status => Observation.Status;
        public string? CorpseIdentity => Observation.CorpseIdentity;
        public string? ItemName => Observation.ItemName;
        public string ItemIds => Observation.LowId == Observation.HighId ? KnownNumber(Observation.LowId)
            : $"{KnownNumber(Observation.LowId)}/{KnownNumber(Observation.HighId)}";
        public int? Quality => Observation.Quality;
        public int? Quantity => Observation.Quantity;
        public int? Credits => Observation.Credits;
        public string? Notes => Observation.Notes;
    }
}
