namespace SteamAccountSwitcher;

public sealed class MainForm : Form
{
    private readonly SteamAccountService _service;
    private readonly List<SteamAccount> _allAccounts = [];
    private readonly List<SteamAccount> _visibleAccounts = [];
    private readonly SearchInput _searchBox = new();
    private readonly AccountTable _accountTable = new();
    private readonly Button _switchButton = new RoundedButton();
    private readonly Button _loginButton = new RoundedButton();
    private readonly Button _refreshButton = new RoundedButton();
    private readonly CheckBox _fastLaunchCheck = new();
    private readonly CheckBox _startSteamCheck = new();
    private readonly Label _countLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _currentLabel = new();
    private Control? _mainContent;
    private CredentialLoginForm? _loginView;
    private bool _loginMode;
    public MainForm(SteamAccountService service)
    {
        _service = service;
        ConfigureWindow();
        BuildLayout();
        Load += (_, _) => RefreshAccounts();
    }

    private void ConfigureWindow()
    {
        Text = "Steam Account Switcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 460);
        Size = new Size(900, 560);
        Font = new Font("Segoe UI", 9F);
        BackColor = Theme.Bg;
        ForeColor = Theme.TextMain;
        KeyPreview = true;
        HandleCreated += (_, _) => DwmWindow.ApplyModernDarkFrame(this);
        KeyDown += (_, e) =>
        {
            if (_loginMode)
            {
                return;
            }

            if (e.Control && e.KeyCode == Keys.F)
            {
                FocusSearch();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.L)
            {
                _ = LoginWithCredentialsAsync();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.F5)
            {
                RefreshAccounts();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape && !_searchBox.Focused)
            {
                FocusSearch();
                e.SuppressKeyPress = true;
            }
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_loginMode && _loginView?.HandleDialogKey(keyData) == true)
        {
            return true;
        }

        if (!_loginMode && keyData == Keys.Enter)
        {
            if (_switchButton.Enabled && _visibleAccounts.Count > 0)
            {
                if (_accountTable.SelectedItem is null)
                {
                    _accountTable.SelectedIndex = 0;
                }

                _ = SwitchSelectedAccountAsync(forceStartSteam: true);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(BuildTitle(), 0, 0);
        root.Controls.Add(BuildSearch(), 0, 1);
        root.Controls.Add(BuildList(), 0, 2);
        root.Controls.Add(BuildActions(), 0, 3);
        root.Controls.Add(BuildStatus(), 0, 4);
        _mainContent = root;
        Controls.Add(root);
    }

    private Control BuildTitle()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        var title = new Label
        {
            Text = "Steam Account Switcher",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Theme.TextMain,
            Location = new Point(0, 4)
        };
        _countLabel.Dock = DockStyle.Right;
        _countLabel.Width = 120;
        _countLabel.TextAlign = ContentAlignment.MiddleRight;
        _countLabel.ForeColor = Theme.TextMuted;
        panel.Controls.Add(title);
        panel.Controls.Add(_countLabel);
        return panel;
    }

    private Control BuildSearch()
    {
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Margin = new Padding(0, 4, 0, 6);
        _searchBox.PlaceholderText = "Search accounts...";
        _searchBox.TextChanged += (_, _) => ApplySearch();
        _searchBox.KeyDown += SearchBoxKeyDown;
        return _searchBox;
    }

    private Control BuildList()
    {
        _accountTable.Dock = DockStyle.Fill;
        _accountTable.Margin = new Padding(0, 4, 0, 0);
        _accountTable.SelectionConfirmed += async (_, _) => await SwitchSelectedAccountAsync();
        _accountTable.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                FocusSearch();
                e.SuppressKeyPress = true;
            }
        };
        return _accountTable;
    }

    private Control BuildActions()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        ConfigureButton(_switchButton, "Switch && Start", true);
        ConfigureButton(_loginButton, "Login...", false);
        ConfigureButton(_refreshButton, "Refresh", false);
        ConfigureCheck(_fastLaunchCheck, "Fast switch", true);
        ConfigureCheck(_startSteamCheck, "Start Steam", true);

        _switchButton.Click += async (_, _) => await SwitchSelectedAccountAsync();
        _loginButton.Click += async (_, _) => await LoginWithCredentialsAsync();
        _refreshButton.Click += (_, _) => RefreshAccounts();

        panel.Controls.Add(_switchButton);
        panel.Controls.Add(_loginButton);
        panel.Controls.Add(_refreshButton);
        panel.Controls.Add(_fastLaunchCheck);
        panel.Controls.Add(_startSteamCheck);
        return panel;
    }

    private Control BuildStatus()
    {
        var status = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, ColumnCount = 2, RowCount = 2, Padding = new Padding(10, 5, 10, 5) };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Theme.TextMain;
        _statusLabel.Text = "Loading...";
        _currentLabel.Dock = DockStyle.Fill;
        _currentLabel.ForeColor = Theme.TextMuted;

        var keys = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextMuted, Text = "Enter/Space switch   Ctrl+L login   Ctrl+F search   F5 refresh" };
        var path = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextMuted, AutoEllipsis = true, Text = _service.Paths.SteamExe };

        status.Controls.Add(_statusLabel, 0, 0);
        status.Controls.Add(keys, 1, 0);
        status.Controls.Add(_currentLabel, 0, 1);
        status.Controls.Add(path, 1, 1);
        return status;
    }

    private static void ConfigureButton(Button button, string text, bool primary)
    {
        button.Text = text;
        button.Size = primary ? new Size(122, 28) : new Size(82, 28);
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? Theme.Accent : Theme.SurfaceAlt;
        button.ForeColor = Theme.TextMain;
        button.Font = new Font("Segoe UI", 9F);
        button.Cursor = Cursors.Hand;
    }

    private static void ConfigureCheck(CheckBox checkBox, string text, bool isChecked)
    {
        checkBox.Text = text;
        checkBox.Checked = isChecked;
        checkBox.AutoSize = true;
        checkBox.ForeColor = Theme.TextMuted;
        checkBox.BackColor = Theme.Bg;
        checkBox.Margin = new Padding(8, 5, 8, 0);
    }

    private void RefreshAccounts()
    {
        try
        {
            var selectedSteamId = _accountTable.SelectedItem?.SteamId;
            _allAccounts.Clear();
            _allAccounts.AddRange(_service.LoadAccounts());
            ApplySearch(selectedSteamId);
            SetBusy(false);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            SetBusy(true, allowRefresh: true);
            MessageBox.Show(this, ex.Message, "Could not load Steam accounts", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SwitchSelectedAccountAsync(bool forceStartSteam = false)
    {
        if (_accountTable.SelectedItem is not SteamAccount account)
        {
            MessageBox.Show(this, "Choose an account first.", "No account selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        try
        {
            var options = new SwitchOptions(forceStartSteam || _startSteamCheck.Checked, _fastLaunchCheck.Checked);
            var progress = new Progress<string>(message => _statusLabel.Text = message);
            await _service.SwitchToAsync(account, options, progress, CancellationToken.None);
            RefreshAccounts();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Switch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetBusy(false);
        }
    }

    private async Task LoginWithCredentialsAsync()
    {
        var leaveSteamRunning = _startSteamCheck.Checked;
        CredentialLoginSession? session = null;
        using var form = new CredentialLoginForm(
            _fastLaunchCheck.Checked,
            async (request, progress, cancellationToken) =>
            {
                SetBusy(true);
                session = await _service.LoginWithCredentialsAsync(request, progress, cancellationToken);
                return await _service.WaitForCredentialLoginAttemptAsync(
                    session,
                    TimeSpan.FromSeconds(25),
                    progress,
                    cancellationToken);
            });

        var mainContent = _mainContent ?? throw new InvalidOperationException("The main view is not initialized.");
        var previousBounds = Bounds;
        var previousMinimumSize = MinimumSize;
        var previousMaximumSize = MaximumSize;
        var previousTitle = Text;
        var previousCenter = new Point(previousBounds.Left + (previousBounds.Width / 2), previousBounds.Top + (previousBounds.Height / 2));
        var closed = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        var loginClientSize = form.ClientSize;
        form.Dock = DockStyle.None;
        form.Size = loginClientSize;
        var closingTransitionStarted = false;
        form.FormClosing += (_, _) =>
        {
            DwmWindow.SuspendRedraw(this);
            closingTransitionStarted = true;
            SuspendLayout();
            Text = previousTitle;
            Bounds = previousBounds;
            MinimumSize = previousMinimumSize;
            MaximumSize = previousMaximumSize;
            mainContent.Visible = true;
            mainContent.BringToFront();
        };
        form.FormClosed += (_, _) =>
        {
            Controls.Remove(form);
            _loginView = null;
            _loginMode = false;
            ResumeLayout(performLayout: true);
            if (closingTransitionStarted)
            {
                DwmWindow.ResumeRedraw(this);
            }

            closed.TrySetResult(form.DialogResult);
        };

        // Build and paint the login control tree while it is covered by the main view.
        Controls.Add(form);
        mainContent.BringToFront();
        form.Show();
        form.PerformLayout();
        form.Update();

        DwmWindow.UpdateAtomically(this, () =>
        {
            _loginMode = true;
            _loginView = form;
            mainContent.Visible = false;
            Text = "Login with Credentials";
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            ClientSize = loginClientSize;
            Location = new Point(previousCenter.X - (Width / 2), previousCenter.Y - (Height / 2));
            form.Dock = DockStyle.Fill;
            form.BringToFront();
        });

        DialogResult dialogResult;
        try
        {
            dialogResult = await closed.Task;
        }
        finally
        {
            Activate();
        }

        SetBusy(false);
        if (dialogResult != DialogResult.OK || form.SuccessfulRequest is null || form.Result is null)
        {
            return;
        }

        if (form.Result.Status == CredentialLoginStatus.SignedIn)
        {
            await CompleteCredentialLoginAsync(form.Result.Account!, leaveSteamRunning);
            return;
        }

        if (form.Result.Status == CredentialLoginStatus.SteamGuardRequired)
        {
            var handoffStarted = false;
            SetBusy(true);
            try
            {
                var progress = new Progress<string>(message => _statusLabel.Text = message);
                await _service.OpenInteractiveLoginAsync(
                    form.SuccessfulRequest.Username,
                    form.SuccessfulRequest.FastLaunch,
                    progress,
                    CancellationToken.None);
                handoffStarted = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Steam Guard handoff failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }

            if (handoffStarted)
            {
                _ = WatchCredentialLoginSaveAsync(form.SuccessfulRequest.Username, leaveSteamRunning);
            }

            return;
        }

        _statusLabel.Text = $"Steam is waiting for {form.SuccessfulRequest.Username} to finish sign-in.";
        _ = WatchCredentialLoginSaveAsync(form.SuccessfulRequest.Username, leaveSteamRunning);
    }

    private async Task WatchCredentialLoginSaveAsync(string username, bool leaveSteamRunning)
    {
        try
        {
            var progress = new Progress<string>(message => _statusLabel.Text = message);
            var savedAccount = await _service.WaitForCredentialLoginToPersistAsync(username, progress, CancellationToken.None);
            if (savedAccount is null)
            {
                RefreshAccounts();
                _statusLabel.Text = $"Steam did not confirm sign-in for {username}. Complete any Steam prompt, then refresh.";
                return;
            }

            await CompleteCredentialLoginAsync(savedAccount, leaveSteamRunning);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
        }
    }

    private async Task CompleteCredentialLoginAsync(SteamAccount account, bool leaveSteamRunning)
    {
        if (!leaveSteamRunning)
        {
            SetBusy(true);
            try
            {
                var progress = new Progress<string>(message => _statusLabel.Text = message);
                await _service.CloseSteamAfterLoginAsync(progress, CancellationToken.None);
            }
            finally
            {
                SetBusy(false);
            }
        }

        RefreshAccounts();
        _statusLabel.Text = leaveSteamRunning
            ? $"Steam saved {account.AccountName}."
            : $"Steam saved {account.AccountName} and closed.";
    }

    private void ApplySearch(string? preferredSteamId = null)
    {
        var query = (_searchBox.Text ?? string.Empty).Trim();
        var selectedSteamId = preferredSteamId ?? _accountTable.SelectedItem?.SteamId;
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allAccounts
            : _allAccounts.Where(account => Contains(account.AccountName, query) || Contains(account.PersonaName, query) || Contains(account.SteamId, query)).ToList();

        _visibleAccounts.Clear();
        _visibleAccounts.AddRange(matches);
        _accountTable.SetItems(_visibleAccounts, selectedSteamId);

        var current = _allAccounts.FirstOrDefault(account => account.MostRecent);
        _countLabel.Text = string.IsNullOrWhiteSpace(query) ? $"{_allAccounts.Count} accounts" : $"{_visibleAccounts.Count}/{_allAccounts.Count}";
        _currentLabel.Text = current is null ? "Current: none" : $"Current: {current.AccountName}";
        _statusLabel.Text = string.IsNullOrWhiteSpace(query) ? "Ready" : $"Filtered: {query}";
        SetBusy(false);
    }

    private void SearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _searchBox.TextLength > 0)
        {
            _searchBox.Clear();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Down && _visibleAccounts.Count > 0)
        {
            _accountTable.Focus();
            if (_accountTable.SelectedIndex < 0) _accountTable.SelectedIndex = 0;
            e.SuppressKeyPress = true;
        }
    }

    private void SetBusy(bool busy, bool allowRefresh = false)
    {
        _accountTable.Enabled = !busy;
        _searchBox.Enabled = !busy;
        _switchButton.Enabled = !busy && _visibleAccounts.Count > 0;
        _loginButton.Enabled = !busy;
        _refreshButton.Enabled = !busy || allowRefresh;
        _fastLaunchCheck.Enabled = !busy;
        _startSteamCheck.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void FocusSearch()
    {
        _searchBox.Focus();
        _searchBox.SelectAll();
    }

    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class RoundedButton : Button
{
    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRectangle(rect, 7);
        using var background = new SolidBrush(Enabled ? BackColor : Theme.SurfaceAlt);
        using var border = new Pen(Theme.Line);
        using var text = new SolidBrush(Enabled ? ForeColor : Theme.TextMuted);

        pevent.Graphics.FillPath(background, path);
        if (BackColor != Theme.Accent)
        {
            pevent.Graphics.DrawPath(border, path);
        }

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            rect,
            Enabled ? ForeColor : Theme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
