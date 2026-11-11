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
    private readonly DispatcherTimer statsTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private EditorApi? api;
    private EditorSession? session;
    private NpcDetail? detail;
    private TargetHint? lastHint;
    private long nextTargetResolve;
    private int? statDraftBase;
    private bool updatingStats;
    private bool statsReading;
    private bool pickerOpen;
    private bool busy;
    private bool bridgeReading;
    private bool settingNpcSelection;

    public MainWindow()
    {
        InitializeComponent();
        bridgeTimer.Tick += BridgeTimer_Tick;
        bridgeTimer.Start();
        statsTimer.Tick += StatsTimer_Tick;
        statsTimer.Start();
        Closed += (_, _) =>
        {
            bridgeTimer.Stop();
            statsTimer.Stop();
            api?.Dispose();
        };
    }

    private EditorApi Api => api ?? throw new InvalidOperationException("Connect to ZoneEngine first.");

    private bool CanWrite => !busy && session is { CanWrite: true } && LockNpc.IsChecked == true && detail != null;

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
        bool sameSpawn = detail?.Npc.Identity == identity;
        if (!sameSpawn)
            ClearDetail();

        NpcDetail next = await Api.GetAsync<NpcDetail>(identity.Path);
        if (next.Npc.Identity != identity)
            throw new InvalidDataException("ZoneEngine returned an unexpected NPC identity. Select the spawn again.");

        RenderDetail(next, preserveStatDraft: sameSpawn);
        Status.Text = $"Loaded {next.Npc.Name}.";
        await Task.WhenAll(LoadLootAsync(identity), LoadSavedEquipmentAsync(identity));
        if (sameSpawn && LootSimulationGrid.ItemsSource != null)
            ClearLootSimulation("NPC refreshed. Run a new simulation for the current loot inputs.");
    }

    private void ClearDetail()
    {
        detail = null;
        LockNpc.IsChecked = false;
        DetailHeading.Text = "Select a spawned NPC";
        DetailIdentity.Text = "";
        Overview.Text = "";
        UploadedNanos.Text = "";
        ActiveNanos.Text = "";
        Brain.Text = "";
        StatsGrid.ItemsSource = null;
        StatsStatus.Text = "";
        EquipmentGrid.ItemsSource = null;
        ChangesGrid.ItemsSource = null;
        ClearLoot();
        ClearEquipment();
    }

    private void RenderDetail(NpcDetail next, bool preserveStatDraft = false)
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
        RefreshStats(preserveStatDraft);
        EquipmentGrid.ItemsSource = next.Equipment;
        if (savedEquipment == null)
            RenderSavedEquipment();
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

    private void RefreshStats(bool preserveDraft = false)
    {
        string query = StatQuery.Text.Trim();
        var prior = StatsGrid.SelectedItem as StatRow;
        bool keepDraft = preserveDraft && prior != null
            && (NewStatBase.IsKeyboardFocusWithin || NewStatBase.Text != FormatStatValue(statDraftBase));
        StatRow[]? rows = detail?.Stats.Where(stat =>
            stat.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || stat.StatId.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal)).ToArray();
        var selected = rows?.FirstOrDefault(row => row.StatId == prior?.StatId);
        updatingStats = true;
        try
        {
            StatsGrid.ItemsSource = rows;
            StatsGrid.SelectedItem = selected;
        }
        finally
        {
            updatingStats = false;
        }
        if (!keepDraft || selected == null)
            SetStatDraft(selected);
    }

    private void Stat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingStats)
            return;
        SetStatDraft(StatsGrid.SelectedItem as StatRow);
        RefreshControls();
    }

    private void SetStatDraft(StatRow? row)
    {
        statDraftBase = row?.RuntimeBase;
        NewStatBase.Text = FormatStatValue(statDraftBase);
    }

    private static string FormatStatValue(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private void RevertStatDraft_Click(object sender, RoutedEventArgs e)
    {
        SetStatDraft(StatsGrid.SelectedItem as StatRow);
        RefreshControls();
    }

    private async void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (api == null || detail == null || busy || statsReading || pickerOpen || !IsActive || Details.SelectedItem != StatsTab)
            return;

        var connection = api;
        var snapshot = detail;
        statsReading = true;
        try
        {
            StatRow[] rows = await connection.GetAsync<StatRow[]>(snapshot.Npc.Identity.Path + "/stats");
            if (connection == api && snapshot == detail && !busy)
            {
                detail = snapshot with { Stats = rows };
                RefreshStats(preserveDraft: true);
                StatsStatus.Text = StatsGrid.SelectedItem is StatRow stat && stat.RuntimeBase != statDraftBase
                    ? "The runtime base changed. Revert the draft before applying a new value."
                    : $"Live values refreshed {DateTime.Now:T}";
                RefreshControls();
            }
        }
        catch (Exception ex)
        {
            if (connection == api && snapshot == detail && !busy)
                StatsStatus.Text = DescribeError(ex);
        }
        finally
        {
            statsReading = false;
        }
    }

    private async void ApplyStat_Click(object sender, RoutedEventArgs e)
    {
        if (!CanWrite || statDraftBase is not int expectedBase
            || StatsGrid.SelectedItem is not StatRow { Editable: true, RuntimeBase: not null } stat)
            return;

        if (!int.TryParse(NewStatBase.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            Status.Text = "Enter a whole-number runtime base value.";
            return;
        }

        await ApplyLiveAsync("stats", new { statId = stat.StatId, expectedBase, value }, "Applied live stat change.");
    }

    private async Task ApplyLiveAsync(string route, object payload, string success, Action? onApplied = null)
    {
        if (!CanWrite)
            return;

        NpcIdentity identity = detail!.Npc.Identity;
        await RunAsync(async () =>
        {
            NpcDetail next = await Api.PostAsync<NpcDetail>($"{identity.Path}/{route}", payload);
            if (next.Npc.Identity != identity)
                throw new InvalidDataException("ZoneEngine returned an unexpected NPC identity. Refresh before further edits.");

            RenderDetail(next, preserveStatDraft: route != "stats");
            onApplied?.Invoke();
            Status.Text = success;
            if (route != "stats")
            {
                await LoadLootAsync(identity);
                ClearLootSimulation("Live inputs changed. Run a new simulation for the current loot inputs.");
            }
        });
    }

    private async void RevertChange_Click(object sender, RoutedEventArgs e)
    {
        if (CanWrite && ChangesGrid.SelectedItem is ChangeRow { Revertible: true, Reverted: false } row)
            await ApplyLiveAsync("revert", new { changeId = row.ChangeId }, "Reverted the selected live change.");
    }

    private void Change_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var row = ChangesGrid.SelectedItem as ChangeRow;
        LiveChangeDetails.Text = row == null ? "" : string.Join(Environment.NewLine,
            $"Scope  {row.Scope ?? "Selected NPC"}", $"Before  {row.Before}", $"After  {row.After}",
            row.RevertedBy == null ? "" : $"Reverted by  {row.RevertedBy}");
        RefreshControls();
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
        if (api == null || busy || bridgeReading || pickerOpen || FollowTarget.IsChecked != true || LockNpc.IsChecked == true)
            return;

        bridgeReading = true;
        var connection = api;
        try
        {
            var (hint, status) = await Task.Run(TargetBridge.Read);
            if (connection != api || busy || pickerOpen || FollowTarget.IsChecked != true || LockNpc.IsChecked == true)
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
        ApplyStat.IsEnabled = CanWrite && statDraftBase.HasValue
            && StatsGrid.SelectedItem is StatRow { Editable: true } stat && stat.RuntimeBase == statDraftBase;
        RevertStatDraft.IsEnabled = !busy && StatsGrid.SelectedItem is StatRow;
        RevertChange.IsEnabled = CanWrite && ChangesGrid.SelectedItem is ChangeRow { Revertible: true, Reverted: false };
        RefreshLootControls();
        RefreshEquipmentControls();
    }

    private void BrowseItems_Click(object sender, RoutedEventArgs e) => OpenCatalog("Item catalog", browseOnly: true);

    private ItemRow? OpenCatalog(string title, bool families = false, string? category = null, bool browseOnly = false)
    {
        var dialog = new ItemPicker(Api, title, families, category, browseOnly) { Owner = this };
        pickerOpen = true;
        try
        {
            return dialog.ShowDialog() == true ? dialog.Selected : null;
        }
        finally
        {
            pickerOpen = false;
        }
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
