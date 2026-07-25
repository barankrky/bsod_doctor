using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
        _watchService = new BsodWatchService(_databaseService, TimeSpan.FromDays(1));

        // Önce veritabanını başlat, sonra geçmişi yükle ve otomatik taramayı başlat
        _ = InitializeAsync(seedPath);
    }

    private async Task InitializeAsync(string seedPath)
    {
        try
        {
            await _databaseService.InitializeAsync(seedPath);
            StatusText = "Hazır";

            // Geçmiş kayıtlarını yükle (çözülmemiş olanlar)
            await RefreshHistoryAsync();

            // DB ve geçmiş hazır olduktan sonra otomatik taramayı başlat
            await StartWatchScanAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Veritabanı hatası: {ex.Message}";
        }
    }

    /// <summary>
    /// Geçmiş analiz kayıtlarını veritabanından yükler.
    /// </summary>
    private async Task RefreshHistoryAsync()
    {
        try
        {
            var items = await _databaseService.GetHistoryAsync(onlyUnresolved: true);
            HistoryItems.Clear();
            foreach (var item in items)
                HistoryItems.Add(item);
        }
        catch
        {
            // Geçmiş yüklenemezse sessizce geç — kullanıcı butonla tekrar deneyebilir
        }
    }

    /// <summary>
    /// One-shot tarama yap, sonucu doğrudan UI'a yansıt.
    /// </summary>
    private async Task StartWatchScanAsync(bool scanAll = false)
    {
        StatusText = scanAll ? "Tüm dump dosyaları taranıyor..." : "Minidump taranıyor...";
        IsAnalyzing = true;

        try
        {
            var result = await _watchService.ScanOnceAsync(scanAll);

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

                // Geçmiş listesini yenile
                await RefreshHistoryAsync();
            }
            else
            {
                StatusText = scanAll
                    ? "Hiçbir dump dosyasında yeni hata bulunamadı."
                    : "Yeni bir BSOD bulunamadı.";
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
    [NotifyPropertyChangedFor(nameof(HasRelatedKbUrls))]
    private string _relatedKbUrls = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDumpFilePath))]
    private string _dumpFilePath = string.Empty;

    [ObservableProperty]
    private int _severity;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isResolved;

    // Tema
    [ObservableProperty]
    private bool _isDarkTheme;

    // Geçmiş listesi
    public ObservableCollection<HistoryItem> HistoryItems { get; } = new();

    [ObservableProperty]
    private HistoryItem? _selectedHistoryItem;

    // Computed property'ler — Visibility binding için
    public bool HasRelatedKbUrls => !string.IsNullOrEmpty(RelatedKbUrls);
    public bool HasDumpFilePath => !string.IsNullOrEmpty(DumpFilePath);

    // ---- Commands ----

    /// <summary>
    /// Taramayı manuel olarak yeniden başlatır (tüm dump'ları tara).
    /// </summary>
    [RelayCommand]
    private async Task ScanNowAsync()
    {
        if (IsAnalyzing) return;

        // Önce ekranı temizle
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

        await StartWatchScanAsync(scanAll: true);
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

            // Geçmiş listesini yenile (çözüldü işaretlenen kayıt artık listeden kaybolur)
            await RefreshHistoryAsync();
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

    /// <summary>
    /// Geçmiş listesini manuel yeniler.
    /// </summary>
    [RelayCommand]
    private async Task RefreshHistoryListAsync()
    {
        await RefreshHistoryAsync();
        StatusText = "Geçmiş yenilendi.";
    }

    /// <summary>
    /// Geçmiş listesinden bir kayda tıklandığında detayları yükler.
    /// </summary>
    [RelayCommand]
    private async Task ShowHistoryDetailAsync()
    {
        if (SelectedHistoryItem == null) return;

        var item = SelectedHistoryItem;

        // Detay alanlarını doldur
        ErrorCode = item.ErrorCode;
        ErrorName = item.ErrorName;
        DumpFilePath = item.DumpFilePath;
        HasResult = true;
        IsResolved = item.IsResolved;
        _currentHistoryId = item.Id;

        // BSOD çözüm bilgilerini veritabanından getir
        try
        {
            var bsodError = await _databaseService.FindErrorByCodeAsync(item.ErrorCode);
            Description = bsodError?.Description ?? "Bu hata kodu için kayıtlı çözüm bulunamadı.";
            SolutionSteps = bsodError?.SolutionSteps ?? string.Empty;
            KesinCozum = bsodError?.KesinCozum ?? string.Empty;
            CommonCauses = bsodError?.CommonCauses ?? string.Empty;
            RelatedKbUrls = bsodError?.RelatedKbUrls ?? string.Empty;
            Severity = bsodError?.Severity ?? 0;
        }
        catch
        {
            Description = "Çözüm bilgisi alınamadı.";
            SolutionSteps = string.Empty;
            KesinCozum = string.Empty;
            CommonCauses = string.Empty;
            RelatedKbUrls = string.Empty;
            Severity = 0;
        }

        StatusText = $"{item.ErrorName} — {item.DisplayTime}";
    }

    /// <summary>
    /// Açık / Koyu tema arasında geçiş yapar.
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;

        // App.xaml'deki MergedDictionary'i değiştir
        var appResources = Application.Current.Resources.MergedDictionaries;
        appResources.Clear();

        var themeName = IsDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";
        var uri = new Uri($"Resources/Themes/{themeName}", UriKind.Relative);
        appResources.Add(new ResourceDictionary { Source = uri });
    }
}
