// SPDX-License-Identifier: GPL-2.0-only
using System.Globalization;
using System.Text.Json;
using LibreLine.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibreLine.Storage.Json;

/// <summary>
/// Player records as one JSON file each, <c>players/{accountId}.json</c> under the data directory. Each write goes
/// to a temporary file that is then renamed into place, so a crash never leaves half a file.
/// </summary>
public sealed class JsonPlayerStore : IPlayerStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string directory;
    private readonly ILogger<JsonPlayerStore> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonPlayerStore(string dataDirectory, ILogger<JsonPlayerStore> logger)
    {
        directory = Path.Combine(dataDirectory, "players");
        Directory.CreateDirectory(directory);
        this.logger = logger;
    }

    public async Task<PlayerRecord?> GetAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(accountId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PlayerRecord>(stream, Options, cancellationToken);
    }

    public async Task SaveAsync(PlayerRecord player, CancellationToken cancellationToken = default)
    {
        var path = PathFor(player.AccountId);
        var temporary = path + ".tmp";
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, player, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<PlayerRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var players = new List<PlayerRecord>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                if (await JsonSerializer.DeserializeAsync<PlayerRecord>(stream, Options, cancellationToken) is { } player)
                {
                    players.Add(player);
                }
            }
            catch (JsonException e)
            {
                logger.LogError(e, "Skipping unreadable player file {Path}", path);
            }
        }

        return players;
    }

    private string PathFor(int accountId) =>
        Path.Combine(directory, accountId.ToString(CultureInfo.InvariantCulture) + ".json");
}

public static class JsonStorageServiceCollectionExtensions
{
    /// <summary>Stores players as JSON files in <paramref name="dataDirectory"/>.</summary>
    public static IServiceCollection AddJsonPlayerStore(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton<IPlayerStore>(provider =>
            new JsonPlayerStore(dataDirectory, provider.GetRequiredService<ILogger<JsonPlayerStore>>()));
        return services;
    }
}
