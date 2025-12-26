using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Roblox Player
/// Treats Roblox as a single entry since it handles all experiences internally.
/// </summary>
public class RobloxDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.Roblox;
    public override string LauncherName => "Roblox";
    
    // Microsoft Store package family name for Roblox
    private const string RobloxStorePackageFamily = "ROBLOXCORPORATION.ROBLOX_55nm5eh3cm0pr";
    private const string RobloxStoreAppId = "ROBLOXCORPORATION.ROBLOX_55nm5eh3cm0pr!App";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetRobloxPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        var path = GetRobloxPath();
        if (!string.IsNullOrEmpty(path))
        {
            // For Store apps, return the shell path as-is
            if (path.StartsWith("shell:"))
                return Task.FromResult<string?>(path);
            return Task.FromResult<string?>(Path.GetDirectoryName(path));
        }
        return Task.FromResult<string?>(null);
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();

        var robloxPath = GetRobloxPath();
        if (string.IsNullOrEmpty(robloxPath))
        {
            result.ErrorMessage = "Roblox not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = robloxPath.StartsWith("shell:") ? robloxPath : Path.GetDirectoryName(robloxPath);

        // No individual games to detect - Roblox handles all experiences internally

        return Task.FromResult(result);
    }

    private string? GetRobloxPath()
    {
        // Method 1: Check for Microsoft Store version
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packagesPath = Path.Combine(localAppData, "Packages", RobloxStorePackageFamily);
        if (Directory.Exists(packagesPath))
        {
            return $"shell:AppsFolder\\{RobloxStoreAppId}";
        }

        // Method 2: Check standalone installation (most common)
        var versionsPath = Path.Combine(localAppData, "Roblox", "Versions");
        if (Directory.Exists(versionsPath))
        {
            // Find the latest version folder with RobloxPlayerBeta.exe
            try
            {
                var versionDirs = Directory.GetDirectories(versionsPath, "version-*")
                    .OrderByDescending(d => Directory.GetLastWriteTime(d));
                
                foreach (var versionDir in versionDirs)
                {
                    var exePath = Path.Combine(versionDir, "RobloxPlayerBeta.exe");
                    if (File.Exists(exePath))
                    {
                        return exePath;
                    }
                }
            }
            catch { }
        }

        // Method 3: Check registry for installation
        var registryPath = ReadUserRegistryValue(
            @"SOFTWARE\ROBLOX Corporation\Environments\roblox-player",
            "version");
        if (!string.IsNullOrEmpty(registryPath))
        {
            var exePath = Path.Combine(localAppData, "Roblox", "Versions", registryPath, "RobloxPlayerBeta.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }
        }

        return null;
    }
}
