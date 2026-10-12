namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.Collections.Generic;
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
                Text = "1. Install prerequisites and run setup to prepare your local files.\r\n2. Download the client, then press Start server.\r\n3. Once the server is ready, create your local account and press Play.",
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
                AddButton("Start server", () => controller.Start());
                AddButton("Create account", () => controller.CreateAccount());
                AddButton("Play", () => controller.Play());
                AddButton("Stop server", () => controller.Stop());
                AddButton("Status", () => controller.CheckStatus());
                AddButton("SQL login", ShowSqlLogin);
                AddButton("Open logs", () => controller.OpenLogs());
                cancelDownload = new Button { Text = "Cancel download", AutoSize = true, MinimumSize = new Size(142, 38), Margin = new Padding(0, 0, 10, 10), Enabled = false };
                cancelDownload.Click += (sender, args) => controller.CancelDownload();
                actions.Controls.Add(cancelDownload);
                RefreshClientFolder();
                status.Text = controller.IsRunning ? "Recovered local services from a previous launcher session. Press Status to check them or Stop server to shut them down."
                    : controller.SetupComplete ? "Ready to start your local server."
                    : "Setup prepares files without starting services. Start server initializes the local database if needed and starts services when you choose.";
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

                if (label == "SQL login")
                {
                    ShowSqlLogin();
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

        private void ShowSqlLogin()
        {
            try
            {
                using (Form dialog = BuildSqlLoginDialog(controller.GetSqlLoginDetails()))
                {
                    dialog.ShowDialog(this);
                }
            }
            catch (Exception exception)
            {
                ShowFailure(exception);
            }
        }

        private Form BuildSqlLoginDialog(IDictionary<string, string> details)
        {
            var dialog = new Form
            {
                Text = "Local SQL login",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(580, 360),
                Font = Font,
                BackColor = Color.White
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var description = new Label { Text = "Use these details to connect to this installation's local SQL database.", Dock = DockStyle.Fill };
            layout.Controls.Add(description, 0, 0);
            layout.SetColumnSpan(description, 2);
            TextBox password = null;
            string[] fields = { "Host", "Port", "Database", "Username", "Password" };
            for (int index = 0; index < fields.Length; index++)
            {
                string field = fields[index];
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                layout.Controls.Add(new Label { Text = field, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, index + 1);
                var value = new TextBox { Text = details[field], ReadOnly = true, Dock = DockStyle.Fill, UseSystemPasswordChar = field == "Password" };
                layout.Controls.Add(value, 1, index + 1);
                if (field == "Password")
                {
                    password = value;
                }
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var showPassword = new CheckBox { Text = "Show password", AutoSize = true };
            showPassword.CheckedChanged += (sender, args) => password.UseSystemPasswordChar = !showPassword.Checked;
            layout.Controls.Add(showPassword, 1, 6);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(142, 38), Margin = new Padding(0, 0, 10, 10) };
            var copyPassword = new Button { Text = "Copy password", AutoSize = true, MinimumSize = new Size(142, 38), Margin = new Padding(0, 0, 10, 10) };
            copyPassword.Click += (sender, args) =>
            {
                try
                {
                    Clipboard.SetText(password.Text);
                }
                catch (Exception exception)
                {
                    ShowFailure(exception);
                }
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(copyPassword);
            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 2);
            dialog.Controls.Add(layout);
            dialog.AcceptButton = close;
            dialog.CancelButton = close;
            return dialog;
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
