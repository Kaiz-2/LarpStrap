# Roblox Windows Player: how it is deployed, launched and observed (as of 2026-10-13)

Source labels used on every fact:
- **[OFFICIAL]**: Roblox or Microsoft documentation or announcement.
- **[3P-CODE]**: third-party source code (Bloxstrap, Fishstrap, rbxbin…), read at HEAD on 2026-10-13 unless a tag is named.
- **[3P-DOC]**: third-party documentation, wiki, forum post or social post.
- **[LIVE]**: response from a public, unauthenticated Roblox endpoint, fetched 2026-10-13. No credentials were sent and no binaries were downloaded. Only text manifests and JSON were read.
- **[LOCAL-OBS]**: read-only observation on the requester's machine on 2026-10-13. A RobloxPlayerBeta 0.742.0.7421053 instance was already running there, started by a third-party launcher. I listed processes, read the window class, logs and registry. I did not launch anything. Ticket and tracker values were redacted.
- **[UNVERIFIED]**: comes from a search-result summary or an inference, and I did not confirm it against a primary source.

Anything dated before 2026 is marked *(possibly outdated)*.

Bloxstrap state checked: last commit on `main` is 2026-08-05 ("Fix Server Types (#6486)") [3P-CODE](https://github.com/bloxstraplabs/bloxstrap/commits/main). Fishstrap state checked: `main` was last pushed 2026-10-11 [3P-CODE](https://github.com/fishstrap/fishstrap).

---

## 1. Deployment protocol: version endpoint, channels, manifests, package mapping, MD5, mirrors, AppSettings.xml, 2025–2026 changes, official documentation

### Takeaway
Roblox does not document the deployment protocol. Every bootstrapper works from the same reverse-engineered flow:

1. `GET https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer[/channel/<ch>]` returns `clientVersionUpload` (the `version-<16hex>` GUID).
2. `GET <mirror>/[channel/common/]<guid>-rbxPkgManifest.txt` returns the package list: a `v0` header, then four lines per package (name, MD5, packed size, size).
3. Download each `<mirror>/<guid>-<package>`, check its MD5, and unzip it into `Versions\<guid>\<mapped subdir>`.
4. Write `AppSettings.xml`.

What changed in 2025–2026:
- Non-production channels now return **401** unless the request carries a per-user `Roblox-Channel-Token`.
- `DeployHistory.txt` has shown `version-hidden` instead of the GUID since **2026-03-17**.
- `bootstrapperVersion` in the client-version JSON is now an empty string.
- The package list ends with `RobloxPlayerInstaller.exe`. `Libraries.zip` and `redist.zip` are gone.
- Fishstrap merged "client distributions" (VNG Games / Vietnam) on **2026-09-27**. VNG uses the CDN sub-path `/vng`, a separate AppData folder (`RobloxPCVNG`) and no custom channels.

### Cited Findings

**Client-version endpoint and channels**
- Bloxstrap builds the path as `/v2/client-version/{BinaryType}`, with `BinaryType = "WindowsPlayer"`. For any channel other than `production` it appends `/channel/{channel}`. It queries `https://clientsettingscdn.roblox.com` first and falls back to `https://clientsettings.roblox.com` if that fails. Results are cached per `{channel}-{BinaryType}` — [3P-CODE Bloxstrap Deployment.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs)
- For a non-default channel, Bloxstrap treats HTTP 401, 403 and 404 as "bad channel" (`BadChannelCodes`) and throws `InvalidChannelException`. The bootstrapper then resets the channel to `production` and retries — [3P-CODE Deployment.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs), [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- Live response on 2026-10-13 for `GET https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer`:
  `{"version":"0.742.0.7421053","clientVersionUpload":"version-cec3ad5889b447cf","bootstrapperVersion":""}`.
  `bootstrapperVersion` is now an empty string — [LIVE](https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer)
- Live response on 2026-10-13 for `GET .../v2/client-version/WindowsPlayer/channel/zbeta` without auth: HTTP **401** with body `{"errors":[{"code":5,"message":"Not authorized to perform this action."}]}` — [LIVE](https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer/channel/zbeta)
- Fishstrap handles private channels as follows:
  - It calls the authenticated endpoint `clientsettings.roblox.com/v2/user-channel?binaryType=<bt>` with the user's cookie. The response gives a `UserChannel`.
  - It sends a `Roblox-Channel-Token` header on client-version requests whenever it holds a `ChannelToken`.
  - `IsChannelPrivate(ch)` probes the client-version endpoint and treats 401/403/404 as private.
  - It maps `production` to `live`.

  — [3P-CODE Fishstrap Deployment.cs](https://github.com/fishstrap/fishstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs)
- The Go library `rbxbin` documents the same flow. When no channel is given, it calls `ClientSettingsV2.GetUserChannel`, takes `uc.Channel` and `uc.Token`, and adds `Roblox-Channel-Token: <token>` to the client-version request. A code comment says the channel resolves to "LIVE" if the client is not authenticated — [3P-CODE sewnie/rbxbin deployment.go](https://github.com/sewnie/rbxbin/blob/master/deployment.go)
- Code search also finds the `Roblox-Channel-Token` header in Froststrap's `Deployment.cs`, Bubblestrap, OrangeBlox and the RoSeal extension, so the scheme is widely implemented. I did not read those files — [3P-CODE GitHub code search: Froststrap/Froststrap Froststrap/RobloxInterfaces/Deployment.cs](https://github.com/Froststrap/Froststrap/blob/main/Froststrap/RobloxInterfaces/Deployment.cs)
- Fishstrap can flag that a channel is behind production (`behindProductionCheck` compares versions). It can also read a version's timestamp from the `Last-Modified` header of `<guid>-rbxPkgManifest.txt` — [3P-CODE Fishstrap Deployment.cs](https://github.com/fishstrap/fishstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs)
- Live check: `Last-Modified` on `setup-aws.rbxcdn.com/version-cec3ad5889b447cf-rbxPkgManifest.txt` is `Mon, 05 Oct 2026 22:39:06 GMT` — [LIVE](https://setup-aws.rbxcdn.com/version-cec3ad5889b447cf-rbxPkgManifest.txt)

**Where the channel comes from (Bloxstrap)**
Bloxstrap checks these sources in order:
1. The `-channel` flag.
2. The regex `channel:([a-zA-Z0-9-_]+)` applied to the launch URI.
3. The registry value `HKCU\SOFTWARE\ROBLOX Corporation\Environments\RobloxPlayer\Channel`, value name `www.roblox.com`.

After resolving, it writes that value back: an empty string for production, otherwise the channel name — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs). Local check: that key exists with a single value named `www.roblox.com` — [LOCAL-OBS]

**rbxPkgManifest.txt format**
- Line 1 must be `v0`; anything else throws. After that, entries repeat in groups of four lines: `name`, `MD5 signature`, `packed size`, `unpacked size` — [3P-CODE Bloxstrap PackageManifest.cs (derived from MaximumADHD's Roblox Studio Mod Manager)](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Models/Manifest/PackageManifest.cs)
- The parser stops when it reaches `RobloxPlayerLauncher.exe`, a legacy entry — [3P-CODE PackageManifest.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Models/Manifest/PackageManifest.cs)
- Live manifest for `version-cec3ad5889b447cf`: header `v0`, then 22 entries in this order:
  `RobloxApp.zip, content-avatar.zip, content-configs.zip, content-fonts.zip, content-models.zip, content-sky.zip, content-sounds.zip, shaders.zip, ssl.zip, content-textures2.zip, content-textures3.zip, content-terrain.zip, content-platform-fonts.zip, content-platform-dictionaries.zip, extracontent-places.zip, extracontent-luapackages.zip, extracontent-translations.zip, extracontent-models.zip, extracontent-textures.zip, WebView2.zip, WebView2RuntimeInstaller.zip, RobloxPlayerInstaller.exe`.
  - Example entry: `RobloxApp.zip / fcb9e6e381cdc82049c9bfe03ae54878 / 137930981 / 177468971`.
  - `Libraries.zip` and `redist.zip`, which Bloxstrap still maps, are absent.

  — [LIVE](https://setup.rbxcdn.com/version-cec3ad5889b447cf-rbxPkgManifest.txt)
- `<guid>-rbxManifest.txt` uses two lines per file: relative path, then MD5. In the live version it lists `COPYRIGHT.txt, RobloxCrashHandler.exe, RobloxPlayerBeta.dll, RobloxPlayerBeta.exe, ssl\cacert.pem, WebView2Loader.dll, MicrosoftEdgeWebview2Setup.exe` plus the content files — [LIVE](https://setup.rbxcdn.com/version-cec3ad5889b447cf-rbxManifest.txt); [3P-CODE Bloxstrap FileManifest.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Models/Manifest/FileManifest.cs)
- The Bloxstrap wiki says rbxManifest "is no longer used for integrity checks" but is still generated. *(Possibly outdated: last edited 2024-10-17.)* — [3P-DOC Bloxstrap wiki deep dive](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)

**Package-to-directory map (Bloxstrap `CommonAppData` + `RobloxPlayerData`)**

| Package | Target directory |
|---|---|
| `RobloxApp.zip` | root |
| `Libraries.zip`, `redist.zip`, `WebView2.zip` | root |
| `shaders.zip` | `shaders\` |
| `ssl.zip` | `ssl\` |
| `WebView2RuntimeInstaller.zip` | `WebView2RuntimeInstaller\` (extracted only if WebView2 is missing) |
| `content-avatar/configs/fonts/sky/sounds/models.zip` | `content\avatar\`, `content\configs\`, `content\fonts\`, `content\sky\`, `content\sounds\`, `content\models\` |
| `content-textures2.zip` | `content\textures\` |
| `content-textures3.zip` | `PlatformContent\pc\textures\` |
| `content-terrain.zip` | `PlatformContent\pc\terrain\` |
| `content-platform-fonts.zip` | `PlatformContent\pc\fonts\` |
| `content-platform-dictionaries.zip` | `PlatformContent\pc\shared_compression_dictionaries\` |
| `extracontent-luapackages/translations/models/textures/places.zip` | `ExtraContent\LuaPackages\`, `ExtraContent\translations\`, `ExtraContent\models\`, `ExtraContent\textures\`, `ExtraContent\places\` |

- Code comment on how to find new mappings: "in case a new package is added, you can find the corresponding directory by opening the stock bootstrapper in a hex editor" — [3P-CODE CommonAppData.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/AppData/CommonAppData.cs); [3P-CODE RobloxPlayerData.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/AppData/RobloxPlayerData.cs)
- A package with no mapping is skipped at extraction, with the log line "WARNING: {name} was not found in the package map!" — [3P-CODE Bootstrapper.cs `ExtractPackage`](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- WebView2 detection reads `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}` and the matching HKCU key. The runtime installer runs with `/silent /install` — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs); [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)

**MD5 verification and download cache**
- Packages download from `Deployment.GetLocation("/{guid}-{package.Name}")`. The download is hashed with MD5 and compared to `package.Signature`; a mismatch throws `ChecksumFailedException`. A code comment notes the signatures were fetched earlier over HTTPS — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- Bloxstrap reuses the official cache at `%LOCALAPPDATA%\Roblox\Downloads\<md5>` when present. A cached file that fails the MD5 check is deleted and downloaded again. Installed package hashes are recorded in `DistributionState.PackageHashes` — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)

**CDN mirrors and how Bloxstrap picks one**
- Bloxstrap races these mirrors, each with a start delay in seconds:

  | Mirror | Delay |
  |---|---|
  | `https://setup.rbxcdn.com` | 0 |
  | `https://setup-aws.rbxcdn.com` | 2 |
  | `https://setup-ak.rbxcdn.com` | 2 |
  | `https://roblox-setup.cachefly.net` | 2 |
  | `https://s3.amazonaws.com/setup.roblox.com` | 4 |

  The first mirror whose `GET <mirror>/versionStudio` returns exactly `version-012732894899482c` wins. That string is described as "the version hash for the last MFC studio". This race is also Bloxstrap's connectivity check — [3P-CODE Deployment.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs)
- Live check: `https://setup.rbxcdn.com/versionStudio` returns `version-012732894899482c` — [LIVE](https://setup.rbxcdn.com/versionStudio)
- Wiki mirror roles: S3 is the origin; `setup.roblox.com` is an alias to avoid ("HTTPS broken"); CloudFront = `setup-aws`, Akamai = `setup-ak`, Cachefly = `roblox-setup.cachefly.net` / `setup-cfly.rbxcdn.com`; `setup.rbxcdn.com` is the preferred balancer. *(Possibly outdated: 2024.)* — [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)
- Fishstrap PR #1499 drops `roblox-setup.cachefly.net` from its mirror list (now `CommonDist.CdnUrls`) — [3P-CODE Fishstrap PR #1499](https://github.com/fishstrap/fishstrap/pull/1499)
- The `/channel/common/` prefix:
  - Bloxstrap always uses `<mirror>/channel/common/<resource>`.
  - Fishstrap adds `/channel/common` only for non-default channels.
  - Both forms return 200 for production (live HEAD check on `setup.rbxcdn.com/channel/common/version-cec3…-rbxPkgManifest.txt`).

  — [3P-CODE Bloxstrap Deployment.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs); [3P-CODE Fishstrap Deployment.cs](https://github.com/fishstrap/fishstrap/blob/main/Bloxstrap/RobloxInterfaces/Deployment.cs); [LIVE](https://setup.rbxcdn.com/channel/common/version-cec3ad5889b447cf-rbxPkgManifest.txt)

**DeployHistory.txt**
- Since 2026-03-17 (`0.713.0.7130910`), new `WindowsPlayer` and `Studio64` lines read `New WindowsPlayer version-hidden at …, file version: …, git hash: …` instead of the GUID. The last line with a visible WindowsPlayer GUID is `version-b130242ed064436f`, 2026-03-11, `0.712.0.7120919`.
- The latest lines show `0.743.0.7431129` deployed on 2026-10-12, while the live client-version endpoint still returns `0.742.0.7421053`. This suggests staged or hidden deployments.

— [LIVE](https://setup.rbxcdn.com/DeployHistory.txt)

**AppSettings.xml**
- After extraction, Bloxstrap writes `Versions\<guid>\AppSettings.xml` containing:
  `<?xml version="1.0" encoding="UTF-8"?><Settings><ContentFolder>content</ContentFolder><BaseUrl>http://www.roblox.com</BaseUrl></Settings>`

  — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- Related local observation: the running client passes `--baseUrl http://www.roblox.com/` to its crash handler, which is consistent with `BaseUrl` — [LOCAL-OBS]

**Install layout**
- Official layout: `%LOCALAPPDATA%\Roblox\Versions\<guid>\`, with the package cache in `%LOCALAPPDATA%\Roblox\Downloads`. The wiki says system-wide installs are no longer supported. *(Possibly outdated: 2024.)* — [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)
- Bloxstrap installs into its own `%LOCALAPPDATA%\Bloxstrap\Versions\<guid>\`. Local check: the running client's path is `...\Bloxstrap\Versions\version-cec3ad5889b447cf\RobloxPlayerBeta.exe` — [LOCAL-OBS]

**Client distributions (Fishstrap PR #1499, branch `feature/client-distributions`, "Client distributions (VNG)", merged 2026-09-27T14:22:05Z)**
- PR description: "Adds VNGGames and groundwork for future distributions."
- New `IDistribution` with `RobloxDomain`, `CdnUrls`, `RobloxPlayerData` and `RobloxStudioData`.
- `DistributorType { Global, VNGGames }`, with `//ChinaJointVenture, // should we even support this?` commented out.
- `VNGGamesDist.RobloxDomain = "robloxapp.vnggames.com"`.
- `RobloxPlayerVNGData`:
  - `ProductName = "Roblox VNG"`
  - `BinaryType = "WindowsPlayer"`
  - `AppDataDirectory = %LOCALAPPDATA%\RobloxPCVNG`
  - `CdnExtension = "/vng"`, so package URLs become `<mirror>/vng/<guid>-<pkg>`
  - `SupportsCustomDeployments = false`, so the channel is always forced to production
- Logs and downloads move to `<AppDataDirectory>\logs` and `\Downloads`.

— [3P-CODE Fishstrap PR #1499](https://github.com/fishstrap/fishstrap/pull/1499)

**Official documentation**
- I found no Roblox document describing client-version, manifests, mirrors or channels. The only official Roblox page on launching is the deep-links doc (see §2), which marks its process as deprecated — [OFFICIAL create.roblox.com deeplinks](https://create.roblox.com/docs/production/promotion/deeplinks)
- The public endpoints themselves carry `X-Terms-Message: Terms apply https://help.roblox.com/hc/articles/115004647846-Roblox-Terms-of-Use` — [LIVE](https://setup-aws.rbxcdn.com/version-cec3ad5889b447cf-rbxPkgManifest.txt)

### Inferences
- A minimal installer needs three things: the client-version JSON, rbxPkgManifest v0 with MD5, and the package-to-directory map plus `AppSettings.xml`. It must also tolerate unknown packages, as Bloxstrap does by skipping them; today that is `RobloxPlayerInstaller.exe`.
- Bloxstrap's parser stops at `RobloxPlayerLauncher.exe`, which no longer appears in the manifest. So `RobloxPlayerInstaller.exe` is parsed as an ordinary package and probably downloaded, then skipped at extraction as unmapped. Not verified at runtime.
- `Libraries.zip` and `redist.zip` are kept in the map only for older versions.
- Non-production channels now effectively need an authenticated user-channel token. An original launcher that never handles credentials should support only `production` (LIVE). On a 401, 403 or 404 it should fall back to production, as Bloxstrap does.
- With DeployHistory hidden, the client-version endpoint is the only practical source of the current GUID. Old GUIDs can still be fetched if you already know them.

### Gaps
- No official specification exists, so all formats are reverse-engineered. The meaning of `bootstrapperVersion` being empty, and the exact date it became empty, are unknown.
- I did not fetch the `/vng/` manifest or VNG's client-version endpoint, so I cannot confirm VNG uses the same GUID as Global.
- I found no announcement explaining why DeployHistory GUIDs are hidden or when channel restrictions began.
- I did not read Froststrap's code beyond the code-search hit.

---

## 2. Launch protocol: roblox-player URI, mapping to RobloxPlayerBeta arguments, official launcher/installer, `--launch-to-tray`, registry keys

### Takeaway
- **Since March 2024**, `RobloxPlayerBeta.exe` is itself the registered `roblox-player:` handler. It receives the raw URI as `%1`, checks for updates, and if needed hands off to `RobloxPlayerInstaller.exe` with the same URI. `RobloxPlayerLauncher.exe` is retired.
- The URI has the form `roblox-player:1+key:value+…`.
- **Before 2024**, launchers translated the URI into flags: `launchmode` → `--<mode>`, `gameinfo` → `-t`, `placelauncherurl` → `-j`, `launchtime` → `--launchtime=`, `browsertrackerid` → `-b`, `robloxLocale` → `--rloc`, `gameLocale` → `--gloc`. RobloxPlayerBeta still accepts `--app -t <ticket> -b <btid>` today.
- **By mid-2026**, the client writes its own autostart entry, `HKCU\…\Run\RobloxPlayerBeta = "<path>\RobloxPlayerBeta.exe" --launch-to-tray`, tied to the in-app "Device Preferences" options.

### Cited Findings

**URI structure**
- Format: `roblox-player:1+key:value+key:value…`. The `+` separates pairs, and the leading `1` is a protocol identifier. Example keys: `launchmode`, `robloxLocale`, `gameLocale`, `LaunchExp`. *(2024, possibly outdated.)* — [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)
- A traced real-world link carried `launchmode`, `gameinfo`, `placelauncherurl`, `browsertrackerid`, `robloxLocale`, `gameLocale` and `channel`. The registry command pointed to `RobloxPlayerBeta.exe` with `%1` — [3P-DOC Opera forum (via search summary) [UNVERIFIED]](https://forums.opera.com/post/339314)
- Bloxstrap v2.5.4 mapped URI keys to client arguments; the comment says it excluded `roblox-player` and `launchtime`:

  | URI key | Client argument |
  |---|---|
  | `launchmode` | `--` + value (e.g. `--play`, `--app`) |
  | `gameinfo` | `-t ` (auth ticket) |
  | `placelauncherurl` | `-j ` (join script URL) |
  | `launchtime` | `--launchtime=` |
  | `browsertrackerid` | `-b ` |
  | `robloxLocale` | `--rloc ` |
  | `gameLocale` | `--gloc ` |
  | `channel` | `-channel ` |

  *(Old tag, outdated as a mechanism.)* — [3P-CODE Bloxstrap v2.5.4 ProtocolHandler.cs](https://github.com/bloxstraplabs/bloxstrap/blob/v2.5.4/Bloxstrap/ProtocolHandler.cs)
- Wiki example of the old model: `roblox-player:1+launchmode:app` became `RobloxPlayerBeta.exe --app --rloc en_us --gloc en_us` — [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)
- Modern `roblox://experiences/start` parameters per the wiki: `placeId, gameInstanceId, accessCode, linkCode, launchData, joinAttemptId, joinAttemptOrigin, reservedServerAccessCode, callId, browserTrackerId, userId` — [3P-DOC wiki](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)
- The official deep-link formats:
  - `https://www.roblox.com/games/start?placeId=<id>&launchData=<string>`
  - `roblox://placeId=<id>&launchData=<string>`
  - Parameters: `placeId`, `userId`, `accessCode`, `linkCode`, `gameInstanceId`, `launchData`.
  - Decoded `launchData` is limited to 200 bytes. The page says "This process is deprecated" and points to share links.

  — [OFFICIAL Creator docs: deep links](https://create.roblox.com/docs/production/promotion/deeplinks)
- Reported bug: on a cold launch, URL-encoded JSON in launch data is decoded incorrectly — [3P-DOC DevForum bug report](https://devforum.roblox.com/t/cold-launching-roblox-using-launch-uris-with-launch-data-decodes-url-encoded-json-incorrectly/4846509)

**Current launch model (since March 2024)**
- `RobloxPlayerBeta.exe` is the protocol handler and checks for updates itself. When an update is needed, it starts `RobloxPlayerInstaller.exe` with the same URI, which then starts the new `RobloxPlayerBeta.exe` with that URI. `RobloxPlayerLauncher.exe` is no longer used.
- In the old model, `RobloxPlayerLauncher.exe` handled the protocol, updated, parsed the URI into flags, started RobloxPlayerBeta, waited for a start signal and exited.
- Bloxstrap v2.6.0 began passing the URI straight to RobloxPlayerBeta.

— [3P-DOC wiki (dated "As of March 2024")](https://github.com/bloxstraplabs/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works)

**How Bloxstrap starts the client today**
- If an argument starts with `roblox:` or `roblox-player:`, that argument becomes `RobloxLaunchArgs`. `-player "<uri>"` sets it the same way. It is then passed unchanged as `ProcessStartInfo.Arguments`, with `WorkingDirectory` set to the version folder — [3P-CODE LaunchSettings.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/LaunchSettings.cs); [3P-CODE Bootstrapper.cs `StartRoblox`](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- If the AppCompat layer `RUNASADMIN` is set for the exe under `HKCU`/`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`, Bloxstrap launches with the `runas` verb. Error 1223 means the user cancelled UAC — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- Code comment: "v2.2.0 - byfron will trip if we keep a process handle open for over a minute". Bloxstrap therefore disposes the `Process` object right after taking the PID. *(Old note, possibly outdated, but still in HEAD.)* — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- After launch, Bloxstrap starts its watcher as `Bloxstrap.exe -watcher "<base64 JSON {ProcessId, LogFile, AutoclosePids}>"`. The watcher polls once per second until that PID is gone — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs); [3P-CODE Watcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Watcher.cs)
- Before launching, and only when the "confirm launches" setting is on, Bloxstrap checks `Mutex.TryOpenExisting("ROBLOX_singletonMutex")` to warn that Roblox is already running. Its comment says the mutex "often hangs around for a few seconds after the window closes" — [3P-CODE LaunchHandler.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/LaunchHandler.cs)

**Observed command line (current client, redacted)**
- The running RobloxPlayerBeta, started by a third-party parent (`Real.exe`), has the command line `"<…>\RobloxPlayerBeta.exe" --app -t <ticket> -b <btid>`, with no `-j`. So flag-style launch still works in 0.742 — [LOCAL-OBS]
- Its log shows `[FLog::LogWin32BTId] LogWin32BTId, App, web launch, websiteBTId is <n>` — [LOCAL-OBS]

**`--launch-to-tray` and autostart**
- Local registry has `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `RobloxPlayerBeta`, data `"C:\Users\…\AppData\Local\Fishstrap\Versions\version-02c37bc51a384b8f\RobloxPlayerBeta.exe" --launch-to-tray`. The path points into a Fishstrap version folder, which suggests the client registered its own executable path — [LOCAL-OBS]
- A RoUtility post says: "The Roblox app now launches automatically when your computer starts and minimizes to the system tray by default". It says to disable it under Settings > Device Preferences by turning off the bottom two options. The post date is 2026-06-19, decoded from the status snowflake — [3P-DOC RoUtility on X](https://x.com/RoUtilityNews/status/2067801134559834501)
- A March 2026 DevForum reply describes Settings → Device Preferences → disable "minimize to tray when closed", in a thread about Roblox staying in Task Manager after closing — [3P-DOC DevForum (via search summary) [UNVERIFIED]](https://devforum.roblox.com/t/roblox-still-open-in-the-task-manager/4528699)
- I found no official Roblox announcement for the autostart/tray feature. I also found no Bloxstrap or Fishstrap code handling `--launch-to-tray` — [3P-DOC search, no results](https://github.com/fishstrap/fishstrap/issues/1332)

**Registry keys**
- Protocol handlers live at `HKCU\Software\Classes\{roblox, roblox-player}`, with `(default) = "URL: Roblox Protocol"`, `URL Protocol = ""`, `DefaultIcon`, and `shell\open\command = "<handler>" <param>`.
  - Bloxstrap registers `"…\Bloxstrap.exe" -player "%1"`.
  - Studio uses `roblox-studio`, `roblox-studio-auth`, the `Roblox.Place` class, and `.rbxl`/`.rbxlx`.

  — [3P-CODE WindowsRegistry.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Utility/WindowsRegistry.cs). Locally, `roblox` and `roblox-player` point to Bloxstrap, and `roblox-studio` points to `%LOCALAPPDATA%\Roblox\Versions\<guid>\RobloxStudioBeta.exe %1` — [LOCAL-OBS]
- Bloxstrap's comment "check registry entries for every launch, just in case the stock bootstrapper changes it back" shows the official client or installer rewrites these keys — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- The official installer writes `HKCU\Software\ROBLOX Corporation\Environments\roblox-studio`, with values `(default), baseHost, version, clientExe`, plus `Capabilities\UrlAssociations`. On this Bloxstrap machine only `Environments\RobloxPlayer\Channel` exists for the player — [LOCAL-OBS]
- An older tutorial used `HKCU\Software\ROBLOX Corporation\Environments\roblox-player` value `LaunchExp` to force the player and skip the desktop app. Its title says "This doesn't work anymore". *(Outdated.)* — [3P-DOC DevForum archive tutorial](https://devforum.roblox.com/t/archive-this-doesnt-work-anymore-how-to-disable-the-roblox-desktop-app-windows-only/1896652)
- The Bloxstrap uninstall key gets `EstimatedSize` (`App.UninstallKey`) — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)

### Inferences
- The lowest-maintenance launch for an original bootstrapper is to start `RobloxPlayerBeta.exe` from the version folder, pass the untouched `roblox-player:` URI as the only argument, and set the working directory to the version folder. Starting with no URI, or with `--app`, opens the desktop app.
- Translating to `-t`/`-j` flags still works but rests on undocumented, legacy behaviour.
- The client re-registers itself (the protocol handler and the `--launch-to-tray` Run entry). A third-party launcher must therefore expect:
  - autostarted, tray-resident RobloxPlayerBeta processes that it did not launch, so it should not assume one process equals one launch;
  - re-checking its protocol registration on every launch, as Bloxstrap does.
- Do not keep long-lived process handles open to RobloxPlayerBeta (Bloxstrap's Byfron/Hyperion note). Supervise by PID polling, or reopen short-lived handles, instead.

### Gaps
- I found no official list of RobloxPlayerBeta command-line flags.
- The exact URI that Roblox's website generates in October 2026 was not captured from a primary source.
- When `--launch-to-tray` first shipped, and its exact behaviour (window hidden or not, desktop app in tray) are unconfirmed. The best dating is "by March 2026 (tray-on-close) / widely reported June 2026 (autostart)".
- `RobloxPlayerInstaller.exe` command-line flags (for example `-isInstallerLaunch`) were not found in any source I read.

---

## 3. Observability: logs, filename pattern, file locking, FLog markers, crash handler, processes, window

### Takeaway
- Each RobloxPlayerBeta instance creates its own log, `%LOCALAPPDATA%\Roblox\logs\<version>_<UTC yyyyMMddTHHmmssZ>_Player_<5 hex>_last.log`.
- The log is created about 4 s after process start and kept open for writing during the session, so readers must open it with `FileShare.ReadWrite`.
- The hex token is **not** the PID.
- A sibling `…_Player_<hex>_CrashHandler_last.log` is created about 1 s later by `RobloxCrashHandler.exe`. That process is a Crashpad-style child of the client.
- Join, teleport and leave can be tracked from a small set of `[FLog::…]` prefixes, all present in the 0.742 logs.
- The main window class is `WINDOWSCLIENT` and its title is `Roblox`.

### Cited Findings

**Log location and naming**
- Bloxstrap's ActivityWatcher looks in `%LOCALAPPDATA%\Roblox\logs`. It picks the newest file whose name contains "Player" and that was created within the last 15 s — [3P-CODE ActivityWatcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Integrations/ActivityWatcher.cs)
- At launch, Bloxstrap also uses a `FileSystemWatcher` on the logs folder with filter `*.log`. It takes the **first created file** as that launch's log and gives up after 15 s ("Unable to identify log file") — [3P-CODE Bootstrapper.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Bootstrapper.cs)
- Local files from two concurrent instances and one later instance:

  | File | Kind |
  |---|---|
  | `0.742.0.7421053_20261013T103546Z_Player_7D1C8_last.log` | Player |
  | `0.742.0.7421053_20261013T103547Z_Player_258EE_CrashHandler_last.log` | CrashHandler |
  | `0.742.0.7421053_20261013T103621Z_Player_12234_last.log` | Player |
  | `0.742.0.7421053_20261013T103622Z_Player_8CB53_CrashHandler_last.log` | CrashHandler |
  | `0.742.0.7421053_20261013T144733Z_Player_DA8AF_last.log` | Player |
  | `0.742.0.7421053_20261013T144734Z_Player_97B1B_CrashHandler_last.log` | CrashHandler |

  - The logs folder also holds `cacert.pem` and a `crashes\` subfolder.
  - Each CrashHandler log has its **own, different** hex token, timestamped 1 s after the Player log.

  — [LOCAL-OBS]
- **The hex suffix is not the PID.**
  - Live RobloxPlayerBeta PID 10280 (0x2828) belongs to `…T144733Z_Player_DA8AF_last.log`.
  - Its RobloxCrashHandler PID 14384 (0x3830) belongs to `…_97B1B_CrashHandler…`.
  - Process start was 14:47:29Z and the log timestamp is 14:47:33Z, so the name records log creation in UTC, not process start.

  — [LOCAL-OBS]
- The same naming pattern existed in 2022: `0.531.0.5310423_20220620T010041Z_Player_32F43_last.log` — [3P-DOC CTF writeup](https://ctftime.org/writeup/34517)
- With the VNG distribution, logs live under `%LOCALAPPDATA%\RobloxPCVNG\logs` — [3P-CODE Fishstrap PR #1499](https://github.com/fishstrap/fishstrap/pull/1499)

**Is the file held open?**
- Opening the live log with `FileShare.Read` (deny writers) fails with a sharing violation. Opening with `FileShare.ReadWrite` succeeds. The file was still growing, last modified about a minute before the check — [LOCAL-OBS]
- Bloxstrap opens it as `FileMode.Open, FileAccess.Read, FileShare.ReadWrite` and tails it with `ReadLineAsync`, sleeping 1 s when it reaches EOF — [3P-CODE ActivityWatcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Integrations/ActivityWatcher.cs)

**Line format (0.742)**
- `<ISO-8601 UTC ms>Z,<seconds since start>,<thread id hex>,<level>[,Warning] [FLog::<Channel>] <message>`
- Example: `2026-10-13T14:47:44.195Z,11.195086,2c44,6 [FLog::Output] ! Joining game '<jobid>' place <id> at <ip>`
- The first line is the `[FLog::Output]` Terms of Use notice. Startup stages are logged as `[FLog::RobloxStarter] Starting module: …` / `… completed` / `RobloxStarter destroyed`.

— [LOCAL-OBS]

**FLog markers Bloxstrap uses**
These come from `ActivityWatcher` on `main`. Bloxstrap matches the message after the first space with `StartsWith`.

| Event | Bloxstrap prefix | Regex / what it extracts |
|---|---|---|
| Join start | `[FLog::Output] ! Joining game` | `! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)` gives JobId, PlaceId, machine address |
| Universe / user | `[FLog::GameJoinLoadTime] Report game_join_loadtime:` | `universeid:([0-9]+).*userid:([0-9]+)`. `referral_page:([^,]+)` containing `RequestPrivateGame` or `GameDetailPageJSHybridEvent` means a private server |
| UDMUX | `[FLog::Network] UDMUX Address = ` | `UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+` |
| Joined (confirmed) | `[FLog::Network] Replicator created: ` | — |
| Disconnected | `[FLog::Network] Time to disconnect replication data:` | — |
| Teleport | `[FLog::UgcExperienceController] UgcExperienceController: doTeleport: joinScriptUrl` | `JoinTypeId"%3a(\d+)%2c`; NewGamePrivateGame or SpecificPrivateGame means a reserved server |
| Back to desktop app | `[FLog::SingleSurfaceApp] leaveUGCGameInternal` | — |
| Game-to-launcher RPC | `[FLog::CreatorOutput] [BloxstrapRPC] <json>` | Throttled to 1 message per second; `SetLaunchData` limited to 200 chars |

- Bloxstrap warns that these entries "are technically volatile! they only get printed depending on their configured FLog level" — [3P-CODE ActivityWatcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Integrations/ActivityWatcher.cs)
- Fishstrap differs in three markers:
  - It confirms a join on `[FLog::Network] serverId:` (pattern `serverId: ([0-9\.]+)\|[0-9]+`).
  - It reads RPC from `[FLog::Output] [BloxstrapRPC]`.
  - It adds `[FLog::Output] Server Prefix: ` with `Server Prefix:.+_(\d{8}T\d{6}Z)_RCC_[0-9a-z]+` to get server uptime.

  Its most recent watcher changes were in May 2026 — [3P-CODE Fishstrap ActivityWatcher.cs](https://github.com/fishstrap/fishstrap/blob/main/Bloxstrap/Integrations/ActivityWatcher.cs)
- Marker counts in the live 0.742 log of one session (5 joins, 4 disconnects):

  | Marker | Count |
  |---|---|
  | `! Joining game` | 5 |
  | `game_join_loadtime` | 5 |
  | `UDMUX Address` | 5 |
  | `Replicator created:` | 5 |
  | `[FLog::Network] serverId:` | 5 |
  | `Time to disconnect replication data:` | 4 |
  | `leaveUGCGameInternal` | 1 |
  | `doTeleport` | 9 |
  | `Server Prefix:` | 5 |
  | `[FLog::CreatorOutput]` | 23 |

  All Bloxstrap and Fishstrap markers are still emitted in 0.742 — [LOCAL-OBS]
- Other useful lines seen in 0.742:
  - Graceful-leave reason: `[DFLog::NetworkClient] Client:Disconnect`, `[DFLog::RbxTransportDummyClient] Disconnected from server for reason: Player: <n> (DisconnectClientInitiated)`.
  - Teleport progress: `[FLog::SessionTransitionFSM] Teleported.`, `UgcExperienceController: finishTeleportWithJoinScriptPayload: begin`.
  - Lua app start: `[FLog::DataModelStarterModule] Launching Lua App`.

  — [LOCAL-OBS]

**Processes, crash handler, window**
- Bloxstrap uses process name `RobloxPlayerBeta` and exe `RobloxPlayerBeta.exe` (`RobloxPlayerData`). It closes Roblox with `CloseMainWindow()` (graceful) or `Kill()` (force) — [3P-CODE RobloxPlayerData.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/AppData/RobloxPlayerData.cs); [3P-CODE Watcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Watcher.cs)
- `RobloxCrashHandler.exe` (PID 14384) is a **child of RobloxPlayerBeta** (parent PID 10280) and started about 5 s after it. It runs with Crashpad-style flags:
  - `--database`, `--metrics-dir`
  - `--url=https://upload.crashes.rbxinfra.com/post?format=minidump`
  - `--crashCounter Win-ROBLOXPlayer-Crash`, `--baseUrl http://www.roblox.com/`, `--buildVariant Win64`
  - `--attachment`, `--initial-client-data`
  - `--no-rate-limit`
  - repeated `--annotation=` keys: `AppVersion, BaseUrl, BuildVariant, CPUMake, EnableSessionEndCallback, EnableSessionStartCallback, EngineVersion, Format, GPUMake, OSPlatform, OSVersion, PID, PlatformId, RobloxChannel, RobloxGitHash, RobloxProduct, TotalMemoryGB, UniqueId, UploadAttachmentKiloByteLimit, UseCrashpad`

  — [LOCAL-OBS]
- Main window of the live client: class `WINDOWSCLIENT`, title `Roblox` — [LOCAL-OBS]
- Bloxstrap's optional "disable app" patch closes the client when `leaveUGCGameInternal` is seen, via the `OnAppClose` → `CloseMainWindow` path — [3P-CODE Watcher.cs](https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/Watcher.cs)

### Inferences
- **Bind logs per instance, not by "newest file".** For per-instance supervision, record the PID and launch time, then take the first new `*_Player_*_last.log` that is **not** `*_CrashHandler_*`, created within about 15 s.
  - Bloxstrap's "first `*.log` created" heuristic can race when several instances launch together, because each launch makes two logs one second apart.
  - The hex token cannot be mapped to a PID.
  - Optional confirmation: correlate a CrashHandler process whose parent is your PID and whose start time is close to the log's.
  - A more robust option (not verified) is to open-handle enumeration to see which PID holds which log open. That requires elevated or handle-enumeration APIs, which may be undesirable near Hyperion.
- Treat `Replicator created:` or `serverId:` as "joined", `Time to disconnect replication data:` as "left", `doTeleport` as a teleport marker, and process exit as session end. Keep the matching list easy to update; both Bloxstrap and Fishstrap changed markers during 2026.
- Window class `WINDOWSCLIENT` plus a PID check identifies the game window. The tray or autostart mode may have no visible window, which is untested.

### Gaps
- No source explains how the 5-hex token is generated or what `_last` means. A random per-session ID is likely, but this is [UNVERIFIED].
- No source covers log retention or rotation rules, or what goes in the `crashes\` folder (likely Crashpad dumps; not inspected).
- I did not check whether the window class differs while the client is in the desktop-app (Lua App) state or in `--launch-to-tray` mode.
- I found no official documentation of FLog channels; all of the above is reverse-engineered.

---

## 4. Microsoft Store / UWP Roblox client status (2025–2026)

### Takeaway
On 2025-09-12 Roblox shipped a new "Roblox - Windows" app on the Microsoft Store and Xbox on PC. The legacy UWP app was renamed "Roblox - Not Supported", delisted, and will be shut down at a future date. The new Store app does not handle `roblox://` or `roblox-player://`; it registers `roblox-uwp-migration`. The Store build is therefore a separate track from the `setup.rbxcdn.com` / `WindowsPlayer` deployment that bootstrappers manage.

### Cited Findings
- The Xbox Wire announcement (2025-09-12), "Roblox Launches Updated App for the Microsoft Store and Xbox on PC", says the new version replaces the legacy app and adds Xbox friend visibility and Microsoft sign-in — [OFFICIAL Xbox Wire](https://news.xbox.com/en-us/2025/09/12/roblox-updated-app-xbox-pc/)
- Roblox Help says the new app is named "Roblox - Windows" with a blue icon, while the old one is "Roblox" with a light-gray icon. It recommends uninstalling the old app after signing in to the new one. I saw this in a search-result summary and did not open the page — [OFFICIAL Roblox Help (via search summary) [UNVERIFIED]](https://en.help.roblox.com/hc/en-us/articles/40842585714196-Get-the-New-and-Improved-Roblox-App-in-the-Microsoft-Store)
- Bloxy News reports the old app was renamed "Roblox - Not Supported", delisted, gets no updates, and will be shut down later — [3P-DOC Bloxy News (Threads)](https://www.threads.com/@bloxy.news/post/DOiOvEZDnkX/the-existing-app-now-renamed-to-roblox-not-supported-has-been-delisted-from-the-?hl=en)
- DevForum bug report (2025-10-29): the "new(er) UWP app" does not support `roblox://` or `roblox-player://` and instead registers `ROBLOX-UWP-MIGRATION`. Example: `roblox://placeId=7871169780` worked before, but `roblox-uwp-migration://placeId=7871169780` does not. No staff reply was visible — [3P-DOC DevForum](https://devforum.roblox.com/t/roblox-and-roblox-player-link-types-on-uwp-app-being-removedassigned-to-roblox-uwp-migration/4032634)

### Inferences
- A third-party bootstrapper should target only the Win32 `WindowsPlayer` deployment. It should detect, and maybe warn about, a Store install, because that install does not compete for `roblox-player:` in the same way.

### Gaps
- No confirmed shutdown date for the legacy Store app.
- No source says whether the new Store app's binaries match `WindowsPlayer` builds, or where it writes logs (probably under the package's `LocalState`; unverified).
