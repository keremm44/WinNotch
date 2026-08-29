using System.IO;
using System.Windows;
using WinNotch.Common;
using WinNotch.Core.Services;
using Xunit;

namespace WinNotch.Tests;

public class FileShelfTransferServiceTests
{
    [Fact]
    public void GetExistingPaths_FiltersMissingSourcesAndCaseInsensitiveDuplicates()
    {
        string root = CreateTempRoot();
        string file = Path.Combine(root, "sample.txt");
        string folder = Path.Combine(root, "folder");
        File.WriteAllText(file, "WinNotch");
        Directory.CreateDirectory(folder);

        try
        {
            var items = new[]
            {
                HeldItem.FromPath(file),
                HeldItem.FromPath(file.ToUpperInvariant()),
                HeldItem.FromPath(folder),
                HeldItem.FromPath(Path.Combine(root, "missing.txt"))
            };

            string[] paths = FileShelfTransferService.GetExistingPaths(items);

            Assert.Equal(2, paths.Length);
            Assert.Contains(paths, path => string.Equals(path, file, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(paths, path => string.Equals(path, folder, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateFileDropDataObject_AdvertisesCanonicalFileDropList()
    {
        string first = @"C:\Temp\one.txt";
        string second = @"C:\Temp\two.txt";

        DataObject data = FileShelfTransferService.CreateFileDropDataObject(new[] { first, second });

        Assert.True(data.GetDataPresent(DataFormats.FileDrop));
        var files = data.GetFileDropList();
        Assert.Equal(2, files.Count);
        Assert.Equal(first, files[0]);
        Assert.Equal(second, files[1]);
    }

    [Fact]
    public void CreateFileDropDataObject_RejectsEmptyTransfer()
    {
        Assert.Throws<ArgumentException>(() =>
            FileShelfTransferService.CreateFileDropDataObject(Array.Empty<string>()));
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "WinNotch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
