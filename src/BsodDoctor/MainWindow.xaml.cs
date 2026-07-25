using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace BsodDoctor;

/// <summary>
/// MainWindow code-behind. Tüm iş mantığı ViewModel'de (MainViewModel).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
