using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for EA App (formerly Origin) games
/// </summary>
public class EAAppDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.EAApp;
    public override string LauncherName => "EA App";

    private const string EARegistryKey = @"SOFTWARE\Electronic Arts\EA Desktop";
    private const string EARegistryKey32 = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetEAAppPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetEAAppPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var eaPath = GetEAAppPath();
        if (string.IsNullOrEmpty(eaPath))
        {
            result.ErrorMessage = "EA App installation not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = eaPath;

        // Primary: Read game paths from registry (works for any install location)
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.Name == game.Name))
                result.Games.Add(game);
        }

        // Fallback: Scan common folders for games not in registry
        var gameInstallPaths = GetGameInstallPaths();
        foreach (var gamesRoot in gameInstallPaths)
        {
            if (!Directory.Exists(gamesRoot))
                continue;

            try
            {
                var gameFolders = Directory.GetDirectories(gamesRoot);
                foreach (var gameFolder in gameFolders)
                {
                    var game = ParseGameFolder(gameFolder);
                    if (game != null && !result.Games.Any(g => g.Name == game.Name))
                    {
                        result.Games.Add(game);
                    }
                }
            }
            catch
            {
                // Continue to next path if access denied
            }
        }

        return Task.FromResult(result);
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();
        
        // EA games register under HKLM\SOFTWARE\WOW6432Node\EA Games\{GameTitle}
        var registryPaths = new[]
        {
            @"SOFTWARE\WOW6432Node\EA Games",
            @"SOFTWARE\EA Games"
        };

        foreach (var regPath in registryPaths)
        {
            try
            {
                var gameKeys = GetRegistrySubKeyNames(regPath);
                foreach (var gameKey in gameKeys)
                {
                    var game = ParseGameFromRegistry(regPath, gameKey);
                    if (game != null && !games.Any(g => g.Name == game.Name))
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

    private DetectedGame? ParseGameFromRegistry(string basePath, string gameKey)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameKey}";
            
            // Read install directory
            var installDir = ReadRegistryValue(fullPath, "Install Dir");
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                return null;

            // Skip non-game entries
            if (IsSkippableFolder(gameKey))
                return null;

            // Validate it's a real game install
            if (!IsValidGameInstall(installDir))
                return null;

            // Find executable
            var exePath = FindMainExecutable(installDir);
            
            // Get content ID for launch command
            var contentId = GetContentIdForGame(gameKey);

            string? launchCommand = null;
            if (!string.IsNullOrEmpty(contentId))
                launchCommand = $"origin://launchgame/{contentId}";
            else if (!string.IsNullOrEmpty(exePath))
                launchCommand = exePath;

            return new DetectedGame
            {
                Name = CleanGameTitle(gameKey),
                ExternalId = contentId ?? gameKey,
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

    private List<string> GetGameInstallPaths()
    {
        var paths = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        // Standard EA Games folders
        paths.Add(Path.Combine(programFiles, "EA Games"));
        paths.Add(Path.Combine(programFilesX86, "EA Games"));
        
        // Origin legacy paths
        paths.Add(Path.Combine(programFiles, "Origin Games"));
        paths.Add(Path.Combine(programFilesX86, "Origin Games"));
        
        // Electronic Arts paths
        paths.Add(Path.Combine(programFiles, "Electronic Arts"));
        paths.Add(Path.Combine(programFilesX86, "Electronic Arts"));

        // Check all fixed drives for EA Games folders
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            {
                var driveRoot = drive.RootDirectory.FullName;
                paths.Add(Path.Combine(driveRoot, "EA Games"));
                paths.Add(Path.Combine(driveRoot, "Origin Games"));
                paths.Add(Path.Combine(driveRoot, "Games", "EA Games"));
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }

        return paths.Distinct().ToList();
    }

    private DetectedGame? ParseGameFolder(string gameFolder)
    {
        try
        {
            var folderName = Path.GetFileName(gameFolder);
            
            // Skip EA Desktop app folder itself
            if (folderName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase))
                return null;

            // Skip common non-game folders
            if (IsSkippableFolder(folderName))
                return null;

            // Must have at least one executable
            if (!IsValidGameInstall(gameFolder))
                return null;

            // Find the main game executable
            var exePath = FindMainExecutable(gameFolder);
            
            // Get content ID from ProgramData if available
            var contentId = GetContentIdForGame(folderName);
            
            // Build launch command
            string? launchCommand = null;
            if (!string.IsNullOrEmpty(contentId))
            {
                launchCommand = $"origin://launchgame/{contentId}";
            }
            else if (!string.IsNullOrEmpty(exePath))
            {
                launchCommand = exePath;
            }

            return new DetectedGame
            {
                Name = CleanGameTitle(folderName),
                ExternalId = contentId ?? folderName,
                InstallPath = gameFolder,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? FindMainExecutable(string gameFolder)
    {
        try
        {
            var folderName = Path.GetFileName(gameFolder)?.ToLowerInvariant() ?? "";
            
            // First check root directory
            var rootExes = Directory.GetFiles(gameFolder, "*.exe", SearchOption.TopDirectoryOnly);
            
            if (rootExes.Length > 0)
            {
                // Prefer exe matching folder name
                var bestMatch = rootExes
                    .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(folderName))
                    .ThenByDescending(f => !IsUtilityExe(Path.GetFileName(f)))
                    .ThenByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();

                if (bestMatch != null && !IsUtilityExe(Path.GetFileName(bestMatch)))
                    return bestMatch;
            }

            // Check one level deep (common for EA games)
            var subDirs = new[] { "Game", "Bin", "Binaries", "Win64", "x64" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(gameFolder, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
                    if (mainExe != null)
                        return mainExe;
                }
            }

            return rootExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
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
            "installer", "setup", "launcher", "helper",
            "eaanticheat", "easyanticheat"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }

    private bool IsSkippableFolder(string folderName)
    {
        var lower = folderName.ToLowerInvariant();
        var skipPatterns = new[]
        {
            "directx", "redist", "vcredist", "_commonredist",
            "support", "tools", "sdk", "__installer"
        };
        return skipPatterns.Any(p => lower.Contains(p));
    }

    private string? GetContentIdForGame(string gameName)
    {
        try
        {
            // Check ProgramData for content ID
            var installDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EA Desktop", "InstallData", gameName);

            if (Directory.Exists(installDataPath))
            {
                // Content ID is in subfolder name (e.g., "base-Origin.SFT.50.0001545")
                var subFolders = Directory.GetDirectories(installDataPath);
                var contentFolder = subFolders.FirstOrDefault();
                if (contentFolder != null)
                {
                    var contentId = Path.GetFileName(contentFolder);
                    // Strip "base-" prefix if present
                    if (contentId.StartsWith("base-", StringComparison.OrdinalIgnoreCase))
                        contentId = contentId.Substring(5);
                    return contentId;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private string? GetEAAppPath()
    {
        // Try registry first
        var path = ReadRegistryValue(EARegistryKey, "DesktopAppPath", use32BitView: true);
        if (!string.IsNullOrEmpty(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }

        path = ReadRegistryValue(EARegistryKey, "InstallLocation", use32BitView: true);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadRegistryValue(EARegistryKey32, "DesktopAppPath");
        if (!string.IsNullOrEmpty(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }

        // Common default paths
        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Electronic Arts", "EA Desktop", "EA Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Electronic Arts", "EA Desktop", "EA Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Origin"),
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
