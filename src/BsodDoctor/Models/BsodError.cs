namespace BsodDoctor.Models;

/// <summary>
/// BSOD hata kodu ve çözüm bilgilerini tutan model sınıfı.
/// </summary>
public class BsodError
{
    public int Id { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? SolutionSteps { get; set; }
    public string? CommonCauses { get; set; }
    public string? RelatedKbUrls { get; set; }
    public int Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>İnsan tarafından okunabilir format: "0x0000001A — MEMORY_MANAGEMENT"</summary>
    public string DisplayName => $"{ErrorCode} — {ErrorName}";

    /// <summary>Ciddiyet seviyesine göre renk kodu</summary>
    public string SeverityColor => Severity switch
    {
        5 => "#FF0000",   // Kırmızı — Kritik
        4 => "#FF4500",   // TuruncuKırmızı — Yüksek
        3 => "#FFA500",   // Turuncu — Orta
        2 => "#FFD700",   // Sarı — Düşük
        _ => "#90EE90",   // Yeşil — Bilgilendirme
    };

    /// <summary>Ciddiyet seviyesi etiketi</summary>
    public string SeverityLabel => Severity switch
    {
        5 => "Kritik",
        4 => "Yüksek",
        3 => "Orta",
        2 => "Düşük",
        1 => "Bilgilendirme",
        _ => "Bilinmiyor",
    };
}
