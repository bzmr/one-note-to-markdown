using System.Windows;

namespace OneNoteMarkdownExporter;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
