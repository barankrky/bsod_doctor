using System.IO;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump dizinini tarayan, yeni hataları bulan ve sonucu döndüren one-shot servis.
/// Sürekli beklemez — tara, bul, döndür, kapan.
/// Event-driven değil, direkt <see cref="AnalysisResult"/> döndürür.
/// </summary>
public class BsodWatchService
{
    private readonly DatabaseService _db;
    private readonly TimeSpan _fileAgeLimit;
    private readonly TimeSpan _cooldown;

    public BsodWatchService(DatabaseService db, TimeSpan? fileAgeLimit = null, TimeSpan? cooldown = null)
    {
        _db = db;
        _fileAgeLimit = fileAgeLimit ?? TimeSpan.FromDays(1);
        _cooldown = cooldown ?? TimeSpan.FromDays(1);
    }

    /// <summary>
    /// One-shot tarama. Yeni hata bulunursa <see cref="AnalysisResult"/> döndürür,
    /// bulunamazsa null döndürür.
    /// </summary>
    public async Task<AnalysisResult?> ScanOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) Minidump dizinini kontrol et
            var dumpDirs = GetDumpDirectories();

            if (dumpDirs.Count == 0)
                return null;

            // 2) Son 1 günde değişmiş .dmp dosyalarını bul
            var dumpFiles = FindRecentDumpFiles(dumpDirs);
            if (dumpFiles.Count == 0)
                return null;

            // 3) Her dosyayı dene — ilk yeni hatayı bulunca dur
            foreach (var dumpFile in dumpFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // .dmp dosyasını oku
                var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(dumpFile);

                if (errorCode == null)
                    continue; // bu dosya okunamadı, diğerine geç

                // Cooldown kontrolü — aynı hata son 24 saatte görüldüyse atla
                var inCooldown = await _db.IsErrorInCooldownAsync(errorCode, _cooldown, cancellationToken);
                if (inCooldown)
                    continue;

                // Veritabanında çözüm var mı?
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

                // History'ye kaydet
                var historyId = await _db.SaveAnalysisRecordAsync(result, cancellationToken);
                result.HistoryId = historyId;

                return result; // sadece ilk yeni hatayı döndür
            }

            // Tüm dosyalar kontrol edildi, yeni hata yok
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

    /// <summary>
    /// Varsayılan minidump dizinlerini döndürür.
    /// Windows'ta C:\Windows\Minidump, Linux'ta test dizini.
    /// </summary>
    private static List<string> GetDumpDirectories()
    {
        var dirs = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            // Registry'den gerçek Minidump dizinini oku (REG_EXPAND_SZ tipindeki değerleri genişlet)
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
            catch
            {
                // Registry erişilemezse fallback'e geç
            }

            // Fallback: registry okunamazsa varsayılan yolları dene
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
            // Linux test ortamı — test .dmp dosyaları
            var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDumps");
            if (Directory.Exists(testDir))
                dirs.Add(testDir);
        }

        return dirs;
    }

    /// <summary>
    /// Belirtilen dizinlerde son 1 günde değişmiş .dmp dosyalarını bulur.
    /// </summary>
    private List<string> FindRecentDumpFiles(List<string> directories)
    {
        var files = new List<string>();
        var cutoff = DateTime.UtcNow - _fileAgeLimit;

        foreach (var dir in directories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.dmp", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        if (lastWrite >= cutoff)
                            files.Add(file);
                    }
                    catch
                    {
                        // erişilemeyen dosya — skip
                    }
                }
            }
            catch
            {
                // erişilemeyen dizin — skip
            }
        }

        return files;
    }
}
