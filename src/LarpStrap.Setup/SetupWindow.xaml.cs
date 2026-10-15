using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using LarpStrap.App.Views;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Setup;
using Microsoft.Win32;

namespace LarpStrap.Setup;

/// <summary>
/// The wizard: Bienvenida → Opciones → Instalación → Listo, or Confirmar → Desinstalación → Listo. All file work is done
/// by <see cref="Installer"/>; this class only shows real stages and results. Success is shown only after the
/// installer's own verification passed.
/// </summary>
public partial class SetupWindow : Window
{
    private static readonly string[] InstallSteps = ["Bienvenida", "Opciones", "Instalación", "Listo"];
    private static readonly string[] UninstallSteps = ["Confirmar", "Desinstalación", "Listo"];

    private readonly SetupArguments _args;
    private readonly ExistingInstall? _existing;
    private readonly long _payloadSize;
    private readonly bool _hasPayload;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _cancellable;
    private Action? _primary, _secondary, _extra;
    private InstallResult? _installed;

    public SetupWindow(SetupArguments args)
    {
        InitializeComponent();
        _args = args;
        _existing = Installer.FindExisting(InstallTarget.UserUninstallKey);
        using (var payload = App.OpenPayload())
        {
            _hasPayload = payload is not null;
            _payloadSize = payload is null ? 0 : Installer.PayloadSize(payload);
        }
        BrandVersion.Text = $"Instalador · versión {App.Version}";
        using (var license = typeof(App).Assembly.GetManifestResourceStream("LICENSE.txt"))
            LicenseText.Text = license is null ? "" : new StreamReader(license).ReadToEnd();
        Closing += OnClosing;

        if (args.Uninstall) ShowUninstallConfirm();
        else ShowWelcome();
    }

    private string Verb => _existing?.Version is not { } v ? "Instalar" : v < App.Version ? "Actualizar" : v == App.Version ? "Reparar" : "Instalar";

    // ---- Install flow --------------------------------------------------------------------------------------------

    private void ShowWelcome()
    {
        Show(WelcomePage, InstallSteps, 0);
        WelcomeTitle.Text = Verb == "Instalar" && _existing?.Version > App.Version ? "Instalar una versión anterior" : $"{Verb} LarpStrap";

        Facts.Children.Clear();
        Facts.RowDefinitions.Clear();
        AddFact("Versión", App.Version.ToString());
        AddFact("Situación", _existing switch
        {
            null => "No está instalado: instalación nueva.",
            { Version: null } e => $"Hay una instalación de versión desconocida en {e.Directory}. Se sustituirán sus archivos; tu configuración se conserva.",
            var e when e.Version < App.Version => $"Está instalada la {e.Version} en {e.Directory}. Se actualizará en esa carpeta y tu configuración se conserva.",
            var e when e.Version == App.Version => $"Ya está instalada la {e.Version} en {e.Directory}. Reparar vuelve a copiar y comprobar sus archivos; tu configuración se conserva.",
            var e => $"Está instalada la {e.Version}, más reciente. Se sustituirá por la {App.Version}; tu configuración se conserva, pero una versión anterior puede ignorar ajustes nuevos.",
        });
        AddFact("Requisitos", $"Windows de 64 bits y .NET 10 Desktop Runtime: presente ({RuntimeInformation.FrameworkDescription}).");
        AddFact("Permisos", "Solo para tu usuario. No pide permisos de administrador.");
        AddFact("Espacio", $"{Math.Max(1, _payloadSize / (1024 * 1024) + 1)} MB");

        var (problem, info) = (App.IsElevated, _hasPayload, _existing is not null && Installer.HasInterruptedInstall(_existing.Directory)) switch
        {
            (true, _, _) => ("Has abierto el instalador como administrador. LarpStrap se instala para tu usuario y no debe ejecutarse elevado: cierra esta ventana y vuelve a abrir el instalador normalmente, sin «Ejecutar como administrador».", null),
            (_, false, _) => ("Este instalador no contiene LarpStrap: es una compilación de desarrollo. Usa el instalador que genera eng/Publish-Release.ps1.", null),
            (_, _, true) => ((string?)null, "Una instalación anterior se interrumpió. Antes de seguir se restaurará la versión que había."),
            _ => (null, null),
        };
        SetBanner(problem ?? info, problem is not null ? "Error" : "Info");

        SetActions(primary: ("_Continuar", problem is null ? ShowOptions : null), secondary: _existing is null ? null : ("_Desinstalar…", ShowUninstallConfirm),
            extra: ("Ver _licencia", ShowLicense), cancel: "Cancelar");
    }

