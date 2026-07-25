using System.IO;
using System.Windows;
using BsodDoctor.Services;
using BsodDoctor.ViewModels;

namespace BsodDoctor;

/// <summary>
/// Uygulama giriş noktası. Dependency injection ve servis başlatma işlemlerini yönetir.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Veri dizinini oluştur (settings.json ve bsod_errors.db burada)
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "bsod_errors.db");
        var settingsPath = Path.Combine(dataDir, "settings.json");

        // Servisleri oluştur (manuel DI — proje boyutu için yeterli)
        IDatabaseService databaseService = new DatabaseService(dbPath);
        IBsodWatchService watchService = new BsodWatchService(databaseService, TimeSpan.FromDays(1));

        var viewModel = new MainViewModel(databaseService, watchService, settingsPath);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();
    }
}
