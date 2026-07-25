using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BsodDoctor.Models;
using BsodDoctor.Services;

namespace BsodDoctor.ViewModels;

/// <summary>
/// Ana pencere için ViewModel.
/// Uygulama açılırken BsodWatchService ile minidump taraması yapar.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly BsodWatchService _watchService;

    private int _currentHistoryId;

    public MainViewModel()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "bsod_errors.db");
        var seedPath = Path.Combine(baseDir, "..", "..", "..", "..", "..", "database", "seed_data.json");

        // Alternatif seed path (çalışma dizinine göre)
        if (!File.Exists(seedPath))
        {
            var cwdSeed = Path.Combine(Environment.CurrentDirectory, "database", "seed_data.json");
            if (File.Exists(cwdSeed))
                seedPath = cwdSeed;
        }

        _databaseService = new DatabaseService(dbPath);
        _watchService = new BsodWatchService(_databaseService, TimeSpan.FromDays(1), TimeSpan.FromDays(1));

        // Veritabanını başlat + seed data import
        _ = InitializeAsync(seedPath);

        // Otomatik tarama başlat
        _ = StartWatchScanAsync();
    }

    private async Task InitializeAsync(string seedPath)
    {
        try
        {
            await _databaseService.InitializeAsync(seedPath);
            StatusText = "Hazır";
        }
        catch (Exception ex)
        {
            StatusText = $"Veritabanı hatası: {ex.Message}";
        }
    }

    /// <summary>
    /// One-shot tarama yap, sonucu doğrudan UI'a yansıt.
    /// </summary>
    private async Task StartWatchScanAsync()
    {
        StatusText = "Minidump taranıyor...";
        IsAnalyzing = true;

        try
        {
            var result = await _watchService.ScanOnceAsync();

            if (result != null)
            {
                // Yeni hata bulundu — UI'ı güncelle
                _currentHistoryId = result.HistoryId;
                ErrorCode = result.ErrorCode;
                ErrorName = result.ErrorName;
                Description = result.Description;
                SolutionSteps = result.SolutionSteps;
                KesinCozum = result.KesinCozum;
                CommonCauses = result.CommonCauses;
                RelatedKbUrls = result.RelatedKbUrls;
                Severity = result.Severity;
                DumpFilePath = result.DumpFilePath;
                HasResult = true;
                IsResolved = false;
                StatusText = $"{result.ErrorName} bulundu!";
            }
            else
            {
                StatusText = "Yeni bir BSOD bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Tarama hatası: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    // ---- Bindable Properties ----

    [ObservableProperty]
    private string _statusText = "Başlatılıyor...";

    [ObservableProperty]
    private string _errorCode = string.Empty;

    [ObservableProperty]
    private string _errorName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _solutionSteps = string.Empty;

    [ObservableProperty]
    private string _kesinCozum = string.Empty;

    [ObservableProperty]
    private string _commonCauses = string.Empty;

    [ObservableProperty]
    private string _relatedKbUrls = string.Empty;

    [ObservableProperty]
    private string _dumpFilePath = string.Empty;

    [ObservableProperty]
    private int _severity;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isResolved;

    // ---- Commands ----

    /// <summary>
    /// Taramayı manuel olarak yeniden başlatır.
    /// </summary>
    [RelayCommand]
    private async Task ScanNowAsync()
    {
        if (IsAnalyzing) return;

        IsAnalyzing = true;
        HasResult = false;
        ErrorCode = string.Empty;
        ErrorName = string.Empty;
        Description = string.Empty;
        SolutionSteps = string.Empty;
        KesinCozum = string.Empty;
        CommonCauses = string.Empty;
        RelatedKbUrls = string.Empty;
        DumpFilePath = string.Empty;
        Severity = 0;
        IsResolved = false;

        await StartWatchScanAsync();
    }

    /// <summary>
    /// Bulunan hatayı "çözüldü" olarak işaretler.
    /// </summary>
    [RelayCommand]
    private async Task MarkResolvedAsync()
    {
        if (_currentHistoryId <= 0) return;

        try
        {
            await _databaseService.MarkAsResolvedAsync(_currentHistoryId, "Kullanıcı tarafından çözüldü olarak işaretlendi.");
            IsResolved = true;
            StatusText = "Hata çözüldü olarak işaretlendi.";
        }
        catch (Exception ex)
        {
            StatusText = $"İşaretleme hatası: {ex.Message}";
        }
    }

    /// <summary>
    /// Sonucu temizler.
    /// </summary>
    [RelayCommand]
    private void ClearResult()
    {
        HasResult = false;
        ErrorCode = string.Empty;
        ErrorName = string.Empty;
        Description = string.Empty;
        SolutionSteps = string.Empty;
        KesinCozum = string.Empty;
        CommonCauses = string.Empty;
        RelatedKbUrls = string.Empty;
        DumpFilePath = string.Empty;
        Severity = 0;
        IsResolved = false;
        _currentHistoryId = 0;
        StatusText = "Hazır";
    }
}
