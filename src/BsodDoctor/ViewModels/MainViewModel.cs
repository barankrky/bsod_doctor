using System.Collections.ObjectModel;
using BsodDoctor.Models;
using BsodDoctor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BsodDoctor.ViewModels;

/// <summary>
/// Ana pencere ViewModel'i. MVVM pattern ile CommunityToolkit.Mvvm kullanır.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IDumpAnalyzer _dumpAnalyzer;
    private readonly EventLogReader _eventLogReader;

    public MainViewModel(
        IDatabaseService databaseService,
        IDumpAnalyzer dumpAnalyzer,
        EventLogReader eventLogReader)
    {
        _databaseService = databaseService;
        _dumpAnalyzer = dumpAnalyzer;
        _eventLogReader = eventLogReader;
    }

    // ===== Observable Properties =====

    [ObservableProperty]
    private ObservableCollection<BsodError> _errorList = [];

    [ObservableProperty]
    private BsodError? _selectedError;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Hazır";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<AnalysisResult> _analysisHistory = [];

    // ===== Commands =====

    /// <summary>İlk yüklemede tüm hata kodlarını getir</summary>
    [RelayCommand]
    private async Task LoadErrorsAsync()
    {
        IsBusy = true;
        StatusMessage = "Veritabanı yükleniyor...";

        try
        {
            var errors = await _databaseService.GetAllErrorsAsync();
            ErrorList = new ObservableCollection<BsodError>(errors);
            StatusMessage = $"{errors.Count} hata kodu yüklendi";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Hata kodunda veya adında arama yap</summary>
    [RelayCommand]
    private async Task SearchErrorsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadErrorsAsync();
            return;
        }

        IsBusy = true;
        StatusMessage = "Aranıyor...";

        try
        {
            var results = await _databaseService.SearchErrorsAsync(SearchQuery);
            ErrorList = new ObservableCollection<BsodError>(results);
            StatusMessage = $"{results.Count} sonuç bulundu";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Arama hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Dump dosyalarını tara ve analiz et</summary>
    [RelayCommand]
    private async Task ScanDumpsAsync()
    {
        IsBusy = true;
        StatusMessage = "Dump dosyaları taranıyor...";

        try
        {
            var dumpFiles = await _dumpAnalyzer.FindDumpFilesAsync();
            StatusMessage = $"{dumpFiles.Count} dump dosyası bulundu";

            foreach (var dumpFile in dumpFiles)
            {
                var result = await _dumpAnalyzer.AnalyzeDumpAsync(dumpFile);

                // Varsa çözümü veritabanından getir
                if (result.ErrorCode != null)
                {
                    var errorDetails = await _databaseService.GetErrorByCodeAsync(result.ErrorCode);
                    if (errorDetails != null)
                    {
                        result.ErrorDetails = errorDetails;
                        result.ErrorName = errorDetails.ErrorName;
                    }
                }

                await _databaseService.SaveAnalysisAsync(result);
            }

            // Analiz geçmişini yenile
            await LoadHistoryAsync();
            StatusMessage = "Tarama tamamlandı";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Tarama hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Event Log'dan BSOD kayıtlarını oku</summary>
    [RelayCommand]
    private async Task ReadEventLogAsync()
    {
        IsBusy = true;
        StatusMessage = "Event Log okunuyor...";

        try
        {
            var recentEvents = _eventLogReader.GetRecentBsodSummary(15);
            StatusMessage = $"{recentEvents.Count} BSOD olayı bulundu";

            foreach (var (time, code, message) in recentEvents)
            {
                if (code != null)
                {
                    var errorDetails = await _databaseService.GetErrorByCodeAsync(code);
                    var result = new AnalysisResult
                    {
                        Timestamp = time,
                        ErrorCode = code,
                        ErrorName = errorDetails?.ErrorName ?? code,
                        ErrorDetails = errorDetails,
                    };
                    await _databaseService.SaveAnalysisAsync(result);
                }
            }

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Event Log hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Analiz geçmişini yükle</summary>
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        try
        {
            var history = await _databaseService.GetAnalysisHistoryAsync(20);
            AnalysisHistory = new ObservableCollection<AnalysisResult>(history);
        }
        catch
        {
            // Geçmiş yüklenemezse sessizce geç
        }
    }

    /// <summary>Seçili hatayı çözüldü olarak işaretle</summary>
    [RelayCommand]
    private async Task MarkAsResolvedAsync()
    {
        if (SelectedError == null) return;

        IsBusy = true;

        try
        {
            var history = AnalysisHistory.FirstOrDefault(h =>
                h.ErrorCode == SelectedError.ErrorCode && !h.Resolved);

            if (history != null)
            {
                history.Resolved = true;
                await _databaseService.SaveAnalysisAsync(history);
                await LoadHistoryAsync();
                StatusMessage = "Hata çözüldü olarak işaretlendi";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
