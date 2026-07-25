using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BsodDoctor.Models;
using BsodDoctor.Services;

namespace BsodDoctor.Service;

/// <summary>
/// Windows Service olarak çalışan dump tarayıcı.
/// Her 30 dakikada bir minidump dizinini kontrol eder,
/// yeni hata bulursa veritabanına kaydeder ve bildirim marker'ı oluşturur.
/// </summary>
public class DumpScannerService : BackgroundService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<DumpScannerService> _logger;

    // Varsayılan tarama aralığı: 30 dakika
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);

    // Cooldown: aynı hata kodunu 7 gün boyunca tekrar bildirme
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromDays(7);

    // Bildirim marker'larının yazılacağı dizin
    private static readonly string PendingDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "BsodDoctor");

    public DumpScannerService(IDatabaseService db, ILogger<DumpScannerService> logger)
    {
        _db = db;
        _logger = logger;

        // Bildirim dizinini oluştur
        Directory.CreateDirectory(PendingDir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BsodDoctorService başlatıldı. İlk tarama 30 saniye içinde yapılacak.");

        // İlk taramayı 30 saniye gecikmeli yap (servisin tam başlaması için)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForNewDumpsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tarama sırasında hata oluştu.");
            }

            // Bir sonraki taramayı bekle
            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Dump dosyalarını tara, yeni hata varsa bildirim marker'ı oluştur.
    /// </summary>
    private async Task ScanForNewDumpsAsync(CancellationToken cancellationToken)
    {
        var dumpDirs = GetDumpDirectories();
        if (dumpDirs.Count == 0)
        {
            _logger.LogDebug("Dump dizini bulunamadı.");
            return;
        }

        foreach (var dir in dumpDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] dumpFiles;
            try
            {
                dumpFiles = Directory.GetFiles(dir, "*.dmp", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dizin taranamadı: {Dir}", dir);
                continue;
            }

            foreach (var dumpFile in dumpFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await ProcessDumpFileAsync(dumpFile, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dosya işlenirken hata: {File}", dumpFile);
                }
            }
        }
    }

    /// <summary>
    /// Tek bir dump dosyasını işle: oku, DB'ye kaydet, bildirim marker'ı oluştur.
    /// </summary>
    private async Task ProcessDumpFileAsync(string dumpFilePath, CancellationToken cancellationToken)
    {
        var (errorCode, errorMessage) = MinidumpReader.ReadBugCheckCode(dumpFilePath);
        if (errorCode == null)
        {
            _logger.LogDebug("Dosyadan hata kodu okunamadı: {File} — {Msg}", dumpFilePath, errorMessage);
            return;
        }

        // Cooldown kontrolü — aynı hata kodu yakın zamanda görüldüyse atla
        var inCooldown = await _db.IsErrorInCooldownAsync(errorCode, NotificationCooldown, cancellationToken);
        if (inCooldown)
        {
            _logger.LogDebug("Hata kodu cooldown'da: {Code} ({File})", errorCode, dumpFilePath);
            return;
        }

        // Hata bilgisini veritabanından getir
        var bsodError = await _db.FindErrorByCodeAsync(errorCode, cancellationToken);

        var result = new AnalysisResult
        {
            DumpFilePath = dumpFilePath,
            ErrorCode = errorCode,
            ErrorName = bsodError?.ErrorName ?? "Bilinmeyen Hata",
            Description = bsodError?.Description ?? string.Empty,
            SolutionSteps = bsodError?.SolutionSteps ?? string.Empty,
            KesinCozum = bsodError?.KesinCozum ?? string.Empty,
            CommonCauses = bsodError?.CommonCauses ?? string.Empty,
            RelatedKbUrls = bsodError?.RelatedKbUrls ?? string.Empty,
            Severity = bsodError?.Severity ?? 0,
            AnalysisTime = DateTime.UtcNow
        };

        // Veritabanına kaydet
        var historyId = await _db.SaveAnalysisRecordAsync(result, cancellationToken);
        result.HistoryId = historyId;

        _logger.LogInformation("Yeni BSOD tespit edildi: {Code} — {Name} (ID: {Id})",
            errorCode, result.ErrorName, historyId);

        // Bildirim marker'ı oluştur
        await CreatePendingNotificationAsync(result, cancellationToken);

        // Bildirim gönderildi olarak işaretle
        await _db.MarkAsNotifiedAsync(historyId, cancellationToken);
    }

    /// <summary>
    /// WPF uygulamasının --notify modunda okuyacağı bildirim marker'ını yazar.
    /// </summary>
    private async Task CreatePendingNotificationAsync(AnalysisResult result, CancellationToken cancellationToken)
    {
        var pending = new PendingNotification
        {
            HistoryId = result.HistoryId,
            ErrorCode = result.ErrorCode,
            ErrorName = result.ErrorName,
            Severity = result.Severity,
            Timestamp = DateTime.UtcNow
        };

        var filePath = Path.Combine(PendingDir, $"pending_{result.HistoryId}.json");
        var json = JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogDebug("Bildirim marker'ı oluşturuldu: {File}", filePath);
    }

    #region Dump Dizini Bulma

    /// <summary>
    /// Minidump dizinlerini registry'den veya varsayılan yollardan bulur.
    /// BsodWatchService.GetDumpDirectories() ile aynı mantık.
    /// </summary>
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
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[BsodDoctorService] Registry erişim hatası: {ex.Message}");
            }

            if (dirs.Count == 0)
            {
                var minidumpDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
                if (Directory.Exists(minidumpDir))
                    dirs.Add(minidumpDir);
            }
        }
        else
        {
            // Linux test ortamı
            var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDumps");
            if (Directory.Exists(testDir))
                dirs.Add(testDir);
        }

        return dirs;
    }

    #endregion
}

/// <summary>
/// Bildirim marker'ı için model — JSON olarak dosyaya yazılır,
/// WPF uygulaması --notify modunda bu dosyayı okuyup toast gösterir.
/// </summary>
internal class PendingNotification
{
    public int HistoryId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorName { get; init; } = string.Empty;
    public int Severity { get; init; }
    public DateTime Timestamp { get; init; }
}
