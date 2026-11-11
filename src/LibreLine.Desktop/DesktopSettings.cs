// SPDX-License-Identifier: GPL-2.0-only
using System.Text.Json;

namespace LibreLine.Desktop;

/// <summary>Where the desktop app keeps its files.</summary>
internal sealed record DesktopPaths(string Root)
{
    /// <summary>The server's data directory: player files and the generated certificate.</summary>
    public string Data => Path.Combine(Root, "data");

    public string Settings => Path.Combine(Root, "desktop.json");

    /// <summary>What the game's settings file held before this app changed it.</summary>
    public string GameState => Path.Combine(Root, "game-settings.json");

    public string GameBackup => Path.Combine(Root, "UserDefault.xml.backup");

    public static DesktopPaths Default() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Libre Line"));
}

/// <summary>The few choices the window remembers between runs.</summary>
internal sealed class DesktopSettings
{
    public int Port { get; set; } = 2445;

    public bool OpenRouterPort { get; set; } = true;

    public static DesktopSettings Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path)) ?? new DesktopSettings()
                : new DesktopSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DesktopSettings();
        }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not worth bothering the player about.
        }
    }
}
