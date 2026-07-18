using System.Net.Http;

namespace BsodDoctor.Services;

/// <summary>
/// Vis agent ile A2A köprüsü üzerinden iletişim kuran servis.
/// Vis (192.168.1.69:8765) BSOD hata kodlarını araştırıp veritabanını günceller.
/// </summary>
public class A2ABridgeService
{
    private readonly HttpClient _httpClient;
    private readonly string _visUrl;
    private readonly string _token;

    public A2ABridgeService(string visUrl = "http://192.168.1.69:8765", string token = "vis-friday-a2a-2026")
    {
        _visUrl = visUrl.TrimEnd('/');
        _token = token;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
    }

    /// <summary>
    /// Vis'e bir BSOD hata kodu gönderir ve çözüm araştırmasını ister.
    /// </summary>
    public async Task RequestSolutionAsync(string errorCode, string errorName, CancellationToken cancellationToken = default)
    {
        // TODO: A2A send_message ile Vis'e hata kodunu gönder
        // Vis araştırıp veritabanını güncelleyecek

        await Task.CompletedTask;
    }
}
