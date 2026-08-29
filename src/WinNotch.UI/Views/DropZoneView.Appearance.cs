using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.UI.Views;

public partial class DropZoneView
{
    public void ApplyAppearance(AppearanceSettings settings)
    {
        AppearanceResolver.NormalizeInPlace(settings);
        DensityProfile density = AppearanceResolver.ResolveDensity(settings);

        DropTargetText.FontSize = 10.5 * density.FontScale;
        FileSummaryText.FontSize = 9.5 * density.FontScale;
        FileIcon.FontSize = 9.5 * density.FontScale;
        CopyFilesButton.FontSize = 9.5 * density.FontScale;
        OpenFolderButton.FontSize = 9.5 * density.FontScale;
        MoreButton.FontSize = 9.5 * density.FontScale;

        double iconSize = 28 * density.ControlScale;
        FileIconSurface.Width = iconSize;
        FileIconSurface.Height = iconSize;
        RemoveButton.Width = 26 * density.ControlScale;
        RemoveButton.Height = 26 * density.ControlScale;

        // SetResourceReference keeps the shelf tint live when Accent/State Accent changes.
        FileIconSurface.SetResourceReference(
            Border.BackgroundProperty,
            "Brush.State.File.Subtle");
        FileIconSurface.SetResourceReference(
            Border.BorderBrushProperty,
            "Brush.State.File.Border");

        if (HasItems)
        {
            RenderShelf();
            if (_isExpanded)
                RenderChips();
        }
    }
}
