using System.Collections;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private SavedEquipmentState? savedEquipment;
    private EquipmentEdit? liveEquipmentDraft;
    private EquipmentSaveEdit? savedEquipmentDraft;
    private string? liveEquipmentDraftText;
    private string? savedEquipmentDraftText;

    private bool CanSaveEquipment => CanWrite && session?.CanSave == true && savedEquipment is { UnavailableReason: null };

    private void ClearEquipment()
    {
        liveEquipmentDraft = null;
        savedEquipmentDraft = null;
        liveEquipmentDraftText = null;
        savedEquipmentDraftText = null;
        savedEquipment = null;
        RenderSavedEquipment();
    }

    private async Task LoadSavedEquipmentAsync(NpcIdentity identity)
    {
        try
        {
            savedEquipment = await Api.GetAsync<SavedEquipmentState>(identity.Path + "/equipment");
            RenderSavedEquipment();
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            savedEquipment = null;
            RenderSavedEquipment();
            SavedEquipmentStatus.Text = DescribeError(ex);
        }
    }

    private void RenderSavedEquipment()
    {
        ConfiguredEquipmentGrid.ItemsSource = (IEnumerable?)savedEquipment?.Entries ?? detail?.ConfiguredEquipment;
        EquipmentHistory.ItemsSource = savedEquipment?.History;
        SavedEquipmentStatus.Text = savedEquipment switch
        {
            null => "",
            { UnavailableReason: string reason } => reason,
            _ => $"{savedEquipment.Definition} · revision {savedEquipment.Revision} · {FormatAffected(savedEquipment.EstimatedAffectedNpcs)}. Changes apply on future spawns."
        };
        RefreshControls();
    }

    private void Equipment_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshControls();

    private void ReplaceEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Replaceable: true } row
            && OpenCatalog("Choose live equipment replacement", category: row.ItemClass) is { } item)
        {
            liveEquipmentDraft = new EquipmentEdit(row.Slot, row.InstanceId, item.Id, item.Quality);
            liveEquipmentDraftText = $"Slot {row.Slot} · Replace {row.Name} with {item.Preview}";
            RefreshControls();
        }
    }

    private void RemoveEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Removable: true } row)
        {
            liveEquipmentDraft = new EquipmentEdit(row.Slot, row.InstanceId, null, null);
            liveEquipmentDraftText = $"Slot {row.Slot} · Remove {row.Name}";
            RefreshControls();
        }
    }

    private void AddArmor_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && detail?.CanAddArmor == true && OpenCatalog("Choose live armor", category: "Armor") is { } item)
        {
            liveEquipmentDraft = new EquipmentEdit(-1, 0, item.Id, item.Quality);
            liveEquipmentDraftText = $"Add {item.Preview}";
            RefreshControls();
        }
    }

    private async void ApplyEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && IsLiveEquipmentDraftCurrent() && liveEquipmentDraft is { } draft)
        {
            await ApplyLiveAsync("equipment", draft, "Applied equipment change to the selected NPC.", () =>
            {
                liveEquipmentDraft = null;
                liveEquipmentDraftText = null;
            });
        }
    }

    private void RevertEquipmentDraft_Click(object sender, RoutedEventArgs e)
    {
        liveEquipmentDraft = null;
        liveEquipmentDraftText = null;
        RefreshControls();
    }

    private void ReplaceSavedEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && savedEquipment is { } state
            && ConfiguredEquipmentGrid.SelectedItem is SavedEquipmentRow { Replaceable: true } row
            && OpenCatalog("Choose template equipment replacement", category: row.Role == "Armor" ? "Armor" : "Weapon") is { } item)
        {
            savedEquipmentDraft = new EquipmentSaveEdit(state.Revision, row.Index, row.LowId, row.HighId, item.Id, item.Quality);
            savedEquipmentDraftText = $"Row {row.Index} · Replace {row.Name} with {item.Preview}";
            RefreshControls();
        }
    }

    private void RemoveSavedEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && savedEquipment is { } state
            && ConfiguredEquipmentGrid.SelectedItem is SavedEquipmentRow { Removable: true } row)
        {
            savedEquipmentDraft = new EquipmentSaveEdit(state.Revision, row.Index, row.LowId, row.HighId, null, null);
            savedEquipmentDraftText = $"Row {row.Index} · Remove {row.Name}";
            RefreshControls();
        }
    }

    private void AddSavedArmor_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && savedEquipment is { CanAddArmor: true } state
            && OpenCatalog("Choose template armor", category: "Armor") is { } item)
        {
            savedEquipmentDraft = new EquipmentSaveEdit(state.Revision, -1, 0, 0, item.Id, item.Quality);
            savedEquipmentDraftText = $"Add {item.Preview}";
            RefreshControls();
        }
    }

    private async void SaveEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && savedEquipmentDraft is { } draft && draft.ExpectedRevision == savedEquipment!.Revision)
            await WriteSavedEquipmentAsync("equipment/save", draft);
    }

    private void RevertSavedEquipmentDraft_Click(object sender, RoutedEventArgs e)
    {
        savedEquipmentDraft = null;
        savedEquipmentDraftText = null;
        RefreshControls();
    }

    private void EquipmentHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var row = EquipmentHistory.SelectedItem as EquipmentHistoryRow;
        EquipmentHistoryDetails.Text = row == null ? "" : $"Before{Environment.NewLine}{FormatLoadout(row.Before)}"
            + $"{Environment.NewLine}{Environment.NewLine}After{Environment.NewLine}{FormatLoadout(row.After)}";
        RefreshControls();
    }

    private static string FormatLoadout(IEnumerable<SavedEquipmentRow> rows) =>
        string.Join(Environment.NewLine, rows.Select(row => $"{row.Name} · {row.LowId}/{row.HighId} · {row.Role}"));

    private async void RestoreEquipmentRevision_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && EquipmentHistory.SelectedItem is EquipmentHistoryRow row && row.Revision < savedEquipment!.Revision)
            await RestoreEquipmentAsync(row.Revision);
    }

    private async void RestorePreviousEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveEquipment && savedEquipment!.Revision > 0)
            await RestoreEquipmentAsync(savedEquipment.Revision - 1);
    }

    private Task RestoreEquipmentAsync(int revision) =>
        WriteSavedEquipmentAsync("equipment/restore", new { expectedRevision = savedEquipment!.Revision, revision });

    private async Task WriteSavedEquipmentAsync(string route, object payload)
    {
        SavedEquipmentState state = savedEquipment!;
        if (!ConfirmTemplateChange(state.Definition, state.EstimatedAffectedNpcs))
            return;

        string path = detail!.Npc.Identity.Path;
        await RunAsync(async () =>
        {
            savedEquipment = await Api.PostAsync<SavedEquipmentState>($"{path}/{route}", payload);
            savedEquipmentDraft = null;
            savedEquipmentDraftText = null;
            RenderSavedEquipment();
            Status.Text = $"Saved template equipment revision {savedEquipment.Revision} for future spawns.";
        });
    }

    private bool IsLiveEquipmentDraftCurrent() => liveEquipmentDraft is { } draft && detail is { } npc
        && (draft.Slot < 0 ? npc.CanAddArmor : npc.Equipment.Any(row => row.Slot == draft.Slot && row.InstanceId == draft.ExpectedInstanceId));

    private void RefreshEquipmentControls()
    {
        ReplaceEquipment.IsEnabled = CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Replaceable: true };
        RemoveEquipment.IsEnabled = CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Removable: true };
        AddArmor.IsEnabled = CanWrite && detail?.CanAddArmor == true;
        ApplyEquipment.IsEnabled = CanWrite && IsLiveEquipmentDraftCurrent();
        RevertEquipmentDraft.IsEnabled = liveEquipmentDraft != null;
        LiveEquipmentDraftStatus.Text = liveEquipmentDraft switch
        {
            null => "Lock the NPC to edit a supported slot.",
            _ when !IsLiveEquipmentDraftCurrent() => "The selected slot changed. Revert the draft and reselect the slot.",
            _ => liveEquipmentDraftText ?? ""
        };
        ReplaceSavedEquipment.IsEnabled = CanSaveEquipment && ConfiguredEquipmentGrid.SelectedItem is SavedEquipmentRow { Replaceable: true };
        RemoveSavedEquipment.IsEnabled = CanSaveEquipment && ConfiguredEquipmentGrid.SelectedItem is SavedEquipmentRow { Removable: true };
        AddSavedArmor.IsEnabled = CanSaveEquipment && savedEquipment?.CanAddArmor == true;
        SaveEquipment.IsEnabled = CanSaveEquipment && savedEquipmentDraft is { } draft && draft.ExpectedRevision == savedEquipment!.Revision;
        RevertSavedEquipmentDraft.IsEnabled = savedEquipmentDraft != null;
        RestorePreviousEquipment.IsEnabled = CanSaveEquipment && savedEquipment!.Revision > 0;
        RestoreEquipmentRevision.IsEnabled = CanSaveEquipment && EquipmentHistory.SelectedItem is EquipmentHistoryRow row
            && row.Revision < savedEquipment!.Revision;
        SavedEquipmentDraftStatus.Text = savedEquipmentDraft switch
        {
            null => "",
            _ when savedEquipmentDraft.ExpectedRevision != savedEquipment?.Revision => "The revision changed. Revert the draft and reselect the row.",
            _ => savedEquipmentDraftText ?? ""
        };
    }
}
