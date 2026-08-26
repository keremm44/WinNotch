namespace WinNotch.Common;

/// <summary>
/// Lightweight metadata for a file/folder held by the WinNotch shelf.
/// IMPORTANT: The file contents are never loaded into memory.
/// </summary>
public sealed class HeldItem
{
    public required string SourcePath { get; init; }
    public required string DisplayName { get; init; }
    public bool IsDirectory { get; init; }
    public long? SizeBytes { get; init; }
    public DateTime AddedAt { get; init; } = DateTime.Now;

    public bool Exists => IsDirectory
        ? Directory.Exists(SourcePath)
        : File.Exists(SourcePath);

    public static HeldItem FromPath(string path)
    {
        bool isDirectory = Directory.Exists(path);
        long? size = null;

        if (!isDirectory && File.Exists(path))
        {
            try
            {
                size = new FileInfo(path).Length;
            }
            catch
            {
                // Metadata is optional; the shelf must still accept the path.
            }
        }

        return new HeldItem
        {
            SourcePath = path,
            DisplayName = Path.GetFileName(path) is { Length: > 0 } name ? name : path,
            IsDirectory = isDirectory,
            SizeBytes = size
        };
    }
}
