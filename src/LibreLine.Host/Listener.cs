// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Net.Sockets;
using LibreLine.Players;
using LibreLine.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreLine.Host;

/// <summary>Accepts TCP connections on the configured port and hands each to the sessions module.</summary>
public sealed class Listener(
    ConnectionHandler connections,
    IPlayerService players,
    IOptions<SessionOptions> sessionOptions,
    IOptions<ServerOptions> options,
    ILogger<Listener> logger) : BackgroundService
{
    private readonly TaskCompletionSource<int> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The port actually listened on, once listening.</summary>
    public Task<int> Port => started.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.IPv6Any, options.Value.Port);
        listener.Server.DualMode = true;
        try
        {
            // Load (or create) the certificate now, so a bad one stops the server at start-up.
            _ = sessionOptions.Value.Certificate;
            await players.LoadAsync(stoppingToken);
            listener.Start();
        }
        catch (Exception e)
        {
            started.TrySetException(e);
            throw;
        }

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        logger.LogInformation("Libre Line is listening on port {Port}", port);
        started.TrySetResult(port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                client.NoDelay = true;
                var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        try
                        {
                            await connections.RunAsync(client.GetStream(), remote, stoppingToken);
                        }
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            logger.LogError(e, "Connection from {Remote} failed", remote);
                        }
                    }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            listener.Stop();
        }
    }
}
