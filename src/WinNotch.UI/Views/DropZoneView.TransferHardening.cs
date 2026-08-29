using System.Diagnostics;
using WinNotch.Core.Services;

using WpfDragDrop = System.Windows.DragDrop;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonState = System.Windows.Input.MouseButtonState;
using WpfPoint = System.Windows.Point;
using WpfSystemParameters = System.Windows.SystemParameters;

namespace WinNotch.UI.Views;

public partial class DropZoneView
{
    private void DragHandle_HardenedMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != WpfMouseButtonState.Pressed || _items.Length == 0)
            return;

        WpfPoint now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < WpfSystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < WpfSystemParameters.MinimumVerticalDragDistance)
            return;

        string[] validPaths = FileShelfTransferService.GetExistingPaths(_items);
        if (validPaths.Length == 0)
        {
            ShowActionFeedback("Kaynak bulunamıyor");
            return;
        }

        DragOutStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            // SetFileDropList is the canonical WPF/Windows representation used by
            // Explorer and other shell drop targets. Keep the source effect Copy-only:
            // File Shelf is a temporary reference surface and must never move sources.
            System.Windows.DataObject data = FileShelfTransferService.CreateFileDropDataObject(validPaths);
            WpfDragDrop.DoDragDrop(DragHandle, data, WpfDragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Drag-out failed: {ex}");
            ShowActionFeedback("Sürükleme başarısız");
        }
        finally
        {
            DragOutCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
