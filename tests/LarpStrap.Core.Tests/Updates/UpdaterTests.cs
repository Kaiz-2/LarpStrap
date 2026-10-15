using System.Net;
using System.Security.Cryptography;
using System.Text;
using LarpStrap.Core.Updates;

namespace LarpStrap.Core.Tests.Updates;

// No network: a fake GitHub serves releases, the signed manifest and the installer.
public sealed class UpdaterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-update-").FullName;
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly byte[] Setup = Encoding.UTF8.GetBytes("fake installer bytes");
    private const string SetupName = "LarpStrap-Setup-0.4.0-win-x64.exe";

    public void Dispose()
    {
        _key.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private Updater Updater(FakeGitHub github, string current = "0.3.0", ECDsa? trusted = null) =>
        new(new HttpClient(github), (trusted ?? _key).ExportSubjectPublicKeyInfo(), Version.Parse(current), _dir);

    private FakeGitHub GitHub(string version = "0.4.0", byte[]? setup = null, byte[]? signature = null, string? manifest = null)
    {
        var body = Encoding.UTF8.GetBytes(manifest ??
            $$"""{"version":"{{version}}","file":"{{SetupName}}","size":{{Setup.Length}},"sha256":"{{Convert.ToHexString(SHA256.HashData(Setup))}}"}""");
        return new FakeGitHub(body, signature ?? _key.SignData(body, HashAlgorithmName.SHA256), setup ?? Setup);
    }

    [Fact]
    public async Task A_newer_signed_release_is_downloaded_and_verified()
    {
        var check = await Updater(GitHub()).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Ready, check.Status);
        Assert.Equal(new Version(0, 4, 0), check.Available);
        Assert.Equal(Setup, File.ReadAllBytes(check.SetupPath!));
        Assert.EndsWith(Path.Combine("0.4.0", SetupName), check.SetupPath);
        Assert.True(LarpStrap.Core.Updates.Updater.StillMatches(check.SetupPath!, check.Sha256!));
    }

    [Fact]
    public void Downloads_of_installed_versions_are_deleted_and_newer_ones_kept()
    {
        foreach (var version in new[] { "0.2.0", "0.3.0", "0.4.0" })
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_dir, version)).FullName, SetupName), "x");

        Updater(GitHub()).DeleteOldDownloads();

        Assert.Equal(["0.4.0"], Directory.GetDirectories(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void The_shipped_public_key_is_a_P256_key()
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(UpdateTrust.PublicKey, out _);
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public async Task The_same_or_an_older_version_is_up_to_date()
    {
        Assert.Equal(UpdateStatus.UpToDate, (await Updater(GitHub("0.3.0")).CheckAndDownloadAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(UpdateStatus.UpToDate, (await Updater(GitHub("0.2.9")).CheckAndDownloadAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task A_forged_signature_is_rejected_and_nothing_is_downloaded()
    {
        var github = GitHub(signature: new byte[64]);

        var check = await Updater(github).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Rejected, check.Status);
        Assert.DoesNotContain(github.Requests, r => r.Contains(SetupName, StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(_dir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_release_signed_with_another_key_is_rejected()
    {
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var github = GitHub();

        var check = await Updater(github, trusted: attacker).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Rejected, check.Status);
    }

    [Fact]
    public async Task A_tampered_installer_is_rejected_and_deleted()
    {
        var tampered = Encoding.UTF8.GetBytes("evil installer bytes"); // same length, other content
        var check = await Updater(GitHub(setup: tampered)).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Rejected, check.Status);
        Assert.Empty(Directory.GetFiles(_dir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_manifest_pointing_at_an_unexpected_file_is_rejected()
    {
        var manifest = $$"""{"version":"0.4.0","file":"..\\evil.exe","size":{{Setup.Length}},"sha256":"{{Convert.ToHexString(SHA256.HashData(Setup))}}"}""";

        var check = await Updater(GitHub(manifest: manifest)).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Rejected, check.Status);
    }

    [Fact]
    public async Task A_redirect_outside_GitHub_is_rejected()
    {
        var github = GitHub();
        github.RedirectSetupTo = new Uri("https://example.com/LarpStrap-Setup.exe");

        var check = await Updater(github).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Rejected, check.Status);
        Assert.Empty(Directory.GetFiles(_dir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_verified_download_from_an_earlier_start_is_reused()
    {
        var first = await Updater(GitHub()).CheckAndDownloadAsync(TestContext.Current.CancellationToken);
        var github = GitHub();

        var second = await Updater(github).CheckAndDownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first.SetupPath, second.SetupPath);
        Assert.DoesNotContain(github.Requests, r => r.EndsWith(SetupName, StringComparison.Ordinal));
        Assert.True(LarpStrap.Core.Updates.Updater.StillMatches(second.SetupPath!, Convert.ToHexString(SHA256.HashData(Setup))));
    }

    [Fact]
    public async Task No_release_with_a_manifest_means_nothing_to_do()
    {
        var github = GitHub();
        github.WithoutManifest = true;

        Assert.Equal(UpdateStatus.NoRelease, (await Updater(github).CheckAndDownloadAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task GitHub_being_down_is_a_failure_to_retry_not_a_crash()
    {
        var github = GitHub();
        github.Down = true;

        Assert.Equal(UpdateStatus.Failed, (await Updater(github).CheckAndDownloadAsync(TestContext.Current.CancellationToken)).Status);
    }

    private sealed class FakeGitHub(byte[] manifest, byte[] signature, byte[] setup) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public Uri? RedirectSetupTo { get; set; }
        public bool WithoutManifest { get; set; }
        public bool Down { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri.ToString());
            if (Down) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var download = "https://github.com/Kaiz-2/LarpStrap/releases/download/v0.4.0/";
            var response = uri.AbsolutePath switch
            {
                "/repos/Kaiz-2/LarpStrap/releases" => Ok(Encoding.UTF8.GetBytes(Releases(download))),
                var p when p.EndsWith("/update.json", StringComparison.Ordinal) => Ok(manifest),
                var p when p.EndsWith("/update.json.sig", StringComparison.Ordinal) => Ok(signature),
                var p when p.EndsWith("/" + SetupName, StringComparison.Ordinal) => Ok(setup),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
            // Simulates HttpClient having followed a redirect: the final address is what the response reports.
            response.RequestMessage = RedirectSetupTo is not null && uri.AbsolutePath.EndsWith(SetupName, StringComparison.Ordinal)
                ? new HttpRequestMessage(HttpMethod.Get, RedirectSetupTo)
                : request;
            return Task.FromResult(response);
        }

        private string Releases(string download)
        {
            var assets = WithoutManifest
                ? $$"""[{"name":"{{SetupName}}","browser_download_url":"{{download}}{{SetupName}}"}]"""
                : $$"""[{"name":"update.json","browser_download_url":"{{download}}update.json"},{"name":"update.json.sig","browser_download_url":"{{download}}update.json.sig"},{"name":"{{SetupName}}","browser_download_url":"{{download}}{{SetupName}}"}]""";
            return $$"""[{"tag_name":"v0.4.1-draft","draft":true,"assets":[]},{"tag_name":"v0.4.0","draft":false,"assets":{{assets}}}]""";
        }

        private static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
    }
}
