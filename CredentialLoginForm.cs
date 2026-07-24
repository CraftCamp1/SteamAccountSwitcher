namespace SteamAccountSwitcher;

public sealed class CredentialLoginForm : Form
{
    private readonly HistoryTextBox _usernameBox = new();
    private readonly HistoryTextBox _passwordBox = new();
    private readonly CheckBox _showPasswordCheck = new();
    private readonly CheckBox _fastLaunchCheck = new();
    private readonly Label _messageLabel = new();
    private readonly RoundedButton _loginButton = new();
    private readonly RoundedButton _cancelButton = new();
    private readonly Func<CredentialLoginRequest, IProgress<string>, CancellationToken, Task<CredentialLoginResult>> _loginAsync;
    private readonly CancellationTokenSource _closeCancellation = new();

    public CredentialLoginRequest? SuccessfulRequest { get; private set; }
    public CredentialLoginResult? Result { get; private set; }

    public CredentialLoginForm(
        bool fastLaunchDefault,
        Func<CredentialLoginRequest, IProgress<string>, CancellationToken, Task<CredentialLoginResult>> loginAsync)
    {
        _loginAsync = loginAsync;
        Text = "Login with Credentials";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 282);
        Font = new Font("Segoe UI", 10F);
        BackColor = Theme.Bg;
        ForeColor = Theme.TextMain;
        HandleCreated += (_, _) => DwmWindow.ApplyModernDarkFrame(this);
        FormClosed += (_, _) => _closeCancellation.Cancel();
        BuildLayout(fastLaunchDefault);
    }

    private void BuildLayout(bool fastLaunchDefault)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(18),
            BackColor = Theme.Bg
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        _fastLaunchCheck.Text = "Fast-close Steam before login";
        _fastLaunchCheck.AutoSize = true;
        _fastLaunchCheck.Checked = fastLaunchDefault;
        _fastLaunchCheck.ForeColor = Theme.TextMuted;
        _fastLaunchCheck.BackColor = Theme.Bg;

        _messageLabel.AutoSize = false;
        _messageLabel.Dock = DockStyle.Fill;
        _messageLabel.Height = 30;
        _messageLabel.ForeColor = Theme.TextMuted;
        _messageLabel.TextAlign = ContentAlignment.MiddleLeft;

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            BackColor = Theme.Bg
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _loginButton.Text = "Login";
        _loginButton.Size = new Size(90, 32);
        _loginButton.BackColor = Theme.Accent;
        _loginButton.ForeColor = Theme.TextMain;
        _loginButton.FlatStyle = FlatStyle.Flat;
        _cancelButton.Text = "Cancel";
        _cancelButton.Size = new Size(90, 32);
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.BackColor = Theme.SurfaceAlt;
        _cancelButton.ForeColor = Theme.TextMain;
        _cancelButton.FlatStyle = FlatStyle.Flat;
        _loginButton.FlatAppearance.BorderSize = 0;
        _cancelButton.FlatAppearance.BorderSize = 0;
        _loginButton.Click += async (_, _) => await SubmitAsync();
        _cancelButton.Click += (_, _) => Close();

        _cancelButton.Anchor = AnchorStyles.Left;
        _loginButton.Anchor = AnchorStyles.Right;
        buttons.Controls.Add(_cancelButton, 0, 0);
        buttons.Controls.Add(_loginButton, 1, 0);

        AcceptButton = _loginButton;
        CancelButton = _cancelButton;

        root.Controls.Add(note, 0, 0);
        root.Controls.Add(_usernameBox, 0, 1);
        root.Controls.Add(_passwordBox, 0, 2);
        root.Controls.Add(_showPasswordCheck, 0, 3);
        root.Controls.Add(_fastLaunchCheck, 0, 4);
        root.Controls.Add(_messageLabel, 0, 5);
        root.Controls.Add(buttons, 0, 6);
        Controls.Add(root);
    }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_usernameBox.Text) || string.IsNullOrEmpty(_passwordBox.Text))
        {
            ShowError("Enter both username and password.");
            return;
        }

        var request = new CredentialLoginRequest(_usernameBox.Text.Trim(), _passwordBox.Text, _fastLaunchCheck.Checked);
        SetBusy(true);
        var progress = new Progress<string>(message =>
        {
            _messageLabel.ForeColor = Theme.TextMuted;
            _messageLabel.Text = message;
        });

        try
        {
            var result = await _loginAsync(request, progress, _closeCancellation.Token);
            if (result.Status == CredentialLoginStatus.InvalidCredentials)
            {
                ShowError("Please check your password and account name and try again.");
                _passwordBox.Focus();
                _passwordBox.SelectAll();
                return;
            }

            SuccessfulRequest = request;
            Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) when (_closeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    public bool HandleDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        if (keyData == Keys.Enter && _loginButton.Enabled)
        {
            _ = SubmitAsync();
            return true;
        }

        return false;
    }

    private void ShowError(string message)
    {
        _messageLabel.ForeColor = Color.FromArgb(255, 96, 96);
        _messageLabel.Text = message;
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        _usernameBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _showPasswordCheck.Enabled = !busy;
        _fastLaunchCheck.Enabled = !busy;
        _loginButton.Enabled = !busy;
        _cancelButton.Enabled = true;
        UseWaitCursor = busy;
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
