using System.Diagnostics;
using Microsoft.Win32;

namespace MailtoPicker;

/// <summary>
/// Registers this exe as a *candidate* mailto: handler, entirely under
/// HKEY_CURRENT_USER. Nothing here makes the app the default; Windows guards
/// the UserChoice key in the kernel (UCPD.sys), so the user has to pick us
/// once in Settings > Apps > Default apps.
/// </summary>
internal static class Registration
{
    public const string AppKeyName = "MailtoPicker";
    public const string DisplayName = "Mailto Picker";
    public const string Description = "Choose which Gmail account opens a mailto: link.";
    public const string ProgId = "MailtoPicker.Url.Mailto";

    private const string CapabilitiesPath = @"Software\" + AppKeyName + @"\Capabilities";
    private const string BackupPath = @"Software\" + AppKeyName + @"\PreviousMailtoHandler";
    private const string MailtoClassPath = @"Software\Classes\mailto";

    public static string ExePath
    {
        get
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path)) return path;
            // Fallback for odd hosting scenarios: the managed dll sits next to the apphost.
            return Path.ChangeExtension(typeof(Registration).Assembly.Location, ".exe");
        }
    }

    public static void Register()
    {
        string exe = ExePath;
        string command = $"\"{exe}\" \"%1\"";
        string icon = $"\"{exe}\",0";

        // The protocol class itself. This is the fallback Windows uses when no
        // explicit UserChoice exists for mailto. Whatever is there now gets
        // stashed first so Unregister can put it back.
        BackUpExistingMailtoClass();
        WriteProtocolClass(MailtoClassPath, "URL:MailTo Protocol", command, icon);

        // A private ProgID, which is what the Capabilities entry points at.
        WriteProtocolClass($@"Software\Classes\{ProgId}", DisplayName, command, icon);

        using (RegistryKey capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesPath, true))
        {
            capabilities.SetValue("ApplicationName", DisplayName);
            capabilities.SetValue("ApplicationDescription", Description);
            capabilities.SetValue("ApplicationIcon", icon);
            using RegistryKey urls = capabilities.CreateSubKey("UrlAssociations", true);
            urls.SetValue("mailto", ProgId);
        }

        using (RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications", true))
        {
            registered.SetValue(AppKeyName, CapabilitiesPath);
        }
    }

    public static void Unregister()
    {
        using (RegistryKey? registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
        {
            if (registered?.GetValue(AppKeyName) is not null)
                registered.DeleteValue(AppKeyName, throwOnMissingValue: false);
        }

        // Before wiping our own key, which is where the backup lives.
        RestoreOrRemoveMailtoClass();

        DeleteTree(@"Software\" + AppKeyName);
        DeleteTree($@"Software\Classes\{ProgId}");
    }

    /// <summary>
    /// Remembers the mailto class as we found it, so uninstalling this app does
    /// not silently destroy whatever handler was registered before.
    /// </summary>
    private static void BackUpExistingMailtoClass()
    {
        using (RegistryKey? alreadySaved = Registry.CurrentUser.OpenSubKey(BackupPath))
        {
            if (alreadySaved is not null) return;   // never overwrite with our own values
        }

        if (OwnsMailtoClass()) return;

        using RegistryKey? mailto = Registry.CurrentUser.OpenSubKey(MailtoClassPath);
        if (mailto is null) return;                 // nothing there to lose

        // The subkeys are recorded even when empty: an existing-but-blank
        // DefaultIcon is still a difference worth putting back.
        using RegistryKey backup = Registry.CurrentUser.CreateSubKey(BackupPath, true);
        if (mailto.GetValue(null) is string description)
            backup.SetValue("Default", description);
        if (mailto.GetValue("URL Protocol") is not null)
            backup.SetValue("HadUrlProtocol", 1, RegistryValueKind.DWord);

        using (RegistryKey? iconKey = mailto.OpenSubKey("DefaultIcon"))
        {
            if (iconKey is not null)
            {
                backup.SetValue("HadDefaultIcon", 1, RegistryValueKind.DWord);
                if (iconKey.GetValue(null) is string existingIcon)
                    backup.SetValue("DefaultIcon", existingIcon);
            }
        }

        using RegistryKey? commandKey = mailto.OpenSubKey(@"shell\open\command");
        if (commandKey is not null)
        {
            backup.SetValue("HadCommand", 1, RegistryValueKind.DWord);
            if (commandKey.GetValue(null) is string existingCommand)
                backup.SetValue("Command", existingCommand);
        }
    }

    private static void RestoreOrRemoveMailtoClass()
    {
        // If something else has taken over the class since we registered, leave
        // it well alone.
        if (!OwnsMailtoClass()) return;

        DeleteTree(MailtoClassPath);

        using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(BackupPath);
        if (backup is null) return;                 // the key did not exist before us

        using RegistryKey mailto = Registry.CurrentUser.CreateSubKey(MailtoClassPath, true);
        if (backup.GetValue("Default") is string description)
            mailto.SetValue(null, description);
        if (backup.GetValue("HadUrlProtocol") is not null)
            mailto.SetValue("URL Protocol", "");

        if (backup.GetValue("HadDefaultIcon") is not null)
        {
            using RegistryKey iconKey = mailto.CreateSubKey("DefaultIcon", true);
            if (backup.GetValue("DefaultIcon") is string icon)
                iconKey.SetValue(null, icon);
        }

        if (backup.GetValue("HadCommand") is not null)
        {
            using RegistryKey commandKey = mailto.CreateSubKey(@"shell\open\command", true);
            if (backup.GetValue("Command") is string command)
                commandKey.SetValue(null, command);
        }
    }

    public static bool IsRegistered()
    {
        using RegistryKey? registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");
        return registered?.GetValue(AppKeyName) is not null;
    }

    private static bool OwnsMailtoClass()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\mailto\shell\open\command");
        return key?.GetValue(null) is string command &&
               command.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteProtocolClass(string path, string description, string command, string icon)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(path, true);
        key.SetValue(null, description);
        key.SetValue("URL Protocol", "");

        using (RegistryKey iconKey = key.CreateSubKey("DefaultIcon", true))
            iconKey.SetValue(null, icon);

        using RegistryKey commandKey = key.CreateSubKey(@"shell\open\command", true);
        commandKey.SetValue(null, command);
    }

    private static void DeleteTree(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch (ArgumentException)
        {
            // Key was already gone.
        }
    }

    /// <summary>Opens Settings > Apps > Default apps so the user can select us.</summary>
    public static void OpenDefaultAppsSettings()
    {
        Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
    }
}
