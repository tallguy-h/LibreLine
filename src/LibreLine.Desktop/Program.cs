// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var single = new Mutex(initiallyOwned: true, @"Local\LibreLine.Desktop", out var first);
        if (!first)
        {
            MessageBox.Show("Libre Line is already running. Look for its window on the taskbar.", "Libre Line",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(DesktopPaths.Default()));
    }
}
