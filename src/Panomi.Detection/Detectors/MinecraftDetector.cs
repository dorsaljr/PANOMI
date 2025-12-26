using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Minecraft Launcher (Java Edition, Bedrock, Dungeons, Legends)
/// Treats the launcher as a single entry since it handles all Minecraft games internally.
/// </summary>
public class MinecraftDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.Minecraft;
    public override string LauncherName => "Minecraft";
    
    // Microsoft Store package family name for Minecraft Launcher
    private const string MinecraftLauncherPackageFamily = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";
    private const string MinecraftStoreAppId = "Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetMinecraftLauncherPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        var path = GetMinecraftLauncherPath();
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

        var launcherPath = GetMinecraftLauncherPath();
        if (string.IsNullOrEmpty(launcherPath))
        {
            result.ErrorMessage = "Minecraft Launcher not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = launcherPath.StartsWith("shell:") ? launcherPath : Path.GetDirectoryName(launcherPath);

        // No individual games to detect - the launcher handles everything
        // Users launch the Minecraft Launcher and pick their game/edition from there

        return Task.FromResult(result);
    }

    private string? GetMinecraftLauncherPath()
    {
        // Method 1: Check for Microsoft Store version (most common now)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packagesPath = Path.Combine(localAppData, "Packages", MinecraftLauncherPackageFamily);
        if (Directory.Exists(packagesPath))
        {
            // Return shell protocol for launching Store apps
            return $"shell:AppsFolder\\{MinecraftStoreAppId}";
        }

        // Method 2: Check registry for installed launcher (HKCU)
        var registryPath = ReadUserRegistryValue(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1C16BBA4-21A3-4E2F-B5C3-4E219B752843}_is1",
            "InstallLocation");
        if (!string.IsNullOrEmpty(registryPath))
        {
            var exePath = Path.Combine(registryPath, "MinecraftLauncher.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }
        }

        // Method 3: Check machine registry (HKLM 64-bit)
        var machineRegistryPath = ReadRegistryValue(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1C16BBA4-21A3-4E2F-B5C3-4E219B752843}_is1",
            "InstallLocation");
        if (!string.IsNullOrEmpty(machineRegistryPath))
        {
            var exePath = Path.Combine(machineRegistryPath, "MinecraftLauncher.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }
        }

        // Method 4: Check WOW6432Node registry (32-bit view)
        var wow64RegistryPath = ReadRegistryValue(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1C16BBA4-21A3-4E2F-B5C3-4E219B752843}_is1",
            "InstallLocation",
            use32BitView: true);
        if (!string.IsNullOrEmpty(wow64RegistryPath))
        {
            var exePath = Path.Combine(wow64RegistryPath, "MinecraftLauncher.exe");
            if (File.Exists(exePath))
            {
                return exePath;
            }
        }

        return null;
    }
}
