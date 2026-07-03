namespace SteamAccountSwitcher;

public sealed class CredentialLoginForm : Form
{
    private readonly TextBox _usernameBox = new();
    private readonly TextBox _passwordBox = new();
    private readonly CheckBox _showPasswordCheck = new();
    private readonly CheckBox _fastLaunchCheck = new();

    public CredentialLoginRequest? Request { get; private set; }

    public CredentialLoginForm(bool fastLaunchDefault)
    {
        Text = "Login with Credentials";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 250);
        Font = new Font("Segoe UI", 10F);
        BackColor = Theme.Bg;
        ForeColor = Theme.TextMain;
        HandleCreated += (_, _) => DwmWindow.ApplyModernDarkFrame(this);
        BuildLayout(fastLaunchDefault);
    }

    private void BuildLayout(bool fastLaunchDefault)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(18),
            BackColor = Theme.Bg
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var note = new Label
        {
            Text = "Credentials are used only to launch Steam and are not saved by this app.",
            AutoSize = false,
            Height = 38,
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextMuted
        };

        ConfigureInput(_usernameBox, "Username");
        ConfigureInput(_passwordBox, "Password");
        _passwordBox.UseSystemPasswordChar = true;

        _showPasswordCheck.Text = "Show password";
        _showPasswordCheck.AutoSize = true;
        _showPasswordCheck.ForeColor = Theme.TextMuted;
        _showPasswordCheck.BackColor = Theme.Bg;
        _showPasswordCheck.CheckedChanged += (_, _) => _passwordBox.UseSystemPasswordChar = !_showPasswordCheck.Checked;

        _fastLaunchCheck.Text = "Use fast launch flags";
        _fastLaunchCheck.AutoSize = true;
        _fastLaunchCheck.Checked = fastLaunchDefault;
        _fastLaunchCheck.ForeColor = Theme.TextMuted;
        _fastLaunchCheck.BackColor = Theme.Bg;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Theme.Bg
        };

        var loginButton = new RoundedButton { Text = "Login", Size = new Size(90, 32), DialogResult = DialogResult.OK, BackColor = Theme.Accent, ForeColor = Theme.TextMain, FlatStyle = FlatStyle.Flat };
        var cancelButton = new RoundedButton { Text = "Cancel", Size = new Size(90, 32), DialogResult = DialogResult.Cancel, BackColor = Theme.SurfaceAlt, ForeColor = Theme.TextMain, FlatStyle = FlatStyle.Flat };
        loginButton.FlatAppearance.BorderSize = 0;
        cancelButton.FlatAppearance.BorderSize = 0;
        loginButton.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_usernameBox.Text) || string.IsNullOrEmpty(_passwordBox.Text))
            {
                MessageBox.Show(this, "Enter both username and password.", "Missing credentials", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            Request = new CredentialLoginRequest(_usernameBox.Text.Trim(), _passwordBox.Text, _fastLaunchCheck.Checked);
        };

        buttons.Controls.Add(loginButton);
        buttons.Controls.Add(cancelButton);

        AcceptButton = loginButton;
        CancelButton = cancelButton;

        root.Controls.Add(note, 0, 0);
        root.Controls.Add(_usernameBox, 0, 1);
        root.Controls.Add(_passwordBox, 0, 2);
        root.Controls.Add(_showPasswordCheck, 0, 3);
        root.Controls.Add(_fastLaunchCheck, 0, 4);
        root.Controls.Add(buttons, 0, 5);
        Controls.Add(root);
    }

    private static void ConfigureInput(TextBox textBox, string placeholder)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 0, 0, 10);
        textBox.PlaceholderText = placeholder;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Theme.Surface;
        textBox.ForeColor = Theme.TextMain;
    }
}
