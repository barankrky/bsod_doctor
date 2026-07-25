using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BsodDoctor.Services;
using BsodDoctor.ViewModels;

namespace BsodDoctor;

/// <summary>
/// Uygulama giriş noktası. Dependency injection ve servis başlatma işlemlerini yönetir.
/// Komut satırı argümanları:
///   --notify              : Arka planda bildirimleri kontrol et ve göster, çık.
///   --open-error=KOD      : Belirtilen hata kodunu doğrudan yükle.
///   --install-shortcut    : AUMID kısayolunu oluştur (setup tarafından çağrılır).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --install-shortcut: AUMID kısayolunu oluştur, çık
        if (e.Args.Contains("--install-shortcut"))
        {
            InstallAumidShortcut();
            Shutdown();
            return;
        }

        // --notify: bildirimleri kontrol et, toast göster, çık
        if (e.Args.Contains("--notify"))
        {
            var notifier = new BackgroundNotifier();
            notifier.ShowPendingNotifications();
            Shutdown();
            return;
        }

        // --open-error=KOD: normal başlat ama belirtilen hatayı yükle
        var openErrorCode = e.Args
            .FirstOrDefault(a => a.StartsWith("--open-error="))
            ?.Split('=', 2)[1];

        // Normal başlatma
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "bsod_errors.db");
        var settingsPath = Path.Combine(dataDir, "settings.json");

        IDatabaseService databaseService = new DatabaseService(dbPath);
        IBsodWatchService watchService = new BsodWatchService(databaseService, TimeSpan.FromDays(1));

        var viewModel = new MainViewModel(databaseService, watchService, settingsPath);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();

        // İstenen hatayı yükle (ViewModel başlatıldıktan sonra)
        if (!string.IsNullOrEmpty(openErrorCode))
        {
            viewModel.Initialization.ContinueWith(async _ =>
            {
                await viewModel.LoadErrorByCodeAsync(openErrorCode);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    /// <summary>
    /// Toast notification'ların çalışması için gerekli AUMID kısayolunu oluşturur.
    /// Setup sırasında veya --install-shortcut argümanı ile çağrılır.
    /// </summary>
    private static void InstallAumidShortcut()
    {
        var appExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(appExePath))
            return;

        try
        {
            // COM kütüphanesini kullanarak .lnk oluştur
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "BSOD Doctor", "BSOD Doctor.lnk");

            var directory = Path.GetDirectoryName(shortcutPath);
            if (directory != null) Directory.CreateDirectory(directory);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                Debug.WriteLine("[App] WScript.Shell bulunamadı.");
                return;
            }

            var shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                Debug.WriteLine("[App] WScript.Shell oluşturulamadı.");
                return;
            }

            var shortcut = shellType.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });

            if (shortcut != null)
            {
                shellType.InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { appExePath });
                shellType.InvokeMember("WorkingDirectory",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut,
                    new object[] { Path.GetDirectoryName(appExePath) ?? string.Empty });
                shellType.InvokeMember("Arguments",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "--notify" });
                shellType.InvokeMember("AppUserModelID",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { BackgroundNotifier.AUMID });
                shellType.InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }

            Debug.WriteLine($"[App] AUMID kısayolu oluşturuldu: {shortcutPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Kısayol oluşturulamadı: {ex.Message}");
        }
    }
}
