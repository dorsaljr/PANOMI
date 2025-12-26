using Panomi.Core.Models;

namespace Panomi.Core.Services;

/// <summary>
/// Service for managing games in the library
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Launch a game and return result with error details if failed
    /// </summary>
    Task<LaunchResult> TryLaunchGameAsync(int gameId);
    /// <summary>
    /// Get all games in the library
    /// </summary>
    Task<IEnumerable<Game>> GetAllGamesAsync();
    
    /// <summary>
    /// Get games filtered by launcher
    /// </summary>
    Task<IEnumerable<Game>> GetGamesByLauncherAsync(int launcherId);
    
    /// <summary>
    /// Get recently played games
    /// </summary>
    Task<IEnumerable<Game>> GetRecentGamesAsync(int count = 10);
    
    /// <summary>
    /// Search games by name
    /// </summary>
    Task<IEnumerable<Game>> SearchGamesAsync(string searchText);
    
    /// <summary>
    /// Get a specific game by ID
    /// </summary>
    Task<Game?> GetGameByIdAsync(int id);
    
    /// <summary>
    /// Add a game manually
    /// </summary>
    Task<Game> AddGameAsync(Game game);
    
    /// <summary>
    /// Update a game
    /// </summary>
    Task UpdateGameAsync(Game game);
    
    /// <summary>
    /// Delete a game
    /// </summary>
    Task DeleteGameAsync(int id);
    
    /// <summary>
    /// Launch a game (calls TryLaunchGameAsync internally)
    /// </summary>
    Task LaunchGameAsync(int gameId);
    
    /// <summary>
    /// Scan all installed launchers for games
    /// </summary>
    Task ScanAllLaunchersAsync();
}
