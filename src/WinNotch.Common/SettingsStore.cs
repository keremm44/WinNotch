// WinNotch.Common/SettingsStore.cs
// Minimal JSON persistence for module and appearance settings.
//
// DESIGN:
// - Single file: %LOCALAPPDATA%/WinNotch/settings.json
// - Load on startup, save on exit and on explicit change
// - Atomic write (write to temp, then rename) to prevent corruption
// - No external packages or heavy persistence layer

using System.IO;
using System.Text.Json;

namespace WinNotch.Common;

/// <summary>
/// Handles loading and saving ModuleSettings to a JSON file.
/// File location: %LOCALAPPDATA%/WinNotch/settings.json
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private static string? s_filePath;

    private static string GetFilePath()
    {
        if (s_filePath == null)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.AppName);
            Directory.CreateDirectory(dir);
            s_filePath = Path.Combine(dir, "settings.json");
        }
        return s_filePath;
    }

    /// <summary>
    /// Loads settings from disk. Returns safe defaults if the file is absent or corrupt.
    /// Missing Appearance data from older versions is migrated in-memory automatically.
    /// </summary>
    public static ModuleSettings Load()
    {
        try
        {
            string path = GetFilePath();
            if (!File.Exists(path))
                return CreateNormalizedDefaults();

            string json = File.ReadAllText(path);
            ModuleSettings settings = JsonSerializer.Deserialize<ModuleSettings>(json, s_options)
                ?? new ModuleSettings();
            Normalize(settings);
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsStore] Load failed: {ex.Message}");
            return CreateNormalizedDefaults();
        }
    }

    /// <summary>
    /// Saves settings to disk. Invalid preset strings are normalized before serialization.
    /// </summary>
    public static void Save(ModuleSettings settings)
    {
        try
        {
            Normalize(settings);

            string path = GetFilePath();
            string tempPath = path + ".tmp";

            string json = JsonSerializer.Serialize(settings, s_options);
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                // Replace in one filesystem operation. Deleting the destination first
                // created a crash window in which a valid settings file was lost.
                File.Replace(tempPath, path, destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsStore] Save failed: {ex.Message}");
        }
    }

    private static ModuleSettings CreateNormalizedDefaults()
    {
        var settings = new ModuleSettings();
        Normalize(settings);
        return settings;
    }

    private static void Normalize(ModuleSettings settings)
    {
        settings.Appearance ??= new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(settings.Appearance);
    }
}
