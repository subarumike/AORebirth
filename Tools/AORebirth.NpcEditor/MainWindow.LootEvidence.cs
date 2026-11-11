using System.Windows;

namespace AORebirth.NpcEditor;

public partial class MainWindow
{
    private void BrowseLootEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (api == null || busy)
            return;
        var dialog = new LootEvidenceWindow(api, session?.CanSave == true, detail?.Npc.Identity) { Owner = this };
        pickerOpen = true;
        try
        {
            dialog.ShowDialog();
            if (dialog.Unauthorized)
                ResetConnection();
        }
        finally
        {
            pickerOpen = false;
        }
        RefreshControls();
    }
}
