using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AORebirth.NpcEditor.Official;

namespace AORebirth.NpcEditor;

public partial class OfficialLootRecordWindow : Window
{
    private readonly OfficialSelectionLock target;

    internal OfficialLootRecordWindow(OfficialSelectionLock target, string label)
    {
        InitializeComponent();
        this.target = target;
        SourceNpc.Text = label;
        ObservationStatus.ItemsSource = new[] { "OBSERVED_DROP", "UNKNOWN_LOOT", "UNRESOLVED_ITEM" };
        ObservationStatus.SelectedIndex = 0;
    }

    internal OfficialLootObservation? Observation { get; private set; }

    private void ObservationStatus_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        bool includeItem = (string?)ObservationStatus.SelectedItem != "UNKNOWN_LOOT";
        ItemName.IsEnabled = LowId.IsEnabled = HighId.IsEnabled = Quality.IsEnabled = Quantity.IsEnabled = includeItem;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmOpened.IsChecked != true)
        {
            StatusText.Text = "Confirm the observation came from a corpse you opened.";
            return;
        }

        try
        {
            string status = (string)ObservationStatus.SelectedItem;
            bool includeItem = status != "UNKNOWN_LOOT";
            var observation = new OfficialLootObservation(target, DateTime.UtcNow.Ticks,
                status, Optional(CorpseIdentity.Text), includeItem ? Optional(ItemName.Text) : null,
                includeItem ? ReadNumber(LowId.Text, "low AOID") : null,
                includeItem ? ReadNumber(HighId.Text, "high AOID") : null,
                includeItem ? ReadNumber(Quality.Text, "QL") : null,
                includeItem ? ReadNumber(Quantity.Text, "quantity") : null,
                ReadNumber(Credits.Text, "credits"), Optional(Notes.Text));
            OfficialObservationExport.ValidateLoot(observation);
            Observation = observation;
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? ReadNumber(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            throw new ArgumentException($"Enter the observed {field} or leave it blank.");
        return number;
    }
}
