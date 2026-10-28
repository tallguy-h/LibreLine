// SPDX-License-Identifier: GPL-2.0-only
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Channels;
using Asmodee.Api;
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreLine.Host.Tests;

/// <summary>Asmodee as the tests need it: tokens that map to accounts, and buddy lists.</summary>
internal sealed class FakeAsmodee : IAsmodeeClient
{
    public Dictionary<string, AsmodeeUser> Users { get; } = [];

    public Dictionary<int, List<AsmodeeBuddy>> Buddies { get; } = [];

    public AsmodeeUser Add(string token, int id, string name, params string[] features)
    {
        var user = new AsmodeeUser(id, name, "en", "UTC", features, []);
        Users[token] = user;
        return user;
    }

    public Task<AsmodeeUser> GetMeAsync(string accessToken, IEnumerable<string> extras, CancellationToken cancellationToken = default) =>
        Users.TryGetValue(accessToken, out var user)
            ? Task.FromResult(user)
            : Task.FromException<AsmodeeUser>(new AsmodeeUnauthorizedException("refused"));

    public Task<AsmodeeUser> GetUserAsync(string accessToken, int userId, IEnumerable<string> extras,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.First(u => u.UserId == userId));

    public Task<IReadOnlyList<AsmodeeBuddy>> GetBuddiesAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AsmodeeBuddy>>(Users.TryGetValue(accessToken, out var user)
            && Buddies.TryGetValue(user.UserId, out var list) ? list : []);
}

/// <summary>The real host on a free port, with a fake Asmodee and a temporary data directory.</summary>
internal sealed class TestServer : IAsyncDisposable
{
    private readonly IHost host;
    private readonly string directory;

    private TestServer(IHost host, string directory, int port)
    {
        this.host = host;
        this.directory = directory;
        Port = port;
    }

    public FakeAsmodee Asmodee { get; private init; } = null!;

    public int Port { get; }

    public IServiceProvider Services => host.Services;

    public static async Task<TestServer> StartAsync(string rules = "enforce")
    {
        var directory = Path.Combine(Path.GetTempPath(), "libreline-host-" + Guid.NewGuid().ToString("N"));
        var asmodee = new FakeAsmodee();
        var host = ServerHost.Build([], services =>
            {
                services.AddSingleton<IAsmodeeClient>(asmodee);
                services.AddLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            },
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LibreLine:Port"] = "0",
                ["LibreLine:DataDirectory"] = directory,
                ["LibreLine:Rules"] = rules,
            }));
        await host.StartAsync();
        var port = await host.Services.GetRequiredService<Listener>().Port.WaitAsync(TimeSpan.FromSeconds(10));
        return new TestServer(host, directory, port) { Asmodee = asmodee };
    }

    public async Task<TestClient> ConnectAsync()
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", Port);
        var tls = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await tls.AuthenticateAsClientAsync("libreline");
        return new TestClient(tcp, tls);
    }

    /// <summary>Connects and logs in, answering <c>AsyncConnected</c>.</summary>
    public async Task<(TestClient Client, AsyncConnectedRequest Connected)> LoginAsync(string token)
    {
        var client = await ConnectAsync();
        var connected = await client.RequestAsync<AsyncConnectedRequest>(new AsyncAuthRequest
        {
            Name = Asmodee.Users[token].LoginName,
            PartnerToken = new PartnerToken { SessionToken = token },
            GameType = "TT",
        });
        return (client, connected);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(TimeSpan.FromSeconds(5));
        host.Dispose();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}

/// <summary>A scripted client: sends requests, and keeps every packet it receives for the test to look at.</summary>
internal sealed class TestClient : IAsyncDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TcpClient tcp;
    private readonly SslStream tls;
    private readonly PacketWriter writer;
    private readonly Channel<Envelope> inbox = Channel.CreateUnbounded<Envelope>();
    private readonly List<Envelope> pending = [];
    private readonly Task reading;
    private long nextId;

    public TestClient(TcpClient tcp, SslStream tls)
    {
        this.tcp = tcp;
        this.tls = tls;
        writer = new PacketWriter(tls);
        reading = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        var reader = new PacketReader(tls);
        try
        {
            while (await reader.ReadAsync(CancellationToken.None) is { } envelope)
            {
                await inbox.Writer.WriteAsync(envelope);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // Closed.
        }
        finally
        {
            inbox.Writer.TryComplete();
        }
    }

    public async Task<long> SendAsync(IMessage message, long? packetId = null)
    {
        var id = packetId ?? Interlocked.Increment(ref nextId);
        await writer.WriteAsync(id, message, CancellationToken.None);
        return id;
    }

    /// <summary>Sends a request and waits for the reply with the same packet id.</summary>
    public async Task<T> RequestAsync<T>(IMessage message) where T : IMessage<T>, new()
    {
        var envelope = await RequestEnvelopeAsync(message);
        if (envelope.Type != MessageTypes.NumberOf<T>())
        {
            var detail = envelope.Type == MessageTypes.NumberOf<ErrorRequest>() ? envelope.Parse<ErrorRequest>().ToString() : "";
            throw new Xunit.Sdk.XunitException(
                $"expected {MessageTypes.Describe(MessageTypes.NumberOf<T>())}, got {MessageTypes.Describe(envelope.Type)} {detail}");
        }

        return envelope.Parse<T>();
    }

    public async Task<Envelope> RequestEnvelopeAsync(IMessage message)
    {
        var id = await SendAsync(message);
        return await NextAsync(e => e.PacketId == id);
    }

    /// <summary>Waits for a push (packet id 0) of the given type.</summary>
    public async Task<T> PushAsync<T>(Func<T, bool>? match = null) where T : IMessage<T>, new()
    {
        var type = MessageTypes.NumberOf<T>();
        var envelope = await NextAsync(e => e.PacketId == 0 && e.Type == type && (match is null || match(e.Parse<T>())));
        return envelope.Parse<T>();
    }

    /// <summary>True if a push of this type arrives within a short time.</summary>
    public async Task<bool> ReceivesAsync<T>(TimeSpan within) where T : IMessage<T>, new()
    {
        var type = MessageTypes.NumberOf<T>();
        try
        {
            await NextAsync(e => e.Type == type, within);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task<Envelope> NextAsync(Func<Envelope, bool> match, TimeSpan? within = null)
    {
        var index = pending.FindIndex(e => match(e));
        if (index >= 0)
        {
            var found = pending[index];
            pending.RemoveAt(index);
            return found;
        }

        using var timeout = new CancellationTokenSource(within ?? Wait);
        try
        {
            while (true)
            {
                var envelope = await inbox.Reader.ReadAsync(timeout.Token);
                if (match(envelope))
                {
                    return envelope;
                }

                pending.Add(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("no matching packet; received " +
                string.Join(", ", pending.Select(e => $"{MessageTypes.Describe(e.Type)}#{e.PacketId}")));
        }
        catch (ChannelClosedException)
        {
            throw new TimeoutException("the connection closed");
        }
    }

    public async Task<bool> ClosedAsync(TimeSpan within)
    {
        var finished = await Task.WhenAny(reading, Task.Delay(within));
        return finished == reading;
    }

    public async ValueTask DisposeAsync()
    {
        tls.Dispose();
        tcp.Dispose();
        await reading;
    }
}
