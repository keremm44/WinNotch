using System.Diagnostics;
using System.Windows;
using WinNotch.Common;

namespace WinNotch.UI;

public partial class MainWindow
{
    private DateTime _lastDragOverTraceAt = DateTime.MinValue;

    static MainWindow()
    {
        // WPF can show the source's NoDrop cursor until DragOver sets an effect.
        // Resolve file-drop acceptance during PreviewDragEnter as well so Explorer
        // receives an explicit Copy effect from the first routed event.
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            DragDrop.PreviewDragEnterEvent,
            new DragEventHandler(MainWindow_PreviewFileDrag),
            handledEventsToo: true);

        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            DragDrop.PreviewDragOverEvent,
            new DragEventHandler(MainWindow_PreviewFileDrag),
            handledEventsToo: true);
    }

    private static void MainWindow_PreviewFileDrag(object sender, DragEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        bool hasFileDrop = HasFileDropFormat(e.Data);
        bool copyAllowed = (e.AllowedEffects & DragDropEffects.Copy) != 0;
        FileDropDecision decision = FileDropPolicy.Evaluate(
            window._settings.ModuleA_DragDrop,
            window._isDraggingOut,
            hasFileDrop,
            copyAllowed);

        e.Effects = decision.Accepted
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        window.TraceFileDrag(e, decision, hasFileDrop, copyAllowed);
        // Do not mark handled. Existing RootGrid handlers still own visual state,
        // Drop execution and the shelf transition.
    }

    private static bool HasFileDropFormat(IDataObject data)
    {
        try
        {
            return data.GetDataPresent(DataFormats.FileDrop, autoConvert: false) ||
                   data.GetDataPresent(DataFormats.FileDrop);
        }
        catch
        {
            return false;
        }
    }

    [Conditional("DEBUG")]
    private void TraceFileDrag(
        DragEventArgs e,
        FileDropDecision decision,
        bool hasFileDrop,
        bool copyAllowed)
    {
        bool isEnter = e.RoutedEvent == DragDrop.PreviewDragEnterEvent;
        DateTime now = DateTime.UtcNow;
        if (!isEnter && (now - _lastDragOverTraceAt).TotalMilliseconds < 500)
            return;

        if (!isEnter)
            _lastDragOverTraceAt = now;

        string formats;
        try
        {
            formats = string.Join(",", e.Data.GetFormats(autoConvert: false));
        }
        catch (Exception ex)
        {
            formats = $"<unavailable:{ex.GetType().Name}>";
        }

        Debug.WriteLine(
            $"[DragDrop] Event={(isEnter ? "PreviewDragEnter" : "PreviewDragOver")} " +
            $"ModuleEnabled={_settings.ModuleA_DragDrop} " +
            $"DraggingOut={_isDraggingOut} " +
            $"FileDropPresent={hasFileDrop} " +
            $"CopyAllowed={copyAllowed} " +
            $"AllowedEffects={e.AllowedEffects} " +
            $"ResolvedEffect={e.Effects} " +
            $"Decision={decision.Reason} " +
            $"State={_currentState} " +
            $"Formats=[{formats}]");
    }
}
