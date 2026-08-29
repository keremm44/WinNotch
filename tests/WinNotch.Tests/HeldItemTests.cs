using System.IO;
using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class HeldItemTests
{
    [Fact]
    public void FromPath_ExistingFile_CapturesTransferMetadata()
    {
        string root = CreateTempRoot();
        string path = Path.Combine(root, "sample.txt");
        File.WriteAllText(path, "WinNotch");

        try
        {
            HeldItem item = HeldItem.FromPath(path);

            Assert.Equal(path, item.SourcePath);
            Assert.Equal("sample.txt", item.DisplayName);
            Assert.False(item.IsDirectory);
            Assert.Equal(new FileInfo(path).Length, item.SizeBytes);
            Assert.True(item.Exists);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FromPath_ExistingDirectory_IsMarkedAsDirectory()
    {
        string root = CreateTempRoot();
        string path = Path.Combine(root, "folder");
        Directory.CreateDirectory(path);

        try
        {
            HeldItem item = HeldItem.FromPath(path);

            Assert.True(item.IsDirectory);
            Assert.Null(item.SizeBytes);
            Assert.True(item.Exists);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Exists_ReflectsSourceRemovalAfterShelfCapture()
    {
        string root = CreateTempRoot();
        string path = Path.Combine(root, "temporary.txt");
        File.WriteAllText(path, "temporary");
        HeldItem item = HeldItem.FromPath(path);

        File.Delete(path);

        try
        {
            Assert.False(item.Exists);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "WinNotch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
