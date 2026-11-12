using System.Windows;

namespace AORebirth.NpcEditor;

public partial class ConnectionModeWindow : Window
{
    public ConnectionModeWindow()
    {
        InitializeComponent();
    }

    private void OpenAORebirth_Click(object sender, RoutedEventArgs e) => OpenMode(new MainWindow());

    private void OpenOfficial_Click(object sender, RoutedEventArgs e) => OpenMode(new OfficialInspectorWindow());

    private void OpenMode(Window window)
    {
        Application.Current.MainWindow = window;
        window.Show();
        Close();
    }
}
