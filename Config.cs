using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailtoPicker;

internal sealed class Account
{
    public string Name { get; set; } = "";

    /// <summary>
    /// The mailbox to send from, handed to Gmail as authuser.
    ///
    /// This is the only account selector. Gmail's /mail/u/N/ index was dropped:
    /// N is a position in the browser's signed-in list rather than an identity,
    /// so it renumbers whenever accounts are signed in or out and silently
    /// composes from the wrong account. An address names the mailbox and cannot
    /// drift, and a wrong one surfaces as Google's account chooser instead.
    /// </summary>
    public string EmailAddress { get; set; } = "";

    public override string ToString() => Name;

    public Account Clone() => new() { Name = Name, EmailAddress = EmailAddress };
}

internal sealed class AppConfig
{
    public List<Account> Accounts { get; set; } = new();

    /// <summary>
    /// Address of the account sent from last time, if any. Keyed on the address
    /// rather than the display name for the same reason accounts are: the name
    /// is free text the user can change at any moment, and renaming an account
    /// would silently reset the picker's default.
    /// </summary>
    public string? LastUsedAddress { get; set; }

    [JsonIgnore]
    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MailtoPicker");

    [JsonIgnore]
    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// A new config starts empty on purpose. Seeding a guessed entry would put a
    /// plausible-looking account in the list that nobody chose; the settings
    /// window asks for the first real one instead.
    /// </summary>
    public static AppConfig CreateDefault() => new();

    /// <summary>
    /// Reads config.json. A missing file is not an error: a default config is
    /// written and returned. A corrupt file throws so the caller can complain
    /// loudly rather than silently losing the user's account list.
    /// </summary>
    public static AppConfig Load()
    {
        string path = FilePath;
        if (!File.Exists(path))
        {
            var fresh = CreateDefault();
            fresh.Save();
            return fresh;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Could not read {path}\r\n\r\n{ex.Message}", ex);
        }

        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path} is not valid JSON.\r\n\r\n{ex.Message}", ex);
        }

        if (config is null)
            throw new InvalidDataException($"{path} is empty or contains only \"null\".");

        if (config.Accounts is null) config.Accounts = new List<Account>();

        // An entry without an address cannot select a mailbox, so it is dropped
        // rather than left in the picker to fail at send time. The settings
        // window then asks for a real one.
        config.Accounts.RemoveAll(a => a is null
            || string.IsNullOrWhiteSpace(a.Name)
            || string.IsNullOrWhiteSpace(a.EmailAddress));
        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = FilePath;
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));

        // Replace in one step so an interrupted write cannot leave a half file.
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    public Account? FindByAddress(string? address) =>
        string.IsNullOrWhiteSpace(address)
            ? null
            : Accounts.FirstOrDefault(a =>
                string.Equals(a.EmailAddress, address, StringComparison.OrdinalIgnoreCase));
}
