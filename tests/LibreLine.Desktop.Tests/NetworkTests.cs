// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Net.Sockets;
using System.Text;
using LibreLine.Desktop.Network;

namespace LibreLine.Desktop.Tests;

public sealed class UpnpParsingTests
{
    [Fact]
    public void The_location_header_is_read_from_an_ssdp_answer()
    {
        var answer = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
                     "Location: http://192.168.1.1:5000/rootDesc.xml\r\n\r\n";

        Assert.Equal(new Uri("http://192.168.1.1:5000/rootDesc.xml"), UpnpClient.ParseLocation(answer));
        Assert.Null(UpnpClient.ParseLocation("HTTP/1.1 200 OK\r\nST: x\r\n\r\n"));
    }

    [Fact]
    public void Port_mapping_services_are_found_ip_before_ppp_relative_to_the_location()
    {
        const string description = """
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0">
              <device><deviceList><device><deviceList><device>
                <serviceList>
                  <service>
                    <serviceType>urn:schemas-upnp-org:service:WANPPPConnection:1</serviceType>
                    <controlURL>/ctl/PPP</controlURL>
                  </service>
                  <service>
                    <serviceType>urn:schemas-upnp-org:service:WANIPConnection:1</serviceType>
                    <controlURL>/ctl/IPConn</controlURL>
                  </service>
                  <service>
                    <serviceType>urn:schemas-upnp-org:service:Layer3Forwarding:1</serviceType>
                    <controlURL>/ctl/L3F</controlURL>
                  </service>
                </serviceList>
              </device></deviceList></device></deviceList></device>
            </root>
            """;

        var services = UpnpClient.ParseDescription(description, new Uri("http://192.168.1.1:5000/rootDesc.xml"));

        Assert.Equal(2, services.Count);
        Assert.Equal(new Uri("http://192.168.1.1:5000/ctl/IPConn"), services[0].ControlUrl);
        Assert.Equal("urn:schemas-upnp-org:service:WANIPConnection:1", services[0].ServiceType);
        Assert.Equal(new Uri("http://192.168.1.1:5000/ctl/PPP"), services[1].ControlUrl);
    }

    [Fact]
    public void Url_base_wins_over_the_location()
    {
        const string description = """
            <root xmlns="urn:schemas-upnp-org:device-1-0"><URLBase>http://10.0.0.1:49000/</URLBase>
              <device><serviceList><service>
                <serviceType>urn:schemas-upnp-org:service:WANIPConnection:2</serviceType>
                <controlURL>upnp/control/wanip</controlURL>
              </service></serviceList></device>
            </root>
            """;

        var services = UpnpClient.ParseDescription(description, new Uri("http://10.0.0.1:5555/desc.xml"));

        Assert.Equal(new Uri("http://10.0.0.1:49000/upnp/control/wanip"), Assert.Single(services).ControlUrl);
    }

