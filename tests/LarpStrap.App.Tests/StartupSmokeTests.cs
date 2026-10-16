using System.Windows;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.App.Views;
using LarpStrap.Core.Settings;
using ThemeMode = LarpStrap.Core.Settings.ThemeMode;

namespace LarpStrap.App.Tests;

// SIMULADA (no window is shown): does what startup does with the XAML. It loads App.xaml, applies each theme, and
// builds and lays out the main window, every page and the confirm dialog. 0.4.2 and 0.4.4 compiled and passed every
// other test, then closed at startup with a XamlParseException: a ToggleButton style on a Button, and StaticResources
// missing across nested dictionaries. An installed copy that crashes at startup never reaches its update check, so
// this test is a release gate (CI and eng/Publish-Release.ps1).
public sealed class StartupSmokeTests
{
    [Fact]
    public void App_resources_main_window_and_every_view_load_in_both_modes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { LoadEverything(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) Assert.Fail(failure.ToString());
    }

    private static void LoadEverything()
    {
        var app = new App();
        app.InitializeComponent();
        var theme = new ThemeService();
        var views = typeof(App).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(HomeView).Namespace && typeof(FrameworkElement).IsAssignableFrom(t)
                && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();
        Assert.Contains(typeof(HomeView), views);

        foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light })
        {
            theme.Apply(mode, AccentColor.Violet, WindowBackdrop.Solid);
            Layout(new MainWindow(new ShellViewModel()));
            Layout(new ConfirmDialog("Título", "Mensaje", "Aceptar", "Cancelar", isDanger: true));
            foreach (var type in views)
                Layout((FrameworkElement)Activator.CreateInstance(type)!);
        }
        app.Dispatcher.InvokeShutdown();
    }

    // Measuring applies control templates, so a broken style inside one fails here as it would on screen.
    private static void Layout(FrameworkElement element)
    {
        var root = element is Window { Content: FrameworkElement content } ? content : element;
        root.Measure(new Size(1280, 800));
        root.Arrange(new Rect(0, 0, 1280, 800));
        root.UpdateLayout();
    }
}
