using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for GOG Galaxy games
/// </summary>
public class GOGGalaxyDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.GOGGalaxy;
    public override string LauncherName => "GOG";

    private const string GOGRegistryKey = @"SOFTWARE\GOG.com\GalaxyClient\paths";
    private const string GOGRegistryKey32 = @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths";
    private const string GOGGamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private const string GOGGamesKey64 = @"SOFTWARE\GOG.com\Games";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetGOGGalaxyPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetGOGGalaxyPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var gogPath = GetGOGGalaxyPath();
        if (string.IsNullOrEmpty(gogPath))
        {
            result.ErrorMessage = "GOG Galaxy installation not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = gogPath;

        // Read games from registry
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.ExternalId == game.ExternalId))
                result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();
        
        // GOG games register under HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\{GameId}
        var registryPaths = new[]
        {
            GOGGamesKey,
            GOGGamesKey64
        };

        // Check HKLM paths
        foreach (var regPath in registryPaths)
        {
            try
            {
                var gameIds = GetRegistrySubKeyNames(regPath);
                foreach (var gameId in gameIds)
                {
                    var game = ParseGameFromRegistry(regPath, gameId);
                    if (game != null && !games.Any(g => g.ExternalId == game.ExternalId))
                        games.Add(game);
                }
            }
            catch
            {
                // Continue if registry access fails
            }
        }
        
        // Check HKCU paths
        foreach (var regPath in registryPaths)
        {
            try
            {
                var gameIds = GetUserRegistrySubKeyNames(regPath);
                foreach (var gameId in gameIds)
                {
                    var game = ParseGameFromUserRegistry(regPath, gameId);
                    if (game != null && !games.Any(g => g.ExternalId == game.ExternalId))
                        games.Add(game);
                }
            }
            catch
            {
                // Continue if registry access fails
            }
        }

        return games;
    }

    private DetectedGame? ParseGameFromRegistry(string basePath, string gameId)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameId}";
            
            // Read game info from registry
            var gameName = ReadRegistryValue(fullPath, "gameName");
            var installDir = ReadRegistryValue(fullPath, "path");
            var exePath = ReadRegistryValue(fullPath, "exe");
            
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(installDir))
                return null;

            if (!Directory.Exists(installDir))
                return null;

            // Validate it's a real game install
            if (!IsValidGameInstall(installDir))
                return null;

            // Build full exe path if relative
            if (!string.IsNullOrEmpty(exePath) && !Path.IsPathRooted(exePath))
                exePath = Path.Combine(installDir, exePath);

            // Fallback to finding exe if not in registry
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                exePath = FindMainExecutable(installDir);

            // Launch command uses GOG Galaxy protocol
            var launchCommand = $"goggalaxy://runGame/{gameId}";

            return new DetectedGame
            {
                Name = CleanGameTitle(gameName),
                ExternalId = gameId,
                InstallPath = installDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private DetectedGame? ParseGameFromUserRegistry(string basePath, string gameId)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameId}";
            
            var gameName = ReadUserRegistryValue(fullPath, "gameName");
            var installDir = ReadUserRegistryValue(fullPath, "path");
            var exePath = ReadUserRegistryValue(fullPath, "exe");
            
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(installDir))
                return null;

            if (!Directory.Exists(installDir))
                return null;

            if (!IsValidGameInstall(installDir))
                return null;

            if (!string.IsNullOrEmpty(exePath) && !Path.IsPathRooted(exePath))
                exePath = Path.Combine(installDir, exePath);

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                exePath = FindMainExecutable(installDir);

            var launchCommand = $"goggalaxy://runGame/{gameId}";

            return new DetectedGame
            {
                Name = CleanGameTitle(gameName),
                ExternalId = gameId,
                InstallPath = installDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? FindMainExecutable(string installDir)
    {
        try
        {
            // Check root directory
            var rootExes = Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly);
            
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
            var subDirs = new[] { "bin", "Bin", "x64", "game", "Game" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(installDir, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
                    if (mainExe != null)
                        return mainExe;
                }
            }

            return rootExes.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private bool IsUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var utilityPatterns = new[]
        {
            "unins", "crash", "report", "update", "patch",
            "redist", "vcredist", "dxsetup", "dotnet",
            "installer", "setup", "gog", "galaxy"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }

    private string? GetGOGGalaxyPath()
    {
        // Try HKLM registry first
        var path = ReadRegistryValue(GOGRegistryKey, "client");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadRegistryValue(GOGRegistryKey32, "client");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try HKCU registry
        path = ReadUserRegistryValue(GOGRegistryKey, "client");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadUserRegistryValue(GOGRegistryKey32, "client");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Common default paths
        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOG.com", "Galaxy Client"),
        };

        foreach (var defaultPath in defaultPaths)
        {
            if (Directory.Exists(defaultPath))
                return defaultPath;
        }

        return null;
    }

    private string CleanGameTitle(string title)
    {
        return title
            .Replace("™", "")
            .Replace("®", "")
            .Replace("  ", " ")
            .Trim();
    }
}
