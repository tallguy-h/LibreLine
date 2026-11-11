// SPDX-License-Identifier: GPL-2.0-only
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LibreLine.Desktop.Network;

/// <summary>A router's port-mapping service, found by <see cref="UpnpClient.DiscoverAsync"/>.</summary>
/// <param name="LocalAddress">This PC's address on the router's network: where mapped ports point.</param>
internal sealed record UpnpGateway(Uri ControlUrl, string ServiceType, IPAddress LocalAddress);

/// <summary>An error the router returned, with its UPnP error code (718: the port is mapped to another device...).</summary>
internal sealed class UpnpException(int code, string description)
    : Exception($"The router refused: {description} (UPnP error {code})")
{
    public const int ConflictInMappingEntry = 718;
    public const int OnlyPermanentLeasesSupported = 725;

    public int Code { get; } = code;

    public string Description { get; } = description;
}

/// <summary>
/// The parts of UPnP Internet Gateway Device the server needs: find the router (SSDP), then ask its WANIPConnection or
/// WANPPPConnection service for the external address and to forward a TCP port (SOAP).
/// </summary>
internal sealed class UpnpClient(HttpClient http)
{
    private static readonly IPEndPoint Multicast = new(IPAddress.Parse("239.255.255.250"), 1900);

    private static readonly string[] SearchTargets =
    [
        "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
        "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
        "urn:schemas-upnp-org:service:WANIPConnection:1",
        "urn:schemas-upnp-org:service:WANIPConnection:2",
        "urn:schemas-upnp-org:service:WANPPPConnection:1",
    ];

    /// <summary>Finds a router that answers port-mapping requests, or null if none does.</summary>
    public async Task<UpnpGateway?> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        foreach (var location in await SearchAsync(timeout, cancellationToken))
        {
            IReadOnlyList<(Uri ControlUrl, string ServiceType)> services;
            try
            {
                services = ParseDescription(await http.GetStringAsync(location, cancellationToken), location);
            }
            catch (Exception e) when (e is HttpRequestException or XmlException or TaskCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                continue;
            }

            var host = await ResolveAsync(location.Host, cancellationToken);
            var local = host is null ? null : NetworkAddresses.LocalAddressFor(host);
            if (local is null)
            {
                continue;
            }

            // A router can list several connections (IP and PPP); use the first that answers.
            foreach (var (controlUrl, serviceType) in services)
            {
                var gateway = new UpnpGateway(controlUrl, serviceType, local);
                try
                {
                    await GetExternalAddressAsync(gateway, cancellationToken);
                    return gateway;
                }
                catch (Exception e) when (e is HttpRequestException or UpnpException or XmlException
                    or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                }
            }
        }

