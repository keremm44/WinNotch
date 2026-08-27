using System.Windows;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        IsVisibleChanged += ClipboardToastView_IsVisibleChanged;
    }

    private void ClipboardToastView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            PublishMeaningfulContextIfAvailable();
            return;
        }

        CancelCollapseGrace();
        CollapseActions(notify: false);
        _currentAction = null;
        _currentImage = null;
        ImagePreview.Source = null;
        ImagePreviewBorder.Visibility = Visibility.Collapsed;
    }
}
