using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class NamedLootWindow : Window
{
    private readonly EditorApi api;
    private readonly bool allowSave;
    private readonly ObservableCollection<NamedLootGroupDraft> groups = [];
    private NamedLootRegistry? registry;
    private NamedLootTable? originalTable;
    private bool hasTableDraft;
    private bool busy;
    private bool settingTableSelection;

    internal NamedLootWindow(EditorApi api, bool allowSave)
    {
        InitializeComponent();
        this.api = api;
        this.allowSave = allowSave;
        Groups.ItemsSource = groups;
        TableCategory.ItemsSource = new[] { "ordinaryNpc", "family", "namedBoss", "dynaBoss", "dungeon",
            "mission", "raid", "quest", "event", "global" };
        GroupMode.ItemsSource = new[] { "INDEPENDENT", "WEIGHTED_ONE", "WEIGHTED_MANY", "GUARANTEED_ALL" };
        EntryEvidence.ItemsSource = new[] { "", "OBSERVED_AVAILABLE_LOOT",
            "COMMUNITY_DOCUMENTED", "INFERRED" };
        SetupAssignmentInputs();
        Loaded += async (_, _) => await RunAsync(LoadRegistryAsync);
        Closing += (_, args) => args.Cancel = busy || !ConfirmDiscardDrafts();
        RefreshActions();
    }

    internal bool Unauthorized { get; private set; }
    internal bool Changed { get; private set; }
    private bool CanSave => !busy && allowSave && !Unauthorized && registry is { UnavailableReason: null };

    private async Task LoadRegistryAsync()
    {
        NamedLootRegistry next = await api.GetAsync<NamedLootRegistry>("loot-tables");
        RenderRegistry(next, originalTable?.Id, originalAssignment?.Id);
        NamedLootStatus.Text = next.UnavailableReason ?? $"Registry revision {next.Revision} · {next.Tables.Length} tables"
            + (allowSave ? "" : " · Read-only session");
    }

    private void RenderRegistry(NamedLootRegistry next, string? tableId, string? assignmentId, bool keepTableDraft = false,
        bool keepAssignmentDraft = false)
    {
        registry = next;
        RegistryHistory.ItemsSource = next.History.OrderByDescending(row => row.Revision).ToArray();
        RenderTableList(tableId);
        if (!keepTableDraft)
        {
            NamedLootTable? table = next.Tables.FirstOrDefault(row => row.Id == tableId);
            RenderTable(table);
        }
        else
        {
            RenderReferences();
        }
        RenderAssignmentList(assignmentId, keepAssignmentDraft);
        RefreshActions();
    }

    private void TableQuery_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            RenderTableList(originalTable?.Id);
    }

    private void RenderTableList(string? selectedId)
    {
        if (registry == null)
            return;
        string query = TableQuery.Text.Trim();
        NamedLootTable[] matches = registry.Tables.Where(table => table.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || table.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || table.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        settingTableSelection = true;
        try
        {
            Tables.ItemsSource = matches;
            Tables.SelectedItem = matches.FirstOrDefault(table => table.Id == selectedId);
        }
        finally
        {
            settingTableSelection = false;
        }
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || settingTableSelection || Tables.SelectedItem is not NamedLootTable table)
            return;
        if (table.Id == originalTable?.Id)
            return;
        if (!ConfirmDiscardTableDraft())
        {
            RenderTableList(originalTable?.Id);
            return;
        }
        RenderTable(table);
    }

    private void RenderTable(NamedLootTable? table, bool draftNew = false)
    {
        originalTable = draftNew ? null : table;
        hasTableDraft = table != null || draftNew;
        TableId.Text = originalTable?.Id ?? "";
        TableName.Text = table == null ? "" : draftNew ? table.Name + " copy" : table.Name;
        TableCategory.SelectedItem = table?.Category;
        TableDescription.Text = table?.Description ?? "";
        TableEvidence.Text = table?.EvidenceReference ?? "";
        groups.Clear();
        foreach (NamedLootGroup group in table?.Groups ?? [])
            groups.Add(new NamedLootGroupDraft(group));
        Groups.SelectedItem = groups.FirstOrDefault();
        RenderReferences();
        RefreshActions();
    }

    private void RenderReferences()
    {
        if (originalTable == null)
        {
            TableReferences.Text = hasTableDraft ? "New table · no saved assignments" : "";
            return;
        }
        var references = ReferencesTo(originalTable.Id);
        TableReferences.Text = references.Length == 0 ? "No assignment references" : string.Join(Environment.NewLine,
            new[] { $"{references.Length} assignment reference(s)" }.Concat(references.Select(DescribeReference)));
    }

    private static string DescribeReference(NamedLootAssignment assignment)
    {
        string target = assignment.Scope switch
        {
            "family" => assignment.FamilyHash ?? "",
            "template" or "namedBoss" => assignment.TemplateHash ?? "",
            "templateBand" => $"{assignment.TemplateHash} / band {assignment.BandIndex}",
            "playfield" => $"PF {assignment.PlayfieldId}",
            "authoredPoint" => $"PF {assignment.PlayfieldId} / authored point {assignment.AuthoredIndex}",
            _ => ""
        };
        return $"{assignment.Id} · {assignment.Scope} {target}".TrimEnd();
    }

    private NamedLootAssignment[] ReferencesTo(string id) => (registry?.Assignments ?? [])
        .Where(assignment => assignment.TableId == id || assignment.TargetGroup?.StartsWith(id + "/", StringComparison.Ordinal) == true)
        .ToArray();

    private void NewTable_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || !ConfirmDiscardTableDraft())
            return;
        RenderTableList(null);
        RenderTable(null, draftNew: true);
        Views.SelectedItem = DefinitionTab;
        TableId.Focus();
    }

    private void CopyTable_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || originalTable == null)
            return;

        try
        {
            NamedLootTable copy = BuildTable();
            RenderTableList(null);
            RenderTable(copy, draftNew: true);
            Views.SelectedItem = DefinitionTab;
            TableId.Focus();
        }
        catch (ArgumentException ex)
        {
            NamedLootStatus.Text = ex.Message;
        }
    }

    private async void SaveTable_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || !hasTableDraft)
            return;
        await RunAsync(async () =>
        {
            NamedLootTable table = BuildTable();
            if (originalTable == null && registry!.Tables.Any(row => row.Id == table.Id))
                throw new ArgumentException("A table already uses that ID. Choose another ID for the new table.");
            var references = ReferencesTo(table.Id);
            if (references.Length > 0 && !Confirm(
                $"Save changes to {table.Id}?\n{PreviewReferences(references)}\nThese assignments use the saved definition on the next eligible death."))
                return;
            NamedLootRegistry next = await api.PostAsync<NamedLootRegistry>("loot-tables/save",
                new { expectedRevision = registry!.Revision, table });
            RenderRegistry(next, table.Id, originalAssignment?.Id, keepAssignmentDraft: true);
            SetSaved(next, $"Saved {table.Id}.");
        });
    }

    private NamedLootTable BuildTable() => new(NamedLootConditionDraft.Required(TableId.Text, "the table ID"),
        NamedLootConditionDraft.Required(TableName.Text, "the display name"), TableDescription.Text.Trim(),
        NamedLootConditionDraft.Required(TableCategory.SelectedItem as string ?? "", "a table category"),
        groups.Select(group => group.Build()).ToArray(), NamedLootConditionDraft.Optional(TableEvidence.Text));

    private async void DeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || originalTable == null)
            return;
        string id = originalTable.Id;
        var references = ReferencesTo(id);
        if (references.Length > 0)
        {
            NamedLootStatus.Text = $"Remove or reassign the {references.Length} references to {id} before deleting it.";
            return;
        }
        if (!Confirm($"Delete named loot table {id}?"))
            return;
        await RunAsync(async () =>
        {
            NamedLootRegistry next = await api.PostAsync<NamedLootRegistry>("loot-tables/delete",
                new { expectedRevision = registry!.Revision, id });
            RenderRegistry(next, null, originalAssignment?.Id, keepAssignmentDraft: true);
            SetSaved(next, $"Deleted {id}.");
        });
    }

    private void Group_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var group = Groups.SelectedItem as NamedLootGroupDraft;
        GroupFields.DataContext = group;
        Entries.ItemsSource = group?.Entries;
        Entries.SelectedItem = group?.Entries.FirstOrDefault();
        EntryFields.DataContext = Entries.SelectedItem;
        RefreshActions();
    }

    private void GroupMode_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void Entry_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        EntryFields.DataContext = Entries.SelectedItem;
        RefreshActions();
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || !hasTableDraft)
            return;
        var draft = new NamedLootGroupDraft { Id = NextId(groups.Select(group => group.Id), "group") };
        groups.Add(draft);
        Groups.SelectedItem = draft;
    }

    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (CanSave && Groups.SelectedItem is NamedLootGroupDraft draft
            && Confirm($"Remove group {draft.Id} and its entries from the table draft?"))
            groups.Remove(draft);
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || Groups.SelectedItem is not NamedLootGroupDraft group)
            return;
        var draft = new NamedLootEntryDraft { Id = NextId(group.Entries.Select(entry => entry.Id), "entry") };
        group.Entries.Add(draft);
        Entries.SelectedItem = draft;
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (CanSave && Groups.SelectedItem is NamedLootGroupDraft group && Entries.SelectedItem is NamedLootEntryDraft entry)
            group.Entries.Remove(entry);
    }

    private static string NextId(IEnumerable<string> ids, string prefix)
    {
        var used = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        int sequence = 1;
        while (used.Contains(FormattableString.Invariant($"{prefix}-{sequence}")))
            sequence++;
        return FormattableString.Invariant($"{prefix}-{sequence}");
    }

    private void PickEntryItem_Click(object sender, RoutedEventArgs e) => PickEntryItem(highOnly: false);
    private void PickHighItem_Click(object sender, RoutedEventArgs e) => PickEntryItem(highOnly: true);

    private void PickEntryItem(bool highOnly)
    {
        if (!CanSave || Entries.SelectedItem is not NamedLootEntryDraft entry)
            return;
        var dialog = new ItemPicker(api, highOnly ? "Choose high RDB template" : "Choose RDB loot item") { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Selected is not { } item)
            return;
        string id = NamedLootConditionDraft.NumberText(item.Id);
        string quality = NamedLootConditionDraft.NumberText(item.Quality);
        if (!highOnly)
        {
            entry.LowId = entry.HighId = id;
            entry.MinQuality = entry.MaxQuality = quality;
        }
        else
        {
            entry.HighId = id;
            entry.MaxQuality = quality;
        }
        NamedLootStatus.Text = item.Preview;
    }

    private async void RefreshRegistry_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmDiscardDrafts())
            await RunAsync(LoadRegistryAsync);
    }

    private void RegistryHistory_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private async void RestoreRegistry_Click(object sender, RoutedEventArgs e)
    {
        if (RegistryHistory.SelectedItem is NamedLootRevision row)
            await RestoreRegistryAsync(row.Revision);
    }

    private async void RestorePreviousRegistry_Click(object sender, RoutedEventArgs e)
    {
        if (registry is { Revision: > 0 } current)
            await RestoreRegistryAsync(current.Revision - 1);
    }

    private async Task RestoreRegistryAsync(int revision)
    {
        if (!CanSave || revision >= registry!.Revision || !ConfirmDiscardDrafts()
            || !Confirm($"Restore the entire named loot registry to revision {revision}?\nAll tables and assignments in that revision will become current."))
            return;
        await RunAsync(async () =>
        {
            NamedLootRegistry next = await api.PostAsync<NamedLootRegistry>("loot-tables/restore",
                new { expectedRevision = registry.Revision, revision });
            RenderRegistry(next, originalTable?.Id, originalAssignment?.Id);
            SetSaved(next, $"Restored registry revision {revision}.");
        });
    }

    private bool ConfirmDiscardDrafts() => ConfirmDiscardTableDraft() && ConfirmDiscardAssignmentDraft();

    private bool ConfirmDiscardTableDraft() => !TableDraftChanged() || Confirm("Discard unsaved table edits?");

    private bool TableDraftChanged()
    {
        if (!hasTableDraft)
            return false;
        if (originalTable == null)
            return TableId.Text.Length > 0 || TableName.Text.Length > 0 || TableDescription.Text.Length > 0
                || TableEvidence.Text.Length > 0 || groups.Count > 0;
        try
        {
            return JsonSerializer.Serialize(BuildTable()) != JsonSerializer.Serialize(originalTable);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private string PreviewReferences(NamedLootAssignment[] references) =>
        string.Join(Environment.NewLine, new[] { $"{references.Length} assignment reference(s)" }
            .Concat(references.Take(10).Select(DescribeReference))
            .Concat(references.Length > 10 ? [$"And {references.Length - 10} more in the Definition tab."] : []));

    private bool Confirm(string message) => MessageBox.Show(this, message, "Named loot tables", MessageBoxButton.OKCancel,
        MessageBoxImage.Question) == MessageBoxResult.OK;

    private void SetSaved(NamedLootRegistry next, string message)
    {
        Changed = true;
        NamedLootStatus.Text = $"{message} Registry revision {next.Revision}.";
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (busy)
            return;
        busy = true;
        RefreshActions();
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Unauthorized = ex is EditorApiException { Status: HttpStatusCode.Unauthorized };
            NamedLootStatus.Text = MainWindow.DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshActions();
        }
    }

    private void RefreshActions()
    {
        if (!IsLoaded)
            return;
        bool hasGroup = Groups.SelectedItem is NamedLootGroupDraft;
        bool hasEntry = Entries.SelectedItem is NamedLootEntryDraft;
        TableQuery.IsEnabled = Tables.IsEnabled = !busy;
        Views.IsEnabled = !busy;
        NewTable.IsEnabled = CanSave;
        CopyTable.IsEnabled = DeleteTable.IsEnabled = CanSave && originalTable != null;
        SaveTable.IsEnabled = CanSave && hasTableDraft;
        TableFields.IsEnabled = CanSave && hasTableDraft;
        TableId.IsReadOnly = originalTable != null;
        GroupFields.IsEnabled = CanSave && hasGroup;
        EntryFields.IsEnabled = CanSave && hasEntry;
        string? mode = GroupMode.SelectedItem as string;
        bool weighted = mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
        GroupRollCount.IsEnabled = mode != "WEIGHTED_ONE";
        GroupEmptyWeight.IsEnabled = EntryWeight.IsEnabled = weighted;
        EntryChance.IsEnabled = mode == "INDEPENDENT";
        AddGroup.IsEnabled = CanSave && hasTableDraft;
        RemoveGroup.IsEnabled = AddEntry.IsEnabled = CanSave && hasGroup;
        RemoveEntry.IsEnabled = CanSave && hasEntry;
        RestoreRegistry.IsEnabled = CanSave && RegistryHistory.SelectedItem is NamedLootRevision revision && revision.Revision < registry!.Revision;
        RestorePreviousRegistry.IsEnabled = CanSave && registry!.Revision > 0;
        RefreshRegistry.IsEnabled = !busy && !Unauthorized;
        CloseDialog.IsEnabled = !busy;
        RefreshAssignmentActions();
    }
}
