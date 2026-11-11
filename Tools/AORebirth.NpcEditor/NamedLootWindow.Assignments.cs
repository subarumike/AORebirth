using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class NamedLootWindow
{
    private NamedLootAssignment? originalAssignment;
    private NamedLootAssignmentDraft? assignmentDraft;
    private bool settingAssignmentSelection;

    private void SetupAssignmentInputs()
    {
        AssignmentScope.ItemsSource = new[] { "global", "family", "template", "templateBand", "playfield", "authoredPoint", "namedBoss" };
        AssignmentMerge.ItemsSource = new[] { "append", "replace", "suppress" };
    }

    private void RenderAssignmentList(string? selectedId, bool preserveDraft)
    {
        settingAssignmentSelection = true;
        try
        {
            NamedLootAssignment? selected = registry?.Assignments.FirstOrDefault(row => row.Id == selectedId);
            string? priorTable = assignmentDraft?.TableId;
            Assignments.ItemsSource = registry?.Assignments;
            Assignments.SelectedItem = selected;
            AssignmentTable.ItemsSource = registry?.Tables.Select(table => table.Id).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!preserveDraft)
                RenderAssignment(selected);
            else if (priorTable != null)
                AssignmentTable.Text = priorTable;
        }
        finally
        {
            settingAssignmentSelection = false;
        }
    }

    private void RenderAssignment(NamedLootAssignment? assignment, bool isNew = false)
    {
        originalAssignment = assignment;
        assignmentDraft = assignment == null && !isNew ? null : new NamedLootAssignmentDraft(assignment, originalTable?.Id);
        AssignmentFields.DataContext = assignmentDraft;
        RefreshAssignmentActions();
    }

    private void Assignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || settingAssignmentSelection || Assignments.SelectedItem is not NamedLootAssignment assignment
            || assignment.Id == originalAssignment?.Id)
            return;
        if (!ConfirmDiscardAssignmentDraft())
        {
            RenderAssignmentList(originalAssignment?.Id, preserveDraft: true);
            return;
        }
        RenderAssignment(assignment);
    }

    private void NewAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || !ConfirmDiscardAssignmentDraft())
            return;
        RenderAssignmentList(null, preserveDraft: false);
        RenderAssignment(null, isNew: true);
        AssignmentId.Focus();
    }

    private void AssignmentScope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            RefreshAssignmentActions();
    }

    private void ChooseAssignmentTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || assignmentDraft is not { } draft)
            return;
        var picker = new NamedLootTargetPicker(api, draft.Scope, draft.PlayfieldId) { Owner = this };
        if (picker.ShowDialog() != true)
            return;
        if (picker.Spawn is { } point)
        {
            draft.SetSpawn(point);
            AssignmentTargetStatus.Text = $"PF {point.PlayfieldId} · Authored point {point.AuthoredIndex} · {point.Hash}";
        }
        else if (picker.SelectedTemplate is { } template)
        {
            draft.TemplateHash = template.Hash;
            draft.BandIndex = NamedLootConditionDraft.NumberText(template.BandIndex);
            draft.FamilyHash = picker.Family ?? "";
            AssignmentTargetStatus.Text = $"{template.Name} · {template.Hash} · band {template.BandIndex}";
        }
    }

    private async void PreviewAssignment_Click(object sender, RoutedEventArgs e) => await EditAssignmentAsync(save: false);
    private async void SaveAssignment_Click(object sender, RoutedEventArgs e) => await EditAssignmentAsync(save: true);

    private async Task EditAssignmentAsync(bool save)
    {
        if (!CanSave || assignmentDraft == null)
            return;
        await RunAsync(async () =>
        {
            NamedLootAssignment assignment = assignmentDraft.Build();
            if (originalAssignment == null && registry!.Assignments.Any(row => row.Id == assignment.Id))
                throw new ArgumentException("An assignment already uses that ID. Choose another ID for the new assignment.");
            var request = new { expectedRevision = registry!.Revision, assignment };
            var impact = await api.PostAsync<NamedLootAssignmentImpact>("loot-tables/assignments/preview", request);
            string review = DescribeImpact(impact, assignment);
            if (!save)
            {
                MessageBox.Show(this, review, "Review named loot assignment", MessageBoxButton.OK, MessageBoxImage.Information);
                NamedLootStatus.Text = $"Validated assignment {assignment.Id}.";
                return;
            }
            if (!Confirm($"Save assignment {assignment.Id}?\n{review}"))
                return;
            NamedLootRegistry next = await api.PostAsync<NamedLootRegistry>("loot-tables/assignments/save", request);
            RenderRegistry(next, originalTable?.Id, assignment.Id, keepTableDraft: true);
            SetSaved(next, $"Saved assignment {assignment.Id}.");
        });
    }

    private async void DeleteAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave || originalAssignment is not { } assignment)
            return;
        await RunAsync(async () =>
        {
            int expectedRevision = registry!.Revision;
            var impact = await api.PostAsync<NamedLootAssignmentImpact>("loot-tables/assignments/preview",
                new { expectedRevision, assignment });
            if (!Confirm($"Delete assignment {assignment.Id}?\n{DescribeImpact(impact, assignment)}"))
                return;
            NamedLootRegistry next = await api.PostAsync<NamedLootRegistry>("loot-tables/assignments/delete",
                new { expectedRevision, id = assignment.Id });
            RenderRegistry(next, originalTable?.Id, null, keepTableDraft: true);
            SetSaved(next, $"Deleted assignment {assignment.Id}.");
        });
    }

    private static string DescribeImpact(NamedLootAssignmentImpact impact, NamedLootAssignment assignment) =>
        string.Join(Environment.NewLine, impact.Definition,
            impact.LoadedMatches is int count ? $"{count:N0} matching loaded NPC(s)" : "Matching loaded NPC count unavailable",
            $"{assignment.Merge} · priority {assignment.Priority} · {impact.AffectedTable ?? assignment.TargetGroup ?? assignment.TableId}",
            impact.Description);

    private bool ConfirmDiscardAssignmentDraft()
    {
        if (assignmentDraft == null)
            return true;
        if (originalAssignment == null && assignmentDraft.Id.Length == 0)
            return true;
        bool changed;
        try
        {
            changed = JsonSerializer.Serialize(assignmentDraft.Build()) != JsonSerializer.Serialize(originalAssignment);
        }
        catch (ArgumentException)
        {
            changed = true;
        }
        return !changed || Confirm("Discard unsaved assignment edits?");
    }

    private void RefreshAssignmentActions()
    {
        bool enabled = CanSave && assignmentDraft != null;
        string scope = AssignmentScope.SelectedItem as string ?? "";
        string merge = AssignmentMerge.SelectedItem as string ?? "";
        AssignmentFields.IsEnabled = enabled;
        AssignmentId.IsReadOnly = originalAssignment != null;
        AssignmentTargets.Visibility = scope == "global" ? Visibility.Collapsed : Visibility.Visible;
        FamilyHashInput.IsEnabled = scope == "family";
        TemplateHashInput.IsEnabled = scope is "template" or "templateBand" or "namedBoss";
        BandIndexInput.IsEnabled = scope == "templateBand";
        TargetPlayfieldInput.IsEnabled = scope is "playfield" or "authoredPoint";
        AuthoredIndexInput.IsReadOnly = true;
        ChooseAssignmentTarget.IsEnabled = scope is "family" or "template" or "templateBand" or "authoredPoint" or "namedBoss";
        AssignmentMergeFields.Visibility = merge is "replace" or "suppress" ? Visibility.Visible : Visibility.Collapsed;
        ReplacementGroupInput.IsEnabled = merge == "replace";
        AssignmentTable.IsEnabled = merge != "suppress";
        NewAssignment.IsEnabled = CanSave;
        PreviewAssignment.IsEnabled = SaveAssignment.IsEnabled = enabled;
        DeleteAssignment.IsEnabled = CanSave && originalAssignment != null;
        AssignmentTargetStatus.Text = scope == "authoredPoint"
            ? "Choose a point from the authored placement catalog to bind its stable signature."
            : "";
    }
}