    private void ShowLicense()
    {
        Show(LicensePage, InstallSteps, 0);
        SetActions(primary: ("_Volver", ShowWelcome), secondary: null, extra: null, cancel: "Cancelar");
    }

    private void ShowOptions()
    {
        Show(OptionsPage, InstallSteps, 1);
        DestinationBox.Text = _existing?.Directory ?? _args.Directory ?? InstallTarget.DefaultDirectory;
        DestinationBox.IsReadOnly = _existing is not null;
        BrowseButton.IsEnabled = _existing is null;
        DestinationNote.Text = _existing is not null
            ? "Se actualiza en su carpeta actual. Para moverla, desinstala primero."
            : "Recomendada: la carpeta de programas de tu usuario.";
        if (_existing is not null)
        {
            var target = InstallTarget.ForCurrentUser(_existing.Directory);
            StartMenuCheck.IsChecked = _existing.Shortcuts.Contains(target.StartMenuShortcut, StringComparer.OrdinalIgnoreCase);
            DesktopCheck.IsChecked = _existing.Shortcuts.Contains(target.DesktopShortcut, StringComparer.OrdinalIgnoreCase);
        }
        SetActions(primary: ($"_{Verb}", StartInstall), secondary: ("_Atrás", ShowWelcome), extra: null, cancel: "Cancelar");
        Validate();
        DestinationBox.Focus();
    }

    private void OnDestinationChanged(object sender, TextChangedEventArgs e) => Validate();

    private bool Validate()
    {
        var error = Installer.ValidateDirectory(DestinationBox.Text, _existing);
        DestinationError.Text = error ?? "";
        Announce(DestinationError);
        PrimaryButton.IsEnabled = error is null;
        return error is null;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Carpeta de instalación de LarpStrap", InitialDirectory = Directory.Exists(DestinationBox.Text) ? DestinationBox.Text : InstallTarget.DefaultDirectory };
        if (dialog.ShowDialog(this) != true) return;
        // An existing, non-empty folder gets its own LarpStrap subfolder instead of mixing files.
        var chosen = dialog.FolderName;
        DestinationBox.Text = Directory.Exists(chosen) && Directory.EnumerateFileSystemEntries(chosen).Any() && !File.Exists(Path.Combine(chosen, InstallTarget.ManifestFile))
            ? Path.Combine(chosen, "LarpStrap") : chosen;
    }

    private void StartInstall()
    {
        if (!Validate()) return;
        var directory = Path.GetFullPath(DestinationBox.Text.Trim());
        if (Installer.CheckWritable(directory, _payloadSize) is { } problem)
        {
            DestinationError.Text = problem;
            Announce(DestinationError);
            return;
        }
        _ = RunInstallAsync(directory, StartMenuCheck.IsChecked == true, DesktopCheck.IsChecked == true);
    }

