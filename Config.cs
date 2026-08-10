using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailtoPicker;

internal sealed class Account
{
    public string Name { get; set; } = "";

    /// <summary>The N in Google's /mail/u/N/ path, i.e. which signed-in account.</summary>
    public int AccountIndex { get; set; }

    public override string ToString() => Name;

    public Account Clone() => new() { Name = Name, AccountIndex = AccountIndex };
}

internal sealed class AppConfig
{
    public List<Account> Accounts { get; set; } = new();

    /// <summary>Display name of the account picked last time, if any.</summary>
    public string? LastUsedAccount { get; set; }

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

    public static AppConfig CreateDefault() => new()
    {
        Accounts = { new Account { Name = "Personal", AccountIndex = 0 } },
        LastUsedAccount = "Personal",
    };

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
        config.Accounts.RemoveAll(a => a is null || string.IsNullOrWhiteSpace(a.Name));
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

    public Account? FindByName(string? name) =>
        name is null ? null : Accounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
}
