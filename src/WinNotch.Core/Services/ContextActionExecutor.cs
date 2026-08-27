using System.Diagnostics;
using System.IO;
using WinNotch.Common;
using WinNotch.Core.Interop;

namespace WinNotch.Core.Services;

/// <summary>
/// Executes only actions explicitly produced by ContextActionResolver.
/// No action is inferred here; this layer only performs the requested OS operation.
/// </summary>
public static class ContextActionExecutor
{
    public static bool TryExecute(ContextAction action, out string? error)
    {
        error = null;

        try
        {
            return action.Kind switch
            {
                ContextActionKind.OpenUrl or ContextActionKind.ComposeEmail
                    => OpenShellTarget(action.Target, out error),
                ContextActionKind.ShowInExplorer
                    => ShowInExplorer(action.Target, out error),
                _ => Fail("Bu aksiyon dış uygulama tarafından yürütülemez.", out error)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContextAction] Execute failed: {ex.Message}");
            error = "Aksiyon açılamadı";
            return false;
        }
    }

    private static bool OpenShellTarget(string target, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(target))
            return Fail("Hedef boş", out error);

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
        return true;
    }

    private static bool ShowInExplorer(string rawPath, out string? error)
    {
        error = null;
        string path = NormalizePath(rawPath);

        if (Directory.Exists(path))
        {
            Shell32.OpenFolder(path);
            return true;
        }

        if (File.Exists(path))
        {
            Shell32.OpenFileInExplorer(path);
            return true;
        }

        return Fail("Kaynak bulunamıyor", out error);
    }

    private static string NormalizePath(string rawPath)
    {
        string path = rawPath.Trim().Trim('"');

        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path[2..]);
        }
        else if (path.StartsWith("./", StringComparison.Ordinal) ||
                 path.StartsWith(".\\", StringComparison.Ordinal))
        {
            path = Path.GetFullPath(path);
        }

        return path;
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
