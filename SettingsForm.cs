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
    private readonly Label _status = new();

    private readonly RegistrationStatus _registrationStatus;
    private readonly string? _registrationError;

    public SettingsForm(AppConfig config, RegistrationStatus status, string? registrationError)
    {
        _config = config;
        _accounts = config.Accounts.Select(a => a.Clone()).ToList();
        _registrationStatus = status;
        _registrationError = registrationError;

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
        _list.Columns.Add("Gmail address");
        _list.SetBounds(12, 12, 340, 252);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        Place(_add, "Add...", 364, 12, 140, 26, OnAdd);
        Place(_edit, "Edit...", 364, 44, 140, 26, (_, _) => EditSelected());
        Place(_remove, "Remove", 364, 76, 140, 26, OnRemove);
        Place(_up, "Move up", 364, 116, 140, 26, (_, _) => MoveSelected(-1));
        Place(_down, "Move down", 364, 148, 140, 26, (_, _) => MoveSelected(1));

        _status.AutoEllipsis = true;
        _status.SetBounds(12, 272, 492, 18);

        Place(_register, "Set as default mail handler...", 12, 300, 200, 28, OnRegister);
        Place(_unregister, "Unregister", 220, 300, 90, 28, OnUnregister);
        Place(_save, "Save", 348, 300, 75, 28, OnSave);

        _cancel.Text = "Cancel";
        _cancel.SetBounds(429, 300, 75, 28);
        _cancel.DialogResult = DialogResult.Cancel;

        ClientSize = new Size(516, 340);
        Controls.AddRange(new Control[]
        {
            _list, _add, _edit, _remove, _up, _down, _status, _register, _unregister, _save, _cancel,
        });

        Text = "Mailto Picker settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        CancelButton = _cancel;

        ResumeLayout(performLayout: false);
        PerformLayout();

        ShowStatus();
        Reload(0);
    }

    /// <summary>
    /// Reports whether Windows is actually routing mail links here, which is the
    /// state the user cares about. Being registered only makes the app
    /// selectable, so saying "registered" while every mailto link goes elsewhere
    /// would be technically true and completely useless.
    /// </summary>
    private void ShowStatus()
    {
        if (_registrationStatus == RegistrationStatus.Failed)
        {
            Set("Could not write the registry entries. " + _registrationError, Color.Firebrick);
            return;
        }

        if (!Registration.IsEffectiveHandler())
        {
            Set("Windows is still sending mail links elsewhere. Use \"Set as default mail handler\".",
                Color.Firebrick);
            return;
        }

        string note = _registrationStatus switch
        {
            RegistrationStatus.Created => " Registry entries were just created.",
            RegistrationStatus.Repaired => " The stored path was stale and now points at this copy.",
            RegistrationStatus.OtherCopy => $" Registered to another copy: {Registration.RegisteredExePath()}",
            _ => "",
        };

        Set("Mailto Picker is handling mail links." + note, SystemColors.GrayText);

        void Set(string text, Color colour)
        {
            _status.Text = text;
            _status.ForeColor = colour;
        }
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
        _list.Columns[0].Width = LogicalToDeviceUnits(135);
        _list.Columns[1].Width = LogicalToDeviceUnits(190);
    }

    private void Reload(int selectIndex)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (Account account in _accounts)
        {
            var item = new ListViewItem(account.Name);
            item.SubItems.Add(account.EmailAddress);
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

        if (_accounts.Count == 1)
        {
            MessageBox.Show(this,
                "This is the only account. Add another before removing this one.",
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Both prompts live here rather than before the window opens, so they
        // have an owner and the user can see what they refer to behind them.
        if (!EnsureFirstAccount())
        {
            DialogResult = DialogResult.Cancel;   // closes a modal form
            return;
        }

        RegistrationPrompt.ReportIfFailed(this, _registrationStatus, _registrationError);

        // Declining does not close the window: the user may well have opened
        // settings to manage accounts, and throwing them out over an unrelated
        // question would make that impossible.
        RegistrationPrompt.EnsureDefaultHandler(this);
        ShowStatus();
    }

    /// <summary>
    /// Demands an account before anything else. Without one there is nowhere to
    /// send mail, so an empty list is not a state worth letting the user sit in.
    /// </summary>
    /// <returns>False if the user declined, in which case there is nothing to do.</returns>
    private bool EnsureFirstAccount()
    {
        if (_accounts.Count > 0) return true;

        using (var dialog = new AccountDialog(null))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                MessageBox.Show(this,
                    "Mailto Picker needs at least one Gmail account before it can send " +
                    "anything.\r\n\r\nRun it again when you are ready to add one.",
                    "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _accounts.Add(dialog.Result);
        }

        // Committed straight away rather than left pending on the Save button:
        // the user was made to create this, so it should not evaporate if they
        // close the window.
        _config.Accounts = _accounts.Select(a => a.Clone()).ToList();
        _config.LastUsedAccount = _config.Accounts[0].Name;
        try
        {
            _config.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save {AppConfig.FilePath}:\r\n\r\n{ex.Message}",
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        Reload(0);
        return true;
    }

    private void OnRegister(object? sender, EventArgs e)
    {
        RegistrationPrompt.EnsureDefaultHandler(this);
        ShowStatus();
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
            ShowStatus();
            MessageBox.Show(this,
                "Registry entries removed.\r\n\r\n" +
                "They will be written again the next time this app runs, since it " +
                "registers itself on startup. Delete the app to be rid of it.",
                "Mailto Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
