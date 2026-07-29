namespace BsodDoctor.Models;

/// <summary>
/// Bildirim marker'ı için model — JSON olarak dosyaya yazılır,
/// WPF uygulaması --notify modunda bu dosyayı okuyup toast gösterir.
/// Service (DumpScannerService) ve BackgroundNotifier arasında paylaşılır.
/// </summary>
public class PendingNotification
{
    public int HistoryId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorName { get; init; } = string.Empty;
    public int Severity { get; init; }
    public DateTime Timestamp { get; init; }
}
