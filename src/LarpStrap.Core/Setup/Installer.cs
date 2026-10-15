using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace LarpStrap.Core.Setup;

/// <summary>Where an installation goes. Every location is injectable so tests never touch the real profile.</summary>
public sealed record InstallTarget(string Directory, string StartMenuDirectory, string DesktopDirectory, string UninstallKey)
{
    public const string AppExe = "LarpStrap.exe";
    public const string SetupExe = "LarpStrap-Setup.exe";
    public const string ManifestFile = "LarpStrap.install.json";
    public const string ShortcutName = "LarpStrap.lnk";
    public const string UserUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LarpStrap";

    /// <summary>Per user, no administrator rights: the same place Windows uses for per-user programs.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LarpStrap");

    public static InstallTarget ForCurrentUser(string directory) => new(
        Path.GetFullPath(directory),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        UserUninstallKey);

    public string AppPath => Path.Combine(Directory, AppExe);
    public string StartMenuShortcut => Path.Combine(StartMenuDirectory, ShortcutName);
    public string DesktopShortcut => Path.Combine(DesktopDirectory, ShortcutName);
}

public sealed record ExistingInstall(string Directory, Version? Version, IReadOnlyList<string> Shortcuts);

public sealed record InstallOptions(bool StartMenuShortcut, bool DesktopShortcut, Version Version, string? SetupCopySource = null);

public enum InstallStage
{
    Checking,
    Extracting,
    Replacing,
    Shortcuts,
    Registering,
    Verifying,
    Removing,
}

public sealed record InstallProgress(InstallStage Stage, double? Fraction = null);

public sealed record InstallResult(int FilesVerified, IReadOnlyList<string> Shortcuts, string AppPath);

public sealed record UninstallResult(int FilesRemoved, bool DirectoryRemoved);

/// <summary>A failure the user can act on. <see cref="Exception.Message"/> is user-facing Spanish; details stay in <see cref="Exception.InnerException"/>.</summary>
public sealed class InstallerException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What LarpStrap installed in a folder: the uninstaller removes exactly this, nothing else.</summary>
public sealed record InstallManifest(string Version, IReadOnlyList<string> Files, IReadOnlyList<string> Shortcuts);

/// <summary>
/// Per-user installer engine. Files are extracted to a staging folder, then swapped in under a journal: every replaced
/// file is kept as <c>*.larpstrap-old</c> until the new set is complete, so a failure — or a crash, recovered on the next
/// run — restores the previous installation exactly. Never elevates, never touches Roblox, user settings
/// (%LOCALAPPDATA%\LarpStrap) or files it did not install.
/// </summary>
public sealed class Installer(InstallTarget target, Func<string, bool>? appIsRunning = null)
{
    private const string Staging = ".larpstrap-staging";
    private const string Journal = ".larpstrap-journal.json";
    private const string OldSuffix = ".larpstrap-old";
    private const int MaxPathLength = 200;

    private readonly Func<string, bool> _appIsRunning = appIsRunning ?? AnyAppRunningFrom;

    /// <summary>Test seam: runs before each file is swapped in (fault injection).</summary>
    internal Action<string>? BeforeReplace { get; init; }

    public static ExistingInstall? FindExisting(string uninstallKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(uninstallKey);
        if (key?.GetValue("InstallLocation") is not string dir || !File.Exists(Path.Combine(dir, InstallTarget.ManifestFile)))
            return null;
        var manifest = new Installer(new InstallTarget(dir, "", "", uninstallKey)).ReadManifest();
        return new(dir, Version.TryParse(key.GetValue("DisplayVersion") as string, out var v) ? v : null, manifest?.Shortcuts ?? []);
    }

    /// <summary>A previous install in this folder stopped mid-swap; the next install or uninstall restores it first.</summary>
    public static bool HasInterruptedInstall(string directory) => File.Exists(Path.Combine(directory, Journal));

