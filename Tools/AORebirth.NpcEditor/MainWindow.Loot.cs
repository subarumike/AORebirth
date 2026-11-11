using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private LootDetail? loot;
    private LootDraftState? lootDraft;
    private bool updatingLoot;

    private bool TemplateLootScope => LootTabs.SelectedItem == SavedLootTab;
    private bool CanSaveLoot => CanWrite && session?.CanSave == true && loot?.Saved.CanSave == true;
    private bool CanApplyLoot => CanWrite && loot?.Live?.CanApply == true;
    private bool CanEditLoot => TemplateLootScope ? CanSaveLoot : CanApplyLoot;

    private LootRow? SelectedLootRow => (TemplateLootScope ? SavedLoot.SelectedItem : EffectiveLoot.SelectedItem) as LootRow;

    private async Task LoadLootAsync(NpcIdentity identity)
    {
        try
        {
            RenderLoot(await Api.GetAsync<LootDetail>(identity.Path + "/loot"));
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            RenderLoot(null);
            EffectiveLootStatus.Text = DescribeError(ex);
        }
    }

    private void ClearLoot()
    {
        lootDraft = null;
        RenderLoot(null);
        SetLootDraft(null);
        LootSimulationGrid.ItemsSource = null;
        LootSimulationStatus.Text = "Simulated frequencies use the current live loot definition. No corpse or persistent item is created.";
    }

    private void RenderLoot(LootDetail? view)
    {
        var liveSelection = (EffectiveLoot.SelectedItem as LootRow)?.Edit;
        var savedSelection = (SavedLoot.SelectedItem as LootRow)?.Edit;
        loot = view;
        updatingLoot = true;
        try
        {
            EffectiveLoot.ItemsSource = view?.Rows;
            SavedLoot.ItemsSource = view?.ConfiguredRows;
            EffectiveLoot.SelectedItem = FindLootRow(view?.Rows, liveSelection);
            SavedLoot.SelectedItem = FindLootRow(view?.ConfiguredRows, savedSelection);
            LiveLootHistory.ItemsSource = view?.Live?.History;
            LootHistory.ItemsSource = view?.Saved.History;
        }
        finally
        {
            updatingLoot = false;
        }

        EffectiveLootStatus.Text = view == null ? "" :
            $"Potential loot for the selected NPC · {FormatHash(view.TemplateHash)} · {view.Rows.Length} rows";
        SavedLootStatus.Text = view?.Saved is { CanSave: true } saved
            ? $"{saved.Definition ?? saved.TemplateHash} · revision {saved.Revision} · {FormatAffected(saved.EstimatedAffectedNpcs)}. Changes apply on future spawns."
            : view?.Saved.UnavailableReason ?? "No writable template band for this NPC.";
        ShowLootRow(SelectedLootRow);
        if (lootDraft == null)
            SetLootDraft(SelectedLootRow);
        RefreshControls();
    }

    private static LootRow? FindLootRow(LootRow[]? rows, LootRowKey? key) =>
        key == null ? null : rows?.FirstOrDefault(row => row.Edit == key);

    private void LootTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != LootTabs)
            return;

        bool editTab = LootTabs.SelectedItem == EffectiveLootTab || TemplateLootScope;
        LootDraft.Visibility = editTab ? Visibility.Visible : Visibility.Collapsed;
        if (editTab)
        {
            if (lootDraft?.Template != TemplateLootScope)
                SetLootDraft(SelectedLootRow);
            ShowLootRow(SelectedLootRow);
        }
        RefreshControls();
    }

    private void LootRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingLoot || !IsLoaded || (TemplateLootScope ? sender != SavedLoot : sender != EffectiveLoot))
            return;

        SetLootDraft(SelectedLootRow);
        ShowLootRow(SelectedLootRow);
        RefreshControls();
    }

    private void ShowLootRow(LootRow? row)
    {
        if (row == null)
        {
            EffectiveLootRow.Text = "";
            return;
        }

        EffectiveLootRow.Text = string.Join(Environment.NewLine,
            new[] { $"{row.Source} · {row.Behavior} · {FormatHash(row.Hash)}",
                $"Provenance  {row.Provenance}", $"Quantity  {row.QuantityRange}", row.Conditions }
            .Concat(row.Groups.Select(group => group.Preview))
            .Concat(row.Items.Select(item => item.Preview)));
    }

    private void SetLootDraft(LootRow? row, bool append = false)
    {
        bool template = TemplateLootScope;
        int? revision = template ? loot?.Saved.Revision : loot?.Live?.Revision;
        lootDraft = revision is int version && (append || row?.Edit != null)
            ? new LootDraftState(template, row?.Edit, version, false)
            : null;
        LootEntry? entry = row?.Entry;
        LootItemHash.Text = entry?.ItemHash ?? "";
        LootChance.Text = FormatStatValue(entry?.Chance);
        LootRepeats.Text = FormatStatValue(entry?.Repeats);
        LootVariance.Text = entry?.MinQuality == null ? FormatStatValue(entry?.LevelMod) : "";
        LootMinQuality.Text = FormatStatValue(entry?.MinQuality);
        LootMaxQuality.Text = FormatStatValue(entry?.MaxQuality);
    }

    private void NewLootRow_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditLoot)
            return;

        (TemplateLootScope ? SavedLoot : EffectiveLoot).SelectedItem = null;
        SetLootDraft(null, append: true);
        RefreshControls();
    }

    private void RemoveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanEditLoot && lootDraft is { Row: not null } draft)
        {
            lootDraft = draft with { Remove = true };
            RefreshControls();
        }
    }

    private void RevertLootDraft_Click(object sender, RoutedEventArgs e)
    {
        SetLootDraft(SelectedLootRow);
        RefreshControls();
    }

    private void PickLootItem_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditLoot || lootDraft == null)
            return;

        if (OpenCatalog("Choose loot item family", families: true) is { Hash: string hash })
            LootItemHash.Text = hash;
    }

    private async void ApplyLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanApplyLoot && lootDraft is { Template: false } draft && TryLootEntry(draft, out var entry))
            await EditLiveLootAsync("loot/live", new { expectedRevision = draft.Revision, row = draft.Row, entry },
                "Applied loot edit to the selected NPC.");
    }

    private async void SaveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && lootDraft is { Template: true } draft && TryLootEntry(draft, out var entry))
            await EditSavedLootAsync("loot", new { scope = "templateBand", expectedRevision = draft.Revision, row = draft.Row, entry });
    }

    private bool TryLootEntry(LootDraftState draft, out LootEntry? entry)
    {
        entry = null;
        if (draft.Remove)
            return true;

        string itemHash = LootItemHash.Text.Trim();
        if (itemHash.Length == 0 || !TryNumber(LootChance.Text, 0, 100, out int chance)
            || !TryNumber(LootRepeats.Text, 1, 20, out int repeats))
        {
            Status.Text = "Choose an item or enter an existing group hash. Set chance to 0–100 and attempts to 1–20.";
            return false;
        }

        if (LootMinQuality.Text.Length > 0 || LootMaxQuality.Text.Length > 0)
        {
            if (!TryNumber(LootMinQuality.Text, 1, int.MaxValue - 1, out int min)
                || !TryNumber(LootMaxQuality.Text, min, int.MaxValue - 1, out int max)
                || (LootVariance.Text.Length != 0 && LootVariance.Text != "0"))
            {
                Status.Text = "Enter both QL bounds in order and leave QL variance blank or zero.";
                return false;
            }
            entry = new LootEntry(itemHash, chance, repeats, 0, min, max);
            return true;
        }

        if (!TryNumber(LootVariance.Text, 0, 100, out int variance))
        {
            Status.Text = "Enter QL variance from 0–100, using 0 for the server default.";
            return false;
        }
        entry = new LootEntry(itemHash, chance, repeats, variance);
        return true;
    }

    private static bool TryNumber(string text, int min, int max, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

    private async void RevertLiveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanApplyLoot && loot?.Live is { } live && LiveLootHistory.SelectedItem is LiveLootChange { Revertible: true } row)
            await EditLiveLootAsync("loot/live/revert", new { expectedRevision = live.Revision, changeId = row.ChangeId },
                "Undid the selected live loot change.");
    }

    private async Task EditLiveLootAsync(string route, object payload, string success)
    {
        string path = detail!.Npc.Identity.Path;
        await RunAsync(async () =>
        {
            LootDetail next = await Api.PostAsync<LootDetail>($"{path}/{route}", payload);
            lootDraft = null;
            RenderLoot(next);
            ClearLootSimulation("Live loot changed. Run a new simulation for the current rows.");
            Status.Text = success;
        });
    }

    private void LootHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender == LootHistory)
        {
            var row = LootHistory.SelectedItem as LootHistoryRow;
            LootHistoryDetails.Text = row == null ? "" : FormatLootHistory(row);
        }
        RefreshControls();
    }

    private static string FormatLootHistory(LootHistoryRow row)
    {
        string scope = row.BandIndex is int band ? $"Template band index {band}" : row.Scope ?? "Template additions";
        if (row.RestoredRevision is int revision)
            return $"{scope} · Restored revision {revision}";
        if (row.RowIndex is not int index)
            return scope;
        return string.Join(Environment.NewLine, $"{scope} · Row index {index}",
            $"Before  {row.Before?.Summary ?? "—"}", $"After  {row.After?.Summary ?? "—"}");
    }

    private async void RevertLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && loot!.Saved.Revision > 0)
            await RestoreLootAsync(loot.Saved.Revision - 1);
    }

    private async void RestoreLootRevision_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && LootHistory.SelectedItem is LootHistoryRow row && row.Revision < loot!.Saved.Revision)
            await RestoreLootAsync(row.Revision);
    }

    private Task RestoreLootAsync(int revision) => EditSavedLootAsync("loot/revert",
        new { scope = "templateBand", expectedRevision = loot!.Saved.Revision, revision });

    private async Task EditSavedLootAsync(string route, object payload)
    {
        SavedLootState saved = loot!.Saved;
        if (!ConfirmTemplateChange(saved.Definition ?? saved.TemplateHash, saved.EstimatedAffectedNpcs))
            return;

        NpcIdentity identity = detail!.Npc.Identity;
        await RunAsync(async () =>
        {
            SavedLootState next = await Api.PostAsync<SavedLootState>($"{identity.Path}/{route}", payload);
            lootDraft = null;
            await LoadLootAsync(identity);
            Status.Text = $"Saved template loot revision {next.Revision} for future spawns.";
        });
    }

    private async void SimulateLoot_Click(object sender, RoutedEventArgs e)
    {
        if (loot?.Live == null || detail == null || !TryNumber(LootRollCount.Text, 1, 10_000, out int rolls))
        {
            Status.Text = "Enter a simulation count from 1 to 10,000.";
            return;
        }

        string path = detail.Npc.Identity.Path;
        await RunAsync(async () =>
        {
            ClearLootSimulation($"Simulating {rolls:N0} corpses…");
            try
            {
                LootSimulation result = await Api.PostAsync<LootSimulation>(path + "/loot/simulate", new { rolls },
                    TimeSpan.FromSeconds(45));
                LootSimulationGrid.ItemsSource = result.Items;
                LootSimulationStatus.Text = $"{result.Rolls:N0} simulated corpses · {result.AverageItemsPerCorpse:F2} items/corpse · "
                    + $"{result.EmptyCorpses:N0} empty ({result.EmptyPercent:F2}%). Observed simulation frequencies are shown below.";
            }
            catch
            {
                ClearLootSimulation("Simulation failed.");
                throw;
            }
        });
    }

    private void ClearLootSimulation(string message)
    {
        LootSimulationGrid.ItemsSource = null;
        LootSimulationStatus.Text = message;
    }

    private void RefreshLootControls()
    {
        int? revision = TemplateLootScope ? loot?.Saved.Revision : loot?.Live?.Revision;
        bool currentDraft = lootDraft != null && lootDraft.Template == TemplateLootScope && lootDraft.Revision == revision;
        bool canEditFields = CanEditLoot && currentDraft && lootDraft?.Remove == false;
        PickLootItem.IsEnabled = LootItemHash.IsEnabled = canEditFields;
        NewLootRow.IsEnabled = CanEditLoot;
        RemoveLoot.IsEnabled = canEditFields && lootDraft?.Row != null;
        ApplyLoot.Visibility = TemplateLootScope ? Visibility.Collapsed : Visibility.Visible;
        SaveLoot.Visibility = TemplateLootScope ? Visibility.Visible : Visibility.Collapsed;
        ApplyLoot.IsEnabled = CanApplyLoot && currentDraft;
        SaveLoot.IsEnabled = CanSaveLoot && currentDraft;
        RevertLootDraft.IsEnabled = lootDraft != null;
        LootChance.IsEnabled = LootRepeats.IsEnabled = LootVariance.IsEnabled = LootMinQuality.IsEnabled = LootMaxQuality.IsEnabled = canEditFields;
        RevertLoot.IsEnabled = CanSaveLoot && loot!.Saved.Revision > 0;
        RestoreLootRevision.IsEnabled = CanSaveLoot && LootHistory.SelectedItem is LootHistoryRow saved && saved.Revision < loot!.Saved.Revision;
        RevertLiveLoot.IsEnabled = CanApplyLoot && LiveLootHistory.SelectedItem is LiveLootChange { Revertible: true };
        SimulateLoot.IsEnabled = loot?.Live != null && !busy;
        LootDraftStatus.Text = lootDraft switch
        {
            null => "Lock the NPC and select an editable row or add an entry.",
            _ when !currentDraft => "The revision changed. Revert the draft and reselect the row.",
            { Remove: true } => "Removal staged. Apply or save to remove the selected row.",
            _ => "100% passes the chance check. Eligibility and corpse capacity still apply."
        };
    }

    private static string FormatAffected(int? count) =>
        count is int number ? $"{number:N0} matching spawned NPC(s)" : "Matching spawn count unavailable";

    private bool ConfirmTemplateChange(string definition, int? affected)
    {
        string message = $"{definition}{Environment.NewLine}{FormatAffected(affected)}{Environment.NewLine}{Environment.NewLine}"
            + "Save changes for future spawns in this template band?";
        return MessageBox.Show(this, message, "Confirm template band scope", MessageBoxButton.OKCancel,
            MessageBoxImage.Question) == MessageBoxResult.OK;
    }

    private sealed record LootDraftState(bool Template, LootRowKey? Row, int Revision, bool Remove);
}