internal sealed class NamedLootAssignmentDraft : NamedLootConditionDraft
{
    private string familyHash;
    private string templateHash;
    private string bandIndex;
    private string playfieldId;
    private string authoredIndex;
    private int? signedPlayfield;
    private int? signedIndex;
    private string? authoredSignature;

    internal NamedLootAssignmentDraft(NamedLootAssignment? assignment, string? defaultTable) : base(assignment?.Condition)
    {
        Id = assignment?.Id ?? "";
        Scope = assignment?.Scope ?? "template";
        Priority = NumberText(assignment?.Priority ?? 0);
        Merge = assignment?.Merge ?? "append";
        TableId = assignment?.TableId ?? defaultTable ?? "";
        familyHash = assignment?.FamilyHash ?? "";
        templateHash = assignment?.TemplateHash ?? "";
        bandIndex = NumberText(assignment?.BandIndex);
        playfieldId = NumberText(assignment?.PlayfieldId);
        authoredIndex = NumberText(assignment?.AuthoredIndex);
        authoredSignature = assignment?.AuthoredSignature;
        signedPlayfield = assignment?.PlayfieldId;
        signedIndex = assignment?.AuthoredIndex;
        TargetGroup = assignment?.TargetGroup ?? "";
        SourceGroupId = assignment?.SourceGroupId ?? "";
    }

