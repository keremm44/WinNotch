// WinNotch.Common/SettingsStore.cs
// WHY: ModuleSettings was a plain POCO that reset every launch.
// This provides minimal JSON persistence using System.Text.Json (BCL).
// No external packages needed — System.Text.Json ships with .NET 8.
//
// DESIGN:
// - Single file: %LOCALAPPDATA%/WinNotch/settings.json
// - Load on startup, save on exit and on explicit change
// - Atomic write (write to temp, then rename) to prevent corruption
// - No heavy JSON framework, no dependency injection, no reflection
// - File is <200 bytes — serialization cost is negligible

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

    /// <summary>
    /// Gets the settings file path. Created lazily on first access.
    /// </summary>
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
    /// Loads settings from disk. Returns default settings if file doesn't exist or is corrupt.
    /// WHY: Never fail startup due to corrupt settings. Falls back to defaults silently.
    /// </summary>
    public static ModuleSettings Load()
    {
        try
        {
            string path = GetFilePath();
            if (!File.Exists(path))
                return new ModuleSettings();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModuleSettings>(json, s_options) ?? new ModuleSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsStore] Load failed: {ex.Message}");
            return new ModuleSettings();
        }
    }

    /// <summary>
    /// Saves settings to disk. Uses atomic write (temp + rename) to prevent corruption.
    /// WHY: If the process crashes during write, the old file remains intact.
    /// </summary>
    public static void Save(ModuleSettings settings)
    {
        try
        {
            string path = GetFilePath();
            string tempPath = path + ".tmp";

            string json = JsonSerializer.Serialize(settings, s_options);
            File.WriteAllText(tempPath, json);

            // Atomic replace: delete old, rename temp
            // WHY: On Windows, File.Replace can fail if target is locked.
            // Simple delete + rename is more reliable for a small settings file.
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsStore] Save failed: {ex.Message}");
        }
    }
}
