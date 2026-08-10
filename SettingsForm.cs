using System.Drawing;
using System.Windows.Forms;

namespace MailtoPicker;

/// <summary>Account list editor, shown when the app starts with no mailto: argument.</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly List<Account> _accounts;
    private readonly ListView _list = new();

    private readonly Button _add = new();
    private readonly Button _edit = new();
    private readonly Button _remove = new();
    private readonly Button _up = new();
    private readonly Button _down = new();
    private readonly Button _register = new();
    private readonly Button _unregister = new();
    private readonly Button _save = new();
    private readonly Button _cancel = new();

    public SettingsForm(AppConfig config)
    {
        _config = config;
        _accounts = config.Accounts.Select(a => a.Clone()).ToList();

        // 96 DPI units throughout; see the note in PickerForm about why the
        // scaling declaration has to sit inside SuspendLayout/ResumeLayout.
        SuspendLayout();

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = false;
        _list.Columns.Add("Display name");
        _list.Columns.Add("Index", -1, HorizontalAlignment.Right);
        _list.SetBounds(12, 12, 340, 252);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        Place(_add, "Add...", 364, 12, 140, 26, OnAdd);
        Place(_edit, "Edit...", 364, 44, 140, 26, (_, _) => EditSelected());
        Place(_remove, "Remove", 364, 76, 140, 26, OnRemove);
        Place(_up, "Move up", 364, 116, 140, 26, (_, _) => MoveSelected(-1));
        Place(_down, "Move down", 364, 148, 140, 26, (_, _) => MoveSelected(1));

        Place(_register, "Register as mailto handler...", 12, 276, 200, 28, OnRegister);
        Place(_unregister, "Unregister", 220, 276, 90, 28, OnUnregister);
        Place(_save, "Save", 348, 276, 75, 28, OnSave);

        _cancel.Text = "Cancel";
        _cancel.SetBounds(429, 276, 75, 28);
        _cancel.DialogResult = DialogResult.Cancel;

        ClientSize = new Size(516, 316);
        Controls.AddRange(new Control[]
        {
            _list, _add, _edit, _remove, _up, _down, _register, _unregister, _save, _cancel,
        });

        Text = "Mailto Picker settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        CancelButton = _cancel;

        ResumeLayout(performLayout: false);
        PerformLayout();

        Reload(0);
    }

    private static void Place(Button button, string text, int x, int y, int width, int height, EventHandler onClick)
    {
        button.Text = text;
        button.SetBounds(x, y, width, height);
        button.Click += onClick;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ScaleColumns();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ScaleColumns();
    }

    /// <summary>
    /// ListView column widths are one of the properties auto-scaling does not
    /// touch, so they stay at their literal pixel value unless set by hand.
    /// </summary>
    private void ScaleColumns()
    {
        _list.Columns[0].Width = LogicalToDeviceUnits(240);
        _list.Columns[1].Width = LogicalToDeviceUnits(75);
    }

    private void Reload(int selectIndex)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (Account account in _accounts)
        {
            var item = new ListViewItem(account.Name);
            item.SubItems.Add(account.AccountIndex.ToString());
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        if (_accounts.Count > 0)
        {
            int index = Math.Clamp(selectIndex, 0, _accounts.Count - 1);
            _list.Items[index].Selected = true;
            _list.Items[index].Focused = true;
        }
        UpdateButtons();
    }

    private int SelectedIndex => _list.SelectedIndices.Count == 0 ? -1 : _list.SelectedIndices[0];

    private void UpdateButtons()
    {
        int index = SelectedIndex;
        _edit.Enabled = _remove.Enabled = index >= 0;
        _up.Enabled = index > 0;
        _down.Enabled = index >= 0 && index < _accounts.Count - 1;
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        using var dialog = new AccountDialog(null);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _accounts.Add(dialog.Result);
        Reload(_accounts.Count - 1);
    }

    private void EditSelected()
    {
        int index = SelectedIndex;
        if (index < 0) return;

        using var dialog = new AccountDialog(_accounts[index]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // Keep "last used" pointing at this account even if it was renamed.
        if (string.Equals(_config.LastUsedAccount, _accounts[index].Name, StringComparison.OrdinalIgnoreCase))
            _config.LastUsedAccount = dialog.Result.Name;

        _accounts[index] = dialog.Result;
        Reload(index);
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        int index = SelectedIndex;
        if (index < 0) return;

        DialogResult answer = MessageBox.Show(this,
            $"Remove \"{_accounts[index].Name}\"?", "Mailto Picker",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        _accounts.RemoveAt(index);
        Reload(index);
    }

    private void MoveSelected(int delta)
    {
        int index = SelectedIndex;
        int target = index + delta;
        if (index < 0 || target < 0 || target >= _accounts.Count) return;

        (_accounts[index], _accounts[target]) = (_accounts[target], _accounts[index]);
        Reload(target);
    }

    private void OnRegister(object? sender, EventArgs e)
    {
        try
        {
            Registration.Register();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not write the registry entries:\r\n\r\n" + ex.Message,
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult answer = MessageBox.Show(this,
            "Registered as a mailto: handler candidate.\r\n\r\n" +
            "Windows will not let an app make itself the default, so open " +
            "Settings > Apps > Default apps, search for \"Mailto Picker\", and set it " +
            "as the handler for MAILTO.\r\n\r\nOpen that page now?",
            "Mailto Picker", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (answer != DialogResult.Yes) return;

        try
        {
            Registration.OpenDefaultAppsSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open Windows Settings:\r\n\r\n" + ex.Message,
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnUnregister(object? sender, EventArgs e)
    {
        DialogResult answer = MessageBox.Show(this,
            "Remove this app's mailto: registry entries?", "Mailto Picker",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        try
        {
            Registration.Unregister();
            MessageBox.Show(this, "Registry entries removed.", "Mailto Picker",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not remove the registry entries:\r\n\r\n" + ex.Message,
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_accounts.Count == 0)
        {
            MessageBox.Show(this, "Add at least one account before saving.", "Mailto Picker",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Accounts = _accounts.Select(a => a.Clone()).ToList();
        if (_config.FindByName(_config.LastUsedAccount) is null)
            _config.LastUsedAccount = _config.Accounts[0].Name;

        try
        {
            _config.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save {AppConfig.FilePath}:\r\n\r\n{ex.Message}",
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
