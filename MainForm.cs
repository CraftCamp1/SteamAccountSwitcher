using System.ComponentModel;

namespace SteamAccountSwitcher;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(248, 249, 251);
    private static readonly Color Line = Color.FromArgb(222, 226, 232);
    private static readonly Color Header = Color.FromArgb(242, 245, 249);
    private static readonly Color Selected = Color.FromArgb(219, 235, 255);
    private static readonly Color Hover = Color.FromArgb(235, 242, 252);
    private static readonly Color TextMain = Color.FromArgb(28, 32, 38);
    private static readonly Color TextMuted = Color.FromArgb(99, 110, 125);
    private static readonly Color Accent = Color.FromArgb(32, 111, 230);

    private readonly SteamAccountService _service;
    private readonly List<SteamAccount> _allAccounts = [];
    private readonly BindingList<SteamAccount> _visibleAccounts = [];
    private readonly TextBox _searchBox = new();
    private readonly ListBox _accountList = new FlickerFreeListBox();
    private readonly Button _switchButton = new();
    private readonly Button _loginButton = new();
    private readonly Button _refreshButton = new();
    private readonly CheckBox _fastLaunchCheck = new();
    private readonly CheckBox _startSteamCheck = new();
    private readonly Label _countLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _currentLabel = new();
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
        BackColor = Bg;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
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

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(BuildTitle(), 0, 0);
        root.Controls.Add(BuildSearch(), 0, 1);
        root.Controls.Add(BuildColumns(), 0, 2);
        root.Controls.Add(BuildList(), 0, 3);
        root.Controls.Add(BuildActions(), 0, 4);
        root.Controls.Add(BuildStatus(), 0, 5);
        Controls.Add(root);
    }

    private Control BuildTitle()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
        var title = new Label
        {
            Text = "Steam Account Switcher",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = TextMain,
            Location = new Point(0, 4)
        };
        _countLabel.Dock = DockStyle.Right;
        _countLabel.Width = 120;
        _countLabel.TextAlign = ContentAlignment.MiddleRight;
        _countLabel.ForeColor = TextMuted;
        panel.Controls.Add(title);
        panel.Controls.Add(_countLabel);
        return panel;
    }

    private Control BuildSearch()
    {
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Margin = new Padding(0, 2, 0, 6);
        _searchBox.PlaceholderText = "Search accounts...";
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.Font = new Font("Segoe UI", 9F);
        _searchBox.TextChanged += (_, _) => ApplySearch();
        _searchBox.KeyDown += SearchBoxKeyDown;
        return _searchBox;
    }

    private static Control BuildColumns()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Header, ColumnCount = 4, Padding = new Padding(8, 3, 8, 0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        foreach (var text in new[] { "Account", "Persona", "SteamID", "State" })
        {
            row.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8.5F) });
        }
        return row;
    }

    private Control BuildList()
    {
        _accountList.Dock = DockStyle.Fill;
        _accountList.Margin = Padding.Empty;
        _accountList.DataSource = _visibleAccounts;
        _accountList.DisplayMember = nameof(SteamAccount.DisplayName);
        _accountList.DrawMode = DrawMode.OwnerDrawFixed;
        _accountList.ItemHeight = 22;
        _accountList.IntegralHeight = false;
        _accountList.BorderStyle = BorderStyle.FixedSingle;
        _accountList.BackColor = Color.White;
        _accountList.DrawItem += DrawAccountRow;
        _accountList.DoubleClick += async (_, _) => await SwitchSelectedAccountAsync();
        _accountList.KeyDown += async (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.SuppressKeyPress = true;
                await SwitchSelectedAccountAsync();
                return;
            }

            if (e.KeyCode == Keys.F2)
            {
                FocusSearch();
                e.SuppressKeyPress = true;
            }
        };
        return _accountList;
    }

    private Control BuildActions()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        ConfigureButton(_switchButton, "Switch && Start", true);
        ConfigureButton(_loginButton, "Login...", false);
        ConfigureButton(_refreshButton, "Refresh", false);
        ConfigureCheck(_fastLaunchCheck, "Fast launch", true);
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
        var status = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, ColumnCount = 2, RowCount = 2, Padding = new Padding(8, 4, 8, 4) };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = TextMain;
        _statusLabel.Text = "Loading...";
        _currentLabel.Dock = DockStyle.Fill;
        _currentLabel.ForeColor = TextMuted;

        var keys = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = TextMuted, Text = "Enter/Space switch   Ctrl+L login   Ctrl+F search   F5 refresh" };
        var path = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = TextMuted, AutoEllipsis = true, Text = _service.Paths.SteamExe };

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
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Line;
        button.BackColor = primary ? Accent : Color.White;
        button.ForeColor = primary ? Color.White : TextMain;
        button.Font = new Font("Segoe UI", 9F);
    }

    private static void ConfigureCheck(CheckBox checkBox, string text, bool isChecked)
    {
        checkBox.Text = text;
        checkBox.Checked = isChecked;
        checkBox.AutoSize = true;
        checkBox.ForeColor = TextMuted;
        checkBox.Margin = new Padding(8, 5, 8, 0);
    }

    private void DrawAccountRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _visibleAccounts.Count) return;

        var account = _visibleAccounts[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected ? Selected : Color.White;

        using var bg = new SolidBrush(background);
        e.Graphics.FillRectangle(bg, e.Bounds);

        var widths = new[] { .32f, .26f, .30f, .12f };
        var left = e.Bounds.Left + 8;
        var total = e.Bounds.Width - 16;
        var y = e.Bounds.Top + 3;
        using var main = new SolidBrush(TextMain);
        using var muted = new SolidBrush(TextMuted);
        using var accent = new SolidBrush(Accent);
        using var font = new Font("Segoe UI", 9F);

        DrawCell(e.Graphics, account.AccountName, font, main, left, y, total * widths[0]);
        left += (int)(total * widths[0]);
        DrawCell(e.Graphics, account.PersonaName, font, muted, left, y, total * widths[1]);
        left += (int)(total * widths[1]);
        DrawCell(e.Graphics, account.SteamId, font, muted, left, y, total * widths[2]);
        left += (int)(total * widths[2]);
        DrawCell(e.Graphics, account.MostRecent ? "Current" : string.Empty, font, accent, left, y, total * widths[3]);

        using var pen = new Pen(Line);
        e.Graphics.DrawLine(pen, e.Bounds.Left + 6, e.Bounds.Bottom - 1, e.Bounds.Right - 6, e.Bounds.Bottom - 1);
        e.DrawFocusRectangle();
    }

    private static void DrawCell(Graphics graphics, string text, Font font, Brush brush, int x, int y, float width)
    {
        var rect = new RectangleF(x, y, width - 8, 18);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private void RefreshAccounts()
    {
        try
        {
            var selectedSteamId = (_accountList.SelectedItem as SteamAccount)?.SteamId;
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

    private async Task SwitchSelectedAccountAsync()
    {
        if (_accountList.SelectedItem is not SteamAccount account)
        {
            MessageBox.Show(this, "Choose an account first.", "No account selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        try
        {
            var options = new SwitchOptions(_startSteamCheck.Checked, _fastLaunchCheck.Checked);
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
        using var form = new CredentialLoginForm(_fastLaunchCheck.Checked);
        if (form.ShowDialog(this) != DialogResult.OK || form.Request is null) return;

        SetBusy(true);
        try
        {
            var progress = new Progress<string>(message => _statusLabel.Text = message);
            await _service.LoginWithCredentialsAsync(form.Request, progress, CancellationToken.None);
            RefreshAccounts();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetBusy(false);
        }
    }

    private void ApplySearch(string? preferredSteamId = null)
    {
        var query = _searchBox.Text.Trim();
        var selectedSteamId = preferredSteamId ?? (_accountList.SelectedItem as SteamAccount)?.SteamId;
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allAccounts
            : _allAccounts.Where(account => Contains(account.AccountName, query) || Contains(account.PersonaName, query) || Contains(account.SteamId, query)).ToList();

        _visibleAccounts.RaiseListChangedEvents = false;
        _visibleAccounts.Clear();
        foreach (var account in matches) _visibleAccounts.Add(account);
        _visibleAccounts.RaiseListChangedEvents = true;
        _visibleAccounts.ResetBindings();

        if (!string.IsNullOrWhiteSpace(selectedSteamId))
        {
            var index = _visibleAccounts.ToList().FindIndex(account => account.SteamId == selectedSteamId);
            if (index >= 0) _accountList.SelectedIndex = index;
        }
        else if (_visibleAccounts.Count > 0)
        {
            _accountList.SelectedIndex = 0;
        }

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

        if (e.KeyCode is Keys.Enter or Keys.Down && _visibleAccounts.Count > 0)
        {
            _accountList.Focus();
            if (_accountList.SelectedIndex < 0) _accountList.SelectedIndex = 0;
            e.SuppressKeyPress = true;
        }
    }

    private void SetBusy(bool busy, bool allowRefresh = false)
    {
        _accountList.Enabled = !busy;
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

file sealed class FlickerFreeListBox : ListBox
{
    public FlickerFreeListBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        UpdateStyles();
    }
}
