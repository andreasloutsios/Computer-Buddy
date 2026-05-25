using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ComputerBuddy.App.Models;

namespace ComputerBuddy.App.Services;

public sealed class SettingsService
{
    private const string AppFolderName = "ComputerBuddy";
    private const string SettingsFileName = "settings.json";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ComputerBuddy.Settings.v1");

    public UserSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
    }

    public void Save(UserSettings settings)
    {
        var path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void SetOpenAiApiKey(UserSettings settings, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            settings.EncryptedOpenAiApiKey = null;
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        settings.EncryptedOpenAiApiKey = Convert.ToBase64String(protectedBytes);
    }

    public string? GetOpenAiApiKey(UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EncryptedOpenAiApiKey))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(settings.EncryptedOpenAiApiKey);
            var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppFolderName, SettingsFileName);
    }
}

