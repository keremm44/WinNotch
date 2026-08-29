using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace WinNotch.TrayApp;

public partial class SettingsWindow
{
    private const string ReactionHelpMarker = "WinNotch.ReactionLevelHelp";

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureReactionLevelHelp();
    }

    private void EnsureReactionLevelHelp()
    {
        const string quietHelp =
            "Sessiz: URL, e-posta, telefon ve renk gibi ince pano olaylarını gizler; " +
            "dosya yolu ve ekran görüntüsü gibi doğrudan olaylar görünmeye devam eder.";
        const string balancedHelp =
            "Dengeli: URL, dosya yolu, e-posta, telefon ve renk gibi anlamlı pano olaylarını gösterir; " +
            "normal metin sessiz kalır.";
        const string activeHelp =
            "Aktif: Dengeli moda ek olarak normal metin pano olaylarını da gösterebilir.";

        ReactionQuietRadio.ToolTip = quietHelp;
        ReactionBalancedRadio.ToolTip = balancedHelp;
        ReactionActiveRadio.ToolTip = activeHelp;
        AutomationProperties.SetHelpText(ReactionQuietRadio, quietHelp);
        AutomationProperties.SetHelpText(ReactionBalancedRadio, balancedHelp);
        AutomationProperties.SetHelpText(ReactionActiveRadio, activeHelp);

        if (ReactionBalancedRadio.Parent is not Grid selectorGrid ||
            selectorGrid.Parent is not Border selectorBorder ||
            selectorBorder.Parent is not StackPanel sectionPanel)
        {
            return;
        }

        foreach (UIElement child in sectionPanel.Children)
        {
            if (child is FrameworkElement element &&
                string.Equals(element.Tag as string, ReactionHelpMarker, StringComparison.Ordinal))
            {
                return;
            }
        }

        var helpText = new TextBlock
        {
            Tag = ReactionHelpMarker,
            Text = $"{quietHelp}\n{balancedHelp}\n{activeHelp}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 7, 2, 0),
            LineHeight = 17
        };
        helpText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Muted");
        helpText.SetResourceReference(TextBlock.FontSizeProperty, "Metric.Settings.BodyFontSize");

        int selectorIndex = sectionPanel.Children.IndexOf(selectorBorder);
        sectionPanel.Children.Insert(selectorIndex >= 0 ? selectorIndex + 1 : sectionPanel.Children.Count, helpText);
    }
}
