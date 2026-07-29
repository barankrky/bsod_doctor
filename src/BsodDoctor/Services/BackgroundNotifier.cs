using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using BsodDoctor.Models;

#if REAL_WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace BsodDoctor.Services;

/// <summary>
/// WPF uygulamasının --notify modunda kullandığı bildirim yöneticisi.
/// Pending notification marker'larını okur, Windows Toast Notification gösterir.
///
/// NOT: Toast notification API'leri (DesktopNotificationManagerCompat) sadece
/// Windows build'lerinde kullanılabilir. Linux'ta build edilirken bu kod
/// #if REAL_WINDOWS ile korunur ve stub derlenir.
/// </summary>
public class BackgroundNotifier
{
    /// <summary>
    /// AppUserModelID — Windows toast notification'ları için benzersiz tanımlayıcı.
    /// Setup sırasında Start Menu'de kısayol oluşturulurken bu ID kullanılır.
    /// </summary>
    public const string AUMID = "NextroByte.BsodDoctor";

    private static readonly string PendingDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "BsodDoctor");

    /// <summary>
    /// Bekleyen bildirimleri kontrol eder ve toast notification gösterir.
    /// </summary>
    public void ShowPendingNotifications()
    {
        try
        {
            if (!Directory.Exists(PendingDir))
            {
                Debug.WriteLine("[BackgroundNotifier] Bildirim dizini bulunamadı.");
                return;
            }

            var pendingFiles = Directory.GetFiles(PendingDir, "pending_*.json");
            if (pendingFiles.Length == 0)
            {
                Debug.WriteLine("[BackgroundNotifier] Bekleyen bildirim yok.");
                return;
            }

            Debug.WriteLine($"[BackgroundNotifier] {pendingFiles.Length} bekleyen bildirim bulundu.");

#if REAL_WINDOWS
            // DesktopNotificationManagerCompat'i başlat (AUMID kayıtlı olmalı)
            DesktopNotificationManagerCompat.RegisterAumidAndComServer<NotificationActivator>(AUMID);
            DesktopNotificationManagerCompat.RegisterActivator<NotificationActivator>();
#endif

            foreach (var file in pendingFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var notification = JsonSerializer.Deserialize<PendingNotification>(json);
                    if (notification == null)
                    {
                        File.Delete(file);
                        continue;
                    }

                    ShowToast(notification);

                    // Marker dosyasını sil
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BackgroundNotifier] Bildirim hatası: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BackgroundNotifier] Başlatma hatası: {ex.Message}");
        }
    }

#if REAL_WINDOWS
    /// <summary>
    /// Windows Toast Notification gösterir (DesktopNotificationManagerCompat ile).
    /// Sadece Windows build'lerinde derlenir.
    /// </summary>
    private static void ShowToast(PendingNotification notification)
    {
        var toast = new ToastContentBuilder()
            .AddText("🚨 Yeni BSOD Tespit Edildi")
            .AddText($"{notification.ErrorCode} — {notification.ErrorName}")
            .AddText($"Ciddiyet: {notification.Severity}/5")
            .AddArgument("action", "openError")
            .AddArgument("errorCode", notification.ErrorCode)
            .GetToastContent();

        var notifier = DesktopNotificationManagerCompat.CreateToastNotifier();
        notifier.Show(toast);

        Debug.WriteLine($"[BackgroundNotifier] Bildirim gösterildi: {notification.ErrorCode}");
    }
#else
    /// <summary>
    /// Linux/macOS build'lerinde toast notification gösterilemez.
    /// Debug log'a yazılır.
    /// </summary>
    private static void ShowToast(PendingNotification notification)
    {
        Debug.WriteLine($"[BackgroundNotifier] (Linux) Bildirim atlandı: {notification.ErrorCode} — {notification.ErrorName}");
    }
#endif
}

#region COM Notification Activator

/// <summary>
/// Toast notification tıklandığında Windows tarafından çağrılan COM aktivator.
/// Setup sırasında CLSID registry'ye kaydedilir.
/// Kullanıcı bildirime tıklayınca ana BSOD Doctor uygulamasını başlatır.
///
/// Bu sınıf sadece Windows build'lerinde derlenir.
/// </summary>
#if REAL_WINDOWS
[ClassInterface(ClassInterfaceType.None)]
[ComVisible(true)]
[Guid("B5E7F3A1-2C4D-4A8F-9E6B-1D3C5F7A9B0E")]
public class BsodNotificationActivator : NotificationActivator
{
    public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "BsodDoctor.exe";

        try
        {
            // arguments formatı: "action=openError&errorCode=0x0000001A"
            // App.xaml.cs --open-error=KOD formatını bekliyor, o yüzden parse et
            var launchArgs = string.Empty;

            if (!string.IsNullOrEmpty(arguments))
            {
                var parts = arguments.Split('&')
                    .Select(p => p.Split('=', 2))
                    .Where(kv => kv.Length == 2)
                    .ToDictionary(kv => kv[0], kv => kv[1]);

                if (parts.TryGetValue("errorCode", out var errorCode) && !string.IsNullOrEmpty(errorCode))
                {
                    launchArgs = $"--open-error={errorCode}";
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = launchArgs,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationActivator] Başlatma hatası: {ex.Message}");
        }
    }
}
#endif

#endregion

// PendingNotification modeli artık shared Models/PendingNotification.cs'de tanımlı
