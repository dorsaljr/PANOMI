using Panomi.Core.Interfaces;
using Panomi.Core.Models;

namespace Panomi.Detection;

/// <summary>
/// Factory for creating and managing launcher detectors
/// </summary>
public class LauncherDetectorFactory
{
    private readonly Dictionary<LauncherType, ILauncherDetector> _detectors = new();

    public LauncherDetectorFactory()
    {
        // Register all available detectors
        RegisterDetector(new Detectors.SteamDetector());
        RegisterDetector(new Detectors.EpicGamesDetector());
        RegisterDetector(new Detectors.EAAppDetector());
        RegisterDetector(new Detectors.UbisoftConnectDetector());
        RegisterDetector(new Detectors.GOGGalaxyDetector());
        RegisterDetector(new Detectors.BattleNetDetector());
        RegisterDetector(new Detectors.RockstarDetector());
        RegisterDetector(new Detectors.RiotDetector());
        RegisterDetector(new Detectors.MinecraftDetector());
        RegisterDetector(new Detectors.RobloxDetector());
        // Additional detectors will be registered as implemented
    }

    private void RegisterDetector(ILauncherDetector detector)
    {
        _detectors[detector.LauncherType] = detector;
    }

    /// <summary>
    /// Get a detector for a specific launcher type
    /// </summary>
    public ILauncherDetector? GetDetector(LauncherType type)
    {
        return _detectors.TryGetValue(type, out var detector) ? detector : null;
    }

    /// <summary>
    /// Get all registered detectors
    /// </summary>
    public IEnumerable<ILauncherDetector> GetAllDetectors()
    {
        return _detectors.Values;
    }

    /// <summary>
    /// Detect all installed launchers
    /// </summary>
    public async Task<Dictionary<LauncherType, bool>> DetectAllLaunchersAsync()
    {
        var results = new Dictionary<LauncherType, bool>();
        
        foreach (var detector in _detectors.Values)
        {
            results[detector.LauncherType] = await detector.IsInstalledAsync();
        }
        
        return results;
    }
}