    /// <summary>A user-facing reason the folder can't be used, or null. Doesn't create anything.</summary>
    public static string? ValidateDirectory(string? path, ExistingInstall? existing)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Elige una carpeta.";
        path = path.Trim();
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return "Elige una carpeta de este PC, no de la red.";
        if (!Path.IsPathFullyQualified(path)) return @"Escribe una ruta completa, por ejemplo C:\Users\tu-usuario\AppData\Local\Programs\LarpStrap.";
        string full;
        try { full = Path.GetFullPath(path).TrimEnd('\\'); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return "La ruta tiene caracteres no válidos."; }
        if (full.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || full.IndexOfAny(['*', '?', '"', '<', '>', '|', ':'], 2) >= 0) return "La ruta tiene caracteres no válidos.";
        if (full.Length > MaxPathLength) return $"La ruta es demasiado larga: usa una de menos de {MaxPathLength} caracteres.";
        if (string.Equals(Path.GetPathRoot(full)?.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase)) return "No instales en la raíz de una unidad: usa una subcarpeta.";

        var drive = new DriveInfo(Path.GetPathRoot(full)!);
        if (!drive.IsReady || drive.DriveType != DriveType.Fixed) return "Elige una carpeta de un disco fijo de este PC.";

        foreach (var system in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData })
        {
            var dir = Environment.GetFolderPath(system);
            if (dir.Length > 0 && IsSameOrInside(full, dir))
                return "Esa carpeta necesita permisos de administrador. LarpStrap se instala solo para tu usuario, sin ellos: elige una carpeta de tu perfil, como la propuesta.";
        }

        if (File.Exists(full)) return "Ya existe un archivo con ese nombre. Elige una carpeta.";
        var isExisting = existing is not null && string.Equals(Path.GetFullPath(existing.Directory).TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase);
        if (Directory.Exists(full) && !isExisting && !File.Exists(Path.Combine(full, InstallTarget.ManifestFile)) && Directory.EnumerateFileSystemEntries(full).Any())
            return "La carpeta no está vacía y no es una instalación de LarpStrap. Elige una carpeta vacía o nueva para no mezclar archivos.";
        return null;
    }

    /// <summary>Tries to write in the folder (or its nearest existing parent). Null when it can; else a user-facing reason.</summary>
    public static string? CheckWritable(string directory, long bytesNeeded)
    {
        var probeDir = Path.GetFullPath(directory);
        while (!Directory.Exists(probeDir))
            probeDir = Path.GetDirectoryName(probeDir) ?? throw new InstallerException("La unidad no existe.");
        try
        {
            var probe = Path.Combine(probeDir, $".larpstrap-probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return "No tienes permiso para escribir en esa carpeta. Elige otra de tu perfil; LarpStrap no pide permisos de administrador.";
        }
        var free = new DriveInfo(Path.GetPathRoot(probeDir)!).AvailableFreeSpace;
        return free < bytesNeeded * 2 ? $"No hay espacio suficiente: hacen falta {bytesNeeded * 2 / (1024 * 1024) + 1} MB libres." : null;
    }

    /// <summary>Uncompressed size of the payload, for the free-space check.</summary>
    public static long PayloadSize(Stream payload)
    {
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
        return zip.Entries.Sum(e => e.Length);
    }

    public InstallResult Install(Stream payload, InstallOptions options, IProgress<InstallProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new(InstallStage.Checking));
        EnsureNotRunning();
        RecoverInterrupted();
        var previous = ReadManifest();
        var createdDirectory = !Directory.Exists(target.Directory);
        Directory.CreateDirectory(target.Directory);

        var staging = Path.Combine(target.Directory, Staging);
        Dictionary<string, string> hashes;
        try
        {
            hashes = Extract(payload, staging, progress, ct);
            if (options.SetupCopySource is { } setup && !SamePath(setup, Path.Combine(target.Directory, InstallTarget.SetupExe)))
            {
                File.Copy(setup, Path.Combine(staging, InstallTarget.SetupExe));
                hashes[InstallTarget.SetupExe] = Hash(setup);
            }
            if (!hashes.ContainsKey(InstallTarget.AppExe))
                throw new InstallerException($"El paquete no contiene {InstallTarget.AppExe}: el instalador está dañado. Descárgalo de nuevo.");
        }
        catch
        {
            TryDeleteDirectory(staging);
            if (createdDirectory) TryDeleteEmptyDirectory(target.Directory);
            throw;
        }

        // From here on nothing is cancellable: the swap either completes or is rolled back.
        var journal = new JournalData([.. hashes.Keys.Order(StringComparer.Ordinal).Select(f => new JournalEntry(f, File.Exists(Path.Combine(target.Directory, f))))],
            createdDirectory, previous?.Version);
        File.WriteAllText(Path.Combine(target.Directory, Journal), JsonSerializer.Serialize(journal));
        var shortcuts = new List<string>();
        try
        {
            Commit(journal, staging, progress);

            progress?.Report(new(InstallStage.Shortcuts));
            shortcuts = UpdateShortcuts(options, previous);

            progress?.Report(new(InstallStage.Registering));
            Register(options.Version, hashes.Keys.Sum(f => new FileInfo(Path.Combine(target.Directory, f)).Length));

            WriteManifest(new InstallManifest(options.Version.ToString(), [.. hashes.Keys.Order(StringComparer.Ordinal)], shortcuts));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            try
            {
                Rollback(journal);
            }
            catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException)
            {
                // The journal stays: the next run of the installer finishes restoring the previous version.
                throw new InstallerException("La instalación falló y no se pudo deshacer del todo ahora. Cierra los programas que usen la carpeta y vuelve a abrir el instalador: terminará de restaurar la versión anterior.", ex);
            }
            RestoreRegistration(previous);
            foreach (var link in shortcuts.Where(l => previous?.Shortcuts.Contains(l, StringComparer.OrdinalIgnoreCase) != true))
                TryDeleteFile(link);
            throw new InstallerException("No se pudieron copiar los archivos de LarpStrap. Se ha dejado la instalación como estaba. Cierra los programas que puedan usar la carpeta (antivirus, Explorador) y pulsa Reintentar.", ex);
        }

        // Committed: the journal goes first, then the leftovers (a crash here only leaves harmless files behind).
        File.Delete(Path.Combine(target.Directory, Journal));
        foreach (var entry in journal.Files) TryDeleteFile(Path.Combine(target.Directory, entry.Path) + OldSuffix);
        if (previous is not null)
            foreach (var stale in previous.Files.Where(f => !hashes.ContainsKey(f))) TryDeleteFile(Path.Combine(target.Directory, stale));
        TryDeleteDirectory(staging);
        DeleteEmptySubdirectories(target.Directory);

        progress?.Report(new(InstallStage.Verifying));
        return Verify(hashes, shortcuts, options.Version);
    }

    /// <summary>Restores the state before an install that was interrupted mid-swap (power loss, killed process). True if it did.</summary>
    public bool RecoverInterrupted()
    {
        var path = Path.Combine(target.Directory, Journal);
        if (!File.Exists(path)) return false;
        JournalData? journal;
        try { journal = JsonSerializer.Deserialize<JournalData>(File.ReadAllText(path)); }
        catch (JsonException) { journal = null; }
        if (journal is null) throw new InstallerException($"Hay una instalación interrumpida que no se puede deshacer automáticamente. Borra la carpeta {target.Directory} y vuelve a instalar.");
        Rollback(journal);
        return true;
    }

    public UninstallResult Uninstall(IProgress<InstallProgress>? progress = null)
    {
        progress?.Report(new(InstallStage.Checking));
        EnsureNotRunning();
        RecoverInterrupted();
        var manifest = ReadManifest() ?? throw new InstallerException($"No hay ninguna instalación de LarpStrap en {target.Directory}.");

        progress?.Report(new(InstallStage.Removing));
        foreach (var link in manifest.Shortcuts.Where(PointsToApp)) TryDeleteFile(link);
        var removed = 0;
        foreach (var file in manifest.Files)
        {
            var full = Inside(file);
            if (!File.Exists(full)) continue;
            try { File.Delete(full); removed++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InstallerException($"No se pudo borrar {file}. Cierra LarpStrap y los programas que puedan usarlo, y vuelve a intentarlo.", ex);
            }
            TryDeleteFile(full + OldSuffix);
        }
        File.Delete(Path.Combine(target.Directory, InstallTarget.ManifestFile));
        TryDeleteDirectory(Path.Combine(target.Directory, Staging));
        DeleteEmptySubdirectories(target.Directory);
        var dirRemoved = TryDeleteEmptyDirectory(target.Directory);

        using (var key = Registry.CurrentUser.OpenSubKey(target.UninstallKey))
        {
            if (key?.GetValue("InstallLocation") is string location && SamePath(location, target.Directory))
                Registry.CurrentUser.DeleteSubKeyTree(target.UninstallKey, throwOnMissingSubKey: false);
        }
        return new(removed, dirRemoved);
    }

    private void EnsureNotRunning()
    {
        if (_appIsRunning(target.Directory))
            throw new InstallerException("LarpStrap está abierto. Ciérralo y pulsa Reintentar. Tus clientes de Roblox no se cierran.");
    }

    private Dictionary<string, string> Extract(Stream payload, string staging, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        TryDeleteDirectory(staging);
        Directory.CreateDirectory(staging);
        var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
        var total = Math.Max(1, zip.Entries.Sum(e => e.Length));
        long done = 0;
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/')) continue;
            var relative = entry.FullName.Replace('/', '\\');
            var destination = Path.GetFullPath(Path.Combine(staging, relative));
            // Zip slip: an entry must land inside the staging folder, and never use our reserved names.
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase) || relative.Contains(".larpstrap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(relative, InstallTarget.ManifestFile, StringComparison.OrdinalIgnoreCase))
                throw new InstallerException("El paquete contiene una ruta no válida: el instalador está dañado o manipulado. Descárgalo de nuevo y comprueba su SHA-256.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = entry.Open())
            using (var output = File.Create(destination))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = input.Read(buffer)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    sha.AppendData(buffer, 0, read);
                    done += read;
                    progress?.Report(new(InstallStage.Extracting, (double)done / total));
                }
                hashes[relative] = Convert.ToHexString(sha.GetHashAndReset());
            }
        }
        return hashes;
    }

    private void Commit(JournalData journal, string staging, IProgress<InstallProgress>? progress)
    {
        var i = 0;
        foreach (var entry in journal.Files)
        {
            progress?.Report(new(InstallStage.Replacing, (double)i++ / journal.Files.Count));
            BeforeReplace?.Invoke(entry.Path);
            var destination = Inside(entry.Path);
            if (entry.Existed)
            {
                TryDeleteFile(destination + OldSuffix);
                File.Move(destination, destination + OldSuffix);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(Path.Combine(staging, entry.Path), destination);
        }
    }

    private void Rollback(JournalData journal)
    {
        foreach (var entry in journal.Files)
        {
            var destination = Inside(entry.Path);
            var old = destination + OldSuffix;
            if (File.Exists(old))
            {
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(old, destination);
            }
            else if (!entry.Existed && File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
        TryDeleteDirectory(Path.Combine(target.Directory, Staging));
        File.Delete(Path.Combine(target.Directory, Journal));
        DeleteEmptySubdirectories(target.Directory);
        if (journal.CreatedDirectory) TryDeleteEmptyDirectory(target.Directory);
    }

    private List<string> UpdateShortcuts(InstallOptions options, InstallManifest? previous)
    {
        var wanted = new List<string>();
        if (options.StartMenuShortcut) wanted.Add(target.StartMenuShortcut);
        if (options.DesktopShortcut) wanted.Add(target.DesktopShortcut);
        foreach (var link in wanted)
            ShellLink.Create(link, target.AppPath, "Abre LarpStrap");
        // Shortcuts the user unticked this time are removed, but only ours (pointing at this installation).
        foreach (var link in previous?.Shortcuts.Except(wanted, StringComparer.OrdinalIgnoreCase) ?? [])
            if (PointsToApp(link)) TryDeleteFile(link);
        return wanted;
    }

    private void Register(Version version, long bytes)
    {
        using var key = Registry.CurrentUser.CreateSubKey(target.UninstallKey);
        var setup = Path.Combine(target.Directory, InstallTarget.SetupExe);
        key.SetValue("DisplayName", "LarpStrap");
        key.SetValue("DisplayVersion", version.ToString());
        key.SetValue("Publisher", "Leir");
        key.SetValue("DisplayIcon", $"{target.AppPath},0");
        key.SetValue("InstallLocation", target.Directory);
        key.SetValue("UninstallString", $"\"{setup}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{setup}\" --uninstall --silent");
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, bytes / 1024), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private void RestoreRegistration(InstallManifest? previous)
    {
        try
        {
            if (previous is null) Registry.CurrentUser.DeleteSubKeyTree(target.UninstallKey, throwOnMissingSubKey: false);
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(target.UninstallKey, writable: true);
                key?.SetValue("DisplayVersion", previous.Version);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private InstallResult Verify(Dictionary<string, string> hashes, List<string> shortcuts, Version version)
    {
        foreach (var (file, hash) in hashes)
        {
            var full = Inside(file);
            if (!File.Exists(full) || Hash(full) != hash)
                throw new InstallerException($"La comprobación final falló: {file} no está o no coincide. Vuelve a ejecutar el instalador para repararlo.");
        }
        var installed = FileVersionInfo.GetVersionInfo(target.AppPath);
        var actual = new Version(installed.FileMajorPart, installed.FileMinorPart, installed.FileBuildPart);
        if (actual != new Version(version.Major, version.Minor, Math.Max(0, version.Build)))
            throw new InstallerException($"La comprobación final falló: se esperaba LarpStrap {version} y hay {actual}. Descarga el instalador de nuevo.");
        foreach (var link in shortcuts.Where(l => !PointsToApp(l)))
            throw new InstallerException($"La comprobación final falló: el acceso directo {Path.GetFileName(link)} no apunta a LarpStrap.");
        using (var key = Registry.CurrentUser.OpenSubKey(target.UninstallKey))
        {
            if (key?.GetValue("InstallLocation") is not string location || !SamePath(location, target.Directory))
                throw new InstallerException("La comprobación final falló: Windows no tiene registrada la desinstalación.");
        }
        return new(hashes.Count, shortcuts, target.AppPath);
    }

    private InstallManifest? ReadManifest()
    {
        var path = Path.Combine(target.Directory, InstallTarget.ManifestFile);
        if (!File.Exists(path)) return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(path));
            // A tampered manifest must not make the uninstaller delete outside the installation.
            return manifest is null ? null : manifest with { Files = [.. manifest.Files.Where(f => IsInside(f))], Shortcuts = manifest.Shortcuts ?? [] };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteManifest(InstallManifest manifest) =>
        File.WriteAllText(Path.Combine(target.Directory, InstallTarget.ManifestFile), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

    private bool PointsToApp(string link) => ShellLink.ReadTarget(link) is { } t && SamePath(t, target.AppPath);

    private string Inside(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(target.Directory, relative));
        return IsInside(relative) ? full : throw new InstallerException("Ruta fuera de la carpeta de instalación.");
    }

    private bool IsInside(string relative) =>
        !Path.IsPathRooted(relative) && Path.GetFullPath(Path.Combine(target.Directory, relative)).StartsWith(Path.GetFullPath(target.Directory).TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase);

    private static bool AnyAppRunningFrom(string directory)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(InstallTarget.AppExe)))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } exe && IsSameOrInside(exe, directory)) return true;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Can't read it (e.g. an elevated LarpStrap): the swap itself will fail and roll back if it's ours.
                }
            }
        }
        return false;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSameOrInside(string path, string directory)
    {
        var dir = Path.GetFullPath(directory).TrimEnd('\\');
        var full = Path.GetFullPath(path).TrimEnd('\\');
        return string.Equals(full, dir, StringComparison.OrdinalIgnoreCase) || full.StartsWith(dir + '\\', StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any()) return false;
            Directory.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void DeleteEmptySubdirectories(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            TryDeleteEmptyDirectory(dir);
    }

    internal sealed record JournalEntry(string Path, bool Existed);

    internal sealed record JournalData(IReadOnlyList<JournalEntry> Files, bool CreatedDirectory, string? PreviousVersion);
}
