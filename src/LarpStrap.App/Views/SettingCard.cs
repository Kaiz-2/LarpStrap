using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LarpStrap.App.Views;

/// <summary>
/// Settings row: icon, title and description on the left, the control (Content) on the right; the control wraps
/// below on narrow widths or large text. Its look lives in Themes/Controls.xaml. Not focusable itself: the control is.
/// </summary>
public sealed class SettingCard : ContentControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    static SettingCard() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingCard), new FrameworkPropertyMetadata(typeof(SettingCard)));

    public SettingCard() => Focusable = false;

    /// <summary>Segoe Fluent Icons glyph.</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    // A group named after the setting, so screen readers announce the control in context.
    protected override AutomationPeer OnCreateAutomationPeer() => new SettingCardAutomationPeer(this);

    private sealed class SettingCardAutomationPeer(SettingCard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(SettingCard);
        protected override string GetNameCore() => ((SettingCard)Owner).Title;
    }
}
