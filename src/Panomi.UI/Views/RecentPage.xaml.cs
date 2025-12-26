using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Panomi.Core.Models;
using Panomi.Core.Services;

namespace Panomi.UI.Views;

public sealed partial class RecentPage : Page
{
    public ObservableCollection<RecentGameViewModel> RecentGames { get; } = new();
    
    private readonly IGameService _gameService;

    public RecentPage()
    {
        this.InitializeComponent();
        
        _gameService = App.GetService<IGameService>();
        
        LoadRecentGames();
    }

    private async void LoadRecentGames()
    {
        RecentGames.Clear();
        
        var games = await _gameService.GetRecentGamesAsync(20);
        foreach (var game in games)
        {
            RecentGames.Add(new RecentGameViewModel(game));
        }
        
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = RecentGames.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentList.Visibility = RecentGames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentGameViewModel game)
        {
            await _gameService.TryLaunchGameAsync(game.Id);
        }
    }
}

public class RecentGameViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string LauncherName { get; }
    public string? IconPath { get; }
    public DateTime? LastPlayed { get; }
    public string LastPlayedText => FormatLastPlayed(LastPlayed);

    public RecentGameViewModel(Game game)
    {
        Id = game.Id;
        Name = game.Name;
        LauncherName = game.Launcher?.Name ?? "Unknown";
        IconPath = game.IconPath;
        LastPlayed = game.LastPlayed;
    }

    private static string FormatLastPlayed(DateTime? lastPlayed)
    {
        if (lastPlayed == null) return "Never";
        
        var diff = DateTime.Now - lastPlayed.Value;
        
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        
        return lastPlayed.Value.ToString("MMM d, yyyy");
    }
}
