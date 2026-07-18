namespace BsodDoctor.Models;

/// <summary>
/// BSOD hata kodlarını ve çözüm bilgilerini tutan model.
/// </summary>
public class BsodError
{
    public int Id { get; set; }
    public string ErrorCode { get; set; } = string.Empty;        // 0x0000001A
    public string ErrorName { get; set; } = string.Empty;         // MEMORY_MANAGEMENT
    public string Category { get; set; } = string.Empty;          // Donanım, Sürücü, Yazılım
    public string Description { get; set; } = string.Empty;       // Kısa açıklama
    public string SolutionSteps { get; set; } = string.Empty;     // Adım adım çözüm (Markdown)
    public string CommonCauses { get; set; } = string.Empty;      // Yaygın nedenler
    public string RelatedKbUrls { get; set; } = string.Empty;     // Microsoft KB linkleri
    public int Severity { get; set; }                             // 1-5
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
