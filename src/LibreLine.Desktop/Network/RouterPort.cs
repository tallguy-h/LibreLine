// SPDX-License-Identifier: GPL-2.0-only
using System.Net;

namespace LibreLine.Desktop.Network;

/// <summary>What happened when the app asked the router to open the port.</summary>
/// <param name="ExternalAddress">The router's internet address, if it said.</param>
/// <param name="Problem">Why friends may not get in, in words for the player; null if all is well.</param>
/// <param name="Conflict">The router already forwards the port to another device.</param>
internal sealed record RouterPortResult(bool Opened, IPAddress? ExternalAddress, IPAddress? LocalAddress, string? Problem,
    bool Conflict = false);

/// <summary>
/// Keeps the server's port forwarded on the router while the server runs. The mapping has a lease of an hour,
/// renewed every half hour, so it disappears by itself if the app stops without removing it.
/// </summary>
internal sealed class RouterPort(UpnpClient upnp, Action<string> log) : IAsyncDisposable
{
    public const string Description = "Libre Line";
    private const int LeaseSeconds = 3600;

    private UpnpGateway? gateway;
    private int port;
    private CancellationTokenSource? renewal;

    public async Task<RouterPortResult> OpenAsync(int serverPort, CancellationToken cancellationToken)
    {
        await CloseAsync();
        log($"Looking for your router to open port {serverPort}...");
        var found = await upnp.DiscoverAsync(TimeSpan.FromSeconds(3), cancellationToken);
        if (found is null)
        {
            log("No router answered UPnP requests.");
            return new RouterPortResult(false, null, null,
                "Your router didn't answer. UPnP may be turned off in its settings.");
        }

        IPAddress? external = null;
        try
        {
            external = await upnp.GetExternalAddressAsync(found, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or UpnpException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
        }

        int lease;
        try
        {
            lease = await AddAsync(found, serverPort, cancellationToken);
        }
        catch (UpnpException e) when (e.Code == UpnpException.ConflictInMappingEntry)
        {
            log($"The router already forwards port {serverPort} to another device.");
            return new RouterPortResult(false, external, found.LocalAddress,
                $"Your router already sends port {serverPort} to another device (perhaps another server). " +
                "Use a different port, or change that rule in your router's settings.", Conflict: true);
        }
        catch (Exception e) when (e is HttpRequestException or UpnpException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            log($"The router didn't open the port: {e.Message}");
            return new RouterPortResult(false, external, found.LocalAddress,
                $"Your router refused to open the port ({e.Message}).");
        }

        gateway = found;
        port = serverPort;
        log($"Your router now forwards TCP port {serverPort} to {found.LocalAddress}.");
        if (lease > 0)
        {
            renewal = new CancellationTokenSource();
            _ = RenewAsync(found, serverPort, renewal.Token);
        }

        var problem = external is not null && NetworkAddresses.IsPrivate(external)
            ? $"Your router's own internet address is {external}, a private address. It is probably behind another " +
              "router or your provider's shared address (CGNAT), so friends over the internet may still not get in."
            : null;
        return new RouterPortResult(true, external, found.LocalAddress, problem);
    }

    /// <summary>Removes the mapping, if this app made one.</summary>
    public async Task CloseAsync()
    {
        var made = gateway;
        gateway = null;
        if (renewal is not null)
        {
            await renewal.CancelAsync();
            renewal.Dispose();
            renewal = null;
        }

        if (made is null)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await upnp.DeletePortMappingAsync(made, port, timeout.Token);
            log($"Your router no longer forwards port {port}.");
        }
        catch (Exception e) when (e is HttpRequestException or UpnpException or OperationCanceledException)
        {
            log($"Couldn't remove the router's port forwarding ({e.Message}); it expires within an hour.");
        }
    }

    public ValueTask DisposeAsync() => new(CloseAsync());

    /// <summary>Adds the mapping with a lease, or a permanent one if the router only accepts those.</summary>
    private async Task<int> AddAsync(UpnpGateway found, int serverPort, CancellationToken cancellationToken)
    {
        try
        {
            await upnp.AddPortMappingAsync(found, serverPort, LeaseSeconds, Description, cancellationToken);
            return LeaseSeconds;
        }
        catch (UpnpException e) when (e.Code == UpnpException.OnlyPermanentLeasesSupported)
        {
            await upnp.AddPortMappingAsync(found, serverPort, 0, Description, cancellationToken);
            return 0;
        }
    }

    private async Task RenewAsync(UpnpGateway found, int serverPort, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(LeaseSeconds / 2));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await upnp.AddPortMappingAsync(found, serverPort, LeaseSeconds, Description, cancellationToken);
                }
                catch (Exception e) when (e is HttpRequestException or UpnpException
                    || e is TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    log($"Couldn't renew the router's port forwarding: {e.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
