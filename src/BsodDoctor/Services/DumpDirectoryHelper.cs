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

                    var dumpFilePath = key.GetValue("DumpFile") as string;
                    if (!string.IsNullOrEmpty(dumpFilePath))
                    {
                        dumpFilePath = Environment.ExpandEnvironmentVariables(dumpFilePath);
                        var dumpDir = Path.GetDirectoryName(dumpFilePath);
                        if (dumpDir != null && Directory.Exists(dumpDir))
                            dirs.Add(dumpDir);
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[DumpDirectoryHelper] Registry erişim hatası: {ex.Message}");
            }

            // Her zaman C:\Windows\Minidump'ı kontrol et — registry'de MinidumpDir
        // olmasa bile çalışsın (registry bypass bug fix)
        var winMinidump = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
        if (Directory.Exists(winMinidump) && !dirs.Contains(winMinidump))
            dirs.Add(winMinidump);

        // MEMORY.DMP'nin bulunduğu dizini de her zaman ekle
        var memoryDmp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP");
        if (File.Exists(memoryDmp))
        {
            var memDir = Path.GetDirectoryName(memoryDmp)!;
            if (!dirs.Contains(memDir))
                dirs.Add(memDir);
        }

        // Test/development amaçlı — proje içindeki TestDumps klasörü
        var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDumps");
        if (Directory.Exists(testDir))
            dirs.Add(testDir);
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
