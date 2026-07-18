namespace BsodDoctor.Models;

/// <summary>
/// Bir BSOD analizi sonucunu temsil eden model.
/// </summary>
public class AnalysisResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string? DumpFilePath { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorName { get; set; }
    public bool Resolved { get; set; }
    public string? UserFeedback { get; set; }

    /// <summary>Analiz sonucuyla eşleşen BSOD hata detayı (navigation property)</summary>
    public BsodError? ErrorDetails { get; set; }

    /// <summary>İnsan tarafından okunabilir özet</summary>
    public string Summary
    {
        get
        {
            var status = Resolved ? "✅ Çözüldü" : "❌ Çözülmedi";
            var error = ErrorName ?? ErrorCode ?? "Bilinmeyen Hata";
            return $"[{Timestamp:dd.MM.yyyy HH:mm}] {error} — {status}";
        }
    }
}
