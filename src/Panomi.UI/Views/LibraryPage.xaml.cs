using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Panomi.Core.Models;
using Panomi.Core.Services;

namespace Panomi.UI.Views;

public sealed partial class LibraryPage : Page
{
    public ObservableCollection<GameViewModel> Games { get; } = new();
    
    private readonly IGameService _gameService;

    public LibraryPage()
    {
        this.InitializeComponent();
        
        _gameService = App.GetService<IGameService>();
        
        LoadGames();
    }

    private async void LoadGames()
    {
        Games.Clear();
        
        var games = await _gameService.GetAllGamesAsync();
        foreach (var game in games)
        {
            Games.Add(new GameViewModel(game));
        }
        
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.Visibility = Games.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            FilterGames(sender.Text);
        }
    }

    private async void FilterGames(string searchText)
    {
        Games.Clear();
        
        IEnumerable<Game> games;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            games = await _gameService.GetAllGamesAsync();
        }
        else
        {
            games = await _gameService.SearchGamesAsync(searchText);
        }
        
        foreach (var game in games)
        {
            Games.Add(new GameViewModel(game));
        }
        
        UpdateEmptyState();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        
        try
        {
            await _gameService.ScanAllLaunchersAsync();
            LoadGames();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Show add game dialog
    }

    private async void GamesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameViewModel game)
        {
            await _gameService.TryLaunchGameAsync(game.Id);
        }
    }
}

public class GameViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string LauncherName { get; }
    public string? IconPath { get; }
    public bool HasIcon => !string.IsNullOrEmpty(IconPath) && System.IO.File.Exists(IconPath);

    public GameViewModel(Game game)
    {
        Id = game.Id;
        Name = game.Name;
        LauncherName = game.Launcher?.Name ?? "Unknown";
        IconPath = game.IconPath;
    }
}
