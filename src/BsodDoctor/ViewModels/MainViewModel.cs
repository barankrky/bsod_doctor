using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BsodDoctor.Models;
using BsodDoctor.Services;

namespace BsodDoctor.ViewModels;

/// <summary>
/// Ana pencere için ViewModel.
/// Servis bağımlılıkları constructor üzerinden enjekte edilir (manual DI).
/// Asenkron başlatma <see cref="Initialization"/> Task'i üzerinden yönetilir.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IBsodWatchService _watchService;
    private readonly string _settingsPath;

    private int _currentHistoryId;

    private record AppSettings
    {
        public bool IsDarkTheme { get; init; }
    }

    public MainViewModel(IDatabaseService databaseService, IBsodWatchService watchService, string settingsPath)
    {
        _databaseService = databaseService;
        _watchService = watchService;
        _settingsPath = settingsPath;

        // Versiyon bilgisini assembly'den al
        VersionText = Assembly.GetExecutingAssembly().GetName()?.Version?.ToString(3) ?? "1.0.0";

        // Asenkron başlatma — hatalar Task içinde yakalanır, fire-and-forget yok
        Initialization = InitializeAsync();
    }

    /// <summary>
    /// ViewModel'in asenkron başlatma işlemini temsil eden Task.
    /// Consumer'lar bu Task'i await ederek başlatmanın tamamlanmasını bekleyebilir.
    /// </summary>
    public Task Initialization { get; }

    private async Task InitializeAsync()
    {
        // Kayıtlı tema tercihini yükle ve uygula
        LoadAndApplyTheme();

        try
        {
            var seedPath = ResolveSeedPath(AppDomain.CurrentDomain.BaseDirectory);
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
            Debug.WriteLine($"[BSOD Doctor] InitializeAsync hatası: {ex}");
        }
    }

    /// <summary>
    /// Seed data JSON dosyasının yolunu bulur.
    /// Öncelik sırası: publish çıktısı → repo kökü.
    /// </summary>
    private static string ResolveSeedPath(string baseDir)
    {
        // 1) Published/build output: {baseDir}/database/seed_data.json
        var published = Path.Combine(baseDir, "database", "seed_data.json");
        if (File.Exists(published))
            return published;

        // 2) Repo kökü: baseDir'den yukarı çık ve database/seed_data.json ara
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "database", "seed_data.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return published; // hiçbiri yoksa — InitializeAsync içindeki catch yakalar
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
        catch (Exception ex)
        {
            // Geçmiş yüklenemezse sessizce geç — kullanıcı butonla tekrar deneyebilir
            Debug.WriteLine($"[BSOD Doctor] Geçmiş yüklenemedi: {ex.Message}");
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
            Debug.WriteLine($"[BSOD Doctor] Tarama hatası: {ex}");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    /// <summary>
    /// Tüm sonuç property'lerini varsayılana döndürür.
    /// </summary>
    private void ResetResultProperties()
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

    // Versiyon
    [ObservableProperty]
    private string _versionText = string.Empty;

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

        ResetResultProperties();
        await StartWatchScanAsync(scanAll: true);
    }

    /// <summary>
    /// Bulunan hatayı "çözüldü" olarak işaretler ve dump dosyasını siler.
    /// </summary>
    [RelayCommand]
    private async Task MarkResolvedAsync()
    {
        if (_currentHistoryId <= 0) return;

        try
        {
            // Önce dump dosyasını sil (tekrar karşına çıkmaması için)
            DeleteDumpFile();

            await _databaseService.MarkAsResolvedAsync(_currentHistoryId, "Kullanıcı tarafından çözüldü olarak işaretlendi ve dump dosyası silindi.");
            IsResolved = true;
            StatusText = "Hata çözüldü olarak işaretlendi ve dump dosyası silindi.";

            // Geçmiş listesini yenile (çözüldü işaretlenen kayıt artık listeden kaybolur)
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"İşaretleme hatası: {ex.Message}";
            Debug.WriteLine($"[BSOD Doctor] Çözüm işaretleme hatası: {ex}");
        }
    }

    /// <summary>
    /// Mevcut hata için dump dosyasını siler (tekrar taranmaması için).
    /// </summary>
    private void DeleteDumpFile()
    {
        if (string.IsNullOrEmpty(DumpFilePath)) return;

        try
        {
            if (File.Exists(DumpFilePath))
            {
                File.Delete(DumpFilePath);
                Debug.WriteLine($"[BSOD Doctor] Dump dosyası silindi: {DumpFilePath}");
            }
        }
        catch (Exception ex)
        {
            // Dosya silinemezse (izin yok, kilitli vs.) uygulama devam etsin
            Debug.WriteLine($"[BSOD Doctor] Dump dosyası silinemedi: {ex.Message}");
        }
    }

    /// <summary>
    /// Sonucu temizler.
    /// </summary>
    [RelayCommand]
    private void ClearResult()
    {
        ResetResultProperties();
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[BSOD Doctor] Çözüm bilgisi alınamadı: {ex.Message}");
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
    /// Açık / Koyu tema arasında geçiş yapar ve tercihi kaydeder.
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
        SaveSettings();
    }

    /// <summary>
    /// Kayıtlı tema tercihini settings.json'dan okur ve uygular.
    /// </summary>
    private void LoadAndApplyTheme()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings?.IsDarkTheme == true)
            {
                IsDarkTheme = true;
                ApplyTheme();
            }
        }
        catch (Exception ex)
        {
            // Bozuk settings dosyası — sessizce geç, varsayılan tema kullanılsın
            Debug.WriteLine($"[BSOD Doctor] Tema ayarı okunamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// Tema ResourceDictionary'ini değiştirir.
    /// </summary>
    private void ApplyTheme()
    {
        var themeName = IsDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";
        var appResources = Application.Current.Resources.MergedDictionaries;
        appResources.Clear();
        var uri = new Uri($"Resources/Themes/{themeName}", UriKind.Relative);
        appResources.Add(new ResourceDictionary { Source = uri });
    }

    /// <summary>
    /// Tema tercihini settings.json'a kaydeder.
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(new AppSettings { IsDarkTheme = IsDarkTheme });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            // Kayıt başarısız — sorun değil, tema bu oturumda çalışır
            Debug.WriteLine($"[BSOD Doctor] Tema kaydedilemedi: {ex.Message}");
        }
    }
}
