namespace SteamAccountSwitcher;

public sealed class AccountTable : Control
{
    private const int HeaderHeight = 28;
    private const int RowHeight = 28;
    private const int ScrollbarWidth = 10;
    private readonly List<SteamAccount> _items = [];
    private int _scrollOffset;
    private int _selectedIndex = -1;

    public event EventHandler? SelectionConfirmed;

    public AccountTable()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);

        BackColor = Theme.Surface;
        ForeColor = Theme.TextMain;
        TabStop = true;
    }

    public IReadOnlyList<SteamAccount> Items => _items;

    public SteamAccount? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var next = _items.Count == 0 ? -1 : Math.Clamp(value, 0, _items.Count - 1);
            if (_selectedIndex == next)
            {
                return;
            }

            _selectedIndex = next;
            EnsureSelectedVisible();
            Invalidate();
        }
    }

    public void SetItems(IEnumerable<SteamAccount> items, string? preferredSteamId)
    {
        _items.Clear();
        _items.AddRange(items);
        _scrollOffset = 0;
        _selectedIndex = -1;

        if (!string.IsNullOrWhiteSpace(preferredSteamId))
        {
            var preferredIndex = _items.FindIndex(account => account.SteamId == preferredSteamId);
            if (preferredIndex >= 0)
            {
                _selectedIndex = preferredIndex;
            }
        }

        if (_selectedIndex < 0 && _items.Count > 0)
        {
            _selectedIndex = 0;
        }

        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Up or Keys.Down or Keys.PageDown or Keys.PageUp or Keys.Home or Keys.End
               || base.IsInputKey(keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Theme.Surface);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DrawOuterFrame(e.Graphics, bounds);
        DrawHeader(e.Graphics, bounds);
        DrawRows(e.Graphics, bounds);
        DrawScrollbar(e.Graphics, bounds);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button != MouseButtons.Left || e.Y < HeaderHeight)
        {
            return;
        }

        var index = _scrollOffset + ((e.Y - HeaderHeight) / RowHeight);
        if (index >= 0 && index < _items.Count)
        {
            SelectedIndex = index;
        }
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (SelectedItem is not null)
        {
            SelectionConfirmed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var direction = e.Delta > 0 ? -3 : 3;
        SetScrollOffset(_scrollOffset + direction);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Enter:
            case Keys.Space:
                if (SelectedItem is not null)
                {
                    SelectionConfirmed?.Invoke(this, EventArgs.Empty);
                    e.SuppressKeyPress = true;
                }
                break;
            case Keys.Up:
                SelectedIndex--;
                e.SuppressKeyPress = true;
                break;
            case Keys.Down:
                SelectedIndex++;
                e.SuppressKeyPress = true;
                break;
            case Keys.PageUp:
                SelectedIndex -= VisibleRowCount();
                e.SuppressKeyPress = true;
                break;
            case Keys.PageDown:
                SelectedIndex += VisibleRowCount();
                e.SuppressKeyPress = true;
                break;
            case Keys.Home:
                SelectedIndex = 0;
                e.SuppressKeyPress = true;
                break;
            case Keys.End:
                SelectedIndex = _items.Count - 1;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void DrawOuterFrame(Graphics graphics, Rectangle bounds)
    {
        using var border = new Pen(Theme.Line);
        graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    private static void DrawHeader(Graphics graphics, Rectangle bounds)
    {
        using var background = new SolidBrush(Theme.SurfaceAlt);
        graphics.FillRectangle(background, 1, 1, bounds.Width - 2, HeaderHeight - 1);

        using var brush = new SolidBrush(Theme.TextMuted);
        using var font = new Font("Segoe UI Semibold", 8.5F);
        DrawCells(graphics, ["Account", "Persona", "SteamID", "State"], font, brush, new Rectangle(1, 5, bounds.Width - ScrollbarWidth - 8, HeaderHeight - 7));
    }

    private void DrawRows(Graphics graphics, Rectangle bounds)
    {
        var visibleRows = VisibleRowCount();
        using var rowLine = new Pen(Theme.Line);
        using var selectedBackground = new SolidBrush(Theme.Selected);
        using var main = new SolidBrush(Theme.TextMain);
        using var muted = new SolidBrush(Theme.TextMuted);
        using var accent = new SolidBrush(Theme.AccentHover);
        using var font = new Font("Segoe UI", 9F);

        for (var row = 0; row < visibleRows; row++)
        {
            var index = _scrollOffset + row;
            if (index >= _items.Count)
            {
                break;
            }

            var y = HeaderHeight + row * RowHeight;
            var account = _items[index];
            var rowRect = new Rectangle(1, y, bounds.Width - ScrollbarWidth - 4, RowHeight);
            if (index == _selectedIndex)
            {
                graphics.FillRectangle(selectedBackground, rowRect);
            }

            DrawCells(
                graphics,
                [account.AccountName, account.PersonaName, account.SteamId, AccountStateText(account)],
                font,
                [main, muted, muted, account.RememberPassword ? accent : muted],
                new Rectangle(rowRect.Left + 10, y + 6, rowRect.Width - 18, RowHeight - 6));

            graphics.DrawLine(rowLine, rowRect.Left + 10, y + RowHeight - 1, rowRect.Right - 8, y + RowHeight - 1);
        }
    }

    private void DrawScrollbar(Graphics graphics, Rectangle bounds)
    {
        var rowCapacity = VisibleRowCount();
        if (_items.Count <= rowCapacity)
        {
            return;
        }

        var track = new Rectangle(bounds.Right - ScrollbarWidth - 3, HeaderHeight + 6, 5, bounds.Height - HeaderHeight - 12);
        using var trackBrush = new SolidBrush(Theme.SurfaceAlt);
        graphics.FillRectangle(trackBrush, track);

        var thumbHeight = Math.Max(28, (int)(track.Height * (rowCapacity / (float)_items.Count)));
        var maxScroll = Math.Max(1, _items.Count - rowCapacity);
        var thumbTop = track.Top + (int)((track.Height - thumbHeight) * (_scrollOffset / (float)maxScroll));
        var thumb = new Rectangle(track.Left, thumbTop, track.Width, thumbHeight);
        using var thumbBrush = new SolidBrush(Theme.TextMuted);
        graphics.FillRectangle(thumbBrush, thumb);
    }

    private static void DrawCells(Graphics graphics, string[] values, Font font, Brush brush, Rectangle bounds)
    {
        DrawCells(graphics, values, font, [brush, brush, brush, brush], bounds);
    }

    private static string AccountStateText(SteamAccount account)
    {
        if (!account.RememberPassword)
        {
            return "Login needed";
        }

        return account.MostRecent ? "Current" : string.Empty;
    }

    private static void DrawCells(Graphics graphics, string[] values, Font font, Brush[] brushes, Rectangle bounds)
    {
        var widths = new[] { .32f, .26f, .30f, .12f };
        var left = bounds.Left;
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        for (var i = 0; i < values.Length; i++)
        {
            var width = (int)(bounds.Width * widths[i]);
            graphics.DrawString(values[i], font, brushes[i], new RectangleF(left, bounds.Top, width - 8, bounds.Height), format);
            left += width;
        }
    }

    private int VisibleRowCount()
    {
        return Math.Max(1, (Height - HeaderHeight - 2) / RowHeight);
    }

    private void EnsureSelectedVisible()
    {
        if (_selectedIndex < 0)
        {
            return;
        }

        var visibleRows = VisibleRowCount();
        if (_selectedIndex < _scrollOffset)
        {
            SetScrollOffset(_selectedIndex);
        }
        else if (_selectedIndex >= _scrollOffset + visibleRows)
        {
            SetScrollOffset(_selectedIndex - visibleRows + 1);
        }
    }

    private void SetScrollOffset(int value)
    {
        var max = Math.Max(0, _items.Count - VisibleRowCount());
        var next = Math.Clamp(value, 0, max);
        if (_scrollOffset == next)
        {
            return;
        }

        _scrollOffset = next;
        Invalidate();
    }
}
