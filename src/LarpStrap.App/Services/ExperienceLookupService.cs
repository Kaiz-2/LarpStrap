using System.Net.Http;
using System.Text.Json;

namespace LarpStrap.App.Services;

/// <summary>
/// Reads public game titles and icons from Roblox's public game APIs (apis/games/thumbnails.roblox.com). Only the place
/// ID is sent: no account data, cookies or tokens.
/// </summary>
public sealed class ExperienceLookupService(HttpClient? client = null)
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly HttpClient _client = client ?? SharedClient;

    public async Task<ExperienceInfo?> LookupAsync(long placeId, CancellationToken cancellationToken)
    {
        string? name = null;
        string? image = null;
        try
        {
            // The unauthenticated place-details endpoint now returns 401. Resolve the public
            // universe ID, then use the public game details endpoint for the canonical title.
            var uri = $"https://apis.roblox.com/universes/v1/places/{placeId}/universe";
            using var response = await _client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (json.RootElement.TryGetProperty("universeId", out var universe) && universe.TryGetInt64(out var universeId) && universeId > 0)
                {
                    using var detailsResponse = await _client.GetAsync($"https://games.roblox.com/v1/games?universeIds={universeId}", cancellationToken);
                    if (detailsResponse.IsSuccessStatusCode)
                    {
                        using var details = await JsonDocument.ParseAsync(await detailsResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                        if (details.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
                            && data[0].TryGetProperty("name", out var title) && title.ValueKind == JsonValueKind.String)
                            name = title.GetString();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException) { }

        try
        {
            var uri = $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&size=512x512&format=Png&isCircular=false";
            using var response = await _client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (json.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0
                    && data[0].TryGetProperty("imageUrl", out var url) && url.ValueKind == JsonValueKind.String)
                    image = url.GetString();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException) { }

        if (image is not null && (!Uri.TryCreate(image, UriKind.Absolute, out var imageUri)
                                  || imageUri.Scheme != Uri.UriSchemeHttps
                                  || !imageUri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase)))
            image = null;

        return name is null && image is null ? null : new ExperienceInfo(name, image);
    }
}

public sealed record ExperienceInfo(string? Name, string? ImageUrl);
