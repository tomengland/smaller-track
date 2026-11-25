using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class SmallerTrack
{
    private const string Title = "Smaller Track";
    private const string SettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";
    private const string BackupName = "SmallerTrackOriginalEdges";
    private const string LeftName = "SuperCurtainLeft";
    private const string RightName = "SuperCurtainRight";

    // The ASUS ASUF1209 touchpad on the GU606AW reports a 147.50 mm X range.
    private const int LeftEdge = 1475;
    private const int RightEdge = 2950;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 0)
            {
                Application.Run(CreateWindow());
                return 0;
            }

            if (args.Length != 1 || (args[0] != "--apply" && args[0] != "--restore"))
                throw new ArgumentException("Open SmallerTrack.exe to use the utility.");

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                    throw new InvalidOperationException("Administrator approval is required to update touchpad settings.");
            }

            using (var writer = new Mutex(false, @"Global\SmallerTrackEdgeSettings"))
            {
                // Serialize the backup and writes even if two windows request an update.
                try { writer.WaitOne(); }
                catch (AbandonedMutexException) { }
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(SettingsPath))
                    {
                        if (args[0] == "--apply")
                            Apply(key);
                        else
                            Restore(key);
                    }
                }
                finally
                {
                    writer.ReleaseMutex();
                }
            }
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static Form CreateWindow()
    {
        var window = new Form
        {
            Text = Title,
            Font = SystemFonts.MessageBoxFont,
            AutoScaleMode = AutoScaleMode.Font,
            ClientSize = new Size(440, 205),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        window.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "ASUS Zephyrus G16 GU606AW  |  Left 10%  ·  Right 20%"
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Widens Windows palm-rejection zones. Some edge touches or clicks may still register. " +
                   "Restart Windows after applying or restoring."
        }, 0, 1);
        var status = new Label { Dock = DockStyle.Fill };
        layout.Controls.Add(status, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(actions, 0, 3);
        var apply = new Button { Text = "Apply", AutoSize = true, Height = 30 };
        var restore = new Button { Text = "Restore original settings", AutoSize = true, Height = 30 };
        actions.Controls.Add(apply);
        actions.Controls.Add(restore);

        Action refresh = delegate
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SettingsPath))
            {
                restore.Enabled = key != null && key.GetValue(BackupName) != null;
                bool saved = key != null && Equals(key.GetValue(LeftName), LeftEdge)
                    && Equals(key.GetValue(RightName), RightEdge);
                status.Text = saved ? "The 10% / 20% settings are saved." : "The 10% / 20% settings are not saved.";
            }
        };
        apply.Click += delegate { if (RunElevated(window, "--apply")) refresh(); };
        restore.Click += delegate { if (RunElevated(window, "--restore")) refresh(); };
        refresh();
        return window;
    }

    private static bool RunElevated(Form owner, string argument)
    {
        try
        {
            using (Process child = Process.Start(new ProcessStartInfo(Application.ExecutablePath, argument)
            {
                UseShellExecute = true,
                Verb = "runas",
                ErrorDialogParentHandle = owner.Handle
            }))
            {
                child.WaitForExit();
                if (child.ExitCode != 0)
                    return false;
            }
            MessageBox.Show(owner, "Restart Windows to load the saved touchpad settings.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Win32Exception error)
        {
            if (error.NativeErrorCode != 1223)
                MessageBox.Show(owner, error.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static void Apply(RegistryKey key)
    {
        string[] previous = ReadBackup(key);
        if (previous == null)
        {
            previous = new[] { EncodeEdge(key, LeftName), EncodeEdge(key, RightName) };
            key.SetValue(BackupName, previous, RegistryValueKind.MultiString);
            key.Flush();
        }

        key.SetValue(LeftName, LeftEdge, RegistryValueKind.DWord);
        key.SetValue(RightName, RightEdge, RegistryValueKind.DWord);
    }

    private static void Restore(RegistryKey key)
    {
        string[] previous = ReadBackup(key);
        if (previous == null)
            return;

        WriteEdge(key, LeftName, DecodeEdge(previous[0]));
        WriteEdge(key, RightName, DecodeEdge(previous[1]));
        key.DeleteValue(BackupName);
    }

    private static string[] ReadBackup(RegistryKey key)
    {
        object value = key.GetValue(BackupName);
        if (value == null)
            return null;
        var backup = value as string[];
        if (backup == null || backup.Length != 2)
            throw new InvalidOperationException("The saved original edge settings could not be read.");
        DecodeEdge(backup[0]);
        DecodeEdge(backup[1]);
        return backup;
    }

    private static string EncodeEdge(RegistryKey key, string name)
    {
        object value = key.GetValue(name);
        if (value == null)
            return "missing";
        if (!(value is int))
            throw new InvalidOperationException("Unexpected registry value for " + name + ".");
        return ((int)value).ToString(CultureInfo.InvariantCulture);
    }

    private static int? DecodeEdge(string entry)
    {
        if (entry == "missing")
            return null;
        int value;
        if (!int.TryParse(entry, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            throw new InvalidOperationException("The saved original edge settings could not be read.");
        return value;
    }

    private static void WriteEdge(RegistryKey key, string name, int? value)
    {
        if (value.HasValue)
            key.SetValue(name, value.Value, RegistryValueKind.DWord);
        else
            key.DeleteValue(name, false);
    }
}
