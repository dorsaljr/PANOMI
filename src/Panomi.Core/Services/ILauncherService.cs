using Panomi.Core.Models;

namespace Panomi.Core.Services;

/// <summary>
/// Service for managing launchers
/// </summary>
public interface ILauncherService
{
    /// <summary>
    /// Get all known launchers
    /// </summary>
    Task<IEnumerable<Launcher>> GetAllLaunchersAsync();
    
    /// <summary>
    /// Get a launcher by ID
    /// </summary>
    Task<Launcher?> GetLauncherByIdAsync(int id);
    
    /// <summary>
    /// Get a launcher by type
    /// </summary>
    Task<Launcher?> GetLauncherByTypeAsync(LauncherType type);
    
    /// <summary>
    /// Scan a specific launcher for games
    /// </summary>
    Task ScanLauncherAsync(int launcherId);
    
    /// <summary>
    /// Scan a launcher by type
    /// </summary>
    Task ScanLauncherAsync(LauncherType type);
    
    /// <summary>
    /// Detect all installed launchers
    /// </summary>
    Task DetectInstalledLaunchersAsync();
}
