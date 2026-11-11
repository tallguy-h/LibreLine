// SPDX-License-Identifier: GPL-2.0-only
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LibreLine.Desktop.Game;

/// <summary>Which server the game on this PC is set to use.</summary>
internal enum GameServerState
{
    /// <summary>The game isn't installed here, or hasn't been started yet.</summary>
    NoSettingsFile,

    /// <summary>The game's built-in server (which has been shut down).</summary>
    Official,

    /// <summary>The server address this app's server listens on locally.</summary>
    ThisServer,

    /// <summary>Some other server.</summary>
    OtherServer,
}

/// <summary>
/// Points the locally installed game at a server, and back. Only the <c>customAsyncServerName</c> line of
/// <c>UserDefault.xml</c> changes. A copy of the file as it was first found is kept in the app's folder, and the
/// value it replaced is remembered so that undoing restores it.
/// </summary>
internal sealed class GameSettings(string settingsFile, string stateFile, string backupFile)
{
    public const string GameProcessName = "Ticket to Ride";

    public static string DefaultSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Days of Wonder", "Ticket to Ride", "UserDefault.xml");

    public string SettingsFile => settingsFile;

    public static bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName(GameProcessName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    /// <summary>The server the game is set to, or null for the built-in one.</summary>
    public string? CurrentServer() =>
        File.Exists(settingsFile) ? GameSettingsText.GetServer(Read(out _)) : null;

    public GameServerState State(string thisServer)
    {
        if (!File.Exists(settingsFile))
        {
            return GameServerState.NoSettingsFile;
        }

        var current = CurrentServer();
        return current is null ? GameServerState.Official
            : string.Equals(current, thisServer, StringComparison.OrdinalIgnoreCase) ? GameServerState.ThisServer
            : GameServerState.OtherServer;
    }

    /// <summary>Sets the game to use <paramref name="server"/>.</summary>
    /// <exception cref="InvalidOperationException">The game is running, or has no settings file yet.</exception>
    public void Apply(string server)
    {
        EnsureEditable();
        var text = Read(out var bom);
        var current = GameSettingsText.GetServer(text);

        // Remember what was there before this app first changed it, unless someone has changed it since.
        var state = LoadState();
        if (state is null || !string.Equals(state.Applied, current, StringComparison.OrdinalIgnoreCase))
        {
            state = new Remembered { Previous = current };
        }

        if (!File.Exists(backupFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
            File.Copy(settingsFile, backupFile);
        }

        Write(GameSettingsText.SetServer(text, server), bom);
        state.Applied = server;
        SaveState(state);
    }

    /// <summary>
    /// Undoes <see cref="Apply"/>: puts back the server that was set before, or removes the setting so the game
    /// uses its built-in server. Returns the server now set, or null.
    /// </summary>
    public string? Revert()
    {
        EnsureEditable();
        var text = Read(out var bom);
        var current = GameSettingsText.GetServer(text);
        var state = LoadState();
        var restore = state is not null && string.Equals(state.Applied, current, StringComparison.OrdinalIgnoreCase)
            ? state.Previous
            : null;

        Write(restore is null ? GameSettingsText.RemoveServer(text) : GameSettingsText.SetServer(text, restore), bom);
        File.Delete(stateFile);
        return restore;
    }

    private void EnsureEditable()
    {
        if (IsGameRunning())
        {
            throw new InvalidOperationException(
                "Ticket to Ride is running. Quit the game completely first: it rewrites its settings when it closes.");
        }

        if (!File.Exists(settingsFile))
        {
            throw new InvalidOperationException(
                "Ticket to Ride hasn't created its settings yet. Start the game once, quit it, then try again.");
        }
    }

    private string Read(out bool bom)
    {
        var bytes = File.ReadAllBytes(settingsFile);
        bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        return new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
    }

    private void Write(string text, bool bom)
    {
        var temporary = settingsFile + ".libreline.tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(bom));
        File.Move(temporary, settingsFile, overwrite: true);
    }

    private Remembered? LoadState()
    {
        try
        {
            return File.Exists(stateFile) ? JsonSerializer.Deserialize<Remembered>(File.ReadAllText(stateFile)) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SaveState(Remembered state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
        File.WriteAllText(stateFile, JsonSerializer.Serialize(state));
    }

    private sealed class Remembered
    {
        /// <summary>The server set before this app changed it; null for the built-in one.</summary>
        public string? Previous { get; set; }

        /// <summary>What this app set.</summary>
        public string? Applied { get; set; }
    }
}
