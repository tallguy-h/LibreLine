// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Desktop.Game;

namespace LibreLine.Desktop;

/// <summary>The first page: host a game, or join one.</summary>
internal sealed class HomeScreen : Screen
{
    private readonly DesktopServices services;
    private readonly Label gameLine = Ui.Text(color: Ui.Quiet);
    private readonly LinkLabel undo;
    private readonly Label hostText;
    private readonly Label joinText;

    public HomeScreen(DesktopServices services, Action host, Action join)
    {
        this.services = services;
        undo = Ui.Link("Put my game's setting back", Undo);

        var title = new Label { AutoSize = true, Text = "Libre Line", Font = Ui.Scaled(this, 2f, FontStyle.Bold) };
        var intro = Ui.Text("Play Ticket to Ride Classic online again, with your friends. What would you like to do?");

        var choices = new TableLayoutPanel
        {
            ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 12),
        };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.Controls.Add(Choice("Host a game", host), 0, 0);
        choices.Controls.Add(Choice("Join a friend's game", join), 1, 0);
        hostText = new Label
        {
            AutoSize = true, Text = "Run a server on this PC. Your friends connect to you, and you play too.",
        };
        joinText = new Label { AutoSize = true, Text = "Someone else is hosting. Enter the address they give you." };
        choices.Controls.Add(hostText, 0, 1);
        choices.Controls.Add(joinText, 1, 1);

        Add(title, intro, choices, gameLine, undo,
            Ui.Text("Unofficial: not made or endorsed by Asmodee or Days of Wonder. Some tickets don't expire.",
                Ui.Quiet));
        Body.Controls[^1].Margin = new Padding(3, 24, 3, 3);
    }

    public override void RefreshGame(bool gameRunning)
    {
        var state = services.Game.State(Ui.LocalServerName(services.Settings.Port));
        gameLine.Text = Ui.DescribeGame(services.Game, Ui.LocalServerName(services.Settings.Port));
        undo.Visible = state is GameServerState.ThisServer or GameServerState.OtherServer;
        undo.Enabled = !gameRunning;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var half = new Size(Math.Max(120, (ClientSize.Width - 72) / 2), 0);
        hostText.MaximumSize = half;
        joinText.MaximumSize = half;
    }

    private Button Choice(string text, Action click)
    {
        var button = new Button
        {
            Text = text, Dock = DockStyle.Fill, Height = 72, Font = Ui.Scaled(this, 1.4f, FontStyle.Bold),
            Margin = new Padding(3, 3, 12, 3),
        };
        button.Click += (_, _) => click();
        return button;
    }

    private void Undo()
    {
        if (Ui.ChangeGame(this, () => services.Game.Revert()))
        {
            MessageBox.Show(this, Ui.DescribeGame(services.Game, Ui.LocalServerName(services.Settings.Port)),
                "Libre Line", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        RefreshGame(GameSettings.IsGameRunning());
    }
}
