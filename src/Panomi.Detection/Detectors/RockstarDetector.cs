using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Rockstar Games Launcher games
/// </summary>
public class RockstarDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.RockstarGames;
    public override string LauncherName => "Rockstar";

    private const string RockstarRegistryKey = @"SOFTWARE\WOW6432Node\Rockstar Games\Launcher";
    private const string RockstarRegistryKey64 = @"SOFTWARE\Rockstar Games\Launcher";

    // Known Rockstar game title IDs and their display names
    private static readonly Dictionary<string, string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "gta5", "Grand Theft Auto V" },
        { "gta5e", "Grand Theft Auto V Enhanced" },
        { "rdr2", "Red Dead Redemption 2" },
        { "lanoire", "L.A. Noire" },
        { "gta4", "Grand Theft Auto IV" },
        { "gta4e", "Grand Theft Auto IV: Episodes from Liberty City" },
        { "mp3", "Max Payne 3" },
        { "bully", "Bully: Scholarship Edition" },
        { "launcher", "Rockstar Games Launcher" }, // Skip this one
    };

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetRockstarLauncherPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetRockstarLauncherPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();

        var launcherPath = GetRockstarLauncherPath();
        if (string.IsNullOrEmpty(launcherPath))
        {
            result.ErrorMessage = "Rockstar Games Launcher not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = launcherPath;

        // Method 1: Scan registry for installed games
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.ExternalId == game.ExternalId))
                result.Games.Add(game);
        }

        // Method 2: Scan titles folder in launcher directory
        var folderGames = GetGamesFromTitlesFolder(launcherPath);
        foreach (var game in folderGames)
        {
            if (!result.Games.Any(g => g.ExternalId == game.ExternalId))
                result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private string? GetRockstarLauncherPath()
    {
        // Try HKLM registry first
        var path = ReadRegistryValue(RockstarRegistryKey, "InstallFolder");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadRegistryValue(RockstarRegistryKey64, "InstallFolder");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try HKCU registry
        path = ReadUserRegistryValue(RockstarRegistryKey, "InstallFolder");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadUserRegistryValue(RockstarRegistryKey64, "InstallFolder");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try uninstall registry
        path = ReadRegistryValue(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Rockstar Games Launcher", "InstallLocation");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Default location
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var defaultPath = Path.Combine(programFiles, "Rockstar Games", "Launcher");
        if (Directory.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();

        // Method 1: Check per-game registry keys (most reliable)
        foreach (var kvp in KnownGames)
        {
            if (kvp.Key == "launcher") continue;
            
            var gameName = kvp.Value;
            var gameId = kvp.Key;
            
            var perGamePaths = new[]
            {
                $@"SOFTWARE\Rockstar Games\{gameName}",
                $@"SOFTWARE\WOW6432Node\Rockstar Games\{gameName}",
            };
            
            foreach (var regPath in perGamePaths)
            {
                var installPath = ReadRegistryValue(regPath, "InstallFolder");
                if (string.IsNullOrEmpty(installPath))
                    installPath = ReadRegistryValue(regPath, "InstallPath");
                    
                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                {
                    var game = CreateDetectedGame(gameId, gameName, installPath);
                    if (game != null && !games.Any(g => g.ExternalId == gameId))
                        games.Add(game);
                    break;
                }
            }
        }

        // Method 2: Check uninstall registry for Rockstar games
        var uninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        var subKeys = GetRegistrySubKeyNames(uninstallKey);

        foreach (var subKey in subKeys)
        {
            var keyPath = $@"{uninstallKey}\{subKey}";
            var publisher = ReadRegistryValue(keyPath, "Publisher");
            
            // Only process Rockstar games
            if (string.IsNullOrEmpty(publisher) || !publisher.Contains("Rockstar", StringComparison.OrdinalIgnoreCase))
                continue;

            var displayName = ReadRegistryValue(keyPath, "DisplayName");
            var installLocation = ReadRegistryValue(keyPath, "InstallLocation");

            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(installLocation))
                continue;

            // Skip the launcher itself
            if (displayName.Contains("Launcher", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip if directory doesn't exist
            if (!Directory.Exists(installLocation))
                continue;

            var gameId = NormalizeGameId(displayName);
            var game = CreateDetectedGame(gameId, displayName, installLocation);
            if (game != null)
                games.Add(game);
        }

        return games;
    }

    private List<DetectedGame> GetGamesFromTitlesFolder(string launcherPath)
    {
        var games = new List<DetectedGame>();

        try
        {
            // Rockstar stores game info in Launcher\settings.json or similar
            // Also check ProgramData for launcher data
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var launcherDataPath = Path.Combine(programData, "Rockstar Games", "Launcher");

            if (Directory.Exists(launcherDataPath))
            {
                // Check for installed games manifest
                var settingsPath = Path.Combine(launcherDataPath, "settings_user.dat");
                if (File.Exists(settingsPath))
                {
                    // This is a binary file, but we can scan for known paths
                    var content = File.ReadAllText(settingsPath);
                    
                    foreach (var kvp in KnownGames)
                    {
                        if (kvp.Key == "launcher") continue;
                        
                        // Check if game ID appears in settings
                        if (content.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                            content.Contains(kvp.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            // Try to find install path from registry
                            var installPath = GetGameInstallPath(kvp.Key, kvp.Value);
                            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                            {
                                var game = CreateDetectedGame(kvp.Key, kvp.Value, installPath);
                                if (game != null && !games.Any(g => g.ExternalId == kvp.Key))
                                    games.Add(game);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Folder scanning failed, registry should have found games
        }

        return games;
    }

    private string? GetGameInstallPath(string gameId, string gameName)
    {
        // Try registry uninstall entries
        var uninstallPaths = new[]
        {
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{gameName}",
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{gameName}",
        };

        foreach (var regPath in uninstallPaths)
        {
            var path = ReadRegistryValue(regPath, "InstallLocation");
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return path;
        }

        // Try Rockstar-specific registry
        var rockstarPaths = new[]
        {
            $@"SOFTWARE\WOW6432Node\Rockstar Games\{gameName}",
            $@"SOFTWARE\Rockstar Games\{gameName}",
        };

        foreach (var regPath in rockstarPaths)
        {
            var path = ReadRegistryValue(regPath, "InstallFolder");
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return path;

            path = ReadRegistryValue(regPath, "InstallPath");
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return path;
        }

        return null;
    }

    private static string NormalizeGameId(string displayName)
    {
        // Convert display name to a game ID
        return displayName
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace(":", "")
            .Replace("-", "")
            .Replace("'", "");
    }

    private DetectedGame? CreateDetectedGame(string gameId, string gameName, string installPath)
    {
        try
        {
            if (!IsValidGameInstall(installPath))
                return null;

            var exePath = FindMainExecutable(installPath, gameName);
            
            // Rockstar games should launch via PlayGTAV.exe or similar launcher exe
            // which handles Rockstar Launcher authentication automatically
            var launchCommand = exePath ?? $"rockstar://launch/{gameId}";

            return new DetectedGame
            {
                Name = gameName,
                ExternalId = gameId,
                InstallPath = installPath,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? FindMainExecutable(string installPath, string gameName)
    {
        try
        {
            // Check root directory first
            var rootExes = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly);
            if (rootExes.Length > 0)
            {
                var bestMatch = rootExes
                    .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
                if (bestMatch != null)
                    return bestMatch;
            }

            // Check common subdirectories
            var subDirs = new[] { "bin", "Bin", "x64", "Win64", "Game", "Binaries" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(installPath, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes
                        .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .FirstOrDefault();
                    if (mainExe != null)
                        return mainExe;
                }
            }

            // Check nested subdirectories
            var nestedSubDirs = new[]
            {
                new[] { "Game", "Bin" },
                new[] { "Game", "Bin", "x64" },
                new[] { "Game", "Bin", "Win64" },
                new[] { "Binaries", "Win64" },
                new[] { "Binaries", "Win32" },
                new[] { "bin", "x64" },
            };
            foreach (var nested in nestedSubDirs)
            {
                var subPath = Path.Combine(new[] { installPath }.Concat(nested).ToArray());
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly)
                        .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .ToList();
                    if (subExes.Count > 0)
                        return subExes.First();
                }
            }

            return rootExes.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var utilityPatterns = new[]
        {
            "unins", "crash", "report", "update", "patch",
            "redist", "vcredist", "dxsetup", "dotnet",
            "installer", "setup", "helper", "launcher",
            "socialclub", "subprocess"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }
}
