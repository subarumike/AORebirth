using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AORebirth.NpcEditor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer bridgeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private EditorApi? api;
    private EditorSession? session;
    private NpcDetail? detail;
    private LootDetail? loot;
    private TargetHint? lastHint;
    private long nextTargetResolve;
    private string? draftLootHash;
    private bool busy;
    private bool bridgeReading;
    private bool settingNpcSelection;

    public MainWindow()
    {
        InitializeComponent();
        bridgeTimer.Tick += BridgeTimer_Tick;
        bridgeTimer.Start();
        Closed += (_, _) =>
        {
            bridgeTimer.Stop();
            api?.Dispose();
        };
    }

    private EditorApi Api => api ?? throw new InvalidOperationException("Connect to ZoneEngine first.");

    private bool CanWrite => !busy && session is { CanWrite: true } && LockNpc.IsChecked == true && detail != null;

    private bool CanSaveLoot => CanWrite && session is { CanSave: true } && loot?.Saved?.CanSave == true;

    private async void Connect_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (api != null)
        {
            try
            {
                await api.LogOutAsync();
                Status.Text = "Disconnected.";
            }
            finally
            {
                ResetConnection();
            }
            return;
        }

        if (!int.TryParse(Port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int port))
            throw new ArgumentException("Enter the local ZoneEngine editor port.");
        if (string.IsNullOrWhiteSpace(UserName.Text) || Password.Password.Length == 0)
            throw new ArgumentException("Enter a GM account and password.");

        var connection = new EditorApi(port, Fingerprint.Text.Trim());
        try
        {
            Status.Text = "Connecting to ZoneEngine…";
            session = await connection.LogInAsync(UserName.Text.Trim(), Password.Password);
            api = connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
        finally
        {
            Password.Clear();
        }

        await SearchNpcsAsync();
    });

    private void ResetConnection()
    {
        api?.Dispose();
        api = null;
        session = null;
        lastHint = null;
        FollowTarget.IsChecked = false;
        NpcGrid.ItemsSource = null;
        ClearDetail();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await RunAsync(SearchNpcsAsync);

    private async void NpcQuery_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await RunAsync(SearchNpcsAsync);
    }

    private async Task SearchNpcsAsync()
    {
        string query = Uri.EscapeDataString(NpcQuery.Text.Trim());
        NpcSummary[] matches = await Api.GetAsync<NpcSummary[]>($"npcs?q={query}");
        NpcIdentity? displayed = detail?.Npc.Identity;
        settingNpcSelection = true;
        try
        {
            NpcGrid.ItemsSource = matches;
            NpcGrid.SelectedItem = matches.FirstOrDefault(npc => npc.Identity == displayed);
        }
        finally
        {
            settingNpcSelection = false;
        }
        string access = session?.CanWrite == true ? "GM editing" : "GM read-only";
        Status.Text = $"Connected · {access} · {matches.Length} NPCs returned";
    }

    private async void Npc_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!settingNpcSelection && LockNpc.IsChecked != true && NpcGrid.SelectedItem is NpcSummary npc)
            await RunAsync(() => LoadDetailAsync(npc.Identity));
    }

    private async void RefreshDetail_Click(object sender, RoutedEventArgs e)
    {
        var identity = detail?.Npc.Identity ?? (NpcGrid.SelectedItem as NpcSummary)?.Identity;
        if (identity != null)
            await RunAsync(() => LoadDetailAsync(identity));
    }

    private async Task LoadDetailAsync(NpcIdentity identity)
    {
        bool keepLock = LockNpc.IsChecked == true && detail?.Npc.Identity == identity;
        ClearDetail();
        NpcDetail next = await Api.GetAsync<NpcDetail>(identity.Path);
        if (next.Npc.Identity != identity)
            throw new InvalidDataException("ZoneEngine returned an unexpected NPC identity. Select the spawn again.");

        RenderDetail(next);
        LockNpc.IsChecked = keepLock;
        Status.Text = $"Loaded {next.Npc.Name}.";
        await LoadLootAsync(identity);
    }

    private async Task LoadLootAsync(NpcIdentity identity)
    {
        try
        {
            loot = await Api.GetAsync<LootDetail>(identity.Path + "/loot");
            EffectiveLoot.ItemsSource = loot.Rows;
            EffectiveLootStatus.Text = $"Runtime loot for this spawn · {FormatHash(loot.TemplateHash)} · {loot.Rows.Length} entries";
            RenderSavedLoot();
        }
        catch (Exception ex) when (ex is not EditorApiException { Status: HttpStatusCode.Unauthorized })
        {
            loot = null;
            EffectiveLoot.ItemsSource = null;
            RenderSavedLoot();
            EffectiveLootStatus.Text = DescribeError(ex);
        }
    }

    private void ClearDetail()
    {
        detail = null;
        loot = null;
        LockNpc.IsChecked = false;
        DetailHeading.Text = "Select a spawned NPC";
        DetailIdentity.Text = "";
        Overview.Text = "";
        UploadedNanos.Text = "";
        ActiveNanos.Text = "";
        Brain.Text = "";
        StatsGrid.ItemsSource = null;
        EquipmentGrid.ItemsSource = null;
        ConfiguredEquipmentGrid.ItemsSource = null;
        ChangesGrid.ItemsSource = null;
        EffectiveLoot.ItemsSource = null;
        EffectiveLootStatus.Text = "";
        SavedLootStatus.Text = "";
        SavedLoot.ItemsSource = null;
        LootHistory.ItemsSource = null;
        ResetLootFields();
    }

    private void RenderDetail(NpcDetail next)
    {
        detail = next;
        NpcSummary npc = next.Npc;
        DetailHeading.Text = $"{npc.Name}  ·  Level {npc.Level}";
        DetailIdentity.Text = $"{npc.Identity}  ·  Client PF {npc.ClientPlayfieldId}";
        Overview.Text = string.Join(Environment.NewLine,
            $"Template            {npc.TemplateId?.ToString() ?? "Unavailable"}  ·  {FormatHash(npc.TemplateHash)}",
            $"MonsterData     {npc.MonsterData?.ToString() ?? "Unavailable"}",
            $"Spawn source    {npc.SpawnSource ?? "Unavailable"}  ·  {FormatHash(npc.SpawnHash)}",
            $"Spawn point       {next.SpawnPointKey ?? "Unavailable"}",
            $"Coordinates      X {next.X:F2}   Y {next.Y:F2}   Z {next.Z:F2}",
            "",
            "Respawn",
            FormatFields(next.Respawn));
        UploadedNanos.Text = FormatNanos(next.UploadedNanos);
        ActiveNanos.Text = FormatNanos(next.ActiveNanos);
        Brain.Text = FormatFields(next.Brain);
        RefreshStats();
        EquipmentGrid.ItemsSource = next.Equipment;
        ConfiguredEquipmentGrid.ItemsSource = next.ConfiguredEquipment;
        ChangesGrid.ItemsSource = next.Changes;
    }

    private static string FormatHash(string? hash) => string.IsNullOrEmpty(hash) ? "Unavailable" : hash;

    private static string FormatNanos(IEnumerable<NanoRow> nanos)
    {
        string[] names = nanos.Select(nano => $"{nano.Name} ({nano.Id})").ToArray();
        return names.Length == 0 ? "None reported" : string.Join(Environment.NewLine, names);
    }

    private static string FormatFields(JsonElement? value)
    {
        if (value is not JsonElement { ValueKind: JsonValueKind.Object } fields)
            return "Unavailable";

        return string.Join(Environment.NewLine, fields.EnumerateObject()
            .Select(field => $"{field.Name}  ·  {field.Value}"));
    }

    private void StatQuery_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            RefreshStats();
    }

    private void RefreshStats()
    {
        string query = StatQuery.Text.Trim();
        StatsGrid.ItemsSource = detail?.Stats.Where(stat =>
            stat.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || stat.StatId.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal)).ToArray();
    }

    private void Stat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NewStatBase.Text = (StatsGrid.SelectedItem as StatRow)?.RuntimeBase?.ToString(CultureInfo.InvariantCulture) ?? "";
        RefreshControls();
    }

    private async void ApplyStat_Click(object sender, RoutedEventArgs e)
    {
        if (!CanWrite || StatsGrid.SelectedItem is not StatRow { Editable: true, RuntimeBase: int expectedBase } stat)
            return;

        if (!int.TryParse(NewStatBase.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            Status.Text = "Enter a whole-number runtime base value.";
            return;
        }

        await ApplyLiveAsync("stats", new { statId = stat.StatId, expectedBase, value }, "Applied live stat change.");
    }

    private async Task ApplyLiveAsync(string route, object payload, string success)
    {
        if (!CanWrite)
            return;

        NpcIdentity identity = detail!.Npc.Identity;
        await RunAsync(async () =>
        {
            NpcDetail next = await Api.PostAsync<NpcDetail>($"{identity.Path}/{route}", payload);
            if (next.Npc.Identity != identity)
                throw new InvalidDataException("ZoneEngine returned an unexpected NPC identity. Refresh before further edits.");

            RenderDetail(next);
            Status.Text = success;
            if (route != "stats")
                await LoadLootAsync(identity);
        });
    }

    private void Equipment_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshControls();

    private void Change_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshControls();

    private async void ReplaceEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Replaceable: true } row)
        {
            if (PickEquipmentItem("Choose live replacement") is ItemRow choice)
                await ApplyLiveAsync("equipment", new EquipmentEdit(row.Slot, row.InstanceId, choice.Id, choice.Quality), "Replaced live NPC equipment.");
        }
    }

    private async void RemoveEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Removable: true } row)
            await ApplyLiveAsync("equipment", new EquipmentEdit(row.Slot, row.InstanceId, null, null), "Removed live NPC armor.");
    }

    private async void AddArmor_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && detail?.CanAddArmor == true && PickEquipmentItem("Choose armor to equip live") is ItemRow choice)
            await ApplyLiveAsync("equipment", new EquipmentEdit(-1, 0, choice.Id, choice.Quality), "Equipped live NPC armor.");
    }

    private ItemRow? PickEquipmentItem(string title)
    {
        var dialog = new ItemPicker(title, async query =>
        {
            var rows = await Api.GetAsync<ItemRow[]>($"items?q={Uri.EscapeDataString(query)}");
            return rows.Select(row => new PickerRow(row.Name, row.Id.ToString(CultureInfo.InvariantCulture), row.Preview, row)).ToArray();
        }) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Selected as ItemRow : null;
    }

    private async void RevertChange_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && ChangesGrid.SelectedItem is ChangeRow { Revertible: true, Reverted: false } row)
            await ApplyLiveAsync("revert", new { changeId = row.ChangeId }, "Reverted the selected live change.");
    }

    private void EffectiveLoot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EffectiveLoot.SelectedItem is not LootRow row)
        {
            EffectiveLootRow.Text = "";
            return;
        }

        string quantities = row.MaxQuantity.HasValue ? $"{row.MinQuantity} – {row.MaxQuantity}" : "Varies by eligible branches";
        EffectiveLootRow.Text = string.Join(Environment.NewLine,
            new[] { $"{row.Source} · {row.Behavior} · {FormatHash(row.Hash)}",
                $"Provenance  {row.Provenance}", $"QL variance %  {row.LevelMod?.ToString() ?? "Unavailable"}",
                $"Quantity  {quantities}", row.Conditions }
            .Concat(row.Groups.Select(group => group.Preview))
            .Concat(row.Items.Select(item => item.Preview)));
    }

    private void RenderSavedLoot()
    {
        var saved = loot?.Saved;
        SavedLoot.ItemsSource = saved?.Entries;
        LootHistory.ItemsSource = saved?.History;
        ResetLootFields();
        SavedLootStatus.Text = saved?.CanSave == true
            ? $"Exact template {FormatHash(saved.TemplateHash)} · revision {saved.Revision}. Applies on future spawns. Original entries stay in place."
                + (session?.CanSave == true ? "" : " This session has no save permission.")
            : saved?.UnavailableReason ?? "No writable template overlay for this NPC.";
    }

    private void SavedLoot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedLoot.SelectedItem is LootEntry row)
        {
            draftLootHash = row.ItemHash;
            LootItemLabel.Text = FormatHash(row.ItemHash);
            LootChance.Text = row.Chance.ToString(CultureInfo.InvariantCulture);
            LootRepeats.Text = row.Repeats.ToString(CultureInfo.InvariantCulture);
            LootVariance.Text = row.LevelMod.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            ResetLootFields();
        }
        RefreshControls();
    }

    private void ResetLootFields()
    {
        draftLootHash = null;
        LootItemLabel.Text = "Select a row or pick a family.";
        LootChance.Text = "";
        LootRepeats.Text = "";
        LootVariance.Text = "";
    }

    private void NewLootRow_Click(object sender, RoutedEventArgs e)
    {
        SavedLoot.SelectedItem = null;
        ResetLootFields();
    }

    private void PickLootItem_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSaveLoot)
            return;

        var dialog = new ItemPicker("Choose loot item family", async query =>
        {
            var rows = await Api.GetAsync<LootItem[]>($"loot-items?q={Uri.EscapeDataString(query)}");
            return rows.Select(row => new PickerRow(row.Name, FormatHash(row.Hash), row.Preview, row)).ToArray();
        }) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Selected is LootItem row)
        {
            draftLootHash = row.Hash;
            LootItemLabel.Text = row.Preview;
        }
    }

    private async void SaveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSaveLoot || loot?.Saved is not SavedLootState saved)
            return;

        if (draftLootHash is not string itemHash
            || !int.TryParse(LootChance.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int chance)
            || !int.TryParse(LootRepeats.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int repeats)
            || !int.TryParse(LootVariance.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int levelMod))
        {
            Status.Text = "Choose an item family and enter whole numbers for chance, repeats, and QL variance.";
            return;
        }

        int? rowIndex = SavedLoot.SelectedIndex >= 0 ? SavedLoot.SelectedIndex : null;
        await EditLootAsync("loot", saved, new { expectedRevision = saved.Revision, rowIndex, entry = new LootEntry(itemHash, chance, repeats, levelMod) });
    }

    private async void RemoveLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && loot?.Saved is SavedLootState saved && SavedLoot.SelectedIndex >= 0)
            await EditLootAsync("loot", saved, new { expectedRevision = saved.Revision, rowIndex = SavedLoot.SelectedIndex, entry = (LootEntry?)null });
    }

    private async void RevertLoot_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && loot?.Saved is SavedLootState saved)
            await EditLootAsync("loot/revert", saved, new { expectedRevision = saved.Revision });
    }

    private void LootHistory_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshControls();

    private async void RestoreLootRevision_Click(object sender, RoutedEventArgs e)
    {
        if (CanSaveLoot && loot?.Saved is SavedLootState saved
            && LootHistory.SelectedItem is LootHistoryRow revision && revision.Revision < saved.Revision)
            await EditLootAsync("loot/revert", saved, new { expectedRevision = saved.Revision, revision = revision.Revision });
    }

    private async Task EditLootAsync(string route, SavedLootState saved, object payload)
    {
        var message = $"Change editor-owned loot for all future spawns of template {FormatHash(saved.TemplateHash)}?";
        if (MessageBox.Show(this, message, "Confirm exact template scope", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        string path = detail!.Npc.Identity.Path;
        await RunAsync(async () =>
        {
            SavedLootState next = await Api.PostAsync<SavedLootState>($"{path}/{route}", payload);
            loot = loot! with { Saved = next };
            RenderSavedLoot();
            Status.Text = $"Saved template loot revision {next.Revision} for future spawns.";
        });
    }

    private void Lock_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (LockNpc.IsChecked == true)
            BridgeStatus.Text = "Locked to the displayed NPC";
        else
            Follow_Changed(sender, e);
        RefreshControls();
    }

    private void Follow_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        lastHint = null;
        if (LockNpc.IsChecked != true)
            BridgeStatus.Text = FollowTarget.IsChecked == true ? "Waiting for the same local AO server’s target…" : "Manual selection";
    }

    private async void BridgeTimer_Tick(object? sender, EventArgs e)
    {
        if (api == null || busy || bridgeReading || FollowTarget.IsChecked != true || LockNpc.IsChecked == true)
            return;

        bridgeReading = true;
        var connection = api;
        try
        {
            var (hint, status) = await Task.Run(TargetBridge.Read);
            if (connection != api || busy || FollowTarget.IsChecked != true || LockNpc.IsChecked == true)
                return;

            long now = Environment.TickCount64;
            if (hint == lastHint && hint != null && now < nextTargetResolve)
                return;

            BridgeStatus.Text = status;
            lastHint = hint;
            if (hint == null)
                return;
            nextTargetResolve = now + 5_000;

            await RunAsync(async () =>
            {
                NpcIdentity identity = await Api.GetAsync<NpcIdentity>(hint.ResolvePath);
                if (detail?.Npc.Identity == identity)
                    return;
                settingNpcSelection = true;
                try
                {
                    NpcGrid.SelectedItem = (NpcGrid.ItemsSource as NpcSummary[])?.FirstOrDefault(npc => npc.Identity == identity);
                }
                finally
                {
                    settingNpcSelection = false;
                }

                await LoadDetailAsync(identity);
            });
        }
        finally
        {
            bridgeReading = false;
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy)
            return;

        busy = true;
        RefreshControls();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (api != null && ex is EditorApiException { Status: HttpStatusCode.Unauthorized })
                ResetConnection();
            Status.Text = DescribeError(ex);
        }
        finally
        {
            busy = false;
            RefreshControls();
        }
    }

    private void RefreshControls()
    {
        if (!IsLoaded)
            return;

        Workspace.IsEnabled = api != null && !busy;
        ConnectButton.IsEnabled = !busy;
        ConnectButton.Content = api == null ? "Connect" : "Disconnect";
        Port.IsEnabled = api == null && !busy;
        UserName.IsEnabled = api == null && !busy;
        Password.IsEnabled = api == null && !busy;
        Fingerprint.IsEnabled = api == null && !busy;
        LockNpc.IsEnabled = detail != null;
        NpcGrid.IsEnabled = LockNpc.IsChecked != true;
        RefreshDetail.IsEnabled = detail != null || NpcGrid.SelectedItem != null;
        Details.IsEnabled = detail != null;
        ApplyStat.IsEnabled = CanWrite && StatsGrid.SelectedItem is StatRow { Editable: true, RuntimeBase: not null };
        ReplaceEquipment.IsEnabled = CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Replaceable: true };
        RemoveEquipment.IsEnabled = CanWrite && EquipmentGrid.SelectedItem is EquipmentRow { Removable: true };
        AddArmor.IsEnabled = CanWrite && detail?.CanAddArmor == true;
        RevertChange.IsEnabled = CanWrite && ChangesGrid.SelectedItem is ChangeRow { Revertible: true, Reverted: false };
        PickLootItem.IsEnabled = CanSaveLoot;
        NewLootRow.IsEnabled = CanSaveLoot;
        SaveLoot.IsEnabled = CanSaveLoot;
        RemoveLoot.IsEnabled = CanSaveLoot && SavedLoot.SelectedIndex >= 0;
        RevertLoot.IsEnabled = CanSaveLoot && loot!.Saved!.History.Length > 0;
        RestoreLootRevision.IsEnabled = CanSaveLoot && LootHistory.SelectedItem is LootHistoryRow revision
            && revision.Revision < loot!.Saved!.Revision;
        LootChance.IsEnabled = CanSaveLoot;
        LootRepeats.IsEnabled = CanSaveLoot;
        LootVariance.IsEnabled = CanSaveLoot;
    }

    internal static string DescribeError(Exception ex) => ex switch
    {
        EditorApiException => ex.Message,
        HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } =>
            "ZoneEngine TLS verification failed. Check the startup fingerprint and port.",
        HttpRequestException => "Could not reach the local ZoneEngine editor endpoint.",
        OperationCanceledException => "The ZoneEngine request timed out. Refresh before retrying a write.",
        JsonException => "ZoneEngine returned an incompatible editor response.",
        _ => ex.Message
    };
}
