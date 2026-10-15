using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LarpStrap.App.Services;

/// <summary>Opens the official Roblox - Windows app, falling back to its Microsoft Store listing.</summary>
public sealed partial class RobloxStoreLauncher
{
    private const string StoreListing = "https://apps.microsoft.com/detail/9PMF91N3LZ3M";
    private const string StoreAppName = "Roblox - Windows";

    public async Task<bool> OpenAsync(CancellationToken cancellationToken = default)
    {
        var appId = await FindStoreAppIdAsync(cancellationToken).ConfigureAwait(false);
        if (appId is null)
        {
            OpenUri(StoreListing);
            return false;
        }

        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var start = new ProcessStartInfo(explorer) { UseShellExecute = true };
        start.ArgumentList.Add($"shell:AppsFolder\\{appId}");
        Process.Start(start)?.Dispose();
        return true;
    }

    /// <summary>
    /// Asks Windows to open the experience through the program registered for <c>roblox:</c> links. That is usually
    /// Roblox - Windows, but another launcher (Bloxstrap, Fishstrap) can own those links; the account is the one signed in there.
    /// </summary>
    public async Task<bool> OpenExperienceAsync(long placeId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(placeId);
        if (await FindStoreAppIdAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            OpenUri(StoreListing);
            return false;
        }

        try
        {
            OpenUri($"roblox://placeId={placeId}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            await OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    // Same lookup as PowerShell's Get-StartApps (the Start menu's app list), but in process through the Shell's own COM
    // object: no PowerShell is started. Shell objects want an STA thread, so it runs on its own one.
    private static Task<string?> FindStoreAppIdAsync(CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { result.TrySetResult(FindStoreAppId()); }
            catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { result.TrySetResult(null); }
        })
        { IsBackground = true, Name = "Roblox - Windows lookup" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task.WaitAsync(TimeSpan.FromSeconds(4), cancellationToken).ContinueWith(
            t => t.IsCompletedSuccessfully ? t.Result : null, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>The app id (AUMID) of Roblox - Windows from the Start menu's app list, or null if it is not installed.</summary>
    public static string? FindStoreAppId()
    {
        if (Type.GetTypeFromProgID("Shell.Application") is not { } type) return null;
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic? apps = shell.NameSpace("shell:AppsFolder");
            if (apps is null) return null;
            foreach (dynamic item in apps.Items())
            {
                if ((string)item.Name != StoreAppName) continue;
                var appId = (string)item.Path;
                return AppIdPattern().IsMatch(appId) ? appId : null;
            }
            return null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void OpenUri(string uri) =>
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();

    [GeneratedRegex("^[A-Za-z0-9._-]+![A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AppIdPattern();
}
