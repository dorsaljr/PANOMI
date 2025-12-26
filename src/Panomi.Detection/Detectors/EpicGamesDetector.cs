using Panomi.Core.Models;
using System.Text.Json;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Epic Games Store games using Registry and JSON manifests
/// </summary>
public class EpicGamesDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.EpicGames;
    public override string LauncherName => "Epic Games";

    private const string EpicRegistryKey = @"SOFTWARE\Epic Games\EpicGamesLauncher";
    private const string EpicRegistryKey32 = @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher";

    public override Task<bool> IsInstalledAsync()
    {
        // Check if manifests folder exists - this is more reliable
        var manifestsPath = GetManifestsPath();
        if (!string.IsNullOrEmpty(manifestsPath) && Directory.Exists(manifestsPath))
            return Task.FromResult(true);
            
        var installPath = GetEpicPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetEpicPath());
    }

    public override async Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();

        // Find the manifests directory first
        var manifestsPath = GetManifestsPath();
        if (string.IsNullOrEmpty(manifestsPath) || !Directory.Exists(manifestsPath))
        {
            result.ErrorMessage = "Epic Games manifests folder not found";
            return result;
        }

        result.IsInstalled = true;
        result.InstallPath = GetEpicPath();

        // Parse all .item manifest files
        var manifestFiles = FindFiles(manifestsPath, "*.item");
        foreach (var manifestFile in manifestFiles)
        {
            var game = await ParseManifestAsync(manifestFile);
            if (game != null)
            {
                result.Games.Add(game);
            }
        }

        return result;
    }

    private string? GetEpicPath()
    {
        // Try common installation paths first
        // Note: Return "Epic Games" folder - IconExtractor adds Launcher subfolder
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
        };

        foreach (var commonPath in commonPaths)
        {
            if (Directory.Exists(commonPath))
                return commonPath;
        }

        return null;
    }

    private string? GetManifestsPath()
    {
        // Manifests are stored in ProgramData
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var manifestsPath = Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        
        if (Directory.Exists(manifestsPath))
            return manifestsPath;

        return null;
    }

    private async Task<DetectedGame?> ParseManifestAsync(string manifestPath)
    {
        var content = await ReadFileAsync(manifestPath);
        if (string.IsNullOrEmpty(content))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Get game info from manifest
            var displayName = GetJsonString(root, "DisplayName");
            var installLocation = GetJsonString(root, "InstallLocation");
            var catalogItemId = GetJsonString(root, "CatalogItemId");
            var appName = GetJsonString(root, "AppName");
            var launchExecutable = GetJsonString(root, "LaunchExecutable");

            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(appName))
                return null;

            // Skip DLCs and add-ons
            if (IsDlcOrAddon(root))
                return null;

            // Only include games that are actually installed with executable files
            if (!IsValidGameInstall(installLocation))
                return null;

            var executablePath = !string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExecutable)
                ? Path.Combine(installLocation, launchExecutable)
                : null;

            return new DetectedGame
            {
                Name = displayName,
                ExternalId = appName,
                InstallPath = installLocation,
                ExecutablePath = executablePath,
                LaunchCommand = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true"
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }
        return null;
    }

    private static bool IsDlcOrAddon(JsonElement root)
    {
        // Check if this is marked as a DLC
        if (root.TryGetProperty("bIsDLC", out var isDlc) && isDlc.ValueKind == JsonValueKind.True)
            return true;

        // Check main game catalog namespace vs this item
        var mainGameCatalogNamespace = GetJsonString(root, "MainGameCatalogNamespace");
        var catalogNamespace = GetJsonString(root, "CatalogNamespace");
        
        if (!string.IsNullOrEmpty(mainGameCatalogNamespace) && 
            !string.IsNullOrEmpty(catalogNamespace) &&
            mainGameCatalogNamespace != catalogNamespace)
        {
            return true;
        }

        return false;
    }
}
