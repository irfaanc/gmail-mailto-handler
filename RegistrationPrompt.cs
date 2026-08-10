using System.Windows.Forms;

namespace MailtoPicker;

/// <summary>
/// The user-facing half of registration. Kept apart from
/// <see cref="Registration"/> so the registry code stays free of any UI.
///
/// Note what is *not* here: an offer to register. Writing the registry entries
/// is unguarded, silent, and reversible, and with no installer the first run is
/// the installation -- so it just happens. The only thing worth a dialog is the
/// step Windows genuinely reserves for the user: choosing the default handler.
/// </summary>
internal static class RegistrationPrompt
{
    private const string Title = "Mailto Picker";

    /// <summary>
    /// Checks whether Windows is routing mail links here, and walks the user
    /// through Settings if not. Asked on every launch while the answer is no:
    /// handling mail links is the only thing this app does, and until it is the
    /// chosen handler it does nothing at all.
    /// </summary>
    /// <returns>True if the app is the default handler by the time this returns.</returns>
    public static bool EnsureDefaultHandler(IWin32Window? owner)
    {
        if (Registration.IsEffectiveHandler()) return true;

        DialogResult answer = Show(owner,
            "Windows is not sending mail links to Mailto Picker" +
            DescribeCurrentHandler() + ".\r\n\r\n" +
            "Make Mailto Picker the handler? This replaces the current one.\r\n\r\n" +
            "Until that is done, mail links keep going wherever they go today " +
            "and this app never sees them.",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes) return false;

        // The direct route: clear the recorded choice and let Windows fall
        // through to our class entry. Windows blocks this for some protocols on
        // some builds, so it is attempted rather than assumed.
        if (Registration.TryClaimDefault(out string? claimError))
        {
            Show(owner, "Done. Mail links now open in Mailto Picker.",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        Show(owner,
            "Windows would not let the change be made directly" +
            (claimError is null ? "" : $" ({claimError})") + ", so it has to be " +
            "done by hand.\r\n\r\nSettings will open next.",
            MessageBoxButtons.OK, MessageBoxIcon.Information);

        try
        {
            Registration.OpenDefaultAppsSettings();
        }
        catch (Exception ex)
        {
            Show(owner,
                "Could not open Windows Settings:\r\n\r\n" + ex.Message +
                "\r\n\r\nOpen Settings > Apps > Default apps by hand and set MAILTO " +
                "to Mailto Picker.",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        // Settings runs out of process and tells us nothing when the user is
        // done, so the only way to know is to look again when they say so.
        bool firstTry = true;
        while (true)
        {
            // Spelled out because the Default apps page is not obvious: the app
            // list at the bottom is the wrong place to look, and the link-type
            // box at the top is the route that works.
            const string Directions =
                "In Settings > Apps > Default apps, type MAILTO into " +
                "\"Set a default for a file type or link type\", then use the " +
                "MAILTO row that appears to choose Mailto Picker.";

            string message = firstTry
                ? "Settings is open.\r\n\r\n" + Directions + "\r\n\r\nThen choose Retry to check."
                : "Windows still has mail links pointed somewhere else" +
                  DescribeCurrentHandler() + ".\r\n\r\n" + Directions +
                  "\r\n\r\nThen choose Retry.";

            DialogResult retry = Show(owner, message, MessageBoxButtons.RetryCancel,
                firstTry ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (retry == DialogResult.Cancel) return false;
            if (Registration.IsEffectiveHandler()) return true;

            firstTry = false;
        }
    }

    private static string DescribeCurrentHandler()
    {
        string? progId = Registration.DefaultHandlerProgId();
        return string.IsNullOrEmpty(progId) ? "" : $" ({progId})";
    }

    /// <summary>Reports the outcome of the startup registry pass, if it failed.</summary>
    public static void ReportIfFailed(IWin32Window? owner, RegistrationStatus status, string? error)
    {
        if (status != RegistrationStatus.Failed) return;

        Show(owner,
            "Could not write this app's registry entries, so Windows will not " +
            "offer it as a mail handler.\r\n\r\n" + error,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static DialogResult Show(IWin32Window? owner, string text,
                                     MessageBoxButtons buttons, MessageBoxIcon icon) =>
        owner is null
            ? MessageBox.Show(text, Title, buttons, icon)
            : MessageBox.Show(owner, text, Title, buttons, icon);
}