    private async Task RunInstallAsync(string directory, bool startMenu, bool desktop)
    {
        var verb = Verb;
        Show(ProgressPage, InstallSteps, 2);
        ProgressTitle.Text = verb switch { "Actualizar" => "Actualizando LarpStrap", "Reparar" => "Reparando LarpStrap", _ => "Instalando LarpStrap" };
        ProgressNote.Text = $"Destino: {directory}";
        Progress.Value = 0;
        SetActions(primary: null, secondary: null, extra: null, cancel: "Cancelar");
        _busy = true;
        _cts = new CancellationTokenSource();
        var target = InstallTarget.ForCurrentUser(directory);
        var options = new InstallOptions(startMenu, desktop, App.Version, Environment.ProcessPath);
        var progress = new Progress<InstallProgress>(OnProgress);
        var token = _cts.Token;
        try
        {
            _installed = await Task.Run(() =>
            {
                using var payload = App.OpenPayload() ?? throw new InstallerException("Este instalador no contiene LarpStrap.");
                return new Installer(target).Install(payload, options, progress, token);
            });
            App.Log?.Write(LogLevel.Info, "Setup.Installed", ("appVersion", App.Version), ("count", _installed.FilesVerified));
            ShowInstalled(_installed, verb, directory);
        }
        catch (OperationCanceledException)
        {
            App.Log?.Write(LogLevel.Info, "Setup.Cancelled");
            ShowResult(InstallSteps, "CANCELADA", "Instalación cancelada", "No se ha cambiado nada: se canceló antes de tocar la instalación.", [], ok: false);
            SetActions(primary: ("_Cerrar", Close), secondary: null, extra: null, cancel: null);
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException or COMException)
        {
            App.Log?.Write(LogLevel.Error, "Setup.Failed", ("exception", ex.InnerException ?? ex));
            var message = ex is InstallerException ? ex.Message : "No se pudo escribir en la carpeta de instalación. Comprueba que tienes permiso y que el antivirus no la bloquea.";
            ShowFailure("No se pudo instalar", message, ex, retry: () => _ = RunInstallAsync(directory, startMenu, desktop));
        }
        finally
        {
            _busy = false;
            _cancellable = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnProgress(InstallProgress p)
    {
        (string text, double start, double end) = p.Stage switch
        {
            InstallStage.Checking => ("Comprobando la carpeta y la instalación anterior…", 0, 0.05),
            InstallStage.Extracting => ("Copiando los archivos de LarpStrap…", 0.05, 0.6),
            InstallStage.Replacing => ("Sustituyendo los archivos (ya no se puede cancelar)…", 0.6, 0.85),
            InstallStage.Shortcuts => ("Creando los accesos directos…", 0.85, 0.9),
            InstallStage.Registering => ("Registrando LarpStrap en Windows para poder desinstalarlo…", 0.9, 0.95),
            InstallStage.Verifying => ("Comprobando que todo quedó bien instalado…", 0.95, 1),
            InstallStage.Removing => ("Quitando los archivos de LarpStrap…", 0.2, 0.9),
            _ => ("", 0, 0),
        };
        // Progress only moves forward and only by work actually done (bytes extracted, files swapped).
        Progress.Value = Math.Max(Progress.Value, start + (end - start) * (p.Fraction ?? 0));
        if (StageText.Text != text)
        {
            StageText.Text = text;
            Announce(StageText);
        }
        _cancellable = p.Stage is InstallStage.Checking or InstallStage.Extracting;
        CancelButton.IsEnabled = _cancellable;
    }

    private void ShowInstalled(InstallResult result, string verb, string directory)
    {
        Progress.Value = 1;
        var shortcuts = result.Shortcuts.Count == 0 ? "Sin accesos directos, como elegiste."
            : "Accesos directos: " + string.Join(" y ", result.Shortcuts.Select(s => s.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Programs), StringComparison.OrdinalIgnoreCase) ? "menú Inicio" : "escritorio")) + ".";
        ShowResult(InstallSteps, "LISTO", verb switch { "Actualizar" => "LarpStrap está actualizado", "Reparar" => "LarpStrap está reparado", _ => "LarpStrap está instalado" },
            "Comprobado después de copiar:",
            [
                $"{result.FilesVerified} archivos copiados y comprobados con SHA-256.",
                $"LarpStrap.exe es la versión {App.Version}.",
                shortcuts,
                "Registrado en Configuración › Aplicaciones para poder desinstalarlo.",
                $"Carpeta: {directory}",
            ], ok: true);
        LaunchCheck.Visibility = Visibility.Visible;
        SetActions(primary: ("_Finalizar", Finish), secondary: null, extra: null, cancel: null);
    }

    private void Finish()
    {
        if (_installed is not null && LaunchCheck.IsChecked == true && LaunchCheck.Visibility == Visibility.Visible)
        {
            try { App.LaunchApp(_installed.AppPath); }
            catch (Win32Exception ex)
            {
                App.Log?.Write(LogLevel.Warning, "Setup.LaunchFailed", ("exception", ex));
                MessageBox.Show(this, "LarpStrap está instalado, pero no se pudo abrir. Ábrelo desde el menú Inicio.", "Instalador de LarpStrap");
            }
        }
        Close();
    }

    // ---- Uninstall flow ------------------------------------------------------------------------------------------

    private string? UninstallDirectory => _args.Directory ?? _existing?.Directory;

