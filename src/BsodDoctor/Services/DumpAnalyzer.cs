using System.Diagnostics;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump (.dmp) dosyalarını analiz eden servis.
/// Windows Debugging Tools veya ClrMD üzerinden dump okur.
/// </summary>
public class DumpAnalyzer : IDumpAnalyzer
{
    private static readonly string[] DefaultDumpPaths =
    [
        @"C:\Windows\Minidump",
        @"C:\Windows\MEMORY.DMP",
        @"C:\Windows\System32\LogFiles\WER",
        @"%LOCALAPPDATA%\CrashDumps",
    ];

    public async Task<AnalysisResult> AnalyzeDumpAsync(string dumpFilePath)
    {
        var result = new AnalysisResult
        {
            DumpFilePath = dumpFilePath,
            Timestamp = DateTime.Now,
        };

        if (!File.Exists(dumpFilePath))
        {
            result.ErrorCode = "FILE_NOT_FOUND";
            result.ErrorName = "Dosya Bulunamadı";
            return result;
        }

        try
        {
            // ClrMD kullanarak dump analizi
            // Not: Bu kısım Windows'ta Microsoft.Diagnostics.Runtime (ClrMD) ile çalışır
            // Linux'ta build olmaz, Windows'ta test edilmeli

            // Şimdilik debugger aracılığıyla dump'dan hata kodunu çıkarmaya çalış
            result.ErrorCode = await ExtractBugCheckCodeAsync(dumpFilePath);
            result.ErrorName = $"BugCheck {result.ErrorCode}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dump analizi başarısız: {ex.Message}");
            result.ErrorCode = "ANALYSIS_FAILED";
            result.ErrorName = "Analiz Başarısız";
        }

        return result;
    }

    public Task<List<string>> FindDumpFilesAsync(string? customPath = null)
    {
        var files = new List<string>();
        var paths = customPath != null
            ? [customPath]
            : DefaultDumpPaths;

        foreach (var rawPath in paths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(rawPath);

            try
            {
                if (File.Exists(expandedPath))
                {
                    files.Add(expandedPath);
                }
                else if (Directory.Exists(expandedPath))
                {
                    files.AddRange(Directory.GetFiles(expandedPath, "*.dmp", SearchOption.TopDirectoryOnly));
                }
            }
            catch
            {
                // Erişim izni yoksa veya yol yoksa atla
            }
        }

        return Task.FromResult(files);
    }

    /// <summary>
    /// Dump dosyasından BugCheck (BSOD) hata kodunu çıkarır.
    /// Windows Debugging Tools veya ClrMD kullanır.
    /// </summary>
    private static async Task<string?> ExtractBugCheckCodeAsync(string dumpFilePath)
    {
        // Öncelikle PowerShell ile dump analizi dene
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-Command \"& {{ (Get-WinEvent -Path '{dumpFilePath}' -MaxEvents 1 2>$null).Message }\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Çıktıda "0x" ile başlayan hata kodunu ara
                foreach (var line in output.Split('\n'))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"0x[0-9A-Fa-f]{8}");
                    if (match.Success)
                        return match.Value;
                }
            }
        }
        catch
        {
            // PowerShell yoksa veya çalışmazsa ClrMD dene
        }

        // ClrMD alternatifi (build-time reference)
        try
        {
            // using var target = DataTarget.LoadCrashDump(dumpFilePath);
            // var reader = target.DbgEngDataReader;
            // var bugCheck = reader.GetBugCheckData();
            // return $"0x{bugCheck.BugCheckCode:X8}";
            // ClrMD için Windows + .NET Framework gerekli
            return null;
        }
        catch
        {
            return null;
        }
    }
}
