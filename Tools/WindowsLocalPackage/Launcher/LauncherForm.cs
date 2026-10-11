namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.Drawing;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    internal sealed class LauncherForm : Form
    {
        private readonly Label status;
        private readonly Label clientFolder;
        private readonly FlowLayoutPanel actions;
        private readonly PackageController controller;
        private Button cancelDownload;
        private bool busy;
        private bool closing;

        internal LauncherForm(string root)
        {
            Text = "AORebirth — Local Server";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(740, 550);
            Size = new Size(780, 590);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.White;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28),
                ColumnCount = 1,
                RowCount = 6
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(layout);

            layout.Controls.Add(new Label { Text = "AORebirth", Font = new Font(Font.FontFamily, 24F, FontStyle.Bold), AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "1. Install prerequisites, then run first-time setup.\r\n2. Download the client (it will be verified and extracted automatically).\r\n3. Create your local account and press Play.",
                Dock = DockStyle.Fill
            }, 0, 1);
            clientFolder = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
            layout.Controls.Add(clientFolder, 0, 2);
            actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, WrapContents = true, AutoScroll = true };
            layout.Controls.Add(actions, 0, 3);
            status = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(45, 70, 95) };
            layout.Controls.Add(status, 0, 4);
            layout.Controls.Add(new Label
            {
                Text = "Keep this window open while playing. Log out before stopping. Back up the complete data folder to retain your characters.",
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Font = new Font(Font.FontFamily, 9F)
            }, 0, 5);

            try
            {
                controller = new PackageController(root, UpdateStatus);
                AddButton("Install prerequisites", () => controller.InstallPrerequisites());
                AddButton("First-time setup", () => controller.Prepare());
                AddButton("Download client", () => controller.DownloadClient());
                AddButton("Choose client folder", ChooseClient);
                AddButton("Create account", () => controller.CreateAccount());
                AddButton("Play", () => controller.Play());
                AddButton("Stop server", () => controller.Stop());
                AddButton("Status", () => controller.CheckStatus());
                AddButton("Open logs", () => controller.OpenLogs());
                cancelDownload = new Button { Text = "Cancel download", AutoSize = true, MinimumSize = new Size(142, 38), Margin = new Padding(0, 0, 10, 10), Enabled = false };
                cancelDownload.Click += (sender, args) => controller.CancelDownload();
                actions.Controls.Add(cancelDownload);
                RefreshClientFolder();
                status.Text = controller.IsRunning ? "Recovered local services from a previous launcher session. Press Status to check them or Stop server to shut them down."
                    : controller.SetupComplete ? "Ready to start your local server."
                    : "First-time setup will initialize a dedicated local database. Windows may ask permission to enable local account registration.";
            }
            catch (Exception exception)
            {
                status.Text = exception.Message;
            }

            FormClosing += OnFormClosing;
        }

        private void AddButton(string label, Action action)
        {
            var button = new Button { Text = label, AutoSize = true, MinimumSize = new Size(142, 38), Margin = new Padding(0, 0, 10, 10) };
            button.Click += async (sender, args) =>
            {
                if (label == "Choose client folder")
                {
                    ChooseClient();
                    return;
                }

                await RunAction(action, label == "Download client");
            };
            actions.Controls.Add(button);
        }

        private void ChooseClient()
        {
            using (var picker = new FolderBrowserDialog
            {
                Description = "Select the folder where you extracted the AORebirth client.",
                ShowNewFolderButton = false,
                SelectedPath = controller.ClientFolder
            })
            {
                if (picker.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    controller.SetClientFolder(picker.SelectedPath);
                    RefreshClientFolder();
                    status.Text = "Client folder saved.";
                }
                catch (Exception exception)
                {
                    ShowFailure(exception);
                }
            }
        }

        private async Task RunAction(Action action, bool downloading = false)
        {
            if (busy)
                return;

            busy = true;
            foreach (Control button in actions.Controls)
            {
                button.Enabled = false;
            }

            cancelDownload.Enabled = downloading;
            UseWaitCursor = true;
            try
            {
                await Task.Run(action);
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    status.Text = exception.Message;
                }
                else
                {
                    ShowFailure(exception);
                }
            }
            finally
            {
                RefreshClientFolder();
                UseWaitCursor = false;
                foreach (Control button in actions.Controls)
                {
                    button.Enabled = true;
                }

                cancelDownload.Enabled = false;
                busy = false;
            }
        }

        private void UpdateStatus(string text)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(UpdateStatus), text);
                return;
            }

            status.Text = text;
        }

        private void RefreshClientFolder()
        {
            clientFolder.Text = "Client folder: " + (string.IsNullOrWhiteSpace(controller.ClientFolder) ? "Choose the extracted client folder." : controller.ClientFolder);
        }

        private void ShowFailure(Exception exception)
        {
            status.Text = exception.Message;
            MessageBox.Show(this, exception.Message, "AORebirth", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void OnFormClosing(object sender, FormClosingEventArgs args)
        {
            if (closing || controller == null)
                return;

            if (busy)
            {
                args.Cancel = true;
                status.Text = "Wait for the current operation to finish before closing.";
                return;
            }

            if (!controller.IsRunning)
                return;

            args.Cancel = true;
            await RunAction(() => controller.Stop());
            if (!controller.IsRunning)
            {
                closing = true;
                Close();
            }
        }
    }
}
