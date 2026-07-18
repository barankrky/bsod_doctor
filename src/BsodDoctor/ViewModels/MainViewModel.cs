using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BsodDoctor.Services;

namespace BsodDoctor.ViewModels;

/// <summary>
/// Ana pencere için ViewModel.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DumpAnalyzer _dumpAnalyzer;
    private readonly EventLogReader _eventLogReader;
    private readonly DatabaseService _databaseService;
    private readonly A2ABridgeService _a2aBridge;

    public MainViewModel()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "bsod_errors.db");
        _dumpAnalyzer = new DumpAnalyzer();
        _eventLogReader = new EventLogReader();
        _databaseService = new DatabaseService(dbPath);
        _a2aBridge = new A2ABridgeService();

        // Veritabanını başlat
        _ = _databaseService.InitializeAsync();
    }

    [ObservableProperty]
    private string _statusText = "Hazır";

    [ObservableProperty]
    private string _errorCode = string.Empty;

    [ObservableProperty]
    private string _errorName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _solutionSteps = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasResult;

    [RelayCommand]
    private async Task ScanDumpsAsync()
    {
        IsAnalyzing = true;
        StatusText = "Dump dosyaları taranıyor...";

        try
        {
            var dumpDir = @"C:\Windows\Minidump";
            var dumpFiles = await _dumpAnalyzer.FindDumpFilesAsync(dumpDir);

            if (dumpFiles.Count == 0)
            {
                StatusText = "Herhangi bir dump dosyası bulunamadı.";
                return;
            }

            StatusText = $"{dumpFiles.Count} adet dump dosyası bulundu, analiz ediliyor...";

            foreach (var dumpFile in dumpFiles)
            {
                var result = await _dumpAnalyzer.AnalyzeDumpAsync(dumpFile);

                if (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode != "UNKNOWN")
                {
                    // Veritabanında ara
                    var dbResult = await _databaseService.FindErrorByCodeAsync(result.ErrorCode);

                    if (dbResult != null)
                    {
                        ErrorCode = dbResult.ErrorCode;
                        ErrorName = dbResult.ErrorName;
                        Description = dbResult.Description;
                        SolutionSteps = dbResult.SolutionSteps;
                        HasResult = true;
                        StatusText = $"Çözüm bulundu: {dbResult.ErrorName}";
                        return;
                    }

                    // Veritabanında yoksa Vis'e sor
                    StatusText = "Vis'ten çözüm araştırılıyor...";
                    await _a2aBridge.RequestSolutionAsync(result.ErrorCode, result.ErrorName);
                    StatusText = $"'{result.ErrorCode}' için Vis'e sorgu gönderildi.";
                }
            }

            StatusText = "Analiz tamamlandı, ancak çözüm bulunamadı.";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task ScanEventLogAsync()
    {
        IsAnalyzing = true;
        StatusText = "Event log taranıyor...";

        try
        {
            var events = await _eventLogReader.ReadBsodEventsAsync();

            if (events.Count == 0)
            {
                StatusText = "Event log'da BSOD kaydı bulunamadı.";
                return;
            }

            // İlk BSOD kaydını göster
            var firstEvent = events[0];
            ErrorCode = firstEvent.ErrorCode;
            ErrorName = firstEvent.ErrorName;
            Description = firstEvent.Description;
            SolutionSteps = firstEvent.SolutionSteps;
            HasResult = true;

            StatusText = $"Event log'da {events.Count} BSOD kaydı bulundu.";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }
}
