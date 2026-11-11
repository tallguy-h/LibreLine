// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Desktop.Game;
using LibreLine.Desktop.Network;

namespace LibreLine.Desktop;

/// <summary>The window: a warning while the game runs, above one of three pages (home, host, join).</summary>
internal sealed class MainForm : Form
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly DesktopServices services;
    private readonly HomeScreen home;
    private readonly HostScreen host;
    private readonly JoinScreen join;
    private readonly Panel pages = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer gameWatch = new() { Interval = 2000 };

    private readonly Label gameRunningBanner = new()
    {
        Dock = DockStyle.Top, AutoSize = false, Visible = false, Padding = new Padding(16, 8, 16, 8),
        BackColor = Color.FromArgb(255, 243, 205), ForeColor = Color.FromArgb(102, 77, 3),
        Text = "⚠  Ticket to Ride is running. Quit the game before changing which server it uses: it saves " +
               "its settings when it closes, which would undo the change.",
    };

    private Screen? current;
    private bool closing;

    public MainForm(DesktopPaths paths)
    {
        var settings = DesktopSettings.Load(paths.Settings);
        var server = new ServerController(paths.Data);
        services = new DesktopServices(paths, settings, server, new RouterPort(new UpnpClient(http), server.Write),
            new GameSettings(GameSettings.DefaultSettingsFile, paths.GameState, paths.GameBackup));

        Text = "Libre Line";
        using (var icon = typeof(MainForm).Assembly.GetManifestResourceStream("LibreLine.ico"))
        {
            if (icon is not null)
            {
                Icon = new Icon(icon);
            }
        }

        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(720, 760);
        MinimumSize = new Size(520, 520);
        StartPosition = FormStartPosition.CenterScreen;

        home = new HomeScreen(services, () => _ = ShowAsync(this.host!), () => _ = ShowAsync(this.join!));
        host = new HostScreen(services, () => _ = ShowAsync(home));
        join = new JoinScreen(services, () => _ = ShowAsync(home));

        gameRunningBanner.Height = (Font.Height * 2) + gameRunningBanner.Padding.Vertical + 4;
        Controls.Add(pages);
        Controls.Add(gameRunningBanner);
        gameWatch.Tick += (_, _) => RefreshGame();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        gameWatch.Start();
        await ShowAsync(home);
    }

    private async Task ShowAsync(Screen screen)
    {
        if (current == screen)
        {
            return;
        }

        pages.SuspendLayout();
        pages.Controls.Clear();
        pages.Controls.Add(screen);
        pages.ResumeLayout();
        current = screen;
        RefreshGame();
        await screen.EnterAsync();
    }

    private void RefreshGame()
    {
        var running = GameSettings.IsGameRunning();
        gameRunningBanner.Visible = running;
        current?.RefreshGame(running);
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (closing || e.CloseReason == CloseReason.WindowsShutDown || !host.IsHosting)
        {
            return;
        }

        if (host.PlayersOnline > 0 && MessageBox.Show(this,
                $"{host.PlayersOnline} player(s) are connected. Closing Libre Line stops your server and ends their " +
                "games. Close anyway?", "Libre Line", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        // Close the router's port and stop the server before the window goes.
        e.Cancel = true;
        closing = true;
        Enabled = false;
        Text = "Libre Line (stopping...)";
        gameWatch.Stop();
        await host.StopAsync();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gameWatch.Dispose();
            home.Dispose();
            host.Dispose();
            join.Dispose();
            http.Dispose();
        }

        base.Dispose(disposing);
    }
}
