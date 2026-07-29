using System.Diagnostics;
using System.IO;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump dizinini tarayan, hataları bulan ve sonucu döndüren servis.
/// </summary>
public class BsodWatchService : IBsodWatchService
{
    private readonly IDatabaseService _db;
    private readonly TimeSpan _cooldown;

    public BsodWatchService(IDatabaseService db, TimeSpan? cooldown = null)
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
            var dumpDirs = DumpDirectoryHelper.GetDumpDirectories();
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
                    AnalysisTime = DateTime.UtcNow
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
                    catch (Exception ex)
                    {
                        // Tek bir dosyaya erişilemezse atla — diğerlerine devam et
                        Debug.WriteLine($"[BSOD Doctor] Dosya atlanıyor (okuma): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Dizine erişilemezse atla — sıradaki dizini dene
                Debug.WriteLine($"[BSOD Doctor] Dizin taranamadı: {ex.Message}");
            }
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
                    // MinidumpReader.ReadBugCheckCode içinde dosya validasyonu yapılır
                    files.Add(file);
                }
            }
            catch (Exception ex)
            {
                // Dizine erişilemezse atla
                Debug.WriteLine($"[BSOD Doctor] Dizin taranamadı: {ex.Message}");
            }
        }
        return files;
    }

    #endregion
}