    private void ShowUninstallConfirm()
    {
        if (UninstallDirectory is not { } dir)
        {
            ShowResult(UninstallSteps, "DESINSTALAR", "LarpStrap no está instalado", "No hay ninguna instalación de LarpStrap registrada para tu usuario.", [], ok: false);
            SetActions(primary: ("_Cerrar", Close), secondary: null, extra: null, cancel: null);
            return;
        }
        Show(UninstallPage, UninstallSteps, 0);
        UninstallTitle.Text = _existing?.Version is { } v ? $"Desinstalar LarpStrap {v}" : "Desinstalar LarpStrap";
        UninstallRemoves.Text = $"Los archivos que instaló LarpStrap en {dir}, sus accesos directos y su entrada en Configuración › Aplicaciones.";
        SetActions(primary: ("_Desinstalar", () => _ = RunUninstallAsync(dir)), secondary: _args.Uninstall ? null : ("_Atrás", ShowWelcome), extra: null, cancel: "Cancelar");
        PrimaryButton.Style = (Style)FindResource("DangerButton");
        SetBanner(App.IsElevated ? "Has abierto el instalador como administrador. Ciérralo y vuelve a abrirlo normalmente." : null, "Error");
        PrimaryButton.IsEnabled = !App.IsElevated;
    }

    private async Task RunUninstallAsync(string directory)
    {
        Show(ProgressPage, UninstallSteps, 1);
        ProgressTitle.Text = "Desinstalando LarpStrap";
        ProgressNote.Text = $"Carpeta: {directory}";
        Progress.Value = 0;
        SetActions(primary: null, secondary: null, extra: null, cancel: "Cancelar");
        CancelButton.IsEnabled = false;
        _busy = true;
        var progress = new Progress<InstallProgress>(OnProgress);
        try
        {
            var result = await Task.Run(() => new Installer(InstallTarget.ForCurrentUser(directory)).Uninstall(progress));
            App.Log?.Write(LogLevel.Info, "Setup.Uninstalled", ("count", result.FilesRemoved));
            Progress.Value = 1;
            ShowResult(UninstallSteps, "LISTO", "LarpStrap se ha desinstalado", "Hecho:",
            [
                $"{result.FilesRemoved} archivos quitados, con sus accesos directos y su entrada en Configuración › Aplicaciones.",
                result.DirectoryRemoved ? "Carpeta de instalación borrada." : $"La carpeta {directory} se conservó porque contiene archivos que no instaló LarpStrap.",
                @"Tu configuración y tus logs siguen en %LOCALAPPDATA%\LarpStrap.",
            ], ok: true);
            SetActions(primary: ("_Finalizar", Close), secondary: null, extra: null, cancel: null);
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException)
        {
            App.Log?.Write(LogLevel.Error, "Setup.Failed", ("exception", ex.InnerException ?? ex));
            ShowFailure("No se pudo desinstalar", ex is InstallerException ? ex.Message : "No se pudo borrar algún archivo. Cierra LarpStrap y vuelve a intentarlo.", ex,
                retry: () => _ = RunUninstallAsync(directory), steps: UninstallSteps);
        }
        finally
        {
            _busy = false;
        }
    }

    // ---- Shared pieces -------------------------------------------------------------------------------------------

    private void ShowFailure(string title, string message, Exception ex, Action retry, string[]? steps = null)
    {
        var detail = ex.InnerException ?? ex;
        ShowResult(steps ?? InstallSteps, "ERROR", title, message,
        [
            "Pulsa Reintentar. Si se repite, descarga el instalador de nuevo y comprueba su SHA-256.",
            $"El log está en {App.LogDirectory} (evento Setup.Failed).",
            $"Detalle técnico: {detail.GetType().Name}: {detail.Message}",
        ], ok: false);
        SetActions(primary: ("_Reintentar", retry), secondary: ("_Cerrar", Close), extra: ("Abrir la carpeta de _logs", OpenLogs), cancel: null);
    }

