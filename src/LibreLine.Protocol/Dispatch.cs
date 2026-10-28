// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibreLine.Protocol;

/// <summary>A client connection, as the message handlers see it.</summary>
public interface IClientConnection
{
    long Id { get; }

    string RemoteAddress { get; }

    bool IsOpen { get; }

    /// <summary>Queues a message. <paramref name="packetId"/> is the request's id for a reply, 0 for a push.</summary>
    void Send(IMessage message, long packetId = 0);

    /// <summary>Closes the connection once queued messages have been sent.</summary>
    void Close();
}

/// <summary>A request being handled: where it came from, and the packet id its reply must carry.</summary>
public sealed record RequestContext(IClientConnection Connection, long PacketId)
{
    public void Reply(IMessage message) => Connection.Send(message, PacketId);

    public void ReplyError(ErrorCode code, string? text = null, long? gameId = null, int? turnIndex = null, int? playerId = null) =>
        Reply(Errors.Create(code, text, gameId, turnIndex, playerId));
}

public static class Errors
{
    /// <summary>An <c>Error</c> (505) message.</summary>
    public static ErrorRequest Create(ErrorCode code, string? text = null, long? gameId = null, int? turnIndex = null,
        int? playerId = null)
    {
        var error = new ErrorRequest { Code = code };
        if (text is not null)
        {
            error.Msg = text;
        }

        if (gameId is { } g)
        {
            error.GameId = g;
        }

        if (turnIndex is { } t)
        {
            error.TurnIndex = t;
        }

        if (playerId is { } p)
        {
            error.PlayerId = p;
        }

        return error;
    }
}

/// <summary>Handles one message type.</summary>
public interface IMessageHandler<in T> where T : IMessage
{
    Task HandleAsync(RequestContext context, T message);
}

/// <summary>Says whether a connection has logged in. Implemented by the sessions module.</summary>
public interface IConnectionAuthorizer
{
    bool IsAuthenticated(IClientConnection connection);
}

/// <summary>
/// One lock for the whole server's state. Handlers run one at a time under it, and timers take it too, so the
/// domains need no locking of their own. Sends only queue, so holding it is short.
/// </summary>
public sealed class ServerLock
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task RunAsync(Func<Task> action)
    {
        await gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        await gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>A handler as registered: its message type and how to call it.</summary>
/// <param name="Exclusive">Run under the <see cref="ServerLock"/>. Login turns it off to call Asmodee unlocked.</param>
/// <param name="RequiresLogin">Refuse it from connections that haven't logged in.</param>
public sealed record HandlerRegistration(
    int Type,
    Func<IServiceProvider, RequestContext, ByteString, Task> Invoke,
    bool Exclusive,
    bool RequiresLogin);

public static class HandlerRegistrationExtensions
{
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services,
        bool exclusive = true, bool requiresLogin = true)
        where TMessage : IMessage<TMessage>, new()
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddSingleton<THandler>();
        services.AddSingleton(new HandlerRegistration(
            MessageTypes.NumberOf<TMessage>(),
            (provider, context, body) =>
            {
                var message = new TMessage();
                message.MergeFrom(body);
                return provider.GetRequiredService<THandler>().HandleAsync(context, message);
            },
            exclusive,
            requiresLogin));
        return services;
    }

    public static IServiceCollection AddProtocol(this IServiceCollection services)
    {
        services.AddSingleton<ServerLock>();
        services.AddSingleton<MessageDispatcher>();
        return services;
    }
}

/// <summary>Routes each received message to its handler.</summary>
public sealed class MessageDispatcher
{
    private readonly Dictionary<int, HandlerRegistration> handlers = [];
    private readonly IServiceProvider services;
    private readonly ServerLock serverLock;
    private readonly IConnectionAuthorizer? authorizer;
    private readonly ILogger<MessageDispatcher> logger;

    public MessageDispatcher(IEnumerable<HandlerRegistration> registrations, IServiceProvider services, ServerLock serverLock,
        ILogger<MessageDispatcher> logger, IConnectionAuthorizer? authorizer = null)
    {
        foreach (var registration in registrations)
        {
            if (!handlers.TryAdd(registration.Type, registration))
            {
                throw new InvalidOperationException($"two handlers for {MessageTypes.Describe(registration.Type)}");
            }
        }

        this.services = services;
        this.serverLock = serverLock;
        this.authorizer = authorizer;
        this.logger = logger;
    }

    public IReadOnlyCollection<int> HandledTypes => handlers.Keys;

    public async Task DispatchAsync(IClientConnection connection, Envelope envelope)
    {
        var context = new RequestContext(connection, envelope.PacketId);
        if (!handlers.TryGetValue(envelope.Type, out var registration))
        {
            // Every request with a reply must get one, or the client's later requests queue behind it.
            logger.LogWarning("Connection {Connection}: no handler for {Message}", connection.Id, MessageTypes.Describe(envelope.Type));
            context.ReplyError(ErrorCode.BadRequest, "not supported by this server");
            return;
        }

        if (registration.RequiresLogin && authorizer is not null && !authorizer.IsAuthenticated(connection))
        {
            context.ReplyError(ErrorCode.UnknownPlayer, "not logged in");
            return;
        }

        try
        {
            if (registration.Exclusive)
            {
                await serverLock.RunAsync(() => registration.Invoke(services, context, envelope.Body));
            }
            else
            {
                await registration.Invoke(services, context, envelope.Body);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Connection {Connection}: {Message} failed", connection.Id, MessageTypes.Describe(envelope.Type));
            context.ReplyError(ErrorCode.BadRequest, "the server couldn't handle this request");
        }
    }
}
