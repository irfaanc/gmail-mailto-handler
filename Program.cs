using System.Diagnostics;
using System.Windows.Forms;

namespace MailtoPicker;

internal static class Program
{
    private const string Title = "Mailto Picker";

    [STAThread]
    private static int Main(string[] args)
    {
        // Generated from the Application* properties in the .csproj, including
        // PerMonitorV2 DPI awareness.
        ApplicationConfiguration.Initialize();

        try
        {
            if (args.Length == 0 || IsSettingsFlag(args[0]))
                return RunSettings();

            if (!MailtoRequest.IsMailto(args[0]))
            {
                ShowError(
                    "This app expects a mailto: link.\r\n\r\nIt was started with:\r\n" +
                    args[0] + "\r\n\r\nRun it with no arguments to edit accounts.");
                return 2;
            }

            return RunPicker(args[0]);
        }
        catch (Exception ex)
        {
            ShowError("Something went wrong:\r\n\r\n" + ex.Message);
            return 1;
        }
    }

    private static bool IsSettingsFlag(string arg) =>
        arg.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("-settings", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("/settings", StringComparison.OrdinalIgnoreCase);

    private static int RunSettings()
    {
        AppConfig? config = LoadConfigOrExplain();
        if (config is null) return 3;

        RegistrationStatus status = Registration.Prepare(out string? registrationError);
        RetryStore.SweepIfStale();

        // Launching the app directly is the other way in to "what did it just do
        // without asking me", alongside clicking the notice.
        ShowLastForward(config);

        // ShowDialog rather than Application.Run: a modeless form ignores
        // DialogResult, so the Cancel button would do nothing.
        using var form = new SettingsForm(config, status, registrationError);
        form.ShowDialog();
        return 0;
    }

    private static int RunPicker(string uri)
    {
        MailtoRequest request;
        try
        {
            request = MailtoRequest.Parse(uri);
        }
        catch (FormatException ex)
        {
            ShowError("That link could not be read.\r\n\r\n" + ex.Message);
            return 2;
        }

        AppConfig? config = LoadConfigOrExplain();
        if (config is null) return 3;

        // Write or repoint the registry entries as needed. Deliberately quiet
        // and non-fatal: the user clicked a mail link, and nothing about the
        // registry should interrupt or delay that.
        Registration.Prepare(out _);

        if (config.Accounts.Count == 0)
        {
            ShowWarning(
                "No accounts are configured yet, so there is nowhere to send this.\r\n\r\n" +
                "Add one in the settings window that opens next and this message will carry on.");
            RunSettings();

            // Pick up whatever the settings window just wrote, then continue
            // with the original link rather than making the user click it again.
            AppConfig? updated = LoadConfigOrExplain();
            if (updated is null || updated.Accounts.Count == 0) return 3;
            config = updated;
        }

        RetryStore.SweepIfStale();

        // Holding Shift forces the picker. Without it a domain rule is a one way
        // door: that domain would never show the picker again, and the only way
        // back would be the explanation window.
        bool forcePicker = (Control.ModifierKeys & Keys.Shift) != 0;

        Rule? rule = config.MatchRule(request.PrimaryRecipient);
        Account? byRule = config.FindByAddress(rule?.EmailAddress);
        if (rule is not null && byRule is not null && !forcePicker)
            return ForwardAutomatically(config, request, uri, rule, byRule);

        using var picker = new PickerForm(config, request);
        if (picker.ShowDialog() != DialogResult.OK || picker.SelectedAccount is null)
            return 0;   // Esc: do nothing at all.

        Account account = picker.SelectedAccount;
        if (!Mail.Open(request, account, null)) return 4;

        // Showing the picker means nothing is unexplained any more, so any record
        // of an earlier automatic forward stops being worth keeping, and the
        // draft it saved stops being worth storing.
        bool dirty = config.LastAutomaticForward is not null;
        config.LastAutomaticForward = null;
        RetryStore.Clear();

        if (picker.RememberAs is RuleKind kind)
        {
            config.SetRule(kind, picker.RememberMatch, account.EmailAddress);
            dirty = true;
        }

        // Worth a warning if it fails, but the mail is already open so this is
        // not fatal.
        if (dirty)
        {
            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                ShowWarning($"The message opened, but the rule could not be saved to\r\n{AppConfig.FilePath}\r\n\r\n{ex.Message}");
            }
        }

        // Asked only after the message is on its way: nothing should delay or
        // interrupt the thing the user actually clicked. Declining here simply
        // ends the run, which is all that is left to do anyway.
        RegistrationPrompt.EnsureDefaultHandler(null);

        return 0;
    }

    /// <summary>
    /// Sends without showing the picker, because a rule said who to send as.
    /// Nothing is confirmed beforehand and nothing is sent: this opens a draft,
    /// and the notice afterwards is what makes it noticeable rather than silent.
    /// </summary>
    private static int ForwardAutomatically(
        AppConfig config, MailtoRequest request, string originalUri, Rule rule, Account account)
    {
        if (!Mail.Open(request, account, null)) return 4;

        config.LastAutomaticForward = new ForwardRecord
        {
            Recipient = request.PrimaryRecipient,
            MatchedRule = rule.Match,
            SentFrom = account.EmailAddress,
            When = DateTimeOffset.Now,
        };

        try
        {
            config.Save();
            RetryStore.Save(originalUri);
        }
        catch (Exception ex)
        {
            // The mail is open either way; this only costs the ability to undo.
            ShowWarning("The message opened, but what happened could not be recorded:\r\n\r\n" + ex.Message);
        }

        // Runs a message loop until the notice fades or is clicked, which is why
        // this launch outlives the browser handoff.
        using var toast = new ToastForm(account.Name, request.PrimaryRecipient, rule.Match);
        Application.Run(new ApplicationContext(toast));

        if (toast.Clicked) ShowLastForward(config);
        return 0;
    }

    /// <summary>
    /// Shows what the last automatic forward did, and clears it. Viewing counts
    /// as having been told, so the record and the saved draft both go.
    /// </summary>
    private static void ShowLastForward(AppConfig config)
    {
        if (config.LastAutomaticForward is not ForwardRecord record) return;

        using (var dialog = new ForwardDialog(config, record, RetryStore.Load()))
        {
            dialog.ShowDialog();
        }

        config.LastAutomaticForward = null;
        RetryStore.Clear();
        try
        {
            config.Save();
        }
        catch
        {
            // Only costs showing the same explanation once more.
        }
    }

    /// <summary>
    /// Loads config.json. A corrupt file gets a visible dialog offering a reset;
    /// returns null when the user would rather go fix the file by hand.
    /// </summary>
    private static AppConfig? LoadConfigOrExplain()
    {
        try
        {
            return AppConfig.Load();
        }
        catch (Exception ex)
        {
            DialogResult answer = MessageBox.Show(
                "The settings file could not be loaded:\r\n\r\n" + ex.Message +
                "\r\n\r\nReplace it with a fresh default config?\r\n" +
                "(Choose No to leave the file alone and fix it yourself.)",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            if (answer != DialogResult.Yes) return null;

            try
            {
                AppConfig fresh = AppConfig.CreateDefault();
                fresh.Save();
                return fresh;
            }
            catch (Exception saveEx)
            {
                ShowError($"Could not write {AppConfig.FilePath}:\r\n\r\n{saveEx.Message}");
                return null;
            }
        }
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static void ShowWarning(string message) =>
        MessageBox.Show(message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
