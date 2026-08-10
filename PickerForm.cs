using System.Drawing;
using System.Windows.Forms;

namespace MailtoPicker;

/// <summary>
/// A ListBox that reports when the user clicks the row that was already
/// selected. Intercepting WM_LBUTTONDOWN is the only way to see the selection
/// as it was *before* the click landed.
/// </summary>
internal sealed class AccountListBox : ListBox
{
    private const int WM_LBUTTONDOWN = 0x0201;

    public event EventHandler? SelectedItemClicked;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg != WM_LBUTTONDOWN)
        {
            base.WndProc(ref m);
            return;
        }

        int lParam = unchecked((int)(long)m.LParam);
        var point = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
        int clicked = IndexFromPoint(point);
        int selectedBefore = SelectedIndex;

        base.WndProc(ref m);

        if (clicked >= 0 && clicked == selectedBefore && SelectedIndex == clicked)
            SelectedItemClicked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The account chooser shown when a mailto: link is opened. Enter, or clicking
/// the highlighted row, sends; Esc cancels.
/// </summary>
internal sealed class PickerForm : Form
{
    private const int MaxVisibleRows = 10;

    private readonly AccountListBox _list = new();
    private readonly Label _recipient = new();
    private readonly Label _hint = new();
    private readonly int _initialIndex;

    public Account? SelectedAccount { get; private set; }

    public PickerForm(AppConfig config, MailtoRequest request)
    {
        // Everything below is in 96 DPI units. Auto-scaling converts it, but
        // only if it is declared while layout is suspended -- assigning
        // AutoScaleMode runs the scaling pass immediately, so declaring it on a
        // form with no controls yet scales nothing and then treats every later
        // measurement as pre-scaled.
        SuspendLayout();

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        _recipient.Text = "To: " + request.DescribeRecipient();
        _recipient.AutoEllipsis = true;
        _recipient.ForeColor = SystemColors.GrayText;
        _recipient.SetBounds(12, 10, 316, 18);

        _list.IntegralHeight = true;
        _list.DisplayMember = nameof(Account.Name);
        _list.SetBounds(12, 34, 316, 44);
        foreach (Account account in config.Accounts)
            _list.Items.Add(account);
        _list.SelectedItemClicked += (_, _) => Accept();
        _list.DoubleClick += (_, _) => Accept();

        _hint.Text = "Enter or click to open  ·  Esc to cancel";
        _hint.ForeColor = SystemColors.GrayText;
        _hint.SetBounds(12, 86, 316, 18);

        ClientSize = new Size(340, 114);
        Controls.Add(_recipient);
        Controls.Add(_list);
        Controls.Add(_hint);

        Text = "Send with which account?";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        KeyPreview = true;
        TopMost = true;
        KeyDown += OnKeyDown;
        Shown += (_, _) =>
        {
            Activate();
            _list.Focus();
        };

        // Pre-select the account used last time, falling back to the first one.
        // Applied in OnLoad, not here: a ListBox drops a SelectedIndex set
        // before its handle exists.
        Account? last = config.FindByAddress(config.LastUsedAddress);
        _initialIndex = last is null ? 0 : config.Accounts.IndexOf(last);

        ResumeLayout(performLayout: false);
        PerformLayout();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToRows();

        if (_list.Items.Count > 0)
            _list.SelectedIndex = Math.Clamp(_initialIndex, 0, _list.Items.Count - 1);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitToRows();
    }

    /// <summary>
    /// Auto-scaling handles Location and Size, but it cannot know that this
    /// list's height is meant to be a whole number of rows: ItemHeight follows
    /// the font, so a scaled pixel height drifts out of step with it and leaves
    /// the last row half-drawn or scrolled out of sight. Measure instead.
    /// </summary>
    private void FitToRows()
    {
        int rows = Math.Clamp(_list.Items.Count, 1, MaxVisibleRows);

        // IntegralHeight trims the slack back to a whole number of rows.
        _list.Height = (rows * _list.ItemHeight) + LogicalToDeviceUnits(8);
        _hint.Top = _list.Bottom + LogicalToDeviceUnits(8);
        ClientSize = new Size(ClientSize.Width, _hint.Bottom + LogicalToDeviceUnits(10));
        CenterToScreen();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                Accept();
                break;
            case Keys.Escape:
                e.Handled = true;
                e.SuppressKeyPress = true;
                DialogResult = DialogResult.Cancel;
                Close();
                break;
        }
    }

    private void Accept()
    {
        if (_list.SelectedItem is not Account account) return;
        SelectedAccount = account;
        DialogResult = DialogResult.OK;
        Close();
    }
}
