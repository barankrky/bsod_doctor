namespace BsodDoctor.Models;

/// <summary>
/// Bir analiz sonucunu temsil eden model.
/// </summary>
public class AnalysisResult
{
    public int HistoryId { get; set; }
    public string DumpFilePath { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SolutionSteps { get; set; } = string.Empty;
    public string KesinCozum { get; set; } = string.Empty;
    public string CommonCauses { get; set; } = string.Empty;
    public string RelatedKbUrls { get; set; } = string.Empty;
    public int Severity { get; set; }
    /// <summary>
    /// Analiz zamanı (UTC). Veritabanındaki timestamp ile tutarlılık için UTC kullanılır.
    /// Not: Bu alan veritabanına kaydedilmez — DB kendi CURRENT_TIMESTAMP'ını kullanır.
    /// </summary>
    public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Çözüm bulundu mu?
    /// </summary>
    public bool HasSolution => !string.IsNullOrEmpty(SolutionSteps);

    /// <summary>
    /// Bu sonuç için Windows bildirimi (toast) daha önce gönderildi mi?
    /// Service tarafından kullanılır — aynı hata için tekrar bildirim göndermemek için.
    /// </summary>
    public bool IsNotified { get; set; }
}
