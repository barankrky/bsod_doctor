using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using BsodDoctor.Models;

namespace BsodDoctor.Services;

/// <summary>
/// Hermes A2A Bridge üzerinden Vis (192.168.1.69:8765) ile iletişim kurar.
/// Bilinmeyen BSOD hatalarını Vis'e araştırtır ve çözümü DB'ye ekler.
/// </summary>
public class A2ABridgeService : IA2ABridgeService
{
    private readonly HttpClient _httpClient;
    private readonly IDatabaseService _databaseService;
    private readonly string _baseUrl;
    private readonly string _token;

    public A2ABridgeService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        // AGENTS.md'de belirtilen Vis adresi
        _baseUrl = Environment.GetEnvironmentVariable("VIS_A2A_URL") ?? "http://192.168.1.69:8765";
        _token = Environment.GetEnvironmentVariable("VIS_A2A_TOKEN") ?? "vis-friday-a2a-2026";

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
    }

    public async Task<BsodError?> QueryVisForSolutionAsync(string errorCode)
    {
        try
        {
            Debug.WriteLine($"[A2A] Vis'e sorgu gönderiliyor: {errorCode}");

            var payload = new
            {
                action = "research_bsod",
                error_code = errorCode,
                source = "bsod_doctor_wpf"
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/bsod-query", payload);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[A2A] Vis yanıt vermedi: {response.StatusCode}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Debug.WriteLine($"[A2A] Vis'ten yanıt alındı");

            // Vis'ten gelen çözümü parse et
            if (result.TryGetProperty("error", out _))
                return null;

            var bsodError = new BsodError
            {
                ErrorCode = errorCode,
                ErrorName = result.TryGetProperty("error_name", out var name) ? name.GetString() ?? "Bilinmiyor" : "Bilinmiyor",
                Category = result.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                Description = result.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                SolutionSteps = result.TryGetProperty("solution_steps", out var steps) ? steps.GetString() : null,
                CommonCauses = result.TryGetProperty("common_causes", out var causes) ? causes.GetString() : null,
                RelatedKbUrls = result.TryGetProperty("related_kb_urls", out var urls) ? urls.GetString() : null,
                Severity = result.TryGetProperty("severity", out var sev) ? sev.GetInt32() : 3,
            };

            // Çözümü veritabanına ekle
            await _databaseService.InsertErrorAsync(bsodError);

            return bsodError;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[A2A] Vis iletişim hatası: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
