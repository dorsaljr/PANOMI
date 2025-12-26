using Panomi.Core.Models;

namespace Panomi.Core.Interfaces;

/// <summary>
/// Interface for detecting installed games from a specific launcher
/// </summary>
public interface ILauncherDetector
{
    /// <summary>
    /// The type of launcher this detector handles
    /// </summary>
    LauncherType LauncherType { get; }
    
    /// <summary>
    /// Display name of the launcher
    /// </summary>
    string LauncherName { get; }
    
    /// <summary>
    /// Check if the launcher is installed on this system
    /// </summary>
    Task<bool> IsInstalledAsync();
    
    /// <summary>
    /// Get the install path of the launcher
    /// </summary>
    Task<string?> GetInstallPathAsync();
    
    /// <summary>
    /// Detect all installed games for this launcher
    /// </summary>
    Task<DetectionResult> DetectGamesAsync();
}
