using Panomi.Core.Models;

namespace Panomi.Core.Services;

/// <summary>
/// Service for extracting and managing icons from installed executables
/// </summary>
public interface IIconService
{
    /// <summary>
    /// Get the icons cache folder path
    /// </summary>
    string IconsFolderPath { get; }
    
    /// <summary>
    /// Get the cached icon path for a game (returns null if not cached)
    /// </summary>
    string? GetIconPath(string gameName);
    
    /// <summary>
    /// Get the cached icon path for a game by ID (returns null if not cached)
    /// </summary>
    string? GetIconPath(int gameId);
    
    /// <summary>
    /// Check if an icon is cached for a game
    /// </summary>
    bool HasIcon(string gameName);
    
    /// <summary>
    /// Ensure the icons cache folder exists
    /// </summary>
    void EnsureIconsFolderExists();
    
    /// <summary>
    /// Extract and cache icon from an executable file
    /// Returns the cached icon path, or null if extraction failed
    /// </summary>
    string? ExtractAndCacheIcon(string executablePath, LauncherType launcherType, string externalId);
    
    /// <summary>
    /// Extract and cache launcher icon from its executable
    /// Returns the cached icon path, or null if extraction failed
    /// </summary>
    string? ExtractLauncherIcon(string launcherExecutablePath, LauncherType launcherType);
    
    /// <summary>
    /// Get the cached icon path for a launcher
    /// </summary>
    string? GetLauncherIconPath(LauncherType launcherType);
    
    /// <summary>
    /// Search for .ico files in game install folder as fallback
    /// </summary>
    string? ExtractIconFromFolder(string installPath, LauncherType launcherType, string externalId);
}
