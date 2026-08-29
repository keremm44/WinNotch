using System.Collections.Specialized;
using System.Windows;
using WinNotch.Common;

namespace WinNotch.Core.Services;

/// <summary>
/// Builds canonical Windows FileDrop payloads for File Shelf transfers.
/// File contents are never loaded; only existing source paths are advertised.
/// </summary>
public static class FileShelfTransferService
{
    public static string[] GetExistingPaths(IEnumerable<HeldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Where(item => item != null && item.Exists)
            .Select(item => item.SourcePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static DataObject CreateFileDropDataObject(IReadOnlyCollection<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string[] normalized = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
            throw new ArgumentException("At least one file-drop path is required.", nameof(paths));

        var files = new StringCollection();
        files.AddRange(normalized);

        var data = new DataObject();
        data.SetFileDropList(files);
        return data;
    }
}
