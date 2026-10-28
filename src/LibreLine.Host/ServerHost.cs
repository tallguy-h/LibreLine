// SPDX-License-Identifier: GPL-2.0-only
using Asmodee.Api;
using LibreLine.Games;
using LibreLine.Lobby;
using LibreLine.Players;
using LibreLine.Protocol;
using LibreLine.Sessions;
using LibreLine.Storage.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreLine.Host;

/// <summary>The composition root: reads the configuration and wires every module.</summary>
public static class ServerHost
{
    /// <param name="configure">Lets tests replace services (the Asmodee client, the clock) after the defaults.</param>
    public static IHost Build(string[] args, Action<IServiceCollection>? configure = null,
        Action<IConfigurationBuilder>? configuration = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        configuration?.Invoke(builder.Configuration);

        var options = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>() ?? new ServerOptions();
        var dataDirectory = Path.GetFullPath(options.DataDirectory, Directory.GetCurrentDirectory());
        options.DataDirectory = dataDirectory;
        builder.Services.AddSingleton(Options.Create(options));

        builder.Services.AddProtocol();
        builder.Services.AddAsmodeeApi(o => o.BaseUrl = options.Asmodee.BaseUrl);
        builder.Services.AddJsonPlayerStore(dataDirectory);
        builder.Services.AddPlayers();
        builder.Services.AddSessions();
        builder.Services.AddOptions<SessionOptions>().Configure<ILoggerFactory>((o, loggers) =>
            o.Certificate = Certificates.Load(options, loggers.CreateLogger("LibreLine.Certificates")));
        builder.Services.AddGames(o =>
        {
            o.Rules = ServerOptions.ParseRules(options.Rules);
            o.Maps = ServerOptions.ParseMaps(options.Maps);
        });
        builder.Services.AddLobby();
        builder.Services.AddSingleton<Listener>();
        builder.Services.AddHostedService(p => p.GetRequiredService<Listener>());

        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    public static async Task<int> RunAsync(string[] args)
    {
        using var host = Build(args);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LibreLine");
        logger.LogInformation("Libre Line: some tickets don't expire. Data directory {Data}",
            host.Services.GetRequiredService<IOptions<ServerOptions>>().Value.DataDirectory);
        await host.RunAsync();
        return 0;
    }
}