    [Fact]
    public void A_soap_fault_becomes_an_upnp_error()
    {
        const string fault = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><s:Fault>
              <faultcode>s:Client</faultcode><faultstring>UPnPError</faultstring>
              <detail><UPnPError xmlns="urn:schemas-upnp-org:control-1-0">
                <errorCode>718</errorCode><errorDescription>ConflictInMappingEntry</errorDescription>
              </UPnPError></detail>
            </s:Fault></s:Body></s:Envelope>
            """;

        var error = Assert.Throws<UpnpException>(() =>
            UpnpClient.ParseSoapResponse(fault, "AddPortMapping", HttpStatusCode.InternalServerError));

        Assert.Equal(UpnpException.ConflictInMappingEntry, error.Code);
        Assert.Equal("ConflictInMappingEntry", error.Description);
    }

    [Fact]
    public void The_envelope_escapes_its_values()
    {
        var envelope = UpnpClient.SoapEnvelope("urn:schemas-upnp-org:service:WANIPConnection:1", "AddPortMapping",
            [("NewPortMappingDescription", "a<b&c")]);

        Assert.Contains("<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">", envelope,
            StringComparison.Ordinal);
        Assert.Contains("<NewPortMappingDescription>a&lt;b&amp;c</NewPortMappingDescription>", envelope,
            StringComparison.Ordinal);
    }
}

public sealed class UpnpClientTests
{
    private const string Service = "urn:schemas-upnp-org:service:WANIPConnection:1";

    [Fact]
    public async Task Add_port_mapping_sends_the_soap_action_and_this_pcs_address()
    {
        await using var router = new FakeRouter((action, _) => (200, Response(action, "")));
        using var http = new HttpClient();
        var gateway = new UpnpGateway(router.ControlUrl, Service, IPAddress.Parse("192.168.1.20"));

        await new UpnpClient(http).AddPortMappingAsync(gateway, 2445, 3600, "Libre Line", CancellationToken.None);

        var (soapAction, body) = Assert.Single(router.Requests);
        Assert.Equal($"\"{Service}#AddPortMapping\"", soapAction);
        Assert.Contains("<NewExternalPort>2445</NewExternalPort>", body, StringComparison.Ordinal);
        Assert.Contains("<NewInternalPort>2445</NewInternalPort>", body, StringComparison.Ordinal);
        Assert.Contains("<NewInternalClient>192.168.1.20</NewInternalClient>", body, StringComparison.Ordinal);
        Assert.Contains("<NewProtocol>TCP</NewProtocol>", body, StringComparison.Ordinal);
        Assert.Contains("<NewLeaseDuration>3600</NewLeaseDuration>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_external_address_is_read()
    {
        await using var router = new FakeRouter((action, _) =>
            (200, Response(action, "<NewExternalIPAddress>203.0.113.7</NewExternalIPAddress>")));
        using var http = new HttpClient();
        var gateway = new UpnpGateway(router.ControlUrl, Service, IPAddress.Parse("192.168.1.20"));

        var address = await new UpnpClient(http).GetExternalAddressAsync(gateway, CancellationToken.None);

        Assert.Equal(IPAddress.Parse("203.0.113.7"), address);
    }

    [Fact]
    public async Task A_refusal_is_reported_with_its_code()
    {
        await using var router = new FakeRouter((_, _) => (500,
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><s:Fault><detail>" +
            "<UPnPError xmlns=\"urn:schemas-upnp-org:control-1-0\"><errorCode>725</errorCode>" +
            "<errorDescription>OnlyPermanentLeasesSupported</errorDescription></UPnPError></detail></s:Fault>" +
            "</s:Body></s:Envelope>"));
        using var http = new HttpClient();
        var gateway = new UpnpGateway(router.ControlUrl, Service, IPAddress.Parse("192.168.1.20"));

        var error = await Assert.ThrowsAsync<UpnpException>(() =>
            new UpnpClient(http).AddPortMappingAsync(gateway, 2445, 3600, "Libre Line", CancellationToken.None));

        Assert.Equal(UpnpException.OnlyPermanentLeasesSupported, error.Code);
    }

    private static string Response(string action, string content) =>
        "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body>" +
        $"<u:{action}Response xmlns:u=\"{Service}\">{content}</u:{action}Response></s:Body></s:Envelope>";

    /// <summary>A minimal HTTP server standing in for a router's control URL.</summary>
    private sealed class FakeRouter : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task serving;
        private readonly CancellationTokenSource stop = new();

        public FakeRouter(Func<string, string, (int Status, string Body)> answer)
        {
            listener.Start();
            ControlUrl = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/ctl/IPConn");
            serving = ServeAsync(answer);
        }

        public Uri ControlUrl { get; }

        public List<(string SoapAction, string Body)> Requests { get; } = [];

        private async Task ServeAsync(Func<string, string, (int Status, string Body)> answer)
        {
            try
            {
                while (true)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    await reader.ReadLineAsync(stop.Token);
                    while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 } line)
                    {
                        var colon = line.IndexOf(':', StringComparison.Ordinal);
                        headers[line[..colon]] = line[(colon + 1)..].Trim();
                    }

                    var body = new char[int.Parse(headers["Content-Length"], System.Globalization.CultureInfo.InvariantCulture)];
                    await reader.ReadBlockAsync(body, stop.Token);
                    var soapAction = headers["SOAPAction"];
                    Requests.Add((soapAction, new string(body)));

                    var action = soapAction.Trim('"')[(soapAction.Trim('"').IndexOf('#', StringComparison.Ordinal) + 1)..];
                    var (status, content) = answer(action, new string(body));
                    var bytes = Encoding.UTF8.GetBytes(content);
                    var head = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Internal Server Error")}\r\n" +
                        $"Content-Type: text/xml; charset=\"utf-8\"\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head, stop.Token);
                    await stream.WriteAsync(bytes, stop.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            listener.Stop();
            await serving;
            stop.Dispose();
        }
    }
}

public sealed class NetworkAddressTests
{
    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.0.10", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("169.254.1.1", true)]
    [InlineData("203.0.113.7", false)]
    [InlineData("8.8.8.8", false)]
    public void Private_addresses_are_recognised(string address, bool isPrivate)
    {
        Assert.Equal(isPrivate, NetworkAddresses.IsPrivate(IPAddress.Parse(address)));
    }

    [Fact]
    public void Server_names_bracket_ipv6()
    {
        Assert.Equal("192.168.1.20:2445", NetworkAddresses.ServerName(IPAddress.Parse("192.168.1.20"), 2445));
        Assert.Equal("[2001:db8::1]:2445", NetworkAddresses.ServerName(IPAddress.Parse("2001:db8::1"), 2445));
    }
}

public sealed class ServerControllerTests
{
    [Fact]
    public async Task The_server_starts_on_a_free_port_and_stops()
    {
        var directory = Path.Combine(Path.GetTempPath(), "libreline-desktop-" + Guid.NewGuid());
        try
        {
            await using var server = new ServerController(directory);
            var lines = new List<string>();
            server.Log += line =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }
            };

            var port = await server.StartAsync(0);
            Assert.True(server.IsRunning);
            Assert.InRange(port, 1, 65535);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
            }

            await server.StopAsync();
            Assert.False(server.IsRunning);
            Assert.True(File.Exists(Path.Combine(directory, "server.pfx")));
            lock (lines)
            {
                Assert.Contains(lines, l => l.Contains($"listening on port {port}", StringComparison.Ordinal));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task A_port_in_use_is_reported()
    {
        using var taken = new TcpListener(IPAddress.IPv6Any, 0);
        taken.Server.DualMode = true;
        taken.Start();
        var port = ((IPEndPoint)taken.LocalEndpoint).Port;
        var directory = Path.Combine(Path.GetTempPath(), "libreline-desktop-" + Guid.NewGuid());
        try
        {
            await using var server = new ServerController(directory);
            var error = await Assert.ThrowsAsync<SocketException>(() => server.StartAsync(port));
            Assert.Equal(SocketError.AddressAlreadyInUse, error.SocketErrorCode);
            Assert.False(server.IsRunning);
        }
        finally
        {
            taken.Stop();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
