using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PowerpalTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string BoardAddress = "http://10.0.0.153";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BackoffPollInterval = TimeSpan.FromMinutes(2);
    private const int FailuresBeforeBackoff = 2;

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private int _consecutiveFailures;

    public TrayApplicationContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = TrayIconRenderer.CreateIcon("--", Color.Gray),
            Text = "PowerpalTray - starting...",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh now", null, async (_, _) => await PollAsync().ConfigureAwait(true));
        menu.Items.Add("Open board page", null, (_, _) => OpenBoardPage());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;

        _timer = new System.Windows.Forms.Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await PollAsync().ConfigureAwait(true);
        _timer.Start();

        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (!await _pollLock.WaitAsync(0).ConfigureAwait(true))
            return;

        try
        {
            var reading = await PowerpalDataService.FetchAsync(BoardAddress).ConfigureAwait(true);
            UpdateTrayIcon(reading);
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private void UpdateTrayIcon(PowerpalReading reading)
    {
        var timestamp = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (reading.Error is not null)
        {
            _consecutiveFailures++;
            _timer.Interval = _consecutiveFailures >= FailuresBeforeBackoff
                ? (int)BackoffPollInterval.TotalMilliseconds
                : (int)PollInterval.TotalMilliseconds;

            SetIcon(TrayIconRenderer.CreateIcon("--", Color.Gray));
            _notifyIcon.Text = Truncate($"Powerpal board offline (last tried {timestamp})");
            return;
        }

        _consecutiveFailures = 0;
        _timer.Interval = (int)PollInterval.TotalMilliseconds;

        if (reading.HasNoReadingYet || reading.Kilowatts is not { } kw)
        {
            SetIcon(TrayIconRenderer.CreateIcon("--", Color.Gray));
            _notifyIcon.Text = Truncate($"Powerpal: no reading yet (updated {timestamp})");
            return;
        }

        SetIcon(TrayIconRenderer.CreateIcon(FormatReading(kw), Color.White));
        _notifyIcon.Text = Truncate($"Grid: {kw:0.00} kW (updated {timestamp})");
    }

    private static string FormatReading(double kw)
    {
        var absKw = Math.Abs(kw);
        if (absKw < 10) return kw.ToString("0.00", CultureInfo.InvariantCulture);
        if (absKw < 100) return kw.ToString("0.0", CultureInfo.InvariantCulture);
        return kw.ToString("0", CultureInfo.InvariantCulture);
    }

    private void SetIcon(Icon newIcon)
    {
        var old = _notifyIcon.Icon;
        _notifyIcon.Icon = newIcon;
        old?.Dispose();
    }

    private static void OpenBoardPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BoardAddress) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show($"Couldn't open the default browser: {ex.Message}", "PowerpalTray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string Truncate(string text) => text.Length <= 127 ? text : text[..127];

    private void ExitApplication()
    {
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        var icon = _notifyIcon.Icon;
        _notifyIcon.Dispose();
        icon?.Dispose();
        Application.Exit();
    }
}