    public string Id { get; set; }
    public string Scope { get; set; }
    public string Priority { get; set; }
    public string Merge { get; set; }
    public string TableId { get; set; }
    public string FamilyHash { get => familyHash; set => SetText(ref familyHash, value); }
    public string TemplateHash { get => templateHash; set => SetText(ref templateHash, value); }
    public string BandIndex { get => bandIndex; set => SetText(ref bandIndex, value); }
    public new string PlayfieldId { get => playfieldId; set => SetText(ref playfieldId, value); }
    public string AuthoredIndex { get => authoredIndex; set => SetText(ref authoredIndex, value); }
    public string TargetGroup { get; set; }
    public string SourceGroupId { get; set; }
    public string ConditionPlayfieldId { get => base.PlayfieldId; set => base.PlayfieldId = value; }

    internal void SetSpawn(NamedLootSpawnTarget point)
    {
        PlayfieldId = NumberText(point.PlayfieldId);
        AuthoredIndex = NumberText(point.AuthoredIndex);
        authoredSignature = point.AuthoredSignature;
        signedPlayfield = point.PlayfieldId;
        signedIndex = point.AuthoredIndex;
    }

    internal NamedLootAssignment Build()
    {
        int? playfield = Scope is "playfield" or "authoredPoint" ? Number(PlayfieldId, "playfield ID", 1) : null;
        int? index = Scope == "authoredPoint" ? Number(AuthoredIndex, "authored point index") : null;
        if (Scope == "authoredPoint" && (authoredSignature == null || signedPlayfield != playfield || signedIndex != index))
            throw new ArgumentException("Choose the authored spawn point to bind its placement signature.");
        return new(Required(Id, "the assignment ID"), Scope, Number(Priority, "priority", int.MinValue), Merge,
            Merge == "suppress" ? null : Required(TableId, "the table ID"),
            Scope == "family" ? Required(FamilyHash, "the family hash") : null,
            Scope is "template" or "templateBand" or "namedBoss" ? Required(TemplateHash, "the template hash") : null,
            Scope == "templateBand" ? Number(BandIndex, "band index") : null, playfield, index,
            Scope == "authoredPoint" ? authoredSignature : null,
            Merge is "replace" or "suppress" ? Required(TargetGroup, "the qualified target table/group") : null,
            Merge == "replace" ? Required(SourceGroupId, "the replacement group ID") : null, BuildCondition());
    }
}
