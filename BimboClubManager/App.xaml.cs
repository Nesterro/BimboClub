using System.Configuration;
using System.Data;
using System.Windows;

namespace BimboClubManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = new MainWindow { DataContext = new ViewModels.MainViewModel() };
        MainWindow.Show();
    }
}