        return null;
    }

    /// <summary>The router's own address on the internet side, or null if it doesn't have one yet.</summary>
    public async Task<IPAddress?> GetExternalAddressAsync(UpnpGateway gateway, CancellationToken cancellationToken)
    {
        var response = await InvokeAsync(gateway, "GetExternalIPAddress", [], cancellationToken);
        var text = response.Elements().FirstOrDefault(e => e.Name.LocalName == "NewExternalIPAddress")?.Value;
        return IPAddress.TryParse(text?.Trim(), out var address) ? address : null;
    }

    /// <summary>Forwards TCP <paramref name="port"/> on the router to the same port on this PC.</summary>
    /// <param name="leaseSeconds">How long the router keeps the mapping; 0 means until removed.</param>
    public Task AddPortMappingAsync(UpnpGateway gateway, int port, int leaseSeconds, string description,
        CancellationToken cancellationToken) =>
        InvokeAsync(gateway, "AddPortMapping",
        [
            ("NewRemoteHost", ""),
            ("NewExternalPort", port.ToString(CultureInfo.InvariantCulture)),
            ("NewProtocol", "TCP"),
            ("NewInternalPort", port.ToString(CultureInfo.InvariantCulture)),
            ("NewInternalClient", gateway.LocalAddress.ToString()),
            ("NewEnabled", "1"),
            ("NewPortMappingDescription", description),
            ("NewLeaseDuration", leaseSeconds.ToString(CultureInfo.InvariantCulture)),
        ], cancellationToken);

    public Task DeletePortMappingAsync(UpnpGateway gateway, int port, CancellationToken cancellationToken) =>
        InvokeAsync(gateway, "DeletePortMapping",
        [
            ("NewRemoteHost", ""),
            ("NewExternalPort", port.ToString(CultureInfo.InvariantCulture)),
            ("NewProtocol", "TCP"),
        ], cancellationToken);

    private async Task<XElement> InvokeAsync(UpnpGateway gateway, string action,
        IReadOnlyList<(string Name, string Value)> arguments, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, gateway.ControlUrl)
        {
            Content = new StringContent(SoapEnvelope(gateway.ServiceType, action, arguments), Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{gateway.ServiceType}#{action}\"");
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseSoapResponse(body, action, response.StatusCode);
    }

    internal static string SoapEnvelope(string serviceType, string action,
        IReadOnlyList<(string Name, string Value)> arguments)
    {
        var text = new StringBuilder();
        text.Append("<?xml version=\"1.0\"?>")
            .Append("<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ")
            .Append("s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>")
            .Append(CultureInfo.InvariantCulture, $"<u:{action} xmlns:u=\"{SecurityElement.Escape(serviceType)}\">");
        foreach (var (name, value) in arguments)
        {
            text.Append(CultureInfo.InvariantCulture, $"<{name}>{SecurityElement.Escape(value)}</{name}>");
        }

        return text.Append(CultureInfo.InvariantCulture, $"</u:{action}></s:Body></s:Envelope>").ToString();
    }

    /// <summary>The <c>{action}Response</c> element, or a <see cref="UpnpException"/> for a SOAP fault.</summary>
    internal static XElement ParseSoapResponse(string body, string action, HttpStatusCode status)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException) when (status != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"The router answered {(int)status} {status}.");
        }

        var error = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "UPnPError");
        if (error is not null)
        {
            var code = error.Elements().FirstOrDefault(e => e.Name.LocalName == "errorCode")?.Value;
            var description = error.Elements().FirstOrDefault(e => e.Name.LocalName == "errorDescription")?.Value;
            throw new UpnpException(int.TryParse(code, CultureInfo.InvariantCulture, out var number) ? number : 0,
                description ?? "unknown error");
        }

        if (status != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"The router answered {(int)status} {status}.");
        }

        return document.Descendants().FirstOrDefault(e => e.Name.LocalName == action + "Response")
            ?? throw new XmlException($"The router's answer has no {action}Response.");
    }

    /// <summary>The port-mapping services in a device description, IP connections before PPP ones.</summary>
    internal static IReadOnlyList<(Uri ControlUrl, string ServiceType)> ParseDescription(string xml, Uri location)
    {
        var document = XDocument.Parse(xml);
        var urlBase = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "URLBase")?.Value.Trim();
        var baseUri = !string.IsNullOrEmpty(urlBase) && Uri.TryCreate(urlBase, UriKind.Absolute, out var parsed)
            ? parsed
            : location;

        var found = new List<(Uri, string, int)>();
        foreach (var service in document.Descendants().Where(e => e.Name.LocalName == "service"))
        {
            var type = service.Elements().FirstOrDefault(e => e.Name.LocalName == "serviceType")?.Value.Trim();
            var control = service.Elements().FirstOrDefault(e => e.Name.LocalName == "controlURL")?.Value.Trim();
            if (type is null || string.IsNullOrEmpty(control))
            {
                continue;
            }

            var rank = type.Contains(":WANIPConnection:", StringComparison.Ordinal) ? 0
                : type.Contains(":WANPPPConnection:", StringComparison.Ordinal) ? 1
                : -1;
            if (rank >= 0 && Uri.TryCreate(baseUri, control, out var controlUrl))
            {
                found.Add((controlUrl, type, rank));
            }
        }

        return found.OrderBy(f => f.Item3).Select(f => (f.Item1, f.Item2)).ToList();
    }

    /// <summary>The <c>LOCATION</c> header of an SSDP answer.</summary>
    internal static Uri? ParseLocation(string response)
    {
        foreach (var line in response.Split('\n'))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line[..colon].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(line[(colon + 1)..].Trim(), UriKind.Absolute, out var location)
                && location.Scheme == Uri.UriSchemeHttp)
            {
                return location;
            }
        }

        return null;
    }

    /// <summary>Sends M-SEARCH from every local IPv4 address and collects the device descriptions that answer.</summary>
    private static async Task<IReadOnlyList<Uri>> SearchAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var locations = new ConcurrentDictionary<Uri, byte>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(timeout);

        var sockets = new List<UdpClient>();
        try
        {
            foreach (var address in NetworkAddresses.LocalAddresses())
            {
                try
                {
                    var socket = new UdpClient(new IPEndPoint(address, 0));
                    sockets.Add(socket);
                    foreach (var target in SearchTargets)
                    {
                        var message = Encoding.ASCII.GetBytes(
                            "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\n" +
                            $"MX: 2\r\nST: {target}\r\n\r\n");
                        await socket.SendAsync(message, Multicast, stop.Token);
                    }
                }
                catch (SocketException)
                {
                    // That adapter can't multicast; try the others.
                }
            }

            await Task.WhenAll(sockets.Select(socket => ReceiveAsync(socket, locations, stop)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var socket in sockets)
            {
                socket.Dispose();
            }
        }

        return locations.Keys.ToList();
    }

    private static async Task ReceiveAsync(UdpClient socket, ConcurrentDictionary<Uri, byte> locations,
        CancellationTokenSource stop)
    {
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(stop.Token);
                if (ParseLocation(Encoding.ASCII.GetString(result.Buffer)) is { } location
                    && locations.TryAdd(location, 0) && locations.Count == 1)
                {
                    // Give other devices a moment to answer too, then stop.
                    stop.CancelAfter(TimeSpan.FromMilliseconds(600));
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
    }

    private static async Task<IPAddress?> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellationToken);
            return addresses.FirstOrDefault();
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
