// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asmodee.Api;

/// <summary>An Asmodee account, as <c>user/me</c> or <c>user/{id}</c> returns it. Personal fields are not kept.</summary>
public sealed record AsmodeeUser(int UserId, string LoginName, string? Language, string? TimeZone,
    IReadOnlyList<string> Features, IReadOnlyList<AsmodeePartner> Partners);

/// <summary>A linked account (<c>partners</c>); partner 12 is Steam.</summary>
public sealed record AsmodeePartner(int PartnerId, string PartnerUserId);

public sealed record AsmodeeBuddy(int Id, string LoginName);

/// <summary>The access token was refused (HTTP 401): invalid or expired.</summary>
public sealed class AsmodeeUnauthorizedException(string message) : Exception(message);

/// <summary>Asmodee's API failed or answered something unexpected.</summary>
public sealed class AsmodeeException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Calls to Asmodee's public API, each made with the player's own access token.</summary>
public interface IAsmodeeClient
{
    /// <summary><c>GET main/v1/user/me</c>: checks the token and says whose it is.</summary>
    Task<AsmodeeUser> GetMeAsync(string accessToken, IEnumerable<string> extras, CancellationToken cancellationToken = default);

    /// <summary><c>GET main/v1/user/{id}</c>: any account's public profile.</summary>
    Task<AsmodeeUser> GetUserAsync(string accessToken, int userId, IEnumerable<string> extras,
        CancellationToken cancellationToken = default);

    /// <summary><c>GET main/v1/user/me/buddies</c>, every page.</summary>
    Task<IReadOnlyList<AsmodeeBuddy>> GetBuddiesAsync(string accessToken, CancellationToken cancellationToken = default);
}

public sealed class AsmodeeOptions
{
    public string BaseUrl { get; set; } = "https://api.asmodee.net/";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>The page size used when reading buddy lists.</summary>
    public int BuddyPageSize { get; set; } = 20;
}

public static class AsmodeeServiceCollectionExtensions
{
    public static IServiceCollection AddAsmodeeApi(this IServiceCollection services, Action<AsmodeeOptions>? configure = null)
    {
        var options = services.AddOptions<AsmodeeOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddHttpClient<IAsmodeeClient, AsmodeeClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<AsmodeeOptions>>().Value;
            var baseUrl = settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = settings.Timeout;
        });
        return services;
    }
}

public sealed class AsmodeeClient(HttpClient http, IOptions<AsmodeeOptions> options) : IAsmodeeClient
{
    public async Task<AsmodeeUser> GetMeAsync(string accessToken, IEnumerable<string> extras,
        CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<UserData>(accessToken, "main/v1/user/me" + ExtrasQuery(extras), cancellationToken);
        return ToUser(data.User);
    }

    public async Task<AsmodeeUser> GetUserAsync(string accessToken, int userId, IEnumerable<string> extras,
        CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<UserData>(accessToken, $"main/v1/user/{userId}" + ExtrasQuery(extras), cancellationToken);
        return ToUser(data.User);
    }

    public async Task<IReadOnlyList<AsmodeeBuddy>> GetBuddiesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Max(1, options.Value.BuddyPageSize);
        var buddies = new List<AsmodeeBuddy>();
        for (var offset = 0; ; offset += pageSize)
        {
            var page = await GetAsync<BuddyData>(accessToken, $"main/v1/user/me/buddies?offset={offset}&limit={pageSize}",
                cancellationToken);
            var entries = page.Buddies ?? [];
            buddies.AddRange(entries.Where(b => b.Id != 0).Select(b => new AsmodeeBuddy(b.Id, b.LoginName ?? "")));
            if (entries.Count == 0 || buddies.Count >= page.Total)
            {
                return buddies;
            }
        }
    }

    private static string ExtrasQuery(IEnumerable<string> extras)
    {
        var list = string.Join(",", extras);
        return list.Length == 0 ? "" : "?extras=" + Uri.EscapeDataString(list);
    }

    private async Task<T> GetAsync<T>(string accessToken, string path, CancellationToken cancellationToken) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new AsmodeeException($"GET {path} failed: {e.Message}", e);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new AsmodeeUnauthorizedException("the access token was refused");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AsmodeeException($"GET {path} answered {(int)response.StatusCode}");
            }

            Envelope<T>? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions, cancellationToken);
            }
            catch (JsonException e)
            {
                throw new AsmodeeException($"GET {path}: unreadable response", e);
            }

            if (envelope is null || envelope.Error || envelope.Data is null)
            {
                throw new AsmodeeException($"GET {path}: error response");
            }

            return envelope.Data;
        }
    }

    private static AsmodeeUser ToUser(UserJson? user)
    {
        if (user is null || user.UserId == 0)
        {
            throw new AsmodeeException("the response holds no user");
        }

        return new AsmodeeUser(
            user.UserId,
            user.LoginName ?? "",
            user.Language,
            user.TimeZone,
            user.Features ?? [],
            (user.Partners ?? []).Select(p => new AsmodeePartner(p.PartnerId, p.PartnerUserId?.ToString() ?? "")).ToList());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private sealed record Envelope<T>(T? Data, bool Error, int Status);

    private sealed record UserData(UserJson? User);

    private sealed record UserJson(int UserId, string? LoginName, string? Language, string? TimeZone,
        List<string>? Features, List<PartnerJson>? Partners);

    private sealed record PartnerJson(int PartnerId, JsonElement? PartnerUserId);

    private sealed record BuddyData(int Total, List<BuddyJson>? Buddies);

    private sealed record BuddyJson(int Id, string? LoginName);
}
