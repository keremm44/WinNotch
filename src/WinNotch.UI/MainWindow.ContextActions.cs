using System.Windows.Media.Imaging;
using WinNotch.Common;
using WinNotch.Core.Services;

namespace WinNotch.UI;

public partial class MainWindow
{
    internal void SetContextSurfaceExpanded(bool expanded)
    {
        if (_currentState is not (NotchState.ClipboardNotify or NotchState.ScreenshotNotify))
            return;

        if (expanded)
        {
            _stateReturnTimer?.Stop();
            _stateReturnTimer = null;

            (double width, double height) = _currentState == NotchState.ScreenshotNotify
                ? (Constants.NotchScreenshotActionWidth, Constants.NotchScreenshotActionHeight)
                : (Constants.NotchClipboardActionWidth, Constants.NotchClipboardActionHeight);

            ApplyContextDimensions(width, height);
            return;
        }

        ApplyDimensions(_currentState);
        ScheduleReturn(
            TimeSpan.FromMilliseconds(Constants.ContextActionLeaveDelayMs),
            GetPersistentState());
    }

    internal void ExecuteContextAction(ContextAction action, BitmapSource? image)
    {
        if (action.Kind == ContextActionKind.SaveScreenshot)
        {
            ScreenshotSaveResult result = ScreenshotSaveService.TrySave(image, out string? error);
            switch (result)
            {
                case ScreenshotSaveResult.Saved:
                    ClipboardToastView.ShowActionFeedback(action.SuccessMessage ?? "Kaydedildi");
                    break;
                case ScreenshotSaveResult.Failed:
                    ClipboardToastView.ShowActionFeedback(error ?? "Kaydetme başarısız");
                    break;
                case ScreenshotSaveResult.Cancelled:
                    break;
            }
            return;
        }

        bool succeeded = ContextActionExecutor.TryExecute(action, out string? executionError);
        ClipboardToastView.ShowActionFeedback(
            succeeded
                ? action.SuccessMessage ?? "Açıldı"
                : executionError ?? "Aksiyon açılamadı");
    }

    private void ApplyContextDimensions(double width, double height)
    {
        if (Math.Abs(width - _currentWidth) < 1 &&
            Math.Abs(height - _currentHeight) < 1)
            return;

        _currentWidth = width;
        _currentHeight = height;
        _motionController.Apply(width, height);
    }
}
