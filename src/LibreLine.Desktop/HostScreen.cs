// SPDX-License-Identifier: GPL-2.0-only
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LibreLine.Desktop.Game;
using LibreLine.Desktop.Network;

namespace LibreLine.Desktop;

/// <summary>Runs the server, opens the router's port, sets up the local game, and shows the addresses to share.</summary>
internal sealed class HostScreen : Screen
{
    private const int MaxLogLines = 1000;

    private readonly DesktopServices services;
    private readonly Action back;

    private readonly Label status = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
    private readonly Label statusDetail = Ui.Text();
    private readonly Button retry;
    private readonly Label players = Ui.Text(color: Ui.Quiet);

    private readonly TextBox lanBox;
    private readonly Label lanOther = Ui.Text(color: Ui.Quiet);
    private readonly TextBox internetBox;
    private readonly Label internetStatus = Ui.Text();
    private readonly Button otherPort;
    private readonly Button copyInvite;

    private readonly Label gameLine = Ui.Text();
    private readonly Button setUpGame;

    private readonly LinkLabel detailsToggle;
    private readonly TableLayoutPanel details;
    private readonly NumericUpDown portBox = new() { Minimum = 1024, Maximum = 65535, Width = 80 };
    private readonly CheckBox upnpBox = new() { AutoSize = true, Text = "Open the port on my router automatically (UPnP)" };
    private readonly TextBox log = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Height = 160,
        Dock = DockStyle.Fill, BackColor = SystemColors.Window,
    };

    private int runningPort;
    private bool busy;
    private IReadOnlyList<string> online = [];
    private CancellationTokenSource? routerWork;

    public HostScreen(DesktopServices services, Action back)
    {
        this.services = services;
        this.back = back;
        status.Font = Ui.Scaled(this, 1.6f, FontStyle.Bold);
        retry = Ui.Button("Try again", async (_, _) => await StartAsync());
        lanBox = Ui.AddressBox(this);
        internetBox = Ui.AddressBox(this);
        internetBox.PlaceholderText = "finding it...";
        otherPort = Ui.Button("Use another port", async (_, _) => await ChangePortAsync(Port + 1));
        copyInvite = Ui.CopyButton("Copy an invitation to send your friends", Invitation,
            _ => AppendLog("Invitation copied to the clipboard."));
        copyInvite.Font = Ui.Scaled(this, 1.1f);
        setUpGame = Ui.Button("Set up my game", (_, _) => SetUpGame(quiet: false));

        portBox.Value = Math.Clamp(services.Settings.Port, (int)portBox.Minimum, (int)portBox.Maximum);
        upnpBox.Checked = services.Settings.OpenRouterPort;
        upnpBox.CheckedChanged += async (_, _) => await UpnpChangedAsync();

        details = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            Visible = false,
        };
        details.Controls.Add(Ui.Row(
            new Label { AutoSize = true, Text = "Port:", Margin = new Padding(3, 7, 3, 3) },
            portBox,
            Ui.Button("Use this port", async (_, _) => await ChangePortAsync((int)portBox.Value)),
            Ui.Link("Open data folder", () => OpenFolder(services.Paths.Data))));
        details.Controls.Add(upnpBox);
        details.Controls.Add(new Label { AutoSize = true, Text = "Server log", Margin = new Padding(3, 8, 3, 0) });
        details.Controls.Add(log);
        detailsToggle = Ui.Link("Show details ▸", ToggleDetails);

        Add(Ui.Row(status, retry),
            statusDetail,
            players,
            Ui.Card("Friends on the same Wi-Fi or network as you",
                Ui.Row(lanBox, Ui.CopyButton("Copy", () => lanBox.Text)),
                lanOther),
            Ui.Card("Friends over the internet",
                Ui.Row(internetBox, Ui.CopyButton("Copy", () => internetBox.Text)),
                internetStatus,
                otherPort),
            Ui.Row(copyInvite),
            Ui.Card("Your game on this PC", gameLine, setUpGame),
            Ui.Row(Ui.Button("Stop hosting", async (_, _) => await StopAndGoBackAsync()), detailsToggle),
            details);
        Body.Controls[^3].Margin = new Padding(0, 8, 0, 0);

        services.Server.Log += line => OnUi(() => AppendLog(line));
        services.Server.PlayersChanged += names => OnUi(() => ShowPlayers(names));
        retry.Visible = false;
        otherPort.Visible = false;
        ShowStatus();
    }

    public bool IsHosting => services.Server.IsRunning;

    public int PlayersOnline { get; private set; }

    private int Port => services.Server.IsRunning ? runningPort : services.Settings.Port;

    public override Task EnterAsync() => StartAsync();

    public override void RefreshGame(bool gameRunning)
    {
        var name = Ui.LocalServerName(Port);
        var state = services.Game.State(name);
        setUpGame.Visible = state != GameServerState.ThisServer;
        setUpGame.Enabled = !gameRunning && state != GameServerState.NoSettingsFile;
        setUpGame.Text = gameRunning ? "Set up my game (quit the game first)" : "Set up my game";
        gameLine.ForeColor = state == GameServerState.ThisServer ? Ui.Good : SystemColors.ControlText;
        gameLine.Text = state switch
        {
            GameServerState.ThisServer when services.Server.IsRunning =>
                "✔ Your game is set up to play here. Start Ticket to Ride and go online as usual.",
            GameServerState.ThisServer => "Your game is set up to play on this server once it's running.",
            GameServerState.NoSettingsFile => Ui.DescribeGame(services.Game, name),
            _ => Ui.DescribeGame(services.Game, name) + " To play on your own server, set it up here.",
        };
    }

    /// <summary>Stops the server and closes the router's port.</summary>
    public async Task StopAsync()
    {
        CancelRouterWork();
        await services.Router.CloseAsync();
        await services.Server.StopAsync();
        ShowStatus();
    }

    private async Task StartAsync()
    {
        if (busy || services.Server.IsRunning)
        {
            return;
        }

        busy = true;
        retry.Visible = false;
        otherPort.Visible = false;
        status.Text = "Starting your server...";
        status.ForeColor = SystemColors.ControlText;
        statusDetail.Text = "";
        var port = services.Settings.Port;
        try
        {
            runningPort = await services.Server.StartAsync(port);
        }
        catch (Exception e)
        {
            var inUse = e is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse }
                        || e.InnerException is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse };
            AppendLog($"Couldn't start the server: {e.Message}");
            status.Text = "⚠ Your server couldn't start";
            status.ForeColor = Ui.Bad;
            statusDetail.Text = inUse
                ? $"Port {port} is already in use on this PC. Is another server running? Try another port."
                : e.Message;
            retry.Visible = true;
            otherPort.Text = $"Use port {port + 1} instead";
            otherPort.Visible = inUse;
            return;
        }
        finally
        {
            busy = false;
        }

        ShowStatus();
        ShowLocalAddresses();
        SetUpGame(quiet: true);
        await RefreshRouterAsync();
    }

    private async Task StopAndGoBackAsync()
    {
        if (PlayersOnline > 0 && MessageBox.Show(FindForm(),
                $"{PlayersOnline} player(s) are connected. Stopping ends their games. Stop anyway?", "Libre Line",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        Enabled = false;
        await StopAsync();
        Enabled = true;
        back();
    }

    private async Task ChangePortAsync(int port)
    {
        if (busy || port is < 1024 or > 65535)
        {
            return;
        }

        services.Settings.Port = port;
        services.SaveSettings();
        portBox.Value = port;
        Enabled = false;
        await StopAsync();
        Enabled = true;
        await StartAsync();
    }

    private async Task UpnpChangedAsync()
    {
        services.Settings.OpenRouterPort = upnpBox.Checked;
        services.SaveSettings();
        if (!services.Server.IsRunning)
        {
            return;
        }

        if (!upnpBox.Checked)
        {
            CancelRouterWork();
            await services.Router.CloseAsync();
        }

        await RefreshRouterAsync();
    }

    /// <summary>Points the local game at this server, unless the game is running or not set up yet.</summary>
    private void SetUpGame(bool quiet)
    {
        var name = Ui.LocalServerName(Port);
        var state = services.Game.State(name);
        if (state is GameServerState.ThisServer or GameServerState.NoSettingsFile
            || (quiet && GameSettings.IsGameRunning()))
        {
            RefreshGame(GameSettings.IsGameRunning());
            return;
        }

        var changed = quiet
            ? TryQuietly(() => services.Game.Apply(name))
            : Ui.ChangeGame(FindForm()!, () => services.Game.Apply(name));
        if (changed)
        {
            AppendLog($"Your game on this PC is set to play on {name}.");
        }

        RefreshGame(GameSettings.IsGameRunning());
    }

    private bool TryQuietly(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException
            or FormatException)
        {
            AppendLog($"Couldn't set up your game: {e.Message}");
            return false;
        }
    }

    private void ShowStatus()
    {
        if (services.Server.IsRunning)
        {
            status.Text = "● Your server is running";
            status.ForeColor = Ui.Good;
            statusDetail.Text = "Give your friends one of the addresses below. Keep this window open while you play.";
        }
        else if (!busy)
        {
            status.Text = "Your server is stopped";
            status.ForeColor = SystemColors.ControlText;
            statusDetail.Text = "";
            internetBox.Text = "";
            internetStatus.Text = "";
        }

        ShowPlayers(services.Server.IsRunning ? null : []);
        RefreshGame(GameSettings.IsGameRunning());
    }

    private void ShowPlayers(IReadOnlyList<string>? names)
    {
        online = names ?? online;
        PlayersOnline = online.Count;
        players.Text = !services.Server.IsRunning ? ""
            : online.Count == 0 ? "No one has connected yet."
            : $"Connected ({online.Count}): {string.Join(", ", online)}";
    }

    private void ShowLocalAddresses()
    {
        var addresses = NetworkAddresses.LocalAddresses();
        lanBox.Text = addresses.Count == 0 ? "" : NetworkAddresses.ServerName(addresses[0], Port);
        lanOther.Text = addresses.Count > 1
            ? "If that doesn't work, this PC also has: " +
              string.Join(", ", addresses.Skip(1).Select(a => NetworkAddresses.ServerName(a, Port)))
            : "";
        lanOther.Visible = addresses.Count > 1;
    }

    /// <summary>Opens the port on the router (if wanted) and works out the address friends use.</summary>
    private async Task RefreshRouterAsync()
    {
        CancelRouterWork();
        routerWork = new CancellationTokenSource();
        var token = routerWork.Token;
        var port = runningPort;
        var local = NetworkAddresses.LocalAddresses().FirstOrDefault();
        internetBox.Text = "";
        otherPort.Visible = false;

        try
        {
            RouterPortResult? result = null;
            if (upnpBox.Checked)
            {
                internetStatus.ForeColor = SystemColors.ControlText;
                internetStatus.Text = "Asking your router to let friends in...";
                result = await services.Router.OpenAsync(port, token);
                local = result.LocalAddress ?? local;
            }

            var external = result?.ExternalAddress;
            var lookedUp = false;
            if (external is null || NetworkAddresses.IsPrivate(external))
            {
                internetStatus.Text = "Finding your internet address...";
                if (await NetworkAddresses.LookUpPublicAddressAsync(token) is { } online)
                {
                    external = online;
                    lookedUp = true;
                }
            }

            token.ThrowIfCancellationRequested();
            internetBox.Text = external is null ? "" : NetworkAddresses.ServerName(external, port);
            internetBox.PlaceholderText = external is null ? "unknown" : "";

            var lines = new List<string>();
            var forward = $"forward TCP port {port} to {local?.ToString() ?? "this PC"} in your router's settings " +
                          "(often called \"port forwarding\" or \"virtual server\")";
            switch (result)
            {
                case null:
                    internetStatus.ForeColor = SystemColors.ControlText;
                    lines.Add($"Automatic port opening is off. Unless you have already done it, {forward}.");
                    break;
                case { Opened: true }:
                    internetStatus.ForeColor = Ui.Good;
                    lines.Add("✔ Your router has opened the way in for your friends.");
                    break;
                case { Conflict: true }:
                    internetStatus.ForeColor = Ui.Bad;
                    otherPort.Text = $"Use port {port + 1} instead";
                    otherPort.Visible = true;
                    break;
                default:
                    internetStatus.ForeColor = Ui.Bad;
                    lines.Add($"⚠ Your router didn't open the way in automatically. To let friends in, " +
                              $"{forward}, or turn on UPnP there.");
                    break;
            }

            if (result?.Problem is { } problem)
            {
                lines.Add(problem);
            }

            if (external is null)
            {
                lines.Add($"Couldn't find your internet address. Search the web for \"what is my IP\" and give " +
                          $"friends that address followed by :{port}.");
            }
            else if (lookedUp && result is not { Opened: true })
            {
                lines.Add("(Your internet address was looked up online.)");
            }

            internetStatus.Text = string.Join(Environment.NewLine, lines);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void CancelRouterWork()
    {
        routerWork?.Cancel();
        routerWork?.Dispose();
        routerWork = null;
    }

    private string Invitation()
    {
        var internet = internetBox.Text;
        var lan = lanBox.Text;
        var main = internet.Length > 0 ? internet : lan;
        if (main.Length == 0)
        {
            return "";
        }

        var alternative = internet.Length > 0 && lan.Length > 0
            ? $"{Environment.NewLine}(If you're on my Wi-Fi, use {lan} instead.)"
            : "";
        return $"""
            Let's play Ticket to Ride online! I'm hosting a Libre Line server.
            Server address: {main}{alternative}

            Easiest: install Libre Line, click "Join a friend's game", paste the address and click "Connect my game".

            Or by hand:
            1. Start Ticket to Ride once (if you never have), then quit it completely.
            2. Press Win+R, type %LOCALAPPDATA%\Days of Wonder\Ticket to Ride and press Enter.
            3. Open UserDefault.xml with Notepad. Straight after the line <userDefaultRoot>, add this line:
               <customAsyncServerName>{main}</customAsyncServerName>
            4. Save the file, start the game and go online as usual.
            """;
    }

    private void ToggleDetails()
    {
        details.Visible = !details.Visible;
        detailsToggle.Text = details.Visible ? "Hide details ▾" : "Show details ▸";
        portBox.Value = Port;
        if (details.Visible)
        {
            ScrollControlIntoView(details);
        }
    }

    private void AppendLog(string line)
    {
        if (log.Lines.Length >= MaxLogLines)
        {
            log.Lines = log.Lines[^(MaxLogLines / 2)..];
        }

        log.AppendText(line + Environment.NewLine);
    }

    private static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true })?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            routerWork?.Dispose();
        }

        base.Dispose(disposing);
    }
}
