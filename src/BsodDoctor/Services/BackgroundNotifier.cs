using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

#if REAL_WINDOWS
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif

namespace BsodDoctor.Services;

/// <summary>
/// WPF uygulamasının --notify modunda kullandığı bildirim yöneticisi.
/// Pending notification marker'larını okur, Windows Toast Notification gösterir.
///
/// NOT: Toast notification API'leri (WinRT) sadece Windows build'lerinde kullanılabilir.
/// Linux'ta build edilirken bu kod conditionally compile edilir.
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
    /// Windows Toast Notification gösterir.
    /// Sadece Windows build'lerinde derlenir.
    /// </summary>
    private static void ShowToast(PendingNotification notification)
    {
        var toastXml = $"""
            <toast activationType="foreground" launch="--open-error={notification.ErrorCode}">
                <visual>
                    <binding template="ToastGeneric">
                        <text>🚨 Yeni BSOD Tespit Edildi</text>
                        <text>{notification.ErrorCode} — {notification.ErrorName}</text>
                        <text>Ciddiyet: {notification.Severity}/5</text>
                    </binding>
                </visual>
                <actions>
                    <action content="🔍 BSOD Doctor'u Aç" arguments="--open-error={notification.ErrorCode}" activationType="foreground"/>
                </actions>
            </toast>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(toastXml);

        var toast = new ToastNotification(doc);
        var notifier = ToastNotificationManager.CreateToastNotifier(AUMID);
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
/// NOT: COM activator sadece Windows'ta çalışır. Linux build'lerinde
/// COMVisible attribute'ları ve CLSID kaydı yok sayılır.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("B5E7F3A1-2C4D-4A8F-9E6B-1D3C5F7A9B0E")]
public class NotificationActivator : INotificationActivationCallback
{
    public void Activate(
        [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [In, MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] NOTIFICATION_USER_INPUT_DATA[]? data,
        [In, MarshalAs(UnmanagedType.U4)] uint dataCount)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "BsodDoctor.exe";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = invokedArgs ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationActivator] Başlatma hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// INotificationActivationCallback COM arabirimi — Windows Shell API'si.
/// Desktop apps için toast tıklama aktivasyonunu yönetir.
/// </summary>
[ComImport]
[Guid("25A45B09-1AD4-40B6-83A3-6A149F4FE0E7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INotificationActivationCallback
{
    void Activate(
        [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [In, MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] NOTIFICATION_USER_INPUT_DATA[]? data,
        [In, MarshalAs(UnmanagedType.U4)] uint dataCount);
}

/// <summary>
/// Toast notification'dan gelen kullanıcı girdi verisi yapısı.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct NOTIFICATION_USER_INPUT_DATA
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Key;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string Value;
}

#endregion

/// <summary>
/// Bildirim marker'ı JSON modeli — DumpScannerService tarafından yazılır,
/// BackgroundNotifier tarafından okunur.
/// </summary>
internal class PendingNotification
{
    public int HistoryId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorName { get; init; } = string.Empty;
    public int Severity { get; init; }
    public DateTime Timestamp { get; init; }
}
