using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump (.dmp) dosyalarını analiz eden servis arayüzü.
/// Microsoft.Diagnostics.Runtime (ClrMD) kullanır.
/// </summary>
public interface IDumpAnalyzer
{
    /// <summary>Minidump dosyasını analiz et ve hata kodunu çıkar</summary>
    Task<AnalysisResult> AnalyzeDumpAsync(string dumpFilePath);

    /// <summary>Belirtilen dizindeki tüm dump dosyalarını tara</summary>
    Task<List<string>> FindDumpFilesAsync(string? customPath = null);
}