    private void ShowResult(string[] steps, string overline, string title, string text, IReadOnlyList<string> rows, bool ok)
    {
        Show(ResultPage, steps, steps.Length - 1);
        ResultOverline.Text = overline;
        ResultTitle.Text = title;
        ResultText.Text = text;
        ResultList.Children.Clear();
        foreach (var row in rows)
        {
            var line = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var glyph = new Glyph { Style = (Style)FindResource("IconText"), Margin = new Thickness(0, 2, 12, 0), VerticalAlignment = VerticalAlignment.Top, Text = ok ? "" : "" };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, ok ? "OkBrush" : "InfoBrush");
            DockPanel.SetDock(glyph, Dock.Left);
            line.Children.Add(glyph);
            line.Children.Add(new TextBlock { Style = (Style)FindResource("BodyText"), TextWrapping = TextWrapping.Wrap, Text = row });
            ResultList.Children.Add(line);
        }
        ResultCard.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LaunchCheck.Visibility = Visibility.Collapsed;
        Announce(ResultTitle);
    }

    private void Show(FrameworkElement page, string[] steps, int current)
    {
        foreach (var p in new FrameworkElement[] { WelcomePage, OptionsPage, ProgressPage, ResultPage, UninstallPage, LicensePage })
            p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;
        SetBanner(null, "Info");
        PrimaryButton.Style = (Style)FindResource("PrimaryButton");

        Steps.Children.Clear();
        for (var i = 0; i < steps.Length; i++)
        {
            var done = i < current;
            var active = i == current;
            // Done: solid white disc with a check. Current: solid white disc with the number. Pending: ring only.
            var disc = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = done ? "✓" : (i + 1).ToString(Presentation.Culture),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            disc.SetResourceReference(Border.BorderBrushProperty, active || done ? "OnBrandBrush" : "OnBrandTrackBrush");
            if (active || done) disc.SetResourceReference(Border.BackgroundProperty, "OnBrandBrush");
            ((TextBlock)disc.Child).SetResourceReference(TextBlock.ForegroundProperty, active || done ? "OnHeroButtonBrush" : "OnBrandMutedBrush");
            var label = new TextBlock
            {
                Margin = new Thickness(12, 0, 0, 0),
                Text = steps[i],
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, active ? "OnBrandBrush" : "OnBrandMutedBrush");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            row.Children.Add(disc);
            row.Children.Add(label);
            AutomationProperties.SetName(row, $"Paso {i + 1} de {steps.Length}: {steps[i]}{(active ? " (actual)" : done ? " (hecho)" : "")}");
            Steps.Children.Add(row);
        }
    }

    private void SetActions((string Text, Action? Run)? primary, (string Text, Action Run)? secondary, (string Text, Action Run)? extra, string? cancel)
    {
        Configure(PrimaryButton, primary?.Text, primary?.Run);
        PrimaryButton.IsEnabled = primary?.Run is not null;
        _primary = primary?.Run;
        Configure(SecondaryButton, secondary?.Text, secondary?.Run);
        _secondary = secondary?.Run;
        Configure(ExtraButton, extra?.Text, extra?.Run);
        _extra = extra?.Run;
        CancelButton.Visibility = cancel is null ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.IsEnabled = true;
        if (PrimaryButton.Visibility == Visibility.Visible && PrimaryButton.IsEnabled) PrimaryButton.Focus();
    }

    private static void Configure(Button button, string? text, Action? run)
    {
        button.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        button.Content = text;
    }

    private void SetBanner(string? text, string kind)
    {
        WelcomeBanner.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        WelcomeBanner.Tag = kind;
        WelcomeBannerText.Text = text ?? "";
        if (text is not null) Announce(WelcomeBannerText);
    }

    private void AddFact(string label, string value)
    {
        var row = Facts.RowDefinitions.Count;
        Facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var name = new TextBlock { Style = (Style)FindResource("CaptionText"), Margin = new Thickness(0, 4, 24, 4), Text = label };
        var text = new TextBlock { Style = (Style)FindResource("BodyText"), Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap, Text = value };
        AutomationProperties.SetName(text, $"{label}: {value}");
        Grid.SetRow(name, row);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 1);
        Facts.Children.Add(name);
        Facts.Children.Add(text);
    }

    private static void Announce(TextBlock text)
    {
        if (text.Text.Length == 0) return;
        var peer = UIElementAutomationPeer.FromElement(text) ?? UIElementAutomationPeer.CreatePeerForElement(text);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void OpenLogs()
    {
        Directory.CreateDirectory(App.LogDirectory);
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { ArgumentList = { App.LogDirectory } })?.Dispose();
    }

    private void OnPrimary(object sender, RoutedEventArgs e) => _primary?.Invoke();

    private void OnSecondary(object sender, RoutedEventArgs e) => _secondary?.Invoke();

    private void OnExtra(object sender, RoutedEventArgs e) => _extra?.Invoke();

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (!_busy) Close();
        else if (_cancellable) CancelInstall();
    }

    // Closing mid-install could stop the swap halfway: only the cancellable part can be interrupted.
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        e.Cancel = true;
        if (_cancellable) CancelInstall();
    }

    private void CancelInstall()
    {
        _cts?.Cancel();
        StageText.Text = "Cancelando…";
        Announce(StageText);
        CancelButton.IsEnabled = false;
    }

    private static class Presentation
    {
        public static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
    }
}
