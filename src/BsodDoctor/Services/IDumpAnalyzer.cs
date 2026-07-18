using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump (.dmp) dosyalarını analiz eden servis arayüzü.
/// </summary>
public interface IDumpAnalyzer
{
    /// <summary>
    /// Bir dump dosyasını analiz eder ve hata bilgilerini döndürür.
    /// </summary>
    Task<AnalysisResult> AnalyzeDumpAsync(string dumpFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen dizindeki tüm dump dosyalarını tarar.
    /// </summary>
    Task<IReadOnlyList<string>> FindDumpFilesAsync(string directoryPath, CancellationToken cancellationToken = default);
}
