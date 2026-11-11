// SPDX-License-Identifier: GPL-2.0-only
using System.Text;
using LibreLine.Desktop.Game;

namespace LibreLine.Desktop.Tests;

public sealed class GameSettingsTextTests
{
    // The game writes a header that isn't well-formed XML, LF line ends and four-space indents.
    private const string Fresh = "<?1.0?>\n<userDefaultRoot>\n    <musicVolume>0.5</musicVolume>\n</userDefaultRoot>\n";

    [Fact]
    public void A_fresh_file_has_no_server()
    {
        Assert.Null(GameSettingsText.GetServer(Fresh));
    }

    [Fact]
    public void Setting_adds_the_line_straight_after_the_root_and_leaves_the_rest()
    {
        var text = GameSettingsText.SetServer(Fresh, "127.0.0.1:2445");

        Assert.Equal(
            "<?1.0?>\n<userDefaultRoot>\n    <customAsyncServerName>127.0.0.1:2445</customAsyncServerName>\n" +
            "    <musicVolume>0.5</musicVolume>\n</userDefaultRoot>\n", text);
        Assert.Equal("127.0.0.1:2445", GameSettingsText.GetServer(text));
    }

    [Fact]
    public void Setting_keeps_windows_line_ends()
    {
        var text = GameSettingsText.SetServer(Fresh.Replace("\n", "\r\n", StringComparison.Ordinal), "a.example:2445");

        Assert.Contains("<userDefaultRoot>\r\n    <customAsyncServerName>a.example:2445</customAsyncServerName>\r\n",
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void Setting_replaces_an_existing_server()
    {
        var text = GameSettingsText.SetServer(GameSettingsText.SetServer(Fresh, "old.example:2445"), "127.0.0.1:2445");

        Assert.Equal("127.0.0.1:2445", GameSettingsText.GetServer(text));
        Assert.Single(text.Split('\n'), line => line.Contains("customAsyncServerName", StringComparison.Ordinal));
    }

    [Fact]
    public void Removing_puts_the_file_back_exactly()
    {
        var text = GameSettingsText.RemoveServer(GameSettingsText.SetServer(Fresh, "127.0.0.1:2445"));

        Assert.Equal(Fresh, text);
    }

    [Fact]
    public void A_file_without_the_root_is_refused()
    {
        Assert.Throws<FormatException>(() => GameSettingsText.SetServer("<?1.0?>\n", "127.0.0.1:2445"));
    }

    [Theory]
    [InlineData("  203.0.113.7:2445 ", "203.0.113.7:2445")]
    [InlineData("<customAsyncServerName>play.example.org:2446</customAsyncServerName>", "play.example.org:2446")]
    [InlineData("Let's play Ticket to Ride online! I'm hosting a Libre Line server.\nServer address: 203.0.113.7:2445\n(If you're on my Wi-Fi, use 192.168.1.61:2445 instead.)", "203.0.113.7:2445")]
    [InlineData("connect to play.example.org.", "play.example.org")]
    [InlineData("hello there", null)]
    [InlineData("", null)]
    public void The_address_is_picked_out_of_what_was_pasted(string pasted, string? server)
    {
        Assert.Equal(server, GameSettingsText.ExtractServer(pasted));
    }

    [Theory]
    [InlineData("127.0.0.1:2445", true)]
    [InlineData("play.example.org", true)]
    [InlineData("play.example.org:2446", true)]
    [InlineData("[2001:db8::1]:2445", true)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    [InlineData("<x>", false)]
    [InlineData("host:port", false)]
    public void Server_names_are_checked(string server, bool valid)
    {
        Assert.Equal(valid, GameSettingsText.IsValidServer(server));
    }
}

public sealed class GameSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "libreline-desktop-" + Guid.NewGuid());
    private readonly GameSettings settings;

    public GameSettingsTests()
    {
        Directory.CreateDirectory(directory);
        settings = new GameSettings(SettingsFile, Path.Combine(directory, "app", "state.json"),
            Path.Combine(directory, "app", "backup.xml"));
    }

    private string SettingsFile => Path.Combine(directory, "UserDefault.xml");

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Without_a_settings_file_nothing_can_change()
    {
        Assert.Equal(GameServerState.NoSettingsFile, settings.State("127.0.0.1:2445"));
        Assert.Throws<InvalidOperationException>(() => settings.Apply("127.0.0.1:2445"));
    }

    [Fact]
    public void Apply_then_revert_restores_the_official_server_and_keeps_a_backup()
    {
        File.WriteAllText(SettingsFile, "<?1.0?>\n<userDefaultRoot>\n    <a>1</a>\n</userDefaultRoot>\n");
        var original = File.ReadAllBytes(SettingsFile);

        settings.Apply("127.0.0.1:2445");
        Assert.Equal(GameServerState.ThisServer, settings.State("127.0.0.1:2445"));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(directory, "app", "backup.xml")));

        Assert.Null(settings.Revert());
        Assert.Equal(GameServerState.Official, settings.State("127.0.0.1:2445"));
        Assert.Equal(original, File.ReadAllBytes(SettingsFile));
    }

    [Fact]
    public void Revert_puts_back_a_server_that_was_set_before()
    {
        File.WriteAllText(SettingsFile,
            "<?1.0?>\n<userDefaultRoot>\n    <customAsyncServerName>friend.example:2445</customAsyncServerName>\n</userDefaultRoot>\n");

        settings.Apply("127.0.0.1:2445");
        Assert.Equal("friend.example:2445", settings.Revert());
        Assert.Equal(GameServerState.OtherServer, settings.State("127.0.0.1:2445"));

        // Undoing again removes it.
        Assert.Null(settings.Revert());
        Assert.Equal(GameServerState.Official, settings.State("127.0.0.1:2445"));
    }

    [Fact]
    public void A_byte_order_mark_is_kept()
    {
        File.WriteAllText(SettingsFile, "<?1.0?>\n<userDefaultRoot>\n</userDefaultRoot>\n", new UTF8Encoding(true));

        settings.Apply("127.0.0.1:2445");

        Assert.True(File.ReadAllBytes(SettingsFile).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }
}
