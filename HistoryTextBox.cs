namespace SteamAccountSwitcher;

internal sealed class HistoryTextBox : TextBox
{
    private const int MaxHistoryEntries = 200;
    private readonly List<string> _undoHistory = [];
    private readonly List<string> _redoHistory = [];
    private string _previousText = string.Empty;
    private bool _restoringHistory;

    protected override void OnTextChanged(EventArgs e)
    {
        if (!_restoringHistory && !string.Equals(Text, _previousText, StringComparison.Ordinal))
        {
            Push(_undoHistory, _previousText);
            _redoHistory.Clear();
        }

        _previousText = Text;
        base.OnTextChanged(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            RestoreFrom(_undoHistory, _redoHistory);
            return true;
        }

        if (keyData is (Keys.Control | Keys.Y) or (Keys.Control | Keys.Shift | Keys.Z))
        {
            RestoreFrom(_redoHistory, _undoHistory);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RestoreFrom(List<string> source, List<string> destination)
    {
        if (source.Count == 0)
        {
            return;
        }

        Push(destination, Text);
        var lastIndex = source.Count - 1;
        var restoredText = source[lastIndex];
        source.RemoveAt(lastIndex);

        _restoringHistory = true;
        try
        {
            Text = restoredText;
            SelectionStart = TextLength;
            SelectionLength = 0;
            _previousText = restoredText;
        }
        finally
        {
            _restoringHistory = false;
        }
    }

    private static void Push(List<string> history, string value)
    {
        if (history.Count > 0 && string.Equals(history[^1], value, StringComparison.Ordinal))
        {
            return;
        }

        if (history.Count == MaxHistoryEntries)
        {
            history.RemoveAt(0);
        }

        history.Add(value);
    }
}
