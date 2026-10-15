using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LarpStrap.App.Views;

/// <summary>
/// Decorative icon (a Segoe Fluent Icons character). It has no automation peer, so screen readers skip it instead of
/// reading a private-use character; whatever it decorates carries its own accessible name. Use a named TextBlock for an
/// icon that conveys information on its own.
/// </summary>
public sealed class Glyph : TextBlock
{
    // FrameworkElement's own default is "no peer"; null keeps the glyph out of the UI Automation tree.
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}
