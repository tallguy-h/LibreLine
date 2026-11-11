// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Desktop.Game;

namespace LibreLine.Desktop;

/// <summary>Points the game on this PC at someone else's server.</summary>
internal sealed class JoinScreen : Screen
{
    private readonly DesktopServices services;
    private readonly TextBox address;
    private readonly Button connect;
    private readonly Label result = Ui.Text();
    private readonly Label current = Ui.Text(color: Ui.Quiet);
    private readonly LinkLabel undo;

    public JoinScreen(DesktopServices services, Action back)
    {
        this.services = services;
        address = Ui.AddressBox(this, readOnly: false);
        address.PlaceholderText = "for example 203.0.113.7:2445";
        address.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Connect();
            }
        };
        connect = Ui.Button("Connect my game", (_, _) => Connect(), Ui.Scaled(this, 1.15f));
        undo = Ui.Link("Put my game's setting back", Undo);

        Add(Ui.Link("← Back", back),
            new Label { AutoSize = true, Text = "Join a friend's game", Font = Ui.Scaled(this, 1.6f, FontStyle.Bold) },
            Ui.Text("Ask the person hosting for their server address (Libre Line shows it to them), then paste it " +
                    "here. You can paste their whole invitation: the address is picked out of it."),
            Ui.Row(address, connect),
            result,
            current,
            undo);
    }

    public override Task EnterAsync()
    {
        result.Text = "";
        address.Focus();
        return Task.CompletedTask;
    }

    public override void RefreshGame(bool gameRunning)
    {
        var state = services.Game.State(Ui.LocalServerName(services.Settings.Port));
        current.Text = Ui.DescribeGame(services.Game, Ui.LocalServerName(services.Settings.Port));
        connect.Enabled = !gameRunning && state != GameServerState.NoSettingsFile;
        undo.Visible = state is GameServerState.ThisServer or GameServerState.OtherServer;
        undo.Enabled = !gameRunning;
    }

    private void Connect()
    {
        var server = GameSettingsText.ExtractServer(address.Text);
        if (server is null)
        {
            Ui.Warn(this, "That doesn't look like a server address. It should look like 203.0.113.7:2445 or " +
                          "play.example.org:2445.");
            return;
        }

        address.Text = server;
        if (Ui.ChangeGame(this, () => services.Game.Apply(server)))
        {
            result.ForeColor = Ui.Good;
            result.Text = $"✔ Done! Your game will connect to {server}. Start Ticket to Ride and go online as usual.";
        }

        RefreshGame(GameSettings.IsGameRunning());
    }

    private void Undo()
    {
        if (Ui.ChangeGame(this, () => services.Game.Revert()))
        {
            result.ForeColor = SystemColors.ControlText;
            result.Text = "Your game's setting has been put back.";
        }

        RefreshGame(GameSettings.IsGameRunning());
    }
}
