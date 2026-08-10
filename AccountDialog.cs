using System.Drawing;
using System.Windows.Forms;

namespace MailtoPicker;

/// <summary>Add/edit dialog for a single account entry.</summary>
internal sealed class AccountDialog : Form
{
    private readonly Label _nameLabel = new();
    private readonly TextBox _name = new();
    private readonly Label _indexLabel = new();
    private readonly NumericUpDown _index = new();
    private readonly Button _ok = new();
    private readonly Button _cancel = new();

    public Account Result { get; private set; } = new();

    public AccountDialog(Account? existing)
    {
        // 96 DPI units throughout; see the note in PickerForm about why the
        // scaling declaration has to sit inside SuspendLayout/ResumeLayout.
        SuspendLayout();

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        _nameLabel.Text = "Display name";
        _nameLabel.SetBounds(12, 12, 336, 18);

        _name.Text = existing?.Name ?? "";
        _name.SetBounds(12, 32, 336, 23);

        _indexLabel.Text = "Account index (the N in mail.google.com/mail/u/N/)";
        _indexLabel.SetBounds(12, 66, 336, 18);

        _index.Minimum = 0;
        _index.Maximum = 99;
        _index.Value = existing?.AccountIndex ?? 0;
        _index.SetBounds(12, 86, 80, 23);

        _ok.Text = "OK";
        _ok.SetBounds(186, 122, 80, 27);
        _ok.DialogResult = DialogResult.OK;
        _ok.Click += OnOk;

        _cancel.Text = "Cancel";
        _cancel.SetBounds(272, 122, 80, 27);
        _cancel.DialogResult = DialogResult.Cancel;

        ClientSize = new Size(364, 161);
        Controls.AddRange(new Control[] { _nameLabel, _name, _indexLabel, _index, _ok, _cancel });

        Text = existing is null ? "Add account" : "Edit account";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AcceptButton = _ok;
        CancelButton = _cancel;

        ResumeLayout(performLayout: false);
        PerformLayout();
    }

    private void OnOk(object? sender, EventArgs e)
    {
        string name = _name.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Give the account a display name.", "Mailto Picker",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            _name.Focus();
            return;
        }

        Result = new Account { Name = name, AccountIndex = (int)_index.Value };
    }
}
