using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Veritabanı işlemleri için arayüz.
/// </summary>
public interface IDatabaseService
{
    /// <summary>Veritabanını oluşturur ve seed data'yı import eder.</summary>
    Task InitializeAsync(string? seedDataPath = null, CancellationToken cancellationToken = default);

    /// <summary>Hata koduna göre BSOD kaydını getirir.</summary>
    Task<BsodError?> FindErrorByCodeAsync(string errorCode, CancellationToken cancellationToken = default);

    /// <summary>Aynı hata kodu cooldown süresinde daha önce görüldü mü?</summary>
    Task<bool> IsErrorInCooldownAsync(string errorCode, TimeSpan cooldown, CancellationToken cancellationToken = default);

    /// <summary>Analiz kaydını veritabanına ekler. HistoryId'yi döndürür.</summary>
    Task<int> SaveAnalysisRecordAsync(AnalysisResult result, CancellationToken cancellationToken = default);

    /// <summary>Analiz kaydını çözüldü olarak işaretler.</summary>
    Task MarkAsResolvedAsync(int historyId, string? feedback = null, CancellationToken cancellationToken = default);

    /// <summary>Geçmiş analiz kayıtlarını getirir.</summary>
    Task<List<HistoryItem>> GetHistoryAsync(bool onlyUnresolved = true, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen analiz kaydı için bildirim daha önce gönderilmiş mi?</summary>
    Task<bool> IsNotifiedAsync(int historyId, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen analiz kaydını bildirim gönderildi olarak işaretler.</summary>
    Task MarkAsNotifiedAsync(int historyId, CancellationToken cancellationToken = default);
}
