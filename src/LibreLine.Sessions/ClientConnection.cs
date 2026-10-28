// SPDX-License-Identifier: GPL-2.0-only
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Google.Protobuf;
using LibreLine.Protocol;
using Microsoft.Extensions.Logging;

namespace LibreLine.Sessions;

public sealed class SessionOptions
{
    /// <summary>The server's TLS certificate. The client doesn't check it.</summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>How long a TLS handshake may take.</summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>A connection that sends nothing for this long is closed (the client pings every 3 seconds).</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>One client connection: a queue of outgoing packets written by its own task.</summary>
internal sealed class ClientConnection(long id, string remoteAddress, ILogger logger) : IClientConnection
{
    private readonly Channel<byte[]> outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private volatile bool open = true;

    public long Id { get; } = id;

    public string RemoteAddress { get; } = remoteAddress;

    public bool IsOpen => open;

    public void Send(IMessage message, long packetId = 0)
    {
        if (!open)
        {
            return;
        }

        logger.LogDebug("Connection {Connection} <- {Message} id {PacketId}", Id,
            MessageTypes.Describe(MessageTypes.NumberOf(message.GetType())), packetId);
        outgoing.Writer.TryWrite(PacketCodec.Encode(packetId, message));
    }

    public void SendRaw(byte[] packet)
    {
        if (open)
        {
            outgoing.Writer.TryWrite(packet);
        }
    }

    public void Close()
    {
        open = false;
        outgoing.Writer.TryComplete();
    }

    public async Task WriteLoopAsync(PacketWriter writer, CancellationToken cancellationToken)
    {
        await foreach (var packet in outgoing.Reader.ReadAllAsync(cancellationToken))
        {
            await writer.WriteAsync(packet, cancellationToken);
        }
    }
}

/// <summary>Runs a client connection from TLS handshake to close.</summary>
public sealed class ConnectionHandler(
    MessageDispatcher dispatcher,
    SessionRegistry registry,
    ServerLock serverLock,
    Microsoft.Extensions.Options.IOptions<SessionOptions> options,
    ILogger<ConnectionHandler> logger)
{
    private long nextId;

    public async Task RunAsync(Stream network, string remoteAddress, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var certificate = settings.Certificate ?? throw new InvalidOperationException("no TLS certificate configured");
        var connection = new ClientConnection(Interlocked.Increment(ref nextId), remoteAddress, logger);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using var tls = new SslStream(network, leaveInnerStreamOpen: false);
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshake.CancelAfter(settings.HandshakeTimeout);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, handshake.Token);
        }
        catch (Exception e) when (e is AuthenticationException or IOException or OperationCanceledException)
        {
            logger.LogInformation("Connection {Connection} from {Remote}: TLS handshake failed: {Error}", connection.Id,
                remoteAddress, e.Message);
            return;
        }

        logger.LogInformation("Connection {Connection} from {Remote} ({Protocol})", connection.Id, remoteAddress, tls.SslProtocol);
        var writer = connection.WriteLoopAsync(new PacketWriter(tls), stopping.Token);

        // When the connection is closed from our side (a newer login), stop reading once the queue has drained.
        _ = writer.ContinueWith(_ =>
        {
            try
            {
                stopping.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already finished.
            }
        }, TaskScheduler.Default);

        try
        {
            var reader = new PacketReader(tls);
            while (connection.IsOpen)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                idle.CancelAfter(settings.IdleTimeout);
                Envelope? envelope;
                try
                {
                    envelope = await reader.ReadAsync(idle.Token);
                }
                catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
                {
                    logger.LogInformation("Connection {Connection} idle; closing", connection.Id);
                    break;
                }

                if (envelope is not { } packet)
                {
                    break;
                }

                if (packet.Type == MessageTypes.Ping)
                {
                    // Echoing the ping unchanged is enough.
                    connection.SendRaw(PacketCodec.Encode(packet.PacketId, packet.Type, packet.Body));
                    continue;
                }

                logger.LogDebug("Connection {Connection} -> {Message} id {PacketId}", connection.Id,
                    MessageTypes.Describe(packet.Type), packet.PacketId);
                await dispatcher.DispatchAsync(connection, packet);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidProtocolBufferException
            or ObjectDisposedException or OperationCanceledException)
        {
            logger.LogInformation("Connection {Connection} closed: {Error}", connection.Id, e.Message);
        }
        finally
        {
            connection.Close();
            await serverLock.RunAsync(() =>
            {
                registry.Remove(connection);
                return Task.CompletedTask;
            });

            try
            {
                using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await writer.WaitAsync(drain.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException
                or AuthenticationException or TimeoutException)
            {
                // The socket is going away anyway.
            }

            await stopping.CancelAsync();
            logger.LogInformation("Connection {Connection} ended", connection.Id);
        }
    }
}
