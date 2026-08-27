using System.Windows;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView
{
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
}
