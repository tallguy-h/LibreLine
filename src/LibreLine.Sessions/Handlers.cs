// SPDX-License-Identifier: GPL-2.0-only
using System.Security.Cryptography;
using Asmodee.Api;
using Com.Daysofwonder.Async;
using Com.Daysofwonder.Game.Push;
using LibreLine.Players;
using LibreLine.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConnectionError = Com.Daysofwonder.Async.AsyncConnectionErrorRequest.Types.ConnectionError;

namespace LibreLine.Sessions;

/// <summary>
/// <c>AsyncAuth</c> (400): checks the Asmodee token, records the login and answers <c>AsyncConnected</c> (406) or
/// <c>AsyncConnectionError</c> (407). Runs outside the server lock while it calls Asmodee.
/// </summary>
internal sealed class AsyncAuthHandler(
    IAsmodeeClient asmodee,
    IPlayerService players,
    SessionRegistry registry,
    PlayerMessages messages,
    ServerLock serverLock,
    ILogger<AsyncAuthHandler> logger) : IMessageHandler<AsyncAuthRequest>
{
    private static readonly string[] Extras = ["features"];

    public async Task HandleAsync(RequestContext context, AsyncAuthRequest message)
    {
        if (message.HasGameType && message.GameType != "TT")
        {
            Refuse(context, ConnectionError.UnknownGameType, $"game type {message.GameType}");
            return;
        }

        var token = message.PartnerToken?.SessionToken;
        if (string.IsNullOrEmpty(token))
        {
            Refuse(context, ConnectionError.NeedAuthentication, "no Asmodee token");
            return;
        }

        AsmodeeUser user;
        try
        {
            user = await asmodee.GetMeAsync(token, Extras);
        }
        catch (AsmodeeUnauthorizedException)
        {
            Refuse(context, ConnectionError.AccessDenied, "Asmodee refused the token");
            return;
        }
        catch (AsmodeeException e)
        {
            logger.LogWarning(e, "Asmodee check failed");
            Refuse(context, ConnectionError.ServerError, "Asmodee couldn't be reached");
            return;
        }

        if (message.HasName && !string.Equals(message.Name, user.LoginName, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Login name {Name} differs from the Asmodee name {LoginName}", message.Name, user.LoginName);
        }

        var buddies = players.NeedsBuddyImport(user.UserId) ? await players.FetchAsmodeeBuddiesAsync(token) : null;
        var steamId = message.Device is { Type: Devices.Types.Type.Steam } device ? device.Token.ToStringUtf8() : null;

        await serverLock.RunAsync(async () =>
        {
            if (!context.Connection.IsOpen)
            {
                return;
            }

            var player = await players.RecordLoginAsync(user, steamId, buddies);
            var session = new Session(RandomNumberGenerator.GetInt32(1, int.MaxValue), player.AccountId, player.Name,
                context.Connection);
            context.Reply(new AsyncConnectedRequest
            {
                Session = new Com.Daysofwonder.Async.Session { Id = session.Id },
                Player = messages.Build(1, player.AccountId),
            });
            logger.LogInformation("Player {Account} ({Name}) logged in on connection {Connection}", player.AccountId,
                player.Name, context.Connection.Id);
            registry.Add(session);
        });
    }

    private void Refuse(RequestContext context, ConnectionError code, string why)
    {
        logger.LogInformation("Login refused on connection {Connection}: {Why}", context.Connection.Id, why);
        context.Reply(new AsyncConnectionErrorRequest { Code = code });
    }
}

/// <summary><c>AsyncDisconnect</c> (401): the client is going away. No reply.</summary>
internal sealed class AsyncDisconnectHandler(SessionRegistry registry) : IMessageHandler<AsyncDisconnectRequest>
{
    public Task HandleAsync(RequestContext context, AsyncDisconnectRequest message)
    {
        registry.Remove(context.Connection);
        context.Connection.Close();
        return Task.CompletedTask;
    }
}

/// <summary><c>AskServerStatistics</c> (408) → <c>ServerStatistics</c> (409).</summary>
internal sealed class AskServerStatisticsHandler(ISessionRegistry registry, IPlayerService players, IServiceProvider services)
    : IMessageHandler<AskServerStatisticsRequest>
{
    public Task HandleAsync(RequestContext context, AskServerStatisticsRequest message)
    {
        context.Reply(new ServerStatisticsRequest
        {
            HostedGames = services.GetService<IHostedGameCounter>()?.HostedGames ?? 0,
            Players = players.Count,
            ConnectedPlayers = registry.All.Count,
        });
        return Task.CompletedTask;
    }
}

/// <summary><c>AsyncUnlinkDevice</c> (412) → <c>AsyncDeviceUnlinked</c> (413). The server keeps no device links.</summary>
internal sealed class AsyncUnlinkDeviceHandler : IMessageHandler<AsyncUnlinkDeviceRequest>
{
    public Task HandleAsync(RequestContext context, AsyncUnlinkDeviceRequest message)
    {
        var reply = new AsyncDeviceUnlinkedRequest();
        if (message.Device is not null)
        {
            reply.Device = message.Device;
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>RegisterPresence</c> (527) → <c>AsyncBuddyPresencePartialUpdate</c> (517).</summary>
internal sealed class RegisterPresenceHandler(SessionRegistry registry) : IMessageHandler<RegisterPresenceRequest>
{
    public Task HandleAsync(RequestContext context, RegisterPresenceRequest message)
    {
        if (registry.Find(context.Connection) is not { } session)
        {
            return Task.CompletedTask;
        }

        var (present, absent) = registry.Register(session, message.PlayerIds);
        var reply = new AsyncBuddyPresencePartialUpdateRequest();
        reply.Present.AddRange(present);
        reply.Absent.AddRange(absent);
        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>UnregisterPresence</c> (528). No reply.</summary>
internal sealed class UnregisterPresenceHandler(SessionRegistry registry) : IMessageHandler<UnregisterPresenceRequest>
{
    public Task HandleAsync(RequestContext context, UnregisterPresenceRequest message)
    {
        if (registry.Find(context.Connection) is { } session)
        {
            registry.Unregister(session, message.PlayerIds);
        }

        return Task.CompletedTask;
    }
}

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddSessions(this IServiceCollection services, Action<SessionOptions>? configure = null)
    {
        var options = services.AddOptions<SessionOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<ISessionRegistry>(p => p.GetRequiredService<SessionRegistry>());
        services.AddSingleton<IPresence>(p => p.GetRequiredService<SessionRegistry>());
        services.AddSingleton<IConnectionAuthorizer>(p => p.GetRequiredService<SessionRegistry>());
        services.AddSingleton<PlayerMessages>();
        services.AddSingleton<ConnectionHandler>();

        services.AddMessageHandler<AsyncAuthRequest, AsyncAuthHandler>(exclusive: false, requiresLogin: false);
        services.AddMessageHandler<AsyncDisconnectRequest, AsyncDisconnectHandler>(requiresLogin: false);
        services.AddMessageHandler<AskServerStatisticsRequest, AskServerStatisticsHandler>(requiresLogin: false);
        services.AddMessageHandler<AsyncUnlinkDeviceRequest, AsyncUnlinkDeviceHandler>();
        services.AddMessageHandler<RegisterPresenceRequest, RegisterPresenceHandler>();
        services.AddMessageHandler<UnregisterPresenceRequest, UnregisterPresenceHandler>();
        return services;
    }
}
