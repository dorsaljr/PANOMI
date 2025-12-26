using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using Panomi.Core.Models;
using Panomi.Core.Services;

namespace Panomi.UI.Views;

public sealed partial class LaunchersPage : Page
{
    public ObservableCollection<LauncherViewModel> Launchers { get; } = new();
    
    private readonly ILauncherService _launcherService;

    public LaunchersPage()
    {
        this.InitializeComponent();
        
        _launcherService = App.GetService<ILauncherService>();
        
        LoadLaunchers();
    }

    private async void LoadLaunchers()
    {
        Launchers.Clear();
        
        var launchers = await _launcherService.GetAllLaunchersAsync();
        foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual))
        {
            Launchers.Add(new LauncherViewModel(launcher));
        }
    }

    private async void ScanLauncher_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int launcherId)
        {
            button.IsEnabled = false;
            
            try
            {
                await _launcherService.ScanLauncherAsync(launcherId);
                LoadLaunchers();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }
}

public class LauncherViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public int GameCount { get; set; }
    public string StatusText => IsInstalled ? "Installed" : "Not detected";
    public SolidColorBrush StatusColor => IsInstalled 
        ? new SolidColorBrush(Microsoft.UI.Colors.Green)
        : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    public LauncherViewModel() { }

    public LauncherViewModel(Launcher launcher)
    {
        Id = launcher.Id;
        Name = launcher.Name;
        IsInstalled = launcher.IsInstalled;
        GameCount = launcher.Games?.Count ?? 0;
    }
}
