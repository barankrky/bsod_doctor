using BsodDoctor.Services;
using BsodDoctor.ViewModels;
using System.Windows;

namespace BsodDoctor;

/// <summary>
/// App.xaml code-behind. Dependency Injection yapılandırmasını içerir.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Servisleri oluştur (basit DI — ileride Microsoft.Extensions.DependencyInjection kullanılabilir)
        var databaseService = new DatabaseService();
        var dumpAnalyzer = new DumpAnalyzer();
        var eventLogReader = new EventLogReader();

        // ViewModel'i oluştur ve MainWindow'a ata
        var viewModel = new MainViewModel(databaseService, dumpAnalyzer, eventLogReader);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();

        // İlk yükleme
        _ = viewModel.LoadErrorsCommand.ExecuteAsync(null);
    }
}
