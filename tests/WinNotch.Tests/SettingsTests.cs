using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class ModuleSettingsTests
{
    [Fact]
    public void DefaultValues_KeepCoreFeaturesOn_AndMediaOptIn()
    {
        var settings = new ModuleSettings();

        Assert.True(settings.ModuleA_DragDrop);
        Assert.True(settings.ModuleB_Clipboard);
        Assert.False(settings.ModuleC_Media);
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
    public void DisablingModule_KeepsOtherDefaultsUnchanged()
    {
        var settings = new ModuleSettings();
        settings.ModuleB_Clipboard = false;

        Assert.True(settings.ModuleA_DragDrop);
        Assert.False(settings.ModuleB_Clipboard);
        Assert.False(settings.ModuleC_Media);
        Assert.True(settings.ModuleE_Screenshot);
    }

    [Fact]
    public void AllModulesCanBeDisabled_Independently()
    {
        var settings = new ModuleSettings
        {
            ModuleA_DragDrop = false,
            ModuleB_Clipboard = false,
            ModuleC_Media = false,
            ModuleE_Screenshot = false
        };

        Assert.False(settings.ModuleA_DragDrop);
        Assert.False(settings.ModuleB_Clipboard);
        Assert.False(settings.ModuleC_Media);
        Assert.False(settings.ModuleE_Screenshot);
    }

    [Fact]
    public void MediaCanBeEnabledExplicitly()
    {
        var settings = new ModuleSettings { ModuleC_Media = true };
        Assert.True(settings.ModuleC_Media);
    }

    [Fact]
    public void AutoStart_DefaultsToFalse()
    {
        var settings = new ModuleSettings();
        Assert.False(settings.AutoStart);
    }
}
