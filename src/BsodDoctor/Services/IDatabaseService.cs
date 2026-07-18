using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Yerel veritabanı (SQLite) işlemlerini yöneten arayüz.
/// </summary>
public interface IDatabaseService
{
    /// <summary>BSOD hata koduna göre çözüm ara</summary>
    Task<BsodError?> GetErrorByCodeAsync(string errorCode);

    /// <summary>Tüm BSOD hata kodlarını getir</summary>
    Task<List<BsodError>> GetAllErrorsAsync();

    /// <summary>Kategoriye göre filtrele</summary>
    Task<List<BsodError>> GetErrorsByCategoryAsync(string category);

    /// <summary>Ciddiyet seviyesine göre filtrele</summary>
    Task<List<BsodError>> GetErrorsBySeverityAsync(int minSeverity);

    /// <summary>Hata kodunda veya adında arama yap</summary>
    Task<List<BsodError>> SearchErrorsAsync(string query);

    /// <summary>Analiz geçmişini kaydet</summary>
    Task SaveAnalysisAsync(AnalysisResult result);

    /// <summary>Analiz geçmişini getir</summary>
    Task<List<AnalysisResult>> GetAnalysisHistoryAsync(int limit = 50);

    /// <summary>Yeni bir BSOD hata kaydı ekle (A2A köprüsü ile)</summary>
    Task InsertErrorAsync(BsodError error);

    /// <summary>Mevcut kaydı güncelle</summary>
    Task UpdateErrorAsync(BsodError error);
}
