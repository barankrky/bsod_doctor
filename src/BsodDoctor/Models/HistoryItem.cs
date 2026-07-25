namespace BsodDoctor.Models;

/// <summary>
/// Geçmiş analiz kaydı — tarih, hata kodu, dump dosyası ve çözüm durumunu taşır.
/// </summary>
public class HistoryItem
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorName { get; set; } = string.Empty;
    public string DumpFilePath { get; set; } = string.Empty;
    public bool IsResolved { get; set; }

    // XAML'de görüntüleme için
    public string DisplayTime => Timestamp.ToString("dd.MM.yyyy HH:mm:ss");
    public string ResolvedIcon => IsResolved ? "✅" : "⏳";
}
