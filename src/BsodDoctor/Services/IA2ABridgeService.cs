using System.Net.Http.Json;
using System.Text.Json;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Hermes A2A Bridge üzerinden Vis agent ile iletişim kuran servis arayüzü.
/// </summary>
public interface IA2ABridgeService
{
    /// <summary>Bilinmeyen bir BSOD hatasını Vis'e sor</summary>
    Task<BsodError?> QueryVisForSolutionAsync(string errorCode);

    /// <summary>Vis'in sağlıklı çalışıp çalışmadığını kontrol et</summary>
    Task<bool> HealthCheckAsync();
}
