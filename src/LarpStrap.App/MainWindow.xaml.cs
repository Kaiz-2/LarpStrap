using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LarpStrap.App.ViewModels;

namespace LarpStrap.App;

// View-only logic: screen-reader announcements, a short page fade, the launch dialog's focus and spinner
// (animations are skipped when Windows animations are off), and the maximised-window inset WindowChrome needs.
public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
        VersionText.Text = $"v{AboutViewModel.AppVersion}";
        shell.PropertyChanged += OnShellChanged;
        LaunchOverlay.IsVisibleChanged += OnLaunchOverlayVisibleChanged;
        StateChanged += (_, _) => Root.Margin = WindowState == WindowState.Maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.StatusMessage))
        {
            var peer = UIElementAutomationPeer.FromElement(LiveStatus) ?? UIElementAutomationPeer.CreatePeerForElement(LiveStatus);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        else if (e.PropertyName == nameof(ShellViewModel.Current) && SystemParameters.ClientAreaAnimation)
        {
            PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(150)));
        }
    }

    private void DismissStatus_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell) shell.Announce("");
    }

    // The dialog is modal while visible: focus goes to Cancel, and returns to the page when the launch ends.
    private void OnLaunchOverlayVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (LaunchOverlay.IsVisible)
        {
            // Deferred: the launch disables the button that started it in the same pass, which would drop focus to the window.
            Dispatcher.BeginInvoke(() => CancelLaunchButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
            if (SystemParameters.ClientAreaAnimation)
                SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.4)) { RepeatBehavior = RepeatBehavior.Forever });
        }
        else
        {
            SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            PageHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }
}
