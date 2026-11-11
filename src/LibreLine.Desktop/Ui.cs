// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Desktop.Game;
using LibreLine.Desktop.Network;

namespace LibreLine.Desktop;

/// <summary>What every screen shares.</summary>
internal sealed record DesktopServices(
    DesktopPaths Paths, DesktopSettings Settings, ServerController Server, RouterPort Router, GameSettings Game)
{
    public void SaveSettings() => Settings.Save(Paths.Settings);
}

/// <summary>A page of the window. Labels tagged <see cref="Ui.Wrap"/> wrap to the page's width.</summary>
internal abstract class Screen : UserControl
{
    protected Screen()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Body = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 12, 16, 16),
        };
        Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(Body);
    }

    protected TableLayoutPanel Body { get; }

    /// <summary>Called when the page is shown.</summary>
    public virtual Task EnterAsync() => Task.CompletedTask;

    /// <summary>Called every few seconds, and after the game's settings change.</summary>
    public abstract void RefreshGame(bool gameRunning);

    protected void Add(params Control[] controls)
    {
        foreach (var control in controls)
        {
            Body.Controls.Add(control);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Ui.FitWrapping(this, Math.Max(240, ClientSize.Width - 72));
    }

    /// <summary>Runs on the UI thread, from any thread; does nothing once the window is gone.</summary>
    protected void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Closing.
        }
    }
}

internal static class Ui
{
    public const string Wrap = "wrap";

    public static readonly Color Good = Color.ForestGreen;
    public static readonly Color Bad = Color.Firebrick;
    public static readonly Color Quiet = SystemColors.GrayText;

    public static Font Scaled(Control control, float scale, FontStyle style = FontStyle.Regular) =>
        new(control.Font.FontFamily, control.Font.Size * scale, style);

    public static Label Text(string text = "", Color? color = null, Font? font = null) => new()
    {
        AutoSize = true, Text = text, Tag = Wrap, ForeColor = color ?? SystemColors.ControlText,
        Font = font, Margin = new Padding(3, 3, 3, 6),
    };

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty,
        };
        row.Controls.AddRange(controls);
        return row;
    }

    public static GroupBox Card(string title, params Control[] content)
    {
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        foreach (var control in content)
        {
            inner.Controls.Add(control);
        }

        var card = new GroupBox
        {
            Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 10, 10), Margin = new Padding(3, 10, 3, 3),
        };
        card.Controls.Add(inner);
        return card;
    }

    public static Button Button(string text, EventHandler click, Font? font = null)
    {
        var button = new Button { AutoSize = true, Text = text, Font = font, Padding = new Padding(6, 2, 6, 2) };
        button.Click += click;
        return button;
    }

    public static LinkLabel Link(string text, Action click)
    {
        var link = new LinkLabel { AutoSize = true, Text = text, Margin = new Padding(3, 8, 3, 3) };
        link.LinkClicked += (_, _) => click();
        return link;
    }

    /// <summary>A large box showing an address to copy.</summary>
    public static TextBox AddressBox(Control owner, bool readOnly = true) => new()
    {
        ReadOnly = readOnly, Width = 300, Font = Scaled(owner, 1.35f), BorderStyle = BorderStyle.FixedSingle,
        BackColor = SystemColors.Window, Margin = new Padding(3, 3, 6, 3),
    };

    public static Button CopyButton(string text, Func<string> value, Action<string>? copied = null)
    {
        var button = new Button { AutoSize = true, Text = text, Padding = new Padding(6, 2, 6, 2) };
        button.Click += (_, _) =>
        {
            var content = value();
            if (content.Length == 0)
            {
                return;
            }

            try
            {
                Clipboard.SetText(content);
                var original = button.Text;
                button.Text = "✔ Copied";
                copied?.Invoke(content);
                var reset = new System.Windows.Forms.Timer { Interval = 1500 };
                reset.Tick += (_, _) =>
                {
                    button.Text = original;
                    reset.Dispose();
                };
                reset.Start();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show(button.FindForm(), "Couldn't use the clipboard. Try again.", "Libre Line");
            }
        };
        return button;
    }

    public static void FitWrapping(Control root, int width)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label { Tag: Wrap } label)
            {
                label.MaximumSize = new Size(width, 0);
            }

            FitWrapping(control, width);
        }
    }

    public static void Warn(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "Libre Line", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Changes the game's settings, showing why not if it can't.</summary>
    public static bool ChangeGame(IWin32Window owner, Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException
            or FormatException)
        {
            Warn(owner, e.Message);
            return false;
        }
    }

    /// <summary>How the game is set, in words.</summary>
    public static string DescribeGame(GameSettings game, string thisServer) => game.State(thisServer) switch
    {
        GameServerState.NoSettingsFile =>
            "Ticket to Ride hasn't been started on this PC yet. Start it once and quit it, then come back.",
        GameServerState.Official => "Your game uses the official server, which has been shut down.",
        GameServerState.ThisServer => $"Your game is set to play on your own server ({thisServer}).",
        _ => $"Your game is set to the server {game.CurrentServer()}.",
    };

    public static string LocalServerName(int port) =>
        NetworkAddresses.ServerName(System.Net.IPAddress.Loopback, port);
}
