// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using LibreLine.Protocol;
using LibreLine.Rules;
using LibreLine.Rules.Maps;
using LibreLine.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreLine.Games;

/// <summary>Runs <see cref="GameService.Tick"/> every second under the server lock: robot wake-ups, time-outs, pruning.</summary>
internal sealed class GameTicker(GameService games, ServerLock serverLock, TimeProvider time, ILogger<GameTicker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await serverLock.RunAsync(games.Tick);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Game tick failed");
            }
        }
    }
}

public static class GamesServiceCollectionExtensions
{
    public static IServiceCollection AddGames(this IServiceCollection services, Action<GamesOptions>? configure = null)
    {
        var options = services.AddOptions<GamesOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMapCatalog>(_ => MapCatalog.LoadEmbedded());
        services.TryAddSingleton<IRulesEngine, RulesEngine>();
        services.TryAddSingleton<IGameStore, InMemoryGameStore>();
        services.AddSingleton<GameMessages>();
        services.AddSingleton<GameService>();
        services.AddSingleton<IGameService>(p => p.GetRequiredService<GameService>());
        services.AddSingleton<IHostedGameCounter>(p => p.GetRequiredService<GameService>());
        services.AddSingleton<ISessionListener>(p => p.GetRequiredService<GameService>());
        services.AddSingleton<GameLookup>();
        services.AddHostedService<GameTicker>();

        services.AddMessageHandler<WhatsNewPussycatRequest, WhatsNewPussycatHandler>();
        services.AddMessageHandler<SwitchedToGameRequest, SwitchedToGameHandler>();
        services.AddMessageHandler<SubscribePresenceServiceRequest, SubscribePresenceHandler>();
        services.AddMessageHandler<UnsubscribePresenceServiceRequest, UnsubscribePresenceHandler>();
        services.AddMessageHandler<UpdateUserDataRequest, UpdateUserDataHandler>();
        services.AddMessageHandler<CommitActionRequest, CommitActionHandler>();
        services.AddMessageHandler<MulticastDataRequest, MulticastDataHandler>();
        services.AddMessageHandler<GameOverRequest, GameOverHandler>();
        services.AddMessageHandler<GameOutcomeConfirmationRequest, GameOutcomeConfirmationHandler>();
        services.AddMessageHandler<GameAbortedConfirmationRequest, GameAbortedConfirmationHandler>();
        services.AddMessageHandler<GetClocksRequest, GetClocksHandler>();
        services.AddMessageHandler<PauseClockRequest, PauseClockHandler>();
        services.AddMessageHandler<ResumeClockRequest, ResumeClockHandler>();
        services.AddMessageHandler<GameForfeitRequest, GameForfeitHandler>();
        services.AddMessageHandler<GameAbortRequest, GameAbortHandler>();
        services.AddMessageHandler<LeaveSyncGameRequest, LeaveSyncGameHandler>();
        services.AddMessageHandler<ResumeSyncGameRequest, ResumeSyncGameHandler>();
        services.AddMessageHandler<EngageGameRequest, EngageGameHandler>();
        services.AddMessageHandler<DisengageGameRequest, DisengageGameHandler>();
        return services;
    }
}
