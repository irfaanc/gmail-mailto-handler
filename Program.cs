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

        using var picker = new PickerForm(config, request);
        if (picker.ShowDialog() != DialogResult.OK || picker.SelectedAccount is null)
            return 0;   // Esc: do nothing at all.

        Account account = picker.SelectedAccount;
        string url = request.ToGmailComposeUrl(account.EmailAddress);

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError("Could not open the browser:\r\n\r\n" + ex.Message + "\r\n\r\nURL:\r\n" + url);
            return 4;
        }

        // Write the rule the user asked for, if any. Worth a warning if it fails,
        // but the mail is already open so this is not fatal.
        if (picker.RememberAs is RuleKind kind)
        {
            config.SetRule(kind, picker.RememberMatch, account.EmailAddress);
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
