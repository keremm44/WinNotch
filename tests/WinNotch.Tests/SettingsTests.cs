// WinNotch.Tests/SettingsTests.cs
// Tests for ModuleSettings defaults and property behavior.

using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class ModuleSettingsTests
{
    [Fact]
    public void DefaultValues_AllModulesEnabled()
    {
        var settings = new ModuleSettings();

        Assert.True(settings.ModuleA_DragDrop);
        Assert.True(settings.ModuleB_Clipboard);
        Assert.True(settings.ModuleC_Media);
        Assert.True(settings.ModuleD_WindowPin);
        Assert.True(settings.ModuleE_Screenshot);
    }

    [Fact]
    public void DefaultMonitorIndex_IsZero()
    {
        var settings = new ModuleSettings();
        Assert.Equal(0, settings.TargetMonitorIndex);
    }

    [Fact]
    public void DefaultDiagnostics_IsDisabled()
    {
        var settings = new ModuleSettings();
        Assert.False(settings.DiagnosticsEnabled);
    }

    [Fact]
    public void DisablingModule_KeepsOthersEnabled()
    {
        var settings = new ModuleSettings();
        settings.ModuleB_Clipboard = false;

        Assert.True(settings.ModuleA_DragDrop);
        Assert.False(settings.ModuleB_Clipboard);
        Assert.True(settings.ModuleC_Media);
        Assert.True(settings.ModuleD_WindowPin);
        Assert.True(settings.ModuleE_Screenshot);
    }

    [Fact]
    public void AllModulesCanBeDisabled_Independently()
    {
        var settings = new ModuleSettings();

        settings.ModuleA_DragDrop = false;
        settings.ModuleB_Clipboard = false;
        settings.ModuleC_Media = false;
        settings.ModuleD_WindowPin = false;
        settings.ModuleE_Screenshot = false;

        Assert.False(settings.ModuleA_DragDrop);
        Assert.False(settings.ModuleB_Clipboard);
        Assert.False(settings.ModuleC_Media);
        Assert.False(settings.ModuleD_WindowPin);
        Assert.False(settings.ModuleE_Screenshot);
    }

    [Fact]
    public void AutoStart_DefaultsToFalse()
    {
        var settings = new ModuleSettings();
        Assert.False(settings.AutoStart);
    }
}
