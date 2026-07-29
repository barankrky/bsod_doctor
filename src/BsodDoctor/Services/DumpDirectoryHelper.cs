using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump dizinlerini bulmak için ortak yardımcı sınıf.
/// Hem WPF uygulaması (BsodWatchService) hem de Windows Service (DumpScannerService)
/// tarafından kullanılır.
/// </summary>
internal static class DumpDirectoryHelper
{
    /// <summary>
    /// Minidump dizinlerini registry'den veya varsayılan yollardan bulur.
    /// </summary>
    public static List<string> GetDumpDirectories()
    {
        var dirs = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\CrashControl");
                if (key != null)
                {
                    var minidumpDir = key.GetValue("MinidumpDir") as string;
                    if (!string.IsNullOrEmpty(minidumpDir))
                    {
                        minidumpDir = Environment.ExpandEnvironmentVariables(minidumpDir);
                        if (Directory.Exists(minidumpDir))
                            dirs.Add(minidumpDir);
                    }

                    var dumpFile = key.GetValue("DumpFile") as string;
                    if (!string.IsNullOrEmpty(dumpFile))
                    {
                        dumpFile = Environment.ExpandEnvironmentVariables(dumpFile);
                        var dumpDir = Path.GetDirectoryName(dumpFile);
                        if (dumpDir != null && File.Exists(dumpFile))
                            dirs.Add(dumpDir);
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[DumpDirectoryHelper] Registry erişim hatası: {ex.Message}");
            }

            if (dirs.Count == 0)
            {
                var minidumpDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
                if (Directory.Exists(minidumpDir))
                    dirs.Add(minidumpDir);

                var memoryDmp = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP");
                if (File.Exists(memoryDmp))
                    dirs.Add(Path.GetDirectoryName(memoryDmp)!);
            }
        }
        else
        {
            var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDumps");
            if (Directory.Exists(testDir))
                dirs.Add(testDir);
        }

        return dirs;
    }
}
