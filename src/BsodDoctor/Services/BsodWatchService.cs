using System.IO;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump dizinini tarayan, hataları bulan ve sonucu döndüren servis.
/// </summary>
public class BsodWatchService
{
    private readonly DatabaseService _db;
    private readonly TimeSpan _cooldown;

    public BsodWatchService(DatabaseService db, TimeSpan? cooldown = null)
    {
        _db = db;
        _cooldown = cooldown ?? TimeSpan.FromDays(1);
    }

    /// <summary>
    /// One-shot tarama.
    /// <paramref name="scanAll"/> = true ise tüm .dmp dosyalarını tara,
    /// false ise sadece son 24 saatte değişmiş olanları tara.
    /// </summary>
    public async Task<AnalysisResult?> ScanOnceAsync(bool scanAll = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var dumpDirs = GetDumpDirectories();
            if (dumpDirs.Count == 0)
                return null;

            var dumpFiles = scanAll
                ? GetAllDumpFiles(dumpDirs)
                : FindRecentDumpFiles(dumpDirs);

            if (dumpFiles.Count == 0)
                return null;

            foreach (var dumpFile in dumpFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(dumpFile);

                if (errorCode == null)
                    continue;

                // Cooldown kontrolü — aynı hata cooldown süresinde görüldüyse atla
                var inCooldown = await _db.IsErrorInCooldownAsync(errorCode, _cooldown, cancellationToken);
                if (inCooldown)
                    continue;

                var bsodError = await _db.FindErrorByCodeAsync(errorCode, cancellationToken);

                var result = new AnalysisResult
                {
                    DumpFilePath = dumpFile,
                    ErrorCode = errorCode,
                    ErrorName = bsodError?.ErrorName ?? "Bilinmeyen Hata",
                    Description = bsodError?.Description ?? "Bu hata kodu için henüz kayıtlı çözüm yok.",
                    SolutionSteps = bsodError?.SolutionSteps ?? string.Empty,
                    KesinCozum = bsodError?.KesinCozum ?? string.Empty,
                    CommonCauses = bsodError?.CommonCauses ?? string.Empty,
                    RelatedKbUrls = bsodError?.RelatedKbUrls ?? string.Empty,
                    Severity = bsodError?.Severity ?? 0,
                    AnalysisTime = DateTime.Now
                };

                var historyId = await _db.SaveAnalysisRecordAsync(result, cancellationToken);
                result.HistoryId = historyId;

                return result;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Tarama sırasında hata: {ex.Message}", ex);
        }
    }

    #region Dizin / Dosya Bulma

    private static List<string> GetDumpDirectories()
    {
        var dirs = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
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
            catch { }

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

    /// <summary>Son 1 günde değişmiş .dmp dosyalarını bulur.</summary>
    private static List<string> FindRecentDumpFiles(List<string> directories)
    {
        var files = new List<string>();
        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(1);

        foreach (var dir in directories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.dmp", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) >= cutoff)
                            files.Add(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        return files;
    }

    /// <summary>Tüm .dmp dosyalarını bulur (yaş sınırı yok).</summary>
    private static List<string> GetAllDumpFiles(List<string> directories)
    {
        var files = new List<string>();
        foreach (var dir in directories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.dmp", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        // Dosyayı okuyabiliyor muyuz diye kontrol et
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (fs.Length >= 36) // minidump header en az 36 byte
                            files.Add(file);
                    }
                    catch { }
                }
            }
            catch { }
        }
        return files;
    }

    #endregion
}
