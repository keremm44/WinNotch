using System.Windows;
using WinNotch.Common;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView
{
    public event EventHandler<LastMeaningfulClipboardContext>? MeaningfulContextAvailable;

    internal void RevealActionsFromPrimaryClick()
    {
        Dispatcher.Invoke(() =>
        {
            CancelCollapseGrace();

            if (_currentAction == null || _isExpanded)
                return;

            _isExpanded = true;
            ActionPanel.Visibility = Visibility.Visible;
            SurfaceMotion.Reveal(ActionPanel, 1.5, 95);
            GetHostWindow()?.SetContextSurfaceExpanded(true);
        });
    }

    internal void PublishMeaningfulContextIfAvailable()
    {
        if (_currentAction == null)
            return;

        ClipboardContentType contentType;
        string rawText;

        switch (_currentAction.Kind)
        {
            case ContextActionKind.OpenUrl:
                contentType = ClipboardContentType.Url;
                rawText = _currentAction.Target;
                break;
            case ContextActionKind.ShowInExplorer:
                contentType = ClipboardContentType.FilePath;
                rawText = _currentAction.Target;
                break;
            case ContextActionKind.ComposeEmail:
                contentType = ClipboardContentType.Email;
                rawText = _currentAction.Target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    ? _currentAction.Target[7..]
                    : _currentAction.Target;
                break;
            default:
                return;
        }

        MeaningfulContextAvailable?.Invoke(
            this,
            new LastMeaningfulClipboardContext(
                contentType,
                rawText,
                rawText,
                DateTime.Now,
                _currentAction));
    }
}
