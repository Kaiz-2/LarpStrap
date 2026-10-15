using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LarpStrap.App.Services;

public interface IDialogService
{
    bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger);
    string? AskSavePath(string suggestedName, string filter);
    void OpenUri(Uri uri);
    void OpenFolder(string path);
}

public sealed class DialogService(ThemeService theme) : IDialogService
{
    public bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger)
    {
        var dialog = new Views.ConfirmDialog(title, message, confirmText, cancelText, isDanger)
        {
            Owner = Application.Current.MainWindow,
        };
        dialog.SourceInitialized += (_, _) => theme.ApplyWindowFrame(dialog);
        return dialog.ShowDialog() == true;
    }

    public string? AskSavePath(string suggestedName, string filter)
    {
        var dialog = new SaveFileDialog { FileName = suggestedName, Filter = filter, AddExtension = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    // Only called with URIs built by the app (roblox.com pages, fixed links); never with user-typed text.
    public void OpenUri(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();

    // Full path: a bare "explorer.exe" could resolve to a copy planted in the current folder (the exe's folder).
    public void OpenFolder(string path) => Process.Start(new ProcessStartInfo(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{path}\"")
    { UseShellExecute = true })?.Dispose();
}
