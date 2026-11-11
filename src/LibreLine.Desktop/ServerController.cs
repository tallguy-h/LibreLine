// SPDX-License-Identifier: GPL-2.0-only
using System.Globalization;
using LibreLine.Host;
using LibreLine.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreLine.Desktop;

/// <summary>Starts and stops the same server <c>LibreLine.Host</c> runs, inside this process.</summary>
internal sealed class ServerController(string dataDirectory) : IAsyncDisposable
{
    private readonly PlayerCounter players = new();
    private IHost? host;

    /// <summary>Every log line the server writes, on whichever thread wrote it.</summary>
    public event Action<string>? Log;

    /// <summary>The names of the players online, whenever someone logs in or out.</summary>
    public event Action<IReadOnlyList<string>>? PlayersChanged
    {
        add => players.Changed += value;
        remove => players.Changed -= value;
    }

    public bool IsRunning => host is not null;

    /// <summary>Adds a line of the app's own to the log.</summary>
    public void Write(string message) =>
        Log?.Invoke(string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:HH:mm:ss} {message}"));

    /// <summary>Starts the server and returns once it listens; throws if it can't (the port is taken, ...).</summary>
    public async Task<int> StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (host is not null)
        {
            throw new InvalidOperationException("The server is already running.");
        }

        var settings = new Dictionary<string, string?>
        {
            [$"{ServerOptions.Section}:Port"] = port.ToString(CultureInfo.InvariantCulture),
            [$"{ServerOptions.Section}:DataDirectory"] = dataDirectory,
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Microsoft"] = "Warning",
            ["Logging:LogLevel:System.Net.Http.HttpClient"] = "Warning",
        };

        var built = ServerHost.Build([], services =>
        {
            services.AddSingleton<ILoggerProvider>(new CallbackLoggerProvider(line => Log?.Invoke(line)));
            services.AddSingleton<ISessionListener>(players);
        }, configuration => configuration.AddInMemoryCollection(settings));

        try
        {
            await built.StartAsync(cancellationToken);
            var actual = await built.Services.GetRequiredService<Listener>().Port.WaitAsync(cancellationToken);
            host = built;
            Write($"Data folder: {dataDirectory}");
            return actual;
        }
        catch
        {
            await StopHostAsync(built);
            throw;
        }
    }

    public async Task StopAsync()
    {
        var running = host;
        host = null;
        if (running is not null)
        {
            await StopHostAsync(running);
            players.Clear();
            Write("The server has stopped.");
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private static async Task StopHostAsync(IHost running)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await running.StopAsync(timeout.Token);
        }
        catch (Exception e) when (e is OperationCanceledException or AggregateException)
        {
            // Stopping anyway.
        }
        finally
        {
            running.Dispose();
        }
    }

    /// <summary>Who is online, kept from the sessions module's notifications (which arrive under its lock).</summary>
    private sealed class PlayerCounter : ISessionListener
    {
        private readonly Dictionary<int, string> online = [];

        public event Action<IReadOnlyList<string>>? Changed;

        public void SessionStarted(Session session) => Update(o => o[session.AccountId] = session.Name);

        public void SessionEnded(Session session) => Update(o => o.Remove(session.AccountId));

        public void Clear() => Update(o => o.Clear());

        private void Update(Action<Dictionary<int, string>> change)
        {
            List<string> names;
            lock (online)
            {
                change(online);
                names = online.Values.Order(StringComparer.CurrentCultureIgnoreCase).ToList();
            }

            Changed?.Invoke(names);
        }
    }
}
