using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Minidump tarama servisi için arayüz.
/// </summary>
public interface IBsodWatchService
{
    /// <summary>Minidump dosyalarını tarar ve ilk bulunan yeni hatayı döndürür.</summary>
    Task<AnalysisResult?> ScanOnceAsync(bool scanAll = false, CancellationToken cancellationToken = default);
}
