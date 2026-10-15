using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LarpStrap.Core.Updates;

/// <summary>What a release says about itself: <c>update.json</c>, signed in <c>update.json.sig</c> (docs/adr/0009).</summary>
public sealed record UpdateManifest(string Version, string File, long Size, string Sha256);

public enum UpdateStatus
{
    /// <summary>The newest signed release is not newer than this LarpStrap.</summary>
    UpToDate,

    /// <summary>A newer, verified installer is on disk at <see cref="UpdateCheck.SetupPath"/>.</summary>
    Ready,

    /// <summary>No release carries a manifest yet.</summary>
    NoRelease,

    /// <summary>Something failed verification (signature, host, size, hash, file name): nothing is kept or run.</summary>
    Rejected,

    /// <summary>Network or GitHub trouble; try again on the next start.</summary>
    Failed,
}

public sealed record UpdateCheck(UpdateStatus Status, Version? Available = null, string? SetupPath = null, string Reason = "", string? Sha256 = null);

/// <summary>
/// Finds, verifies and downloads LarpStrap updates from the public GitHub releases (docs/adr/0009). An installer is only
/// ever returned after: an ECDSA P-256 signature by LarpStrap's own key over the manifest's exact bytes, a version newer
/// than this one, every URL on GitHub's hosts, and the downloaded file's size and SHA-256 matching the manifest.
/// </summary>
public sealed partial class Updater(HttpClient http, byte[] trustedPublicKey, Version current, string downloadDirectory, string repository = Updater.Repository)
{
    public const string Repository = "Kaiz-2/LarpStrap";
    private const int MaxManifestBytes = 64 * 1024;
    private const long MaxSetupBytes = 100L * 1024 * 1024;

    private static readonly string[] AllowedHosts =
        ["api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    public async Task<UpdateCheck> CheckAndDownloadAsync(CancellationToken ct)
    {
        try
        {
            var release = await FindReleaseAsync(ct);
            if (release is null) return new(UpdateStatus.NoRelease);

            var manifestBytes = await DownloadSmallAsync(release.Manifest, ct);
            var signature = await DownloadSmallAsync(release.Signature, ct);
            if (!SignatureIsValid(manifestBytes, signature))
                return new(UpdateStatus.Rejected, Reason: "firma");

            var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestBytes, JsonOptions);
            if (manifest is null || !System.Version.TryParse(manifest.Version, out var available))
                return new(UpdateStatus.Rejected, Reason: "manifiesto");
            if (Normalize(available) <= Normalize(current))
                return new(UpdateStatus.UpToDate, available);
            if (!SetupFileName().IsMatch(manifest.File) || manifest.Size is <= 0 or > MaxSetupBytes
                || manifest.Sha256.Length != 64 || !release.Assets.TryGetValue(manifest.File, out var setupUrl))
                return new(UpdateStatus.Rejected, available, Reason: "manifiesto");

            var path = await DownloadSetupAsync(setupUrl, manifest, available, ct);
            return path is null ? new(UpdateStatus.Rejected, available, Reason: "instalador") : new(UpdateStatus.Ready, available, path, Sha256: manifest.Sha256);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException or UnauthorizedAccessException)
        {
            return new(UpdateStatus.Failed, Reason: ex.GetType().Name);
        }
        catch (UntrustedHostException)
        {
            return new(UpdateStatus.Rejected, Reason: "dominio");
        }
    }

    /// <summary>Deletes downloads of this version or older: once installed, they are only dead weight.</summary>
    public void DeleteOldDownloads()
    {
        if (!Directory.Exists(downloadDirectory)) return;
        foreach (var folder in Directory.EnumerateDirectories(downloadDirectory))
        {
            if (!System.Version.TryParse(Path.GetFileName(folder), out var version) || Normalize(version) > Normalize(current)) continue;
            try { Directory.Delete(folder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // retried on the next start
        }
    }

    /// <summary>True when the file at <paramref name="path"/> still has the manifest's hash (checked again right before running it).</summary>
    public static bool StillMatches(string path, string sha256)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool SignatureIsValid(byte[] manifest, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(trustedPublicKey, out _);
            return key.VerifyData(manifest, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private sealed record Release(Uri Manifest, Uri Signature, Dictionary<string, Uri> Assets);

    // Newest first, as GitHub lists them; drafts are skipped and pre-releases count (LarpStrap ships as pre-release).
    private async Task<Release?> FindReleaseAsync(CancellationToken ct)
    {
        var json = await DownloadSmallAsync(new Uri($"https://api.github.com/repos/{repository}/releases?per_page=10"), ct, MaxManifestBytes * 16);
        using var doc = JsonDocument.Parse(json);
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var assets = new Dictionary<string, Uri>(StringComparer.Ordinal);
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString();
                if (name is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri)) assets[name] = uri;
            }
            if (assets.TryGetValue("update.json", out var manifest) && assets.TryGetValue("update.json.sig", out var signature))
                return new(manifest, signature, assets);
        }
        return null;
    }

    private async Task<byte[]> DownloadSmallAsync(Uri uri, CancellationToken ct, int limit = MaxManifestBytes)
    {
        using var response = await SendAsync(uri, ct);
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > limit) throw new IOException("Respuesta demasiado grande.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private async Task<string?> DownloadSetupAsync(Uri uri, UpdateManifest manifest, Version version, CancellationToken ct)
    {
        var folder = Directory.CreateDirectory(Path.Combine(downloadDirectory, Normalize(version).ToString())).FullName;
        var path = Path.Combine(folder, manifest.File);
        if (File.Exists(path) && new FileInfo(path).Length == manifest.Size && StillMatches(path, manifest.Sha256))
            return path; // already downloaded and verified on an earlier start

        var partial = path + ".partial";
        try
        {
            var verified = false;
            using (var response = await SendAsync(uri, ct))
            await using (var body = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(partial))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var chunk = new byte[81920];
                long total = 0;
                int read;
                while ((read = await body.ReadAsync(chunk, ct)) > 0 && (total += read) <= manifest.Size)
                {
                    sha.AppendData(chunk, 0, read);
                    await output.WriteAsync(chunk.AsMemory(0, read), ct);
                }
                verified = total == manifest.Size
                    && Convert.ToHexString(sha.GetHashAndReset()).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase);
            }
            // Only now is the file closed, so a rejected download can really be deleted.
            if (!verified) return Discard(partial);
            File.Move(partial, path, overwrite: true);
            return path;
        }
        catch
        {
            Discard(partial);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken ct)
    {
        EnsureTrusted(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("LarpStrap-Updater");
        if (uri.Host == "api.github.com") request.Headers.Accept.ParseAdd("application/vnd.github+json");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        // Redirects are followed by HttpClient: the final address must be on GitHub too.
        if (response.RequestMessage?.RequestUri is { } final) EnsureTrusted(final);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static void EnsureTrusted(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new UntrustedHostException();
    }

    private static string? Discard(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [GeneratedRegex(@"^LarpStrap-Setup-\d+\.\d+\.\d+-win-x64\.exe$")]
    private static partial Regex SetupFileName();

    private sealed class UntrustedHostException : Exception;
}
