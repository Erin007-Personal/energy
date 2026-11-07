using System;
using System.Threading;
using System.Windows.Forms;

namespace PowerpalTray;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\PowerpalTray-3F1C9B7A-5E2D-4A6B-9C3E-7A2B8D4F1C6E";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("PowerpalTray is already running (check the system tray).", "PowerpalTray",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }
}
