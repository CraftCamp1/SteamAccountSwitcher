namespace SteamAccountSwitcher;

public sealed class SearchInput : UserControl
{
    private readonly HistoryTextBox _textBox = new();

    public SearchInput()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = 32;
        BackColor = Theme.Bg;
        Padding = new Padding(10, 7, 10, 0);

        _textBox.BorderStyle = BorderStyle.None;
        _textBox.BackColor = Theme.Surface;
        _textBox.ForeColor = Theme.TextMain;
        _textBox.Font = new Font("Segoe UI", 9F);
        _textBox.Dock = DockStyle.Fill;
        _textBox.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        _textBox.KeyDown += (_, e) => OnKeyDown(e);
        Controls.Add(_textBox);
    }

    public string PlaceholderText
    {
        get => _textBox.PlaceholderText;
        set => _textBox.PlaceholderText = value;
    }

#pragma warning disable CS8765
    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value ?? string.Empty;
    }
#pragma warning restore CS8765

    public int TextLength => _textBox.TextLength;

    public void Clear()
    {
        _textBox.Clear();
    }

    public void SelectAll()
    {
        _textBox.SelectAll();
    }

    public new bool Focus()
    {
        return _textBox.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRectangle(rect, 8);
        using var background = new SolidBrush(Theme.Surface);
        using var border = new Pen(Theme.Line);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);
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
